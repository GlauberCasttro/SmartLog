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
/// SmartLogEconomyDetector
/// </summary>
[ExcludeFromCodeCoverage]
internal class SmartLogEconomyDetector : ISmartLogEconomyDetector
{
    private readonly SmartLogOptions _options; // Configurações gerais da SDK
    private readonly MetricsRegistry _metricsRegistry; // Métricas registradas pela aplicação
    private readonly LoggingLevelSwitch _loggingLevelSwitch; // Switch de nível do Serilog
    private readonly IDatabase _redisDatabase; // Instância Redis para leitura/escrita
    private readonly Timer _detectionTimer; // Timer que dispara ciclos periódicos de detecção
    private int _isDetectionRunning = 0; // NÃO-STATIC: Flag de concorrência por instância
    public SmartLogDecision LastDecision { get; private set; } // Última decisão tomada pelo algoritmo de detecção
    private readonly ILogLevelSwitcherService _logLevelSwitcherService;

    public SmartLogEconomyDetector(
        SmartLogOptions options,
        MetricsRegistry registry,
        LoggingLevelSwitch levelSwitch,
        IConnectionMultiplexer redis,
        ILogLevelSwitcherService logLevelSwitcherService,
       bool enableAutoDetection = true)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options), "SmartLogOptions cannot be null.");
        _metricsRegistry = registry ?? throw new ArgumentNullException(nameof(registry), "MetricsRegistry cannot be null.");
        _loggingLevelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch), "LoggingLevelSwitch cannot be null.");
        _redisDatabase = redis?.GetDatabase() ?? throw new ArgumentNullException(nameof(redis), "IConnectionMultiplexer cannot be null.");
        _logLevelSwitcherService = logLevelSwitcherService ?? throw new ArgumentNullException(nameof(logLevelSwitcherService), "ILogLevelSwitcherService cannot be null.");

        LastDecision = new SmartLogDecision
        {
            RecommendedLevel = _options.EconomyLevel,
            Reason = "SDK Initialized.",
            HealthScore = 0,
            ShouldSwitchToHighVerbosity = false
        };

        if (enableAutoDetection) _detectionTimer = new Timer(RunDetectionCycleCallback, null, TimeSpan.Zero, _options.DetectionInterval);
    }

    /// <summary>
    /// Timer callback thread-safe com proteção contra falhas críticas.
    /// Executa detecção de anomalias de forma assíncrona sem bloquear o timer.
    /// </summary>
    private void RunDetectionCycleCallback(object? state)
    {
        Task.Run(async () =>
        {
            try
            {
                await RunDetectionCycleAsync();
            }
            catch (Exception ex)
            {
                // ✅ Log crítico para debugging - NÃO reset redundante de lock
                // O finally do RunDetectionCycleAsync já garante a liberação do lock
                WriteLine($"[CRITICAL] SmartLogEconomyDetector timer callback failed: {ex}");
            }
        });
    }

    /// <summary>
    /// Executa um ciclo de detecção de anomalias e ajusta o nível de log se necessário.
    /// INTERNAL para segurança - apenas testes podem acessar via InternalsVisibleTo
    /// </summary>
    /// <returns></returns>
    internal async Task RunDetectionCycleAsync()
    {
        if (Interlocked.CompareExchange(ref _isDetectionRunning, 1, 0) != 0) return;

        try
        {
            LastDecision = MakeSimpleDecisionByErrors();

            if (_loggingLevelSwitch.MinimumLevel == LastDecision.RecommendedLevel) return;

            // Se mudança não precisa respeitar tempo mínimo, executa imediatamente
            if (HelpersService.DoNotShouldRespectMinimumTime(_loggingLevelSwitch.MinimumLevel, LastDecision.RecommendedLevel))
            {
                await SwitchLogLevelAsync(LastDecision.RecommendedLevel);
                return;
            }

            // Para mudanças críticas, verifica tempo mínimo
            if (await CanSwitchAfterMinimumTimeAsync())
            {
                await SwitchLogLevelAsync(LastDecision.RecommendedLevel);
                return;
            }
        }
        catch (Exception ex)
        {
            WriteLine($"[ERROR] Failed to run detection cycle: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _isDetectionRunning, 0);
        }
    }

    /// <summary>
    /// Toma uma decisão simples baseada na contagem de erros recentes.
    /// </summary>
    /// <returns></returns>
    private SmartLogDecision MakeSimpleDecisionByErrors()
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var (errorCount, _, _) = CountRecentLogEvents(currentTime, _options.LogWindowSeconds);

        return errorCount >= _options.AbsoluteErrorThreshold
            ? new SmartLogDecision
            {
                ShouldSwitchToHighVerbosity = true,
                Reason = $"Error count threshold exceeded. Errors: {errorCount}, Threshold: {_options.AbsoluteErrorThreshold}.",
                RecommendedLevel = _options.HighVerbosityLevel,
                HealthScore = errorCount
            } : new SmartLogDecision
            {
                ShouldSwitchToHighVerbosity = false,
                Reason = $"Error count within acceptable range. Errors: {errorCount}, Threshold: {_options.AbsoluteErrorThreshold}.",
                RecommendedLevel = _options.EconomyLevel,
                HealthScore = errorCount
            };
    }

    /// <summary>
    /// Publica uma evento no channel do Redis para alterar o nível de log.
    /// </summary>
    /// <param name="newLevel"></param>
    /// <returns></returns>
    private async Task SwitchLogLevelAsync(LogEventLevel newLevel) => await _logLevelSwitcherService.SwitchLevelAsync(newLevel, LogChangeType.Automatico, _options.MinimumHighVerbosityDurationInMinute);

    /// <summary>
    /// Conta a quantidade de eventos de log recentes dentro de uma janela de tempo especificada.
    /// </summary>
    /// <param name="currentTime"></param>
    /// <param name="windowSeconds"></param>
    /// <returns></returns>
    private (int ErrorCount, int WarningCount, int TotalLogs) CountRecentLogEvents(long currentTime, int windowSeconds)
    {
        int errorCount = 0, warningCount = 0, totalLogs = 0;
        var cutoffTime = currentTime - windowSeconds;

        _metricsRegistry.ProcessLogEvents(events =>
        {
            foreach (var (Timestamp, Level) in events)
            {
                // Otimizado: calcula cutoffTime uma vez e compara diretamente
                if (Timestamp < cutoffTime) continue;
                totalLogs++;

                if (Level >= LogEventLevel.Error) errorCount++;
                else if (Level == LogEventLevel.Warning) warningCount++;
            }
        });

        return (errorCount, warningCount, totalLogs);
    }

    /// <summary>
    /// Verifica se pode fazer a mudança após verificar o tempo mínimo necessário.
    /// </summary>
    /// <returns>True se pode mudar, False se deve aguardar</returns>
    private async Task<bool> CanSwitchAfterMinimumTimeAsync()
    {
        var key = GlobalConfig.GetRedisSwitchTimestampKey(_options.AppName);
        var lastSwitchTimestamp = await _redisDatabase.StringGetAsync(key);

        // Se não há timestamp anterior, pode mudar imediatamente
        if (!lastSwitchTimestamp.HasValue) return true;

        // Verifica se o tempo mínimo já passou
        var lastSwitchTime = DateTime.Parse(lastSwitchTimestamp!, CultureInfo.InvariantCulture).ToUniversalTime();
        var minimumWaitUntil = lastSwitchTime.Add(TimeSpan.FromMinutes(_options.MinimumHighVerbosityDurationInMinute));

        return DateTime.UtcNow >= minimumWaitUntil;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _detectionTimer?.Change(Timeout.Infinite, 0);
            _detectionTimer?.Dispose();
        }
    }
}