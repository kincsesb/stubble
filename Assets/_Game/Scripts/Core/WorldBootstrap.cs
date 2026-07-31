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
        [Header("References")]
        public SaveSystem saveSystem;
        public ParcelBoundary[] parcels = new ParcelBoundary[4];
        public GameObject endScreenRoot;

        int _completedParcels;

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
        }

        void OnParcelCompleted(ParcelBoundary parcel)
        {
            int idx = System.Array.IndexOf(parcels, parcel);
            _completedParcels++;
            saveSystem?.SaveGame();

            SteamManager.Instance?.OnParcelComplete(idx);
            if (_completedParcels >= 4)
            {
                SteamManager.Instance?.OnAllParcelsComplete();
                if (endScreenRoot != null) endScreenRoot.SetActive(true);
            }
        }
    }
}
