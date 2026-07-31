// P2-04: Host-authoritative economy + bale spawning sync.
#if MIRROR
using Mirror;
#endif

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
#if MIRROR
    public class EconomyNetSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnMoneySync))]
        int _syncedMoney;

        // ------------------------------------------------------------------ //
        // Money sync
        // ------------------------------------------------------------------ //

        public override void OnStartServer()
        {
            if (CurrencyManager.Instance != null)
                CurrencyManager.Instance.OnMoneyChanged += OnServerMoneyChanged;
        }

        void OnServerMoneyChanged(int oldVal, int newVal)
        {
            _syncedMoney = newVal;
        }

        void OnMoneySync(int oldVal, int newVal)
        {
            // Only update on non-host clients (host drives CurrencyManager directly)
            if (!isServer) CurrencyManager.Instance?.SetMoney(newVal);
        }

        // ------------------------------------------------------------------ //
        // Purchase / sell — client asks host
        // ------------------------------------------------------------------ //

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
        public void CmdSellBales(int roundCount, int squareCount)
        {
            // SaleStand logic runs on host
            int earnings = (roundCount * 10) + (squareCount * 8); // placeholder prices
            CurrencyManager.Instance?.Earn(earnings);
        }

        // ------------------------------------------------------------------ //
        // Bale spawn — host spawns, Mirror replicates to all clients
        // ------------------------------------------------------------------ //

        [Server]
        public void ServerSpawnHayPile(Vector3 pos, GameObject pilePrefab)
        {
            if (pilePrefab == null) return;
            var go = Instantiate(pilePrefab, pos, Quaternion.identity);
            NetworkServer.Spawn(go);
        }

        [Server]
        public void ServerSpawnBale(Vector3 pos, Quaternion rot, GameObject balePrefab)
        {
            if (balePrefab == null) return;
            var go = Instantiate(balePrefab, pos, rot);
            NetworkServer.Spawn(go);
        }
    }
#else
    // Stub — compiles without Mirror; spawns directly (single-player behaviour)
    public class EconomyNetSync : UnityEngine.MonoBehaviour
    {
        public void ServerSpawnHayPile(UnityEngine.Vector3 pos, UnityEngine.GameObject prefab)
        {
            if (prefab != null) UnityEngine.Object.Instantiate(prefab, pos, UnityEngine.Quaternion.identity);
        }
        public void ServerSpawnBale(UnityEngine.Vector3 pos, UnityEngine.Quaternion rot, UnityEngine.GameObject prefab)
        {
            if (prefab != null) UnityEngine.Object.Instantiate(prefab, pos, rot);
        }
    }
#endif
}
