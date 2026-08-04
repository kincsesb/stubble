using UnityEngine;

namespace Fields.Grass
{
    /// <summary>
    /// Emits cartoon grass-clipping particles whenever a GrassField cell is cut.
    /// Two particle systems: main grass clippings (green→yellow) + tiny sparkle stars.
    /// </summary>
    [RequireComponent(typeof(GrassField))]
    public class GrassCutFX : MonoBehaviour
    {
        [Header("Particle Setup")]
        [Tooltip("Optional custom mesh for each grass-clipping particle")]
        public Mesh customMesh;
        [Tooltip("Grass blade colour for particles (matches GrassBlade material _GrassTop)")]
        public Color grassColor = new Color(0.42f, 0.72f, 0.22f, 1f);
        [Range(4, 32)]
        public int particlesPerCut = 14;
        public float emitRadius = 0.28f;
        [Tooltip("Peak upward velocity of particles")]
        public float burstUpForce = 2.8f;
        public float burstSpread = 1.6f;
        public float particleLifetime = 0.85f;
        public float startSize = 0.13f;

        [Header("Sparkle overlay")]
        [Tooltip("Small white star particles for cartoon pop")]
        public int sparklesPerCut = 5;
        public float sparkleSize = 0.06f;
        public float sparkleLifetime = 0.4f;

        GrassField _field;
        ParticleSystem _ps;
        ParticleSystem _sparklePS;
        bool _firstCutFired;

        void Awake()
        {
            _field     = GetComponent<GrassField>();
            _ps        = BuildGrassParticleSystem();
            _sparklePS = BuildSparkleSystem();
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

            var ep = new ParticleSystem.EmitParams { position = worldPos, applyShapeToPosition = true };
            _ps.Emit(ep, particlesPerCut);
            _sparklePS?.Emit(ep, sparklesPerCut);

            Fields.UI.HUDController.Instance?.PulseHit();

            if (!_firstCutFired)
            {
                _firstCutFired = true;
                Fields.Core.SteamManager.Instance?.OnFirstCut();
            }
        }

        // ------------------------------------------------------------------ //

        ParticleSystem BuildGrassParticleSystem()
        {
            var go = new GameObject("GrassCutFX_PS");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = false;
            main.playOnAwake     = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(particleLifetime * 0.7f, particleLifetime);
            main.startSpeed      = 0f;
            main.startSize       = new ParticleSystem.MinMaxCurve(startSize * 0.6f, startSize * 1.4f);
            main.startColor      = new ParticleSystem.MinMaxGradient(grassColor, new Color(0.85f, 0.80f, 0.15f));
            main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 3000;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = emitRadius;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space   = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(-burstSpread, burstSpread);
            vol.y = new ParticleSystem.MinMaxCurve(burstUpForce * 0.5f, burstUpForce);
            vol.z = new ParticleSystem.MinMaxCurve(-burstSpread, burstSpread);

            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space   = ParticleSystemSimulationSpace.World;
            force.y = new ParticleSystem.MinMaxCurve(-6f);

            // Rotation over lifetime — tumbling chips
            var rotOL = ps.rotationOverLifetime;
            rotOL.enabled = true;
            rotOL.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

            // Size over lifetime — quick pop then shrink
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f,   0f,  0f, 8f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.7f,  0.6f),
                new Keyframe(1f,   0f)));

            // Color over lifetime: green → yellow → fade
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(grassColor, 0f),
                    new GradientColorKey(new Color(0.85f, 0.80f, 0.10f), 0.5f),
                    new GradientColorKey(new Color(0.70f, 0.65f, 0.08f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            if (customMesh != null)
            {
                rend.renderMode = ParticleSystemRenderMode.Mesh;
                rend.mesh       = customMesh;
            }

            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.white; // colour driven by particle colour-over-lifetime
            rend.material = mat;

            ps.Play();
            return ps;
        }

        ParticleSystem BuildSparkleSystem()
        {
            var go = new GameObject("GrassCutFX_Sparkles");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = false;
            main.playOnAwake     = false;
            main.startLifetime   = sparkleLifetime;
            main.startSpeed      = 0f;
            main.startSize       = new ParticleSystem.MinMaxCurve(sparkleSize * 0.5f, sparkleSize * 1.5f);
            main.startColor      = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.95f, 0.6f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 500;

            var emission2 = ps.emission;
            emission2.enabled = false;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = emitRadius * 0.4f;

            var vol = ps.velocityOverLifetime;
            vol.enabled = true;
            vol.space   = ParticleSystemSimulationSpace.World;
            vol.x = new ParticleSystem.MinMaxCurve(-burstSpread * 0.6f, burstSpread * 0.6f);
            vol.y = new ParticleSystem.MinMaxCurve(burstUpForce * 0.8f, burstUpForce * 1.4f);
            vol.z = new ParticleSystem.MinMaxCurve(-burstSpread * 0.6f, burstSpread * 0.6f);

            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space   = ParticleSystemSimulationSpace.World;
            force.y = new ParticleSystem.MinMaxCurve(-8f);

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.9f, 0.4f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(g);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;

            var mat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                mat = new Material(Shader.Find("Sprites/Default"));
            mat.color = Color.white;
            rend.material = mat;

            ps.Play();
            return ps;
        }
    }
}
