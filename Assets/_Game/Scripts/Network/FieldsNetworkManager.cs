using Mirror;
using kcp2k;
using UnityEngine;
using Fields.Grass;

namespace Fields.Network
{
    /// <summary>
    /// P2-01: Replaces Mirror's default NetworkManager on the scene GO.
    /// Handles player prefab spawning at spawn points and sends late-join
    /// grass snapshots to newly connected clients.
    /// </summary>
    public class FieldsNetworkManager : NetworkManager
    {
        [Header("Fields overrides")]
        public Transform[] spawnPoints;
        [Tooltip("Bale prefab used by EconomyNetSync when a client requests a bale spawn")]
        public GameObject balePrefab;

        int _spawnIndex;

        public override void Start()
        {
            base.Start();
            // If Steam is not running (editor / no Steam), auto-start as local host using KCP
            // so that Mirror's NetworkScenePostProcess re-enables scene objects (terrains, etc.)
#if STEAMWORKS_NET
            bool steamUp = Steamworks.SteamAPI.IsSteamRunning();
#else
            bool steamUp = false;
#endif
            if (!steamUp)
            {
                var kcp = GetComponent<KcpTransport>() ?? gameObject.AddComponent<KcpTransport>();
                Transport.active = kcp;
                autoCreatePlayer = false; // scene already has the player GO; don't spawn a second one
                StartHost();
            }
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            Transform sp = spawnPoints != null && spawnPoints.Length > 0
                ? spawnPoints[_spawnIndex % spawnPoints.Length]
                : null;

            Vector3 pos = sp != null ? sp.position : Vector3.zero;
            Quaternion rot = sp != null ? sp.rotation : Quaternion.identity;

            GameObject player = Instantiate(playerPrefab, pos, rot);
            NetworkServer.AddPlayerForConnection(conn, player);
            _spawnIndex++;

            SendLateJoinSnapshots(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            _spawnIndex = Mathf.Max(0, _spawnIndex - 1);
        }

        void SendLateJoinSnapshots(NetworkConnectionToClient conn)
        {
            var syncs = FindObjectsByType<GrassNetSync>(FindObjectsSortMode.None);
            foreach (var s in syncs)
                s.SendGridSnapshot(conn);
        }
    }
}
