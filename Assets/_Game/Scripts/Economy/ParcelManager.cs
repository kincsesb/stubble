using System;
using Fields.Core;
using Fields.Core.Data;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// Tracks which parcels are unlocked and handles gate opening.
    /// Parcel 0 (Home Paddock) is unlocked from the start.
    /// </summary>
    public class ParcelManager : MonoBehaviour
    {
        public static ParcelManager Instance { get; private set; }

        [Header("Parcel definitions (index matches parcel number 0-3)")]
        public ParcelData[] parcels = new ParcelData[4];

        [Header("Gates — one per parcel 1-3 (index 0 = gate to parcel 1)")]
        public ParcelGate[] gates = new ParcelGate[3];

        public event Action<int> OnParcelUnlocked;

        bool[] _unlocked = new bool[4];

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _unlocked[0] = true; // Home Paddock always available
        }

        public bool IsUnlocked(int index) => index >= 0 && index < 4 && _unlocked[index];

        public bool TryUnlock(int parcelIndex)
        {
            if (parcelIndex <= 0 || parcelIndex >= 4) return false;
            if (_unlocked[parcelIndex]) return false;
            var data = parcels[parcelIndex];
            if (data == null) return false;
            if (!CurrencyManager.Instance.TrySpend(data.unlockCost)) return false;
            _unlocked[parcelIndex] = true;
            if (parcelIndex - 1 < gates.Length && gates[parcelIndex - 1] != null)
                gates[parcelIndex - 1].Open();
            OnParcelUnlocked?.Invoke(parcelIndex);
            return true;
        }

        public bool[] GetUnlockedArray() => (bool[])_unlocked.Clone();
        public void LoadState(bool[] unlocked) => _unlocked = unlocked;
    }

    /// <summary>Placeholder gate with animation stub. Animator integration in P1.</summary>
    public class ParcelGate : MonoBehaviour, IInteractable
    {
        public bool IsOpen { get; private set; }

        public void Open()
        {
            IsOpen = true;
            // Animator.SetBool("Open", true) — wired in P1
            if (TryGetComponent<Collider>(out var col)) col.enabled = false;
        }

        public void Interact(PlayerController player)
        {
            // Could show "Unlock for X$" prompt — stubbed until Shop UI in P1
        }
    }
}