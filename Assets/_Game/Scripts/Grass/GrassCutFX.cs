using UnityEngine;

namespace Fields.Grass
{
    [RequireComponent(typeof(GrassField))]
    public class GrassCutFX : MonoBehaviour
    {
        [Header("Clipping particles")]
        [Tooltip("Assign GrassBladeFX.mat (Assets/_Game/Materials/) here — pre-configured URP transparent material with Cutted_Grass texture")]
        public Material clipMaterial;
        [Tooltip("Fallback texture if no clipMaterial assigned")]
        public Texture2D bladeTex;
        public Color grassColor     = new Color(0.42f, 0.72f, 0.22f, 1f);
        [Range(4, 64)]
        public int particlesPerCut  = 40;
        public float emitRadius     = 0.25f;
        public float burstUpForce   = 2.5f;
        public float burstSpread    = 1.2f;
        public float particleLifetime = 0.70f;
        public float startSize      = 0.18f;

        GrassField _field;
        ParticleSystem _ps;
        bool _firstCutFired;

        void Awake()
        {
            _field = GetComponent<GrassField>();
            _ps    = BuildClippingSystem();
            _field.OnCellCut += OnCellCut;
        }

        void OnDestroy()
        {
            if (_field != null) _field.OnCellCut -= OnCellCut;
        }

        void OnCellCut(int col, int row)
        {
            if (_ps == null) return;
            Vector3 wp = _field.CellToWorld(col, row);
            wp.y += 0.08f;

            var ep = new ParticleSystem.EmitParams { position = wp, applyShapeToPosition = true };
            _ps.Emit(ep, particlesPerCut);

            Fields.UI.HUDController.Instance?.PulseHit();

            if (!_firstCutFired)
            {
                _firstCutFired = true;
                Fields.Core.SteamManager.Instance?.OnFirstCut();
            }
        }

        // ------------------------------------------------------------------ //

        ParticleSystem BuildClippingSystem()
        {
            var go = new GameObject("GrassCutFX_PS");
            go.transform.SetParent(transform, false);

            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop            = false;
            main.playOnAwake     = false;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(particleLifetime * 0.55f, particleLifetime);
            main.startSpeed      = 0f;
            main.startSize       = new ParticleSystem.MinMaxCurve(startSize * 0.7f, startSize * 1.4f);
            main.startColor      = new ParticleSystem.MinMaxGradient(Color.white, new Color(0.9f, 1f, 0.8f));
            main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = 2000;

            var emission = ps.emission;
            emission.enabled = false;

            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius    = emitRadius;

            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(-burstSpread, burstSpread);
            vel.y = new ParticleSystem.MinMaxCurve(burstUpForce * 0.55f, burstUpForce * 1.1f);
            vel.z = new ParticleSystem.MinMaxCurve(-burstSpread, burstSpread);

            var force = ps.forceOverLifetime;
            force.enabled = true;
            force.space   = ParticleSystemSimulationSpace.World;
            force.y = new ParticleSystem.MinMaxCurve(-9f);

            var rotOL = ps.rotationOverLifetime;
            rotOL.enabled = true;
            rotOL.z = new ParticleSystem.MinMaxCurve(-200f * Mathf.Deg2Rad, 200f * Mathf.Deg2Rad);

            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
                new Keyframe(0f,    0.2f, 0f, 10f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.6f,  0.75f),
                new Keyframe(1f,    0f)));

            // bright yellow-white burst → vivid green → dry yellow → fade out
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1.0f, 1.0f, 0.85f),  0f),
                    new GradientColorKey(new Color(0.70f, 0.95f, 0.35f), 0.15f),
                    new GradientColorKey(grassColor,                      0.35f),
                    new GradientColorKey(new Color(0.80f, 0.72f, 0.12f), 0.70f),
                    new GradientColorKey(new Color(0.58f, 0.52f, 0.06f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rend = go.GetComponent<ParticleSystemRenderer>();
            rend.renderMode = ParticleSystemRenderMode.Billboard;

            Material mat;
            if (clipMaterial != null)
            {
                mat = new Material(clipMaterial);
            }
            else
            {
                var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                mat = new Material(shader);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_ZWrite", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.color = Color.white;
                if (bladeTex != null) { mat.SetTexture("_BaseMap", bladeTex); mat.mainTexture = bladeTex; }
            }
            rend.material = mat;

            ps.Play();
            return ps;
        }
    }
}
