using System;
using UnityEngine;

namespace OneMoreMove.Presentation
{
    public enum Language
    {
        Turkish,
        English
    }

    /// <summary>
    /// The UI language. Every player-facing string is written once as <c>L("Türkçe", "English")</c>, so a missing
    /// translation is a compile error, not a runtime surprise. Turkish is the source language of the content.
    /// </summary>
    public static class Localization
    {
        public static Language Current { get; private set; } = Language.Turkish;

        /// <summary>Code of <see cref="Current"/> as stored in settings and level translations ("tr", "en").</summary>
        public static string Code => ToCode(Current);

        /// <summary>Raised after <see cref="Current"/> changes; screens re-apply their texts.</summary>
        public static event Action Changed;

        /// <summary>Test seam: the device language the game would see.</summary>
        internal static SystemLanguage? DeviceLanguageOverride { get; set; }

        /// <summary>Turkish devices play in Turkish, every other device in English.</summary>
        public static Language DeviceDefault =>
            (DeviceLanguageOverride ?? Application.systemLanguage) == SystemLanguage.Turkish ? Language.Turkish : Language.English;

        public static Language[] All { get; } = { Language.Turkish, Language.English };

        /// <summary>The language for a saved setting; null or unknown follows the device.</summary>
        public static Language FromCode(string code)
        {
            switch (code)
            {
                case "tr": return Language.Turkish;
                case "en": return Language.English;
                default: return DeviceDefault;
            }
        }

        public static string ToCode(Language language) => language == Language.Turkish ? "tr" : "en";

        /// <summary>A language's name in that language, as language pickers show it.</summary>
        public static string NativeName(Language language) => language == Language.Turkish ? "Türkçe" : "English";

        public static void Set(Language language)
        {
            if (Current == language) return;
            Current = language;
            Changed?.Invoke();
        }

        public static string L(string turkish, string english) => Current == Language.Turkish ? turkish : english;
    }
}
