namespace Engine.Entities;

using Core.Vehicles;

/// <summary>
/// A store that manages a fixed number of EVs.
/// It allows renting and returning EV slots, and provides access to the EVs for modification.
/// The store uses a stack to keep track of available indexes for efficient renting and returning.
/// </summary>
/// <param name="totalAmountOfEvs">Maximum number of EVs that can be rented at once.</param>
public class EVStore(int totalAmountOfEvs)
{
    private readonly EV[] _evs = new EV[totalAmountOfEvs];
    private readonly Stack<uint> _availibleIndexes = new(
        Enumerable.Range(0, totalAmountOfEvs).Select(i => (uint)i)
    );

    /// <summary>
    /// Returns a reference to the EV at the specified index. The caller can modify the EV in-place. Throws if the index is invalid.
    /// Make sure to call this after calling TryRent and using the returned index, otherwise you might be accessing an EV that is
    /// currently rented by another caller or an uninitialized slot.
    /// </summary>
    /// <param name="index">The index of the EV you want.</param>
    /// <returns>Ref to an ev.</returns>
    /// <example>
    /// if (store.TryRent(out uint index))
    /// {
    ///     ref var ev = ref store.GetEV(index);
    ///     ev = new EV(battery: 100, speed: 0); // overwrites the slot in-place
    /// }.
    /// </example>
    public ref EV GetEV(uint index) => ref _evs[index]; // throws if invalid

    /// <summary>
    /// Attempts to rent an EV slot. If successful, the caller can use the returned index to access and modify the EV in-place.
    /// </summary>
    /// <param name="index">The index that has been rented. 0 If unsuccessful.</param>
    /// <returns>Inidcates if the renting was successful.</returns>
    /// <example>
    /// if (store.TryRent(out uint index))
    /// {
    ///     ref var ev = ref store.GetEV(index);
    ///     ev = new EV(battery: 100, speed: 0); // overwrites the slot in-place
    /// }.
    /// </example>
    public bool TryRent(out uint index)
    {
        if (_availibleIndexes.Count == 0)
        {
            index = 0;
            return false;
        }

        index = _availibleIndexes.Pop();
        return true;
    }

    /// <summary>
    /// Attempts to rent multiple EV slots at once.
    /// </summary>
    /// <param name="indexes">A span to be filled with the rented indexes.</param>
    /// <returns>Indicates if the rent was successful.</returns>
    /// <example>
    /// <code><![CDATA[
    /// Span<uint> indexes = stackalloc uint[3];
    /// if (store.TryRentBulk(indexes))
    /// {
    ///     for (int i = 0; i < indexes.Length; i++)
    ///     {
    ///         // Your logic here
    ///     }
    /// }
    /// ]]></code>
    /// </example>
    public bool TryRentBulk(Span<uint> indexes)
    {
        if (_availibleIndexes.Count < indexes.Length)
            return false;

        for (var i = 0; i < indexes.Length; i++)
            indexes[i] = _availibleIndexes.Pop();

        return true;
    }

    /// <summary>
    /// Returns an EV slot back to the store, making it available for renting again.
    /// The caller should ensure that the EV at the specified index is no longer in use before calling this method.
    /// </summary>
    /// <param name="index">The index of the EV slot to return. Must be a valid index that was previously rented and not already returned.</param>
    /// <exception cref="ArgumentOutOfRangeException">Throws if the index is out of bounds.</exception>
    public void Return(uint index)
    {
        if (index >= _evs.Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Invalid EV index.");
        _availibleIndexes.Push(index);
    }
}
