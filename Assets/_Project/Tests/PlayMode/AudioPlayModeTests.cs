using System.Linq;
using NUnit.Framework;
using OneMoreMove.Core;
using OneMoreMove.Presentation.Audio;
using UnityEngine;

namespace OneMoreMove.Tests
{
    public sealed class AudioPlayModeTests
    {
        [Test]
        public void Cues_FollowTheMoveEvents()
        {
            var level = AsciiLevelParser.Parse("PoG");
            var rules = new RulesEngine(level);
            var start = level.CreateInitialState();

            var step = rules.TryMove(start, Direction.Right);
            Assert.That(Names(SoundCues.For(step)), Is.EqualTo(new[] { Sound.Step, Sound.Gate }));

            var win = rules.TryMove(step.NextState, Direction.Right);
            Assert.That(Names(SoundCues.For(win)), Is.EqualTo(new[] { Sound.Step, Sound.Gate, Sound.Win }));
            Assert.That(SoundCues.For(win).Last().Delay, Is.GreaterThan(0f), "The win sound follows the step.");

            Assert.That(Names(SoundCues.For(rules.TryMove(start, Direction.Wait))), Is.EqualTo(new[] { Sound.Wait, Sound.Gate }));
            Assert.That(Names(SoundCues.For(rules.TryMove(start, Direction.Left))), Is.EqualTo(new[] { Sound.Reject }));
        }

        [Test]
        public void EchoBlocks_AreAudible()
        {
            var level = AsciiLevelParser.Parse(".P..G", "...E#");
            var result = new RulesEngine(level).TryMove(level.CreateInitialState(), Direction.Left);
            Assert.That(Names(SoundCues.For(result)), Does.Contain(Sound.EchoBlocked));
        }

        [Test]
        public void EverySound_IsAudibleAndClickFree([Values] Sound sound)
        {
            var samples = SoundFactory.Synthesize(sound);

            Assert.That(samples.Length, Is.GreaterThan(SoundFactory.SampleRate / 20));
            Assert.That(samples.Max(Mathf.Abs), Is.InRange(0.05f, 1f));
            Assert.That(Mathf.Abs(samples[0]), Is.LessThan(0.01f));
            Assert.That(Mathf.Abs(samples[samples.Length - 1]), Is.LessThan(0.01f));
        }

        [Test]
        public void Music_LoopsWithoutAClick()
        {
            var samples = SoundFactory.SynthesizeMusic();

            Assert.That(samples.Length, Is.EqualTo(Mathf.RoundToInt(SoundFactory.MusicSeconds * SoundFactory.MusicSampleRate)));
            Assert.That(samples.Max(Mathf.Abs), Is.InRange(0.02f, 1f));
            Assert.That(Mathf.Abs(samples[samples.Length - 1] - samples[0]), Is.LessThan(0.02f), "The last sample must flow into the first.");
        }

        [Test]
        public void MutedMusic_DoesNotPlay()
        {
            var host = new GameObject("AudioHost");
            try
            {
                var audio = new AudioService(host);
                audio.SetVolumes(0.5f, 0f);
                audio.StartMusic();
                audio.Play(Sound.Step);

                Assert.That(audio.MusicVolume, Is.EqualTo(0f));
                Assert.That(host.GetComponents<AudioSource>().Where(s => s.loop).All(s => !s.isPlaying), Is.True);
            }
            finally
            {
                Object.Destroy(host);
            }
        }

        private static Sound[] Names(System.Collections.Generic.IEnumerable<SoundCue> cues) => cues.Select(c => c.Sound).ToArray();
    }
}
