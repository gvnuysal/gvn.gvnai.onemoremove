using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Persistence;
using OneMoreMove.Session;

namespace OneMoreMove.Tests
{
    public sealed class PersistenceTests
    {
        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "onemoremove-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
        }

        private static SaveData SampleData(int moves = 1)
        {
            var level = TestLevels.EchoExample();
            var session = new GameSession(level);
            for (var i = 0; i < moves && !session.IsWon; i++) session.TryMove(Direction.Right);
            session.TakeHint();
            session.AddActiveTime(42.25);

            var progress = new[] { new ProgressRecord("tutorial_gate_01", 1, true, 4, 3, new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc)) };
            var settings = new GameSettings { ReducedMotion = true, ShowMovePreview = true, TextScalePercent = 125 };
            return new SaveData(RulesEngine.Version, 3, session.ToSnapshot(), progress, settings, level.Id);
        }

        [Test]
        public void RoundTrip_RestoresEveryField()
        {
            var store = new FileSaveStore(_directory);
            var data = SampleData(moves: 2);
            store.Save(data);

            var loaded = new FileSaveStore(_directory).Load();

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Loaded));
            var session = loaded.Data.ActiveSession;
            Assert.That(session.LevelId, Is.EqualTo(data.ActiveSession.LevelId));
            Assert.That(session.LevelRevision, Is.EqualTo(data.ActiveSession.LevelRevision));
            Assert.That(session.State, Is.EqualTo(data.ActiveSession.State));
            Assert.That(session.UndoStates, Is.EqualTo(data.ActiveSession.UndoStates));
            Assert.That(session.HintUsed, Is.True);
            Assert.That(session.Attempts, Is.EqualTo(data.ActiveSession.Attempts));
            Assert.That(session.ActiveSeconds, Is.EqualTo(42.25));

            var progress = loaded.Data.Progress.Single();
            Assert.That(progress.BestMoves, Is.EqualTo(4));
            Assert.That(progress.BestStars, Is.EqualTo(3));
            Assert.That(progress.LastPlayedUtc, Is.EqualTo(new DateTime(2026, 9, 29, 10, 0, 0, DateTimeKind.Utc)));
            Assert.That(loaded.Data.Settings.TextScalePercent, Is.EqualTo(125));
            Assert.That(loaded.Data.Settings.ReducedMotion, Is.True);
            Assert.That(loaded.Data.ContentVersion, Is.EqualTo(3));
            Assert.That(loaded.Data.LastPlayedLevelId, Is.EqualTo("echo_example"));
        }

        [Test]
        public void NoFiles_IsNoSave()
        {
            Assert.That(new FileSaveStore(_directory).Load().Status, Is.EqualTo(SaveLoadStatus.NoSave));
        }

        [Test]
        public void SecondWrite_KeepsThePreviousGoodFileAsBackup()
        {
            var store = new FileSaveStore(_directory);
            store.Save(SampleData(1));
            store.Save(SampleData(2));

            Assert.That(File.Exists(store.BackupPath), Is.True);
            Assert.That(File.Exists(store.TempPath), Is.False);
        }

        [Test]
        public void CorruptedMain_OpensTheBackup_AndNeverRotatesTheCorruptFileIntoTheBackup()
        {
            var store = new FileSaveStore(_directory);
            store.Save(SampleData(1));
            store.Save(SampleData(2));
            File.WriteAllText(store.MainPath, "{ not json");

            var reopened = new FileSaveStore(_directory);
            var loaded = reopened.Load();
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.RecoveredFromBackup));
            Assert.That(loaded.Data.ActiveSession.State.MoveCount, Is.EqualTo(1));

            reopened.Save(SampleData(3));
            Assert.That(new FileSaveStore(_directory).Load().Data.ActiveSession.State.MoveCount, Is.EqualTo(3));
            Assert.That(File.ReadAllText(reopened.BackupPath), Does.Not.Contain("not json"));
        }

        [Test]
        public void ChecksumMismatch_IsTreatedAsCorruption()
        {
            var store = new FileSaveStore(_directory);
            store.Save(SampleData(1));
            var tampered = File.ReadAllText(store.MainPath).Replace("\"attempts\": 1", "\"attempts\": 7");
            Assert.That(tampered, Does.Contain("\"attempts\": 7"));
            File.WriteAllText(store.MainPath, tampered);

            Assert.That(new FileSaveStore(_directory).Load().Status, Is.EqualTo(SaveLoadStatus.CorruptedReset));
        }

        [Test]
        public void BothFilesCorrupted_ResetsAndKeepsADiagnosticCopy()
        {
            var store = new FileSaveStore(_directory);
            store.Save(SampleData(1));
            store.Save(SampleData(2));
            File.WriteAllText(store.MainPath, "garbage");
            File.WriteAllText(store.BackupPath, "garbage");

            var reopened = new FileSaveStore(_directory);
            Assert.That(reopened.Load().Status, Is.EqualTo(SaveLoadStatus.CorruptedReset));
            Assert.That(File.Exists(reopened.CorruptPath), Is.True);

            reopened.Save(SampleData(1));
            Assert.That(new FileSaveStore(_directory).Load().Status, Is.EqualTo(SaveLoadStatus.Loaded));
        }

        [Test]
        public void FutureSchema_IsNotOverwritten()
        {
            var store = new FileSaveStore(_directory);
            store.Save(SampleData(1));
            var future = JObject.Parse(File.ReadAllText(store.MainPath));
            future["schemaVersion"] = SaveSerializer.CurrentSchemaVersion + 1;
            File.WriteAllText(store.MainPath, future.ToString());
            var original = File.ReadAllText(store.MainPath);

            var reopened = new FileSaveStore(_directory);
            Assert.That(reopened.Load().Status, Is.EqualTo(SaveLoadStatus.UnsupportedVersion));
            reopened.Save(SampleData(2));

            Assert.That(File.ReadAllText(store.MainPath), Is.EqualTo(original));
        }

        [Test]
        public void Migrations_RunInSequence()
        {
            // Write as schema 1, then read with a serializer whose current schema is 2.
            new FileSaveStore(_directory).Save(SampleData(1));
            var migrator = new SaveMigrator(2, new ISaveMigration[] { new RenameLastPlayed() });
            var loaded = new FileSaveStore(_directory, new SaveSerializer(migrator)).Load();

            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Loaded));
            Assert.That(loaded.Data.LastPlayedLevelId, Is.EqualTo("migrated"));
        }

        [Test]
        public void MissingMigrationStep_IsInvalid()
        {
            new FileSaveStore(_directory).Save(SampleData(1));
            var loaded = new FileSaveStore(_directory, new SaveSerializer(new SaveMigrator(3))).Load();
            Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.CorruptedReset));
        }

        [Test]
        public void QueuedStore_WritesTheNewestDocument_InOrder()
        {
            var store = new QueuedSaveStore(new FileSaveStore(_directory), e => Assert.Fail(e.ToString()));
            for (var i = 1; i <= 5; i++) store.Save(SampleData(i));
            store.Flush();

            var loaded = new FileSaveStore(_directory).Load();
            Assert.That(loaded.Data.ActiveSession.State.MoveCount, Is.EqualTo(3), "EchoExample is won after 3 moves; the newest saved state has 3.");
        }

        [Test]
        public void QueuedStore_Flush_NeverReturnsBeforeTheNewestWriteLands()
        {
            // Regression: the worker could take the newest document just before Flush looked at the queue, and
            // Flush returned before that write reached the disk (lost save on quit).
            for (var run = 0; run < 200; run++)
            {
                var directory = Path.Combine(_directory, "run" + run);
                var store = new QueuedSaveStore(new FileSaveStore(directory), e => Assert.Fail(e.ToString()));
                for (var i = 1; i <= 5; i++) store.Save(SampleData(i));
                store.Flush();

                var loaded = new FileSaveStore(directory).Load();
                Assert.That(loaded.Status, Is.EqualTo(SaveLoadStatus.Loaded), $"run {run}");
                Assert.That(loaded.Data.ActiveSession.State.MoveCount, Is.EqualTo(3), $"run {run}");
            }
        }

        [Test]
        public void LevelJson_ParsesTheDocumentExample()
        {
            const string json = "{ \"id\": \"tutorial_gate_01\", \"revision\": 1, \"rulesVersion\": 1, \"width\": 5, \"height\": 1, \"walls\": [], " +
                                "\"gates\": [{ \"x\": 2, \"y\": 0, \"open\": false }], \"player\": { \"x\": 0, \"y\": 0 }, \"echo\": null, " +
                                "\"goal\": { \"x\": 4, \"y\": 0 }, \"parMoves\": 5, \"optimalMoves\": 4, \"solution\": [\"Right\", \"Right\", \"Right\", \"Right\"] }";
            var level = LevelJson.Parse(json);

            Assert.That(level.Width, Is.EqualTo(5));
            Assert.That(level.Gates.Single().InitiallyOpen, Is.False);
            Assert.That(level.OptimalMoves, Is.EqualTo(4));
            Assert.That(SolutionReplayer.Replay(level, level.KnownSolution).ReachedGoal, Is.True);

            var roundTrip = LevelJson.Parse(LevelJson.ToJson(level));
            Assert.That(AsciiLevelParser.Render(roundTrip), Is.EqualTo(AsciiLevelParser.Render(level)));
        }

        [Test]
        public void LevelJson_RoundTripsAWaitInTheSolution()
        {
            const string json = "{ \"id\": \"wait_01\", \"revision\": 1, \"rulesVersion\": 2, \"width\": 3, \"height\": 1, \"walls\": [], " +
                                "\"gates\": [{ \"x\": 1, \"y\": 0, \"open\": false }], \"player\": { \"x\": 0, \"y\": 0 }, \"echo\": null, " +
                                "\"goal\": { \"x\": 2, \"y\": 0 }, \"parMoves\": 4, \"optimalMoves\": 3, \"solution\": [\"Wait\", \"Right\", \"Right\"] }";
            var level = LevelJson.Parse(json);

            Assert.That(level.KnownSolution, Is.EqualTo(new[] { Direction.Wait, Direction.Right, Direction.Right }));
            Assert.That(SolutionReplayer.Replay(level, level.KnownSolution).ReachedGoal, Is.True);
            Assert.That(LevelJson.Parse(LevelJson.ToJson(level)).KnownSolution, Is.EqualTo(level.KnownSolution));
        }

        [Test]
        public void LevelJson_CarriesTheLevelTip()
        {
            var level = LevelJson.Parse("{ \"id\": \"tip_01\", \"parMoves\": 5, \"tip\": \" Bekle. \", \"map\": [\"P..xG\"] }");

            Assert.That(level.Tip, Is.EqualTo("Bekle."));
            Assert.That(LevelJson.Parse(LevelJson.ToJson(level)).Tip, Is.EqualTo("Bekle."));
            Assert.That(LevelJson.Parse("{ \"id\": \"no_tip\", \"parMoves\": 5, \"map\": [\"PG\"] }").Tip, Is.Null);
        }

        [Test]
        public void LevelJson_AcceptsAMap()
        {
            var level = LevelJson.Parse("{ \"id\": \"m\", \"map\": [\"P.x\", \"E#G\"], \"parMoves\": 3 }");
            Assert.That(AsciiLevelParser.Render(level), Is.EqualTo(new[] { "P.x", "E#G" }));
        }

        private sealed class RenameLastPlayed : ISaveMigration
        {
            public int FromVersion => 1;
            public void Apply(JObject document) => document["lastPlayedLevelId"] = "migrated";
        }
    }
}
