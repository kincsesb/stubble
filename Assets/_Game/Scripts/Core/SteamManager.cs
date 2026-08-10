// Steamworks.NET is installed via OpenUPM: com.rlabrecque.steamworks.net
// All Steam calls are guarded by STEAMWORKS_NET so the project compiles without it.
#if STEAMWORKS_NET
using Steamworks;
#endif

using System;
using UnityEngine;
using Fields.Save;

namespace Fields.Core
{
    /// <summary>
    /// Initialises Steam, manages achievements, wraps Cloud save.
    /// Requires Steamworks.NET (OpenUPM: com.rlabrecque.steamworks.net).
    /// Achievement API names must match the app's Steamworks partner dashboard.
    /// </summary>
    public class SteamManager : MonoBehaviour
    {
        public static SteamManager Instance { get; private set; }

        // Achievement string IDs — must match Steamworks partner dashboard exactly.
        // Client to provide final list; placeholders below.
        public static class Achievements
        {
            // ── Core progression ────────────────────────────────────────────── //
            public const string FIRST_CUT        = "ACH_FIRST_CUT";
            public const string ALL_FIELDS        = "ACH_ALL_FIELDS";      // All grass cut (replaces parcel system)
            public const string FIRST_BALE        = "ACH_FIRST_BALE";
            public const string TEN_BALES         = "ACH_TEN_BALES";
            public const string HUNDRED_BALES     = "ACH_HUNDRED_BALES";
            public const string FIRST_SALE        = "ACH_FIRST_SALE";
            public const string EARN_100          = "ACH_EARN_100";
            public const string EARN_1000         = "ACH_EARN_1000";
            public const string EARN_10000        = "ACH_EARN_10000";
            public const string PERFECT_FIELD     = "ACH_PERFECT_FIELD";   // 100% complete

            // ── Tools ────────────────────────────────────────────────────────── //
            public const string BUY_SCYTHE        = "ACH_BUY_SCYTHE";
            public const string BUY_TRIMMER       = "ACH_BUY_TRIMMER";
            public const string BUY_PUSH_MOWER    = "ACH_BUY_PUSH_MOWER";
            public const string BUY_RIDE_ON       = "ACH_BUY_RIDE_ON";
            public const string MAX_UPGRADE       = "ACH_MAX_UPGRADE";

            // ── Baling & rolling ─────────────────────────────────────────────── //
            public const string ROUND_BALE        = "ACH_ROUND_BALE";
            public const string BALE_ROLL         = "ACH_BALE_ROLL";       // Round bale rolls > 10 m
            public const string BALE_MARATHON     = "ACH_BALE_MARATHON";   // Round bale rolls > 30 m total
            public const string BIG_DELIVERY      = "ACH_BIG_DELIVERY";    // Sell 10+ bales in one trip

            // ── Speed & skill ────────────────────────────────────────────────── //
            public const string SPEED_RUN         = "ACH_SPEED_RUN";       // Full game under 30 min
            public const string THE_HARD_WAY      = "ACH_THE_HARD_WAY";    // 200 swings with bare hand

            // ── Co-op ────────────────────────────────────────────────────────── //
            public const string COOP_CUT          = "ACH_COOP_CUT";
            public const string COOP_BALE         = "ACH_COOP_BALE";

            // ── Secret / easter egg ──────────────────────────────────────────── //
            public const string HESOYAM           = "ACH_HESOYAM";         // Used hesoyam cheat
            public const string MOTHERLODE        = "ACH_MOTHERLODE";      // Used motherlode cheat
            public const string DOOM              = "ACH_DOOM";            // Used iddqd cheat
            public const string IT_WAS_MORTAL     = "ACH_IT_WAS_MORTAL";   // IDDQD expired naturally
            public const string KONAMI_CODE        = "ACH_KONAMI_CODE";     // Typed konami in AI terminal
            public const string MEET_AI            = "ACH_MEET_AI";         // Opened AI mode
            public const string XYZZY              = "ACH_XYZZY";           // Used xyzzy cheat
            public const string PET_THE_CAT        = "ACH_PET_THE_CAT";     // Petted cat 10 times
            public const string IDLE_WATCHER       = "ACH_IDLE_WATCHER";    // Stood still 10 min
            public const string SUNRISE            = "ACH_SUNRISE";         // Play at 6 AM real time
        }

        // ── Achievement thresholds (referenced by game systems) ──────────── //
        public static class Thresholds
        {
            public const int   BALES_TEN          = 10;
            public const int   BALES_HUNDRED      = 100;
            public const int   EARN_100           = 100;
            public const int   EARN_1000          = 1000;
            public const int   EARN_10000         = 10000;
            public const float BALE_ROLL_SHORT_M  = 10f;
            public const float BALE_ROLL_LONG_M   = 30f;
            public const int   BIG_DELIVERY_BALES = 10;
            public const int   HARD_WAY_SWINGS    = 200;
            public const int   PET_CAT_COUNT      = 10;
            public const float IDLE_SECONDS       = 600f;   // 10 minutes
            public const float SPEEDRUN_SECONDS   = 1800f;  // 30 minutes
        }

        // ------------------------------------------------------------------ //

#if STEAMWORKS_NET
        bool _steamInitialised;
        Callback<UserAchievementStored_t> _achCallback;
#endif

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitSteam();
        }

        void OnDestroy()
        {
#if STEAMWORKS_NET
            if (_steamInitialised) SteamAPI.Shutdown();
#endif
        }

        void Update()
        {
#if STEAMWORKS_NET
            if (_steamInitialised) SteamAPI.RunCallbacks();
#endif
        }

        // ------------------------------------------------------------------ //

        void InitSteam()
        {
#if STEAMWORKS_NET
            if (!Packsize.Test()) { Debug.LogError("[Steam] Wrong Steamworks.NET Packsize — 64-bit mismatch."); return; }
            if (!DllCheck.Test())  { Debug.LogError("[Steam] DLL check failed."); return; }
            try
            {
                _steamInitialised = SteamAPI.Init();
                if (!_steamInitialised) { Debug.LogWarning("[Steam] SteamAPI.Init() failed — Steam not running or app not registered."); return; }
                _achCallback = Callback<UserAchievementStored_t>.Create(OnAchievementStored);
                SteamUserStats.RequestCurrentStats();
                Debug.Log($"[Steam] Initialised — AppID {SteamUtils.GetAppID()} — User: {SteamFriends.GetPersonaName()}");
            }
            catch (Exception e) { Debug.LogException(e); }
#endif
        }

        /// <summary>Unlocks a Steam achievement. Safe to call before Steam is ready — silently ignored.</summary>
        public void UnlockAchievement(string achievementId, bool triggerFeel = true)
        {
#if STEAMWORKS_NET
            if (!_steamInitialised) return;
            SteamUserStats.SetAchievement(achievementId);
            SteamUserStats.StoreStats();
#else
            Debug.Log($"[Steam stub] Achievement unlocked: {achievementId}");
#endif
            if (triggerFeel)
                Fields.Feel.GameFeelController.Instance?.TriggerAchievementFeel();
        }

        /// <summary>Upload save data to Steam Cloud. Overwrites previous slot.</summary>
        public void CloudSave(string json)
        {
#if STEAMWORKS_NET
            if (!_steamInitialised) return;
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
            SteamRemoteStorage.FileWrite("save_slot0.json", bytes, bytes.Length);
#endif
        }

        /// <summary>Download save data from Steam Cloud. Returns null if not found.</summary>
        public string CloudLoad()
        {
#if STEAMWORKS_NET
            if (!_steamInitialised) return null;
            const string FILE = "save_slot0.json";
            if (!SteamRemoteStorage.FileExists(FILE)) return null;
            int size = SteamRemoteStorage.GetFileSize(FILE);
            byte[] bytes = new byte[size];
            SteamRemoteStorage.FileRead(FILE, bytes, size);
            return System.Text.Encoding.UTF8.GetString(bytes);
#else
            return null;
#endif
        }

        // ------------------------------------------------------------------ //

#if STEAMWORKS_NET
        void OnAchievementStored(UserAchievementStored_t pCallback)
        {
            Debug.Log($"[Steam] Achievement stored: {pCallback.m_rgchAchievementName}");
        }
#endif

        // ------------------------------------------------------------------ //
        // Convenience wrappers called from game systems
        // ------------------------------------------------------------------ //

        public void OnFirstCut()           => UnlockAchievement(Achievements.FIRST_CUT);
        public void OnAllFieldsComplete()  => UnlockAchievement(Achievements.ALL_FIELDS);
        public void OnFirstBale()          => UnlockAchievement(Achievements.FIRST_BALE);
        public void OnFirstSale()          => UnlockAchievement(Achievements.FIRST_SALE);
        public void OnEarnMoney(int total)
        {
            if (total >= 100)   UnlockAchievement(Achievements.EARN_100);
            if (total >= 1000)  UnlockAchievement(Achievements.EARN_1000);
            if (total >= 10000) UnlockAchievement(Achievements.EARN_10000);
        }
        public void OnToolPurchased(string toolName)
        {
            switch (toolName)
            {
                case "StringTrimmer": UnlockAchievement(Achievements.BUY_TRIMMER);   break;
                case "PushMower":     UnlockAchievement(Achievements.BUY_PUSH_MOWER); break;
                case "RideOnMower":   UnlockAchievement(Achievements.BUY_RIDE_ON);   break;
                case "LongScythe":    UnlockAchievement(Achievements.BUY_SCYTHE);    break;
            }
        }
        public void OnMaxUpgrade() => UnlockAchievement(Achievements.MAX_UPGRADE);
        public void OnRoundBale()  => UnlockAchievement(Achievements.ROUND_BALE);
        public void OnCoopCut()    => UnlockAchievement(Achievements.COOP_CUT);
    }
}
