// P2-01: Mirror NetworkManager subclass — player spawn, late-join grass sync.
#if MIRROR
using Mirror;
#endif

using UnityEngine;
using Fields.Grass;

namespace Fields.Network
{
    /// <summary>
    /// P2-01: Replaces Mirror's default NetworkManager on the scene GO.
    /// Handles player prefab spawning at spawn points and sends late-join
    /// grass snapshots to newly connected clients.
    /// </summary>
#if MIRROR
    public class FieldsNetworkManager : NetworkManager
    {
        [Header("Fields overrides")]
        public Transform[] spawnPoints;

        int _spawnIndex;

        // ------------------------------------------------------------------ //
        // Connection hooks
        // ------------------------------------------------------------------ //

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

            // Send grass grid snapshots for all parcels to the new client
            SendLateJoinSnapshots(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            _spawnIndex = Mathf.Max(0, _spawnIndex - 1);
        }

        // ------------------------------------------------------------------ //
        // Late-join: send all grass field states to the new client
        // ------------------------------------------------------------------ //

        void SendLateJoinSnapshots(NetworkConnectionToClient conn)
        {
            var syncs = FindObjectsByType<GrassNetSync>(FindObjectsSortMode.None);
            foreach (var s in syncs)
                s.SendGridSnapshot(conn);
        }
    }
#else
    public class FieldsNetworkManager : UnityEngine.MonoBehaviour { }
#endif
}
