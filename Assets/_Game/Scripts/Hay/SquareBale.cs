using Fields.Core;
using UnityEngine;

namespace Fields.Hay
{
    public interface IPickupable
    {
        bool CanPickup(Transform byPlayer);
        void OnPickup(Transform carrier, int stackIndex);
        void OnDrop(Vector3 worldPosition);
    }

    /// <summary>
    /// Square bale — stackable, player carries 1/2/3.
    /// When carried, parented to the player root (not camera child) so it
    /// stays horizontal regardless of camera pitch.
    /// Stack layout: each additional bale stacks on top at +baleHeight offset.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(BoxCollider))]
    public class SquareBale : MonoBehaviour, IPickupable, IInteractable
    {
        [Header("Stack")]
        [Tooltip("How many hay units this bale contains")]
        public int hayUnits = 60;
        [Tooltip("Stack index when player holds multiple (0 = bottom)")]
        public int stackIndex;

        [Header("Throw Physics")]
        [Tooltip("Tumble spin speed in radians/s when thrown — end-over-end rotation")]
        public float throwAngularSpeed   = 7f;
        [Tooltip("Bounciness of the bale after throw (0 = no bounce, 1 = full bounce)")]
        [Range(0f, 1f)]
        public float throwBounciness     = 0.35f;
        [Tooltip("Linear damping after throw (lower = slides/rolls farther)")]
        public float throwLinearDamping  = 0.2f;
        [Tooltip("Angular damping after throw (lower = spins longer)")]
        public float throwAngularDamping = 1.0f;

        [Header("Carry offsets")]
        [Tooltip("Forward distance from player root when carried")]
        public float carryForward   = 0.70f;
        [Tooltip("Height of the BOTTOM bale center above player root Y")]
        public float carryBaseHeight = 1.30f;
        [Tooltip("Height added per additional bale in the stack")]
        public float carryStackStep  = 0.55f;

        [Header("Throw Audio")]
        public AudioClip throwClip;
        public AudioClip landClip;
        [Range(0f, 1f)]
        public float throwVolume = 0.8f;
        [Range(0f, 1f)]
        public float landVolume  = 0.9f;
        [Tooltip("Impact speed (m/s) below which the land sound is skipped")]
        public float landMinSpeed       = 0.8f;
        [Tooltip("Impact speed (m/s) at which landVolume is fully reached")]
        public float landFullVolumeSpeed = 6f;
        [Range(0.5f, 2f)]
        public float landPitchMin = 0.85f;
        [Range(0.5f, 2f)]
        public float landPitchMax = 1.15f;
        [Tooltip("Min seconds between successive bounce sounds")]
        public float landCooldown = 0.08f;

        /// <summary>Parcel where the hay was cut. Set by PlayerController on spawn.</summary>
        public int OriginParcelIndex { get; set; } = 0;

        /// <summary>True when made with the round baler upgrade (4× sale value).</summary>
        public bool IsRound { get; set; } = false;

        Rigidbody _rb;
        bool _isCarried;
        AudioSource _audioSource;
        float _lastLandTime = -999f;

        static PhysicsMaterial _throwMat;

        // Throw metadata
        bool    _wasThrown;
        Vector3 _throwOrigin;
        string  _throwerName;

        public bool    WasThrown    => _wasThrown;
        public Vector3 ThrowOrigin  => _throwOrigin;
        public string  ThrowerName  => _throwerName;

        // ── Outline (highlight nearest interactable bale) ───────────────── //
        GameObject   _outlineGO;
        static Material _outlineMat;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.playOnAwake  = false;
            BuildOutline();
        }

        void BuildOutline()
        {
            var visual = transform.Find("Visual");
            var mf = visual != null ? visual.GetComponent<MeshFilter>() : GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;

            var outlineParent = visual != null ? visual.transform : transform;
            _outlineGO = new GameObject("BaleOutline");
            _outlineGO.transform.SetParent(outlineParent, worldPositionStays: false);
            _outlineGO.transform.localPosition = Vector3.zero;
            _outlineGO.transform.localRotation = Quaternion.identity;
            _outlineGO.transform.localScale = Vector3.one * 1.07f;

            var mfOut = _outlineGO.AddComponent<MeshFilter>();
            mfOut.sharedMesh = mf.sharedMesh;

            var mrOut = _outlineGO.AddComponent<MeshRenderer>();
            if (_outlineMat == null)
            {
                _outlineMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
                {
                    name = "BaleOutline_Mat"
                };
                _outlineMat.SetColor("_BaseColor", new Color(1f, 0.85f, 0f, 1f));
                // Front-face culling renders only back faces → cheap outline
                _outlineMat.SetFloat("_Cull", 1f);
            }
            mrOut.sharedMaterial = _outlineMat;
            mrOut.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mrOut.receiveShadows = false;
            _outlineGO.SetActive(false);
        }

        public void SetHighlighted(bool on)
        {
            if (_outlineGO != null)
                _outlineGO.SetActive(on && !_isCarried);
        }

        // ------------------------------------------------------------------ //
        // IPickupable
        // ------------------------------------------------------------------ //

        public bool CanPickup(Transform byPlayer)
        {
            if (_isCarried) return false;
            var player = byPlayer.GetComponentInParent<PlayerController>()
                      ?? byPlayer.GetComponent<PlayerController>();
            return player == null || player.CarriedBaleCount < 3;
        }

        public void OnPickup(Transform carrier, int index)
        {
            stackIndex = index;
            _isCarried = true;
            _rb.isKinematic = true;
            GetComponent<Collider>().enabled = false;

            // Parent to player root (not camera/hands child) so the bale
            // stays horizontal when the camera pitches up/down.
            transform.SetParent(carrier, worldPositionStays: false);
            transform.localPosition = new Vector3(0f,
                carryBaseHeight + index * carryStackStep,
                carryForward);
            transform.localRotation = Quaternion.identity; // flat, same as on ground
        }

        public void OnDrop(Vector3 worldPosition)
        {
            _isCarried = false;
            transform.SetParent(null);
            transform.position = worldPosition;
            _rb.isKinematic = false;
            GetComponent<Collider>().enabled = true;
        }

        public void OnThrow(Vector3 origin, string throwerName, Vector3 velocity)
        {
            _wasThrown   = true;
            _throwOrigin = origin;
            _throwerName = throwerName;

            _isCarried = false;
            transform.SetParent(null);
            _rb.isKinematic = false;

            var col = GetComponent<Collider>();
            col.enabled = true;

            // Bouncy physics material so the bale rolls and bounces on landing.
            // Cached as a static to avoid allocating a new material on every throw.
            if (_throwMat == null)
            {
                _throwMat = new PhysicsMaterial("BaleThrow")
                {
                    bounciness      = throwBounciness,
                    dynamicFriction = 0.25f,
                    staticFriction  = 0.3f,
                    frictionCombine = PhysicsMaterialCombine.Minimum,
                    bounceCombine   = PhysicsMaterialCombine.Maximum
                };
            }
            col.sharedMaterial = _throwMat;

            // Low drag so it keeps momentum and rolls out after landing.
            _rb.linearDamping  = throwLinearDamping;
            _rb.angularDamping = throwAngularDamping;

            _rb.linearVelocity = velocity;

            if (throwClip != null) _audioSource.PlayOneShot(throwClip, throwVolume);

            // Tumbling spin: end-over-end around the lateral axis of the throw direction.
            var spinAxis = Vector3.Cross(velocity.normalized, Vector3.up);
            if (spinAxis.sqrMagnitude < 0.01f) spinAxis = Vector3.right;
            _rb.angularVelocity = spinAxis.normalized * throwAngularSpeed;
        }

        // ------------------------------------------------------------------ //

        void OnCollisionEnter(Collision col)
        {
            if (!_wasThrown || landClip == null) return;
            if (col.gameObject.GetComponentInParent<SquareBale>() != null) return;

            float impactSpeed = col.relativeVelocity.magnitude;
            if (impactSpeed < landMinSpeed) return;
            if (Time.time - _lastLandTime < landCooldown) return;

            float vol = Mathf.Clamp01(impactSpeed / Mathf.Max(0.001f, landFullVolumeSpeed)) * landVolume;
            _audioSource.pitch = Random.Range(landPitchMin, landPitchMax);
            _audioSource.PlayOneShot(landClip, vol);
            _lastLandTime = Time.time;
        }

        // ------------------------------------------------------------------ //
        // IInteractable
        // ------------------------------------------------------------------ //

        public void Interact(PlayerController player)
        {
            if (_isCarried) return;
            if (player.CarriedBaleCount >= 3) return;
            player.PickupBale(this);
        }

        // ------------------------------------------------------------------ //

        public bool IsCarried => _isCarried;
        public int HayUnits => hayUnits;
    }
}
