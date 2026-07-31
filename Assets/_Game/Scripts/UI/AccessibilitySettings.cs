using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Fields.UI
{
    /// <summary>
    /// Accessibility settings: FOV slider, motion blur toggle, colorblind mode.
    /// Persists via PlayerPrefs. Apply on Awake so settings survive scene loads.
    /// </summary>
    public class AccessibilitySettings : MonoBehaviour
    {
        public static AccessibilitySettings Instance { get; private set; }

        [Header("References")]
        public Volume postProcessVolume;
        public Fields.Core.PlayerController player;

        // Defaults
        const float DEFAULT_FOV   = 60f;
        const bool  DEFAULT_BLUR  = false;
        const int   DEFAULT_CB    = 0;

        // Runtime values
        float _fov;
        bool  _motionBlurOn;
        int   _colorblindMode; // 0=none, 1=deuteranopia, 2=protanopia, 3=tritanopia

        MotionBlur _blurComp;

        static readonly string[] COLORBLIND_SHADER_KEYWORD = {
            "", "COLORBLIND_DEUTERANOPIA", "COLORBLIND_PROTANOPIA", "COLORBLIND_TRITANOPIA"
        };

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            _fov            = PlayerPrefs.GetFloat("a11y_fov",  DEFAULT_FOV);
            _motionBlurOn   = PlayerPrefs.GetInt("a11y_blur",   DEFAULT_BLUR ? 1 : 0) == 1;
            _colorblindMode = PlayerPrefs.GetInt("a11y_cb",     DEFAULT_CB);

            if (postProcessVolume != null)
                postProcessVolume.profile.TryGet(out _blurComp);

            ApplyAll();
        }

        // ------------------------------------------------------------------ //

        public float FOV
        {
            get => _fov;
            set
            {
                _fov = Mathf.Clamp(value, 50f, 110f);
                PlayerPrefs.SetFloat("a11y_fov", _fov);
                ApplyFOV();
            }
        }

        public bool MotionBlurEnabled
        {
            get => _motionBlurOn;
            set
            {
                _motionBlurOn = value;
                PlayerPrefs.SetInt("a11y_blur", value ? 1 : 0);
                ApplyBlur();
            }
        }

        public int ColorblindMode
        {
            get => _colorblindMode;
            set
            {
                _colorblindMode = Mathf.Clamp(value, 0, 3);
                PlayerPrefs.SetInt("a11y_cb", _colorblindMode);
                ApplyColorblind();
            }
        }

        // ------------------------------------------------------------------ //

        void ApplyAll()
        {
            ApplyFOV();
            ApplyBlur();
            ApplyColorblind();
        }

        void ApplyFOV()
        {
            if (player != null)
            {
                player.baseFOV   = _fov;
                player.sprintFOV = _fov + 5f;
            }
            var cam = Camera.main;
            if (cam != null) cam.fieldOfView = _fov;
        }

        void ApplyBlur()
        {
            if (_blurComp != null) _blurComp.active = _motionBlurOn;
        }

        void ApplyColorblind()
        {
            // Global shader keyword approach — simple but effective for URP
            for (int i = 1; i < COLORBLIND_SHADER_KEYWORD.Length; i++)
                Shader.DisableKeyword(COLORBLIND_SHADER_KEYWORD[i]);

            if (_colorblindMode > 0)
                Shader.EnableKeyword(COLORBLIND_SHADER_KEYWORD[_colorblindMode]);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        bool _showUI;

        void Update() { if (Input.GetKeyDown(KeyCode.F10)) _showUI = !_showUI; }

        void OnGUI()
        {
            if (!_showUI) return;
            GUILayout.BeginArea(new Rect(Screen.width - 280, 10, 270, 220), GUI.skin.box);
            GUILayout.Label("Accessibility Settings (F10)");

            GUILayout.Label($"FOV: {_fov:F0}");
            float newFov = GUILayout.HorizontalSlider(_fov, 50f, 110f);
            if (!Mathf.Approximately(newFov, _fov)) FOV = newFov;

            bool newBlur = GUILayout.Toggle(_motionBlurOn, "Motion Blur");
            if (newBlur != _motionBlurOn) MotionBlurEnabled = newBlur;

            GUILayout.Label("Colorblind Mode:");
            string[] labels = { "None", "Deuteranopia", "Protanopia", "Tritanopia" };
            for (int i = 0; i < labels.Length; i++)
            {
                if (GUILayout.Button((_colorblindMode == i ? "● " : "  ") + labels[i]))
                    ColorblindMode = i;
            }

            GUILayout.EndArea();
        }
#endif
    }
}
