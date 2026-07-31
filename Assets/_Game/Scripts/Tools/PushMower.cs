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
        public float baseDeckWidth = 0.5f;
        [Tooltip("Movement penalty multiplier when in uncut grass")]
        public float uncutMovePenalty = 0.80f;

        bool _deckEngaged;
        bool _primaryHeld;
        GrassField _targetField;
        Vector3 _prevDeckPos;

        public override void OnEquip()
        {
            base.OnEquip();
            _prevDeckPos = deckCenter != null ? deckCenter.position : transform.position;
        }

        public override void OnUsePrimary(bool pressed)
        {
            _primaryHeld = pressed;
            if (pressed && !_engineRunning) StartEngine();
        }

        public void OnDeckToggle(InputValue value)
        {
            if (value.isPressed)
            {
                _deckEngaged = !_deckEngaged;
                // P1: audio "clunk" on deck toggle
            }
        }

        protected override void Update()
        {
            base.Update();
            if (!_isEquipped || !_engineRunning || !_deckEngaged) return;

            Vector3 deckPos = deckCenter != null ? deckCenter.position : transform.position;

            if (_targetField == null) _targetField = FindNearestField(deckPos);

            if (_targetField != null)
            {
                float radius = baseDeckWidth * CurrentPower * 0.5f;
                _targetField.CutCapsule(_prevDeckPos, deckPos, radius);
            }

            _prevDeckPos = deckPos;
        }

        GrassField FindNearestField(Vector3 near)
        {
            var fields = UnityEngine.Object.FindObjectsByType<GrassField>(UnityEngine.FindObjectsSortMode.None);
            GrassField best = null; float bestDist = float.MaxValue;
            foreach (var f in fields) { float d = (f.transform.position - near).sqrMagnitude; if (d < bestDist) { bestDist = d; best = f; } }
            return best;
        }

        /// <summary>Returns the move speed multiplier to apply when pushing this mower.</summary>
        public float GetMovementMultiplier(bool inUncutGrass) =>
            _engineRunning && _deckEngaged && inUncutGrass ? uncutMovePenalty : 1f;

        public bool DeckEngaged => _deckEngaged;
    }
}
