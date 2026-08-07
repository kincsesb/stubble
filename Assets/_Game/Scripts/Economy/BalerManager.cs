using Fields.Core.Data;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// Tracks round baler ownership, baler compression upgrade level, and hay value upgrade level.
    /// Spec §6.4: Hay Value multipliers 1.0×/1.25×/1.55×/1.90×.
    /// Spec §7.2: Round baler is a separate purchasable item.
    /// </summary>
    public class BalerManager : MonoBehaviour
    {
        public static BalerManager Instance { get; private set; }

        [Header("Round Baler data asset (assign in Inspector)")]
        public BalerData roundBalerData;

        public static readonly int[] BalerUpgradeCosts = { 200, 500, 1200 };
        public static readonly float[] HayValueMultipliers = { 1f, 1.25f, 1.55f, 1.90f };
        public static readonly int[] HayValueCosts = { 300, 700, 1500 };

        bool _roundBalerOwned;
        int _balerLevel;
        int _hayValueLevel;

        public bool RoundBalerOwned => _roundBalerOwned;
        public int BalerLevel => _balerLevel;
        public int HayValueLevel => _hayValueLevel;
        public float HayValueMultiplier => HayValueMultipliers[Mathf.Clamp(_hayValueLevel, 0, 3)];

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public bool TryPurchaseRoundBaler()
        {
            if (_roundBalerOwned || roundBalerData == null) return false;
            if (!CurrencyManager.Instance.TrySpend(roundBalerData.purchaseCost)) return false;
            _roundBalerOwned = true;
            TrackSpent(roundBalerData.purchaseCost);
            return true;
        }

        public bool TryUpgradeBaler()
        {
            if (_balerLevel >= 3) return false;
            int cost = BalerUpgradeCosts[_balerLevel];
            if (!CurrencyManager.Instance.TrySpend(cost)) return false;
            _balerLevel++;
            TrackSpent(cost);
            return true;
        }

        public bool TryUpgradeHayValue()
        {
            if (_hayValueLevel >= 3) return false;
            int cost = HayValueCosts[_hayValueLevel];
            if (!CurrencyManager.Instance.TrySpend(cost)) return false;
            _hayValueLevel++;
            TrackSpent(cost);
            return true;
        }

        static void TrackSpent(int amount)
        {
            var player = Fields.Core.SessionState.Instance?.GetPlayer(0);
            if (player != null) player.MoneySpent += amount;
        }

        // Save / Load
        public bool GetRoundBalerOwned() => _roundBalerOwned;
        public int[] GetLevels() => new int[] { _balerLevel, _hayValueLevel, 0 };

        public void LoadState(bool roundOwned, int[] levels)
        {
            _roundBalerOwned = roundOwned;
            if (levels != null && levels.Length > 0) _balerLevel    = Mathf.Clamp(levels[0], 0, 3);
            if (levels != null && levels.Length > 1) _hayValueLevel = Mathf.Clamp(levels[1], 0, 3);
        }
    }
}
