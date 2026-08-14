using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Round bale identity component. Preserves the PlayerController-facing API
    /// (CanStartPush / StartPush / StopPush / PushUpdate / HayUnits).
    /// All motion is handled by RoundBaleController on this same GO.
    /// Anti-pattern #8: rolling downhill is INTENTIONAL — do NOT fix it.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(RoundBaleController))]
    public class RoundBale : MonoBehaviour
    {
        [Header("Contents")]
        public int hayUnits = 60;

        [Header("Push interaction")]
        [Tooltip("Max distance for player to start pushing (m)")]
        public float interactRadius = 3.0f;

        RoundBaleController _ctrl;
        bool _beingPushed;
        Fields.Core.PlayerController _pusher;

        void Awake()
        {
            _ctrl = GetComponent<RoundBaleController>();
        }

        // ------------------------------------------------------------------ //
        // PlayerController API — signatures must not change
        // ------------------------------------------------------------------ //

        public bool CanStartPush(Fields.Core.PlayerController player)
        {
            if (_beingPushed) return false;
            float dx = player.transform.position.x - transform.position.x;
            float dz = player.transform.position.z - transform.position.z;
            return dx * dx + dz * dz <= interactRadius * interactRadius;
        }

        public void StartPush(Fields.Core.PlayerController player)
        {
            _beingPushed = true;
            _pusher      = player;
            _ctrl?.StartPush(player);
        }

        public void StopPush()
        {
            _beingPushed = false;
            _pusher      = null;
            _ctrl?.StopPush();
        }

        /// <summary>
        /// Called every frame by PlayerController while push button held.
        /// forwardInput: W(+1) / S(-1) | sideInput: D(+1) / A(-1).
        /// </summary>
        public void PushUpdate(float forwardInput, float sideInput)
        {
            if (!_beingPushed || _ctrl == null) return;
            _ctrl.ApplyPushInput(forwardInput, sideInput);
            SnapPusherToEdge();
        }

        void SnapPusherToEdge()
        {
            if (_pusher == null || _ctrl == null || _ctrl.config == null) return;
            float edgeDist = _ctrl.config.Radius + 0.6f;
            // Keep pusher behind the bale (opposite to travel direction)
            Vector3 behind = -_ctrl.Heading;
            Vector3 target = transform.position + behind * edgeDist;
            target.y = _pusher.transform.position.y;
            _pusher.SetPositionXZ(target);
        }

        public int     HayUnits    => hayUnits;
        public Vector3 GetHeading()    => _ctrl != null ? _ctrl.Heading   : transform.forward;
        public float   GetRollAngle()  => _ctrl != null ? _ctrl.RollAngle : 0f;
    }
}