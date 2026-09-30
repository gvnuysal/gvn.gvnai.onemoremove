using System;
using System.Threading.Tasks;
using OneMoreMove.Session;

namespace OneMoreMove.Persistence
{
    /// <summary>
    /// Moves file writes off the main thread. Requests are serialised on the caller's thread (an immutable string),
    /// coalesced so only the newest pending document is written, and written strictly in order: an older document can
    /// never overwrite a newer one.
    /// </summary>
    public sealed class QueuedSaveStore : ISaveStore
    {
        private readonly FileSaveStore _inner;
        private readonly Action<Exception> _onError;
        private readonly object _queueLock = new object();
        private readonly object _writeLock = new object();

        private string _pendingJson;
        private long _pendingSequence;
        private long _lastWrittenSequence;
        private bool _draining;

        public QueuedSaveStore(FileSaveStore inner, Action<Exception> onError = null)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
            _onError = onError;
        }

        public SaveLoadResult Load() => _inner.Load();

        public void Save(SaveData data)
        {
            var json = _inner.Serializer.Serialize(data);
            lock (_queueLock)
            {
                _pendingJson = json;
                _pendingSequence++;
                if (_draining) return;
                _draining = true;
            }

            Task.Run(Drain);
        }

        /// <summary>
        /// Returns once the newest document saved so far is on disk. A document is only ever taken from the queue while
        /// holding the write lock, so a write the worker has started always finishes before Flush can return.
        /// </summary>
        public void Flush()
        {
            lock (_writeLock)
            {
                if (TakePending(out var json, out var sequence, stopDraining: false)) Write(json, sequence);
            }
        }

        private void Drain()
        {
            while (true)
            {
                lock (_writeLock)
                {
                    if (!TakePending(out var json, out var sequence, stopDraining: true)) return;
                    Write(json, sequence);
                }
            }
        }

        private bool TakePending(out string json, out long sequence, bool stopDraining)
        {
            lock (_queueLock)
            {
                json = _pendingJson;
                sequence = _pendingSequence;
                _pendingJson = null;
                if (json == null && stopDraining) _draining = false;
                return json != null;
            }
        }

        /// <summary>Caller holds <see cref="_writeLock"/>.</summary>
        private void Write(string json, long sequence)
        {
            if (sequence <= _lastWrittenSequence) return;
            try
            {
                _inner.WriteText(json);
                _lastWrittenSequence = sequence;
            }
            catch (Exception e)
            {
                _onError?.Invoke(e);
            }
        }
    }
}
