using UnityEditor;
using UnityEngine;
using TMPro;
using System.IO;

public static class FontGenerator
{
    [MenuItem("Tools/Generate Vietnamese Font")]
    public static void GenerateFont()
    {
        string winFontPath = @"C:\Windows\Fonts\segoeui.ttf";
        string targetDir = Path.Combine(Application.dataPath, "TextMesh Pro/Fonts");
        string targetFontPath = Path.Combine(targetDir, "SegoeUI.ttf");

        if (!Directory.Exists(targetDir))
        {
            Directory.CreateDirectory(targetDir);
        }

        if (File.Exists(winFontPath))
        {
            File.Copy(winFontPath, targetFontPath, true);
            Debug.Log("Copied Segoe UI TTF to project.");
        }
        else
        {
            Debug.LogError("Segoe UI font file not found at " + winFontPath);
            return;
        }

        AssetDatabase.Refresh();

        // Load the imported TTF font asset
        string relFontPath = "Assets/TextMesh Pro/Fonts/SegoeUI.ttf";
        Font ttfFont = AssetDatabase.LoadAssetAtPath<Font>(relFontPath);
        if (ttfFont == null)
        {
            Debug.LogError("Failed to load TTF Font at " + relFontPath);
            return;
        }

        // Create the TMP Font Asset
        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(ttfFont);
        if (fontAsset == null)
        {
            Debug.LogError("Failed to create TMP Font Asset.");
            return;
        }

        // Set Atlas Population Mode to Dynamic
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;

        string resourcesDir = Path.Combine(Application.dataPath, "TextMesh Pro/Resources/Fonts & Materials");
        if (!Directory.Exists(resourcesDir))
        {
            Directory.CreateDirectory(resourcesDir);
        }

        string relAssetPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/SegoeUI SDF.asset";
        AssetDatabase.CreateAsset(fontAsset, relAssetPath);

        // Register it as a fallback in the default LiberationSans SDF font
        string libSansPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        TMP_FontAsset libSans = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(libSansPath);
        if (libSans != null)
        {
            if (libSans.fallbackFontAssetTable == null)
            {
                libSans.fallbackFontAssetTable = new System.Collections.Generic.List<TMP_FontAsset>();
            }
            if (!libSans.fallbackFontAssetTable.Contains(fontAsset))
            {
                libSans.fallbackFontAssetTable.Add(fontAsset);
                EditorUtility.SetDirty(libSans);
            }
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Successfully generated SegoeUI SDF Font Asset at " + relAssetPath);
    }
}
