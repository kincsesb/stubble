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

        const string HINTS =
            "<b>Irányítás</b>\n" +
            "WASD / Bal joystick — Mozgás\n" +
            "Egér / Jobb joystick — Nézés\n" +
            "Shift / RT — Sprint\n" +
            "\n" +
            "<b>Eszközök</b>\n" +
            "LMB / RT — Vágás / Használat\n" +
            "Scroll / LB–RB — Eszköz váltás\n" +
            "1–5 — Gyors eszköz kiválasztás\n" +
            "\n" +
            "<b>Objektumok</b>\n" +
            "E / X gomb — Interakció / Felvétel\n" +
            "Q / O gomb — Bála lerakás\n" +
            "\n" +
            "Tab / Select — Súgó bezárás";

        void Start()
        {
            if (tooltipText != null) tooltipText.text = HINTS;
            SetVisible(visibleOnStart);
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
