using System;
using UnityEngine;

namespace Fields.Economy
{
    /// <summary>
    /// Single-player authority for money. Co-op P2 replaces this with
    /// NetworkBehaviour + NetworkVariable<int> and ServerRpc validation.
    /// </summary>
    public class CurrencyManager : MonoBehaviour
    {
        public static CurrencyManager Instance { get; private set; }

        public event Action<int, int> OnMoneyChanged; // (oldValue, newValue)

        int _money;
        public int Money => _money;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        public void SetMoney(int amount)
        {
            int old = _money;
            _money = Mathf.Max(0, amount);
            if (old != _money) OnMoneyChanged?.Invoke(old, _money);
        }

        public void Earn(int amount)
        {
            if (amount <= 0) return;
            int old = _money;
            _money += amount;
            OnMoneyChanged?.Invoke(old, _money);
            Fields.Audio.ToolAudioManager.Instance?.PlayMoney();
            Fields.Core.SteamManager.Instance?.OnEarnMoney(_money);
        }

        public bool TrySpend(int amount)
        {
            if (_money < amount) return false;
            int old = _money;
            _money -= amount;
            OnMoneyChanged?.Invoke(old, _money);
            return true;
        }
    }
}