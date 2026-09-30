namespace OneMoreMove.Core
{
    public enum MoveEventType
    {
        PlayerMoved,
        PlayerWaited,
        EchoMoved,
        EchoBlocked,
        GatesToggled,
        LevelWon
    }

    /// <summary>A fact produced by an accepted move, in the order the rules applied it. Presentation replays these.</summary>
    public readonly struct MoveEvent
    {
        public readonly MoveEventType Type;
        public readonly GridPos From;
        public readonly GridPos To;
        public readonly EchoBlockReason EchoBlockReason;
        public readonly ulong GateBitsBefore;
        public readonly ulong GateBitsAfter;

        private MoveEvent(MoveEventType type, GridPos from, GridPos to, EchoBlockReason echoBlockReason, ulong gateBitsBefore, ulong gateBitsAfter)
        {
            Type = type;
            From = from;
            To = to;
            EchoBlockReason = echoBlockReason;
            GateBitsBefore = gateBitsBefore;
            GateBitsAfter = gateBitsAfter;
        }

        public static MoveEvent PlayerMoved(GridPos from, GridPos to) => new MoveEvent(MoveEventType.PlayerMoved, from, to, EchoBlockReason.None, 0, 0);
        public static MoveEvent PlayerWaited(GridPos at) => new MoveEvent(MoveEventType.PlayerWaited, at, at, EchoBlockReason.None, 0, 0);
        public static MoveEvent EchoMoved(GridPos from, GridPos to) => new MoveEvent(MoveEventType.EchoMoved, from, to, EchoBlockReason.None, 0, 0);
        public static MoveEvent EchoBlocked(GridPos at, EchoBlockReason reason) => new MoveEvent(MoveEventType.EchoBlocked, at, at, reason, 0, 0);
        public static MoveEvent GatesToggled(ulong before, ulong after) => new MoveEvent(MoveEventType.GatesToggled, default, default, EchoBlockReason.None, before, after);
        public static MoveEvent LevelWon(GridPos at) => new MoveEvent(MoveEventType.LevelWon, at, at, EchoBlockReason.None, 0, 0);

        public override string ToString()
        {
            switch (Type)
            {
                case MoveEventType.EchoBlocked: return $"EchoBlocked{From}:{EchoBlockReason}";
                case MoveEventType.GatesToggled: return $"GatesToggled 0x{GateBitsBefore:X}->0x{GateBitsAfter:X}";
                case MoveEventType.LevelWon: return "LevelWon";
                case MoveEventType.PlayerWaited: return $"PlayerWaited{From}";
                default: return $"{Type}{From}->{To}";
            }
        }
    }
}
