// ============================================================================
// FILE: Editor/FontImportFixer.cs
// PURPOSE: Forces embedded Sporefront fonts to use Dynamic character set so
//          Unity's legacy Text component can render at arbitrary sizes.
// ============================================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Sporefront.Editor
{
    /// <summary>
    /// Post-processor that ensures every TTF placed under Resources/Fonts/
    /// is imported as a Dynamic font. Run via Assets > Reimport All or by
    /// right-clicking the font and choosing Reimport.
    /// </summary>
    public class FontImportFixer : AssetPostprocessor
    {
        void OnPreprocessAsset()
        {
            if (!assetPath.StartsWith("Assets/Resources/Fonts/")) return;
            if (!assetPath.EndsWith(".ttf") && !assetPath.EndsWith(".otf")) return;

            var fontImporter = assetImporter as TrueTypeFontImporter;
            if (fontImporter == null) return;

            if (fontImporter.fontTextureCase != FontTextureCase.Dynamic)
            {
                fontImporter.fontTextureCase = FontTextureCase.Dynamic;
                Debug.Log($"[FontImportFixer] Set {System.IO.Path.GetFileName(assetPath)} to Dynamic.");
            }
        }
    }
}
#endif
