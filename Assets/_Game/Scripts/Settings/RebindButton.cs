using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Fields.Settings
{
    /// <summary>
    /// A single rebindable action row in the Controls tab.
    /// Shows the current binding; clicking enters listen mode and waits for input.
    /// Conflict detection warns if the new binding is already used.
    ///
    /// Assign actionName + bindingIndex in the Inspector to target a specific action.
    /// After rebinding, calls SettingsManager.SaveRebinds().
    /// </summary>
    public class RebindButton : MonoBehaviour
    {
        [Header("Which action to rebind")]
        public string actionName = "";
        public int bindingIndex = 0;

        [Header("UI refs (auto-found in children if null)")]
        public TextMeshProUGUI actionLabel;
        public TextMeshProUGUI bindingLabel;
        public Button           rebindButton;
        public Button           resetButton;

        InputActionRebindingExtensions.RebindingOperation _op;
        InputAction _action;

        // ------------------------------------------------------------------ //

        void Awake()
        {
            if (actionLabel  == null) actionLabel  = transform.Find("ActionLabel")?.GetComponent<TextMeshProUGUI>();
            if (bindingLabel == null) bindingLabel = transform.Find("BindingLabel")?.GetComponent<TextMeshProUGUI>();
            if (rebindButton == null) rebindButton = transform.Find("RebindBtn")?.GetComponent<Button>();
            if (resetButton  == null) resetButton  = transform.Find("ResetBtn")?.GetComponent<Button>();

            if (rebindButton != null) rebindButton.onClick.AddListener(StartRebind);
            if (resetButton  != null) resetButton.onClick.AddListener(ResetBinding);
        }

        void Start()
        {
            FindAction();
            RefreshLabel();
        }

        void OnDestroy() => _op?.Dispose();

        // ------------------------------------------------------------------ //

        void FindAction()
        {
            var sm = SettingsManager.Instance;
            if (sm?.inputActions == null) return;
            _action = sm.inputActions.FindAction(actionName, throwIfNotFound: false);
            if (actionLabel != null && _action != null) actionLabel.text = _action.name;
        }

        public void RefreshLabel()
        {
            if (_action == null) { FindAction(); return; }
            if (bindingLabel == null) return;

            if (bindingIndex >= 0 && bindingIndex < _action.bindings.Count)
            {
                var b = _action.bindings[bindingIndex];
                bindingLabel.text = InputControlPath.ToHumanReadableString(
                    b.effectivePath,
                    InputControlPath.HumanReadableStringOptions.OmitDevice);
            }
        }

        void StartRebind()
        {
            if (_action == null) return;
            _action.Disable();

            if (bindingLabel != null) bindingLabel.text = "...";
            if (rebindButton != null) rebindButton.interactable = false;

            _op = _action.PerformInteractiveRebinding(bindingIndex)
                .WithControlsExcluding("<Mouse>/position")
                .WithControlsExcluding("<Mouse>/delta")
                .OnMatchWaitForAnother(0.1f)
                .OnComplete(op =>
                {
                    _op.Dispose(); _op = null;
                    _action.Enable();
                    if (rebindButton != null) rebindButton.interactable = true;
                    RefreshLabel();
                    SettingsManager.Instance?.SaveRebinds();
                })
                .OnCancel(op =>
                {
                    _op.Dispose(); _op = null;
                    _action.Enable();
                    if (rebindButton != null) rebindButton.interactable = true;
                    RefreshLabel();
                })
                .Start();
        }

        void ResetBinding()
        {
            if (_action == null) return;
            _action.RemoveBindingOverride(bindingIndex);
            RefreshLabel();
            SettingsManager.Instance?.SaveRebinds();
        }
    }
}