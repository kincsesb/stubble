using UnityEngine;
using UnityEngine.InputSystem;

namespace Fields.UI
{
    /// <summary>
    /// Listens to the Pause InputAction (Esc / Gamepad Start) and pushes PauseScreen.
    /// UIManager handles Esc when screens are already open (pops one level).
    /// This controller only acts when the stack is empty — no conflict.
    /// </summary>
    public class PauseController : MonoBehaviour
    {
        [Header("Screen reference (assign in Inspector)")]
        public PauseScreen pauseScreen;

        InputAction _pauseAction;
        bool        _hooked;

        // ------------------------------------------------------------------ //

        void Start()  => TryHook();
        void Update() { if (!_hooked) TryHook(); }

        void OnDestroy()
        {
            if (_pauseAction != null)
                _pauseAction.performed -= OnPausePerformed;
        }

        void TryHook()
        {
            var all = PlayerInput.all;
            if (all.Count == 0) return;
            _hooked = true;

            _pauseAction = all[0].actions.FindAction("Pause", throwIfNotFound: false);
            if (_pauseAction != null)
            {
                _pauseAction.performed += OnPausePerformed;
                _pauseAction.Enable();
            }
            else
            {
                Debug.LogWarning("[PauseController] 'Pause' action not found in InputActionAsset.");
            }
        }

        // ------------------------------------------------------------------ //

        void OnPausePerformed(InputAction.CallbackContext ctx)
        {
            if (UIManager.Instance == null || pauseScreen == null) return;

            // Terminal intercepts ESC first — don't open pause menu behind it.
            if (Fields.World.CheatCodeTerminal.IsAnyOpen) return;

            // If UI is already open, UIManager's Update() handles Esc → Pop().
            // We only open the pause menu when the stack is empty.
            if (UIManager.Instance.HasAnyOpen) return;

            // Guard: only open pause when the game is actually running.
            // PlayerController.Instance is null while the player root is inactive (main menu).
            var pc = Fields.Core.PlayerController.Instance;
            if (pc == null || !pc.gameObject.activeInHierarchy) return;

            UIManager.Instance.Push(pauseScreen);
        }
    }
}