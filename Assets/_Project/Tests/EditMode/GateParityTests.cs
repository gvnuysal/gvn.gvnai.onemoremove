using System;
using NUnit.Framework;
using OneMoreMove.Core;

namespace OneMoreMove.Tests
{
    /// <summary>
    /// Design analysis, not a rule: every directional move flips all gates and moves the player exactly one cell on a
    /// bipartite grid. With directional moves only and no echo, whether the player finds a gate open from a neighbouring
    /// cell therefore never changes during a level. <see cref="Direction.Wait"/> (rules v2) exists to break that parity;
    /// a blocked echo breaks it too.
    /// </summary>
    public sealed class GateParityTests
    {
        [Test]
        public void DirectionalMovesOnly_WithoutEcho_GateEnterabilityIsFixedForTheWholeLevel()
        {
            var level = TestLevels.Parse(
                ".....",
                ".x.o.",
                "..P..",
                ".o.x.",
                "....G");
            var rules = new RulesEngine(level);
            var random = new Random(1234);

            for (var run = 0; run < 200; run++)
            {
                var state = level.CreateInitialState();
                for (var step = 0; step < 60 && !state.IsWon; step++)
                {
                    for (var gate = 0; gate < level.Gates.Count; gate++)
                    {
                        var cell = level.Gates[gate].Position;
                        var neighbour = (Math.Abs(state.Player.X - cell.X) + Math.Abs(state.Player.Y - cell.Y)) == 1;
                        if (!neighbour) continue;

                        var expectedOpen = PredictOpenFromNeighbour(level, gate);
                        Assert.That(state.IsGateOpen(gate), Is.EqualTo(expectedOpen), $"Gate {cell} seen from {state.Player}");
                    }

                    state = rules.TryMove(state, Directions.All[random.Next(Directions.All.Count)]).NextState;
                }
            }
        }

        [Test]
        public void Wait_FlipsGateEnterability_FromTheSameCell()
        {
            var level = TestLevels.Parse("PxG");
            var rules = new RulesEngine(level);
            var start = level.CreateInitialState();

            Assert.That(rules.TryMove(start, Direction.Right).RejectReason, Is.EqualTo(RejectReason.GateClosed));

            var waited = rules.TryMove(start, Direction.Wait).NextState;
            Assert.That(waited.Player, Is.EqualTo(start.Player));
            Assert.That(rules.TryMove(waited, Direction.Right).Accepted, Is.True, "One wait shifts the gate's phase by one turn.");
        }

        private static bool PredictOpenFromNeighbour(LevelDefinition level, int gate)
        {
            var cell = level.Gates[gate].Position;
            var startParity = (level.PlayerStart.X + level.PlayerStart.Y) & 1;
            var neighbourParity = ((cell.X + cell.Y) & 1) ^ 1;
            var elapsedParity = startParity ^ neighbourParity;
            return level.Gates[gate].InitiallyOpen ^ (elapsedParity == 1);
        }
    }
}
