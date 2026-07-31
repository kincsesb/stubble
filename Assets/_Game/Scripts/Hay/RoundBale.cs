using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Round bale — Rigidbody ~80 kg, rolls downhill on slopes ≥8°.
    /// Anti-pattern #8: rolling is INTENTIONAL gameplay — do NOT fix it.
    /// Player can push (collider), never carries (too heavy).
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

        Rigidbody _rb;
        bool _physicsApplied;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            ApplyPhysicsSettings();
        }

        void ApplyPhysicsSettings()
        {
            _rb.mass = mass;
            _rb.linearDamping = drag;
            _rb.angularDamping = angularDrag;
            // Freeze position until slope threshold hit — prevents micro-sliding on flat ground
            _rb.constraints = RigidbodyConstraints.FreezePosition;
        }

        void Update()
        {
            CheckSlope();
        }

        void CheckSlope()
        {
            if (!Physics.Raycast(transform.position, Vector3.down, out var hit, 0.8f)) return;

            float angle = Vector3.Angle(Vector3.up, hit.normal);
            if (angle >= rollSlopeThreshold && !_physicsApplied)
            {
                // Release position freeze — bale rolls freely
                _rb.constraints = RigidbodyConstraints.None;
                _physicsApplied = true;
            }
            else if (angle < rollSlopeThreshold * 0.5f && _physicsApplied
                     && _rb.linearVelocity.sqrMagnitude < 0.01f)
            {
                // Re-freeze once settled on flat ground (hysteresis: half threshold)
                _rb.constraints = RigidbodyConstraints.FreezePosition;
                _physicsApplied = false;
            }
        }

        void OnCollisionEnter(Collision col)
        {
            // Push player sideways — never topple (anti-pattern #7)
            var player = col.gameObject.GetComponent<Fields.Core.PlayerController>();
            if (player == null) return;

            Vector3 pushDir = (col.transform.position - transform.position).normalized;
            pushDir.y = 0f;
            player.ExternalPush(pushDir * _rb.linearVelocity.magnitude * 0.5f);
        }

        public int HayUnits => hayUnits;
    }
}
