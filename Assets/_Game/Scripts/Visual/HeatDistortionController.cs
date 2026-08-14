using System.Collections.Generic;
using UnityEngine;

namespace Fields.Visual
{
    /// <summary>
    /// Drives HeatDistortion shader globals based on player distance to fence.
    /// Assign fenceColliders in the Inspector, or tag fence objects with fenceTag.
    /// </summary>
    public class HeatDistortionController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("If null, uses this GameObject's transform")]
        public Transform playerTransform;

        [Tooltip("Fence colliders for proximity detection. Auto-found by tag if empty.")]
        public Collider[] fenceColliders;

        [Tooltip("Tag used to auto-find fence colliders on Start")]
        public string fenceTag = "Fence";

        [Header("Distance Thresholds")]
        [Tooltip("Beyond this distance (metres) the blur begins")]
        [Range(20f, 300f)]
        public float heatStartDistance = 100f;

        [Tooltip("At this distance the blur reaches its maximum")]
        [Range(50f, 400f)]
        public float heatMaxDistance = 180f;

        [Header("Blur")]
        [Tooltip("Blur radius in pixels at maximum distance (open field)")]
        [Range(0f, 20f)]
        public float heatBlurRadius = 5f;

        [Header("Shimmer (optional UV warp, set to 0 to disable)")]
        [Tooltip("Subtle animated UV distortion on top of the blur")]
        [Range(0f, 0.1f)]
        public float heatShimmerIntensity = 0.012f;

        [Range(1f, 20f)]
        public float noiseScale = 4f;

        [Range(0.05f, 3f)]
        public float noiseSpeed = 0.4f;

        [Header("Near-Fence Override")]
        [Tooltip("Distance from fence where the effect boost starts")]
        [Range(5f, 100f)]
        public float fenceInfluenceRadius = 30f;

        [Tooltip("Heat start distance when standing right at the fence")]
        [Range(10f, 150f)]
        public float fenceHeatStartDistance = 35f;

        [Tooltip("Blur radius in pixels at the fence")]
        [Range(0f, 40f)]
        public float fenceHeatBlurRadius = 14f;

        [Tooltip("Shimmer intensity at the fence")]
        [Range(0f, 0.2f)]
        public float fenceShimmerIntensity = 0.04f;

        // Shader property IDs
        static readonly int s_StartDist   = Shader.PropertyToID("_HeatStartDistance");
        static readonly int s_MaxDist     = Shader.PropertyToID("_HeatMaxDistance");
        static readonly int s_BlurRadius  = Shader.PropertyToID("_HeatBlurRadius");
        static readonly int s_Shimmer     = Shader.PropertyToID("_HeatShimmerIntensity");
        static readonly int s_NoiseScale  = Shader.PropertyToID("_HeatNoiseScale");
        static readonly int s_Speed       = Shader.PropertyToID("_HeatSpeed");

        // ------------------------------------------------------------------ //

        void Start()
        {
            if (playerTransform == null)
                playerTransform = transform;

            if (fenceColliders == null || fenceColliders.Length == 0)
                AutoFindFenceColliders();

            Shader.SetGlobalFloat(s_NoiseScale, noiseScale);
            Shader.SetGlobalFloat(s_Speed,      noiseSpeed);
        }

        void Update()
        {
            float fenceT = ComputeFenceProximityT();

            float startDist  = Mathf.Lerp(heatStartDistance,      fenceHeatStartDistance, fenceT);
            float maxDist    = startDist + Mathf.Lerp(heatMaxDistance - heatStartDistance, 40f, fenceT);
            float blurRadius = Mathf.Lerp(heatBlurRadius,          fenceHeatBlurRadius,    fenceT);
            float shimmer    = Mathf.Lerp(heatShimmerIntensity,    fenceShimmerIntensity,  fenceT);

            Shader.SetGlobalFloat(s_StartDist,  startDist);
            Shader.SetGlobalFloat(s_MaxDist,    maxDist);
            Shader.SetGlobalFloat(s_BlurRadius, blurRadius);
            Shader.SetGlobalFloat(s_Shimmer,    shimmer);
        }

        float ComputeFenceProximityT()
        {
            if (fenceColliders == null || fenceColliders.Length == 0)
                return 0f;

            Vector3 pos = playerTransform.position;
            float nearest = float.MaxValue;

            foreach (var col in fenceColliders)
            {
                if (col == null) continue;
                float d = Vector3.Distance(pos, col.ClosestPoint(pos));
                if (d < nearest) nearest = d;
            }

            if (nearest >= fenceInfluenceRadius) return 0f;

            float t = 1f - (nearest / fenceInfluenceRadius);
            return Mathf.SmoothStep(0f, 1f, t);
        }

        void AutoFindFenceColliders()
        {
            var objs = GameObject.FindGameObjectsWithTag(fenceTag);
            var list = new List<Collider>();
            foreach (var obj in objs)
            {
                var cols = obj.GetComponentsInChildren<Collider>();
                list.AddRange(cols);
            }
            fenceColliders = list.ToArray();
            if (fenceColliders.Length > 0)
                Debug.Log($"[HeatDistortion] Auto-found {fenceColliders.Length} fence collider(s) with tag '{fenceTag}'.");
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (!Application.isPlaying) return;
            Shader.SetGlobalFloat(s_NoiseScale, noiseScale);
            Shader.SetGlobalFloat(s_Speed,      noiseSpeed);
        }

        void OnDrawGizmosSelected()
        {
            if (playerTransform == null) return;
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            Gizmos.DrawWireSphere(playerTransform.position, fenceInfluenceRadius);
        }
#endif
    }
}
