using Serilog.Events;
using SmartLog.Core.Models;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace SmartLog.Core.Service;

/// <summary>
/// Repositório central e thread-safe para métricas de saúde da aplicação.
/// Usa buffers circulares (ConcurrentQueue com tamanho limitado) para evitar consumo excessivo de memória.
/// </summary>
[ExcludeFromCodeCoverage]
public class MetricsRegistry(SmartLogOptions options)
{
    private readonly ConcurrentQueue<(long Timestamp, int LatencyMs, bool IsHttpError)> _requestMetrics = new();
    private readonly ConcurrentQueue<(long Timestamp, LogEventLevel Level)> _logEvents = new();

    /// <summary>
    /// Registra uma métrica de uma requisição HTTP.
    /// OPÇÃO 1: LRU com Time Window - Garante limite de buffer + preserva histórico para decisões (Usando ainda somente para metricas)
    /// </summary>
    public void AddRequestMetric(int latencyMs, bool isHttpError)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // SEMPRE aceita a nova métrica
        _requestMetrics.Enqueue((timestamp, latencyMs, isHttpError));

        // PRIMEIRO: Mantém buffer no limite (evita memory leak)
        while (_requestMetrics.Count > options.CircularBufferSize)
        {
            _requestMetrics.TryDequeue(out _);
        }

        // SEGUNDO: Remove métricas fora da janela (preserva histórico relevante)
        CleanRequestMetricsOutsideWindow(timestamp);
    }

    /// <summary>
    /// Remove métricas de requisição que estão fora da janela de tempo configurada.
    /// </summary>
    /// <param name="currentTimestamp">Timestamp atual em segundos Unix</param>
    private void CleanRequestMetricsOutsideWindow(long currentTimestamp)
    {
        var cutoffTime = currentTimestamp - options.LogWindowSeconds;

        while (_requestMetrics.TryPeek(out var oldest) && oldest.Timestamp < cutoffTime)
        {
            _requestMetrics.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Registra um evento de log emitido pela aplicação.
    /// ESTRATÉGIA A: Time Window Priority - Prioriza janela temporal, LRU como fallback
    /// </summary>
    public void RecordLogEvent(LogEventLevel level)
    {
        if (level < LogEventLevel.Warning) return;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // PRIMEIRO: Remove eventos fora da janela (critério temporal)
        CleanEventsOutsideWindow(timestamp);

        // DEPOIS: Aceita o novo evento
        _logEvents.Enqueue((timestamp, level));

        // SE AINDA ESTIVER CHEIO: Remove os mais antigos (LRU como fallback)
        while (_logEvents.Count > options.CircularBufferSize)
        {
            _logEvents.TryDequeue(out _);
        }
    }

    /// <summary>
    /// Remove eventos que estão fora da janela de tempo configurada.
    /// Isso preserva o histórico relevante para tomada de decisões.
    /// </summary>
    /// <param name="currentTimestamp">Timestamp atual em segundos Unix</param>
    /// <returns>Quantidade de eventos removidos por estarem fora da janela temporal</returns>
    private void CleanEventsOutsideWindow(long currentTimestamp)
    {
        var cutoffTime = currentTimestamp - options.LogWindowSeconds;

        while (_logEvents.TryPeek(out var oldest) && oldest.Timestamp < cutoffTime)
        {
            _logEvents.TryDequeue(out _);
        }
    }

    public int GetRequestMetricsCount() => _requestMetrics.Count; // O(1), thread-safe

    public int GetLogEventsSnapshotCount() => _logEvents.Count; // O(1), thread-safe

    /// <summary>
    /// Obtém estatísticas detalhadas do buffer para observabilidade
    /// </summary>
    public BufferHealthStats GetBufferHealthStats()
    {
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var stats = new BufferHealthStats
        {
            TotalLogEvents = _logEvents.Count,
            TotalRequestMetrics = _requestMetrics.Count,
            BufferSizeLimit = options.CircularBufferSize,
            WindowSizeSeconds = options.LogWindowSeconds
        };

        // Calcula eventos dentro da janela
        ProcessLogEvents(events =>
        {
            foreach (var (timestamp, level) in events)
            {
                if (currentTime - timestamp <= options.LogWindowSeconds)
                {
                    stats.EventsInWindow++;
                    if (level >= LogEventLevel.Error) stats.ErrorsInWindow++;
                    else if (level == LogEventLevel.Warning) stats.WarningsInWindow++;
                }
            }
        });

        stats.BufferUtilization = stats.TotalLogEvents / (double)options.CircularBufferSize;
        return stats;
    }

    /// <summary>
    /// Metodo para processar evento externo ara evitar criar uma nova lista para calculo externo
    /// </summary>
    /// <param name="processor"></param>
    public void ProcessLogEvents(Action<ConcurrentQueue<(long Timestamp, LogEventLevel Level)>> processor) => processor(_logEvents);

    /// <summary>
    /// GetMetrics permite filtrar as métricas de requisição com base em um predicado.
    /// </summary>
    /// <param name="predicate"></param>
    /// <returns></returns>
    public IEnumerable<(long Timestamp, int LatencyMs, bool IsHttpError)> GetMetrics(Func<(long Timestamp, int LatencyMs, bool IsHttpError), bool> predicate) => _requestMetrics.Where(predicate);
}