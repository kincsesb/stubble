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

            _completed = true;
            OnParcelCompleted?.Invoke(this);
        }

        public float CompletionPercent =>
            grassField != null ? grassField.GetCompletionPercent() : 0f;

        public bool IsCompleted => _completed;

        /// <summary>Resets completion flag so the parcel can be completed again (used by loop ending).</summary>
        public void ResetCompletion() => _completed = false;

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                OnPlayerEntered?.Invoke(this);
                int id = parcelData != null ? parcelData.parcelIndex : 0;
                Fields.Core.GameEvents.FireParcelEntered(id, 0);
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                OnPlayerExited?.Invoke(this);
                int id = parcelData != null ? parcelData.parcelIndex : 0;
                Fields.Core.GameEvents.FireParcelExited(id, 0);
            }
        }

        public event Action<ParcelBoundary> OnPlayerEntered;
        public event Action<ParcelBoundary> OnPlayerExited;
    }
}
