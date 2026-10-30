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
            if (assetPath != "Assets/YasudaHaruto/Resources/RuneSpell/MagicAimArrow.png") return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
        }
    }
}
