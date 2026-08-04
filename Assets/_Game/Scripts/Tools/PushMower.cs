using Fields.Grass;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fields.Tools
{
    /// <summary>
    /// Push Mower — continuous deck cutting, -20% movement speed in uncut grass,
    /// pivot-style steering, deck toggle.
    /// Movement slowdown is applied through PlayerController.
    /// </summary>
    public class PushMower : PoweredToolBase
    {
        [Header("Mower Deck")]
        public Transform deckCenter;
        [Tooltip("Deck cut width at upgrade level 0")]
        public float baseDeckWidth = 1.2f;
        [Tooltip("Movement penalty multiplier when in uncut grass")]
        public float uncutMovePenalty = 0.80f;

        [Header("Running Animation")]
        [Tooltip("Handle oscillation angle while running")]
        public float handleOscillationAngle = 0.08f;
        [Tooltip("Oscillation frequency Hz")]
        public float oscillationHz = 12f;

        bool _deckEngaged;
        bool _primaryHeld;
        GrassField _targetField;
        Vector3 _prevDeckPos;
        Quaternion _restLocalRotation;

        [Tooltip("Distance ahead of player where the deck cuts")]
        public float deckForwardOffset = 0.7f;

        public override void OnEquip()
        {
            base.OnEquip();
            _restLocalRotation = transform.localRotation;
            _prevDeckPos = CalcDeckPos();
        }

        public override void OnUnequip()
        {
            if (_engineRunning) StopEngine();
            _primaryHeld = false;
            base.OnUnequip();
        }

        protected override void OnEngineStarted()
        {
            _deckEngaged = true;
            Fields.Audio.ToolAudioManager.Instance?.StartPushMower();
        }

        protected override void OnEngineStopped()
        {
            _deckEngaged = false;
            Fields.Audio.ToolAudioManager.Instance?.StopPushMower();
        }

        public override void OnUsePrimary(bool pressed)
        {
            _primaryHeld = pressed;
            if (pressed && !_engineRunning) StartEngine();
            else if (!pressed && _engineRunning) StopEngine();
        }

        public void OnDeckToggle(InputValue value)
        {
            if (value.isPressed)
            {
                _deckEngaged = !_deckEngaged;
                // P1: audio "clunk" on deck toggle
            }
        }

        Vector3 CalcDeckPos()
        {
            var player = Fields.Core.PlayerController.Instance;
            if (player == null)
                return deckCenter != null ? deckCenter.position : transform.position;

            Transform cam = player.cameraRoot;
            Vector3 forward = cam != null ? cam.forward : player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f) forward = player.transform.forward;
            forward.Normalize();

            Vector3 pos = player.transform.position + forward * deckForwardOffset;
            return Fields.Grass.GrassField.SnapToTerrain(pos);
        }

        protected override void Update()
        {
            base.Update();
            if (!_isEquipped || !_engineRunning || !_deckEngaged) return;

            Vector3 deckPos = CalcDeckPos();

            // Refresh field when player moves to a different parcel
            if (_targetField == null || !_targetField.ContainsWorldPoint(deckPos))
                _targetField = FindNearestField(deckPos);

            if (_targetField != null)
            {
                float radius = baseDeckWidth * CurrentPower * 0.5f;
                _targetField.CutCapsule(_prevDeckPos, deckPos, radius);
            }

            _prevDeckPos = deckPos;

            // Subtle handle vibration while deck is engaged
            float osc = Mathf.Sin(Time.time * oscillationHz * Mathf.PI * 2f) * handleOscillationAngle;
            transform.localRotation = _restLocalRotation * Quaternion.Euler(osc, 0f, 0f);
        }

        void LateUpdate()
        {
            if (!_isEquipped || (_engineRunning && _deckEngaged)) return;
            transform.localRotation = Quaternion.Slerp(
                transform.localRotation, _restLocalRotation, Time.deltaTime * 8f);
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

        public override string ToolTip => "Tolható fűnyíró  —  LMB: motor · Szélesebb sáv, lassabb mély fűben";

        /// <summary>Returns the move speed multiplier to apply when pushing this mower.</summary>
        public float GetMovementMultiplier(bool inUncutGrass) =>
            _engineRunning && _deckEngaged && inUncutGrass ? uncutMovePenalty : 1f;

        public bool DeckEngaged => _deckEngaged;
    }
}
