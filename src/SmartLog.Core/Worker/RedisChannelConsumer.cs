using Microsoft.Extensions.Hosting;
using SmartLog.Core.Interfaces;
using System.Diagnostics.CodeAnalysis;
using static System.Console;

namespace SmartLog.Core.Worker;

/// <summary>
/// Worker para iniciar o consumer do redis
/// </summary>
/// <param name="eventChannelRedis"></param>
[ExcludeFromCodeCoverage]
public class RedisChannelConsumer(IEventChannelRedis eventChannelRedis) : IHostedService
{
    private readonly IEventChannelRedis _eventChannelRedis = eventChannelRedis;

    /// <summary>
    /// StartAsync
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        WriteLine("[INFO]: Starting Redis channel consumer...");
        await RunConsumerAsync(cancellationToken);
    }

    /// <summary>
    /// Starta o consumer
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task RunConsumerAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _eventChannelRedis.ConsumerAsyn(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            WriteLine($"[ERROR]: Redis consumer failed: {ex.Message}");
        }
    }

    /// <summary>
    /// StopAsync
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        WriteLine("[INFO]: Stopping  Redis channel consumer...");
        return Task.CompletedTask;
    }
}