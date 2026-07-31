using System.Collections.Generic;
using Fields.Core.Data;
using Fields.Hay;
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

        [Header("Head Bob (placeholder values, tuned in P3)")]
        public float bobAmplitude = 0.03f;
        public float bobFrequency = 1.8f;

        // Components
        CharacterController _cc;

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

        // Stamina
        float _stamina = 100f;

        // Carry
        List<HayPile> _carriedBales = new List<HayPile>(3);
        List<Fields.Hay.SquareBale> _carriedSquareBales = new List<Fields.Hay.SquareBale>(3);

        // External velocity (applied next move frame)
        Vector3 _externalVelocity;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _stamina = config != null ? config.hayUnitsPerCollectionCell : 100f;
        }

        void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            HandleLook();
            HandleMovement();
            HandleBob();
            RegenStamina();
        }

        // ------------------------------------------------------------------ //
        // Input System callbacks (wired via PlayerInput component)
        // ------------------------------------------------------------------ //

        public void OnMove(InputValue value) => _moveInput = value.Get<Vector2>();

        public void OnLook(InputValue value) => _lookInput = value.Get<Vector2>();

        public void OnSprint(InputValue value) => _sprintHeld = value.isPressed;

        public void OnInteract(InputValue value)
        {
            if (value.isPressed) TryInteract();
        }

        public void OnDrop(InputValue value)
        {
            if (value.isPressed && _carriedBales.Count > 0) DropTopBale();
        }

        // ------------------------------------------------------------------ //
        // Movement
        // ------------------------------------------------------------------ //

        void HandleLook()
        {
            Vector2 look = _lookInput;

            // Scale based on input device — Input System provides pixels for mouse,
            // normalised for gamepad; distinguish by magnitude.
            bool isGamepad = look.sqrMagnitude <= 1.01f && !Mouse.current.delta.IsActuated();
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

            // Gravity
            if (!_cc.isGrounded) move.y -= 9.81f * Time.deltaTime;

            // External impulse (round bale push, etc.)
            move += _externalVelocity;
            _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, 10f * Time.deltaTime);

            _cc.Move(move * Time.deltaTime);
        }

        void HandleBob()
        {
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

        public bool TryPickupBale(HayPile bale)
        {
            if (_carriedBales.Count >= 3) return false;
            if (!bale.CanPickup(transform)) return false;
            _carriedBales.Add(bale);
            bale.OnPickup(toolHolder != null ? toolHolder : transform);
            return true;
        }

        void DropTopBale()
        {
            if (_carriedBales.Count == 0) return;
            var top = _carriedBales[^1];
            _carriedBales.RemoveAt(_carriedBales.Count - 1);
            top.OnDrop(transform.position + transform.forward * 1.2f);
        }

        public int CarriedBaleCount => _carriedBales.Count + _carriedSquareBales.Count;

        public bool PickupBale(Fields.Hay.SquareBale bale)
        {
            if (_carriedSquareBales.Count + _carriedBales.Count >= 3) return false;
            if (!bale.CanPickup(transform)) return false;
            _carriedSquareBales.Add(bale);
            bale.OnPickup(toolHolder != null ? toolHolder : transform);
            return true;
        }

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
        // Interact
        // ------------------------------------------------------------------ //

        void TryInteract()
        {
            if (!Physics.Raycast(
                    cameraRoot != null ? cameraRoot.position : transform.position + Vector3.up * 1.6f,
                    cameraRoot != null ? cameraRoot.forward : transform.forward,
                    out RaycastHit hit, 2.5f))
                return;

            if (hit.collider.TryGetComponent<IInteractable>(out var interactable))
                interactable.Interact(this);
        }
    }

    public interface IInteractable
    {
        void Interact(PlayerController player);
    }
}