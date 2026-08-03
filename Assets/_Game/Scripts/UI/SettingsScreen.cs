using Fields.Settings;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Fields.UI
{
    /// <summary>
    /// Settings screen — five tabs: Display, Audio, Controls, Accessibility, Language.
    /// Single instance shared between the pause menu and (future) main menu.
    /// Every setting applies live on change (no Apply button), except
    /// resolution and window mode which use a 15-second revert dialog.
    ///
    /// All values read from / written to SettingsManager.Instance.Data immediately.
    /// </summary>
    public class SettingsScreen : UIScreen
    {
        [Header("Tab navigation")]
        public Button[]     tabButtons;    // [0]=Display [1]=Audio [2]=Controls [3]=Accessibility [4]=Language
        public GameObject[] tabPanels;     // parallel to tabButtons
        public Button       closeButton;

        // ── Display tab ───────────────────────────────────────────────────── //
        [Header("Display tab")]
        public Slider  fovSlider;
        public TextMeshProUGUI fovValueLabel;
        public Button[] windowModeButtons;   // [0]=Fullscreen [1]=Borderless [2]=Windowed
        public Button[] qualityButtons;      // [0]=Low [1]=Medium [2]=High
        public Toggle  vsyncToggle;
        public Button[] frameCapButtons;     // [0]=30 [1]=60 [2]=120 [3]=144 [4]=Unlimited
        // Resolution dropdown is built at runtime — expensive, done in OnScreenPushed

        // ── Audio tab ─────────────────────────────────────────────────────── //
        [Header("Audio tab")]
        public Slider masterSlider;
        public Slider toolsSlider;
        public Slider worldSlider;
        public Slider ambienceSlider;
        public Slider uiSlider;
        public TextMeshProUGUI masterLabel;
        public TextMeshProUGUI toolsLabel;
        public TextMeshProUGUI worldLabel;
        public TextMeshProUGUI ambienceLabel;
        public TextMeshProUGUI uiLabel;

        // ── Controls tab ─────────────────────────────────────────────────── //
        [Header("Controls tab")]
        public Slider  mouseSensXSlider;
        public Slider  mouseSensYSlider;
        public Toggle  invertYToggle;
        public Slider  gamepadSensSlider;
        public Slider  gamepadDeadzoneSlider;
        public Button  toolHoldButton;
        public Button  toolToggleButton;
        public Button  sprintHoldButton;
        public Button  sprintToggleButton;
        public Button  resetRebindsButton;
        // RebindButton rows are children of this panel, discovered at runtime.

        // ── Accessibility tab ─────────────────────────────────────────────── //
        [Header("Accessibility tab")]
        public Toggle headBobToggle;
        public Toggle cameraShakeToggle;
        public Slider cameraShakeSlider;
        public Slider swingKickSlider;
        public Toggle rumbleToggle;
        public Slider rumbleSlider;
        public Button[] uiScaleButtons;      // [0]=80% [1]=100% [2]=120% [3]=150%
        public Toggle showStaminaToggle;
        public Toggle showCarryToggle;
        public Toggle showMoneyToggle;
        public Toggle showCompletionToggle;
        public Toggle contextualHintsToggle;
        public Toggle highContrastToggle;

        // ── Language tab ─────────────────────────────────────────────────── //
        [Header("Language tab")]
        public Button[] languageButtons;     // same order as LocalizationManager.Language enum

        static readonly string[] LANG_CODES =
        {
            "en", "hu", "de", "ru", "zh-Hans", "pl", "es", "pt-BR", "jp"
        };

        // ── Revert dialog ─────────────────────────────────────────────────── //
        [Header("Resolution revert dialog")]
        public GameObject revertDialog;
        public TextMeshProUGUI revertCountdownText;

        float _revertTimer;
        bool  _revertActive;
        const float REVERT_SECONDS = 15f;

        // Don't re-register listeners on every push
        bool _listenersHooked;
        int  _activeTab;

        // ================================================================== //
        // UIScreen lifecycle
        // ================================================================== //

        protected override void Awake()
        {
            base.Awake();
            HookListeners();
        }

        void OnEnable()
        {
            if (Fields.Core.LocalizationManager.Instance != null)
                Fields.Core.LocalizationManager.Instance.OnLanguageChanged += RefreshAllValues;
        }

        void OnDisable()
        {
            if (Fields.Core.LocalizationManager.Instance != null)
                Fields.Core.LocalizationManager.Instance.OnLanguageChanged -= RefreshAllValues;
        }

        protected override void OnScreenPushed()
        {
            SelectTab(0);
            RefreshAllValues();
        }

        protected override void OnScreenResumed()
        {
            RefreshAllValues();
        }

        protected override void OnScreenClosed()
        {
            if (_revertActive) CancelRevert();
        }

        protected override GameObject GetDefaultFocus() =>
            tabButtons is { Length: > 0 } ? tabButtons[0].gameObject : null;

        void Update()
        {
            if (!_revertActive) return;
            _revertTimer -= Time.unscaledDeltaTime;
            if (revertCountdownText != null)
                revertCountdownText.text = $"Reverting in {Mathf.CeilToInt(_revertTimer)}s…";
            if (_revertTimer <= 0f) ExecuteRevert();
        }

        // ================================================================== //
        // Tab selection
        // ================================================================== //

        public void ClickTab(int index)
        {
            SelectTab(index);
        }

        void SelectTab(int index)
        {
            _activeTab = index;
            for (int i = 0; i < tabPanels.Length; i++)
                if (tabPanels[i] != null) tabPanels[i].SetActive(i == index);
            SetActiveButton(tabButtons, index);
        }

        // ================================================================== //
        // Refresh all controls from SettingsManager.Data
        // ================================================================== //

        void RefreshAllValues()
        {
            var d = SettingsManager.Instance?.Data;
            if (d == null) return;

            // Display
            if (fovSlider   != null) { fovSlider.SetValueWithoutNotify(d.fov); UpdateFOVLabel(d.fov); }
            SetActiveButton(windowModeButtons, d.windowMode);
            SetActiveButton(qualityButtons,    d.qualityPreset);
            if (vsyncToggle != null) vsyncToggle.SetIsOnWithoutNotify(d.vsync);
            SetActiveButton(frameCapButtons,   d.frameCapIndex);

            // Audio
            SetSliderLabel(masterSlider,   masterLabel,   d.masterVolume);
            SetSliderLabel(toolsSlider,    toolsLabel,    d.toolsVolume);
            SetSliderLabel(worldSlider,    worldLabel,    d.worldVolume);
            SetSliderLabel(ambienceSlider, ambienceLabel, d.ambienceVolume);
            SetSliderLabel(uiSlider,       uiLabel,       d.uiVolume);

            // Controls
            if (mouseSensXSlider     != null) mouseSensXSlider.SetValueWithoutNotify(d.mouseSensX);
            if (mouseSensYSlider     != null) mouseSensYSlider.SetValueWithoutNotify(d.mouseSensY);
            if (invertYToggle        != null) invertYToggle.SetIsOnWithoutNotify(d.invertY);
            if (gamepadSensSlider    != null) gamepadSensSlider.SetValueWithoutNotify(d.gamepadSens);
            if (gamepadDeadzoneSlider != null) gamepadDeadzoneSlider.SetValueWithoutNotify(d.gamepadDeadzone);
            UpdateToolUseButtons(d.toolUseHold);
            UpdateSprintButtons(d.sprintHold);

            // Accessibility
            if (headBobToggle      != null) headBobToggle.SetIsOnWithoutNotify(d.headBob);
            if (cameraShakeToggle  != null) cameraShakeToggle.SetIsOnWithoutNotify(d.cameraShake);
            if (cameraShakeSlider  != null) cameraShakeSlider.SetValueWithoutNotify(d.cameraShakeIntensity);
            if (swingKickSlider    != null) swingKickSlider.SetValueWithoutNotify(d.swingKickIntensity);
            if (rumbleToggle       != null) rumbleToggle.SetIsOnWithoutNotify(d.controllerRumble);
            if (rumbleSlider       != null) rumbleSlider.SetValueWithoutNotify(d.rumbleIntensity);
            SetActiveButton(uiScaleButtons, d.uiScale);
            if (showStaminaToggle     != null) showStaminaToggle.SetIsOnWithoutNotify(d.showStaminaBar);
            if (showCarryToggle       != null) showCarryToggle.SetIsOnWithoutNotify(d.showCarryIndicator);
            if (showMoneyToggle       != null) showMoneyToggle.SetIsOnWithoutNotify(d.showMoneyCounter);
            if (showCompletionToggle  != null) showCompletionToggle.SetIsOnWithoutNotify(d.showCompletionPct);
            if (contextualHintsToggle != null) contextualHintsToggle.SetIsOnWithoutNotify(d.contextualHints);
            if (highContrastToggle    != null) highContrastToggle.SetIsOnWithoutNotify(d.highContrastHUD);

            // Language
            int langIdx = System.Array.IndexOf(LANG_CODES, d.language);
            SetActiveButton(languageButtons, langIdx);
        }

        // ================================================================== //
        // Listener wiring — done once in Awake
        // ================================================================== //

        void HookListeners()
        {
            if (_listenersHooked) return;
            _listenersHooked = true;

            // Close
            if (closeButton != null)
                closeButton.onClick.AddListener(() => UIManager.Instance?.Pop());

            // Tabs
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null) continue;
                int idx = i;
                tabButtons[i].onClick.AddListener(() => SelectTab(idx));
            }

            // Display
            if (fovSlider != null)
            {
                fovSlider.onValueChanged.AddListener(v =>
                {
                    SettingsManager.Instance?.ApplyFOV(v);
                    UpdateFOVLabel(v);
                });
            }
            if (vsyncToggle != null)
                vsyncToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.ApplyVSync(v));

            WireIndexButtons(windowModeButtons, i => { SettingsManager.Instance?.ApplyWindowMode(i); SetActiveButton(windowModeButtons, i); });
            WireIndexButtons(qualityButtons,    i => { SettingsManager.Instance?.ApplyQuality(i);    SetActiveButton(qualityButtons,    i); });
            WireIndexButtons(frameCapButtons,   i => { SettingsManager.Instance?.ApplyFrameCap(i);   SetActiveButton(frameCapButtons,   i); });

            // Audio
            if (masterSlider   != null) masterSlider.onValueChanged.AddListener(v => { SettingsManager.Instance?.SetMasterVolume(v);   UpdatePctLabel(masterLabel,   v); });
            if (toolsSlider    != null) toolsSlider.onValueChanged.AddListener(v => { SettingsManager.Instance?.SetToolsVolume(v);    UpdatePctLabel(toolsLabel,    v); });
            if (worldSlider    != null) worldSlider.onValueChanged.AddListener(v => { SettingsManager.Instance?.SetWorldVolume(v);    UpdatePctLabel(worldLabel,    v); });
            if (ambienceSlider != null) ambienceSlider.onValueChanged.AddListener(v => { SettingsManager.Instance?.SetAmbienceVolume(v); UpdatePctLabel(ambienceLabel, v); });
            if (uiSlider       != null) uiSlider.onValueChanged.AddListener(v => { SettingsManager.Instance?.SetUIVolume(v);       UpdatePctLabel(uiLabel,       v); });

            // Controls
            if (mouseSensXSlider     != null) mouseSensXSlider.onValueChanged.AddListener(v => SettingsManager.Instance?.SetMouseSensX(v));
            if (mouseSensYSlider     != null) mouseSensYSlider.onValueChanged.AddListener(v => SettingsManager.Instance?.SetMouseSensY(v));
            if (invertYToggle        != null) invertYToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetInvertY(v));
            if (gamepadSensSlider    != null) gamepadSensSlider.onValueChanged.AddListener(v => SettingsManager.Instance?.SetGamepadSens(v));
            if (gamepadDeadzoneSlider != null) gamepadDeadzoneSlider.onValueChanged.AddListener(v => SettingsManager.Instance?.SetGamepadDeadzone(v));
            if (toolHoldButton       != null) toolHoldButton.onClick.AddListener(() => { SettingsManager.Instance?.SetToolUseHold(true);  UpdateToolUseButtons(true);  });
            if (toolToggleButton     != null) toolToggleButton.onClick.AddListener(() => { SettingsManager.Instance?.SetToolUseHold(false); UpdateToolUseButtons(false); });
            if (sprintHoldButton     != null) sprintHoldButton.onClick.AddListener(() => { SettingsManager.Instance?.SetSprintHold(true);  UpdateSprintButtons(true);  });
            if (sprintToggleButton   != null) sprintToggleButton.onClick.AddListener(() => { SettingsManager.Instance?.SetSprintHold(false); UpdateSprintButtons(false); });
            if (resetRebindsButton   != null) resetRebindsButton.onClick.AddListener(() => SettingsManager.Instance?.ResetRebinds());

            // Accessibility
            if (headBobToggle      != null) headBobToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetHeadBob(v));
            if (cameraShakeToggle  != null) cameraShakeToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetCameraShake(v));
            if (cameraShakeSlider  != null) cameraShakeSlider.onValueChanged.AddListener(v => SettingsManager.Instance?.SetCameraShakeIntensity(v));
            if (swingKickSlider    != null) swingKickSlider.onValueChanged.AddListener(v => SettingsManager.Instance?.SetSwingKick(v));
            if (rumbleToggle       != null) rumbleToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetRumble(v));
            if (rumbleSlider       != null) rumbleSlider.onValueChanged.AddListener(v => SettingsManager.Instance?.SetRumbleIntensity(v));
            WireIndexButtons(uiScaleButtons, i => { SettingsManager.Instance?.SetUIScale(i); SetActiveButton(uiScaleButtons, i); });
            if (showStaminaToggle     != null) showStaminaToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetHUDToggle("stamina",    v));
            if (showCarryToggle       != null) showCarryToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetHUDToggle("carry",      v));
            if (showMoneyToggle       != null) showMoneyToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetHUDToggle("money",      v));
            if (showCompletionToggle  != null) showCompletionToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetHUDToggle("completion", v));
            if (contextualHintsToggle != null) contextualHintsToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetHUDToggle("hints",      v));
            if (highContrastToggle    != null) highContrastToggle.onValueChanged.AddListener(v => SettingsManager.Instance?.SetHUDToggle("contrast",   v));

            // Language
            for (int i = 0; i < languageButtons.Length && i < LANG_CODES.Length; i++)
            {
                int idx = i;
                if (languageButtons[i] != null)
                    languageButtons[i].onClick.AddListener(() =>
                    {
                        SettingsManager.Instance?.SetLanguage(LANG_CODES[idx]);
                        SetActiveButton(languageButtons, idx);
                    });
            }
        }

        // ================================================================== //
        // Resolution revert
        // ================================================================== //

        public void BeginRevertCountdown()
        {
            _revertActive = true;
            _revertTimer  = REVERT_SECONDS;
            if (revertDialog != null) revertDialog.SetActive(true);
        }

        public void ConfirmResolution()
        {
            _revertActive = false;
            if (revertDialog != null) revertDialog.SetActive(false);
        }

        void ExecuteRevert()
        {
            _revertActive = false;
            if (revertDialog != null) revertDialog.SetActive(false);
            SettingsManager.Instance?.RevertResolution();
        }

        void CancelRevert() => ExecuteRevert();

        // ================================================================== //
        // Helpers
        // ================================================================== //

        static void WireIndexButtons(Button[] buttons, System.Action<int> callback)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                int idx = i;
                buttons[i].onClick.AddListener(() => callback(idx));
            }
        }

        static void SetActiveButton(Button[] buttons, int activeIndex)
        {
            if (buttons == null) return;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                // Tint the selected button green, others grey
                var img = buttons[i].GetComponent<UnityEngine.UI.Image>();
                if (img != null)
                    img.color = i == activeIndex
                        ? new Color(0.22f, 0.55f, 0.22f)
                        : new Color(0.22f, 0.22f, 0.22f);
            }
        }

        static void SetSliderLabel(Slider slider, TextMeshProUGUI label, float value)
        {
            if (slider != null) slider.SetValueWithoutNotify(value);
            UpdatePctLabel(label, value);
        }

        static void UpdatePctLabel(TextMeshProUGUI label, float value)
        {
            if (label != null) label.text = $"{Mathf.RoundToInt(value * 100)}%";
        }

        void UpdateFOVLabel(float v)
        {
            if (fovValueLabel != null) fovValueLabel.text = $"{Mathf.RoundToInt(v)}°";
        }

        void UpdateToolUseButtons(bool hold)
        {
            SetButtonHighlight(toolHoldButton,   hold);
            SetButtonHighlight(toolToggleButton, !hold);
        }

        void UpdateSprintButtons(bool hold)
        {
            SetButtonHighlight(sprintHoldButton,   hold);
            SetButtonHighlight(sprintToggleButton, !hold);
        }

        static void SetButtonHighlight(Button btn, bool active)
        {
            if (btn == null) return;
            var img = btn.GetComponent<UnityEngine.UI.Image>();
            if (img != null) img.color = active ? new Color(0.22f, 0.55f, 0.22f) : new Color(0.22f, 0.22f, 0.22f);
        }
    }
}