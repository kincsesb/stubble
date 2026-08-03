using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Generic two-button confirmation modal pushed onto the UIManager stack.
    ///
    /// Usage:
    ///   confirmModal.Show("Quit", "Are you sure?", () => Application.Quit());
    ///
    /// Default focus is Cancel (safer action). The modal pops itself after either button.
    /// </summary>
    public class ConfirmModal : UIScreen
    {
        [Header("UI refs")]
        public TextMeshProUGUI headerText;
        public TextMeshProUGUI bodyText;
        public Button          confirmButton;
        public Button          cancelButton;

        Action _onConfirm;
        Action _onCancel;

        protected override void Awake()
        {
            base.Awake();
            if (confirmButton) confirmButton.onClick.AddListener(ClickConfirm);
            if (cancelButton)  cancelButton.onClick.AddListener(ClickCancel);
        }

        // ------------------------------------------------------------------ //

        public void Show(string header, string body, Action onConfirm, Action onCancel = null)
        {
            if (headerText) headerText.text = header;
            if (bodyText)   bodyText.text   = body;
            _onConfirm = onConfirm;
            _onCancel  = onCancel;
            UIManager.Instance?.Push(this);
        }

        // ------------------------------------------------------------------ //

        void ClickConfirm()
        {
            UIManager.Instance?.Pop();
            _onConfirm?.Invoke();
        }

        void ClickCancel()
        {
            UIManager.Instance?.Pop();
            _onCancel?.Invoke();
        }

        // Cancel is the safe default so the player can't accidentally confirm.
        protected override GameObject GetDefaultFocus() =>
            cancelButton != null ? cancelButton.gameObject : null;
    }
}