using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace OneMoreMove.Persistence
{
    /// <summary>Upgrades a save document from <see cref="FromVersion"/> to <c>FromVersion + 1</c>, in place.</summary>
    public interface ISaveMigration
    {
        int FromVersion { get; }
        void Apply(JObject document);
    }

    /// <summary>Applies migrations in sequence until the document reaches the current schema version.</summary>
    public sealed class SaveMigrator
    {
        private readonly Dictionary<int, ISaveMigration> _migrations;

        public SaveMigrator(int currentVersion, IEnumerable<ISaveMigration> migrations = null)
        {
            CurrentVersion = currentVersion;
            _migrations = (migrations ?? Enumerable.Empty<ISaveMigration>()).ToDictionary(m => m.FromVersion);
        }

        public int CurrentVersion { get; }

        public bool IsFutureVersion(int version) => version > CurrentVersion;

        /// <summary>Returns false when a step is missing; the document must then be treated as invalid.</summary>
        public bool TryMigrate(JObject document, out string error)
        {
            var version = document.Value<int?>("schemaVersion") ?? 0;
            if (version < 1)
            {
                error = "Missing schemaVersion.";
                return false;
            }

            while (version < CurrentVersion)
            {
                if (!_migrations.TryGetValue(version, out var migration))
                {
                    error = $"No migration from schema {version}.";
                    return false;
                }

                migration.Apply(document);
                version++;
                document["schemaVersion"] = version;
            }

            error = null;
            return true;
        }
    }

    internal static class SaveChecksum
    {
        /// <summary>SHA-256 of the compact document without its checksum. Detects corruption; it is not anti-cheat.</summary>
        public static string Compute(JObject document)
        {
            var copy = (JObject)document.DeepClone();
            copy.Remove("checksum");
            var bytes = System.Text.Encoding.UTF8.GetBytes(copy.ToString(Newtonsoft.Json.Formatting.None));
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", string.Empty).ToLowerInvariant();
            }
        }
    }
}
