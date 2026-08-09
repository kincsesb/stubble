using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Round bale — Rigidbody ~80 kg, rolls downhill on slopes ≥8°.
    /// Anti-pattern #8: rolling is INTENTIONAL gameplay — do NOT fix it.
    /// Player can push (collider), never carries (too heavy).
    ///
    /// Push interaction:
    ///   W/S  — rolls the bale straight forward/backward (translation + angular vel)
    ///   A/D  — rotates the bale in place around Y axis (no translation)
    ///   W/S and A/D are mutually exclusive per frame.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class RoundBale : MonoBehaviour
    {
        [Header("Physics")]
        public float mass = 80f;
        public float drag = 1.5f;
        public float angularDrag = 2.0f;
        [Tooltip("Slope angle in degrees at which rolling begins")]
        public float rollSlopeThreshold = 8f;

        [Header("Contents")]
        public int hayUnits = 60;

        [Header("Push interaction")]
        [Tooltip("Linear speed (m/s) when player rolls the bale")]
        public float pushSpeed = 2.5f;
        [Tooltip("Rotation speed (deg/s) when player spins the bale in place")]
        public float rotateSpeed = 90f;
        [Tooltip("Max distance from bale centre for player to start pushing")]
        public float interactRadius = 3.0f;

        Rigidbody _rb;
        SphereCollider _sphere;
        bool _physicsApplied;

        // Push state
        bool _beingPushed;
        Fields.Core.PlayerController _pusher;
        Vector3 _pusherDir; // XZ unit vector: bale→player (player's side of the bale)

        // ------------------------------------------------------------------ //

        void Awake()
        {
            _rb     = GetComponent<Rigidbody>();
            _sphere = GetComponent<SphereCollider>();
            ApplyPhysicsSettings();
        }

        void ApplyPhysicsSettings()
        {
            _rb.mass          = mass;
            _rb.linearDamping = drag;
            _rb.angularDamping = angularDrag;
            _rb.constraints   = RigidbodyConstraints.FreezePosition;
        }

        void Update()
        {
            if (!_beingPushed)
                CheckSlope();
        }

        void CheckSlope()
        {
            if (!Physics.Raycast(transform.position, Vector3.down, out var hit, 0.8f + WorldRadius)) return;

            float angle = Vector3.Angle(Vector3.up, hit.normal);
            if (angle >= rollSlopeThreshold && !_physicsApplied)
            {
                _rb.constraints  = RigidbodyConstraints.None;
                _physicsApplied  = true;
            }
            else if (angle < rollSlopeThreshold * 0.5f && _physicsApplied
                     && _rb.linearVelocity.sqrMagnitude < 0.01f)
            {
                _rb.constraints  = RigidbodyConstraints.FreezePosition;
                _physicsApplied  = false;
            }
        }

        void OnCollisionEnter(Collision col)
        {
            var player = col.gameObject.GetComponent<Fields.Core.PlayerController>();
            if (player == null) return;
            Vector3 pushDir = (col.transform.position - transform.position).normalized;
            pushDir.y = 0f;
            player.ExternalPush(pushDir * _rb.linearVelocity.magnitude * 0.5f);
        }

        // ------------------------------------------------------------------ //
        // Push interaction
        // ------------------------------------------------------------------ //

        float WorldRadius => (_sphere != null ? _sphere.radius : 0.5f) * transform.localScale.x;

        public bool CanStartPush(Fields.Core.PlayerController player)
        {
            if (_beingPushed) return false;
            float dx = player.transform.position.x - transform.position.x;
            float dz = player.transform.position.z - transform.position.z;
            return (dx * dx + dz * dz) <= interactRadius * interactRadius;
        }

        public void StartPush(Fields.Core.PlayerController player)
        {
            _beingPushed    = true;
            _pusher         = player;
            _physicsApplied = false;
            _rb.constraints = RigidbodyConstraints.None;

            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            _pusherDir = toPlayer.sqrMagnitude > 0.01f ? toPlayer.normalized : Vector3.forward;
        }

        public void StopPush()
        {
            _beingPushed    = false;
            _pusher         = null;
            _rb.linearVelocity    = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.constraints = RigidbodyConstraints.FreezePosition;
            _physicsApplied = false;
        }

        /// <summary>
        /// Called by PlayerController every frame while the player holds the push button.
        /// forwardInput: W(+1)/S(-1), sideInput: D(+1)/A(-1). Mutually exclusive per frame.
        /// </summary>
        public void PushUpdate(float forwardInput, float sideInput)
        {
            if (!_beingPushed || _pusher == null) return;

            bool rolling  = Mathf.Abs(forwardInput) > 0.1f;
            bool rotating = Mathf.Abs(sideInput)    > 0.1f && !rolling;

            if (rolling)
            {
                _rb.constraints = RigidbodyConstraints.None;

                // Bale moves in the direction the pusher is facing
                Vector3 fwd = _pusher.transform.forward * Mathf.Sign(forwardInput);
                float   spd = pushSpeed * Mathf.Abs(forwardInput);

                _rb.linearVelocity = new Vector3(fwd.x, _rb.linearVelocity.y, fwd.z) * spd;

                // Angular: axis perpendicular to motion so the bale visually rolls
                Vector3 rollAxis = Vector3.Cross(Vector3.up, fwd);
                _rb.angularVelocity = rollAxis * (spd / Mathf.Max(WorldRadius, 0.1f));

                // Player is always on the opposite side of travel
                _pusherDir = -fwd;
                SnapPusherToEdge();
            }
            else if (rotating)
            {
                // Freeze XZ translation, allow Y (gravity)
                _rb.constraints = RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
                _rb.linearVelocity    = Vector3.zero;
                _rb.angularVelocity = new Vector3(0f, sideInput * rotateSpeed * Mathf.Deg2Rad, 0f);

                // Player stays at their current side while the bale spins
                SnapPusherToEdge();
            }
            else
            {
                // No input — dampen to rest
                _rb.linearVelocity        = Vector3.Lerp(_rb.linearVelocity,        Vector3.zero, Time.deltaTime * 8f);
                _rb.angularVelocity = Vector3.Lerp(_rb.angularVelocity, Vector3.zero, Time.deltaTime * 8f);
                _rb.constraints     = RigidbodyConstraints.FreezePosition;
            }
        }

        void SnapPusherToEdge()
        {
            if (_pusher == null) return;
            float edgeDist = WorldRadius + 0.6f; // bale surface + player body offset
            Vector3 target = transform.position + _pusherDir * edgeDist;
            target.y = _pusher.transform.position.y;
            _pusher.SetPositionXZ(target);
        }

        public int HayUnits => hayUnits;
    }
}