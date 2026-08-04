using Fields.Grass;
using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Long Scythe — sweeps a fan in the camera's forward direction during the swing arc.
    /// Fan angle ~45deg total, reach scaled by CurrentPower. Terrain-snapped.
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

        const float WINDUP_END = 0.25f;
        const float SWEEP_END  = 0.55f;

        GrassField _targetField;
        Vector3 _prevSweepTip;

        protected override void OnSweepBegin()
        {
            _prevSweepTip = CalcFanTip(0f);
            TryConsumeStamina(14f);
        }

        protected override void OnSweepTick(float rawTimer)
        {
            float sweepT = Mathf.Clamp01((rawTimer - WINDUP_END) / (SWEEP_END - WINDUP_END));
            Vector3 tip = CalcFanTip(sweepT);

            if (_targetField == null) _targetField = FindNearestField(tip);
            if (_targetField != null)
                _targetField.CutCapsule(_prevSweepTip, tip, cutRadius * CurrentPower);

            _prevSweepTip = tip;
        }

        protected override void OnSweepEnd() => _targetField = null;

        public override string ToolTip => "Hosszú kasza  —  LMB: ívelt kaszálás · Stamina szükséges";

        // sweepT 0..1 maps left edge to right edge of fan
        Vector3 CalcFanTip(float sweepT)
        {
            var player = Fields.Core.PlayerController.Instance;
            Vector3 origin = player != null ? player.transform.position : transform.position;

            // Camera forward projected to XZ plane
            Transform cam = player?.cameraRoot;
            Vector3 forward = cam != null ? cam.forward : transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = transform.forward;
            forward.Normalize();

            // Sweep from -fanHalfAngle to +fanHalfAngle
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