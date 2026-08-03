using System;
using System.IO;
using Fields.Core;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.InputSystem;

namespace Fields.Settings
{
    /// <summary>
    /// Single source of truth for all user settings.
    ///
    /// File: Application.persistentDataPath/settings.json
    /// Separate from save_slot0.dat — survives New Game and save deletion.
    ///
    /// Usage:
    ///   SettingsManager.Instance.Set(ref data.masterVolume, value);
    ///   ...SettingsManager.OnSettingsChanged subscribes for refresh.
    ///
    /// Resolution / window mode changes show a 15-second revert dialog — handled
    /// by SettingsScreen; this class applies immediately and reverts on request.
    /// </summary>
    public class SettingsManager : MonoBehaviour
    {
        public static SettingsManager Instance { get; private set; }

        const string SETTINGS_FILE = "settings.json";

        // ── Audio mixer ───────────────────────────────────────────────────── //
        [Header("Audio")]
        public AudioMixer masterMixer;

        const string PARAM_MASTER   = "MasterVolume";
        const string PARAM_TOOLS    = "ToolsVolume";
        const string PARAM_WORLD    = "WorldVolume";
        const string PARAM_AMBIENCE = "AmbienceVolume";
        const string PARAM_UI       = "UIVolume";

        // ── Input action asset (for rebind serialization) ─────────────────── //
        [Header("Input")]
        public InputActionAsset inputActions;

        // ── Settings data ─────────────────────────────────────────────────── //
        public SettingsData Data { get; private set; } = new SettingsData();

        /// <summary>Fired whenever any setting changes. Subscribe to refresh UI.</summary>
        public static event Action OnSettingsChanged;

        // Auto-save debounce
        float _dirtyTimer;
        bool  _dirty;
        const float SAVE_DEBOUNCE = 1f;

        // Resolution revert support
        Resolution _prevResolution;
        FullScreenMode _prevWindowMode;

        // ================================================================== //
        // Lifecycle
        // ================================================================== //

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSettings();
        }

        void Start()
        {
            ApplyAll();
        }

        void Update()
        {
            if (!_dirty) return;
            _dirtyTimer += Time.unscaledDeltaTime;
            if (_dirtyTimer >= SAVE_DEBOUNCE)
            {
                _dirty      = false;
                _dirtyTimer = 0f;
                SaveSettings();
            }
        }

        void OnApplicationQuit() => SaveSettings();

        // ================================================================== //
        // Load / Save
        // ================================================================== //

        public void LoadSettings()
        {
            string path = SettingsPath();
            if (!File.Exists(path))
            {
                MigrateFromPlayerPrefs();
                return;
            }
            try
            {
                string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                Data = JsonUtility.FromJson<SettingsData>(json) ?? new SettingsData();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[SettingsManager] Failed to load settings: {ex.Message}. Using defaults.");
                Data = new SettingsData();
            }
        }

        public void SaveSettings()
        {
            try
            {
                string json = JsonUtility.ToJson(Data, prettyPrint: true);
                File.WriteAllText(SettingsPath(), json, System.Text.Encoding.UTF8);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SettingsManager] Failed to save settings: {ex.Message}");
            }
        }

        // ================================================================== //
        // Apply all settings
        // ================================================================== //

        public void ApplyAll()
        {
            ApplyDisplay();
            ApplyAudio();
            ApplyControls();
            ApplyAccessibility();
            ApplyLanguage();
        }

        // ── Display ───────────────────────────────────────────────────────── //

        public void ApplyDisplay()
        {
            ApplyFOV(Data.fov);
            ApplyWindowMode(Data.windowMode);
            ApplyVSync(Data.vsync);
            ApplyFrameCap(Data.frameCapIndex);
            ApplyQuality(Data.qualityPreset);
            // Resolution applied separately (has revert dialog)
            if (Data.resolutionIndex >= 0) ApplyResolution(Data.resolutionIndex);
        }

        public void ApplyFOV(float fov)
        {
            Data.fov = Mathf.Clamp(fov, 60f, 100f);
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                pc.baseFOV   = Data.fov;
                pc.sprintFOV = Data.fov + 5f;
            }
            var cam = Camera.main;
            if (cam != null) cam.fieldOfView = Data.fov;
        }

        public void ApplyResolution(int index)
        {
            var resolutions = Screen.resolutions;
            if (index < 0 || index >= resolutions.Length) return;
            Data.resolutionIndex = index;
            var r = resolutions[index];
            Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRateRatio);
            MarkDirty();
        }

        public void ApplyWindowMode(int mode)
        {
            Data.windowMode = Mathf.Clamp(mode, 0, 2);
            Screen.fullScreenMode = mode switch
            {
                0 => FullScreenMode.ExclusiveFullScreen,
                1 => FullScreenMode.FullScreenWindow,
                _ => FullScreenMode.Windowed,
            };
            MarkDirty();
        }

        public void ApplyVSync(bool on)
        {
            Data.vsync = on;
            QualitySettings.vSyncCount = on ? 1 : 0;
            MarkDirty();
        }

        public void ApplyFrameCap(int index)
        {
            Data.frameCapIndex = Mathf.Clamp(index, 0, 4);
            int[] caps = { 30, 60, 120, 144, -1 };
            Application.targetFrameRate = caps[Data.frameCapIndex];
            MarkDirty();
        }

        public void ApplyQuality(int preset)
        {
            Data.qualityPreset = Mathf.Clamp(preset, 0, 2);
            // Quality presets must be named "Low", "Medium", "High" in Project Settings.
            QualitySettings.SetQualityLevel(Data.qualityPreset, applyExpensiveChanges: true);
            MarkDirty();
        }

        // ── Audio ─────────────────────────────────────────────────────────── //

        public void ApplyAudio()
        {
            SetMixerVolume(PARAM_MASTER,   Data.masterVolume);
            SetMixerVolume(PARAM_TOOLS,    Data.toolsVolume);
            SetMixerVolume(PARAM_WORLD,    Data.worldVolume);
            SetMixerVolume(PARAM_AMBIENCE, Data.ambienceVolume);
            SetMixerVolume(PARAM_UI,       Data.uiVolume);
        }

        public void SetMasterVolume(float v)   { Data.masterVolume   = Saturate(v); SetMixerVolume(PARAM_MASTER,   v); MarkDirty(); }
        public void SetToolsVolume(float v)    { Data.toolsVolume    = Saturate(v); SetMixerVolume(PARAM_TOOLS,    v); MarkDirty(); }
        public void SetWorldVolume(float v)    { Data.worldVolume    = Saturate(v); SetMixerVolume(PARAM_WORLD,    v); MarkDirty(); }
        public void SetAmbienceVolume(float v) { Data.ambienceVolume = Saturate(v); SetMixerVolume(PARAM_AMBIENCE, v); MarkDirty(); }
        public void SetUIVolume(float v)       { Data.uiVolume       = Saturate(v); SetMixerVolume(PARAM_UI,       v); MarkDirty(); }

        void SetMixerVolume(string param, float linear)
        {
            if (masterMixer == null) return;
            float db = linear > 0f ? Mathf.Log10(Mathf.Max(linear, 0.0001f)) * 20f : -80f;
            masterMixer.SetFloat(param, db);
        }

        // ── Controls ─────────────────────────────────────────────────────── //

        public void ApplyControls()
        {
            var pc = PlayerController.Instance;
            if (pc != null)
            {
                pc.mouseSensitivity   = Data.mouseSensX;
                pc.gamepadSensitivity = Data.gamepadSens;
            }
            RestoreRebinds();
        }

        public void SetMouseSensX(float v)        { Data.mouseSensX      = Mathf.Max(v, 0.01f); ApplyControls(); MarkDirty(); }
        public void SetMouseSensY(float v)        { Data.mouseSensY      = Mathf.Max(v, 0.01f); ApplyControls(); MarkDirty(); }
        public void SetInvertY(bool v)            { Data.invertY         = v;                    ApplyControls(); MarkDirty(); }
        public void SetGamepadSens(float v)       { Data.gamepadSens     = Mathf.Max(v, 1f);    ApplyControls(); MarkDirty(); }
        public void SetGamepadDeadzone(float v)   { Data.gamepadDeadzone = Saturate(v);         MarkDirty(); }
        public void SetToolUseHold(bool hold)     { Data.toolUseHold     = hold;                MarkDirty(); }
        public void SetSprintHold(bool hold)      { Data.sprintHold      = hold;                MarkDirty(); }

        /// <summary>Call after completing a rebind to persist it.</summary>
        public void SaveRebinds()
        {
            if (inputActions == null) return;
            Data.inputBindingsJson = inputActions.SaveBindingOverridesAsJson();
            MarkDirty();
        }

        void RestoreRebinds()
        {
            if (inputActions == null || string.IsNullOrEmpty(Data.inputBindingsJson)) return;
            try { inputActions.LoadBindingOverridesFromJson(Data.inputBindingsJson); }
            catch (Exception ex) { Debug.LogWarning($"[SettingsManager] Rebind restore failed: {ex.Message}"); }
        }

        public void ResetRebinds()
        {
            if (inputActions == null) return;
            inputActions.RemoveAllBindingOverrides();
            Data.inputBindingsJson = "";
            MarkDirty();
        }

        // ── Accessibility ─────────────────────────────────────────────────── //

        public void ApplyAccessibility()
        {
            // Head bob and camera shake systems are stubbed — applied when those
            // subsystems are built. UI scale and HUD toggles applied below.
            ApplyUIScale(Data.uiScale);
            ApplyHUDToggles();
        }

        public void SetHeadBob(bool v)             { Data.headBob             = v;           MarkDirty(); NotifyChanged(); }
        public void SetCameraShake(bool v)         { Data.cameraShake         = v;           MarkDirty(); NotifyChanged(); }
        public void SetCameraShakeIntensity(float v){ Data.cameraShakeIntensity = Saturate(v); MarkDirty(); NotifyChanged(); }
        public void SetSwingKick(float v)          { Data.swingKickIntensity  = Saturate(v); MarkDirty(); NotifyChanged(); }
        public void SetRumble(bool v)              { Data.controllerRumble    = v;           MarkDirty(); NotifyChanged(); }
        public void SetRumbleIntensity(float v)    { Data.rumbleIntensity     = Saturate(v); MarkDirty(); NotifyChanged(); }

        public void SetUIScale(int index)
        {
            Data.uiScale = Mathf.Clamp(index, 0, 3);
            ApplyUIScale(Data.uiScale);
            MarkDirty(); NotifyChanged();
        }

        void ApplyUIScale(int index)
        {
            // UI canvas scalers read SettingsManager.Instance.Data.uiScale each time
            // they resize — they poll this value on Start/screen resize.
            // (UIScaleApplier component, future hook.)
        }

        public void SetHUDToggle(string key, bool value)
        {
            switch (key)
            {
                case "stamina":    Data.showStaminaBar          = value; break;
                case "carry":      Data.showCarryIndicator      = value; break;
                case "money":      Data.showMoneyCounter        = value; break;
                case "completion": Data.showCompletionPct       = value; break;
                case "nameplates": Data.showTeammateNameplates  = value; break;
                case "hints":      Data.contextualHints         = value; break;
                case "contrast":   Data.highContrastHUD         = value; break;
            }
            ApplyHUDToggles();
            MarkDirty();
        }

        void ApplyHUDToggles()
        {
            var hud = UI.HUDController.Instance;
            if (hud == null) return;
            if (hud.staminaBar != null)  hud.staminaBar.gameObject.SetActive(Data.showStaminaBar);
            if (hud.moneyText  != null)  hud.moneyText.gameObject.SetActive(Data.showMoneyCounter);
            if (hud.completionText != null) hud.completionText.gameObject.SetActive(Data.showCompletionPct);
            if (hud.baleCountText  != null) hud.baleCountText.gameObject.SetActive(Data.showCarryIndicator);
        }

        // ── Language ─────────────────────────────────────────────────────── //

        public void ApplyLanguage()
        {
            var loc = LocalizationManager.Instance;
            if (loc == null) return;
            var lang = LanguageCodeToEnum(Data.language);
            loc.SetLanguage(lang);
        }

        public void SetLanguage(string code)
        {
            Data.language = code;
            ApplyLanguage();
            MarkDirty();
        }

        // ── Resolution revert ─────────────────────────────────────────────── //

        public void SnapshotCurrentResolution()
        {
            _prevResolution = Screen.currentResolution;
            _prevWindowMode = Screen.fullScreenMode;
        }

        public void RevertResolution()
        {
            Screen.SetResolution(_prevResolution.width, _prevResolution.height,
                _prevWindowMode, _prevResolution.refreshRateRatio);
        }

        // ================================================================== //
        // Migration from PlayerPrefs (first run)
        // ================================================================== //

        void MigrateFromPlayerPrefs()
        {
            // AccessibilitySettings used PlayerPrefs keys a11y_fov / a11y_blur
            Data.fov      = PlayerPrefs.GetFloat("a11y_fov", 70f);
            Data.language = PlayerPrefs.GetString("loc_lang", "en");
            Data = Data ?? new SettingsData();
            SaveSettings();
        }

        // ================================================================== //
        // Helpers
        // ================================================================== //

        void MarkDirty()
        {
            _dirty = true;
            _dirtyTimer = 0f;
        }

        void NotifyChanged() => OnSettingsChanged?.Invoke();

        static float Saturate(float v) => Mathf.Clamp01(v);

        static string SettingsPath() =>
            Path.Combine(Application.persistentDataPath, SETTINGS_FILE);

        static LocalizationManager.Language LanguageCodeToEnum(string code) => code switch
        {
            "hu"    => LocalizationManager.Language.Hungarian,
            "de"    => LocalizationManager.Language.German,
            "ru"    => LocalizationManager.Language.Russian,
            "zh-Hans" => LocalizationManager.Language.ChineseSimplified,
            "pl"    => LocalizationManager.Language.Polish,
            "es"    => LocalizationManager.Language.Spanish,
            "pt-BR" => LocalizationManager.Language.PortugueseBrazil,
            "jp"    => LocalizationManager.Language.Japanese,
            _       => LocalizationManager.Language.English,
        };
    }
}