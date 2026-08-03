using Fields.Core;
using Fields.Economy;
using UnityEngine;

namespace Fields.Hay
{
    /// <summary>
    /// Trigger zone: when the player enters carrying SquareBales, they are auto-sold for money.
    /// Place a trigger Collider on this GameObject and set the layer to Default or a dedicated Trigger layer.
    /// A pulsing LineRenderer ring is spawned at runtime to mark the zone visually.
    /// </summary>
    public class DeliveryZone : MonoBehaviour
    {
        [Tooltip("Price per bale (overridden by BalerManager hay value if set)")]
        public int pricePerBale = 50;

        [Header("Ring visual")]
        [Tooltip("Leave 0 to auto-detect from trigger collider.")]
        public float ringRadius = 0f;
        [Tooltip("Optional URP-compatible material for the ring. If null, uses Sprites/Default.")]
        public Material ringMaterial;
        public Color ringColorA = new Color(1f, 0.80f, 0.05f, 0.65f);
        public Color ringColorB = new Color(1f, 1.00f, 0.80f, 1.00f);
        [Tooltip("Pulse speed in cycles per second")]
        public float pulseSpeed = 2.5f;

        [Header("Visual feedback")]
        [Tooltip("Optional particle effect played on each sale")]
        public ParticleSystem saleEffect;

        LineRenderer _ring;
        float _pulseTimer;

        // ------------------------------------------------------------------ //

        void Start()
        {
            BuildRingVisual();
        }

        void Update()
        {
            AnimateRing();
        }

        // ------------------------------------------------------------------ //

        void BuildRingVisual()
        {
            float radius = ringRadius > 0f ? ringRadius : DetectTriggerRadius();

            var go = new GameObject("DeliveryRing");
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.up * 0.06f;
            go.transform.localRotation = Quaternion.identity;

            _ring = go.AddComponent<LineRenderer>();
            _ring.loop = true;
            _ring.useWorldSpace = false;
            _ring.alignment = LineAlignment.View;
            _ring.widthMultiplier = 0.12f;
            _ring.numCornerVertices = 4;
            _ring.numCapVertices = 0;
            _ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ring.receiveShadows = false;

            if (ringMaterial != null)
            {
                _ring.material = ringMaterial;
            }
            else
            {
                // Try URP particles first, then 2D sprite, then basic unlit
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                if (shader != null) _ring.material = new Material(shader);
            }

            const int Segs = 64;
            _ring.positionCount = Segs;
            for (int i = 0; i < Segs; i++)
            {
                float angle = i / (float)Segs * Mathf.PI * 2f;
                _ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            _ring.startColor = _ring.endColor = ringColorA;
        }

        void AnimateRing()
        {
            if (_ring == null) return;
            _pulseTimer += Time.deltaTime;
            float t = (Mathf.Sin(_pulseTimer * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            _ring.startColor = _ring.endColor = Color.Lerp(ringColorA, ringColorB, t);
            _ring.widthMultiplier = Mathf.Lerp(0.07f, 0.22f, t);
        }

        float DetectTriggerRadius()
        {
            float scaleXZ = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
            var sphere = GetComponent<SphereCollider>();
            if (sphere != null) return sphere.radius * scaleXZ;
            var box = GetComponent<BoxCollider>();
            if (box != null) return Mathf.Min(box.size.x, box.size.z) * 0.5f * scaleXZ;
            return 4f;
        }

        // ------------------------------------------------------------------ //

        void OnTriggerEnter(Collider other)
        {
            var player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;

            var bales = player.GetCarriedSquareBales();
            if (bales.Count == 0) return;

            int total = CalcTotal(bales.Count);
            CurrencyManager.Instance?.Earn(total);
            player.DropSquareBales();

            // Destroy the dropped bales immediately — they were just sold
            var dropped = Object.FindObjectsByType<SquareBale>(FindObjectsSortMode.None);
            foreach (var b in dropped)
            {
                float dist = Vector3.Distance(b.transform.position, transform.position);
                if (dist < 10f) Object.Destroy(b.gameObject);
            }

            if (saleEffect != null) saleEffect.Play();
            Debug.Log($"[DeliveryZone] Sold {bales.Count} bale(s) for ${total}");
        }

        int CalcTotal(int baleCount)
        {
            var bm = Fields.Economy.BalerManager.Instance;
            float mult = bm != null ? bm.HayValueMultiplier : 1f;
            return Mathf.RoundToInt(pricePerBale * baleCount * mult);
        }
    }
}
