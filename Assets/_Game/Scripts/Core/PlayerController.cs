using System.Collections.Generic;
using Fields.Core.Data;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fields.Core
{
    /// <summary>
    /// First-person player controller. Designed as NetworkBehaviour stub —
    /// NGO integration added in P2. All logic gates on IsOwner equivalent.
    /// CharacterController-based movement, no Rigidbody.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [Header("References")]
        public GameConfig config;
        public Transform cameraRoot;
        public Transform handsRoot;
        public Transform toolHolder;

        [Header("Look")]
        public float mouseSensitivity = 0.15f;
        public float gamepadSensitivity = 180f;
        public float lookPitchMin = -80f;
        public float lookPitchMax = 80f;
        [Tooltip("Gamepad stick dead-zone")]
        public float gamepadDeadZone = 0.1f;

        [Header("Head Bob")]
        public float bobAmplitude = 0.03f;
        public float bobFrequency = 1.8f;

        [Header("FOV")]
        public float baseFOV = 60f;
        public float sprintFOV = 65f;
        public float fovLerpSpeed = 6f;

        [Header("Jump")]
        public float jumpHeight = 1.2f;

        [Header("Baling")]
        [Tooltip("Radius around the player to collect hay units from the accumulation grid")]
        public float balingRadius = 12f;
        [Tooltip("Minimum accumulated hay units needed to start baling")]
        public float balingThreshold = 60f;
        [Tooltip("Seconds of holding E to produce a bale")]
        public float balingDuration = 2.5f;
        [Tooltip("SquareBale prefab spawned when baling completes")]
        public GameObject squareBalePrefab;

        [Header("Haptics")]
        [Tooltip("Duration of swing impact rumble in seconds")]
        public float hapticSwingDuration = 0.12f;
        public float hapticSwingLow = 0.3f;
        public float hapticSwingHigh = 0.6f;
        [Tooltip("Light rumble while sprinting")]
        public float hapticSprintLow = 0.05f;

        // Components
        CharacterController _cc;
        Camera _camera;

        // Input state
        Vector2 _moveInput;
        Vector2 _lookInput;
        bool _sprintHeld;

        // Camera pitch/yaw
        float _yaw;
        float _pitch;

        // Bob
        float _bobTimer;
        Vector3 _bobOffset;

        // Haptics
        float _hapticTimer;
        float _hapticLow;
        float _hapticHigh;

        // Stamina
        float _stamina = 100f;

        // Carry
        List<Fields.Hay.SquareBale> _carriedSquareBales = new List<Fields.Hay.SquareBale>(3);

        // Baling
        bool _interactHeld;
        bool _balingReady;
        float _balingTimer;
        bool _balingRequiresFreshPress = true; // prevents auto-fire when E held before ready
        Fields.Hay.HayAccumulationSystem[] _hayAccumSystems;

        // Vertical velocity (gravity + jump, accumulated across frames)
        float _yVelocity;

        // Jump request flag — set by OnJump, consumed in HandleMovement
        bool _jumpRequested;

        // External velocity (applied next move frame)
        Vector3 _externalVelocity;

        // When true, HandleMovement/Bob/FOV are suppressed (e.g. riding mower)
        public bool IsMounted { get; set; }

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _cc = GetComponent<CharacterController>();
            _stamina = 100f;
            _camera = Camera.main;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (_camera != null) _camera.fieldOfView = baseFOV;
            _hayAccumSystems = Object.FindObjectsByType<Fields.Hay.HayAccumulationSystem>(FindObjectsSortMode.None);
        }

        void Update()
        {
            HandleLook();
            HandleMovement();
            HandleBob();
            HandleFOV();
            HandleHaptics();
            RegenStamina();
            HandleBaling();
        }

        // ------------------------------------------------------------------ //
        // Input System callbacks (wired via PlayerInput component)
        // ------------------------------------------------------------------ //

        public void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();

        public void OnLook(InputValue value) => _lookInput = value.Get<Vector2>();

        public void OnSprint(InputValue value) => _sprintHeld = value.isPressed;

        public void OnInteract(InputValue value)
        {
            _interactHeld = value.isPressed;
            if (value.isPressed)
            {
                if (_balingReady)
                    _balingRequiresFreshPress = false; // intentional press → allow baling
                else
                    TryInteract();
            }
            else
            {
                _balingRequiresFreshPress = true; // release resets: next hold must be a new press
            }
        }

        public void OnJump(InputValue value)
        {
            if (value.isPressed) _jumpRequested = true;
        }

        public void OnDrop(InputValue value)
        {
            if (value.isPressed && _carriedSquareBales.Count > 0) DropSquareBales();
        }

        // ------------------------------------------------------------------ //
        // Movement
        // ------------------------------------------------------------------ //

        void HandleLook()
        {
            Vector2 look = _lookInput;

            bool isGamepad = look.sqrMagnitude <= 1.01f && !Mouse.current.delta.IsActuated();
            if (isGamepad)
            {
                // Apply circular dead-zone for gamepad sticks
                if (look.sqrMagnitude < gamepadDeadZone * gamepadDeadZone)
                    look = Vector2.zero;
                else
                    look = look.normalized * ((look.magnitude - gamepadDeadZone) / (1f - gamepadDeadZone));
            }

            float scale = isGamepad ? gamepadSensitivity * Time.deltaTime : mouseSensitivity;

            _yaw += look.x * scale;
            _pitch -= look.y * scale;
            _pitch = Mathf.Clamp(_pitch, lookPitchMin, lookPitchMax);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraRoot != null)
                cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        void HandleMovement()
        {
            if (IsMounted) return;
            bool moving = _moveInput.sqrMagnitude > 0.01f && _cc.isGrounded;
            bool running = moving && _sprintHeld;
            var audio = Fields.Audio.ToolAudioManager.Instance;
            if (audio != null)
            {
                if (running)       { audio.StopFootstepsWalk(); audio.StartFootstepsRun(); }
                else if (moving)   { audio.StopFootstepsRun();  audio.StartFootstepsWalk(); }
                else               { audio.StopFootstepsWalk(); audio.StopFootstepsRun(); }
            }

            float targetSpeed = _sprintHeld
                ? config.baseSprintSpeed
                : config.baseWalkSpeed;

            // Carry penalty (combined hay piles + square bales)
            int baleCount = Mathf.Clamp(CarriedBaleCount, 0, 3);
            if (baleCount > 0 && config != null && config.baleCarrySpeedPenalties.Length >= baleCount)
            {
                float penalty = config.baleCarrySpeedPenalties[baleCount - 1];
                targetSpeed *= (1f - penalty);
            }

            Vector3 move = transform.TransformDirection(
                new Vector3(_moveInput.x, 0f, _moveInput.y)) * targetSpeed;

            // Vertical velocity — accumulated so gravity actually accelerates
            if (_cc.isGrounded)
            {
                _yVelocity = -2f; // small constant keeps CC pressed to ground
                if (_jumpRequested)
                {
                    _yVelocity = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
                    _jumpRequested = false;
                }
            }
            else
            {
                _yVelocity += Physics.gravity.y * Time.deltaTime;
            }
            _jumpRequested = false; // discard if not grounded
            move.y = _yVelocity;

            // External impulse (round bale push, etc.)
            move += _externalVelocity;
            _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, 10f * Time.deltaTime);

            _cc.Move(move * Time.deltaTime);
        }

        void HandleBob()
        {
            if (IsMounted) return;
            if (_moveInput.sqrMagnitude > 0.01f && _cc.isGrounded)
            {
                _bobTimer += Time.deltaTime * bobFrequency * Mathf.PI * 2f;
                _bobOffset = new Vector3(
                    Mathf.Sin(_bobTimer * 0.5f) * bobAmplitude * 0.5f,
                    Mathf.Sin(_bobTimer) * bobAmplitude,
                    0f);
            }
            else
            {
                _bobTimer = 0f;
                _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, Time.deltaTime * 8f);
            }

            if (cameraRoot != null)
                cameraRoot.localPosition = new Vector3(0f, 1.65f, 0f) + _bobOffset;
        }

        void HandleFOV()
        {
            if (IsMounted || _camera == null) return;
            bool moving = _moveInput.sqrMagnitude > 0.01f;
            float targetFOV = (_sprintHeld && moving) ? sprintFOV : baseFOV;
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFOV, Time.deltaTime * fovLerpSpeed);
        }

        void HandleHaptics()
        {
#if ENABLE_INPUT_SYSTEM
            var pad = UnityEngine.InputSystem.Gamepad.current;
            if (pad == null) return;

            if (_hapticTimer > 0f)
            {
                _hapticTimer -= Time.deltaTime;
                pad.SetMotorSpeeds(_hapticLow, _hapticHigh);
            }
            else
            {
                // Light sprint rumble
                bool sprinting = _sprintHeld && _moveInput.sqrMagnitude > 0.01f && _cc.isGrounded;
                pad.SetMotorSpeeds(sprinting ? hapticSprintLow : 0f, 0f);
            }
#endif
        }

        /// <summary>Triggers a one-shot gamepad rumble (called from tool hit events).</summary>
        public void TriggerHaptics(float low, float high, float duration)
        {
            _hapticLow   = low;
            _hapticHigh  = high;
            _hapticTimer = duration;
        }

        void RegenStamina()
        {
            if (config == null) return;
            _stamina = Mathf.Min(_stamina + config.staminaRegen * Time.deltaTime, 100f);
        }

        // ------------------------------------------------------------------ //
        // Stamina
        // ------------------------------------------------------------------ //

        public bool TryConsumeStamina(float amount)
        {
            if (_stamina < amount * 0.25f) return false; // slow but never hard-lock
            _stamina = Mathf.Max(0f, _stamina - amount);
            return true;
        }

        public float StaminaNormalized =>
            config != null ? Mathf.Clamp01(_stamina / 100f) : 1f;

        // ------------------------------------------------------------------ //
        // Carry
        // ------------------------------------------------------------------ //

        public int CarriedBaleCount => _carriedSquareBales.Count;

        public bool PickupBale(Fields.Hay.SquareBale bale)
        {
            if (_carriedSquareBales.Count >= 3) return false;
            if (!bale.CanPickup(transform)) return false;
            _carriedSquareBales.Add(bale);
            bale.OnPickup(toolHolder != null ? toolHolder : transform);
            return true;
        }

        public List<Fields.Hay.SquareBale> GetCarriedSquareBales() =>
            new List<Fields.Hay.SquareBale>(_carriedSquareBales);

        public void DropSquareBales()
        {
            for (int i = _carriedSquareBales.Count - 1; i >= 0; i--)
            {
                var b = _carriedSquareBales[i];
                _carriedSquareBales.RemoveAt(i);
                b.OnDrop(transform.position + transform.forward * 1.2f);
            }
        }

        public void ExternalPush(Vector3 force)
        {
            _externalVelocity += force;
        }

        // ------------------------------------------------------------------ //
        // Baling (hold E on cut grass — accumulation-grid based)
        // ------------------------------------------------------------------ //

        void HandleBaling()
        {
            float hayNearby = GetHayNearby();
            bool wasReady = _balingReady;
            _balingReady = hayNearby >= balingThreshold;

            // When readiness is lost, require a new E press next time
            if (wasReady && !_balingReady) _balingRequiresFreshPress = true;

            bool canBale = _interactHeld && _balingReady && !_balingRequiresFreshPress;
            if (canBale)
            {
                _balingTimer += Time.deltaTime;
                if (_balingTimer >= Mathf.Max(0.5f, balingDuration))
                    CompleteBaling();
            }
            else
            {
                _balingTimer = 0f;
            }
        }

        float GetHayNearby()
        {
            // GrassFields may be inactive at Start() — refresh until we find them
            if (_hayAccumSystems == null || _hayAccumSystems.Length == 0)
                _hayAccumSystems = Object.FindObjectsByType<Fields.Hay.HayAccumulationSystem>(FindObjectsSortMode.None);
            if (_hayAccumSystems == null || _hayAccumSystems.Length == 0) return 0f;
            float total = 0f;
            foreach (var sys in _hayAccumSystems)
                if (sys != null) total += sys.GetHayInRadius(transform.position, balingRadius);
            return total;
        }


        void CompleteBaling()
        {
            _balingTimer = 0f;
            _balingRequiresFreshPress = true; // must re-press E for next bale
            Fields.Audio.ToolAudioManager.Instance?.StopBaler();
            Fields.UI.HUDController.Instance?.TriggerBalingFlash();

            // Consume hay from accumulation grids in radius
            float needed = balingThreshold;
            if (_hayAccumSystems != null)
                foreach (var sys in _hayAccumSystems)
                {
                    if (sys == null || needed <= 0f) continue;
                    needed -= sys.ConsumeHayInRadius(transform.position, balingRadius, needed);
                }

            Vector3 spawnPos = Fields.Grass.GrassField.SnapToTerrain(transform.position + transform.forward * 1.5f);
            if (squareBalePrefab != null)
            {
                Object.Instantiate(squareBalePrefab, spawnPos, Quaternion.identity);
                Debug.Log($"[Baling] COMPLETE — SquareBale spawned at {spawnPos}");
            }
            else
                Debug.LogError("[Baling] FAILED — squareBalePrefab is NULL!");
        }

        public bool IsBaling => _interactHeld && _balingReady && !_balingRequiresFreshPress;
        public float BalingProgress => balingDuration > 0f ? Mathf.Clamp01(_balingTimer / balingDuration) : 0f;
        public bool BalingReady => _balingReady;

        // ------------------------------------------------------------------ //
        // Interact
        // ------------------------------------------------------------------ //

        // ------------------------------------------------------------------ //
        // Interact hint (used by HUD)
        // ------------------------------------------------------------------ //

        public string GetInteractHint()
        {
            if (IsMounted) return string.Empty;
            if (BalingReady)
                return IsBaling ? "Release [E] to cancel" : "Hay Making  —  Hold  [E]";

            // Forward raycast for eye-level objects
            var origin  = cameraRoot != null ? cameraRoot.position : transform.position + Vector3.up * 1.6f;
            var forward = cameraRoot != null ? cameraRoot.forward  : transform.forward;
            if (Physics.Raycast(origin, forward, out RaycastHit hit, 4f))
            {
                if (hit.collider.GetComponentInParent<Fields.Hay.SquareBale>() != null)
                    return CarriedBaleCount < 3 ? "Pick Up Bale  —  [E]" : "Carrying max bales  (3/3)";
                if (hit.collider.GetComponentInParent<IInteractable>() != null)
                    return "Interact  —  [E]";
            }
            // Ground proximity for bales (bale at feet, player looking forward)
            var groundFront = transform.position + forward * 2f + Vector3.up * 0.5f;
            foreach (var col in Physics.OverlapSphere(groundFront, 1.5f))
            {
                if (col.GetComponentInParent<Fields.Hay.SquareBale>() != null)
                    return CarriedBaleCount < 3 ? "Pick Up Bale  —  [E]" : "Carrying max bales  (3/3)";
            }

            if (CarriedBaleCount > 0) return "Drop Bales  —  [G]";
            return string.Empty;
        }

        void TryInteract()
        {
            var origin  = cameraRoot != null ? cameraRoot.position : transform.position + Vector3.up * 1.6f;
            var forward = cameraRoot != null ? cameraRoot.forward  : transform.forward;

            // Primary: eye-level forward raycast
            if (Physics.Raycast(origin, forward, out RaycastHit hit, 4f))
            {
                var ia = hit.collider.GetComponentInParent<IInteractable>();
                if (ia != null) { ia.Interact(this); return; }
            }

            // Fallback: proximity sphere in front at waist height (catches ground bales)
            var groundFront = transform.position + forward * 2f + Vector3.up * 0.5f;
            foreach (var col in Physics.OverlapSphere(groundFront, 1.5f))
            {
                var ia = col.GetComponentInParent<IInteractable>();
                if (ia != null) { ia.Interact(this); return; }
            }
        }
    }

    public interface IInteractable
    {
        void Interact(PlayerController player);
    }
}