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

        [Header("Carry offsets")]
        [Tooltip("Forward distance from player root when carried")]
        public float carryForward   = 0.70f;
        [Tooltip("Height of the BOTTOM bale center above player root Y")]
        public float carryBaseHeight = 1.30f;
        [Tooltip("Height added per additional bale in the stack")]
        public float carryStackStep  = 0.55f;

        /// <summary>Parcel where the hay was cut. Set by PlayerController on spawn.</summary>
        public int OriginParcelIndex { get; set; } = 0;

        /// <summary>True when made with the round baler upgrade (4× sale value).</summary>
        public bool IsRound { get; set; } = false;

        Rigidbody _rb;
        bool _isCarried;

        // ── Outline (highlight nearest interactable bale) ───────────────── //
        GameObject   _outlineGO;
        static Material _outlineMat;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
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
