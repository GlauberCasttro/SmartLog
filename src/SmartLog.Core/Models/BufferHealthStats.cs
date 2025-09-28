using System.Diagnostics.CodeAnalysis;

namespace SmartLog.Core.Models;

/// <summary>
/// Estatísticas de saúde do buffer para observabilidade
/// </summary>
[ExcludeFromCodeCoverage]
public class BufferHealthStats
{
    /// <summary>
    /// Total de eventos de log registrados no buffer.
    /// </summary>
    public int TotalLogEvents { get; set; }

    /// <summary>
    /// Total de métricas de requisição registradas no buffer.
    /// </summary>
    public int TotalRequestMetrics { get; set; }

    /// <summary>
    /// Número de eventos presentes na janela de observação atual.
    /// </summary>
    public int EventsInWindow { get; set; }

    /// <summary>
    /// Número de erros registrados na janela de observação atual.
    /// </summary>
    public int ErrorsInWindow { get; set; }

    /// <summary>
    /// Número de avisos registrados na janela de observação atual.
    /// </summary>
    public int WarningsInWindow { get; set; }

    /// <summary>
    /// Limite máximo de tamanho do buffer.
    /// </summary>
    public int BufferSizeLimit { get; set; }

    /// <summary>
    /// Tamanho da janela de observação em segundos.
    /// </summary>
    public int WindowSizeSeconds { get; set; }

    /// <summary>
    /// Percentual de utilização do buffer (0.0 a 1.0).
    /// </summary>
    public double BufferUtilization { get; set; }

    /// <summary>
    /// Indica se o buffer está saudável (utilização abaixo de 80%).
    /// </summary>
    public bool IsHealthy => BufferUtilization < 0.8; // 80% de utilização como limite

    /// <summary>
    /// Status textual de saúde do buffer ("Healthy" ou "Warning").
    /// </summary>
    public string HealthStatus => IsHealthy ? "Healthy" : "Warning";

    /// <summary>
    /// Total de erros HTTP registrados no buffer.
    /// </summary>
    public int TotalErrosHttp { get; set; }
}