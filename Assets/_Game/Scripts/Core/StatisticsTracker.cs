using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fields.Core
{
    /// <summary>
    /// Subscribes to GameEvents and writes counters into SessionState.
    /// Never polls — purely event-driven.
    /// Owned by the same persistent GameObject as SessionState.
    /// Survives scene reloads (DontDestroyOnLoad via SessionState's GO).
    ///
    /// Time-in-parcel: accumulated in Update only while unpaused and
    /// the player is inside the parcel collider.
    /// </summary>
    public class StatisticsTracker : MonoBehaviour
    {
        public static StatisticsTracker Instance { get; private set; }

        // Per-parcel "player is currently inside" flags (index = parcelId).
        // In single-player only index 0 ever matters; in co-op there's one
        // array per player, but for now we track only localPlayer (id=0).
        readonly bool[] _insideParcel = new bool[4];

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
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
            // Reset in-parcel flags — player re-enters on scene load
            for (int i = 0; i < _insideParcel.Length; i++) _insideParcel[i] = false;
        }

        void Update()
        {
            if (Time.timeScale <= 0f) return; // don't tick while paused

            var ss = SessionState.Instance;
            if (ss == null) return;

            float dt = Time.deltaTime;

            for (int p = 0; p < _insideParcel.Length; p++)
            {
                if (!_insideParcel[p]) continue;
                var parcel = ss.GetParcel(p);
                if (parcel != null) parcel.TimeSpentSeconds += dt;

                // Per-player playtime (local player only in single-player)
                var player = ss.GetPlayer(ss.LocalPlayerId);
                if (player != null) player.PlaytimeSeconds += dt;
            }
        }

        // ------------------------------------------------------------------ //
        // Event subscriptions
        // ------------------------------------------------------------------ //

        void Subscribe()
        {
            GameEvents.OnGridCellCut   += OnGridCellCut;
            GameEvents.OnHayPileSpawned += OnHayPileSpawned;
            GameEvents.OnHayConsumed   += OnHayConsumed;
            GameEvents.OnBaleCreated   += OnBaleCreated;
            GameEvents.OnBaleSold      += OnBaleSold;
            GameEvents.OnToolPurchased += OnToolPurchased;
            GameEvents.OnToolUpgraded  += OnToolUpgraded;
            GameEvents.OnParcelUnlocked += OnParcelUnlocked;
            GameEvents.OnParcelEntered += OnParcelEntered;
            GameEvents.OnParcelExited  += OnParcelExited;
        }

        void Unsubscribe()
        {
            GameEvents.OnGridCellCut    -= OnGridCellCut;
            GameEvents.OnHayPileSpawned -= OnHayPileSpawned;
            GameEvents.OnHayConsumed    -= OnHayConsumed;
            GameEvents.OnBaleCreated    -= OnBaleCreated;
            GameEvents.OnBaleSold       -= OnBaleSold;
            GameEvents.OnToolPurchased  -= OnToolPurchased;
            GameEvents.OnToolUpgraded   -= OnToolUpgraded;
            GameEvents.OnParcelUnlocked -= OnParcelUnlocked;
            GameEvents.OnParcelEntered  -= OnParcelEntered;
            GameEvents.OnParcelExited   -= OnParcelExited;
        }

        // ------------------------------------------------------------------ //
        // Handlers
        // ------------------------------------------------------------------ //

        void OnGridCellCut(int parcelId, int playerId, float cellAreaM2)
        {
            var ss = SessionState.Instance;
            if (ss == null) return;

            var parcel = ss.GetParcel(parcelId);
            if (parcel != null) parcel.AreaCutCells++;

            var player = ss.GetPlayer(playerId);
            if (player != null) player.AreaCutCells++;
        }

        void OnHayPileSpawned(int parcelId, Vector3 _)
        {
            var parcel = SessionState.Instance?.GetParcel(parcelId);
            if (parcel != null)
            {
                parcel.HayPilesSpawned++;
                parcel.HayPilesInField++;
            }
        }

        void OnHayConsumed(int parcelId, int playerId)
        {
            var parcel = SessionState.Instance?.GetParcel(parcelId);
            if (parcel != null && parcel.HayPilesInField > 0)
            {
                parcel.HayPilesCollected++;
                parcel.HayPilesInField--;
            }

            var player = SessionState.Instance?.GetPlayer(playerId);
            if (player != null) player.HayPilesCollected++;
        }

        void OnBaleCreated(int parcelId, int playerId, bool isRound)
        {
            var ss = SessionState.Instance;
            if (ss == null) return;

            var parcel = ss.GetParcel(parcelId);
            if (parcel != null)
            {
                if (isRound) parcel.RoundBalesMade++;
                else         parcel.SquareBalesMade++;
            }

            var player = ss.GetPlayer(playerId);
            if (player != null)
            {
                if (isRound) player.RoundBalesMade++;
                else         player.SquareBalesMade++;
            }
        }

        void OnBaleSold(int parcelId, int playerId, int value)
        {
            var ss = SessionState.Instance;
            if (ss == null) return;

            var parcel = ss.GetParcel(parcelId);
            if (parcel != null) parcel.MoneyEarned += value;

            var player = ss.GetPlayer(playerId);
            if (player != null) player.MoneyEarned += value;
        }

        void OnToolPurchased(int playerId, int toolIndex)
        {
            var player = SessionState.Instance?.GetPlayer(playerId);
            if (player != null) player.MoneySpent +=
                GetToolCost(toolIndex); // best-effort; actual cost tracked by CurrencyManager
        }

        void OnToolUpgraded(int playerId, int toolIndex, int newLevel)
        {
            // Tool upgrade levels are already stored in ToolUnlockManager.
            // No SessionState counter needed beyond what SaveData already captures.
        }

        void OnParcelUnlocked(int parcelId, int cost)
        {
            var player = SessionState.Instance?.GetPlayer(SessionState.Instance.LocalPlayerId);
            if (player != null) player.MoneySpent += cost;
        }

        void OnParcelEntered(int parcelId, int playerId)
        {
            if (parcelId >= 0 && parcelId < _insideParcel.Length)
                _insideParcel[parcelId] = true;
        }

        void OnParcelExited(int parcelId, int playerId)
        {
            if (parcelId >= 0 && parcelId < _insideParcel.Length)
                _insideParcel[parcelId] = false;
        }

        // ------------------------------------------------------------------ //

        static int GetToolCost(int toolIndex)
        {
            var um = Economy.ToolUnlockManager.Instance;
            if (um == null || toolIndex < 0 || toolIndex >= um.allTools.Length) return 0;
            var data = um.allTools[toolIndex];
            return data != null ? data.purchaseCost : 0;
        }
    }
}