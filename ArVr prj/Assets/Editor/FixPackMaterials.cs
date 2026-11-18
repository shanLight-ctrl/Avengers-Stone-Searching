#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Put this file in Assets/Editor/. Then run: Tools > Cosmic > Fix Pack Materials To URP
// Rebuilds the pack's HDRP-style materials as URP Lit, reusing their saved Albedo + Normal textures.
// The MaskMap (HDRP packed texture) is not compatible with URP Lit, so metallic/smoothness are set to fixed values.
public static class FixPackMaterials
{
    const string PackFolder = "Assets/Dzeruza";

    [MenuItem("Tools/Cosmic/Fix Pack Materials To URP")]
    static void Run()
    {
        var urp = Shader.Find("Universal Render Pipeline/Lit");
        if (urp == null) { Debug.LogError("URP Lit shader not found. Is URP installed and active?"); return; }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { PackFolder });
        int fixedCount = 0, noAlbedo = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Read the saved properties straight from the serialized data, so it works even with a broken shader.
            var so = new SerializedObject(mat);
            Texture albedo = null, normal = null;
            Color color = Color.white;
            string seen = "";

            var texEnvs = so.FindProperty("m_SavedProperties.m_TexEnvs");
            for (int i = 0; i < texEnvs.arraySize; i++)
            {
                var e = texEnvs.GetArrayElementAtIndex(i);
                string name = e.FindPropertyRelative("first").stringValue;
                var t = e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                if (t == null) continue;
                seen += name + " ";
                if (name == "_BaseColorMap" || name == "_BaseMap" || name == "_MainTex") { if (albedo == null) albedo = t; }
                else if (name == "_NormalMap" || name == "_BumpMap") { if (normal == null) normal = t; }
            }

            var colors = so.FindProperty("m_SavedProperties.m_Colors");
            for (int i = 0; i < colors.arraySize; i++)
            {
                var e = colors.GetArrayElementAtIndex(i);
                string name = e.FindPropertyRelative("first").stringValue;
                if (name == "_BaseColor" || name == "_Color") color = e.FindPropertyRelative("second").colorValue;
            }

            mat.shader = urp;
            mat.SetColor("_BaseColor", color);
            if (albedo != null) mat.SetTexture("_BaseMap", albedo);
            else { noAlbedo++; Debug.LogWarning($"No albedo found for {path}. Texture properties seen: {seen}"); }
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Metallic", 0.5f);
            mat.SetFloat("_Smoothness", 0.5f);

            EditorUtility.SetDirty(mat);
            fixedCount++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"Fix Pack Materials: updated {fixedCount} materials, {noAlbedo} had no albedo texture.");
    }
}
#endif
