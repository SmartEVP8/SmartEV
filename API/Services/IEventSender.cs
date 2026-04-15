namespace API.Services;

using Protocol;

/// <summary>
/// Defines a contract for sending protocol events to connected clients.
/// </summary>
public interface IEventSender
{
    /// <summary>
    /// Sends a protocol event to the connected client, if any.
    /// </summary>
    /// <param name="envelope">The envelope containing the event data.</param>
    /// <param name="cancelToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation that sends the event.</returns>
    Task SendAsync(Envelope envelope, CancellationToken cancelToken = default);
}
