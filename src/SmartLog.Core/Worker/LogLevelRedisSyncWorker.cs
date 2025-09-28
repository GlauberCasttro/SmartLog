using Microsoft.Extensions.Hosting;
using Serilog.Core;
using Serilog.Events;
using SmartLog.Core.Enums;
using SmartLog.Core.Helpers;
using SmartLog.Core.Interfaces;
using SmartLog.Core.Models;
using StackExchange.Redis;
using System.Diagnostics.CodeAnalysis;
using static System.Console;

namespace SmartLog.Core.Worker;

/// <summary>
/// LoggerSyncWorker é responsável por sincronizar o nível de log periodicamente a cada 2 minutos usando PeriodicTimer
/// </summary>
/// <param name="redis"></param>
/// <param name="levelSwitch"></param>
/// <param name="options"></param>
[ExcludeFromCodeCoverage]
public class LogLevelRedisSyncWorker(
    IConnectionMultiplexer redis,
    LoggingLevelSwitch levelSwitch,
    SmartLogOptions options, 
    ISmartLogEconomyDetector smartLogEconomyDetector) : BackgroundService
{
    private readonly IDatabase _database = redis.GetDatabase();
    private readonly ISmartLogEconomyDetector _smartLogEconomyDetector = smartLogEconomyDetector;
    private readonly LoggingLevelSwitch _levelSwitch = levelSwitch;
    private readonly SmartLogOptions _options = options;

    /// <summary>
    /// Execução do serviço em segundo plano que roda a cada 2 minutos para sincronizar o nível de log.
    /// Usa PeriodicTimer para máxima performance e eficiência de recursos.
    /// </summary>
    /// <param name="stoppingToken"></param>
    /// <returns></returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(_options.LoadWorkerSincronizedInMinute));
        await SyncLogLevelFromRedis().ConfigureAwait(false);

        WriteLine($"[INFO]: Continuous monitoring enabled - checking every 5 minutes. Log recomendado atual: {_smartLogEconomyDetector.LastDecision.RecommendedLevel}");

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            try
            {
                await SyncLogLevelFromRedis().ConfigureAwait(false);
            }
            catch (RedisException ex)
            {
                WriteLine($"[ERROR]: Redis error: {ex.Message}. Retrying next tick.");
            }
            catch (Exception ex)
            {
                WriteLine($"[ERROR]: Unexpected error: {ex.Message}. Retrying next tick.");
            }
        }
    }

    /// <summary>
    /// Sincroniza o nível de log a partir do Redis
    /// </summary>
    /// <returns></returns>
    private async Task SyncLogLevelFromRedis()
    {
        try
        {
            var keyRedis = GlobalConfig.GetRedisKeyChannel(_options.AppName);
            var redisValue = await _database.StringGetAsync(keyRedis);
            if (!redisValue.HasValue) return;

            if (!redisValue.TryParseLogMessage(out var levelRedis, out var type, out var expiration))
            {
                WriteLine("[WARN]: Valor de log no Redis inválido.");
                return;
            }

            await ApplyLogLevel(_levelSwitch, levelRedis, type, expiration);
        }
        catch (Exception ex)
        {
            WriteLine($"[ERROR]: Falha ao sincronizar nível de log: {ex.Message}");
        }
    }

    /// <summary>
    /// ApplyLogLevel
    /// </summary>
    /// <param name="switcher"></param>
    /// <param name="levelRedis"></param>
    /// <param name="type"></param>
    /// <param name="expiration"></param>
    /// <returns></returns>
    private static Task ApplyLogLevel(LoggingLevelSwitch switcher, LogEventLevel level, LogChangeType type, DateTime expiration)
    {
        var currentTime = DateTime.UtcNow;
        if (currentTime > expiration) return Task.CompletedTask;

        if (switcher.MinimumLevel != level)
        {
            var previousLevel = switcher.MinimumLevel;
            switcher.MinimumLevel = level;
            WriteLine($"[INFO]: Log level changed: {previousLevel} → {level} (Type: {type})");
        }

        return Task.CompletedTask;
    }
}