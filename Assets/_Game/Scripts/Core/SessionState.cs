using System;
using Fields.Economy;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Fields.Core
{
    /// <summary>
    /// Single authority object for all mutable game state.
    /// UI reads exclusively from here — never from gameplay MonoBehaviours directly.
    /// Single-player is a session with one participant (playerId = 0).
    /// Co-op adds more participants; the interface stays identical.
    /// Survives scene reloads (DontDestroyOnLoad).
    /// </summary>
    public class SessionState : MonoBehaviour
    {
        public static SessionState Instance { get; private set; }

        // ------------------------------------------------------------------ //
        // Session identity
        // ------------------------------------------------------------------ //

        public bool IsMultiplayer { get; private set; }
        public int LocalPlayerId  { get; private set; }
        public int PlayerCount    => Players.Length;

        // ------------------------------------------------------------------ //
        // Economy
        // ------------------------------------------------------------------ //

        /// <summary>Shared wallet balance. Mirrors CurrencyManager.</summary>
        public int SharedWallet { get; private set; }

        /// <summary>Fired whenever the wallet changes. Args: (oldValue, newValue).</summary>
        public event Action<int, int> OnWalletChanged;

        // ------------------------------------------------------------------ //
        // Per-player and per-parcel data
        // ------------------------------------------------------------------ //

        public PlayerSessionData[] Players { get; private set; }
        public ParcelSessionData[]  Parcels { get; private set; }

        // ------------------------------------------------------------------ //
        // Playtime
        // ------------------------------------------------------------------ //

        /// <summary>Total unpaused gameplay seconds this session.</summary>
        public float TotalPlaytime { get; private set; }

        // ------------------------------------------------------------------ //
        // Lifecycle
        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            InitSession(multiplayer: false, playerCount: 1, localId: 0);
        }

        void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Managers' Awake() has run by this point — re-hook them.
            HookManagers();
        }

        void Start()
        {
            // First scene: Awake already ran but Start is the right place for
            // cross-object wiring since all Awakes are guaranteed complete.
            HookManagers();
        }

        void Update()
        {
            // Accumulate real gameplay time; freeze while paused.
            if (Time.timeScale > 0f)
                TotalPlaytime += Time.unscaledDeltaTime;
        }

        // ------------------------------------------------------------------ //
        // Public session control
        // ------------------------------------------------------------------ //

        public void StartSinglePlayer()
        {
            InitSession(multiplayer: false, playerCount: 1, localId: 0);
        }

        /// <summary>Call when a co-op session begins (Stage 2).</summary>
        public void StartMultiplayer(int playerCount, int localId)
        {
            InitSession(multiplayer: true, playerCount: playerCount, localId: localId);
        }

        // ------------------------------------------------------------------ //
        // Accessors
        // ------------------------------------------------------------------ //

        public PlayerSessionData GetPlayer(int id) =>
            id >= 0 && id < Players.Length ? Players[id] : null;

        public ParcelSessionData GetParcel(int index) =>
            index >= 0 && index < Parcels.Length ? Parcels[index] : null;

        // ------------------------------------------------------------------ //
        // Internal
        // ------------------------------------------------------------------ //

        void InitSession(bool multiplayer, int playerCount, int localId)
        {
            IsMultiplayer  = multiplayer;
            LocalPlayerId  = localId;
            TotalPlaytime  = 0f;

            Players = new PlayerSessionData[playerCount];
            for (int i = 0; i < playerCount; i++)
                Players[i] = new PlayerSessionData { PlayerId = i };

            Parcels = new ParcelSessionData[4];
            for (int i = 0; i < 4; i++)
                Parcels[i] = new ParcelSessionData { ParcelIndex = i };
        }

        void HookManagers()
        {
            var cm = CurrencyManager.Instance;
            if (cm != null)
            {
                cm.OnMoneyChanged -= OnMoneyChanged; // guard against double-subscribe
                cm.OnMoneyChanged += OnMoneyChanged;
                SharedWallet = cm.Money;
            }
        }

        void OnMoneyChanged(int oldVal, int newVal)
        {
            SharedWallet = newVal;
            OnWalletChanged?.Invoke(oldVal, newVal);
        }
    }

    // ======================================================================= //
    // Data containers
    // ======================================================================= //

    [Serializable]
    public class PlayerSessionData
    {
        public int PlayerId;

        // All counters are long — a player can cut millions of cells.
        public long AreaCutCells;
        public long HayPilesCollected;
        public long SquareBalesMade;
        public long RoundBalesMade;
        public long MoneyEarned;
        public long MoneySpent;
        public long TotalSwings;
        public double DistanceTravelledM;
        public float PlaytimeSeconds;
    }

    [Serializable]
    public class ParcelSessionData
    {
        public int   ParcelIndex;

        // Geometry
        public long  AreaCutCells;
        public long  AreaTotalCells;

        // Hay lifecycle
        public long  HayPilesSpawned;
        public long  HayPilesCollected;
        public long  HayPilesInField;   // = Spawned - Collected; shown highlighted in Journal

        // Bales
        public long  SquareBalesMade;
        public long  RoundBalesMade;

        // Economy & time
        public long  MoneyEarned;       // attributed at moment of sale to cutting parcel
        public float TimeSpentSeconds;

        // Completion: both conditions must be true simultaneously.
        public bool  GrassCutComplete;
        public bool  HayClearComplete;
        public bool  IsCompleted => GrassCutComplete && HayClearComplete;
    }
}
