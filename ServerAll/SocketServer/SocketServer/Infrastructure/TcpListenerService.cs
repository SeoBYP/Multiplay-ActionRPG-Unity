using Microsoft.Extensions.Hosting;

namespace Server.Infrastructure;

public class TcpListenerService(TcpNetworkListener listener) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        listener.Start();
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        listener.Stop();
        await base.StopAsync(cancellationToken);
    }
}
