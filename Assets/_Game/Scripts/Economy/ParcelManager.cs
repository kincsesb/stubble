using System;
using Fields.Core.Data;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// Tracks which parcels are unlocked (progression + achievements).
    /// All parcels are physically accessible from the start — no gates.
    /// Parcel 0 is always unlocked; others unlock via shop purchase.
    /// </summary>
    public class ParcelManager : MonoBehaviour
    {
        public static ParcelManager Instance { get; private set; }

        [Header("Parcel definitions (index matches parcel number 0-3)")]
        public ParcelData[] parcels = new ParcelData[4];

        public event Action<int> OnParcelUnlocked;

        bool[] _unlocked = new bool[4];

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _unlocked[0] = true; // Parcel 0 always available
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
            OnParcelUnlocked?.Invoke(parcelIndex);
            Fields.Core.GameEvents.FireParcelUnlocked(parcelIndex, data.unlockCost);
            return true;
        }

        public bool[] GetUnlockedArray() => (bool[])_unlocked.Clone();
        public void LoadState(bool[] unlocked) => _unlocked = unlocked;
    }
}
