using System;

namespace Fields.Settings
{
    // ======================================================================= //
    // Serialized settings blob — lives in settings.json, NOT in the save file.
    // Survives deleting the save. Survives starting a New Game.
    // All fields must have sensible defaults (applied on first run).
    // ======================================================================= //

    [Serializable]
    public class SettingsData
    {
        // ── Display ────────────────────────────────────────────────────────── //
        public int   resolutionIndex   = -1;    // -1 = current screen resolution
        public int   windowMode        = 0;     // 0=Fullscreen 1=Borderless 2=Windowed
        public int   qualityPreset     = 1;     // 0=Low 1=Medium 2=High
        public float fov               = 70f;   // 60-100
        public bool  vsync             = true;
        public int   frameCapIndex     = 1;     // 0=30 1=60 2=120 3=144 4=Unlimited
        public float brightness        = 1f;    // 0-2 (gamma ramp)

        // ── Audio (linear 0-1; converted to dB via log10*20 when applying) ─ //
        public float masterVolume      = 1f;
        public float toolsVolume       = 1f;
        public float worldVolume       = 1f;
        public float ambienceVolume    = 1f;
        public float uiVolume          = 1f;

        // ── Controls ───────────────────────────────────────────────────────── //
        public float mouseSensX        = 0.15f;
        public float mouseSensY        = 0.15f;
        public bool  invertY           = false;
        public float gamepadSens       = 180f;
        public float gamepadDeadzone   = 0.1f;
        public bool  toolUseHold       = true;  // false = toggle
        public bool  sprintHold        = true;  // false = toggle
        /// <summary>JSON produced by InputActionAsset.SaveBindingOverridesAsJson()</summary>
        public string inputBindingsJson = "";

        // ── Accessibility ─────────────────────────────────────────────────── //
        public bool  headBob               = true;
        public bool  cameraShake           = true;
        public float cameraShakeIntensity  = 1f;   // 0-1
        public float swingKickIntensity    = 1f;   // 0-1
        public bool  controllerRumble      = true;
        public float rumbleIntensity       = 1f;   // 0-1
        public int   uiScale               = 1;    // 0=80% 1=100% 2=120% 3=150%
        public bool  showStaminaBar        = true;
        public bool  showCarryIndicator    = true;
        public bool  showMoneyCounter      = true;
        public bool  showCompletionPct     = true;
        public bool  showTeammateNameplates = true;
        public bool  contextualHints       = true;
        public bool  highContrastHUD       = false;

        // ── Language ──────────────────────────────────────────────────────── //
        public string language = "en";
    }
}