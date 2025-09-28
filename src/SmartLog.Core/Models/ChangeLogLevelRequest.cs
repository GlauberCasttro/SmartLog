using SmartLog.Core.Helpers;
using System.ComponentModel.DataAnnotations;

namespace SmartLog.Core.Models;

/// <summary>
/// Model para  requisição de mudança de nível de log.
/// </summary>
public class ChangeLogLevelRequest
{
    /// <summary>
    /// Level, Information, Warning, Error, Debug, Verbose, Fatal
    /// </summary>
    [SerilogLevelValid]
    public string Level { get; set; }

    /// <summary>
    /// Tempo em minutos para expiração do nível de log.
    /// </summary>
    [Range(1, int.MaxValue, ErrorMessage = "TimeExpiration deve ser > 0")]
    public int TimeExpirationInMinute { get; set; }
}