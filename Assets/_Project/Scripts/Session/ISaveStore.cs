namespace OneMoreMove.Session
{
    public enum SaveLoadStatus
    {
        /// <summary>First launch: nothing stored yet.</summary>
        NoSave,
        Loaded,

        /// <summary>The main file was invalid; the last good backup was used.</summary>
        RecoveredFromBackup,

        /// <summary>Main and backup were both invalid; the game starts fresh and tells the player.</summary>
        CorruptedReset,

        /// <summary>The file comes from a newer game version. It is left untouched and saving is disabled.</summary>
        UnsupportedVersion
    }

    public sealed class SaveLoadResult
    {
        public SaveLoadResult(SaveLoadStatus status, SaveData data, string detail = null)
        {
            Status = status;
            Data = data;
            Detail = detail;
        }

        public SaveLoadStatus Status { get; }

        /// <summary>Null unless <see cref="Status"/> is Loaded or RecoveredFromBackup.</summary>
        public SaveData Data { get; }

        public string Detail { get; }
    }

    /// <summary>Port for local persistence; implemented outside the application layer.</summary>
    public interface ISaveStore
    {
        SaveLoadResult Load();

        /// <summary>Requests a save. Implementations may write asynchronously but must keep writes in order.</summary>
        void Save(SaveData data);

        /// <summary>Blocks until every requested save is on disk (app pause, focus loss, quit).</summary>
        void Flush();
    }
}
