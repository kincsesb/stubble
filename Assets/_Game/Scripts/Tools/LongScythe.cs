using Fields.Grass;
using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Long Scythe — sweeps a fan in the camera's forward direction during the swing arc.
    /// Fan angle ~45deg total, reach scaled by CurrentPower. Terrain-snapped.
    ///
    /// Blade wear: each sweep increments _wear 0→1.
    /// At full wear: cut radius drops to wornRadiusFraction of base, stamina cost triples.
    /// Restored by WhetstoneInteractable.
    /// </summary>
    public class LongScythe : MeleeToolBase
    {
        void Awake()
        {
            animWindUpRotation   = new Vector3(-10f,  70f,  10f);
            animSweepEndRotation = new Vector3(  8f, -85f, -12f);
        }

        [Header("Scythe Fan Geometry")]
        [Tooltip("Half-angle of the fan sweep in degrees (total arc = 2x this)")]
        public float fanHalfAngle = 22.5f;
        [Tooltip("Reach from player origin at upgrade level 0")]
        public float baseReach = 1.6f;
        [Tooltip("Capsule radius for each cut segment")]
        public float cutRadius = 0.15f;

        [Header("Blade Wear")]
        [Tooltip("Wear gained per full swing. 0.01 = ~100 swings to full dull (~3 min constant swinging)")]
        [SerializeField] float wearPerSwing = 0.01f;
        [Tooltip("At full wear, cut radius is this fraction of base (min effectiveness)")]
        [SerializeField] float wornRadiusFraction = 0.3f;
        [Tooltip("At full wear, stamina cost multiplies by this factor")]
        [SerializeField] float wornStaminaMultiplier = 3f;

        float _wear;       // 0 = sharp, 1 = fully dull
        bool _warnedDull;

        // Public API for WhetstoneInteractable and HUD
        public float WearNormalized => _wear;

        public void SetWear(float w)
        {
            _wear = Mathf.Clamp01(w);
            if (_wear < 0.8f) _warnedDull = false;
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _warnedDull = _wear >= 0.8f;
        }

        // ------------------------------------------------------------------ //

        const float WINDUP_END = 0.25f;
        const float SWEEP_END  = 0.55f;

        GrassField _targetField;
        Vector3 _prevSweepTip;

        protected override void OnSweepBegin()
        {
            _prevSweepTip = CalcFanTip(0f);

            float staminaCost = 5f * Mathf.Lerp(1f, wornStaminaMultiplier, _wear);
            TryConsumeStamina(staminaCost);

            _wear = Mathf.Min(1f, _wear + wearPerSwing);

            if (_wear >= 0.8f && !_warnedDull)
            {
                _warnedDull = true;
                Fields.UI.HUDController.Instance?.ShowToolTip("Kasza tompa!  Keress fenokot.", 3f);
            }
        }

        protected override void OnSweepTick(float rawTimer)
        {
            float sweepT = Mathf.Clamp01((rawTimer - WINDUP_END) / (SWEEP_END - WINDUP_END));
            Vector3 tip = CalcFanTip(sweepT);

            if (_targetField == null) _targetField = FindNearestField(tip);
            if (_targetField != null)
            {
                float effectiveRadius = cutRadius * CurrentPower * Mathf.Lerp(1f, wornRadiusFraction, _wear);
                _targetField.CutCapsule(_prevSweepTip, tip, effectiveRadius);
            }

            _prevSweepTip = tip;
        }

        protected override void OnSweepEnd() => _targetField = null;

        public override string ToolTip => "Hosszú kasza  —  LMB: ívelt kaszálás · Stamina szükséges";

        // sweepT 0..1 maps left edge to right edge of fan
        Vector3 CalcFanTip(float sweepT)
        {
            var player = Fields.Core.PlayerController.Instance;
            Vector3 origin = player != null ? player.transform.position : transform.position;

            Transform cam = player?.cameraRoot;
            Vector3 forward = cam != null ? cam.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
            forward.Normalize();

            float angle = Mathf.Lerp(-fanHalfAngle, fanHalfAngle, sweepT);
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;

            float reach = baseReach * CurrentPower;
            Vector3 worldTip = origin + dir * reach;
            return GrassField.SnapToTerrain(worldTip);
        }

        GrassField FindNearestField(Vector3 near)
        {
            var fields = UnityEngine.Object.FindObjectsByType<GrassField>(UnityEngine.FindObjectsSortMode.None);
            foreach (var f in fields)
                if (f.ContainsWorldPoint(near)) return f;
            GrassField best = null; float bestDist = float.MaxValue;
            foreach (var f in fields) { float d = (f.transform.position - near).sqrMagnitude; if (d < bestDist) { bestDist = d; best = f; } }
            return best;
        }
    }
}
