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
        public float balingThreshold = 540f;
        [Tooltip("Seconds of holding E to produce a bale")]
        public float balingDuration = 2.5f;
        [Tooltip("SquareBale prefab spawned when baling completes (no round baler)")]
        public GameObject squareBalePrefab;
        [Tooltip("Bale prefab used when the round baler upgrade is owned (4× value). Falls back to squareBalePrefab if unassigned.")]
        public GameObject roundBalePrefab;

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
        Fields.Tools.ToolHolder _toolHolder;

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

        // Current parcel (set externally by WorldBootstrap on ParcelBoundary enter/exit)
        public int CurrentParcelIndex { get; set; } = 0;

        // Baling
        bool _interactHeld;
        bool _balingReady;
        float _balingTimer;
        bool _balingRequiresFreshPress = true; // prevents auto-fire when E held before ready
        bool _balingActive;                     // tracks baling-in-progress to detect start transition
        Fields.Hay.HayAccumulationSystem[] _hayAccumSystems;

        // Camera lock (frozen during baling)
        bool _lookLocked;
        float _lockedYaw;
        float _lockedPitch;

        // Vertical velocity (gravity + jump, accumulated across frames)
        float _yVelocity;

        // Jump request flag — set by OnJump, consumed in HandleMovement
        bool _jumpRequested;

        // External velocity (applied next move frame)
        Vector3 _externalVelocity;

        // Round bale pushing
        Fields.Hay.RoundBale _nearestBale;
        Fields.Hay.RoundBale _pushingBale;

        // Square bale outline highlight
        Fields.Hay.SquareBale _highlightedSquareBale;

        // When true, HandleMovement/Bob/FOV are suppressed (e.g. riding mower)
        public bool IsMounted { get; set; }

        // When true, all player input is blocked (shop open, etc.)
        public bool InputLocked { get; set; }

        // Set by SaveSystem.ApplySaveData() so StartGame(freshStart:false) can teleport to saved pos
        public static Vector3? PendingSpawnPosition { get; set; }
        public static float    PendingSpawnRotY     { get; set; }

        public void SetYaw(float yaw) { _yaw = yaw; }

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
            _toolHolder = GetComponentInChildren<Fields.Tools.ToolHolder>(true);
        }

        void Update()
        {
            HandleLook();
            ScanNearestBale();
            ScanNearestSquareBale();
            HandleMovement();
            HandleRoundBalePush();
            HandleBob();
            HandleFOV();
            HandleHaptics();
            RegenStamina();
            HandleBaling();
        }

        // ------------------------------------------------------------------ //
        // Input System callbacks (wired via PlayerInput component)
        // ------------------------------------------------------------------ //

        public void OnMove(InputValue value)
        {
            _moveInput = InputLocked ? Vector2.zero : value.Get<Vector2>();
        }

        public void OnLook(InputValue value) => _lookInput = value.Get<Vector2>();

        public void OnSprint(InputValue value) => _sprintHeld = InputLocked ? false : value.isPressed;

        public void OnInteract(InputValue value)
        {
            if (InputLocked) return;
            _interactHeld = value.isPressed;
            if (value.isPressed)
            {
                if (_balingReady && IsLookingDown)
                {
                    _balingRequiresFreshPress = false;
                }
                else if (_pushingBale != null)
                {
                    // Toggle off — re-pressing E releases the bale
                    _pushingBale.StopPush();
                    _pushingBale = null;
                    _balingRequiresFreshPress = true;
                }
                else if (_nearestBale != null)
                {
                    // Toggle on — start pushing
                    _pushingBale = _nearestBale;
                    _pushingBale.StartPush(this);
                    _balingRequiresFreshPress = true;
                }
                else
                {
                    TryInteract();
                }
            }
            else
            {
                // E released: only affects baling gate, not bale push (push is now toggle)
                _balingRequiresFreshPress = true;
            }
        }

        public void OnJump(InputValue value)
        {
            if (!InputLocked && value.isPressed) _jumpRequested = true;
        }

        public void OnDrop(InputValue value)
        {
            if (!InputLocked && value.isPressed && _carriedSquareBales.Count > 0) DropSquareBales();
        }

        // ------------------------------------------------------------------ //
        // Movement
        // ------------------------------------------------------------------ //

        void HandleLook()
        {
            if (InputLocked)
            {
                _lookInput = Vector2.zero;
                return;
            }
            if (_lookLocked)
            {
                transform.rotation = Quaternion.Euler(0f, _lockedYaw, 0f);
                if (cameraRoot != null)
                    cameraRoot.localRotation = Quaternion.Euler(_lockedPitch, 0f, 0f);
                return;
            }
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

            if (!IsMounted)
                _yaw += look.x * scale;
            _pitch -= look.y * scale;
            _pitch = Mathf.Clamp(_pitch, lookPitchMin, lookPitchMax);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (cameraRoot != null)
                cameraRoot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>Called by mounted vehicles to keep the camera facing vehicle forward.</summary>
        public void SyncMountedYaw(float worldYaw) => _yaw = worldYaw;

        void HandleMovement()
        {
            if (IsMounted) return;
            if (_pushingBale != null)
            {
                // Only apply gravity — XZ movement is controlled by bale push
                if (!_cc.isGrounded) _yVelocity += Physics.gravity.y * Time.deltaTime;
                else _yVelocity = -2f;
                _cc.Move(new Vector3(0f, _yVelocity * Time.deltaTime, 0f));
                return;
            }
            // Block WASD movement while baling; keep gravity + jump ticking normally
            Vector2 moveInput = IsBaling ? Vector2.zero : _moveInput;
            bool moving = moveInput.sqrMagnitude > 0.01f && _cc.isGrounded;
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
                new Vector3(moveInput.x, 0f, moveInput.y)) * targetSpeed;

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
            bool sprinting = _sprintHeld && _moveInput.sqrMagnitude > 0.01f && _cc.isGrounded && !IsMounted;
            if (sprinting)
                _stamina = Mathf.Max(0f, _stamina - config.staminaRegen * 3f * Time.deltaTime);
            else
                _stamina = Mathf.Min(_stamina + config.staminaRegen * Time.deltaTime, 100f);
        }

        // ------------------------------------------------------------------ //
        // Stamina
        // ------------------------------------------------------------------ //

        public bool TryConsumeStamina(float amount)
        {
            if (CheatCodeActivator.Instance?.IsIddqdActive == true) return true;
            if (_stamina < amount * 0.25f) return false; // slow but never hard-lock
            _stamina = Mathf.Max(0f, _stamina - amount);
            return true;
        }

        public void RefillStamina() => _stamina = 100f;

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
            int index = _carriedSquareBales.Count; // 0, 1, or 2
            _carriedSquareBales.Add(bale);
            // Carrier = player root (not HandsRoot/CameraRoot child) so the
            // bale stays horizontal regardless of camera pitch.
            bale.OnPickup(transform, index);
            // Auto-switch to barehand so carried bales are always visible
            _toolHolder?.EquipBareHand();
            return true;
        }

        public List<Fields.Hay.SquareBale> GetCarriedSquareBales() =>
            new List<Fields.Hay.SquareBale>(_carriedSquareBales);

        public void DropSquareBales()
        {
            int count = _carriedSquareBales.Count;
            for (int i = count - 1; i >= 0; i--)
            {
                var b = _carriedSquareBales[i];
                _carriedSquareBales.RemoveAt(i);
                b.OnDrop(transform.position + transform.forward * 1.2f);
            }
            if (count > 0)
            {
                Fields.Feel.GameFeelController.Instance?.StartShake(0.20f, 0.032f * count, 20f);
                Fields.UI.HUDController.Instance?.TriggerBaleDropFeel(count);
            }
        }

        public void ExternalPush(Vector3 force)
        {
            _externalVelocity += force;
        }

        /// <summary>Snap the player's XZ position (used by RoundBale to keep player at bale edge).</summary>
        public void SetPositionXZ(Vector3 worldPos)
        {
            Vector3 delta = new Vector3(worldPos.x - transform.position.x, 0f, worldPos.z - transform.position.z);
            _cc.Move(delta);
        }

        // ------------------------------------------------------------------ //
        // Square bale outline highlight
        // ------------------------------------------------------------------ //

        void ScanNearestSquareBale()
        {
            Fields.Hay.SquareBale nearest = null;

            if (!InputLocked && !IsMounted && CarriedBaleCount < 3)
            {
                var origin  = cameraRoot != null ? cameraRoot.position : transform.position + Vector3.up * 1.6f;
                var forward = cameraRoot != null ? cameraRoot.forward  : transform.forward;

                // Eye-level raycast
                if (Physics.Raycast(origin, forward, out RaycastHit hit, 4f))
                    nearest = hit.collider.GetComponentInParent<Fields.Hay.SquareBale>();

                // Ground proximity sphere (bales at feet)
                if (nearest == null)
                {
                    var groundFront = transform.position + forward * 2f + Vector3.up * 0.5f;
                    float closestSq = float.MaxValue;
                    foreach (var col in Physics.OverlapSphere(groundFront, 1.5f))
                    {
                        var sb = col.GetComponentInParent<Fields.Hay.SquareBale>();
                        if (sb == null || sb.IsCarried) continue;
                        float dSq = (sb.transform.position - transform.position).sqrMagnitude;
                        if (dSq < closestSq) { closestSq = dSq; nearest = sb; }
                    }
                }
            }

            if (nearest == _highlightedSquareBale) return;
            _highlightedSquareBale?.SetHighlighted(false);
            _highlightedSquareBale = nearest;
            _highlightedSquareBale?.SetHighlighted(true);
        }

        // ------------------------------------------------------------------ //
        // Round bale interaction
        // ------------------------------------------------------------------ //

        void ScanNearestBale()
        {
            if (_pushingBale != null) return; // already pushing one

            _nearestBale = null;
            float closestSq = float.MaxValue;
            foreach (var col in Physics.OverlapSphere(transform.position, 3.5f))
            {
                var rb = col.GetComponentInParent<Fields.Hay.RoundBale>();
                if (rb == null || !rb.CanStartPush(this)) continue;
                float dSq = (rb.transform.position - transform.position).sqrMagnitude;
                if (dSq < closestSq) { closestSq = dSq; _nearestBale = rb; }
            }
        }

        void HandleRoundBalePush()
        {
            if (_pushingBale == null) return;

            // Safety: stop if bale got too far
            if ((_pushingBale.transform.position - transform.position).sqrMagnitude > 25f)
            {
                _pushingBale.StopPush();
                _pushingBale = null;
                return;
            }

            _pushingBale.PushUpdate(_moveInput.y, _moveInput.x);
        }

        // ------------------------------------------------------------------ //
        // Baling (hold E on cut grass — accumulation-grid based)
        // ------------------------------------------------------------------ //

        void HandleBaling()
        {
            float hayNearby = GetHayNearby();
            bool wasReady = _balingReady;
            _balingReady = hayNearby >= balingThreshold;

            // When readiness is lost, cancel active baling and require new E press
            if (wasReady && !_balingReady)
            {
                _balingRequiresFreshPress = true;
                if (_balingActive)
                {
                    _balingActive = false;
                    _balingTimer  = 0f;
                    _lookLocked   = false;
                }
                return;
            }

            // Start baling on single E press — no need to hold
            if (!_balingActive && _interactHeld && _balingReady && !_balingRequiresFreshPress && IsLookingDown)
            {
                _balingActive = true;
                _balingTimer  = 0f;
                _lookLocked   = true;
                _lockedYaw    = _yaw;
                _lockedPitch  = _pitch;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible   = false;
                _toolHolder?.EquipBareHand();
            }

            // Once started, runs to completion regardless of E being held
            if (_balingActive)
            {
                _balingTimer += Time.deltaTime;
                if (_balingTimer >= Mathf.Max(0.5f, balingDuration))
                    CompleteBaling();
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
            _balingActive = false;
            _lookLocked   = false;
            _balingTimer  = 0f;
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

            bool makeRound = Fields.Economy.BalerManager.Instance?.RoundBalerOwned == true;
            GameObject prefab = makeRound && roundBalePrefab != null ? roundBalePrefab : squareBalePrefab;

            Vector3 spawnPos = Fields.Grass.GrassField.SnapToTerrain(transform.position + transform.forward * 1.5f);
            if (prefab != null)
            {
                var baleGO = Object.Instantiate(prefab, spawnPos, Quaternion.identity);
                var bale   = baleGO.GetComponent<Fields.Hay.SquareBale>();
                if (bale != null)
                {
                    bale.OriginParcelIndex = CurrentParcelIndex;
                    bale.IsRound = makeRound;
                }
                Fields.Core.GameEvents.FireBaleCreated(CurrentParcelIndex, 0, isRound: makeRound);
                Fields.Core.GameEvents.FireHayConsumed(CurrentParcelIndex, 0);
                Debug.Log($"[Baling] COMPLETE — {(makeRound ? "RoundBale" : "SquareBale")} spawned at {spawnPos} (parcel {CurrentParcelIndex})");
            }
            else
                Debug.LogError("[Baling] FAILED — balePrefab is NULL!");
        }

        // Positive pitch = looking down (camera pitched below horizon)
        public bool IsLookingDown => _pitch > 30f;

        public bool IsBaling => _balingActive;
        public float BalingProgress => balingDuration > 0f ? Mathf.Clamp01(_balingTimer / balingDuration) : 0f;
        public bool BalingReady => _balingReady && IsLookingDown;

        // ------------------------------------------------------------------ //
        // Interact
        // ------------------------------------------------------------------ //

        // ------------------------------------------------------------------ //
        // Interact hint (used by HUD)
        // ------------------------------------------------------------------ //

        static string L(string key) =>
            Fields.Core.LocalizationManager.Instance != null
                ? Fields.Core.LocalizationManager.Instance.Get(key)
                : key;

        public string GetInteractHint()
        {
            if (IsMounted) return string.Empty;

            // Pushing a bale takes priority over all other hints
            if (_pushingBale != null) return L("hud.bale_push_active");

            // BalingReady now includes IsLookingDown check — only show hint when looking down at hay
            if (BalingReady)
                return IsBaling ? L("hud.baling_cancel") : L("hud.baling_start");

            // Round bale nearby — offer push hint before other interactions
            if (_nearestBale != null) return L("hud.bale_push_start");

            // Forward raycast for eye-level objects
            var origin  = cameraRoot != null ? cameraRoot.position : transform.position + Vector3.up * 1.6f;
            var forward = cameraRoot != null ? cameraRoot.forward  : transform.forward;
            if (Physics.Raycast(origin, forward, out RaycastHit hit, 4f))
            {
                if (hit.collider.GetComponentInParent<Fields.Hay.SquareBale>() != null)
                    return CarriedBaleCount < 3 ? L("hud.pickup_bale") : L("hud.carry_full");
                if (hit.collider.GetComponentInParent<Fields.World.CheatCodeTerminal>() != null)
                    return "[E]  Use Computer";
                var hitIa = hit.collider.GetComponentInParent<IInteractable>();
                if (hitIa != null)
                    return hitIa is IHintProvider hp ? hp.GetHint(this) : L("hud.interact");
            }
            // Ground proximity for bales (bale at feet, player looking forward)
            var groundFront = transform.position + forward * 2f + Vector3.up * 0.5f;
            foreach (var col in Physics.OverlapSphere(groundFront, 1.5f))
            {
                if (col.GetComponentInParent<Fields.Hay.SquareBale>() != null)
                    return CarriedBaleCount < 3 ? L("hud.pickup_bale") : L("hud.carry_full");
            }

            if (CarriedBaleCount > 0) return L("hud.drop_bales");
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

    /// <summary>Optional extension for IInteractable — returns a context-sensitive HUD hint.</summary>
    public interface IHintProvider
    {
        string GetHint(PlayerController player);
    }
}