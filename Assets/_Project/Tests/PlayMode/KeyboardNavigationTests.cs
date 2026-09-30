using System.Collections;
using System.IO;
using NUnit.Framework;
using OneMoreMove.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OneMoreMove.Tests
{
    /// <summary>
    /// Menus must work without a pointer. Unity's input provider turns arrow keys, the d-pad and Enter into UI Toolkit
    /// navigation events, but only while the application has focus, which a batchmode test run never has. So these tests
    /// send the navigation events themselves and check what this project owns: the focus order and activation.
    /// </summary>
    public sealed class KeyboardNavigationTests
    {
        private string _saveDirectory;

        [SetUp]
        public void SetUp()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(), "onemoremove-keys-" + System.Guid.NewGuid().ToString("N"));
            GameBootstrap.SaveDirectoryOverride = _saveDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            GameBootstrap.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDirectory)) Directory.Delete(_saveDirectory, true);
        }

        [UnityTest]
        public IEnumerator NavigationEvents_DriveTheMenus()
        {
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;

            var root = Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            var focus = root.panel.focusController;
            Assert.That(Focused(focus), Is.EqualTo("continue-button"), "The menu opens with a focused button.");

            yield return Move(focus, NavigationMoveEvent.Direction.Down);
            Assert.That(Focused(focus), Is.EqualTo("levels-button"));
            yield return Move(focus, NavigationMoveEvent.Direction.Down);
            Assert.That(Focused(focus), Is.EqualTo("settings-button"));
            yield return Move(focus, NavigationMoveEvent.Direction.Up);
            Assert.That(Focused(focus), Is.EqualTo("levels-button"));

            yield return Submit(focus);
            Assert.That(root.Q("level-select").resolvedStyle.display, Is.EqualTo(DisplayStyle.Flex), "Submit opens the level list.");
            Assert.That(Focused(focus), Is.EqualTo("level-0"), "The first open level is focused.");

            yield return Move(focus, NavigationMoveEvent.Direction.Right);
            Assert.That(Focused(focus), Is.Not.EqualTo("level-0"), "Arrows move across the level grid.");
        }

        private static string Focused(FocusController focus) => (focus.focusedElement as VisualElement)?.name;

        private static IEnumerator Move(FocusController focus, NavigationMoveEvent.Direction direction)
        {
            var target = (VisualElement)focus.focusedElement;
            using (var e = NavigationMoveEvent.GetPooled(direction))
            {
                e.target = target;
                target.SendEvent(e);
            }

            yield return null;
        }

        private static IEnumerator Submit(FocusController focus)
        {
            var target = (VisualElement)focus.focusedElement;
            using (var e = NavigationSubmitEvent.GetPooled())
            {
                e.target = target;
                target.SendEvent(e);
            }

            yield return null;
            yield return null;
        }
    }
}
