using Fields.Core;
using Fields.Grass;
using Fields.World;
using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Round bale physics controller — PhysX-driven rolling.
    ///
    /// Physics model:
    ///   SphereCollider + high friction → PhysX handles rolling, gravity, terrain following.
    ///   Player push = AddForce in heading direction.
    ///   Rolling resistance = counter-force applied in FixedUpdate.
    ///   Heading = derived from linearVelocity XZ when moving.
    ///   Visual child inherits root Rigidbody rotation directly (localRotation = identity).
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    public class RoundBaleController : MonoBehaviour
    {
        [Header("Config")]
        public RoundBaleConfig config;

        [Header("Hierarchy")]
        [Tooltip("Child transform with MeshRenderer — inherits root rotation, no override.")]
        public Transform visual;

        [Header("Rolling Audio")]
        public AudioSource rollAudioSource;
        [Tooltip("One clip per surface type: [0]=uncut grass, [1]=stubble, [2]=dirt, [3]=gravel")]
        public AudioClip[] rollSurfaceClips = new AudioClip[4];
        public float rollPitchScale  = 0.4f;
        public float rollVolumeScale = 0.8f;
        [Tooltip("Crossfade duration when surface changes (s)")]
        public float surfaceCrossfadeTime = 0.15f;

        [Header("Impact Audio")]
        public AudioClip[] impactClips = new AudioClip[5];
        public float heavyImpactThreshold = 4f;

        [Header("Particles")]
        public ParticleSystem dustParticles;

        // ── State ───────────────────────────────────────────────────────────── //
        Rigidbody _rb;
        float   _speed;       // XZ speed magnitude, derived from rb.linearVelocity
        Vector3 _heading;     // XZ unit vector, derived from velocity when moving
        float   _rollAngle;   // accumulated degrees (for save/load continuity)

        // ── Ground ──────────────────────────────────────────────────────────── //
        Vector3 _groundNormal = Vector3.up;
        byte    _surfaceIndex;

        // ── Push interaction ─────────────────────────────────────────────────── //
        bool             _isPushed;
        PlayerController _pusher;
        float            _pendingForward;
        float            _pendingSide;

        // ── Audio ────────────────────────────────────────────────────────────── //
        int         _activeSurface = -1;
        float       _crossfadeTimer;
        AudioSource _impactSource;

        // ── Feel ─────────────────────────────────────────────────────────────── //
        float     _postCollisionTimer;
        Transform _cameraTransform;

        // ── Grass field lookup ────────────────────────────────────────────────── //
        GrassField[] _grassFields;

        // ================================================================== //

        void Awake()
        {
            _rb              = GetComponent<Rigidbody>();
            _cameraTransform = Camera.main?.transform;
            _impactSource    = gameObject.AddComponent<AudioSource>();
            _impactSource.spatialBlend = 1f;
            // Must initialise here so SetStateFromSave (called right after Instantiate) can use it.
            _grassFields = Object.FindObjectsByType<GrassField>(FindObjectsSortMode.None);
        }

        void Start()
        {
            if (config == null) { Debug.LogError("[RoundBaleController] config is not assigned!", this); return; }
            ApplyRigidbodySettings();
            _heading = transform.forward;
            _heading.y = 0f;
            if (_heading.sqrMagnitude < 0.001f) _heading = Vector3.forward;
            _heading.Normalize();
        }

        void ApplyRigidbodySettings()
        {
            _rb.mass               = config.mass;
            _rb.linearDamping      = 0f;
            _rb.angularDamping     = 3f;   // prevents chaotic spinning while allowing natural rolling
            _rb.useGravity         = true;
            _rb.constraints        = RigidbodyConstraints.None;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.interpolation      = RigidbodyInterpolation.Interpolate;

            // SphereCollider radius in local space (root scale = 3)
            float localRadius = config.Radius / Mathf.Max(transform.lossyScale.x, 0.001f);
            var sphere = GetComponent<SphereCollider>();
            if (sphere != null)
            {
                sphere.radius = localRadius;
                sphere.center = Vector3.zero;
                // High friction so PhysX creates torque → natural rolling, not sliding
                sphere.sharedMaterial = new PhysicsMaterial("BaleRoll")
                {
                    dynamicFriction = 0.6f,
                    staticFriction  = 0.6f,
                    bounciness      = 0f,
                    frictionCombine = PhysicsMaterialCombine.Multiply,
                    bounceCombine   = PhysicsMaterialCombine.Minimum,
                };
            }
        }

        // ================================================================== //
        // Push API
        // ================================================================== //

        public void StartPush(PlayerController pusher)
        {
            _isPushed = true;
            _pusher   = pusher;
        }

        public void StopPush()
        {
            _isPushed = false;
            _pusher   = null;
        }

        public void ApplyPushInput(float forwardInput, float sideInput)
        {
            _pendingForward = forwardInput;
            _pendingSide    = sideInput;
        }

        // ================================================================== //
        // FixedUpdate
        // ================================================================== //

        void FixedUpdate()
        {
            if (config == null) return;

            SampleGround();
            UpdateHeading();

            if (_isPushed) ConsumePushInput();

            ApplyRollingResistance();
            ApplyQuadraticDamping();
            ClampVelocity();
            UpdateRollAngle();
            UpdateAudio();
            UpdateDust();

            if (_postCollisionTimer > 0f) _postCollisionTimer -= Time.fixedDeltaTime;
        }

        // ── Ground sampling (surface type only — height handled by PhysX) ── //

        void SampleGround()
        {
            var sampler = TerrainSampler.Instance;
            if (sampler == null) return;
            var gs = sampler.Sample(transform.position);
            if (!gs.valid) return;
            _groundNormal = gs.normal;
            _surfaceIndex = ResolveSurface(gs.surfaceIndex, transform.position);
        }

        byte ResolveSurface(byte terrainSurface, Vector3 pos)
        {
            if (terrainSurface != 0) return terrainSurface;
            if (_grassFields == null) return 0;
            foreach (var f in _grassFields)
                if (f != null && f.ContainsWorldPoint(pos))
                    return f.IsPositionCut(pos) ? (byte)1 : (byte)0;
            return 0;
        }

        // ── Heading — derived from velocity when moving ──────────────────── //

        void UpdateHeading()
        {
            Vector3 vel = _rb.linearVelocity;
            vel.y = 0f;
            _speed = vel.magnitude;
            if (_speed > 0.3f)
                _heading = vel / _speed;
        }

        // ── Push input ───────────────────────────────────────────────────── //

        void ConsumePushInput()
        {
            if (Mathf.Abs(_pendingForward) > 0.1f)
            {
                float force = _pendingForward * config.pushAcceleration * _rb.mass;
                _rb.AddForce(_heading * force, ForceMode.Force);
            }

            if (Mathf.Abs(_pendingSide) > 0.1f)
            {
                // Steer: sideways force perpendicular to heading
                Vector3 sideways = Vector3.Cross(Vector3.up, _heading).normalized;
                float force = _pendingSide * config.pushAcceleration * 0.4f * _rb.mass;
                _rb.AddForce(sideways * force, ForceMode.Force);
            }

            _pendingForward = _pendingSide = 0f;
        }

        // ── Rolling resistance (counter-force opposing linear velocity) ───── //

        void ApplyRollingResistance()
        {
            Vector3 vel = _rb.linearVelocity;
            float speed = vel.magnitude;
            if (speed < 0.001f) return;

            float r = GetResistance(_surfaceIndex);
            // Cap so it doesn't overshoot zero in one step
            float maxDecel = speed / Time.fixedDeltaTime;
            float decel    = Mathf.Min(r, maxDecel);
            _rb.AddForce(-vel.normalized * decel * _rb.mass, ForceMode.Force);
        }

        float GetResistance(byte surface) => surface switch
        {
            1 => config.resistanceCutStubble,
            2 => config.resistanceDirt,
            3 => config.resistanceGravel,
            _ => config.resistanceUncutGrass,
        };

        // ── Quadratic damping above threshold ────────────────────────────── //

        void ApplyQuadraticDamping()
        {
            float speed = _rb.linearVelocity.magnitude;
            if (speed <= config.quadDampThreshold) return;
            float excess = speed - config.quadDampThreshold;
            _rb.AddForce(-_rb.linearVelocity.normalized * config.quadDampCoeff * excess * excess * _rb.mass, ForceMode.Force);
        }

        void ClampVelocity()
        {
            Vector3 vel   = _rb.linearVelocity;
            Vector3 velXZ = new Vector3(vel.x, 0f, vel.z);
            if (velXZ.magnitude > config.speedCap)
            {
                velXZ = velXZ.normalized * config.speedCap;
                _rb.linearVelocity = new Vector3(velXZ.x, vel.y, velXZ.z);
            }
        }

        // ── Roll angle for save continuity (from angular velocity) ─────── //

        void UpdateRollAngle()
        {
            _rollAngle += _rb.angularVelocity.magnitude * Mathf.Rad2Deg * Time.fixedDeltaTime;
            _rollAngle %= 360f;
        }

        // ================================================================== //
        // Collisions
        // ================================================================== //

        void OnCollisionEnter(Collision col)
        {
            _postCollisionTimer = config.postCollisionExemptTime;

            // Bale-to-bale: apply impulse transfer
            var otherCtrl = col.gameObject.GetComponentInParent<RoundBaleController>();
            if (otherCtrl != null && otherCtrl != this)
            {
                float transferImpulse = _speed * config.baleToBaleTransfer * _rb.mass;
                otherCtrl._rb.AddForce(_heading * transferImpulse, ForceMode.Impulse);
                _rb.AddForce(-_heading * transferImpulse * config.baleToBaleTransfer, ForceMode.Impulse);
                PlayImpact(_speed * config.baleToBaleTransfer);
                return;
            }

            // Player: displace laterally
            var player = col.gameObject.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                Vector3 pushDir = col.transform.position - transform.position;
                pushDir.y = 0f;
                if (pushDir.sqrMagnitude > 0.001f)
                    player.SetPositionXZ(player.transform.position + pushDir.normalized * 0.5f);
                return;
            }

            // Light prop (rigidbody, not a bale)
            var propRb = col.gameObject.GetComponentInParent<Rigidbody>();
            if (propRb != null && col.gameObject.GetComponentInParent<RoundBale>() == null)
            {
                propRb.AddForce(_heading * config.mass * Mathf.Abs(_speed), ForceMode.Impulse);
                return;
            }

            // Static geometry
            if (col.contactCount == 0) return;
            Vector3 contactNormal = col.contacts[0].normal;
            float dot = Vector3.Dot(_heading, contactNormal);
            float lostSpeed = Mathf.Abs(_speed) * config.headOnSpeedLoss;
            PlayImpact(lostSpeed);
            ShakeCamera(lostSpeed);
            TriggerHapticImpact(lostSpeed);
        }

        void PlayImpact(float relativeSpeed)
        {
            if (impactClips == null || impactClips.Length == 0) return;
            float t   = Mathf.Clamp01(relativeSpeed / Mathf.Max(0.001f, config.speedCap));
            int   idx = relativeSpeed >= heavyImpactThreshold
                        ? impactClips.Length - 1
                        : Mathf.Clamp(Mathf.FloorToInt(t * (impactClips.Length - 1)), 0, impactClips.Length - 2);
            if (impactClips[idx] != null) _impactSource.PlayOneShot(impactClips[idx]);
        }

        void ShakeCamera(float lostSpeed)
        {
            if (_cameraTransform == null) return;
            if ((_cameraTransform.position - transform.position).sqrMagnitude >
                config.cameraShakeProximity * config.cameraShakeProximity) return;
            var gfc = Fields.Feel.GameFeelController.Instance;
            if (gfc == null) return;
            gfc.StartShake(0.25f, lostSpeed * config.cameraShakeScalePerSpeed, 18f);
            gfc.TriggerBaleImpact(lostSpeed);   // fires MMF_Player for heavy impacts
        }

        void TriggerHapticImpact(float lostSpeed)
        {
#if ENABLE_INPUT_SYSTEM
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null) return;
            float intensity = Mathf.Clamp01(lostSpeed / config.speedCap) * config.hapticRumbleScale;
            pad.SetMotorSpeeds(intensity * 0.5f, intensity);
#endif
        }

        // ================================================================== //
        // Feel
        // ================================================================== //

        void UpdateAudio()
        {
            if (rollAudioSource == null) return;
            float absSpeed = _speed;

            int targetSurface = Mathf.Clamp(_surfaceIndex, 0, rollSurfaceClips.Length - 1);
            if (targetSurface != _activeSurface)
            {
                _crossfadeTimer += Time.fixedDeltaTime;
                if (_crossfadeTimer >= surfaceCrossfadeTime)
                {
                    _activeSurface  = targetSurface;
                    _crossfadeTimer = 0f;
                    var clip = _activeSurface < rollSurfaceClips.Length ? rollSurfaceClips[_activeSurface] : null;
                    if (clip != null) { rollAudioSource.clip = clip; }
                }
            }
            else _crossfadeTimer = 0f;

            if (absSpeed > 0.05f)
            {
                if (!rollAudioSource.isPlaying) rollAudioSource.Play();
                rollAudioSource.pitch  = 1f + absSpeed * rollPitchScale;
                rollAudioSource.volume = Mathf.Clamp01(absSpeed / config.speedCap) * rollVolumeScale;
            }
            else if (rollAudioSource.isPlaying) rollAudioSource.Stop();

#if ENABLE_INPUT_SYSTEM
            if (_cameraTransform != null && absSpeed > 0.5f)
            {
                float dist = (_cameraTransform.position - transform.position).magnitude;
                if (dist < config.cameraShakeProximity)
                {
                    float r = (1f - dist / config.cameraShakeProximity)
                              * (absSpeed / config.speedCap) * config.hapticRumbleScale * 0.15f;
                    UnityEngine.InputSystem.Gamepad.current?.SetMotorSpeeds(r, 0f);
                }
            }
#endif
        }

        void UpdateDust()
        {
            if (dustParticles == null) return;
            var em = dustParticles.emission;
            em.rateOverTime = _speed > 0.1f ? _speed * 3f : 0f;
        }

        // ================================================================== //
        // Public queries
        // ================================================================== //

        public float   Speed     => _speed;
        public Vector3 Heading   => _heading;
        public float   RollAngle => _rollAngle;

        public void SetStateFromSave(Vector3 heading, float rollAngle)
        {
            if (_grassFields == null)
                _grassFields = Object.FindObjectsByType<GrassField>(FindObjectsSortMode.None);
            _heading = heading;
            _heading.y = 0f;
            if (_heading.sqrMagnitude < 0.001f) _heading = Vector3.forward;
            _heading.Normalize();
            _rollAngle = rollAngle;
            _speed     = 0f;
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }
    }
}
