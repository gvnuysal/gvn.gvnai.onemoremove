using System.Collections.Generic;
using OneMoreMove.Core;
using UnityEngine;

namespace OneMoreMove.Presentation.Audio
{
    /// <summary>One sound to play, <see cref="Delay"/> seconds after the move starts.</summary>
    public readonly struct SoundCue
    {
        public readonly Sound Sound;
        public readonly float Delay;

        public SoundCue(Sound sound, float delay = 0f)
        {
            Sound = sound;
            Delay = delay;
        }

        public override string ToString() => Delay > 0f ? $"{Sound}+{Delay:0.00}s" : Sound.ToString();
    }

    /// <summary>
    /// Maps a move to its sounds, in step with <see cref="BoardView.PlayMove"/>. Every sound repeats something the board
    /// already shows, so no information is carried by audio alone.
    /// </summary>
    public static class SoundCues
    {
        public static IReadOnlyList<SoundCue> For(MoveResult result)
        {
            var cues = new List<SoundCue>(4);
            if (!result.Accepted)
            {
                cues.Add(new SoundCue(Sound.Reject));
                return cues;
            }

            foreach (var e in result.Events)
            {
                switch (e.Type)
                {
                    case MoveEventType.PlayerMoved: cues.Add(new SoundCue(Sound.Step)); break;
                    case MoveEventType.PlayerWaited: cues.Add(new SoundCue(Sound.Wait)); break;
                    case MoveEventType.EchoBlocked: cues.Add(new SoundCue(Sound.EchoBlocked, 0.04f)); break;
                    case MoveEventType.GatesToggled: cues.Add(new SoundCue(Sound.Gate, 0.07f)); break;
                    case MoveEventType.LevelWon: cues.Add(new SoundCue(Sound.Win, 0.16f)); break;
                }
            }

            return cues;
        }
    }

    /// <summary>
    /// Plays effects through a small round-robin pool (so a delayed cue never cuts off the previous one) and loops
    /// the music. Volumes come from the player's settings.
    /// </summary>
    public sealed class AudioService
    {
        private const int Voices = 6;

        private readonly AudioSource[] _voices = new AudioSource[Voices];
        private readonly AudioSource _music;
        private int _next;
        private float _sfxVolume = 1f;

        public AudioService(GameObject host)
        {
            if (Object.FindAnyObjectByType<AudioListener>() == null) host.AddComponent<AudioListener>();

            for (var i = 0; i < Voices; i++) _voices[i] = CreateSource(host, loop: false);
            _music = CreateSource(host, loop: true);
        }

        public float MusicVolume => _music.volume;

        public void SetVolumes(float sfx, float music)
        {
            _sfxVolume = Mathf.Clamp01(sfx);
            _music.volume = Mathf.Clamp01(music);
            if (_music.volume > 0f && !_music.isPlaying && _music.clip != null) _music.Play();
            if (_music.volume <= 0f && _music.isPlaying) _music.Pause();
        }

        public void StartMusic()
        {
            if (_music.clip == null) _music.clip = SoundFactory.Music();
            if (_music.volume > 0f && !_music.isPlaying) _music.Play();
        }

        public void PlayMove(MoveResult result)
        {
            foreach (var cue in SoundCues.For(result)) Play(cue.Sound, cue.Delay);
        }

        public void Play(Sound sound, float delay = 0f)
        {
            if (_sfxVolume <= 0f) return;

            var source = _voices[_next];
            _next = (_next + 1) % Voices;
            source.clip = SoundFactory.Get(sound);
            source.volume = _sfxVolume;
            if (delay > 0f) source.PlayDelayed(delay);
            else source.Play();
        }

        private static AudioSource CreateSource(GameObject host, bool loop)
        {
            var source = host.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }
    }
}
