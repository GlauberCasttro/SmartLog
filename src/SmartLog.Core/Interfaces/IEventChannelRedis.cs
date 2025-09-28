namespace SmartLog.Core.Interfaces;

/// <summary>
/// Controlo de eventos do Redis para o SmartLog.
/// </summary>
public interface IEventChannelRedis
{
    /// <summary>
    /// Consumr do canal Redis que escuta as mensagens publicadas.
    /// </summary>
    /// <returns></returns>
    Task ConsumerAsyn(CancellationToken cancellationToken);
}