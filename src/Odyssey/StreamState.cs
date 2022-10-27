namespace Odyssey;

/// <summary>
/// Represents the state of a stream
/// </summary>
public readonly struct StreamState : IEquatable<StreamState>, IComparable<StreamState>, IComparable
{
    public readonly long _value;

    private StreamState(long value)
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
    public static StreamState AtVersion(long version) => new(version);

    /// <summary>
    /// Represents the state where the stream does not exist
    /// </summary>
    public static readonly StreamState NoStream = new(Constants.NoStream);

    /// <summary>
    /// Represents the state where the stream exists at any version
    /// </summary>
    public static readonly StreamState StreamExists = new(Constants.StreamExists);

    /// <summary>
    /// Represents any state of the stream or version
    /// </summary>
    public static readonly StreamState Any = new(Constants.Any);

    /// <inheritdoc />
    public int CompareTo(StreamState other) => _value.CompareTo(other._value);

    /// <inheritdoc />
    public int CompareTo(object? obj) => obj switch
    {
        null => 1,
        StreamState other => CompareTo(other),
        _ => throw new ArgumentException($"Object is not a {nameof(StreamState)}"),
    };

    /// <inheritdoc />
    public bool Equals(StreamState other) => _value == other._value;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is StreamState other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => _value.GetHashCode();

    /// <summary>
    /// Compares left and right for equality.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns>True if left is equal to right.</returns>
    public static bool operator ==(StreamState left, StreamState right) => left.Equals(right);

    /// <summary>
    /// Compares left and right for inequality.
    /// </summary>
    /// <param name="left"></param>
    /// <param name="right"></param>
    /// <returns>True if left is not equal to right.</returns>
    public static bool operator !=(StreamState left, StreamState right) => !left.Equals(right);

    /// <summary>
    /// Converts a <see cref="StreamState"/> to a <see cref="long" />.
    /// </summary>
    /// <param name="ExpectedState"></param>
    /// <returns></returns>
    public static implicit operator long(StreamState expectedState) => expectedState._value;

    /// <inheritdoc />
    public override string ToString() => _value switch
    {
        Constants.NoStream => nameof(NoStream),
        Constants.StreamExists => nameof(StreamExists),
        Constants.Any => nameof(Any),
        _ => _value.ToString()
    };
}