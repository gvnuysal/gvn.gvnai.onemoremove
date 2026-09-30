using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneMoreMove.Presentation.Audio
{
    public enum Sound
    {
        Step,
        Wait,
        Gate,
        EchoBlocked,
        Reject,
        Undo,
        Win
    }

    /// <summary>
    /// Synthesises every sound at runtime (soft sine voices with smooth envelopes), like <see cref="ShapeSpriteFactory"/>
    /// does for the art: no imported audio, nothing to license. Clips are cached for the lifetime of the app.
    /// </summary>
    public static class SoundFactory
    {
        public const int SampleRate = 44100;
        public const int MusicSampleRate = 22050;
        public const float MusicSeconds = 16f;

        private static readonly Dictionary<Sound, AudioClip> Cache = new Dictionary<Sound, AudioClip>();
        private static AudioClip _music;

        public static AudioClip Get(Sound sound)
        {
            if (Cache.TryGetValue(sound, out var clip) && clip != null) return clip;

            var samples = Synthesize(sound);
            clip = AudioClip.Create(sound.ToString(), samples.Length, 1, SampleRate, false);
            clip.SetData(samples, 0);
            Cache[sound] = clip;
            return clip;
        }

        /// <summary>A calm, seamlessly looping pad with sparse plucks in A minor pentatonic.</summary>
        public static AudioClip Music()
        {
            if (_music != null) return _music;

            var samples = SynthesizeMusic();
            _music = AudioClip.Create("Music", samples.Length, 1, MusicSampleRate, false);
            _music.SetData(samples, 0);
            return _music;
        }

        public static float[] Synthesize(Sound sound)
        {
            switch (sound)
            {
                case Sound.Step: return Render(0.11f, (t, d) => Pluck(t, 523.25f, 38f) * 0.55f);
                case Sound.Wait:
                    return Render(0.26f, (t, d) => 0.35f * Swell(t, d) * (Sine(t, 329.63f) + 0.5f * Sine(t, 493.88f)));
                case Sound.Gate:
                    return Render(0.34f, (t, d) => 0.3f * Decay(t, 11f) * (Sine(t, 783.99f) + 0.6f * Sine(t, 1174.66f) + 0.25f * Sine(t, 1567.98f)));
                case Sound.EchoBlocked:
                    return Render(0.14f, (t, d) => 0.5f * Decay(t, 30f) * Sine(t, 180f - 500f * t));
                case Sound.Reject:
                    return Render(0.16f, (t, d) => 0.35f * Decay(t, 16f) * (Sine(t, 110f) + 0.35f * Sine(t, 330f)) * (0.75f + 0.25f * Sine(t, 30f)));
                case Sound.Undo:
                    return Render(0.15f, (t, d) => 0.45f * Decay(t, 20f) * Sine(t, 660f - 1200f * t));
                case Sound.Win:
                    var notes = new[] { 523.25f, 659.25f, 783.99f, 1046.5f };
                    return Render(0.95f, (t, d) =>
                    {
                        var value = 0f;
                        for (var i = 0; i < notes.Length; i++)
                        {
                            var start = i * 0.11f;
                            if (t >= start) value += Pluck(t - start, notes[i], 5.5f) * 0.32f;
                        }

                        return value;
                    });
                default: throw new ArgumentOutOfRangeException(nameof(sound), sound, null);
            }
        }

        public static float[] SynthesizeMusic()
        {
            var length = Mathf.RoundToInt(MusicSeconds * MusicSampleRate);
            var samples = new float[length];

            // Am – F – C – G, four seconds each. Chord voices fade in and out over the neighbouring chord so the loop
            // wraps without a click: every envelope is evaluated on the circular time axis.
            var chords = new[]
            {
                new[] { 220.00f, 261.63f, 329.63f },
                new[] { 174.61f, 220.00f, 261.63f },
                new[] { 196.00f, 261.63f, 329.63f },
                new[] { 196.00f, 246.94f, 293.66f }
            };
            const float chordSeconds = MusicSeconds / 4f;

            for (var c = 0; c < chords.Length; c++)
            {
                var center = (c + 0.5f) * chordSeconds;
                foreach (var frequency in chords[c])
                {
                    for (var i = 0; i < length; i++)
                    {
                        var t = (float)i / MusicSampleRate;
                        var distance = CircularDistance(t, center, MusicSeconds);
                        var envelope = Mathf.Clamp01(1f - distance / chordSeconds);
                        if (envelope <= 0f) continue;

                        // Whole cycles per loop keep every voice phase-continuous at the wrap point.
                        var loopFrequency = Mathf.Round(frequency * MusicSeconds) / MusicSeconds;
                        samples[i] += 0.05f * envelope * envelope * (Sine(t, loopFrequency) + 0.3f * Sine(t, loopFrequency * 2f));
                    }
                }
            }

            // Sparse plucks, deterministic, from the A minor pentatonic scale.
            var scale = new[] { 440f, 523.25f, 587.33f, 659.25f, 783.99f };
            var random = new System.Random(7);
            for (var beat = 0f; beat < MusicSeconds - 1.5f; beat += 1f)
            {
                if (random.NextDouble() < 0.45) continue;
                var frequency = scale[random.Next(scale.Length)];
                var start = Mathf.RoundToInt(beat * MusicSampleRate);
                var noteLength = Mathf.RoundToInt(1.4f * MusicSampleRate);
                for (var i = 0; i < noteLength && start + i < length; i++)
                {
                    samples[start + i] += Pluck((float)i / MusicSampleRate, frequency, 3.2f) * 0.07f;
                }
            }

            return samples;
        }

        private static float[] Render(float seconds, Func<float, float, float> voice)
        {
            var length = Mathf.RoundToInt(seconds * SampleRate);
            var samples = new float[length];
            for (var i = 0; i < length; i++)
            {
                var t = (float)i / SampleRate;
                // A 5 ms fade at both ends removes clicks whatever the voice does.
                var edge = Mathf.Min(1f, Mathf.Min(t, seconds - t) / 0.005f);
                samples[i] = Mathf.Clamp(voice(t, seconds) * edge, -1f, 1f);
            }

            return samples;
        }

        private static float Sine(float t, float frequency) => Mathf.Sin(2f * Mathf.PI * frequency * t);

        private static float Decay(float t, float rate) => Mathf.Exp(-rate * t);

        private static float Swell(float t, float duration) => Mathf.Sin(Mathf.PI * Mathf.Clamp01(t / duration));

        /// <summary>A soft plucked note: fundamental plus a quieter octave, exponential decay.</summary>
        private static float Pluck(float t, float frequency, float decay) =>
            Decay(t, decay) * (Sine(t, frequency) + 0.3f * Sine(t, frequency * 2f) * Decay(t, decay));

        private static float CircularDistance(float a, float b, float period)
        {
            var d = Mathf.Abs(a - b) % period;
            return Mathf.Min(d, period - d);
        }
    }
}
