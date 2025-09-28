using Serilog.Events;
using SmartLog.Core.Enums;
using StackExchange.Redis;
using System.Globalization;

namespace SmartLog.Core.Helpers;
internal static class HelpersService
{
    /// <summary>
    /// Monta a mensagem para publicação no Redis com o padrão:
    /// "NivelLog:TipoAlteracao:DuracaoEmMinutos"
    /// </summary>
    /// <param name="level">Nível do log (ex: Information, Warning, etc.)</param>
    /// <param name="changeType">Tipo de alteração (Manual ou Automatic)</param>
    /// <param name="duration">Duração que manterá o nível (TimeSpan)</param>
    /// <returns>String formatada para publicação</returns>
    internal static string FormatLogLevelMessage(LogEventLevel level, LogChangeType changeType, int duration) => $"{level}:{changeType}:{DateTime.UtcNow.AddMinutes(duration).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Realiza o parse de uma mensagem de log publicada no Redis.
    /// </summary>
    /// <param name="input"></param>
    /// <param name="level"></param>
    /// <param name="type"></param>
    /// <param name="expiration"></param>
    /// <returns></returns>
    internal static bool TryParseLogMessage(this RedisValue input, out LogEventLevel level, out LogChangeType type, out DateTime expiration)
    {
        level = default;
        type = default;
        expiration = default;

        if (input.IsNullOrEmpty) return false;

        string[] parts = input.ToString().Split(':', 3);
        if (parts.Length != 3) return false;

        if (!Enum.TryParse(parts[0], true, out level) ||
            !Enum.TryParse(parts[1], true, out type))
            return false;

        const string formatoEsperado = "dd/MM/yyyy HH:mm:ss";
        if (!DateTime.TryParseExact(parts[2].Trim(), formatoEsperado, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedExpiration))
            return false;

        expiration = DateTime.SpecifyKind(parsedExpiration, DateTimeKind.Utc);
        return true;
    }

    /// <summary>
    /// Determina se uma mudança de nível de log deve respeitar o tempo mínimo.
    /// REGRA: Só considera tempo quando DIMINUI verbosidade (ex: Information → Error)
    /// LogEventLevel enum: Verbose(0) < Debug(1) < Information(2) < Warning(3) < Error(4) < Fatal(5)
    // Menor número = MAIS verboso
    // Maior número = MENOS verboso
    // Exemplos:
    // Information(2) → Error(4): DIMINUINDO verbosidade → RESPEITA tempo
    // Information(2) → Debug(1): AUMENTANDO verbosidade → SEM tempo (situação crítica)
    // Error(4) → Information(2): AUMENTANDO verbosidade → SEM tempo
    /// </summary>
    /// <param name="currentLevel">Nível atual no Redis</param>
    /// <param name="targetLevel">Nível desejado pela decisão</param>
    /// <returns>True se deve respeitar tempo mínimo</returns>
    internal static bool DoNotShouldRespectMinimumTime(LogEventLevel currentLevel, LogEventLevel levelRecomend) => (int)levelRecomend < (int)currentLevel;

    /// <summary>
    /// Realiza o depara do enum interno
    /// </summary>
    /// <param name="level"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentOutOfRangeException"></exception>
    // Substitua o método GetSerilogLevel pelo novo TryGetSerilogLevel
    internal static bool TryGetSerilogLevel(string level, out LogEventLevel levelResult, out string errorMessage)
    {
        if (Enum.TryParse(level, true, out levelResult))
        {
            errorMessage = null;
            return true;
        }

        errorMessage = $"Log level não suportadado: '{level}'. Valores permitidos: {string.Join(", ", Enum.GetNames(typeof(LogEventLevel)))}";
        return false;
    }
    
    /// <summary>
    /// Retorna os nomes dos valores do enum LogEventLevel.
    /// </summary>
    /// <returns>Array de strings com os nomes dos níveis de log.</returns>
    internal static string[] GetLogEventLevelNames() => Enum.GetNames(typeof(LogEventLevel));
    
}