using Fields.Hay;
using UnityEngine;

namespace Fields.UI
{
    /// <summary>
    /// Runtime tuning panel for RoundBaleConfig. Toggle with F8.
    /// Writes back to the ScriptableObject — values persist for the session.
    /// Spec §10: all listed values must be adjustable while standing on a slope in play mode.
    /// </summary>
    public class BaleDebugPanel : MonoBehaviour
    {
        public RoundBaleConfig config;
        public KeyCode         toggleKey = KeyCode.F8;

        bool    _visible;
        Rect    _windowRect = new Rect(10f, 10f, 330f, 740f);
        Vector2 _scroll;

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current?.f8Key.wasPressedThisFrame == true) _visible = !_visible;
#else
            if (Input.GetKeyDown(toggleKey)) _visible = !_visible;
#endif
        }

        void OnGUI()
        {
            if (!_visible || config == null) return;
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect, DrawWindow, $"Round Bale Tuning ({toggleKey})");
        }

        void DrawWindow(int id)
        {
            _scroll = GUILayout.BeginScrollView(_scroll);

            Section("Push / Speed");
            config.pushAcceleration  = Slider("Push Accel (m/s²)",  config.pushAcceleration,  0.5f, 8f);
            config.speedCap          = Slider("Speed Cap (m/s)",     config.speedCap,           2f,  20f);
            config.quadDampThreshold = Slider("Quad Damp Above",     config.quadDampThreshold,  2f,  12f);
            config.quadDampCoeff     = Slider("Quad Damp Coeff",     config.quadDampCoeff,      0f,   8f);
            config.mass              = Slider("Mass (kg)",           config.mass,              50f, 500f);

            Section("Rolling Resistance (m/s²)");
            config.resistanceUncutGrass = Slider("Uncut Grass", config.resistanceUncutGrass, 0.1f, 5f);
            config.resistanceCutStubble = Slider("Cut Stubble", config.resistanceCutStubble, 0.1f, 5f);
            config.resistanceDirt       = Slider("Dirt",        config.resistanceDirt,        0.1f, 5f);
            config.resistanceGravel     = Slider("Gravel",      config.resistanceGravel,      0.1f, 5f);

            Section("Steering");
            config.maxSteerRate        = Slider("Max Steer Rate (°/s)", config.maxSteerRate,       10f, 180f);
            config.steerSpeedThreshold = Slider("Steer Threshold (m/s)", config.steerSpeedThreshold, 0.5f, 8f);

            Section("Ground Alignment");
            config.groundAlignRate = Slider("Align Rate", config.groundAlignRate, 1f, 30f);

            Section("Collisions");
            config.headOnSpeedLoss         = Slider("Head-On Speed Loss",    config.headOnSpeedLoss,         0f, 1f);
            config.baleToBaleTransfer      = Slider("Bale-Bale Transfer",    config.baleToBaleTransfer,      0f, 1f);
            config.postCollisionExemptTime = Slider("Post-Coll Exempt (s)", config.postCollisionExemptTime, 0f, 0.5f);

            Section("Feel");
            config.cameraShakeScalePerSpeed = Slider("Shake / Speed", config.cameraShakeScalePerSpeed, 0f, 0.02f);
            config.cameraShakeProximity     = Slider("Shake Proximity (m)", config.cameraShakeProximity, 0.5f, 10f);
            config.hapticRumbleScale        = Slider("Haptic Scale",   config.hapticRumbleScale,     0f, 2f);

            GUILayout.EndScrollView();
            GUI.DragWindow();
        }

        float Slider(string label, float current, float min, float max)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"{label}: {current:F2}", GUILayout.Width(210));
            float val = GUILayout.HorizontalSlider(current, min, max);
            GUILayout.EndHorizontal();
            return val;
        }

        void Section(string title)
        {
            GUILayout.Space(4);
            GUILayout.Label($"── {title} ──", GUI.skin.box);
        }
    }
}