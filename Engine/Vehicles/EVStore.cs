namespace Engine.Vehicles;

using Core.Vehicles;

/// <summary>
/// Manages the storage and allocation of EV instances in a contiguous block of memory.
/// </summary>
/// <param name="totalCapacity">The maximum capacity of evs.</param>
public class EVStore(int totalCapacity)
{
    private readonly Stack<int> _freeIndexes = new(Enumerable.Range(0, totalCapacity));
    private readonly EV[] _evs = new EV[totalCapacity];

    public delegate void EVInitializer(int index, ref EV ev);
    public bool TryAllocate(int amount, EVInitializer initialize, Span<int> allocatedIndexes = default)
    {
        if (_freeIndexes.Count < amount)
            return false;
        for (var i = 0; i < amount; i++)
        {
            var index = _freeIndexes.Pop();
            initialize(index, ref _evs[index]);
            if (!allocatedIndexes.IsEmpty)
                allocatedIndexes[i] = index;
        }
        return true;
    }

    /// <summary>
    /// Returns the number of free indexes currently available for allocation.
    /// </summary>
    /// <returns>The Available capacity.</returns>
    public int AvailableCapacity() => _freeIndexes.Count;

    /// <summary>
    /// Puts the <paramref name="index"/> back in the pool of free indexes.
    /// </summary>
    /// <param name="index">Index to be put back in the pool of free indexes.</param>
    public void Free(int index) => _freeIndexes.Push(index);

    /// <summary>Sets the EV at the specified index to the provided EV instance.</summary>
    /// <param name="index">The index to update.</param>
    /// <param name="ev">The reference that will be set at <paramref name="index"/>.</param>
    public void Set(int index, ref EV ev) => _evs[index] = ev;

    /// <summary>Returns a reference to the EV at the specified index, allowing for direct modification.</summary>
    /// <param name="index">The index of the EV ti retrueve.</param>
    /// <returns>A reference to the EV at the specified index.</returns>
    public ref EV Get(int index) => ref _evs[index];

    public int Count => totalCapacity;
}
