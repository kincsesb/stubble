using Fields.Economy;
using Fields.Save;
using UnityEngine;

namespace Fields.Core
{
    /// <summary>
    /// Bootstrap: loads save data, wires autosave triggers, handles game-over/end screen stub.
    /// Placed on a persistent GameObject in the Game scene.
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
            // Load existing save — if none, fresh game
            bool loaded = saveSystem != null && saveSystem.LoadGame();
            if (!loaded) Debug.Log("[WorldBootstrap] No save found — fresh game.");

            foreach (var p in parcels)
            {
                if (p == null) continue;
                p.OnParcelCompleted += OnParcelCompleted;
            }

            if (endScreenRoot != null) endScreenRoot.SetActive(false);
        }

        void OnParcelCompleted(ParcelBoundary parcel)
        {
            _completedParcels++;
            saveSystem?.SaveGame();

            // When all 4 parcels done → end screen
            if (_completedParcels >= 4)
            {
                if (endScreenRoot != null) endScreenRoot.SetActive(true);
                // P1-05 will add stats table population
            }
        }
    }
}
