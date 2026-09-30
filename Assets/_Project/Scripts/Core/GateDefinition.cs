namespace OneMoreMove.Core
{
    /// <summary>A gate cell and its state at level start. At most one gate per cell.</summary>
    public readonly struct GateDefinition
    {
        public readonly GridPos Position;
        public readonly bool InitiallyOpen;

        public GateDefinition(GridPos position, bool initiallyOpen)
        {
            Position = position;
            InitiallyOpen = initiallyOpen;
        }

        public override string ToString() => $"Gate{Position}{(InitiallyOpen ? "open" : "closed")}";
    }
}
