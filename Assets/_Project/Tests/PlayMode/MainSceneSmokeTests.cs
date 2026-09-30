using System.Collections;
using System.IO;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OneMoreMove.Tests
{
    /// <summary>Boots the real Main scene: composition root, UXML bindings, catalog and a first move.</summary>
    public sealed class MainSceneSmokeTests
    {
        private string _saveDirectory;

        [SetUp]
        public void SetUp()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(), "onemoremove-smoke-" + System.Guid.NewGuid().ToString("N"));
            GameBootstrap.SaveDirectoryOverride = _saveDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            GameBootstrap.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDirectory)) Directory.Delete(_saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator MainScene_Boots_ShowsTheMenu_AndPlaysTheFirstLevel()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;

            var bootstrap = Object.FindAnyObjectByType<GameBootstrap>();
            Assert.That(bootstrap, Is.Not.Null);
            Assert.That(bootstrap.Game, Is.Not.Null, "Bootstrap did not start.");
            var seedLevels = Directory.GetFiles(Path.Combine(Application.dataPath, "_Project", "Content", "LevelSource"), "*.json").Length;
            Assert.That(bootstrap.Game.Library.Count, Is.EqualTo(seedLevels), "The catalog must ship every seed level.");

            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q("main-menu").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex));

            bootstrap.Gameplay.Open(0);
            Assert.That(bootstrap.Gameplay.TryMove(Direction.Right), Is.True);
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(bootstrap.Game.Current.State.MoveCount, Is.EqualTo(1));

            bootstrap.Gameplay.Suspend();
            Assert.That(File.Exists(Path.Combine(_saveDirectory, "save.json")), Is.True, "Suspend writes the save.");
        }
    }
}
