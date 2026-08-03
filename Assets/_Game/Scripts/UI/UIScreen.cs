using UnityEngine;
using UnityEngine.EventSystems;

namespace Fields.UI
{
    /// <summary>
    /// Base class for every UI screen managed by UIManager.
    /// Each screen has exactly one Canvas. Screens are pooled (never destroyed):
    /// they start inactive and are shown/hidden by UIManager via Push/Pop.
    ///
    /// Interaction gating: a CanvasGroup is added automatically.
    /// When buried, interactable=false and blocksRaycasts=false so no mouse or
    /// keyboard navigation can reach the buried screen.
    /// </summary>
    [RequireComponent(typeof(Canvas))]
    public abstract class UIScreen : MonoBehaviour
    {
        Canvas       _canvas;
        CanvasGroup  _group;
        GameObject   _savedFocus;

        // ------------------------------------------------------------------ //
        // Unity lifecycle
        // ------------------------------------------------------------------ //

        protected virtual void Awake()
        {
            _canvas = GetComponent<Canvas>();

            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

            // All screens start hidden; UIManager shows them on Push.
            gameObject.SetActive(false);
            SetInteractable(false);
        }

        // ------------------------------------------------------------------ //
        // UIManager lifecycle — called only by UIManager
        // ------------------------------------------------------------------ //

        /// <summary>This screen is now the topmost screen.</summary>
        public void OnPush()
        {
            gameObject.SetActive(true);
            SetInteractable(true);
            OnScreenPushed();
            RestoreFocus();
        }

        /// <summary>Another screen was pushed on top; this screen stays visible but non-interactive.</summary>
        public void OnBury()
        {
            SaveFocus();
            SetInteractable(false);
            OnScreenBuried();
        }

        /// <summary>The screen above was popped; this screen is topmost again.</summary>
        public void OnResume()
        {
            SetInteractable(true);
            OnScreenResumed();
            RestoreFocus();
        }

        /// <summary>This screen is being removed from the stack.</summary>
        public void OnClose()
        {
            _savedFocus = null;
            OnScreenClosed();
            SetInteractable(false);
            gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ //
        // Subclass hooks
        // ------------------------------------------------------------------ //

        /// <summary>Called after the screen becomes active and interactable.</summary>
        protected virtual void OnScreenPushed()  { }

        /// <summary>Called after the screen becomes non-interactive (buried).</summary>
        protected virtual void OnScreenBuried()  { }

        /// <summary>Called after the screen becomes interactable again (resume).</summary>
        protected virtual void OnScreenResumed() { }

        /// <summary>Called just before the screen is hidden and deactivated.</summary>
        protected virtual void OnScreenClosed()  { }

        /// <summary>
        /// Return the default gamepad focus element for this screen.
        /// The returned object will be selected in the EventSystem on open,
        /// or when no previously saved focus exists.
        /// </summary>
        protected virtual GameObject GetDefaultFocus() => null;

        // ------------------------------------------------------------------ //
        // Internal helpers
        // ------------------------------------------------------------------ //

        void SetInteractable(bool on)
        {
            if (_group == null) return;
            _group.interactable    = on;
            _group.blocksRaycasts  = on;
        }

        void SaveFocus()
        {
            if (EventSystem.current != null)
                _savedFocus = EventSystem.current.currentSelectedGameObject;
        }

        void RestoreFocus()
        {
            if (EventSystem.current == null) return;
            var target = (_savedFocus != null && _savedFocus.activeInHierarchy)
                ? _savedFocus
                : GetDefaultFocus();
            EventSystem.current.SetSelectedGameObject(target);
        }
    }
}