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
        public Color highlightedColor = new Color(0.88f, 0.95f, 0.72f, 1.00f);  // sage green hover
        public Color pressedColor     = new Color(0.68f, 0.78f, 0.50f, 1.00f);  // darker sage pressed
        public Color selectedColor    = new Color(1.00f, 1.00f, 1.00f, 1.00f);
        public Color disabledColor    = new Color(0.65f, 0.60f, 0.52f, 0.55f);  // muted taupe
        public float colorMultiplier  = 1f;
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
            highlightedColor = new Color(0.88f, 0.95f, 0.72f, 1.00f),  // sage green hover
            pressedColor     = new Color(0.68f, 0.78f, 0.50f, 1.00f),  // darker sage pressed
            selectedColor    = new Color(1.00f, 1.00f, 1.00f, 1.00f),
            disabledColor    = new Color(0.65f, 0.60f, 0.52f, 0.55f),  // muted taupe
            colorMultiplier  = 1f,
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
