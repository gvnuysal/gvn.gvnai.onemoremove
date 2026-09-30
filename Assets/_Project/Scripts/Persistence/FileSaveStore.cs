using System;
using System.IO;
using System.Text;
using OneMoreMove.Session;

namespace OneMoreMove.Persistence
{
    /// <summary>
    /// Synchronous file store with a last-good backup. Writes go to a temp file in the same directory which then
    /// atomically replaces the main file; the previous main file becomes the backup.
    /// Not thread-safe on its own: <see cref="QueuedSaveStore"/> serialises access.
    /// </summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

        private readonly SaveSerializer _serializer;
        private bool _mainIsInvalid;

        public FileSaveStore(string directory, SaveSerializer serializer = null, string fileName = "save.json")
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("Directory is required.", nameof(directory));
            Directory = directory;
            MainPath = Path.Combine(directory, fileName);
            BackupPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(fileName) + ".bak" + Path.GetExtension(fileName));
            TempPath = MainPath + ".tmp";
            CorruptPath = Path.Combine(directory, Path.GetFileNameWithoutExtension(fileName) + ".corrupt" + Path.GetExtension(fileName));
            _serializer = serializer ?? new SaveSerializer();
        }

        public string Directory { get; }
        public string MainPath { get; }
        public string BackupPath { get; }
        public string TempPath { get; }
        public string CorruptPath { get; }

        /// <summary>True after loading a save from a newer game version: the file must not be overwritten.</summary>
        public bool WritesBlocked { get; private set; }

        public SaveSerializer Serializer => _serializer;

        public SaveLoadResult Load()
        {
            var hasMain = File.Exists(MainPath);
            var hasBackup = File.Exists(BackupPath);
            if (!hasMain && !hasBackup) return new SaveLoadResult(SaveLoadStatus.NoSave, null);

            var main = hasMain ? Read(MainPath) : null;
            if (main?.Status == SaveReadStatus.Ok) return new SaveLoadResult(SaveLoadStatus.Loaded, main.Data);
            if (main?.Status == SaveReadStatus.FutureVersion) return Unsupported(main.Error);

            // The main file is missing or broken. It must never be rotated into the backup slot.
            _mainIsInvalid = hasMain;

            var backup = hasBackup ? Read(BackupPath) : null;
            if (backup?.Status == SaveReadStatus.Ok) return new SaveLoadResult(SaveLoadStatus.RecoveredFromBackup, backup.Data, main?.Error);
            if (backup?.Status == SaveReadStatus.FutureVersion) return Unsupported(backup.Error);

            if (hasMain) TryMoveAside(MainPath);
            return new SaveLoadResult(SaveLoadStatus.CorruptedReset, null, main?.Error ?? backup?.Error);
        }

        public void Save(SaveData data) => WriteText(_serializer.Serialize(data));

        public void Flush()
        {
        }

        /// <summary>Writes an already serialised document. Skipped silently when writes are blocked.</summary>
        public void WriteText(string json)
        {
            if (WritesBlocked) return;

            System.IO.Directory.CreateDirectory(Directory);
            using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream, Utf8NoBom))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(MainPath) && !_mainIsInvalid)
            {
                try
                {
                    File.Replace(TempPath, MainPath, BackupPath, true);
                    return;
                }
                catch (Exception e) when (e is PlatformNotSupportedException || e is IOException || e is UnauthorizedAccessException)
                {
                    // Fallback for file systems without an atomic replace: keep a backup, then move into place.
                    File.Copy(MainPath, BackupPath, true);
                    File.Delete(MainPath);
                }
            }
            else if (File.Exists(MainPath))
            {
                File.Delete(MainPath);
            }

            File.Move(TempPath, MainPath);
            _mainIsInvalid = false;
        }

        private SaveReadResult Read(string path)
        {
            try
            {
                return _serializer.Deserialize(File.ReadAllText(path, Utf8NoBom));
            }
            catch (IOException e)
            {
                return new SaveReadResult(SaveReadStatus.Invalid, null, e.Message);
            }
        }

        private SaveLoadResult Unsupported(string detail)
        {
            WritesBlocked = true;
            return new SaveLoadResult(SaveLoadStatus.UnsupportedVersion, null, detail);
        }

        private void TryMoveAside(string path)
        {
            try
            {
                File.Copy(path, CorruptPath, true);
            }
            catch (IOException)
            {
                // Keeping a diagnostic copy is best effort.
            }
        }
    }
}
