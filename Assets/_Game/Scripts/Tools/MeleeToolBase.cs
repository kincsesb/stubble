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

        [Header("Swing Animation")]
        [Tooltip("Local Euler angles at rest")]
        public Vector3 animRestRotation     = new Vector3(  0f,   0f,   0f);
        [Tooltip("Local Euler angles at wind-up peak (pulled back to right)")]
        public Vector3 animWindUpRotation   = new Vector3(-10f,  70f,  10f);
        [Tooltip("Local Euler angles at sweep follow-through (sweeps right→left)")]
        public Vector3 animSweepEndRotation = new Vector3(  8f, -85f, -12f);

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
        protected Fields.Core.PlayerController _owner;

        // ------------------------------------------------------------------ //

        public override void OnEquip()
        {
            base.OnEquip();
            _maxStamina = CurrentEndurance;
            _stamina = _maxStamina;
            _feelController = GetComponentInParent<SwingFeelController>();
            _owner = GetComponentInParent<Fields.Core.PlayerController>();
        }

        void Update()
        {
            if (!_isEquipped) return;
            TickSwing();
            TickAnimation();
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
            Fields.Audio.ToolAudioManager.Instance?.PlaySwing();
            OnWindUpBegin();
        }

        // Called internally when sweep phase begins — override to extend
        void OnSweepPhaseBegin()
        {
            // Haptic: strong thump when blade enters grass
            _owner?.TriggerHaptics(0.35f, 0.65f, 0.10f);
            OnSweepBegin();
        }

        // Spec §5.5: at zero stamina, swing speed drops to ~40% of normal.
        // Smooth degradation begins below 30% stamina.
        float EffectiveDuration
        {
            get
            {
                if (_maxStamina <= 0f) return swingDuration;
                float factor = Mathf.Clamp01(_stamina / (_maxStamina * 0.3f));
                return swingDuration / Mathf.Lerp(0.4f, 1f, factor);
            }
        }

        void TickSwing()
        {
            if (_phase == SwingPhase.Idle) return;

            _swingTimer += Time.deltaTime / EffectiveDuration;

            switch (_phase)
            {
                case SwingPhase.WindUp:
                    if (_swingTimer >= WINDUP_END)
                    {
                        _phase = SwingPhase.Sweep;
                        OnSweepPhaseBegin();
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
                        Fields.Audio.ToolAudioManager.Instance?.StopSwing();
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
        // Swing Animation — drives tool's localRotation through the arc
        // ------------------------------------------------------------------ //

        void TickAnimation()
        {
            Quaternion target;
            switch (_phase)
            {
                case SwingPhase.WindUp:
                {
                    float t = Mathf.Clamp01(_swingTimer / WINDUP_END);
                    target = Quaternion.Slerp(
                        Quaternion.Euler(animRestRotation),
                        Quaternion.Euler(animWindUpRotation), t);
                    break;
                }
                case SwingPhase.Sweep:
                {
                    float t = Mathf.Clamp01((_swingTimer - WINDUP_END) / (SWEEP_END - WINDUP_END));
                    // ease-in: fast sweep start
                    float ease = 1f - (1f - t) * (1f - t);
                    target = Quaternion.Slerp(
                        Quaternion.Euler(animWindUpRotation),
                        Quaternion.Euler(animSweepEndRotation), ease);
                    break;
                }
                case SwingPhase.Recovery:
                {
                    float t = Mathf.Clamp01((_swingTimer - SWEEP_END) / (1f - SWEEP_END));
                    target = Quaternion.Slerp(
                        Quaternion.Euler(animSweepEndRotation),
                        Quaternion.Euler(animRestRotation), t);
                    break;
                }
                default:
                    target = Quaternion.Euler(animRestRotation);
                    break;
            }

            transform.localRotation = Quaternion.Slerp(transform.localRotation, target, Time.deltaTime * 25f);
        }

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
