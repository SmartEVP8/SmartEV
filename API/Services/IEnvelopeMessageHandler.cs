namespace API.Services;

using Protocol;

/// <summary>
/// Processes simulation protocol messages and generates appropriate responses.
/// </summary>
public interface IEnvelopeMessageHandler
{
    Task<Envelope> HandleInitRequestAsync(InitRequest request, CancellationToken cancellationToken);
    Task<Envelope> HandleGetSnapshotRequestAsync(GetSnapshotRequest request, CancellationToken cancellationToken);
}