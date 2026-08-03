using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Scrollable credits screen.
    /// Auto-scrolls from top to bottom; any key input or gamepad button skips back.
    /// Closes itself when the scroll reaches the bottom.
    /// </summary>
    public class CreditsScreen : UIScreen
    {
        [Header("Scroll")]
        public ScrollRect scrollRect;
        [Tooltip("Pixels per second for automatic scrolling.")]
        public float scrollSpeed = 30f;

        [Header("Navigation")]
        public Button backButton;

        bool _autoScroll;

        protected override void Awake()
        {
            base.Awake();
            if (backButton) backButton.onClick.AddListener(() => UIManager.Instance?.Pop());
        }

        protected override void OnScreenPushed()
        {
            _autoScroll = true;
            if (scrollRect) scrollRect.verticalNormalizedPosition = 1f;
        }

        protected override void OnScreenClosed()
        {
            _autoScroll = false;
        }

        void Update()
        {
            if (!_autoScroll || scrollRect == null) return;

            // Any key or gamepad press dismisses credits immediately (spec §3).
            bool anyKey    = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool anyButton = Gamepad.current  != null && (
                Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.buttonEast.wasPressedThisFrame  ||
                Gamepad.current.startButton.wasPressedThisFrame);

            if (anyKey || anyButton)
            {
                UIManager.Instance?.Pop();
                return;
            }

            // Content height in pixels / 1000 gives scroll rate in normalized units per second.
            float contentHeight = scrollRect.content != null
                ? scrollRect.content.rect.height
                : 1000f;
            float step = scrollSpeed / contentHeight * Time.unscaledDeltaTime;
            scrollRect.verticalNormalizedPosition =
                Mathf.Clamp01(scrollRect.verticalNormalizedPosition - step);

            if (scrollRect.verticalNormalizedPosition <= 0f)
                UIManager.Instance?.Pop();
        }

        protected override GameObject GetDefaultFocus() =>
            backButton != null ? backButton.gameObject : null;
    }
}