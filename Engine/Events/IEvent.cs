namespace Engine.Events;

public interface IEvent
{
    bool HasBeenCancelled { get; set; }
}
