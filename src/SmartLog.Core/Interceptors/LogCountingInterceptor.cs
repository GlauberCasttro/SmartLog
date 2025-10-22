using Serilog.Core;
using Serilog.Events;
using SmartLog.Core.Service;

namespace SmartLog.Core.Interceptors;

/// <summary>
/// Um Sink customizado do Serilog que intercepta todos os eventos de log gerados,
/// antes mesmo do filtro de nível (LoggingLevelSwitch) ser aplicado.
/// Seu único propósito é registrar os eventos no MetricsRegistry para análise de saúde.
/// </summary>
internal class LogCountingInterceptor(MetricsRegistry metricsRegistry) : ILogEventSink
{
    private readonly MetricsRegistry _metricsRegistry = metricsRegistry;

    /// <summary>
    /// Este método é chamado pelo Serilog para cada evento de log.
    /// </summary>
    public void Emit(LogEvent logEvent) => _metricsRegistry.RecordLogEvent(logEvent.Level);
}