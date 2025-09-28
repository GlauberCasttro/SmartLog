using Serilog.Events;
using SmartLog.Core.Enums;

namespace SmartLog.Core.Interfaces;

/// <summary>
/// Define um serviço responsável por iniciar a troca de nível de log no sistema.
/// </summary>
public interface ILogLevelSwitcherService
{
    /// <summary>
    /// Publica uma solicitação de mudança de nível de log.
    /// </summary>
    /// <param name="newLevel">O novo nível de log a ser aplicado.</param>
    /// <param name="changeType">O tipo da mudança (Automática ou Manual).</param>
    /// <param name="expirationInMinutes">A duração em minutos para a regra. Este valor é sempre obrigatório.</param>
    /// <returns>Task que representa a operação assíncrona.</returns>
    Task SwitchLevelAsync(LogEventLevel newLevel, LogChangeType changeType, int expirationInMinutes);
}