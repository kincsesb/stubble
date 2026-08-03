using Mirror;
using UnityEngine;
using Fields.Economy;
using Fields.Hay;

namespace Fields.Network
{
    /// <summary>
    /// P2-04: Host owns economy state. Clients request purchases/sales via Command.
    /// CurrencyManager runs only on host; clients receive money updates via SyncVar.
    /// Bale spawning is host-authoritative — NetworkServer.Spawn used throughout.
    /// </summary>
    public class EconomyNetSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnMoneySync))]
        int _syncedMoney;

        public override void OnStartServer()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnMoneyChanged += OnServerMoneyChanged;
        }

        void OnServerMoneyChanged(int oldVal, int newVal) => _syncedMoney = newVal;

        void OnMoneySync(int oldVal, int newVal)
        {
            if (!isServer) CurrencyManager.Instance?.SetMoney(newVal);
        }

        [Command(requiresAuthority = false)]
        void CmdSpawnBale(Vector3 pos, Quaternion rot)
        {
            var mgr = FieldsNetworkManager.singleton as FieldsNetworkManager;
            if (mgr != null) ServerSpawnBale(pos, rot, mgr.balePrefab);
        }

        [Command(requiresAuthority = false)]
        public void CmdRequestPurchaseTool(int toolIndex)
        {
            ToolUnlockManager.Instance?.TryPurchase(toolIndex);
        }

        [Command(requiresAuthority = false)]
        public void CmdRequestUpgradeTool(int toolIndex)
        {
            ToolUnlockManager.Instance?.TryUpgrade(toolIndex);
        }

        [Command(requiresAuthority = false)]
        public void CmdSellBales(int squareCount)
        {
            int earnings = squareCount * 8;
            CurrencyManager.Instance?.Earn(earnings);
        }

        [Server]
        public void ServerSpawnBale(Vector3 pos, Quaternion rot, GameObject balePrefab)
        {
            if (balePrefab == null) return;
            var go = Instantiate(balePrefab, pos, rot);
            NetworkServer.Spawn(go);
        }
    }
}
