using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fields.UI
{
    /// <summary>
    /// Screen stack manager. The single point of control for all UI screens.
    ///
    /// Rules enforced here:
    ///  - Only the topmost screen is interactive.
    ///  - Esc / gamepad B always pops one level — never traps the player.
    ///  - Opening any screen unlocks the cursor; popping the last one re-locks it.
    ///  - Gamepad focus is saved per screen and restored on resume/pop.
    ///
    /// Screens are pooled: Push shows them, Pop hides them.
    /// Never instantiate or destroy screens through UIManager.
    ///
    /// Usage:
    ///   UIManager.Instance.Push(myScreen);   // open
    ///   UIManager.Instance.Pop();             // close topmost
    ///   UIManager.Instance.PopAll();          // return to gameplay
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [Header("Theme")]
        [Tooltip("Assign the UITheme ScriptableObject here so UITheme.Instance is available at runtime.")]
        public UITheme theme;

        [Header("Pause")]
        [Tooltip("Assign PauseScreen here. UIManager opens it when Esc/Start is pressed with no UI open.")]
        public PauseScreen pauseScreen;

        readonly Stack<UIScreen> _stack = new Stack<UIScreen>();

        // ------------------------------------------------------------------ //
        // Lifecycle
        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void OnEnable()  => SceneManager.sceneLoaded += OnSceneLoaded;
        void OnDisable() => SceneManager.sceneLoaded -= OnSceneLoaded;

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Scene transition clears any leftover stack state without running Close()
            // callbacks (those screens are already destroyed with the old scene).
            _stack.Clear();
            UpdateCursor();
        }

        void Update()
        {
            // Esc / Start: pop when UI is open, open pause when UI is closed.
            // Handled here (raw polling) to avoid InputAction callback race conditions
            // where callbacks fire before this Update and cause immediate open+close.
            bool cancelOrPause = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                              || (Gamepad.current  != null && Gamepad.current.startButton.wasPressedThisFrame);
            bool cancelOnly    =  Gamepad.current  != null && Gamepad.current.buttonEast.wasPressedThisFrame;

            if (cancelOrPause || cancelOnly)
            {
                // Shop is outside the UIManager stack — handle it first so ESC closes the shop
                // instead of opening the pause menu.
                if (ShopUI.Instance != null && ShopUI.Instance.IsOpen)
                {
                    ShopUI.Instance.Close();
                    return;
                }

                // Cheat terminal intercepts ESC — close it instead of opening pause menu.
                if (Fields.World.CheatCodeTerminal.IsAnyOpen)
                {
                    Fields.World.CheatCodeTerminal.CloseIfOpen();
                    return;
                }

                if (_stack.Count > 0)
                    Pop();
                else if (cancelOrPause && pauseScreen != null)
                    Push(pauseScreen);
            }
        }

        // ------------------------------------------------------------------ //
        // Public API
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Push a screen onto the stack. It becomes topmost and interactive.
        /// The screen below (if any) is buried — visible but non-interactive.
        /// </summary>
        public void Push(UIScreen screen)
        {
            if (screen == null)
            {
                Debug.LogWarning("[UIManager] Push called with null screen.");
                return;
            }

            // Prevent double-push of the same screen instance.
            if (_stack.Count > 0 && _stack.Peek() == screen) return;

            if (_stack.Count > 0)
                _stack.Peek().OnBury();

            _stack.Push(screen);
            screen.OnPush();

            UpdateCursor();
        }

        /// <summary>
        /// Close the topmost screen and restore the one below to interactive.
        /// Does nothing if the stack is empty.
        /// </summary>
        public void Pop()
        {
            if (_stack.Count == 0) return;

            _stack.Pop().OnClose();

            if (_stack.Count > 0)
                _stack.Peek().OnResume();

            UpdateCursor();
        }

        /// <summary>Close every screen and return to gameplay.</summary>
        public void PopAll()
        {
            while (_stack.Count > 0)
                _stack.Pop().OnClose();

            UpdateCursor();
        }

        /// <summary>True if this screen is anywhere in the stack.</summary>
        public bool IsOpen(UIScreen screen)
        {
            foreach (var s in _stack)
                if (s == screen) return true;
            return false;
        }

        /// <summary>True if the given screen is the topmost (active) screen.</summary>
        public bool IsTopmost(UIScreen screen) =>
            _stack.Count > 0 && _stack.Peek() == screen;

        public bool HasAnyOpen => _stack.Count > 0;

        public int Depth => _stack.Count;

        // ------------------------------------------------------------------ //
        // Cursor
        // ------------------------------------------------------------------ //

        void UpdateCursor()
        {
            bool uiOpen = _stack.Count > 0;
            Cursor.lockState = uiOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible   = uiOpen;
        }
    }
}