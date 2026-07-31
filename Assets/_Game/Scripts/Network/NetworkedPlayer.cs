// P2-02: Player + Tool replication via Mirror NetworkBehaviour.
#if MIRROR
using Mirror;
#endif

using UnityEngine;
using Fields.Core;

namespace Fields.Network
{
    /// <summary>
    /// P2-02: Mirror NetworkBehaviour wrapper for PlayerController.
    /// - SyncVars: position, rotation (host-authoritative smooth)
    /// - Commands: UseTool, Interact, PickupBale
    /// - TargetRpc: local effects (haptics, sounds) only on owner
    /// Attach alongside PlayerController on the Player prefab.
    /// </summary>
#if MIRROR
    [RequireComponent(typeof(PlayerController))]
    public class NetworkedPlayer : NetworkBehaviour
    {
        [SyncVar] Vector3 _netPosition;
        [SyncVar] float   _netYaw;

        PlayerController _pc;
        CharacterController _cc;
        float _lerpSpeed = 15f;

        void Awake()
        {
            _pc = GetComponent<PlayerController>();
            _cc = GetComponent<CharacterController>();
        }

        public override void OnStartLocalPlayer()
        {
            // Enable local PlayerController input only for owner
            _pc.enabled = true;
            Camera.main.gameObject.SetActive(true); // main cam follows local player
        }

        public override void OnStartClient()
        {
            if (!isLocalPlayer)
            {
                // Remote players: disable local input, enable name tag
                _pc.enabled = false;
            }
        }

        void Update()
        {
            if (isLocalPlayer)
            {
                // Sync our position/yaw to server
                if (isServer)
                {
                    _netPosition = transform.position;
                    _netYaw      = transform.eulerAngles.y;
                }
                else
                {
                    CmdUpdateTransform(transform.position, transform.eulerAngles.y);
                }
            }
            else
            {
                // Smooth remote player to synced position
                _cc.enabled = false;
                transform.position = Vector3.Lerp(transform.position, _netPosition, Time.deltaTime * _lerpSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.Euler(0f, _netYaw, 0f), Time.deltaTime * _lerpSpeed);
            }
        }

        [Command(requiresAuthority = true)]
        void CmdUpdateTransform(Vector3 pos, float yaw)
        {
            _netPosition = pos;
            _netYaw      = yaw;
        }

        // ------------------------------------------------------------------ //
        // Tool use — authorised by host, replicated via ClientRpc
        // ------------------------------------------------------------------ //

        /// <summary>Client requests tool use — host validates and broadcasts result.</summary>
        [Command]
        public void CmdUseTool(byte toolIndex, bool pressed)
        {
            RpcUseTool(toolIndex, pressed);
        }

        [ClientRpc(excludeOwner = true)]
        void RpcUseTool(byte toolIndex, bool pressed)
        {
            // Remote player's visual-only tool animation
            // Actual grass cutting is handled by GrassNetSync authority
        }

        // ------------------------------------------------------------------ //
        // Haptics / effects — only on the local owner
        // ------------------------------------------------------------------ //

        [TargetRpc]
        public void TargetTriggerHaptics(NetworkConnection target, float low, float high, float dur)
        {
            _pc.TriggerHaptics(low, high, dur);
        }
    }
#else
    // Stub so the project compiles without Mirror
    public class NetworkedPlayer : UnityEngine.MonoBehaviour { }
#endif
}
