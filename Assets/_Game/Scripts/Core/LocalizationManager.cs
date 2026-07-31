using System;
using System.Collections.Generic;
using UnityEngine;

namespace Fields.Core
{
    /// <summary>
    /// Simple key-based localization. String tables are loaded from Resources/Localization/*.json.
    /// Falls back to English if the requested key is missing in the active language.
    /// Client provides translated string files; only English is seeded here.
    /// </summary>
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        public event Action OnLanguageChanged;

        public enum Language
        {
            English = 0,
            Hungarian,
            German,
            Russian,
            ChineseSimplified,
            Polish,
            Spanish,
            PortugueseBrazil,
            Japanese
        }

        static readonly string[] LANG_CODES =
        {
            "en", "hu", "de", "ru", "zh-Hans", "pl", "es", "pt-BR", "jp"
        };

        Language _active = Language.English;
        Dictionary<string, string> _table = new Dictionary<string, string>();
        Dictionary<string, string> _fallback = new Dictionary<string, string>(); // English always loaded

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Seed English fallback from built-in defaults
            foreach (var kv in DefaultEnglishStrings())
                _fallback[kv.Key] = kv.Value;

            string saved = PlayerPrefs.GetString("loc_lang", "en");
            Language lang = LanguageFromCode(saved);
            SetLanguage(lang, silent: true);
        }

        // ------------------------------------------------------------------ //

        public Language ActiveLanguage => _active;

        public void SetLanguage(Language lang, bool silent = false)
        {
            _active = lang;
            PlayerPrefs.SetString("loc_lang", LANG_CODES[(int)lang]);
            LoadTable(LANG_CODES[(int)lang]);
            if (!silent) OnLanguageChanged?.Invoke();
        }

        public string Get(string key)
        {
            if (_table.TryGetValue(key, out string v)) return v;
            if (_fallback.TryGetValue(key, out string fb)) return fb;
            return $"[{key}]";
        }

        // Convenience: format with args
        public string Get(string key, params object[] args)
        {
            string fmt = Get(key);
            try { return string.Format(fmt, args); }
            catch { return fmt; }
        }

        // ------------------------------------------------------------------ //

        void LoadTable(string langCode)
        {
            _table.Clear();
            var asset = Resources.Load<TextAsset>($"Localization/{langCode}");
            if (asset == null)
            {
                Debug.LogWarning($"[Loc] No string table for '{langCode}' — using English fallback.");
                return;
            }
            var raw = JsonUtility.FromJson<StringTableJson>(asset.text);
            if (raw?.entries == null) return;
            foreach (var e in raw.entries) _table[e.key] = e.value;
        }

        static Language LanguageFromCode(string code)
        {
            for (int i = 0; i < LANG_CODES.Length; i++)
                if (LANG_CODES[i] == code) return (Language)i;
            return Language.English;
        }

        // ------------------------------------------------------------------ //
        // English defaults — client to override via Resources/Localization/en.json
        // ------------------------------------------------------------------ //

        static Dictionary<string, string> DefaultEnglishStrings() => new()
        {
            // HUD
            { "hud.money",        "$ {0}" },
            { "hud.completion",   "{0}%" },
            { "hud.bales",        "[{0}]" },

            // Shop
            { "shop.tab.tools",    "Tools" },
            { "shop.tab.upgrades", "Upgrades" },
            { "shop.tab.parcels",  "Parcels" },
            { "shop.buy",         "Buy  ${0}" },
            { "shop.upgrade",     "Upgrade  ${0}" },
            { "shop.owned",       "Owned" },
            { "shop.maxlevel",    "Max Level" },
            { "shop.unlock",      "Unlock  ${0}" },
            { "shop.locked",      "Locked" },
            { "shop.notenough",   "Not enough money" },

            // Tool names
            { "tool.handsickle",   "Hand Sickle" },
            { "tool.longscythe",   "Long Scythe" },
            { "tool.trimmer",      "String Trimmer" },
            { "tool.pushmower",    "Push Mower" },
            { "tool.rideon",       "Ride-On Mower" },

            // Parcels
            { "parcel.0.name", "Home Field" },
            { "parcel.1.name", "East Meadow" },
            { "parcel.2.name", "Hillside" },
            { "parcel.3.name", "South Plateau" },

            // Per-parcel completion pop
            { "endscreen.title",    "Parcel Complete!" },
            { "endscreen.earned",   "Earned: ${0}" },
            { "endscreen.time",     "Time: {0}:{1:D2}" },
            { "endscreen.continue", "Continue" },

            // Final end screen (all 4 parcels done)
            { "end.title",    "All Fields Cleared!" },
            { "end.earnings", "Total earnings: ${0}" },
            { "end.time",     "Time: {0:00}:{1:00}" },
            { "end.playagain","Play Again" },
            { "end.quit",     "Quit" },

            // Hints (max 5 per spec §8.11)
            { "hint.0", "Hold primary to use your tool." },
            { "hint.1", "Sell bales at the stand for money." },
            { "hint.2", "Carry up to 3 bales at once." },
            { "hint.3", "Uncut grass slows the push mower." },
            { "hint.4", "Round bales roll on slopes — use it!" },

            // Accessibility menu
            { "a11y.title",       "Accessibility" },
            { "a11y.fov",         "Field of View: {0}°" },
            { "a11y.blur",        "Motion Blur" },
            { "a11y.colorblind",  "Colorblind Mode" },
            { "a11y.cb.none",     "None" },
            { "a11y.cb.deut",     "Deuteranopia" },
            { "a11y.cb.prot",     "Protanopia" },
            { "a11y.cb.trit",     "Tritanopia" },
        };

        // ------------------------------------------------------------------ //

        [Serializable]
        class StringTableJson
        {
            public StringEntry[] entries;
        }

        [Serializable]
        class StringEntry
        {
            public string key;
            public string value;
        }
    }
}
