using Fields.Economy;
using Fields.Save;
using Fields.UI;
using UnityEngine;

namespace Fields.Core
{
    /// <summary>
    /// Bootstrap: loads save data, wires autosave triggers, handles game-over/end screen.
    /// Placed on a persistent GameObject in the Game scene.
    /// Also routes ParcelBoundary player-enter events → HUDController.activeGrassField.
    /// </summary>
    public class WorldBootstrap : MonoBehaviour
    {
        public static WorldBootstrap Instance { get; private set; }

        [Header("References")]
        public SaveSystem saveSystem;
        public ParcelBoundary[] parcels = new ParcelBoundary[4];
        public GameObject endScreenRoot;

        [Header("Main menu")]
        public MainMenuScreen mainMenuScreen;
        public GameObject playerRoot;   // hidden until game starts

        int _completedParcels;

        /// <summary>Number of non-null ParcelBoundary entries actually wired in the Inspector.</summary>
        public int ActiveParcelCount
        {
            get
            {
                int n = 0;
                foreach (var p in parcels) if (p != null) n++;
                return Mathf.Max(1, n);
            }
        }

        public int CompletedParcels => _completedParcels;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            bool loaded = saveSystem != null && saveSystem.LoadGame();
            if (!loaded) Debug.Log("[WorldBootstrap] No save found — fresh game.");

            foreach (var p in parcels)
            {
                if (p == null) continue;
                p.OnParcelCompleted += OnParcelCompleted;
                p.OnPlayerEntered   += OnPlayerEnteredParcel;
            }

            if (endScreenRoot != null) endScreenRoot.SetActive(false);

            // Default active field = first parcel (parcel 0 is always unlocked)
            if (parcels.Length > 0 && parcels[0] != null && HUDController.Instance != null)
                HUDController.Instance.activeGrassField = parcels[0].grassField;

            // Show main menu — player stays hidden until Continue or New Game.
            if (mainMenuScreen != null)
            {
                if (playerRoot != null) playerRoot.SetActive(false);
                UIManager.Instance?.Push(mainMenuScreen);
            }
        }

        void OnDestroy()
        {
            foreach (var p in parcels)
            {
                if (p == null) continue;
                p.OnParcelCompleted -= OnParcelCompleted;
                p.OnPlayerEntered   -= OnPlayerEnteredParcel;
            }
        }

        void OnPlayerEnteredParcel(ParcelBoundary parcel)
        {
            if (HUDController.Instance != null)
                HUDController.Instance.activeGrassField = parcel.grassField;

            int idx = parcel.parcelData != null ? parcel.parcelData.parcelIndex : System.Array.IndexOf(parcels, parcel);
            if (Fields.Core.PlayerController.Instance != null)
                Fields.Core.PlayerController.Instance.CurrentParcelIndex = idx;
        }

        void OnParcelCompleted(ParcelBoundary parcel)
        {
            int idx = System.Array.IndexOf(parcels, parcel);
            _completedParcels++;
            saveSystem?.SaveGame();

            float parcelTime = SessionState.Instance?.GetParcel(idx)?.TimeSpentSeconds ?? 0f;
            GameEvents.FireParcelCompleted(idx, parcelTime);

            SteamManager.Instance?.OnParcelComplete(idx);
            if (_completedParcels >= ActiveParcelCount)
            {
                float totalTime = SessionState.Instance?.TotalPlaytime ?? 0f;
                GameEvents.FireFullGameCompleted(totalTime);

                SteamManager.Instance?.OnAllParcelsComplete();
                if (endScreenRoot != null) endScreenRoot.SetActive(true);
            }
        }
    }
}
