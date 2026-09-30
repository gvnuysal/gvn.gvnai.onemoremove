using System;
using System.Threading;
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

        public void Flush()
        {
            string json;
            long sequence;
            lock (_queueLock)
            {
                json = _pendingJson;
                sequence = _pendingSequence;
                _pendingJson = null;
            }

            if (json != null) Write(json, sequence);

            // Wait for a write the worker may have taken just before us.
            lock (_writeLock)
            {
            }
        }

        private void Drain()
        {
            while (true)
            {
                string json;
                long sequence;
                lock (_queueLock)
                {
                    if (_pendingJson == null)
                    {
                        _draining = false;
                        return;
                    }

                    json = _pendingJson;
                    sequence = _pendingSequence;
                    _pendingJson = null;
                }

                Write(json, sequence);
            }
        }

        private void Write(string json, long sequence)
        {
            lock (_writeLock)
            {
                if (sequence <= Interlocked.Read(ref _lastWrittenSequence)) return;
                try
                {
                    _inner.WriteText(json);
                    Interlocked.Exchange(ref _lastWrittenSequence, sequence);
                }
                catch (Exception e)
                {
                    _onError?.Invoke(e);
                }
            }
        }
    }
}
