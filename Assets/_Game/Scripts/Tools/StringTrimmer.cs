using Fields.Grass;
using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// String Trimmer — continuous cutting while primary is held.
    /// Bogging: when cutting uncut grass continuously, cut rate drops -20% and RPM animates down.
    /// 0.3° continuous camera vibration while running.
    /// </summary>
    public class StringTrimmer : PoweredToolBase
    {
        [Header("Trimmer")]
        public Transform headCenter;
        [Tooltip("Base cut radius at upgrade level 0")]
        public float baseCutRadius = 0.18f;
        [Tooltip("How quickly RPM drops when bogging (0-1 per second)")]
        public float boggingRate = 0.4f;
        [Tooltip("Camera vibration amplitude while running")]
        public float vibrationAmplitude = 0.3f;

        GrassField _targetField;
        bool _primaryHeld;
        float _rpm = 1f;      // 0-1 normalised RPM
        float _bogFactor = 1f; // 1 = no bog, 0.8 = full bog (-20% cut rate)

        Fields.Feel.SwingFeelController _feelController;

        protected override void OnEngineStarted() => _rpm = 1f;
        protected override void OnEngineStopped() => _rpm = 0f;

        public override void OnEquip()
        {
            base.OnEquip();
            _feelController = GetComponentInParent<Fields.Feel.SwingFeelController>();
        }

        public override void OnUsePrimary(bool pressed) => _primaryHeld = pressed;

        protected override void Update()
        {
            base.Update(); // fuel drain

            if (!_isEquipped) return;

            if (_primaryHeld && !_engineRunning) StartEngine();
            if (!_primaryHeld && _engineRunning) StopEngine();

            if (!_engineRunning) return;

            // Find target field
            Vector3 cutPos = headCenter != null ? headCenter.position : transform.position;
            if (_targetField == null) _targetField = FindNearestField(cutPos);

            if (_targetField != null)
            {
                // Detect bog: sample 3 cells ahead to see if uncut
                bool inUncut = IsCuttingUncut(cutPos);
                float targetBog = inUncut ? 0.8f : 1f;
                _bogFactor = Mathf.MoveTowards(_bogFactor, targetBog, boggingRate * Time.deltaTime);

                float radius = baseCutRadius * CurrentPower * _bogFactor;
                _targetField.CutArea(cutPos, radius);
            }
            else { _targetField = null; }

            // RPM simulation
            float targetRpm = _bogFactor;
            _rpm = Mathf.MoveTowards(_rpm, targetRpm, 2f * Time.deltaTime);

            // Vibration feel: idle — no per-frame call needed; P3-02 adds proper MMF loop
        }

        bool IsCuttingUncut(Vector3 pos)
        {
            if (_targetField == null) return false;
            return _targetField.GetCompletionPercent() < 99f;
        }

        GrassField FindNearestField(Vector3 near)
        {
            var fields = UnityEngine.Object.FindObjectsByType<GrassField>(UnityEngine.FindObjectsSortMode.None);
            GrassField best = null; float bestDist = float.MaxValue;
            foreach (var f in fields) { float d = (f.transform.position - near).sqrMagnitude; if (d < bestDist) { bestDist = d; best = f; } }
            return best;
        }

        public float RPM => _rpm;
        public float BogFactor => _bogFactor;
    }
}
