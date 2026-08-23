using System.Collections;
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

        /// <summary>Resets completion state after a loop ending so the field can be won again.</summary>
        public void ResetAllParcels()
        {
            _completedParcels = 0;
            foreach (var p in parcels)
                if (p != null) p.ResetCompletion();
        }

        /// <summary>Set before scene reload to skip the main menu and start a fresh game immediately.</summary>
        public static bool PendingFreshStart { get; set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        void Start()
        {
            bool loaded = saveSystem != null && saveSystem.LoadGame();

            // Stale post-ending save: all parcels 100% cut means the game was already completed.
            // The nuclear sequence should have deleted this save but may have failed (crash, Steam sync lag, etc.).
            // Wipe it here so the main menu shows fresh grass and the nuclear sequence can't auto-fire.
            if (loaded && saveSystem != null)
            {
                bool allComplete = true;
                foreach (var gf in saveSystem.grassFields)
                    if (gf == null || gf.GetCompletionPercent() < 99.9f) { allComplete = false; break; }
                if (allComplete)
                {
                    Debug.Log("[WorldBootstrap] Post-ending save detected — wiping and resetting to fresh game.");
                    saveSystem.DeleteSave();
                    saveSystem.DeleteBackup();
                    foreach (var gf in saveSystem.grassFields)
                        gf?.ResetGrass();
                    StartCoroutine(ResetGrassNextFrame());
                    loaded = false;
                }
            }

            if (!loaded)
            {
                Debug.Log("[WorldBootstrap] No save found — fresh game.");
                if (saveSystem != null)
                    foreach (var gf in saveSystem.grassFields)
                        gf?.ResetGrass();

                // One-frame delayed reset: GrassChunkManager.Start() and GrassField.Start()
                // may run in any order; this guarantees the GPU mask is rebuilt after all
                // MonoBehaviour Start() calls have completed.
                StartCoroutine(ResetGrassNextFrame());
            }

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

                if (PendingFreshStart)
                {
                    PendingFreshStart = false;
                    mainMenuScreen.StartGame(freshStart: true);
                }
                else
                {
                    // Wait one frame so UIManager.OnSceneLoaded (which clears the stack)
                    // fires before we push. This is reliable across Unity versions and builds.
                    StartCoroutine(ShowMainMenuNextFrame());
                }
            }
        }

        IEnumerator ShowMainMenuNextFrame()
        {
            yield return null;
            UIManager.Instance?.Push(mainMenuScreen);
        }

        IEnumerator ResetGrassNextFrame()
        {
            yield return null;
            if (saveSystem != null)
                foreach (var gf in saveSystem.grassFields)
                    gf?.ResetGrass();
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

            if (_completedParcels >= ActiveParcelCount)
            {
                float totalTime = SessionState.Instance?.TotalPlaytime ?? 0f;
                GameEvents.FireFullGameCompleted(totalTime);

                SteamManager.Instance?.OnAllFieldsComplete();
                // EndingOrchestrator handles when to show endScreenRoot
            }
        }
    }
}
