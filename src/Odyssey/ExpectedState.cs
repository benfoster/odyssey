namespace Odyssey;

/// <summary>
/// Represents the expected state of a stream
/// </summary>
public readonly struct ExpectedState : IEquatable<ExpectedState>, IComparable<ExpectedState>, IComparable
{
    public readonly long _value;

    private ExpectedState(long value)
    {
        _value = value;
    }


    private static class Constants
    {
        public const int NoStream = -1;
        public const int StreamExists = -2;
        public const int Any = -3;
    }

    /// <summary>
    /// Represents the state where the stream exists at a specific version
    /// </summary>
    /// <param name="version">The expected version of the stream</param>
    public static ExpectedState AtVersion(long version) => new(version);

    /// <summary>
    /// Represents the state where the stream does not exist
    /// </summary>
    public static readonly ExpectedState NoStream = new(Constants.NoStream);

    /// <summary>
    /// Represents the state where the stream exists at any version
    /// </summary>
    public static readonly ExpectedState StreamExists = new(Constants.StreamExists);

    /// <summary>
    /// Represents any state of the stream or version
    /// </summary>
    public static readonly ExpectedState Any = new(Constants.Any);

    /// <inheritdoc />
    public int CompareTo(ExpectedState other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        ExpectedState other => CompareTo(other),
        _ => throw new ArgumentException($"Object is not a {nameof(ExpectedState)}"),
    };

    /// <inheritdoc />
    public bool Equals(ExpectedState other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ExpectedState other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Compares left and right for equality.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns>True if left is equal to right.</returns>
    public static bool operator ==(ExpectedState left, ExpectedState right) => left.Equals(right);

    /// <summary>
    /// Compares left and right for inequality.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns>True if left is not equal to right.</returns>
    public static bool operator !=(ExpectedState left, ExpectedState right) => !left.Equals(right);

    /// <summary>
    /// Converts a <see cref="ExpectedState"/> to a <see cref="long" />.
    /// </summary>
    /// <param name="ExpectedState"></param>
    /// <returns></returns>
    public static implicit operator long(ExpectedState expectedState) => expectedState._value;

    /// <inheritdoc />
    public override string ToString() => _value switch
    {
        Constants.NoStream => nameof(NoStream),
        Constants.StreamExists => nameof(StreamExists),
        Constants.Any => nameof(Any),
        _ => _value.ToString()
    };
}