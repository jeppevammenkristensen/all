namespace JRK.JevRunner;

/// <summary>
/// Represents either a single item or a sequence of items.
/// </summary>
/// <typeparam name="T">The type of item represented by the union.</typeparam>
public union OneOrMore<T>(T, IEnumerable<T>)
{
    /// <summary>
    /// Wraps the supplied sequence without copying it or validating its item count.
    /// </summary>
    public static OneOrMore<T> FromEnumerable(IEnumerable<T> list)
    {
        return list;
    }
    
    /// <summary>
    /// Returns the stored sequence, or a single-item sequence when the union holds one item.
    /// </summary>
    public IEnumerable<T> ToEnumerable() => Value switch
    {
        T t => [t],
        IEnumerable<T> e => e,
        _ => throw new NotImplementedException()
    };
}
