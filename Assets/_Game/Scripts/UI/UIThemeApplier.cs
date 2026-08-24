using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Drop on any screen root (MainMenu, Pause, Journal, Settings, etc.) and it will
    /// auto-apply UITheme fonts and button hover colours to every child at Start.
    ///
    /// Font assignment heuristic:
    ///   fontSize >= displayThreshold  → ShantellSans (titles, buttons)
    ///   fontSize <  displayThreshold  → CourierPrime (body, stats)
    ///   GameObjects whose name contains "Note" or "Caveat" → Caveat
    /// </summary>
    public class UIThemeApplier : MonoBehaviour
    {
        [Tooltip("Leave null to use UITheme.Instance (the ScriptableObject loaded at runtime).")]
        public UITheme theme;

        [Tooltip("TMP text at or above this font size gets the Display font (ShantellSans).")]
        public float displayThreshold = 20f;

        void Start()
        {
            var t = theme != null ? theme : UITheme.Instance;
            if (t == null) return;
            ApplyFonts(t);
            ApplyButtonColors(t);
        }

        void ApplyFonts(UITheme t)
        {
            foreach (var tmp in GetComponentsInChildren<TMP_Text>(includeInactive: true))
            {
                string n = tmp.gameObject.name;
                bool isNote = n.IndexOf("Note", System.StringComparison.OrdinalIgnoreCase) >= 0
                           || n.IndexOf("Caveat", System.StringComparison.OrdinalIgnoreCase) >= 0;

                if (isNote)
                    t.ApplyFont(tmp, UITheme.FontRole.Handwritten);
                else if (tmp.fontSize >= displayThreshold)
                    t.ApplyFont(tmp, UITheme.FontRole.Display);
                else
                    t.ApplyFont(tmp, UITheme.FontRole.Body);
            }
        }

        void ApplyButtonColors(UITheme t)
        {
            var block = new ColorBlock
            {
                normalColor      = Color.white,
                highlightedColor = new Color(1.10f, 1.05f, 0.90f, 1f),
                pressedColor     = new Color(0.78f, 0.70f, 0.56f, 1f),
                selectedColor    = Color.white,
                disabledColor    = new Color(0.65f, 0.60f, 0.52f, 0.55f),
                colorMultiplier  = 1f,
                fadeDuration     = 0.12f,
            };
            foreach (var btn in GetComponentsInChildren<Button>(includeInactive: true))
                btn.colors = block;
        }
    }
}