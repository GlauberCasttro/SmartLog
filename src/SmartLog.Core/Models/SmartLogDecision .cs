using Serilog.Events;
using System.Diagnostics.CodeAnalysis;

namespace SmartLog.Core.Models;

/// <summary>
/// Encapsula o resultado da decisão tomada pelo detector de anomalias.
/// </summary>
[ExcludeFromCodeCoverage]
public class SmartLogDecision
{
    /// <summary>
    /// Indica se o sistema deve mudar para o nível de alta verbosidade.
    /// </summary>
    public bool ShouldSwitchToHighVerbosity { get; set; }

    /// <summary>
    /// Uma string explicando o motivo da decisão.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// O nível de log recomendado pela decisão.
    /// </summary>
    public LogEventLevel RecommendedLevel { get; set; }

    /// <summary>
    /// O Health Score calculado que levou a esta decisão.
    /// </summary>
    public float HealthScore { get; set; }
}