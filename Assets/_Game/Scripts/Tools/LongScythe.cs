using Fields.Grass;
using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Long Scythe — wider, slower melee tool. Arc width 1.4–2.4m (power-driven).
    /// Deeper swing sound hook, heavier feel via longer swing duration.
    /// </summary>
    public class LongScythe : MeleeToolBase
    {
        void Awake()
        {
            animWindUpRotation   = new Vector3(-10f,  70f,  10f);
            animSweepEndRotation = new Vector3(  8f, -85f, -12f);
        }

        [Header("Scythe Geometry")]
        public Transform bladeTipLeft;
        public Transform bladeTipRight;
        [Tooltip("Base arc half-width at upgrade level 0 (spec: 0.7m each side = 1.4m total)")]
        public float baseHalfArcWidth = 0.7f;

        GrassField _targetField;
        Vector3 _prevLeft, _prevRight;

        protected override void OnSweepBegin()
        {
            _prevLeft  = bladeTipLeft  != null ? bladeTipLeft.position  : transform.position + transform.right * -baseHalfArcWidth;
            _prevRight = bladeTipRight != null ? bladeTipRight.position : transform.position + transform.right *  baseHalfArcWidth;
            TryConsumeStamina(14f); // slightly more expensive than sickle
        }

        protected override void OnSweepTick(float normalizedTime)
        {
            Vector3 curLeft  = bladeTipLeft  != null ? bladeTipLeft.position  : transform.position + transform.right * -baseHalfArcWidth * CurrentPower;
            Vector3 curRight = bladeTipRight != null ? bladeTipRight.position : transform.position + transform.right *  baseHalfArcWidth * CurrentPower;

            float radius = 0.15f * CurrentPower;

            if (_targetField == null) _targetField = FindNearestField(curLeft);
            if (_targetField != null)
            {
                _targetField.CutCapsule(_prevLeft,  curLeft,  radius);
                _targetField.CutCapsule(_prevRight, curRight, radius);
                // Also fill the gap between tips
                _targetField.CutCapsule(curLeft, curRight, radius);
            }

            _prevLeft  = curLeft;
            _prevRight = curRight;
        }

        protected override void OnSweepEnd() => _targetField = null;

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
