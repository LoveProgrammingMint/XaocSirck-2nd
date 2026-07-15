using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace XaocSirck2Service;

public interface IUpdateTrigger
{
    Task WaitAsync(CancellationToken cancellationToken = default);
    void Trigger();
}

public sealed class UpdateTrigger : IUpdateTrigger
{
    private readonly Channel<bool> _channel = Channel.CreateUnbounded<bool>();

    public Task WaitAsync(CancellationToken cancellationToken = default)
        => _channel.Reader.ReadAsync(cancellationToken).AsTask();

    public void Trigger()
        => _channel.Writer.TryWrite(true);
}
