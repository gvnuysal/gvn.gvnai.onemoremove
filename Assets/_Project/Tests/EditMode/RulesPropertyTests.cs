using System;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Session;

namespace OneMoreMove.Tests
{
    /// <summary>Seeded random command sequences: invariants, Move+Undo symmetry and determinism.</summary>
    public sealed class RulesPropertyTests
    {
        private static readonly LevelDefinition[] Levels =
        {
            TestLevels.GateTutorial(),
            TestLevels.EchoExample(),
            TestLevels.EchoExampleWithWall(),
            TestLevels.Parse(".x.o.", "#.E.#", "o.P.x", ".#.#.", "x...G")
        };

        [Test]
        public void RandomPlay_KeepsPiecesOnBoardAndApart_AndUndoIsExact([Values(1, 2, 3, 4, 5)] int seed)
        {
            foreach (var level in Levels)
            {
                var random = new Random(seed);
                var session = new GameSession(level);
                for (var step = 0; step < 300; step++)
                {
                    if (session.IsWon) session.Restart();

                    var before = session.State;
                    var result = session.TryMove(Directions.Commands[random.Next(Directions.Commands.Count)]);
                    AssertInvariants(level, session.State);

                    if (result.Accepted && random.NextDouble() < 0.3)
                    {
                        if (!session.IsWon)
                        {
                            Assert.That(session.Undo(), Is.True);
                            Assert.That(session.State, Is.EqualTo(before), "Move + Undo must restore the exact state.");
                        }
                    }
                    else if (!result.Accepted)
                    {
                        Assert.That(session.State, Is.SameAs(before));
                    }
                }
            }
        }

        [Test]
        public void SameStartAndCommands_ProduceTheSameFinalState([Values(7, 8, 9)] int seed)
        {
            foreach (var level in Levels)
            {
                var random = new Random(seed);
                var commands = new Direction[80];
                for (var i = 0; i < commands.Length; i++) commands[i] = Directions.Commands[random.Next(Directions.Commands.Count)];

                var a = Play(level, commands);
                var b = Play(level, commands);
                Assert.That(a, Is.EqualTo(b));
            }
        }

        private static BoardState Play(LevelDefinition level, Direction[] commands)
        {
            var rules = new RulesEngine(level);
            var state = level.CreateInitialState();
            foreach (var command in commands) state = rules.TryMove(state, command).NextState;
            return state;
        }

        private static void AssertInvariants(LevelDefinition level, BoardState state)
        {
            Assert.That(level.InBounds(state.Player), Is.True);
            Assert.That(level.IsWall(state.Player), Is.False);
            if (state.Echo.HasValue)
            {
                Assert.That(level.InBounds(state.Echo.Value), Is.True);
                Assert.That(level.IsWall(state.Echo.Value), Is.False);
                Assert.That(state.Echo.Value, Is.Not.EqualTo(state.Player));
            }
        }
    }
}
