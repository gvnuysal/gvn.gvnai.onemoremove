using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Presentation;
using OneMoreMove.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace OneMoreMove.Tests
{
    public sealed class LocalizationTests
    {
        private const string TurkishLetters = "çğıöşüÇĞİÖŞÜ";

        private string _saveDirectory;

        [SetUp]
        public void SetUp()
        {
            _saveDirectory = Path.Combine(Path.GetTempPath(), "onemoremove-l10n-" + Guid.NewGuid().ToString("N"));
            GameBootstrap.SaveDirectoryOverride = _saveDirectory;
        }

        [TearDown]
        public void TearDown()
        {
            Localization.DeviceLanguageOverride = null;
            Localization.Set(Language.Turkish);
            GameBootstrap.SaveDirectoryOverride = null;
            if (Directory.Exists(_saveDirectory)) Directory.Delete(_saveDirectory, true);
        }

        /// <summary>Every text the game can show, with sample arguments.</summary>
        private static IEnumerable<(string Name, string Text)> AllStrings()
        {
            foreach (var property in typeof(Strings).GetProperties(BindingFlags.Public | BindingFlags.Static).Where(p => p.PropertyType == typeof(string)))
                yield return (property.Name, (string)property.GetValue(null));

            yield return ("Moves", Strings.Moves(3));
            yield return ("UndoAvailable", Strings.UndoAvailable(2));
            yield return ("Stars", Strings.Stars(2));
            yield return ("BestMoves", Strings.BestMoves(7));
            yield return ("MovesResult", Strings.MovesResult(9, 12));
            yield return ("TextScale", Strings.TextScale(125));
            yield return ("Ranking", Strings.Ranking(new RunResult(true, 9, 8, 3, 40, null)));
            foreach (RejectReason reason in Enum.GetValues(typeof(RejectReason))) yield return ("Reject." + reason, Strings.Reject(reason));
            foreach (EchoBlockReason reason in Enum.GetValues(typeof(EchoBlockReason))) yield return ("EchoBlocked." + reason, Strings.EchoBlocked(reason));
            foreach (MechanicTopic topic in Enum.GetValues(typeof(MechanicTopic))) yield return ("Reminder." + topic, Strings.Reminder(topic));
            foreach (var direction in Directions.Commands) yield return ("Direction." + direction, Strings.DirectionName(direction));
            foreach (HintKind kind in Enum.GetValues(typeof(HintKind)))
                yield return ("Hint." + kind, Strings.HintText(new Hint(kind, MechanicTopic.Gates, Direction.Wait, 4, 0)));
            foreach (SaveLoadStatus status in new[] { SaveLoadStatus.RecoveredFromBackup, SaveLoadStatus.CorruptedReset, SaveLoadStatus.UnsupportedVersion })
                yield return ("Load." + status, Strings.LoadStatus(status));
            foreach (ResumeDiscardReason reason in new[] { ResumeDiscardReason.LevelUpdated, ResumeDiscardReason.LevelMissing, ResumeDiscardReason.InvalidSnapshot })
                yield return ("Resume." + reason, Strings.ResumeDiscarded(reason));

            var level = AsciiLevelParser.Parse(".PoG", "E#..");
            yield return ("Board", BoardDescriber.Describe(level, level.CreateInitialState()));
            yield return ("Move", BoardDescriber.DescribeMove(new RulesEngine(level).TryMove(level.CreateInitialState(), Direction.Right)));
        }

        [Test]
        public void EveryText_ExistsInBothLanguages()
        {
            Localization.Set(Language.Turkish);
            var turkish = AllStrings().ToList();
            Localization.Set(Language.English);
            var english = AllStrings().ToList();

            Assert.That(english.Count, Is.EqualTo(turkish.Count));
            foreach (var (name, text) in turkish.Concat(english)) Assert.That(text, Is.Not.Null.And.Not.Empty, name);
            foreach (var (name, text) in english) Assert.That(text.IndexOfAny(TurkishLetters.ToCharArray()), Is.EqualTo(-1), $"{name} is not translated: {text}");
            Assert.That(english.Zip(turkish, (e, t) => e.Text != t.Text).Count(changed => changed), Is.GreaterThan(english.Count * 9 / 10),
                "Almost every text differs between the languages.");
        }

        [Test]
        public void Language_FollowsTheDevice_UnlessChosen()
        {
            Localization.DeviceLanguageOverride = SystemLanguage.Turkish;
            Assert.That(Localization.FromCode(null), Is.EqualTo(Language.Turkish));
            Localization.DeviceLanguageOverride = SystemLanguage.German;
            Assert.That(Localization.FromCode(null), Is.EqualTo(Language.English), "Every other device plays in English.");
            Assert.That(Localization.FromCode("tr"), Is.EqualTo(Language.Turkish), "A saved choice wins over the device.");
        }

        [UnityTest]
        public IEnumerator EnglishDevice_ShowsAnEnglishGame_AndTheSettingSwitchesItLive()
        {
            Localization.DeviceLanguageOverride = SystemLanguage.English;
            yield return SceneManager.LoadSceneAsync("Main", LoadSceneMode.Single);
            yield return null;

            var root = UnityEngine.Object.FindAnyObjectByType<UIDocument>().rootVisualElement;
            Assert.That(root.Q<Button>("continue-button").text, Is.EqualTo("Continue"));

            // No UXML text escaped translation, visible or not (the language picker names Turkish in Turkish on purpose).
            var leaks = new List<string>();
            root.Query<TextElement>().ForEach(t =>
            {
                if (t.text.IndexOfAny(TurkishLetters.ToCharArray()) >= 0 && t.text != "Türkçe") leaks.Add($"{t.name}: {t.text}");
            });
            root.Query<VisualElement>().ForEach(e =>
            {
                var label = (e as BaseField<bool>)?.label ?? (e as BaseField<int>)?.label ?? (e as BaseField<string>)?.label;
                if (label != null && label.IndexOfAny(TurkishLetters.ToCharArray()) >= 0) leaks.Add($"{e.name} label: {label}");
            });
            Assert.That(leaks, Is.Empty);

            // Level names and tips come from the level's English translation.
            Submit(root.Q<Button>("continue-button"));
            yield return null;
            Assert.That(root.Q<Label>("level-name").text, Is.EqualTo("1. First Step"));
            StringAssert.StartsWith("Move your piece", root.Q<Label>("level-tip").text);

            // Switching in the settings changes every screen at once and is saved.
            root.Q<DropdownField>("language").index = (int)Language.Turkish;
            yield return null;
            Assert.That(root.Q<Label>("level-name").text, Is.EqualTo("1. İlk Adım"));
            Assert.That(root.Q<Button>("continue-button").text, Is.EqualTo("Devam Et"));
            Assert.That(UnityEngine.Object.FindAnyObjectByType<GameBootstrap>().Game.Settings.Language, Is.EqualTo("tr"));
        }

        private static void Submit(Button button)
        {
            using (var submit = NavigationSubmitEvent.GetPooled())
            {
                submit.target = button;
                button.SendEvent(submit);
            }
        }
    }
}
