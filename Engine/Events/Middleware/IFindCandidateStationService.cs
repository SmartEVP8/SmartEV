using Engine.Events;

public interface IFindCandidateStationService
{
    Task<Dictionary<ushort, float>> GetCandidateStationFromCache(int evId);

    Action<IMiddlewareEvent> PreComputeCandidateStation();
}
