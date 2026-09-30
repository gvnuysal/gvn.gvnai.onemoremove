using System;
using System.Collections.Generic;
using UnityEngine;

namespace OneMoreMove.Presentation
{
    /// <summary>
    /// Minimal time-based tweening. The domain result is computed before any tween starts; tweens only show it, so
    /// <see cref="CompleteAll"/> can always jump to the final picture (focus loss, reduced motion, next input).
    /// </summary>
    public sealed class TweenRunner
    {
        private readonly List<Tween> _active = new List<Tween>();
        private readonly List<Tween> _finished = new List<Tween>();
        private Action _onIdle;

        private int _blockingCount;

        /// <summary>True while a blocking tween (a state transition) runs. Decorative effects never block input.</summary>
        public bool IsRunning => _blockingCount > 0;

        /// <param name="apply">Receives eased progress 0..1.</param>
        /// <param name="blocking">False for purely decorative feedback (flashes, pulses) that must not delay input.</param>
        public void Add(float delay, float duration, Action<float> apply, Action complete = null, bool blocking = true)
        {
            _active.Add(new Tween { Delay = delay, Duration = Mathf.Max(0.0001f, duration), Apply = apply, Complete = complete, Blocking = blocking });
            if (blocking) _blockingCount++;
        }

        /// <summary>Invoked once when the currently queued blocking tweens have finished (immediately if none).</summary>
        public void WhenIdle(Action action)
        {
            if (!IsRunning)
            {
                action?.Invoke();
                return;
            }

            _onIdle += action;
        }

        public void Tick(float deltaTime)
        {
            if (_active.Count == 0) return;

            _finished.Clear();
            for (var i = 0; i < _active.Count; i++)
            {
                var tween = _active[i];
                tween.Elapsed += deltaTime;
                var t = Mathf.Clamp01((tween.Elapsed - tween.Delay) / tween.Duration);
                if (tween.Elapsed >= tween.Delay) tween.Apply?.Invoke(EaseOutCubic(t));
                if (t >= 1f) _finished.Add(tween);
            }

            var blockingFinished = false;
            foreach (var tween in _finished)
            {
                _active.Remove(tween);
                if (tween.Blocking)
                {
                    _blockingCount--;
                    blockingFinished = true;
                }

                tween.Complete?.Invoke();
            }

            if (blockingFinished && _blockingCount == 0) FireIdle();
        }

        public void CompleteAll()
        {
            while (_active.Count > 0)
            {
                var pending = _active.ToArray();
                _active.Clear();
                _blockingCount = 0;
                foreach (var tween in pending)
                {
                    tween.Apply?.Invoke(1f);
                    tween.Complete?.Invoke();
                }
            }

            FireIdle();
        }

        /// <summary>Drops tweens without applying them; used when the board is rebuilt from a fresh state.</summary>
        public void Cancel()
        {
            _active.Clear();
            _blockingCount = 0;
            _onIdle = null;
        }

        private void FireIdle()
        {
            var idle = _onIdle;
            _onIdle = null;
            idle?.Invoke();
        }

        private static float EaseOutCubic(float t)
        {
            var inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private sealed class Tween
        {
            public float Delay;
            public float Duration;
            public float Elapsed;
            public Action<float> Apply;
            public Action Complete;
            public bool Blocking;
        }
    }
}
