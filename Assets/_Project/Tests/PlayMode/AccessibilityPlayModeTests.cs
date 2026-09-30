using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Presentation;
using UnityEngine;
using UnityEngine.Accessibility;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OneMoreMove.Tests
{
    public sealed class AccessibilityPlayModeTests
    {
        // The expected texts below are Turkish; the device language of the test machine must not matter.
        [SetUp]
        public void SetUp()
        {
            Localization.DeviceLanguageOverride = SystemLanguage.Turkish;
            Localization.Set(Language.Turkish);
        }

        [TearDown]
        public void TearDown() => Localization.DeviceLanguageOverride = null;

        private static Dictionary<string, Color> ThemeColors()
        {
            var uss = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "UI", "Theme.uss"));
            return Regex.Matches(uss, @"--([a-z-]+):\s*(#[0-9A-Fa-f]{6})")
                .Cast<Match>()
                .ToDictionary(m => m.Groups[1].Value, m => ColorUtility.TryParseHtmlString(m.Groups[2].Value, out var c) ? c : Color.magenta);
        }

        /// <summary>WCAG 2.x contrast ratio.</summary>
        private static float Contrast(Color a, Color b)
        {
            float Channel(float c) => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            float Luminance(Color c) => 0.2126f * Channel(c.r) + 0.7152f * Channel(c.g) + 0.0722f * Channel(c.b);
            var la = Luminance(a);
            var lb = Luminance(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        [TestCase("text", "bg")]
        [TestCase("text", "surface")]
        [TestCase("text", "surface-raised")]
        [TestCase("muted", "bg")]
        [TestCase("muted", "surface")]
        [TestCase("muted", "surface-raised")]
        [TestCase("primary-text", "primary")]
        [TestCase("primary", "bg")]
        [TestCase("primary", "surface-raised")]
        public void UiText_MeetsWcagAA(string foreground, string background)
        {
            var colors = ThemeColors();
            Assert.That(Contrast(colors[foreground], colors[background]), Is.GreaterThanOrEqualTo(4.5f), $"{foreground} on {background}");
        }

        [Test]
        public void FocusRing_IsVisible()
        {
            var colors = ThemeColors();
            Assert.That(Contrast(colors["focus"], colors["bg"]), Is.GreaterThanOrEqualTo(3f));
            Assert.That(Contrast(colors["focus"], colors["surface-raised"]), Is.GreaterThanOrEqualTo(3f));
        }

        [Test]
        public void BoardElements_StandOutFromTheFloor()
        {
            // WCAG 1.4.11: graphics needed to understand the board reach 3:1 against their background.
            foreach (var (name, color) in new[]
                     {
                         ("wall", Palette.Wall), ("goal", Palette.Goal), ("gate", Palette.Gate), ("player", Palette.Primary),
                         ("echo", Palette.Accent), ("reject", Palette.Reject)
                     })
            {
                Assert.That(Contrast(color, Palette.Floor), Is.GreaterThanOrEqualTo(3f), name);
            }

            Assert.That(Contrast(Palette.Wall, Palette.Gate), Is.GreaterThanOrEqualTo(3f), "Walls and gates must not look alike.");
        }

        [Test]
        public void BoardDescription_NamesPiecesAndNeighbours()
        {
            var level = AsciiLevelParser.Parse(".P.x", "E#.G");
            var text = BoardDescriber.Describe(level, level.CreateInitialState());

            StringAssert.Contains("Taşın 2. sütun 1. satırda", text);
            StringAssert.Contains("Hedef 4. sütun 2. satırda", text);
            StringAssert.Contains("Yankı 1. sütun 2. satırda", text);
            StringAssert.Contains("Yukarı: kenar", text);
            StringAssert.Contains("Aşağı: duvar", text);
            StringAssert.Contains("Kapılar: 0 açık, 1 kapalı", text);
        }

        [Test]
        public void MoveAnnouncement_FollowsTheEvents()
        {
            var level = AsciiLevelParser.Parse("PoG");
            var rules = new RulesEngine(level);
            var first = rules.TryMove(level.CreateInitialState(), Direction.Right);

            Assert.That(BoardDescriber.DescribeMove(first), Is.EqualTo("Sağ. Kapılar değişti."));
            Assert.That(BoardDescriber.DescribeMove(rules.TryMove(first.NextState, Direction.Right)), Does.EndWith("Bölüm tamamlandı."));
            Assert.That(BoardDescriber.DescribeMove(rules.TryMove(level.CreateInitialState(), Direction.Left)), Is.Not.Empty);
        }

        [UnityTest]
        public IEnumerator ScreenReader_SeesTheMenuAndTheBoard()
        {
            var saveDirectory = Path.Combine(Path.GetTempPath(), "onemoremove-a11y-" + System.Guid.NewGuid().ToString("N"));
            GameBootstrap.SaveDirectoryOverride = saveDirectory;
            AssistiveSupport.screenReaderStatusOverride = AssistiveSupport.ScreenReaderStatusOverride.ForceEnabled;
            try
            {
                yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
                yield return new WaitForSecondsRealtime(0.5f);

                var menu = Labels();
                Assert.That(menu, Does.Contain("Bir Hamle Daha"));
                Assert.That(menu, Does.Contain("Bölümler"));
                Assert.That(menu, Does.Contain("Ayarlar"));

                // Activate "Devam Et" the way a screen reader does: a navigation submit on the button.
                var continueButton = Object.FindAnyObjectByType<UIDocument>().rootVisualElement.Q<Button>("continue-button");
                using (var submit = NavigationSubmitEvent.GetPooled())
                {
                    submit.target = continueButton;
                    continueButton.SendEvent(submit);
                }

                yield return new WaitForSecondsRealtime(0.5f);

                var hud = Labels();
                Assert.That(hud.Any(l => l.StartsWith("Tahta 5 sütun")), Is.True, string.Join(" | ", hud));
                Assert.That(hud.Any(l => l.StartsWith("Bekle")), Is.True, "The icon-only wait button is read by its tooltip.");
                Assert.That(hud, Does.Not.Contain("Devam Et"), "Hidden screens are not read.");
            }
            finally
            {
                AssistiveSupport.screenReaderStatusOverride = AssistiveSupport.ScreenReaderStatusOverride.OSDriven;
                GameBootstrap.SaveDirectoryOverride = null;
                if (Directory.Exists(saveDirectory)) Directory.Delete(saveDirectory, true);
            }
        }

        private static List<string> Labels() =>
            AssistiveSupport.activeHierarchy?.rootNodes.Select(n => n.label).ToList() ?? new List<string>();
    }
}
