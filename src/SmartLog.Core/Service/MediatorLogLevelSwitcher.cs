using Serilog.Events;
using SmartLog.Core.Enums;
using SmartLog.Core.Helpers;
using SmartLog.Core.Interfaces;
using SmartLog.Core.Models;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;

namespace SmartLog.Core.Service;

/// <summary>
///  Implementa a lógica para publicar eventos de mudança de nível de log no Redis.
/// Este serviço não possui conhecimento sobre regras de negócio como durações padrão.
/// </summary>
/// <param name="redisConnection"></param>
/// <param name="options"></param>
/// <param name="loggingLevelSwitch"></param>
[ExcludeFromCodeCoverage]
internal class MediatorLogLevelSwitcher(IConnectionMultiplexer redisConnection, SmartLogOptions options) : ILogLevelSwitcherService
{
    private readonly ISubscriber _redisSubscriber = redisConnection.GetSubscriber();
    private readonly SmartLogOptions _options = options ?? throw new ArgumentNullException(nameof(options), "SmartLogOptions cannot be null.");

    /// <summary>
    /// Publica o evento no channel do redis
    /// </summary>
    /// <param name="levelOptions"></param>
    /// <param name="changeType"></param>
    /// <param name="expirationInMinutes"></param>
    /// <returns></returns>
    public async Task SwitchLevelAsync(LogEventLevel newLevel, LogChangeType changeType, int expirationInMinutes)
    {
        // 1. Formata a mensagem com os parâmetros recebidos diretamente.
        var formattedMessage = HelpersService.FormatLogLevelMessage(newLevel, changeType, expirationInMinutes);

        //Se opção de persistência para todos as pods estiver desabilitada, apenas publica a mensagem.
        if (options.EnableRedisChannelListener)
        {
            var channel = GlobalConfig.GetRedisKeyChannel(_options.AppName);

            // 4. Publica a mensagem no canal do Redis.
            await _redisSubscriber.PublishAsync(RedisChannel.Literal(channel), formattedMessage);
            return;
        }

        //Se não quer ler o canal, criar apenas no redis para o worker processar
        Console.WriteLine($"[DEBUG] Redis channel listener is disabled. Skipping publish to Redis channel but updating location: {formattedMessage}");
    }
}