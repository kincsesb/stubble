using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Central ScriptableObject holding all UI sprites, fonts and colours.
    /// Create one asset via Create → Game → UI Theme, assign it in the scene,
    /// and all UI builders pick it up through UITheme.Instance.
    ///
    /// After creating this asset you still need to:
    ///  1. Set every PNG in Assets/UI to TextureType = Sprite (2D and UI) in Import Settings.
    ///  2. For each .ttf in Assets/_Game/Fonts, right-click → Create → TextMesh Pro → Font Asset.
    ///  3. Assign the resulting sprites and font assets in the Inspector here.
    /// </summary>
    [CreateAssetMenu(fileName = "UITheme", menuName = "Game/UI Theme")]
    public class UITheme : ScriptableObject
    {
        static UITheme _instance;
        public static UITheme Instance => _instance;
        void OnEnable()  => _instance = this;
        void OnDisable() { if (_instance == this) _instance = null; }

        // ── Sprites ────────────────────────────────────────────────────────── //

        [Header("Button sprites")]
        [Tooltip("button-empty-1.png — wide rectangular text button")]
        public Sprite buttonWide;
        [Tooltip("button-square-empty.png — small square icon button")]
        public Sprite buttonSquare;
        [Tooltip("button-circle-empty.png — round icon button")]
        public Sprite buttonCircle;

        [Header("Panel sprites")]
        [Tooltip("panel-empty-1.png — horizontal info board with brass rivets")]
        public Sprite panelBg;
        [Tooltip("book-empty-1.png — open spiral notebook (Journal background)")]
        public Sprite bookBg;
        [Tooltip("empty-notebook-1.png — upright notebook with tabs (Settings background)")]
        public Sprite notebookBg;
        [Tooltip("tag-empty-1.png — price tag with string (Shop item label)")]
        public Sprite tagLabel;

        [Header("Tab sprites")]
        [Tooltip("tab-empty-1.png — tab with paperclip (active state)")]
        public Sprite tabActive;
        [Tooltip("tab-empty-2.png — tab without paperclip (inactive state)")]
        public Sprite tabInactive;

        [Header("Progress bar sprites")]
        [Tooltip("progress-bar-empty-1.png — wooden-framed horizontal bar")]
        public Sprite progressBarBg;
        [Tooltip("progress-bar-empty-2.png — rope-framed horizontal bar")]
        public Sprite progressBarRope;

        [Header("Scrollbar sprites")]
        [Tooltip("scrollbar-empty.png — vertical track")]
        public Sprite scrollbarTrack;
        [Tooltip("scrollable-thingy-1.png — round thumb handle")]
        public Sprite scrollbarThumb;

        // ── Fonts ──────────────────────────────────────────────────────────── //

        [Header("Fonts — create TMP Font Asset from each .ttf then assign here")]
        [Tooltip("ShantellSans — titles, buttons, tab labels, headings")]
        public TMP_FontAsset fontDisplay;
        [Tooltip("CourierPrime — body text, stats, HUD numbers, descriptions")]
        public TMP_FontAsset fontBody;
        [Tooltip("Caveat — handwritten notes, small cetli labels (optional)")]
        public TMP_FontAsset fontHandwritten;

        // ── Colour palette ─────────────────────────────────────────────────── //

        [Header("Colours")]
        public Color inkDark       = new Color(0.18f, 0.12f, 0.08f, 1f);   // dark brown ink
        public Color inkMedium     = new Color(0.40f, 0.32f, 0.22f, 1f);   // mid brown ink
        public Color parchment     = new Color(0.94f, 0.88f, 0.76f, 1f);   // cream/parchment
        public Color parchmentDark = new Color(0.78f, 0.68f, 0.52f, 1f);   // darker parchment
        public Color accentGreen   = new Color(0.32f, 0.47f, 0.25f, 1f);   // olive green
        public Color accentGold    = new Color(0.72f, 0.55f, 0.18f, 1f);   // warm rustic gold
        public Color accentRed     = new Color(0.62f, 0.22f, 0.15f, 1f);   // muted barn red
        public Color ropeColor     = new Color(0.72f, 0.58f, 0.32f, 1f);   // rope/jute

        // ── Helpers ────────────────────────────────────────────────────────── //

        public void ApplyFont(TMP_Text text, FontRole role)
        {
            if (text == null) return;
            switch (role)
            {
                case FontRole.Display     when fontDisplay     != null: text.font = fontDisplay;     break;
                case FontRole.Body        when fontBody        != null: text.font = fontBody;        break;
                case FontRole.Handwritten when fontHandwritten != null: text.font = fontHandwritten; break;
            }
        }

        public enum FontRole { Display, Body, Handwritten }
    }
}