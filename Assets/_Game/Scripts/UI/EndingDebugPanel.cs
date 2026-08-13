using Fields.Core;
using Fields.Grass;
using UnityEngine;

namespace Fields.UI
{
    /// <summary>
    /// Debug panel for testing the ending system. Toggle with F9 or click [DBG] in top-left.
    /// </summary>
    public class EndingDebugPanel : MonoBehaviour
    {
        bool   _visible;
        float  _timerSeconds = 5400f;
        Rect   _windowRect   = new Rect(10f, 36f, 330f, 240f);
        string _statusMsg    = "";

        void Update()
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current?.f9Key.wasPressedThisFrame == true)
                _visible = !_visible;
#else
            if (Input.GetKeyDown(KeyCode.F9)) _visible = !_visible;
#endif
        }

        void OnGUI()
        {
            // Always-visible toggle button — no Game View focus needed
            if (GUI.Button(new Rect(4, 4, 72, 22), _visible ? "[DBG x]" : "[DBG]"))
                _visible = !_visible;

            if (!_visible) return;
            _windowRect = GUILayout.Window(
                GUIUtility.GetControlID(FocusType.Passive),
                _windowRect, DrawWindow, "Ending Debug (F9)");
        }

        void DrawWindow(int id)
        {
            // ── Grass ────────────────────────────────────────────────────── //
            GUILayout.Label("── Grass ──", GUI.skin.box);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Cut All ~99.5%", GUILayout.Height(28)))
                CutAllGrass();
            GUILayout.Label(GetCompletionSummary(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ── Timer ────────────────────────────────────────────────────── //
            GUILayout.Label("── Timer (Session) ──", GUI.skin.box);

            var ss = SessionState.Instance;
            float sessionCurrent = ss != null ? ss.TotalPlaytime : 0f;
            GUILayout.Label($"Session: {FormatTime(sessionCurrent)}   |   Set to: {FormatTime(_timerSeconds)}");

            float newTimer = GUILayout.HorizontalSlider(_timerSeconds, 0f, 14400f);
            if (Mathf.Abs(newTimer - _timerSeconds) > 1f)
            {
                _timerSeconds = newTimer;
                ApplyTimer();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("1h 30m\n(Loop)"))   { _timerSeconds = 5400f;  ApplyTimer(); }
            if (GUILayout.Button("2h 59m\n(Loop)"))   { _timerSeconds = 10799f; ApplyTimer(); }
            if (GUILayout.Button("3h 01m\n(Nuke)"))   { _timerSeconds = 10860f; ApplyTimer(); }
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            // ── Fire ─────────────────────────────────────────────────────── //
            GUILayout.Label("── Ending ──", GUI.skin.box);
            if (GUILayout.Button("FIRE ENDING", GUILayout.Height(28)))
                FireEnding();

            if (_statusMsg != "")
                GUILayout.Label(_statusMsg);

            GUI.DragWindow();
        }

        // ── Helpers ──────────────────────────────────────────────────────── //

        void CutAllGrass()
        {
            var fields = FindObjectsByType<GrassField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (fields.Length == 0) { _statusMsg = "No GrassFields found!"; return; }

            foreach (var gf in fields)
            {
                float w = gf.fieldSize.x;
                float h = gf.fieldSize.y;
                Vector3 origin = gf.transform.position;

                // Single CutCapsule covers ~99.5% in X, full height in Z → 1 GPU draw call
                float pad = gf.config != null ? gf.config.gridCellSize : 0.5f;
                Vector3 from   = origin + new Vector3(0f,        0f, h * 0.5f);
                Vector3 to     = origin + new Vector3(w * 0.995f, 0f, h * 0.5f);
                float   radius = h * 0.5f + pad;

                gf.CutCapsule(from, to, radius);
            }

            _statusMsg = $"Cut {fields.Length} field(s). ~{GetCompletionSummary()} done.";
        }

        void ApplyTimer()
        {
            var ss = SessionState.Instance;
            if (ss == null) { _statusMsg = "SessionState null — start game first!"; return; }
            ss.LoadTotalPlaytime(_timerSeconds);
            _statusMsg = $"Timer -> {FormatTime(_timerSeconds)}";
        }

        void FireEnding()
        {
            var ss = SessionState.Instance;
            float t = ss != null ? ss.TotalPlaytime : _timerSeconds;
            GameEvents.FireFullGameCompleted(t);
            _statusMsg = $"Fired! t={FormatTime(t)}";
        }

        string GetCompletionSummary()
        {
            var fields = FindObjectsByType<GrassField>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (fields.Length == 0) return "no fields";
            float sum = 0f;
            foreach (var gf in fields) sum += gf.GetCompletionPercent();
            return $"{sum / fields.Length:F1}%";
        }

        static string FormatTime(float s)
        {
            int h = Mathf.FloorToInt(s / 3600f);
            int m = Mathf.FloorToInt(s % 3600f / 60f);
            int sec = Mathf.FloorToInt(s % 60f);
            return $"{h:00}:{m:00}:{sec:00}";
        }
    }
}
