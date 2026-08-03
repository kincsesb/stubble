using System;
using System.IO;
using Fields.Save;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fields.Core
{
    /// <summary>
    /// Tracks and persists personal-best records (spec §6.3, §8.1).
    ///
    /// File: Application.persistentDataPath/records.json
    /// Separate from save_slot0.dat — survives New Game and save deletion.
    /// DontDestroyOnLoad: owned by the same persistent GO as SessionState.
    ///
    /// Tracked records:
    ///   - Fastest parcel completion (per parcel, time-in-parcel seconds)
    ///   - Largest area cut in a single session (m²)
    ///   - Most bales in one delivery trip
    ///   - Longest continuous cutting streak (gap > STREAK_BREAK_S breaks it)
    ///   - Full-game completion time (all 4 parcels done)
    ///
    /// Steam Leaderboards: stub slots for full-game time and fastest parcel.
    /// </summary>
    public class RecordsManager : MonoBehaviour
    {
        public static RecordsManager Instance { get; private set; }

        const string RECORDS_FILE    = "records.json";
        const float  STREAK_BREAK_S  = 3f;    // seconds of no cutting = streak broken
        const float  DELIVERY_WINDOW = 2f;    // sales within this window = one delivery
        const float  CELL_AREA_M2    = 0.16f; // 0.4 m × 0.4 m

        public RecordsData Data { get; private set; } = new RecordsData();

        // ── Runtime streak state ───────────────────────────────────────────── //
        bool  _streakActive;
        float _streakStartTime;
        float _lastCutTime;

        // ── Runtime delivery state ─────────────────────────────────────────── //
        int   _deliveryBaleCount;
        float _lastSaleTime;

        // ── Session area (reset each game start) ──────────────────────────── //
        long _sessionAreaCutCells;

        // ── Dirty flag — debounced 2 s write ──────────────────────────────── //
        bool  _dirty;
        float _dirtyTimer;
        const float SAVE_DEBOUNCE = 2f;

        // ================================================================== //
        // Lifecycle
        // ================================================================== //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            LoadRecords();
        }

        void OnEnable()
        {
            Subscribe();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDisable()
        {
            Unsubscribe();
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetSessionState();
        }

        void Update()
        {
            // ── Cutting streak expiry ──────────────────────────────────────── //
            if (_streakActive && Time.unscaledTime - _lastCutTime > STREAK_BREAK_S)
            {
                float duration = _lastCutTime - _streakStartTime;
                if (duration > Data.longestCuttingStreakSeconds)
                {
                    Data.longestCuttingStreakSeconds = duration;
                    MarkDirty();
                }
                _streakActive = false;
            }

            // ── Delivery burst expiry ──────────────────────────────────────── //
            if (_deliveryBaleCount > 0 &&
                Time.unscaledTime - _lastSaleTime > DELIVERY_WINDOW)
            {
                if (_deliveryBaleCount > Data.mostBalesDelivered)
                {
                    Data.mostBalesDelivered = _deliveryBaleCount;
                    MarkDirty();
                }
                _deliveryBaleCount = 0;
            }

            // ── Debounced save ─────────────────────────────────────────────── //
            if (_dirty)
            {
                _dirtyTimer += Time.unscaledDeltaTime;
                if (_dirtyTimer >= SAVE_DEBOUNCE)
                {
                    _dirty      = false;
                    _dirtyTimer = 0f;
                    SaveRecords();
                }
            }
        }

        void OnApplicationQuit() => SaveRecords();

        // ================================================================== //
        // Public — called from game-start flow
        // ================================================================== //

        /// <summary>Reset session-only counters when a new game or Continue starts.</summary>
        public void ResetSessionState()
        {
            _sessionAreaCutCells = 0;
            _streakActive        = false;
            _deliveryBaleCount   = 0;
        }

        // ================================================================== //
        // Event subscriptions
        // ================================================================== //

        void Subscribe()
        {
            GameEvents.OnGridCellCut     += OnCellCut;
            GameEvents.OnBaleSold        += OnBaleSold;
            GameEvents.OnParcelCompleted += OnParcelCompleted;
            GameEvents.OnFullGameCompleted += OnFullGameCompleted;
        }

        void Unsubscribe()
        {
            GameEvents.OnGridCellCut       -= OnCellCut;
            GameEvents.OnBaleSold          -= OnBaleSold;
            GameEvents.OnParcelCompleted   -= OnParcelCompleted;
            GameEvents.OnFullGameCompleted -= OnFullGameCompleted;
        }

        // ================================================================== //
        // Handlers
        // ================================================================== //

        void OnCellCut(int parcelId, int playerId, float cellAreaM2)
        {
            _sessionAreaCutCells++;

            double sessionM2 = _sessionAreaCutCells * CELL_AREA_M2;
            if (sessionM2 > Data.largestAreaCutM2)
            {
                Data.largestAreaCutM2 = sessionM2;
                MarkDirty();
            }

            float now = Time.unscaledTime;
            if (!_streakActive)
            {
                _streakActive    = true;
                _streakStartTime = now;
            }
            _lastCutTime = now;
        }

        void OnBaleSold(int parcelId, int playerId, int value)
        {
            _deliveryBaleCount++;
            _lastSaleTime = Time.unscaledTime;
        }

        void OnParcelCompleted(int parcelId, float timeSpentSeconds)
        {
            if (parcelId < 0 || parcelId >= Data.fastestParcelSeconds.Length) return;

            bool isFirst = Data.fastestParcelSeconds[parcelId] < 0f;
            if (isFirst || timeSpentSeconds < Data.fastestParcelSeconds[parcelId])
            {
                Data.fastestParcelSeconds[parcelId] = timeSpentSeconds;
                MarkDirty();
                SubmitParcelLeaderboard(parcelId, timeSpentSeconds);
            }
        }

        void OnFullGameCompleted(float totalPlaytimeSeconds)
        {
            bool isFirst = Data.fullGameCompletionSeconds < 0f;
            if (isFirst || totalPlaytimeSeconds < Data.fullGameCompletionSeconds)
            {
                Data.fullGameCompletionSeconds = totalPlaytimeSeconds;
                MarkDirty();
                SubmitFullGameLeaderboard(totalPlaytimeSeconds);
            }
        }

        // ================================================================== //
        // Steam Leaderboard stubs (wire when SteamManager supports it)
        // ================================================================== //

        static void SubmitParcelLeaderboard(int parcelId, float seconds)
        {
            int ms = Mathf.RoundToInt(seconds * 1000f);
            Debug.Log($"[RecordsManager] Steam leaderboard: FastestParcel_{parcelId} = {ms} ms");
            // SteamManager.Instance?.SubmitLeaderboard($"FastestParcel_{parcelId}", ms);
        }

        static void SubmitFullGameLeaderboard(float seconds)
        {
            int ms = Mathf.RoundToInt(seconds * 1000f);
            Debug.Log($"[RecordsManager] Steam leaderboard: FullGameTime = {ms} ms");
            // SteamManager.Instance?.SubmitLeaderboard("FullGameTime", ms);
        }

        // ================================================================== //
        // Load / Save
        // ================================================================== //

        public void LoadRecords()
        {
            string path = RecordsPath();
            if (!File.Exists(path)) return;
            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                Data = JsonUtility.FromJson<RecordsData>(json) ?? new RecordsData();
                // Guard against a corrupted array (e.g. fewer than 4 entries)
                if (Data.fastestParcelSeconds == null || Data.fastestParcelSeconds.Length < 4)
                    Data.fastestParcelSeconds = new float[] { -1f, -1f, -1f, -1f };
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RecordsManager] Load failed: {ex.Message}. Resetting records.");
                Data = new RecordsData();
            }
        }

        public void SaveRecords()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, prettyPrint: true);
                File.WriteAllText(RecordsPath(), json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RecordsManager] Save failed: {ex.Message}");
            }
        }

        // ================================================================== //
        // Helpers
        // ================================================================== //

        void MarkDirty()
        {
            _dirty      = true;
            _dirtyTimer = 0f;
        }

        static string RecordsPath() =>
            Path.Combine(Application.persistentDataPath, RECORDS_FILE);
    }
}
