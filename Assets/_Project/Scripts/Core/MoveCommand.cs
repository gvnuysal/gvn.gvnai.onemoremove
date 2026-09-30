namespace OneMoreMove.Core
{
    /// <summary>A player command: one step in a direction, or <see cref="Direction.Wait"/> (rules version 2+).</summary>
    public readonly struct MoveCommand
    {
        public readonly Direction Direction;

        public MoveCommand(Direction direction)
        {
            Direction = direction;
        }

        public static implicit operator MoveCommand(Direction direction) => new MoveCommand(direction);

        public override string ToString() => Direction.ToString();
    }
}
