using Fields.Grass;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fields.Tools
{
    /// <summary>
    /// Ride-On Mower — arcade kinematic vehicle. Player mounts on OnEquip.
    /// Speed ramps 0 → full in 1.5 s. Body roll/pitch from velocity.
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
        public float baseDeckWidth = 0.9f;

        [Header("Feel")]
        [Tooltip("Max body roll in degrees when turning")]
        public float maxBodyRoll = 4f;
        [Tooltip("Max body pitch in degrees on slopes")]
        public float maxBodyPitch = 6f;

        [Header("Mounted Camera Offset")]
        [Tooltip("Camera local position while seated")]
        public Vector3 seatedCamOffset = new Vector3(0f, 0.6f, 0.3f);

        CharacterController _cc;
        GrassField _targetField;
        Vector3 _prevDeckPos;

        float _currentSpeed;
        float _turnInput;
        float _driveInput;

        bool _deckEngaged = true;
        bool _mounted;

        // Smooth body roll / pitch
        float _rollVelocity, _pitchVelocity;
        float _smoothedRoll, _smoothedPitch;

        // Original camera local position (restored on dismount)
        Transform _mountedCamera;
        Vector3 _origCamLocalPos;

        // ------------------------------------------------------------------ //

        protected override void OnEngineStarted()
        {
            _currentSpeed = 0f;
            Fields.Audio.ToolAudioManager.Instance?.StartTractor();
        }

        protected override void OnEngineStopped()
        {
            _currentSpeed = 0f;
            Fields.Audio.ToolAudioManager.Instance?.StopTractor();
        }

        public override void OnEquip()
        {
            base.OnEquip();
            _cc = GetComponent<CharacterController>();
            _prevDeckPos = deckCenter != null ? deckCenter.position : transform.position;

            _mounted = true;

            // Lock player movement and teleport them to mower seat
            var player = Fields.Core.PlayerController.Instance;
            if (player != null)
            {
                player.IsMounted = true;
                player.transform.position = transform.position;
            }

            // Shift camera to seated offset
            _mountedCamera = Camera.main?.transform;
            if (_mountedCamera != null)
            {
                _origCamLocalPos = _mountedCamera.localPosition;
                _mountedCamera.localPosition = seatedCamOffset;
            }
        }

        public override void OnUnequip()
        {
            base.OnUnequip();
            _mounted = false;

            var player = Fields.Core.PlayerController.Instance;
            if (player != null) player.IsMounted = false;

            if (_mountedCamera != null)
                _mountedCamera.localPosition = _origCamLocalPos;
            if (_engineRunning) StopEngine();
        }

        public override void OnUsePrimary(bool pressed)
        {
            if (pressed && !_engineRunning) StartEngine();
            else if (!pressed && _engineRunning) StopEngine();
        }

        // ------------------------------------------------------------------ //

        protected override void Update()
        {
            base.Update(); // fuel drain

            if (!_isEquipped || !_mounted) return;

            // Keep player position synced to mower seat
            var player = Fields.Core.PlayerController.Instance;
            if (player != null) player.transform.position = transform.position;

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
            // Prefer new Input System; fall back to legacy axis if neither device active
            var kb  = Keyboard.current;
            var gp  = Gamepad.current;

            float drive = 0f, turn = 0f;
            if (gp != null)
            {
                drive = gp.leftStick.y.ReadValue();
                turn  = gp.leftStick.x.ReadValue();
            }
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)   drive += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) drive -= 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) turn  -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) turn  += 1f;
            }
            _driveInput = Mathf.Clamp(drive, -1f, 1f);
            _turnInput  = Mathf.Clamp(turn,  -1f, 1f);
        }

        void DriveAndSteer()
        {
            // Acceleration ramp (0 → topSpeed * CurrentSpeed in accelerationTime seconds)
            float targetSpeed = _driveInput * topSpeed * CurrentSpeed;
            float accel = topSpeed / Mathf.Max(0.01f, accelerationTime);
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, targetSpeed, accel * Time.deltaTime);

            // Steering (only while moving)
            if (Mathf.Abs(_currentSpeed) > 0.05f)
                transform.Rotate(Vector3.up, _turnInput * turnSpeed * Time.deltaTime * Mathf.Sign(_currentSpeed));

            // Move via CharacterController (applies gravity automatically)
            Vector3 move = transform.forward * _currentSpeed;
            move.y -= 9.81f; // gravity
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
            // Roll: lean into turns, scaled by speed
            float speedFactor = Mathf.Clamp01(Mathf.Abs(_currentSpeed) / topSpeed);
            float targetRoll  = -_turnInput * maxBodyRoll * speedFactor;
            _smoothedRoll = Mathf.SmoothDamp(_smoothedRoll, targetRoll, ref _rollVelocity, 0.15f);

            // Pitch: read slope from CharacterController ground normal
            float targetPitch = 0f;
            if (_cc.isGrounded)
            {
                if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out var hit, 0.8f))
                    targetPitch = -Vector3.SignedAngle(Vector3.up, hit.normal, transform.right);
            }
            targetPitch = Mathf.Clamp(targetPitch, -maxBodyPitch, maxBodyPitch);
            _smoothedPitch = Mathf.SmoothDamp(_smoothedPitch, targetPitch, ref _pitchVelocity, 0.2f);

            // Apply to visual body (this transform's child, or self if no visual child)
            var visual = transform.childCount > 0 ? transform.GetChild(0) : transform;
            visual.localRotation = Quaternion.Euler(_smoothedPitch, 0f, _smoothedRoll);
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

        public float CurrentSpeedNormalized => topSpeed > 0 ? _currentSpeed / topSpeed : 0f;
        public bool DeckEngaged => _deckEngaged;
        public void ToggleDeck() => _deckEngaged = !_deckEngaged;
    }
}
