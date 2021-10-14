using System.Collections.Generic;
using System.Linq;
using UnityEditor.U2D;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    internal class PSDImportPostProcessor : AssetPostprocessor
    {
        private static string s_CurrentApplyAssetPath = null;
        
        void OnPostprocessSprites(Texture2D texture, Sprite[] sprites)
        {
            var dataProviderFactories = new SpriteDataProviderFactories();
            dataProviderFactories.Init();
            PSDImporter psd = AssetImporter.GetAtPath(assetPath) as PSDImporter;
            if (psd == null)
                return;
            ISpriteEditorDataProvider importer = dataProviderFactories.GetSpriteEditorDataProviderFromObject(psd);
            if (importer != null)
            {
                importer.InitSpriteEditorDataProvider();
                var textureDataProvider = importer.GetDataProvider<ITextureDataProvider>();
                int actualWidth = 0, actualHeight = 0;
                textureDataProvider.GetTextureActualWidthAndHeight(out actualWidth, out actualHeight);
            }
        }
        
        public static string currentApplyAssetPath
        {
            set { s_CurrentApplyAssetPath = value; }
        }
        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromPath)
        {
            if (!string.IsNullOrEmpty(s_CurrentApplyAssetPath))
            {
                foreach (var asset in importedAssets)
                {
                    if (asset == s_CurrentApplyAssetPath)
                    {
                        var obj = AssetDatabase.LoadMainAssetAtPath(asset);
                        Selection.activeObject = obj;
                        Unsupported.SceneTrackerFlushDirty();
                        s_CurrentApplyAssetPath = null;
                        break;
                    }
                }
            }
        }
    }
}
