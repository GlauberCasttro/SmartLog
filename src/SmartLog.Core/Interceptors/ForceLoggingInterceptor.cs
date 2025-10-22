using Serilog.Core;
using Serilog.Events;

namespace SmartLog.Core.Interceptors;

/// <summary>
/// Interceptor para processar logs com força independente do nível de verbosidade
/// </summary>
internal class ForceLoggingInterceptor(LoggingLevelSwitch levelSwitch, IEnumerable<string> specialPropertyNames) : ILogEventFilter
{
    private readonly LoggingLevelSwitch _levelSwitch = levelSwitch ?? throw new ArgumentNullException(nameof(levelSwitch));
    private readonly HashSet<string> _specialPropertyNames = [.. specialPropertyNames ?? ["force"]];

    public bool IsEnabled(LogEvent logEvent)
    {
        // Se qualquer propriedade especial for true, permite o log
        if (HasForceProperty(logEvent)) return true;

        // Caso contrário, respeita o nível configurado
        return logEvent.Level >= _levelSwitch.MinimumLevel;
    }

    /// <summary>
    /// Verifica se o log tem alguma propriedade especial igual a true
    /// </summary>
    private bool HasForceProperty(LogEvent logEvent) => _specialPropertyNames.Any(prop =>
                logEvent.Properties.TryGetValue(prop, out var value)
                && value is ScalarValue { Value: true });
}