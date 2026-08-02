#if UNITY_EDITOR
using System.IO;
using Fields.Grass;
using Fields.Hay;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor menu items under "Fields/".
/// Grass + hay reset requires Play mode (GPU buffers and MonoBehaviour state only exist then).
/// Save file deletion can be done any time.
/// </summary>
public static class FieldsEditorTools
{
    [MenuItem("Fields/Delete Save File")]
    static void DeleteSaveFile()
    {
        string path = Path.Combine(Application.persistentDataPath, "fields_save.json");
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log($"[Fields] Save file deleted: {path}");
        }
        else
        {
            Debug.Log($"[Fields] No save file found at: {path}");
        }
    }

    [MenuItem("Fields/Reset Grass + Hay (Play Mode only)")]
    static void ResetGrassAndHay()
    {
        if (!Application.isPlaying)
        {
            EditorUtility.DisplayDialog("Fields Reset",
                "Kérlek lépj Play módba, majd ismét kattints erre a menüpontra.",
                "OK");
            return;
        }

        int fieldCount = 0, hayCount = 0, pileCount = 0;

        foreach (var field in Object.FindObjectsByType<GrassField>(FindObjectsSortMode.None))
        {
            field.ResetGrass();
            fieldCount++;
        }

        foreach (var hay in Object.FindObjectsByType<HayAccumulationSystem>(FindObjectsSortMode.None))
        {
            hay.ResetHay();
            hayCount++;
        }

        foreach (var pile in Object.FindObjectsByType<HayPile>(FindObjectsSortMode.None))
        {
            Object.Destroy(pile.gameObject);
            pileCount++;
        }

        Fields.Save.SaveSystem.Instance?.DeleteSave();

        Debug.Log($"[Fields] Reset: {fieldCount} grass fields, {hayCount} hay systems, {pileCount} piles destroyed. Save deleted.");
    }

    [MenuItem("Fields/Reset Grass + Hay (Play Mode only)", validate = true)]
    static bool ResetGrassValidate() => true; // always shown; runtime check inside method
}
#endif