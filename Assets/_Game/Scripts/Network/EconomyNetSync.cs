using Mirror;
using UnityEngine;
using Fields.Economy;
using Fields.Hay;

namespace Fields.Network
{
    /// <summary>
    /// P2-04: Host owns economy state. Clients request purchases/sales via Command.
    /// CurrencyManager runs only on host; clients receive money updates via SyncVar.
    /// Bale/HayPile spawning is host-authoritative — NetworkServer.Spawn used throughout.
    /// </summary>
    public class EconomyNetSync : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnMoneySync))]
        int _syncedMoney;

        public override void OnStartClient()
        {
            if (isServer) return;

            foreach (var baler in Object.FindObjectsByType<Baler>(FindObjectsSortMode.None))
                baler.SpawnBaleInterceptor = (pos, rot, prefab) => { CmdSpawnBale(pos, rot); return true; };

            foreach (var hay in Object.FindObjectsByType<HayAccumulationSystem>(FindObjectsSortMode.None))
                hay.SpawnHayPileInterceptor = pos => { CmdSpawnHayPile(pos); return true; };
        }

        void OnDestroy()
        {
            foreach (var baler in Object.FindObjectsByType<Baler>(FindObjectsSortMode.None))
                if (baler.SpawnBaleInterceptor != null) baler.SpawnBaleInterceptor = null;
            foreach (var hay in Object.FindObjectsByType<HayAccumulationSystem>(FindObjectsSortMode.None))
                if (hay.SpawnHayPileInterceptor != null) hay.SpawnHayPileInterceptor = null;
        }

        [Command(requiresAuthority = false)]
        void CmdSpawnBale(Vector3 pos, Quaternion rot)
        {
            var mgr = FieldsNetworkManager.singleton as FieldsNetworkManager;
            if (mgr != null) ServerSpawnBale(pos, rot, mgr.balePrefab);
        }

        [Command(requiresAuthority = false)]
        void CmdSpawnHayPile(Vector3 pos)
        {
            var mgr = FieldsNetworkManager.singleton as FieldsNetworkManager;
            if (mgr != null) ServerSpawnHayPile(pos, mgr.hayPilePrefab);
        }

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
            int earnings = (roundCount * 10) + (squareCount * 8);
            CurrencyManager.Instance?.Earn(earnings);
        }

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
}
