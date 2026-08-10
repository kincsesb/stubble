using TMPro;
using UnityEditor;
using UnityEngine;

namespace Fields.Editor
{
    [InitializeOnLoad]
    public static class TmpCjkFontSetup
    {
        const string DONE_PREF_KEY = "TmpCjkFontSetup_Done_v1";

        static TmpCjkFontSetup()
        {
            if (SessionState.GetBool(DONE_PREF_KEY, false)) return;
            SessionState.SetBool(DONE_PREF_KEY, true);
            EditorApplication.delayCall += RunSetup;
        }

        static void RunSetup()
        {
            var settings = TMP_Settings.instance;
            if (settings == null) return;

            var so   = new SerializedObject(settings);
            var prop = so.FindProperty("m_EnableOSFallbackSupport");
            if (prop == null || prop.boolValue) return;

            prop.boolValue = true;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[TmpCjkFontSetup] TMP OS font fallback enabled for CJK rendering.");
        }
    }
}
