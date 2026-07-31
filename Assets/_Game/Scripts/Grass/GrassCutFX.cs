using UnityEngine;

namespace Fields.Grass
{
    /// <summary>
    /// Emits yellow grass-clipping particles whenever a GrassField cell is cut.
    /// Attach alongside GrassField on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(GrassField))]
    public class GrassCutFX : MonoBehaviour
    {
        [Header("Particle Setup")]
        [Tooltip("Optional custom mesh for each grass-clipping particle")]
        public Mesh customMesh;
        [Tooltip("Matches GrassBlade_Material _GrassTop color")]
        public Color grassColor = new Color(0.42f, 0.72f, 0.22f, 1f);
        [Range(4, 24)]
        public int particlesPerCut = 10;
        public float emitRadius = 0.25f;
        [Tooltip("How high particles burst upward")]
        public float burstUpForce = 1.8f;
        public float burstSpread = 1.2f;
        public float particleLifetime = 0.7f;
        public float startSize = 0.08f;

        GrassField _field;
        ParticleSystem _ps;
        bool _firstCutFired;

        void Awake()
        {
            _field = GetComponent<GrassField>();
            _ps    = BuildParticleSystem();
            _field.OnCellCut += OnCellCut;
        }

        void OnDestroy()
        {
            if (_field != null) _field.OnCellCut -= OnCellCut;
        }

        void OnCellCut(int col, int row)
        {
            if (_ps == null) return;
            Vector3 worldPos = _field.CellToWorld(col, row);
            worldPos.y += 0.05f;

            var emitParams = new ParticleSystem.EmitParams
            {
                position   = worldPos,
                applyShapeToPosition = true
            };
            _ps.Emit(emitParams, particlesPerCut);

            Fields.UI.HUDController.Instance?.PulseHit();

            if (!_firstCutFired)
            {
                _firstCutFired = true;
                Fields.Core.SteamManager.Instance?.OnFirstCut();
            }
        }

        ParticleSystem BuildParticleSystem()
        {
            var go = new GameObject("GrassCutFX_PS");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Main
            var main       = ps.main;
            main.loop      = false;
            main.playOnAwake = false;
            main.startLifetime = particleLifetime;
            main.startSpeed = 0f;
            main.startSize  = startSize;
            main.startColor = grassColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 2000;

            // Emission — manual only
            var emission   = ps.emission;
            emission.enabled = false;

            // Shape — sphere for spread
            var shape      = ps.shape;
            shape.enabled  = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius   = emitRadius;

            // Velocity over lifetime — upward burst + spread
            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space   = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(-burstSpread, burstSpread);
            vol.y = new ParticleSystem.MinMaxCurve(burstUpForce * 0.5f, burstUpForce);
            vol.z = new ParticleSystem.MinMaxCurve(-burstSpread, burstSpread);

            // Gravity
            var forceOverLifetime = ps.forceOverLifetime;
            forceOverLifetime.enabled = true;
            forceOverLifetime.space   = ParticleSystemSimulationSpace.World;
            forceOverLifetime.y = new ParticleSystem.MinMaxCurve(-4f);

            // Size over lifetime — shrink to zero
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.6f, 0.6f),
                new Keyframe(1f, 0f));
            sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Renderer
            var rend = go.GetComponent<ParticleSystemRenderer>();
            if (customMesh != null)
            {
                rend.renderMode = ParticleSystemRenderMode.Mesh;
                rend.mesh       = customMesh;
            }
            else
            {
                rend.renderMode = ParticleSystemRenderMode.Billboard;
            }

            // Create an unlit material tinted with grassColor
            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = grassColor;
            rend.material = mat;

            ps.Play();
            return ps;
        }
    }
}
