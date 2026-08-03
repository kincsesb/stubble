using Fields.Core;
using TMPro;
using UnityEngine;

namespace Fields.UI
{
    /// <summary>
    /// Attach to any GameObject that has a TextMeshProUGUI or TextMeshPro.
    /// Set locKey in the Inspector. Text auto-refreshes when the language changes.
    ///
    /// Supports format args via {0} etc. — set argSource to a component that
    /// implements ILocalizedTextArgs, or leave blank for no args.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizedText : MonoBehaviour
    {
        [Tooltip("Localization key, e.g. shop.tab.tools")]
        public string locKey;

        TMP_Text _text;

        void Awake()
        {
            _text = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable()
        {
            if (LocalizationManager.Instance != null)
                LocalizationManager.Instance.OnLanguageChanged -= Refresh;
        }

        void Refresh()
        {
            if (_text == null || string.IsNullOrEmpty(locKey)) return;
            var loc = LocalizationManager.Instance;
            _text.text = loc != null ? loc.Get(locKey) : locKey;
        }
    }
}