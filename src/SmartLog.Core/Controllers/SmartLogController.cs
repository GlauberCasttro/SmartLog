using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Serilog.Core;
using SmartLog.Core.Enums;
using SmartLog.Core.Helpers;
using SmartLog.Core.Interfaces;
using SmartLog.Core.Models;
using SmartLog.Core.Service;
using System.Diagnostics.CodeAnalysis;

namespace SmartLog.Core.Controllers;

/// <summary>
/// API para monitorar o estado interno da SDK de Logging Inteligente.
/// </summary>
[Route("api/smart-logs")]
[ExcludeFromCodeCoverage]
[ApiController]
public class SmartLogController(ISmartLogEconomyDetector detector, MetricsRegistry registry, LoggingLevelSwitch levelSwitch, SmartLogOptions options, ILogLevelSwitcherService logLevelSwitcherService) : ControllerBase
{
    private readonly ISmartLogEconomyDetector _detector = detector;
    private readonly MetricsRegistry _registry = registry;
    private readonly LoggingLevelSwitch _levelSwitch = levelSwitch;
    private readonly SmartLogOptions _options = options;
    private readonly ILogLevelSwitcherService _logLevelSwitcherService = logLevelSwitcherService;

    /// <summary>
    /// Retorna o status atual e a última decisão tomada pelo detector.
    /// </summary>
    [HttpGet("status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetStatus()
    {
        var lastDecision = _detector.LastDecision;

        var status = new
        {
            CurrentLogLevel = _levelSwitch.MinimumLevel.ToString(),
            EconomyLevel = _options.EconomyLevel.ToString(),
            HighVerbosityLevel = _options.HighVerbosityLevel.ToString(),
            LastDecision = new
            {
                Timestamp = DateTime.UtcNow,
                lastDecision.RecommendedLevel,
                lastDecision.ShouldSwitchToHighVerbosity,
                lastDecision.Reason,
                lastDecision.HealthScore
            },
            ConfiguracoesGerais = _options
        };

        return Ok(status);
    }

    /// <summary>
    /// Retorna as contagens atuais dos buffers de métricas em memória.
    /// </summary>
    [HttpGet("metrics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetMetrics()
    {
        var healthStats = _registry.GetBufferHealthStats();

        var metrics = new
        {
            // Métricas básicas (backward compatibility)
            RequestMetricsCount = _registry.GetRequestMetricsCount(),
            LogEventsCount = _registry.GetLogEventsSnapshotCount(),
            BufferSizeLimit = _options.CircularBufferSize,
            TotalErrosHttp = _registry.GetMetrics(e => e.IsHttpError).Count(),

            // Novas métricas de saúde
            BufferHealth = new
            {
                healthStats.TotalLogEvents,
                healthStats.TotalRequestMetrics,
                healthStats.EventsInWindow,
                healthStats.ErrorsInWindow,
                healthStats.WarningsInWindow,
                healthStats.BufferUtilization,
                healthStats.IsHealthy,
                healthStats.HealthStatus,
                healthStats.WindowSizeSeconds
            }
        };

        return Ok(metrics);
    }

    /// <summary>
    /// Retorna os nomes dos níveis de log disponíveis no Serilog.
    /// </summary>
    [HttpGet("levels")]   
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public IActionResult GetLogEventLevelNames() => Ok(HelpersService.GetLogEventLevelNames());

    /// <summary>
    /// Altera manualmente o nível de log do sistema por um período especificado.
    /// </summary>
    /// <param name="level">O nível de log a ser aplicado. Valores válidos:
    /// Verbose (Anything and everything you might want to know about a running block of code),
    /// Debug (Internal system events that aren't necessarily observable from the outside),
    /// Information (The lifeblood of operational intelligence - things happen),
    /// Warning (Service is degraded or endangered),
    /// Error (Functionality is unavailable, invariants are broken or data is lost),
    /// Fatal (If you have a pager, it goes off when one of these occurs).
    /// </param>
    /// <param name="timeExpiration">Duração em minutos para manter o novo nível de log antes de reverter automaticamente.</param>
    /// <returns>Retorna 200 OK se a alteração foi processada com sucesso.</returns>
    /// <remarks>
    /// Esta operação permite override manual temporário do sistema de detecção automática.
    /// Após o tempo de expiração, o sistema retornará ao comportamento automático baseado nas métricas.
    /// </remarks>
    /// <example>
    /// PATCH /api/smart-logs/level?level=Debug&timeExpiration=30
    /// Configura o log para nível Debug por 30 minutos.
    /// </example>
    /// <summary>
    /// Altera manualmente o nível de log por tempo determinado.
    /// </summary>
    /// <param name="request">Dados da alteração: Level (Verbose|Debug|Information|Warning|Error|Fatal) e TimeExpiration (minutos).</param>
    /// <returns>202 Accepted quando processado com sucesso.</returns>
    [HttpPost("level")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangeLevel([FromBody] ChangeLogLevelRequest request)
    {
        HelpersService.TryGetSerilogLevel(request.Level, out var serilogLevel, out _);

        await _logLevelSwitcherService.SwitchLevelAsync(serilogLevel, LogChangeType.Manual, request.TimeExpirationInMinute);
        return Accepted();
    }
}