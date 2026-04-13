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

    /// <summary>
    /// Attempts to allocate a specified number of EV instances, initializing them using the provided EVInitializer delegate.
    /// </summary>
    /// <param name="index">The index of the EV to initialize.</param>
    /// <param name="ev">The EV instance to initialize.</param>
    public delegate void EVInitializer(int index, ref EV ev);

    /// <summary>
    /// Tries to allocate a specified number of EV instances, initializing them using the provided EVInitializer delegate.
    /// </summary>
    /// <param name="amount">The number of EV instances to allocate.</param>
    /// <param name="initialize">The delegate used to initialize each allocated EV instance.</param>
    /// <param name="allocatedIndexes">A span to receive the indexes of the allocated EV instances.</param>
    /// <returns>True if the allocation was successful, false otherwise.</returns>
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
    /// Tries to allocate a specified number of EV instances, initializing them using the provided EVInitializer delegate.
    /// </summary>
    /// <param name="initialize">The delegate used to initialize the allocated EV instance.</param>
    /// <param name="allocatedIndex">The allocated index. -1 if allocation fails.</param>
    /// <returns>True if the allocation was successful, false otherwise.</returns>
    public bool TryAllocate(EVInitializer initialize, out int allocatedIndex)
    {
        allocatedIndex = -1;
        if (_freeIndexes.Count < 1)
            return false;

        var index = _freeIndexes.Pop();
        initialize(index, ref _evs[index]);
        allocatedIndex = index;
        return true;
    }

    /// <summary>
    /// Tries to allocate a specified number of EV instances, returning their indexes for parallel initialization by the caller.
    /// </summary>
    /// <param name="amount">The number of EV instances to allocate.</param>
    /// <param name="allocatedIndexes">A span to receive the indexes of the allocated EV instances.</param>
    /// <returns>True if the allocation was successful, false otherwise.</returns>
    public bool TryAllocateParallel(int amount, int[] allocatedIndexes)
    {
        if (_freeIndexes.Count < amount)
            return false;

        for (var i = 0; i < amount; i++)
            allocatedIndexes[i] = _freeIndexes.Pop();

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
    public void Free(int index)
    {
        _evs[index] = default;
        _freeIndexes.Push(index);
    }

    /// <summary>Sets the EV at the specified index to the provided EV instance.</summary>
    /// <param name="index">The index to update.</param>
    /// <param name="ev">The reference that will be set at <paramref name="index"/>.</param>
    public void Set(int index, ref EV ev) => _evs[index] = ev;

    /// <summary>Returns a reference to the EV at the specified index, allowing for direct modification.</summary>
    /// <param name="index">The index of the EV ti retrueve.</param>
    /// <returns>A reference to the EV at the specified index.</returns>
    public ref EV Get(int index) => ref _evs[index];

    /// <summary>
    /// Gets all currently allocated EV indexes.
    /// </summary>
    /// <returns>An enumerable of allocated EV indexes.</returns>
    public IEnumerable<int> GetAllocatedIndexes()
    {
        var free = new HashSet<int>(_freeIndexes);
        for (var i = 0; i < totalCapacity; i++)
        {
            if (!free.Contains(i))
                yield return i;
        }
    }

    /// <summary>
    /// Gets the total capacity of the EVStore, which is the maximum number of EV instances it can hold.
    /// </summary>
    public int Count => totalCapacity;

    /// <summary>
    /// Gets the total number of EVs currently in the simulation.
    /// </summary>
    /// <returns>The count of allocated EVs in the store.</returns>
    public uint GetTotalEVsInSimulation() => (uint)(totalCapacity - _freeIndexes.Count);

    /// <summary>
    /// Gets the total number of EVs currently charging.
    /// </summary>
    /// <returns>The count of EVs actively charging.</returns>
    public uint GetChargingEVCount()
    {
        var count = 0u;
        for (var i = 0; i < totalCapacity; i++)
        {
            if (_evs[i].EVState == State.Charging)
                count++;
        }

        return count;
    }
}
