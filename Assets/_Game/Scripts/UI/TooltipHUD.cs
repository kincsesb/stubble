using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Fields.UI
{
    /// <summary>
    /// Displays a compact control-hints panel in the HUD.
    /// Toggle visibility with Tab or Gamepad Select.
    /// </summary>
    public class TooltipHUD : MonoBehaviour
    {
        [Header("References")]
        public GameObject panel;
        public TextMeshProUGUI tooltipText;

        [Header("Settings")]
        public bool visibleOnStart = true;

        // Control layout is language-independent — kept as-is.
        // Game mechanic hints (max 5 per spec §8.11) pulled from LocalizationManager.
        const string CONTROLS =
            "<b>Controls</b>\n" +
            "WASD / L-Stick — Move\n" +
            "Mouse / R-Stick — Look\n" +
            "Shift / RT — Sprint\n" +
            "LMB / RT — Use tool\n" +
            "Scroll / LB–RB — Switch tool\n" +
            "E / X — Interact\n" +
            "Q / O — Drop bale\n" +
            "Tab / Select — Close";

        void Start()
        {
            RefreshText();
            SetVisible(visibleOnStart);
            if (Fields.Core.LocalizationManager.Instance != null)
                Fields.Core.LocalizationManager.Instance.OnLanguageChanged += RefreshText;
        }

        void OnDestroy()
        {
            if (Fields.Core.LocalizationManager.Instance != null)
                Fields.Core.LocalizationManager.Instance.OnLanguageChanged -= RefreshText;
        }

        void RefreshText()
        {
            if (tooltipText == null) return;
            var loc = Fields.Core.LocalizationManager.Instance;
            string hints = loc != null
                ? $"{loc.Get("hint.0")}\n{loc.Get("hint.1")}\n{loc.Get("hint.2")}\n{loc.Get("hint.3")}\n{loc.Get("hint.4")}"
                : string.Empty;
            tooltipText.text = CONTROLS + (hints.Length > 0 ? "\n\n<b>Tips</b>\n" + hints : string.Empty);
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.tabKey.wasPressedThisFrame)
                SetVisible(!IsVisible);

            if (Gamepad.current != null && Gamepad.current.selectButton.wasPressedThisFrame)
                SetVisible(!IsVisible);
        }

        void SetVisible(bool show)
        {
            if (panel != null) panel.SetActive(show);
        }

        bool IsVisible => panel != null && panel.activeSelf;
    }
}
