using Fields.Grass;
using MoreMountains.Feedbacks;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fields.Tools
{
    /// <summary>
    /// Ride-On Mower — arcade kinematic vehicle. Player mounts on OnEquip.
    /// Speed ramps 0 → full in 1.5 s. Body roll/pitch + wheel spin + steering from velocity.
    /// Cuts a wide capsule trail while the deck is engaged.
    /// Anti-pattern #6: NO realistic physics — arcade steering only.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class RideOnMower : PoweredToolBase
    {
        [Header("Mower Movement")]
        public float topSpeed = 5f;
        [Tooltip("Seconds to reach top speed from 0")]
        public float accelerationTime = 1.5f;
        public float turnSpeed = 80f;

        [Header("Deck")]
        public Transform deckCenter;
        public float baseDeckWidth = 1.8f;

        [Header("Feel — Body")]
        [Tooltip("Max body roll in degrees when turning")]
        public float maxBodyRoll = 4f;
        [Tooltip("Max body pitch in degrees on slopes")]
        public float maxBodyPitch = 6f;

        [Header("Feel — Wheels (rolling)")]
        [Tooltip("All parts that should spin when driving — both sides, front + rear")]
        public Transform[] driveWheels = new Transform[0];
        [Tooltip("Approximate wheel radius in model-local units (unscaled)")]
        public float wheelRadius = 0.23f;

        [Header("Feel — Steering wheel")]
        [Tooltip("tripo_part_19 — steering wheel mesh")]
        public Transform steeringWheel;
        [Tooltip("Max steering wheel rotation angle in degrees")]
        public float maxSteeringAngle = 120f;

        [Header("Mounted Camera Offset")]
        [Tooltip("Camera local position while seated")]
        public Vector3 seatedCamOffset = new Vector3(0f, 0.5f, -1.2f);

        // ------------------------------------------------------------------ //

        CharacterController _cc;
        GrassField _targetField;
        Vector3 _prevDeckPos;

        float _currentSpeed;
        float _turnInput;
        float _driveInput;

        bool _deckEngaged = true;
        bool _mounted;

        // Body feel smoothing
        float _rollVelocity, _pitchVelocity;
        float _smoothedRoll, _smoothedPitch;

        // Wheel rolling — accumulated angle in degrees
        float _wheelRotAngle;

        // Steering spring (Feel MMSpringFloat)
        MMSpringFloat _steeringSpring = new MMSpringFloat
        {
            Damping   = 0.55f,
            Frequency = 8f
        };

        // Original camera local position (restored on dismount)
        Transform _mountedCamera;
        Vector3 _origCamLocalPos;

        // Original parent (restored on dismount so ToolHolder can manage the tool)
        Transform _originalParent;

        // ------------------------------------------------------------------ //

        protected override void OnEngineStarted()
        {
            _currentSpeed = 0f;
            var audio = Fields.Audio.ToolAudioManager.Instance;
            audio?.StartTractor();
            audio?.StartPushMower();
            if (audio != null && audio.pushMowerLoop != null)
                audio.pushMowerLoop.volume = audio.masterVolume * 0.5f;
        }

        protected override void OnEngineStopped()
        {
            _currentSpeed = 0f;
            Fields.Audio.ToolAudioManager.Instance?.StopTractor();
            Fields.Audio.ToolAudioManager.Instance?.StopPushMower();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _cc = GetComponent<CharacterController>();
            _prevDeckPos = deckCenter != null ? deckCenter.position : transform.position;

            _mounted = true;

            // Detach from player hierarchy so the mower's CharacterController can move freely
            // without conflicting with the player's own CharacterController.
            _originalParent = transform.parent;
            transform.SetParent(null, worldPositionStays: true);

            var player = Fields.Core.PlayerController.Instance;
            if (player != null)
            {
                player.IsMounted = true;
                player.transform.position = transform.position;
                // Mower drives in transform.right (+90° from root forward); sync camera to match.
                player.SyncMountedYaw(transform.eulerAngles.y + 90f);
            }

            // Shift camera to seated offset
            _mountedCamera = Camera.main?.transform;
            if (_mountedCamera != null)
            {
                _origCamLocalPos = _mountedCamera.localPosition;
                _mountedCamera.localPosition = seatedCamOffset;
            }

            // Auto-start engine immediately on mount
            if (!_engineRunning) StartEngine();
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            _mounted = false;

            if (_originalParent != null)
                transform.SetParent(_originalParent, worldPositionStays: true);

            var player = Fields.Core.PlayerController.Instance;
            if (player != null) player.IsMounted = false;

            if (_mountedCamera != null)
                _mountedCamera.localPosition = _origCamLocalPos;
            if (_engineRunning) StopEngine();
        }

        public override void OnUsePrimary(bool pressed)
        {
            if (pressed) { if (_engineRunning) StopEngine(); else StartEngine(); }
        }

        // ------------------------------------------------------------------ //

        protected override void Update()
        {
            base.Update(); // fuel drain

            if (!_isEquipped || !_mounted) return;

            var player = Fields.Core.PlayerController.Instance;
            if (player != null)
            {
                player.transform.position = transform.position;
                // Keep camera yaw locked to mower driving direction (transform.right = +90° offset).
                player.SyncMountedYaw(transform.eulerAngles.y + 90f);
            }

            ReadInput();

            if (_engineRunning)
            {
                DriveAndSteer();
                CutGrass();
            }

            ApplyBodyFeel();
        }

        // ------------------------------------------------------------------ //

        void ReadInput()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            float drive = 0f, turn = 0f;
            if (gp != null)
            {
                drive = gp.leftStick.y.ReadValue();
                turn  = gp.leftStick.x.ReadValue();
            }
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    drive += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  drive -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  turn  -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) turn  += 1f;
            }
            _driveInput = Mathf.Clamp(drive, -1f, 1f);
            _turnInput  = Mathf.Clamp(turn,  -1f, 1f);
        }

        void DriveAndSteer()
        {
            float targetSpeed = _driveInput * topSpeed * CurrentSpeed;
            float accel = topSpeed / Mathf.Max(0.01f, accelerationTime);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accel * Time.deltaTime);

            if (Mathf.Abs(_currentSpeed) > 0.05f)
                transform.Rotate(Vector3.up, _turnInput * turnSpeed * Time.deltaTime * Mathf.Sign(_currentSpeed));

            // Model's visual front = transform.right (90° offset from root Z).
            Vector3 move = transform.right * _currentSpeed;
            move.y -= 9.81f;
            _cc.Move(move * Time.deltaTime);
        }

        void CutGrass()
        {
            if (!_deckEngaged) return;

            Vector3 rawPos = deckCenter != null ? deckCenter.position : transform.position;
            Vector3 deckPos = GrassField.SnapToTerrain(rawPos);
            if (_targetField == null) _targetField = FindNearestField(deckPos);

            if (_targetField != null)
            {
                float radius = baseDeckWidth * CurrentPower * 0.5f;
                _targetField.CutCapsule(_prevDeckPos, deckPos, radius);
            }

            _prevDeckPos = deckPos;
        }

        void ApplyBodyFeel()
        {
            var visual = transform.childCount > 0 ? transform.GetChild(0) : transform;

            // ── Body roll / pitch ──────────────────────────────────────────── //
            float speedFactor = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / topSpeed);
            float targetRoll  = -_turnInput * maxBodyRoll * speedFactor;
            _smoothedRoll  = Mathf.SmoothDamp(_smoothedRoll,  targetRoll,  ref _rollVelocity,  0.15f);

            float targetPitch = 0f;
            if (_cc != null && _cc.isGrounded)
            {
                if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out var hit, 0.8f))
                    targetPitch = -Vector3.SignedAngle(Vector3.up, hit.normal, transform.right);
            }
            targetPitch = Mathf.Clamp(targetPitch, -maxBodyPitch, maxBodyPitch);
            _smoothedPitch = Mathf.SmoothDamp(_smoothedPitch, targetPitch, ref _pitchVelocity, 0.2f);

            // Base X=-90 corrects imported model orientation; pitch/roll are additive feel.
            visual.localRotation = Quaternion.Euler(-90f + _smoothedPitch, 0f, _smoothedRoll);

            // ── All drive wheels — spin on Y based on drive speed ───────────── //
            float degreesPerSec = (_currentSpeed / Mathf.Max(0.001f, wheelRadius)) * Mathf.Rad2Deg;
            _wheelRotAngle += degreesPerSec * Time.deltaTime;

            foreach (var wheel in driveWheels)
            {
                if (wheel == null) continue;
                wheel.localRotation = Quaternion.Euler(0f, _wheelRotAngle, 0f);
            }

            // ── Steering wheel ─────────────────────────────────────────────── //
            if (steeringWheel != null)
            {
                _steeringSpring.TargetValue = _turnInput * maxSteeringAngle;
                _steeringSpring.UpdateSpringValue(Time.deltaTime);
                steeringWheel.localRotation = Quaternion.Euler(0f, 0f, _steeringSpring.CurrentValue);
            }
        }

        GrassField FindNearestField(Vector3 near)
        {
            var fields = Object.FindObjectsByType<GrassField>(FindObjectsSortMode.None);
            foreach (var f in fields)
                if (f.ContainsWorldPoint(near)) return f;
            GrassField best = null; float bestDist = float.MaxValue;
            foreach (var f in fields) { float d = (f.transform.position - near).sqrMagnitude; if (d < bestDist) { bestDist = d; best = f; } }
            return best;
        }

        // ------------------------------------------------------------------ //

        public override string ToolTip =>
            Fields.Core.LocalizationManager.Instance?.Get("tool.rideon.tooltip")
            ?? "Ride-On Mower  —  WASD: drive · Cuts automatically while moving";

        public float CurrentSpeedNormalized => topSpeed > 0 ? _currentSpeed / topSpeed : 0f;
        public bool DeckEngaged => _deckEngaged;
        public void ToggleDeck() => _deckEngaged = !_deckEngaged;
    }
}
