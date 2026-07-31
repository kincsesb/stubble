using Fields.Core;
using Fields.Feel;
using UnityEngine;

namespace Fields.Tools
{
    /// <summary>
    /// Swing-cycle base for melee tools (sickle, scythe).
    /// Spec §8.1.1: WindUp 25% | Sweep 30% | Recovery 45%.
    /// Input never lost — queued during Recovery ≥70%.
    /// Stamina pool — soft slowdown only, no hard lockout.
    /// </summary>
    public abstract class MeleeToolBase : BaseTool
    {
        [Header("Swing")]
        [Tooltip("Full swing duration in seconds")]
        public float swingDuration = 0.55f;

        // Spec proportions
        const float WINDUP_END = 0.25f;
        const float SWEEP_END = 0.55f;  // 25% + 30%
        // Recovery 45% fills the rest

        const float QUEUE_WINDOW_START = 0.70f; // can queue next swing from 70% of cycle

        // State
        protected SwingPhase _phase = SwingPhase.Idle;
        float _swingTimer;
        bool _inputQueued;

        // Stamina
        float _stamina;
        float _maxStamina;

        // Feel
        protected SwingFeelController _feelController;
        PlayerController _owner;

        // ------------------------------------------------------------------ //

        public override void OnEquip()
        {
            base.OnEquip();
            _maxStamina = CurrentEndurance;
            _stamina = _maxStamina;
            _feelController = GetComponentInParent<SwingFeelController>();
        }

        void Update()
        {
            if (!_isEquipped) return;
            TickSwing();
            RegenStamina();
        }

        // ------------------------------------------------------------------ //
        // Input
        // ------------------------------------------------------------------ //

        public override void OnUsePrimary(bool pressed)
        {
            if (!pressed) return;

            if (_phase == SwingPhase.Idle)
                StartSwing();
            else if (_phase == SwingPhase.Recovery && _swingTimer >= QUEUE_WINDOW_START)
                _inputQueued = true;
            // Before queue window — input silently accepted; pressed again will register
        }

        // ------------------------------------------------------------------ //
        // Swing FSM
        // ------------------------------------------------------------------ //

        void StartSwing()
        {
            _phase = SwingPhase.WindUp;
            _swingTimer = 0f;
            _inputQueued = false;
            OnWindUpBegin();
        }

        void TickSwing()
        {
            if (_phase == SwingPhase.Idle) return;

            _swingTimer += Time.deltaTime / swingDuration;

            switch (_phase)
            {
                case SwingPhase.WindUp:
                    if (_swingTimer >= WINDUP_END)
                    {
                        _phase = SwingPhase.Sweep;
                        OnSweepBegin();
                    }
                    break;

                case SwingPhase.Sweep:
                    OnSweepTick(_swingTimer);
                    if (_swingTimer >= SWEEP_END)
                    {
                        _phase = SwingPhase.Recovery;
                        OnSweepEnd();
                    }
                    break;

                case SwingPhase.Recovery:
                    if (_swingTimer >= 1f)
                    {
                        _phase = SwingPhase.Idle;
                        OnSwingComplete();
                        if (_inputQueued)
                        {
                            _inputQueued = false;
                            StartSwing();
                        }
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ //
        // Stamina
        // ------------------------------------------------------------------ //

        void RegenStamina()
        {
            if (_phase == SwingPhase.Idle)
                _stamina = Mathf.Min(_stamina + 20f * Time.deltaTime, _maxStamina);
        }

        protected bool TryConsumeStamina(float amount)
        {
            // Soft limit: allow swing even at low stamina, just slower
            _stamina = Mathf.Max(0f, _stamina - amount);
            return true;
        }

        public float StaminaNormalized => _maxStamina > 0 ? _stamina / _maxStamina : 0f;
        public SwingPhase CurrentPhase => _phase;

        // ------------------------------------------------------------------ //
        // Overrideable hooks
        // ------------------------------------------------------------------ //

        protected virtual void OnWindUpBegin() { }
        protected virtual void OnSweepBegin() { }
        protected virtual void OnSweepTick(float normalizedTime) { }
        protected virtual void OnSweepEnd() { }
        protected virtual void OnSwingComplete() { }
    }

    public enum SwingPhase { Idle, WindUp, Sweep, Recovery }
}
