using System;
using Fields.Core.Data;
using Fields.Grass;
using Fields.Hay;
using UnityEngine;

namespace Fields.Core
{
    /// <summary>
    /// One parcel: boundary trigger, completion tracking, and references to
    /// its GrassField and HayAccumulationSystem.
    /// Completion condition: 100% grass cut AND 0 HayPiles remaining.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class ParcelBoundary : MonoBehaviour
    {
        [Header("Data")]
        public ParcelData parcelData;

        [Header("Systems")]
        public GrassField grassField;
        public HayAccumulationSystem haySystem;

        public event Action<ParcelBoundary> OnParcelCompleted;

        bool _completed;

        // ------------------------------------------------------------------ //

        void Start()
        {
            // TerrainCollider cannot be a trigger — use the dedicated BoxCollider
            var box = GetComponent<BoxCollider>();
            if (box != null) box.isTrigger = true;
        }

        void Update()
        {
            if (_completed) return;
            CheckCompletion();
        }

        void CheckCompletion()
        {
            if (grassField == null) return;
            if (grassField.GetCompletionPercent() < 99.9f) return;

            // Check no HayPiles remain in parcel
            var piles = FindObjectsByType<Hay.HayPile>(FindObjectsSortMode.None);
            foreach (var pile in piles)
            {
                if (pile.IsCarried) continue;
                // Check if this pile is within our bounds
                var col = GetComponent<Collider>();
                if (col.bounds.Contains(pile.transform.position)) return;
            }

            _completed = true;
            OnParcelCompleted?.Invoke(this);
        }

        public float CompletionPercent =>
            grassField != null ? grassField.GetCompletionPercent() : 0f;

        public bool IsCompleted => _completed;

        void OnTriggerEnter(Collider other)
        {
            // Used by HUD to set activeGrassField when player enters parcel
            if (other.CompareTag("Player"))
                OnPlayerEntered?.Invoke(this);
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
                OnPlayerExited?.Invoke(this);
        }

        public event Action<ParcelBoundary> OnPlayerEntered;
        public event Action<ParcelBoundary> OnPlayerExited;
    }
}
