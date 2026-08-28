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

        public float CurrencyRate { get; private set; } = 1f;

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
            CurrencyRate = 1f;
            var asset = Resources.Load<TextAsset>($"Localization/{langCode}");
            if (asset == null)
            {
                Debug.LogWarning($"[Loc] No string table for '{langCode}' — using English fallback.");
                return;
            }
            var raw = JsonUtility.FromJson<StringTableJson>(asset.text);
            if (raw?.entries == null) return;
            foreach (var e in raw.entries) _table[e.key] = e.value;
            if (_table.TryGetValue("currency.rate", out string rateStr) &&
                float.TryParse(rateStr, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, out float rate))
                CurrencyRate = rate;
        }

        public long ToLocal(long usd) => (long)System.Math.Round(usd * CurrencyRate);

        public string FormatMoney(long usd)
        {
            long local = ToLocal(usd);
            string fmt = Get("hud.money");
            try { return string.Format(fmt, local); }
            catch { return $"${local}"; }
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
            { "hud.baling_progress",  "Baling...  {0}%" },
            { "hud.baling_ready",     "[E]  Bale" },
            { "hud.baling_start",     "[E]  Bale" },
            { "hud.baling_cancel",    "Baling..." },
            { "hud.bale_created",     "Bale Created!" },
            { "hud.bale_drop_single", "Bale dropped" },
            { "hud.bale_drop_multi",  "{0} bales dropped" },
            { "hud.pickup_bale",      "[E]  Pick up bale" },
            { "hud.carry_full",       "Hands full" },
            { "hud.drop_bales",       "[G]  Drop bales" },
            { "hud.interact",         "[E]  Interact" },
            { "hud.bale_push_start",  "[E]  Push bale" },
            { "hud.bale_push_active", "[WASD]  Roll / Rotate  |  [E]  Release" },

            // Shop
            { "shop.tab.tools",    "Tools" },
            { "shop.tab.upgrades", "Upgrades" },
            { "shop.tab.parcels",  "Parcels" },
            { "shop.balance",      "Balance:  {0}" },
            { "shop.buy",         "Buy  {0}" },
            { "shop.upgrade",     "Upgrade  {0}" },
            { "shop.owned",       "Owned" },
            { "shop.maxlevel",    "Max Level" },
            { "shop.unlock",      "Unlock  {0}" },
            { "shop.locked",      "Locked" },
            { "shop.notenough",   "Not enough money" },
            { "shop.upgrades.empty", "No tools owned yet. Buy one in the Tools tab." },
            { "shop.unlocks.empty",  "No content available." },

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
            { "parcel.hay_clear",         "Clear" },
            { "parcel.status.complete",   "Complete" },
            { "parcel.status.inprogress", "In Progress" },

            // Per-parcel completion pop
            { "endscreen.title",    "Parcel Complete!" },
            { "endscreen.earned",   "Earned: ${0}" },
            { "endscreen.time",     "Time: {0}:{1:D2}" },
            { "endscreen.continue", "Continue" },

            // Final end screen (all 4 parcels done)
            { "end.title",             "All Fields Cleared!" },
            { "end.title.nuclear",     "☢ Nuclear Ending" },
            { "end.earnings",          "Total earnings: ${0}" },
            { "end.time",              "Time: {0:00}:{1:00}" },
            { "end.playagain",         "Play Again" },
            { "end.quit",              "Quit" },
            { "end.stat.time",         "⏱ {0}" },
            { "end.stat.money",        "💰 {0}" },
            { "end.stat.area",         "🌿 {0} m²" },
            { "end.graph.header",      "CUT AREA OVER TIME" },
            { "end.graph.yaxis",       "m²" },
            { "end.graph.xaxis",       "Time (min)" },
            { "end.ach.header",        "ACHIEVEMENTS THIS SESSION" },
            { "end.ach.empty",         "No achievements unlocked this session." },
            { "end.button.mainmenu",   "Main Menu" },

            // Hints (max 5 per spec §8.11)
            { "hint.0", "Hold primary to use your tool." },
            { "hint.1", "Sell bales at the stand for money." },
            { "hint.2", "Carry up to 3 bales at once." },
            { "hint.3", "Uncut grass slows the push mower." },
            { "hint.4", "Round bales roll on slopes — use it!" },

            // Main menu buttons + confirm dialogs
            { "menu.continue",             "Continue" },
            { "menu.newgame",              "New Game" },
            { "menu.playwithfriends",      "Play with Friends" },
            { "menu.settings",             "Settings" },
            { "menu.credits",              "Credits" },
            { "menu.quit",                 "Quit" },
            { "menu.overwrite.header",     "New Game" },
            { "menu.overwrite.body",       "Starting a new game will overwrite your existing save. This cannot be undone." },
            { "menu.quit.header",          "Quit" },
            { "menu.quit.body",            "Are you sure you want to quit?" },

            // Pause menu
            { "pause.resume",              "Resume" },
            { "pause.journal",             "Journal" },
            { "pause.settings",            "Settings" },
            { "pause.quit",                "Quit" },
            { "pause.coop_notice",         "The game continues while this menu is open." },

            // Journal tabs
            { "journal.tab.parcels",       "Parcels" },
            { "journal.tab.statistics",    "Statistics" },
            { "journal.tab.records",       "Records" },
            { "journal.close",             "Close" },
            { "journal.all_fields",        "Field" },

            // Journal statistics labels
            { "journal.stat.areacut",      "Area Cut" },
            { "journal.stat.hay",          "Hay Collected" },
            { "journal.stat.squarebales",  "Square Bales" },
            { "journal.stat.roundbales",   "Round Bales" },
            { "journal.stat.earned",       "Money Earned" },
            { "journal.stat.spent",        "Money Spent" },
            { "journal.stat.distance",     "Distance" },
            { "journal.stat.swings",       "Swings" },
            { "journal.stat.playtime",     "Playtime" },
            { "journal.stat.wallet",       "Wallet" },
            { "journal.stat.parcels",      "Parcels Completed" },
            { "journal.stat.overall",      "Overall Completion" },
            { "journal.stat.totaltime",    "Total Playtime" },

            // Journal records labels
            { "journal.rec.fastest0",      "Home Field" },
            { "journal.rec.fastest1",      "East Meadow" },
            { "journal.rec.fastest2",      "Hillside" },
            { "journal.rec.fastest3",      "South Plateau" },
            { "journal.rec.area",          "Largest Area" },
            { "journal.rec.bales",         "Most Bales Delivered" },
            { "journal.rec.streak",        "Longest Streak" },
            { "journal.rec.fullgame",      "Full Game Completion" },

            // Journal records section headers (dynamic content)
            { "journal.rec.sec.parcels",         "Fastest Parcels" },
            { "journal.rec.sec.session",         "All-Time Records" },
            { "journal.rec.sec.fullgame",        "Full Game" },
            { "journal.rec.sec.throw",           "Throw Records" },

            // Journal records row labels (dynamic content)
            { "journal.rec.label.largest",       "Largest Area" },
            { "journal.rec.label.bales",         "Most Bales Delivered" },
            { "journal.rec.label.streak",        "Longest Streak" },
            { "journal.rec.label.time",          "Best Time" },
            { "journal.rec.label.longestthrow",  "Longest Throw" },
            { "journal.rec.label.bestbale",      "Best Single Bale" },

            // Settings display labels
            { "settings.display.fov",      "Field of View" },
            { "settings.display.wm",       "Window Mode" },
            { "settings.display.quality",  "Quality" },
            { "settings.display.vsync",    "V-Sync" },
            { "settings.display.framecap", "Frame Cap" },
            { "settings.audio.master",     "Master" },
            { "settings.audio.tools",      "Tools" },
            { "settings.audio.world",      "World" },
            { "settings.audio.ambience",   "Ambience" },
            { "settings.audio.ui",         "UI" },
            { "settings.controls.sensx",   "Mouse Sensitivity X" },
            { "settings.controls.sensy",   "Mouse Sensitivity Y" },
            { "settings.controls.inverty", "Invert Y" },
            { "settings.controls.gpsens",  "Gamepad Sensitivity" },
            { "settings.controls.gpdead",  "Gamepad Deadzone" },
            { "settings.controls.tooluse", "Tool Use" },
            { "settings.controls.sprint",  "Sprint" },
            { "settings.controls.hold",    "Hold" },
            { "settings.controls.toggle",  "Toggle" },
            { "settings.controls.rebinds", "Reset Rebinds" },
            { "settings.revert.countdown", "Reverting in {0}s…" },
            { "settings.revert.confirm",   "Keep Settings" },

            // Co-op screen
            { "coop.notice",     "Guests start with base tools and their progress is not saved.\nThe host owns the save file." },
            { "coop.join.empty", "No joinable lobbies found.\nAsk a friend to host and invite you." },

            // Tooltip / context help panel
            { "tooltip.controls",    "<b>Controls</b>\nWASD / L-Stick — Move\nMouse / R-Stick — Look\nShift / LStick — Sprint\nLMB / West btn — Use tool\nScroll / LB–RB — Switch tool\nE / North btn — Interact\nG — Drop bale\nJ / View tap — Journal\nTab hold / View hold — This panel\nEsc / Start — Pause" },
            { "tooltip.tips_header", "<b>Tips</b>" },

            // Confirm modal buttons
            { "confirm.ok",     "Confirm" },
            { "confirm.cancel", "Cancel" },
            { "credits.close",  "Close" },

            // Accessibility menu
            { "a11y.title",       "Accessibility" },
            { "a11y.fov",         "Field of View: {0}°" },
            { "a11y.blur",        "Motion Blur" },
            { "a11y.colorblind",  "Colorblind Mode" },
            { "a11y.cb.none",     "None" },
            { "a11y.cb.deut",     "Deuteranopia" },
            { "a11y.cb.prot",     "Protanopia" },
            { "a11y.cb.trit",     "Tritanopia" },

            // Settings screen
            { "settings.tab.display",       "Display" },
            { "settings.tab.audio",         "Audio" },
            { "settings.tab.controls",      "Controls" },
            { "settings.tab.accessibility", "Accessibility" },
            { "settings.tab.language",      "Language" },
            { "settings.wm.fullscreen",     "Fullscreen" },
            { "settings.wm.borderless",     "Borderless" },
            { "settings.wm.windowed",       "Windowed" },
            { "settings.quality.low",       "Low" },
            { "settings.quality.medium",    "Medium" },
            { "settings.quality.high",      "High" },
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
