using Mirror;
using UnityEngine;
using Fields.Grass;
using Fields.Save;

namespace Fields.Network
{
    /// <summary>
    /// P2-03: Attaches to each GrassField GO alongside GrassField component.
    /// Host validates all cut events; clients replay locally from the same data
    /// so GPU mask stays identical on all machines without RT sync.
    /// </summary>
    public class GrassNetSync : NetworkBehaviour
    {
        GrassField _field;
        int _parcelIndex;

        void Awake()
        {
            _field = GetComponent<GrassField>();
        }

        public void SetParcelIndex(int idx) => _parcelIndex = idx;

        public override void OnStartClient()
        {
            if (!isServer && _field != null)
            {
                _field.CutAreaInterceptor    = (pos, r)  => { CmdCutArea(pos, r);     return true; };
                _field.CutCapsuleInterceptor = (f, t, r) => { CmdCutCapsule(f, t, r); return true; };
            }
        }

        void OnDestroy()
        {
            if (_field != null)
            {
                _field.CutAreaInterceptor    = null;
                _field.CutCapsuleInterceptor = null;
            }
        }

        public void RequestCutArea(Vector3 worldPos, float radius)
        {
            if (isServer) ApplyCutArea(worldPos, radius);
            else          CmdCutArea(worldPos, radius);
        }

        public void RequestCutCapsule(Vector3 from, Vector3 to, float radius)
        {
            if (isServer) ApplyCutCapsule(from, to, radius);
            else          CmdCutCapsule(from, to, radius);
        }

        [Command(requiresAuthority = false)]
        void CmdCutArea(Vector3 pos, float radius) => RpcCutArea(pos, radius);

        [Command(requiresAuthority = false)]
        void CmdCutCapsule(Vector3 from, Vector3 to, float radius) => RpcCutCapsule(from, to, radius);

        [ClientRpc]
        void RpcCutArea(Vector3 pos, float radius)
        {
            if (!isServer) ApplyCutArea(pos, radius);
        }

        [ClientRpc]
        void RpcCutCapsule(Vector3 from, Vector3 to, float radius)
        {
            if (!isServer) ApplyCutCapsule(from, to, radius);
        }

        void ApplyCutArea(Vector3 pos, float radius)          => _field?.CutArea(pos, radius);
        void ApplyCutCapsule(Vector3 f, Vector3 t, float r)   => _field?.CutCapsule(f, t, r);

        [Server]
        public void SendGridSnapshot(NetworkConnection conn)
        {
            if (_field == null) return;
            var grid = _field.GetCutGrid();
            int[] rle = RLEEncoder.Encode(grid, _field.GridCols, _field.GridRows);
            TargetReceiveGridSnapshot(conn, rle, _field.GridCols, _field.GridRows);
        }

        [TargetRpc]
        void TargetReceiveGridSnapshot(NetworkConnection target, int[] rle, int cols, int rows)
        {
            bool[,] grid = RLEEncoder.Decode(rle, cols, rows);
            _field?.LoadCutGrid(grid);
        }
    }
}
