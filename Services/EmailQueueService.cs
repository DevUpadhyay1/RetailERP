using System.Threading.Channels;

namespace RetailERP.Services;

/// <summary>
/// Sprint 9: In-memory email queue backed by a Channel.
/// Producers enqueue emails; the EmailSenderWorker drains the channel in the background.
/// </summary>
public sealed class EmailQueueService
{
    private readonly Channel<QueuedEmail> _channel =
        Channel.CreateBounded<QueuedEmail>(new BoundedChannelOptions(500)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelReader<QueuedEmail> Reader => _channel.Reader;

    public async ValueTask EnqueueAsync(string to, string subject, string htmlBody,
                                        CancellationToken ct = default)
    {
        await _channel.Writer.WriteAsync(new QueuedEmail(to, subject, htmlBody), ct);
    }
}

public sealed record QueuedEmail(string To, string Subject, string HtmlBody);
