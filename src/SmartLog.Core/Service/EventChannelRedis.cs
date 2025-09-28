using Serilog.Core;
using Serilog.Events;
using SmartLog.Core.Enums;
using SmartLog.Core.Helpers;
using SmartLog.Core.Interfaces;
using SmartLog.Core.Models;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using static System.Console;

namespace SmartLog.Core.Service;

/// <summary>
/// Configuração do consumer do canal do redis
/// </summary>
/// <param name="redisConnection"></param>
/// <param name="levelSwitch"></param>
/// <param name="options"></param>
[ExcludeFromCodeCoverage]
internal class EventChannelRedis(IConnectionMultiplexer redisConnection, LoggingLevelSwitch levelSwitch, SmartLogOptions options) : IEventChannelRedis
{
    private readonly ISubscriber _subscriber = redisConnection.GetSubscriber();
    private readonly IDatabase _database = redisConnection.GetDatabase();

    /// <summary>
    /// Consumidor do canal Redis para receber mensagens de alteração de nível de log.
    /// </summary>
    /// <returns></returns>
    public async Task ConsumerAsyn(CancellationToken cancellationToken)
    {
        try
        {
              var channel = RedisChannel.Literal(GlobalConfig.GetRedisKeyChannel(options.AppName));
              await _subscriber.SubscribeAsync(channel, async (_, redisValue) =>
              {
                    // Usando a extensão direto no RedisValue
                    if (!redisValue.TryParseLogMessage(out var incomingLevel, out var incomingType, out var _)) return;

                    //Valor atual do Redis
                    var currentValue = await _database.StringGetAsync(GlobalConfig.GetRedisKeyChannel(options.AppName));

                    if (!currentValue.HasValue)
                    {
                        await ApplyLogLevel(levelSwitch, incomingLevel, incomingType, redisValue);
                        return;
                    }

                    //lógica de verificação de tipo e expiração
                    if (!currentValue.TryParseLogMessage(out LogEventLevel _, out var currentType, out var currentExpiration)) return;

                    if (CanApplyChange(
                        incomingType,
                        currentType,
                        currentExpiration)) 
                    {
                        await ApplyLogLevel(levelSwitch, incomingLevel, incomingType, redisValue);
                    }
                    else if (incomingType == LogChangeType.Automatico && currentType == LogChangeType.Manual)
                    {
                        WriteLine("[INFO]: Log manual ainda não expirado. Ignorando alteração automática.");
                    }
              });

            cancellationToken.Register(() =>
            {
                _subscriber.Unsubscribe(channel);
                WriteLine("[INFO]: Redis channel consumer unsubscribed due to cancellation.");
            });
        }
        catch (Exception ex)
        {
            WriteLine($"[ERROR]: Falha ao consumir canal Redis: {ex}, Maquina: {Environment.MachineName}");
        }

        await Task.CompletedTask;
    }

    /// <summary>
    ///Metodo para verificar se a mudança de nível de log pode ser aplicada.
    /// </summary>
    /// <param name="incomingType"></param>
    /// <param name="currentType"></param>
    /// <param name="currentExpiration"></param>
    /// <returns></returns>
    private static bool CanApplyChange(
        LogChangeType incomingType,
        LogChangeType currentType,
        DateTime currentExpiration)
    {
        if (incomingType == LogChangeType.Manual) return true;
        if (incomingType == LogChangeType.Automatico && currentType == LogChangeType.Automatico) return true;
        if (incomingType == LogChangeType.Automatico && currentType == LogChangeType.Manual) return DateTime.UtcNow >= currentExpiration;

        return false;
    }

    /// <summary>
    /// Aplica o novo nível de log e atualiza o Redis com a mensagem de log.
    /// </summary>
    /// <param name="levelSwitch"></param>
    /// <param name="newLevel"></param>
    /// <param name="changeLevelType"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task ApplyLogLevel(LoggingLevelSwitch levelSwitch, LogEventLevel newLevel, LogChangeType changeLevelType, string message)
    {
        levelSwitch.MinimumLevel = newLevel;
        await SetValueMessageTime(message);

        await UpdateValueSwitchTime(changeLevelType);
        WriteLine($"[INFO]: Level alterado no POD {Environment.MachineName} para novo level {newLevel} de forma {changeLevelType}.");
    }

    /// <summary>
    /// Atualiza o valor no Redis com a mensagem formatada e define um tempo de expiração.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    private async Task SetValueMessageTime(string message)
    {
        var keyRedis = GlobalConfig.GetRedisKeyChannel(options.AppName);
        await _database.StringSetAsync(keyRedis, message, expiry: TimeSpan.FromDays(7));
    }

    /// <summary>
    /// Adiciona apenas quando o processo é automtico para garantir o tempo minimo que deve permanecer no level
    /// </summary>
    /// <param name="changeLevelType"></param>
    /// <returns></returns>
    private async Task UpdateValueSwitchTime(LogChangeType changeLevelType)
    {
        var keySwitchTime = GlobalConfig.GetRedisSwitchTimestampKey(options.AppName);
        if (changeLevelType == LogChangeType.Automatico)
        {
            //Talvez aqui, a expiração seja no tempo do _options.MinimumHighVerbosityDurationInMinute que é o tempo minimo que deve manter quando alteração for automatica, ou seja, o tempo que essa flag vai expirar no redis. Avaliar
            await _database.StringSetAsync(keySwitchTime, DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture), expiry: TimeSpan.FromDays(7));
            return;
        }

        await _database.KeyDeleteAsync(keySwitchTime);
    }
}