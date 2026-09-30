namespace OneMoreMove.Core
{
    /// <summary>Why the player's move was rejected. A rejected move changes nothing.</summary>
    public enum RejectReason
    {
        None = 0,
        OutOfBounds,
        Wall,
        GateClosed,
        BlockedByEcho,
        LevelAlreadyWon,
        InvalidCommand
    }

    /// <summary>Why the echo stayed in place. A blocked echo never rejects the player's move.</summary>
    public enum EchoBlockReason
    {
        None = 0,
        OutOfBounds,
        Wall,
        GateClosed,
        BlockedByPlayer
    }
}
