using UnityEditor;
using UnityEngine;
using System.IO;

namespace Fields.Editor
{
    /// <summary>
    /// Automatically converts all Red_Deer cat/accessory materials from the
    /// Built-In Standard shader to Universal Render Pipeline/Lit.
    /// Runs once on editor load; skips materials that are already on URP/Lit.
    /// </summary>
    [InitializeOnLoad]
    public static class CatMaterialFixer
    {
        const string CAT_FOLDER    = "Assets/Red_Deer/Cartoon_Animals";
        const string DONE_PREF_KEY = "CatMaterialFixer_Done_v1";

        static CatMaterialFixer()
        {
            // Skip if already ran this session / project
            if (SessionState.GetBool(DONE_PREF_KEY, false)) return;
            SessionState.SetBool(DONE_PREF_KEY, true);

            // Defer to next editor update so the AssetDatabase is fully ready
            EditorApplication.delayCall += RunFix;
        }

        static void RunFix()
        {
            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;   // URP not installed — silently skip

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { CAT_FOLDER });
            int fixedCount = 0;

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null || mat.shader == urpLit) continue;

                ConvertToUrpLit(mat, urpLit);
                EditorUtility.SetDirty(mat);
                fixedCount++;
            }

            if (fixedCount > 0)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[CatMaterialFixer] Converted {fixedCount} cat material(s) to URP/Lit.");
            }
        }

        static void ConvertToUrpLit(Material mat, Shader urpLit)
        {
            Texture mainTex       = mat.GetTexture("_MainTex");
            Color   baseColor     = mat.HasProperty("_Color")         ? mat.GetColor("_Color")     : Color.white;
            Texture bumpMap       = mat.GetTexture("_BumpMap");
            Texture metallicGloss = mat.GetTexture("_MetallicGlossMap");
            Texture occlusionMap  = mat.GetTexture("_OcclusionMap");
            Texture emissionMap   = mat.GetTexture("_EmissionMap");
            Color   emissionColor = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
            float   metallic      = mat.HasProperty("_Metallic")      ? mat.GetFloat("_Metallic")      : 0f;
            float   smoothness    = mat.HasProperty("_Glossiness")    ? mat.GetFloat("_Glossiness")    : 0.5f;

            mat.shader = urpLit;

            mat.SetTexture("_BaseMap",  mainTex);
            if (bumpMap       != null) mat.SetTexture("_BumpMap",          bumpMap);
            if (metallicGloss != null) mat.SetTexture("_MetallicGlossMap", metallicGloss);
            if (occlusionMap  != null) mat.SetTexture("_OcclusionMap",     occlusionMap);
            if (emissionMap   != null) mat.SetTexture("_EmissionMap",      emissionMap);

            mat.SetColor("_BaseColor",     baseColor);
            mat.SetColor("_EmissionColor", emissionColor);
            mat.SetFloat("_Metallic",      metallic);
            mat.SetFloat("_Smoothness",    smoothness);

            mat.SetFloat("_WorkflowMode",   1f);
            mat.SetFloat("_Surface",        0f);
            mat.SetFloat("_Cull",           2f);
            mat.SetFloat("_AlphaClip",      0f);
            mat.SetFloat("_ReceiveShadows", 1f);

            if (bumpMap       != null) mat.EnableKeyword("_NORMALMAP");
            if (metallicGloss != null) mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (emissionMap   != null || emissionColor != Color.black)
                mat.EnableKeyword("_EMISSION");

            mat.renderQueue = -1;
        }
    }
}
