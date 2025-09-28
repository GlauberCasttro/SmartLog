using SmartLog.Core.Models;

namespace SmartLog.Core.Interfaces;

/// <summary>
/// Interface para SmartLogEconomyDetector para permitir mocking em testes
/// Não expõe RunDetectionCycleAsync por questões de segurança
/// </summary>
public interface ISmartLogEconomyDetector : IDisposable
{
    /// <summary>
    /// Última decisão tomada pelo algoritmo de detecção
    /// </summary>
    SmartLogDecision LastDecision { get; }
}