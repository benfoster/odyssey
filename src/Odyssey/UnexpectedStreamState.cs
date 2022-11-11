namespace Odyssey;

/// <summary>
/// Represents an append operation that fails due to the stream being at an unexpected state
/// </summary>
public readonly struct UnexpectedStreamState
{
    public UnexpectedStreamState(StreamState expectedState)
    {
        ExpectedState = expectedState;
    }

    /// <summary>
    /// Gets the expected state of the stream
    /// </summary>
    public StreamState ExpectedState { get; }
}