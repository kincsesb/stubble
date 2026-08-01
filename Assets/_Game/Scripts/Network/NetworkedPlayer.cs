using Mirror;
using UnityEngine;
using Fields.Core;
using Fields.Tools;

namespace Fields.Network
{
    /// <summary>
    /// P2-02: Mirror NetworkBehaviour wrapper for PlayerController.
    /// SyncVars: position, yaw. Commands: UseTool. TargetRpc: haptics on owner only.
    /// Attach alongside PlayerController on the Player prefab.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class NetworkedPlayer : NetworkBehaviour
    {
        [SyncVar] Vector3 _netPosition;
        [SyncVar] float   _netYaw;

        PlayerController _pc;
        CharacterController _cc;
        ToolHolder _toolHolder;
        float _lerpSpeed = 15f;

        void Awake()
        {
            _pc         = GetComponent<PlayerController>();
            _cc         = GetComponent<CharacterController>();
            _toolHolder = GetComponentInChildren<ToolHolder>();
        }

        public override void OnStartLocalPlayer()
        {
            _pc.enabled = true;
            Camera.main.gameObject.SetActive(true);

            if (_toolHolder != null)
                _toolHolder.OnToolAction += OnLocalToolAction;
        }

        void OnDestroy()
        {
            if (_toolHolder != null)
                _toolHolder.OnToolAction -= OnLocalToolAction;
        }

        void OnLocalToolAction(int toolIndex, bool pressed)
        {
            CmdUseTool((byte)toolIndex, pressed);
        }

        public override void OnStartClient()
        {
            if (!isLocalPlayer)
                _pc.enabled = false;
        }

        void Update()
        {
            if (isLocalPlayer)
            {
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

        [Command]
        public void CmdUseTool(byte toolIndex, bool pressed)
        {
            RpcUseTool(toolIndex, pressed);
        }

        [ClientRpc(includeOwner = false)]
        void RpcUseTool(byte toolIndex, bool pressed)
        {
            _toolHolder?.ForceUsePrimary(toolIndex, pressed);
        }

        [TargetRpc]
        public void TargetTriggerHaptics(NetworkConnection target, float low, float high, float dur)
        {
            _pc.TriggerHaptics(low, high, dur);
        }
    }
}
