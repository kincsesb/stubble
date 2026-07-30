using System;
using Fields.Core.Data;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// Tracks which tools are owned and their upgrade levels.
    /// Co-op P2 wraps this in NetworkList<bool> / NetworkList<int> with ServerRpc.
    /// </summary>
    public class ToolUnlockManager : MonoBehaviour
    {
        public static ToolUnlockManager Instance { get; private set; }

        [Header("All 5 ToolData assets (order matches ToolType enum)")]
        public ToolData[] allTools = new ToolData[5];

        public event Action<int> OnToolUnlocked;           // toolIndex
        public event Action<int, int> OnToolUpgraded;      // toolIndex, newLevel

        // Parallel arrays — index matches ToolType enum
        bool[] _toolsOwned = new bool[5];
        int[] _toolLevels = new int[5];

        // Hand Sickle always owned from start
        const int HAND_SICKLE_INDEX = 0;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _toolsOwned[HAND_SICKLE_INDEX] = true;
        }

        // ------------------------------------------------------------------ //

        public bool IsOwned(int toolIndex) => toolIndex >= 0 && toolIndex < 5 && _toolsOwned[toolIndex];
        public int GetLevel(int toolIndex) => toolIndex >= 0 && toolIndex < 5 ? _toolLevels[toolIndex] : 0;

        public bool TryPurchase(int toolIndex)
        {
            if (toolIndex < 0 || toolIndex >= 5) return false;
            if (_toolsOwned[toolIndex]) return false;
            var data = allTools[toolIndex];
            if (data == null) return false;
            if (!CurrencyManager.Instance.TrySpend(data.purchaseCost)) return false;
            _toolsOwned[toolIndex] = true;
            OnToolUnlocked?.Invoke(toolIndex);
            return true;
        }

        public bool TryUpgrade(int toolIndex)
        {
            if (!IsOwned(toolIndex)) return false;
            int current = _toolLevels[toolIndex];
            if (current >= 3) return false;
            var data = allTools[toolIndex];
            if (data == null || data.upgradeCosts.Length <= current) return false;
            if (!CurrencyManager.Instance.TrySpend(data.upgradeCosts[current])) return false;
            _toolLevels[toolIndex]++;
            OnToolUpgraded?.Invoke(toolIndex, _toolLevels[toolIndex]);
            return true;
        }

        // Save / load support
        public bool[] GetOwnedArray() => (bool[])_toolsOwned.Clone();
        public int[] GetLevelsArray() => (int[])_toolLevels.Clone();
        public void LoadState(bool[] owned, int[] levels)
        {
            _toolsOwned = owned;
            _toolLevels = levels;
        }
    }
}