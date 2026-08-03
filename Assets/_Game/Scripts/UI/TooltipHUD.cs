using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fields.UI
{
    /// <summary>
    /// Context-help tooltip panel.
    ///
    /// Keyboard : Tab HOLD (>0.4 s) → show; release → hide.
    /// Gamepad  : View/Select HOLD (>0.4 s) → show; release → hide.
    ///            View/Select TAP  (<0.4 s) → opens Journal (handled separately).
    ///
    /// Subscribes to the ContextHelp InputAction (Hold interaction) so the
    /// Input System handles the tap/hold discrimination automatically.
    /// </summary>
    public class TooltipHUD : MonoBehaviour
    {
        [Header("References")]
        public GameObject panel;
        public TextMeshProUGUI tooltipText;
        public JournalScreen journalScreen;

        const string CONTROLS =
            "<b>Controls</b>\n" +
            "WASD / L-Stick — Move\n" +
            "Mouse / R-Stick — Look\n" +
            "Shift / LStick — Sprint\n" +
            "LMB / West btn — Use tool\n" +
            "Scroll / LB–RB — Switch tool\n" +
            "E / North btn — Interact\n" +
            "Q — Drop bale\n" +
            "J / View tap — Journal\n" +
            "Tab hold / View hold — This panel\n" +
            "Esc / Start — Pause";

        InputAction _helpAction;
        InputAction _journalAction;
        bool        _actionsHooked;

        // ------------------------------------------------------------------ //

        void Start()
        {
            RefreshText();
            SetVisible(false);

            if (Fields.Core.LocalizationManager.Instance != null)
                Fields.Core.LocalizationManager.Instance.OnLanguageChanged += RefreshText;

            TryHookActions();
        }

        void Update()
        {
            if (!_actionsHooked) TryHookActions();
        }

        void OnDestroy()
        {
            if (Fields.Core.LocalizationManager.Instance != null)
                Fields.Core.LocalizationManager.Instance.OnLanguageChanged -= RefreshText;

            UnhookActions();
        }

        // ------------------------------------------------------------------ //

        void TryHookActions()
        {
            // PlayerInput lives on the Player GO which starts disabled; retry each
            // frame until it becomes active (user presses Continue / New Game).
            var all = PlayerInput.all;
            if (all.Count == 0) return;
            _actionsHooked = true;

            var actions = all[0].actions;

            _helpAction = actions.FindAction("ContextHelp", throwIfNotFound: false);
            if (_helpAction != null)
            {
                _helpAction.performed += OnHelpPerformed;
                _helpAction.canceled  += OnHelpCanceled;
                _helpAction.Enable();
            }

            _journalAction = actions.FindAction("Journal", throwIfNotFound: false);
            if (_journalAction != null)
            {
                _journalAction.performed += OnJournalPerformed;
                _journalAction.Enable();
            }
        }

        void UnhookActions()
        {
            if (_helpAction != null)
            {
                _helpAction.performed -= OnHelpPerformed;
                _helpAction.canceled  -= OnHelpCanceled;
            }
            if (_journalAction != null)
                _journalAction.performed -= OnJournalPerformed;
        }

        // ------------------------------------------------------------------ //
        // Action callbacks
        // ------------------------------------------------------------------ //

        // Hold threshold reached → show tooltip (only when no other UI is open).
        void OnHelpPerformed(InputAction.CallbackContext ctx)
        {
            if (UIManager.Instance != null && UIManager.Instance.HasAnyOpen) return;
            SetVisible(true);
        }

        // Button released (hold ended or interrupted) → hide tooltip.
        void OnHelpCanceled(InputAction.CallbackContext ctx) => SetVisible(false);

        // Tap on View button or J key → toggle Journal.
        // Blocked when another screen (e.g. Pause) is already on top.
        void OnJournalPerformed(InputAction.CallbackContext ctx)
        {
            var ui = UIManager.Instance;
            if (ui == null || journalScreen == null) return;

            if (ui.IsTopmost(journalScreen))
            {
                ui.Pop();
            }
            else if (!ui.HasAnyOpen)
            {
                ui.Push(journalScreen);
            }
        }

        // ------------------------------------------------------------------ //

        void RefreshText()
        {
            if (tooltipText == null) return;
            var loc = Fields.Core.LocalizationManager.Instance;
            string hints = loc != null
                ? $"{loc.Get("hint.0")}\n{loc.Get("hint.1")}\n{loc.Get("hint.2")}\n{loc.Get("hint.3")}\n{loc.Get("hint.4")}"
                : string.Empty;
            tooltipText.text = CONTROLS + (hints.Length > 0 ? "\n\n<b>Tips</b>\n" + hints : string.Empty);
        }

        void SetVisible(bool show)
        {
            if (panel != null) panel.SetActive(show);
        }
    }
}