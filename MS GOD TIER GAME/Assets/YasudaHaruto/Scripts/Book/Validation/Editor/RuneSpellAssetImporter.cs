using UnityEditor;

namespace YasudaHaruto.BookValidation.Editor
{
    public sealed class RuneSpellAssetImporter : AssetPostprocessor
    {
        [InitializeOnLoadMethod]
        private static void EnsureSpriteImport()
        {
            EditorApplication.delayCall += () =>
            {
                var importer = AssetImporter.GetAtPath("Assets/YasudaHaruto/Resources/RuneSpell/MagicAimArrow.png") as TextureImporter;
                if (importer != null && importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.SaveAndReimport();
                }
            };
        }

        private void OnPreprocessTexture()
        {
            const string root = "Assets/YasudaHaruto/Resources/RuneSpell/";
            if (assetPath == root + "Zyumon_Z.png" || assetPath == root + "Text_Hanatsu.png")
            {
                var texture = (TextureImporter)assetImporter;
                texture.alphaIsTransparency = true;
                texture.mipmapEnabled = false;
                texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
                texture.npotScale = TextureImporterNPOTScale.None;
                return;
            }
            if (assetPath != root + "MagicAimArrow.png" &&
                !assetPath.StartsWith(root + "DirectionFrames/", System.StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = assetPath.EndsWith("MagicAimArrow.png") ? 512 : 1024;
            importer.textureCompression = assetPath.EndsWith("MagicAimArrow.png")
                ? TextureImporterCompression.Uncompressed : TextureImporterCompression.Compressed;
        }
    }
}
