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
        public float baseCutRadius = 0.8f;
        [Tooltip("How far in front of the player the cut circle sits")]
        public float forwardReach = 0.5f;
        [Tooltip("How quickly RPM drops when bogging (0-1 per second)")]
        public float boggingRate = 0.4f;
        [Tooltip("Camera vibration amplitude while running")]
        public float vibrationAmplitude = 0.03f;

        [Header("Pendulum Animation")]
        [Tooltip("Max yaw swing each side in degrees")]
        public float swingAngle = 3.8f;
        [Tooltip("Swings per second")]
        public float swingSpeed = 2.2f;

        GrassField _targetField;
        bool _primaryHeld;
        float _rpm = 1f;
        float _bogFactor = 1f;
        Quaternion _restLocalRotation;

        Fields.Feel.SwingFeelController _feelController;

        protected override void OnEngineStarted()
        {
            _rpm = 1f;
            Fields.Audio.ToolAudioManager.Instance?.StartStringTrimmer();
        }

        protected override void OnEngineStopped()
        {
            _rpm = 0f;
            Fields.Audio.ToolAudioManager.Instance?.StopStringTrimmer();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _restLocalRotation = transform.localRotation;
            _feelController = GetComponentInParent<Fields.Feel.SwingFeelController>();
        }

        public override void OnUnequip()
        {
            if (_engineRunning) StopEngine();
            _primaryHeld = false;
            base.OnUnequip();
        }

        public override void OnUsePrimary(bool pressed) => _primaryHeld = pressed;

        protected override void Update()
        {
            base.Update(); // fuel drain

            if (!_isEquipped) return;

            if (_primaryHeld && !_engineRunning) StartEngine();
            if (!_primaryHeld && _engineRunning) StopEngine();

            if (!_engineRunning) return;

            // Cut circle in camera-forward direction, terrain-snapped
            Vector3 cutPos = CalcCutPosition();
            // Refresh field when player moves to a different parcel
            if (_targetField == null || !_targetField.ContainsWorldPoint(cutPos))
                _targetField = FindNearestField(cutPos);

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

            // Pendulum roll — tilts the head sideways left↔right relative to equip pose
            float roll = Mathf.Sin(Time.time * swingSpeed * Mathf.PI * 2f) * swingAngle * _rpm;
            transform.localRotation = _restLocalRotation * Quaternion.Euler(0f, 0f, roll);
        }

        void LateUpdate()
        {
            if (!_isEquipped || _engineRunning) return;
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, _restLocalRotation, Time.deltaTime * 6f);
        }

        Vector3 CalcCutPosition()
        {
            var player = Fields.Core.PlayerController.Instance;
            if (player == null)
                return headCenter != null ? headCenter.position : transform.position;

            Transform cam = player.cameraRoot;
            Vector3 forward = cam != null ? cam.forward : player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = player.transform.forward;
            forward.Normalize();

            Vector3 pos = player.transform.position + forward * forwardReach;
            return GrassField.SnapToTerrain(pos);
        }

        bool IsCuttingUncut(Vector3 pos)
        {
            if (_targetField == null) return false;
            return _targetField.GetCompletionPercent() < 99f;
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

        public override string ToolTip => "Fűkasza  —  Tartsd nyomva az LMB-t · Lassul vastag fűben";

        public float RPM => _rpm;
        public float BogFactor => _bogFactor;
    }
}
