using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Drop this on any Canvas (or the scene root) and it will apply a consistent
    /// hover ColorBlock to every Button in that Canvas at runtime.
    /// Also safe to call manually: UIButtonHoverStyle.ApplyToButton(btn).
    /// </summary>
    public class UIButtonHoverStyle : MonoBehaviour
    {
        [Tooltip("Leave at 0,0,0,0 to use the project defaults below.")]
        public Color normalColor      = new Color(1.00f, 1.00f, 1.00f, 1.00f);
        public Color highlightedColor = new Color(1.00f, 1.00f, 1.00f, 1.00f);
        public Color pressedColor     = new Color(0.80f, 0.90f, 1.00f, 1.00f);
        public Color selectedColor    = new Color(1.00f, 1.00f, 1.00f, 1.00f);
        public Color disabledColor    = new Color(0.50f, 0.50f, 0.50f, 0.50f);
        public float colorMultiplier  = 3f;
        public float fadeDuration     = 0.12f;

        void Start() => ApplyToAll();

        /// <summary>Applies the style to all Buttons in this GameObject's hierarchy.</summary>
        public void ApplyToAll()
        {
            foreach (var btn in GetComponentsInChildren<Button>(includeInactive: true))
                ApplyToButton(btn, BuildBlock());
        }

        ColorBlock BuildBlock() => new ColorBlock
        {
            normalColor      = normalColor,
            highlightedColor = highlightedColor,
            pressedColor     = pressedColor,
            selectedColor    = selectedColor,
            disabledColor    = disabledColor,
            colorMultiplier  = colorMultiplier,
            fadeDuration     = fadeDuration,
        };

        // ── Static helpers ─────────────────────────────────────────────── //

        public static readonly ColorBlock DefaultBlock = new ColorBlock
        {
            normalColor      = new Color(1.00f, 1.00f, 1.00f, 1.00f),
            highlightedColor = new Color(1.00f, 1.00f, 1.00f, 1.00f),
            pressedColor     = new Color(0.80f, 0.90f, 1.00f, 1.00f),
            selectedColor    = new Color(1.00f, 1.00f, 1.00f, 1.00f),
            disabledColor    = new Color(0.50f, 0.50f, 0.50f, 0.50f),
            colorMultiplier  = 3f,
            fadeDuration     = 0.12f,
        };

        public static void ApplyToButton(Button btn) => ApplyToButton(btn, DefaultBlock);

        public static void ApplyToButton(Button btn, ColorBlock block)
        {
            if (btn == null) return;
            btn.colors = block;
        }
    }
}
