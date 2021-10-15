using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using UnityEngine.U2D;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.PSD
{
    internal class TextureUtils
    {
        // public static Dictionary<GUID, Sprite> CreateAtlas(Sprite[] originalSprites, int atlasSize, string pngPath, float pixelsPerUnit)
        // {
        //     if (originalSprites.Length == 0)
        //         return null;
        //     
        //     Rect[] rects;
        //     var atlas = new Texture2D(atlasSize, atlasSize);
        //     var textureArray = new Texture2D[originalSprites.Length];
        //     for (var i = 0; i < originalSprites.Length; ++i) {
        //         textureArray[i] = Sprite2Texture(originalSprites[i]);
        //     }
        //     rects = atlas.PackTextures(textureArray, 2, atlasSize);
        //     var spriteMetaDatas = new List<UnityEditor.SpriteMetaData>();
        //
        //     for (var i = 0; i < rects.Length; i++) {
        //         var smd = new UnityEditor.SpriteMetaData {
        //             name = originalSprites[i].name,
        //             rect = new Rect(rects[i].xMin * atlas.width, rects[i].yMin * atlas.height, rects[i].width * atlas.width, rects[i].height * atlas.height),
        //             pivot = new Vector2(0.5f, 0.5f),
        //             alignment = (int)SpriteAlignment.Center
        //         };
        //         spriteMetaDatas.Add(smd);
        //     }
        //
        //     byte[] buf = atlas.EncodeToPNG();
        //
        //     File.WriteAllBytes(pngPath, buf);
        //     AssetDatabase.Refresh();
        //
        //     var textureImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;
        //
        //     textureImporter.maxTextureSize = atlasSize;
        //     textureImporter.spritesheet = spriteMetaDatas.ToArray();
        //     textureImporter.textureType = TextureImporterType.Sprite;
        //     textureImporter.spriteImportMode = SpriteImportMode.Multiple;
        //     textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
        //     textureImporter.spritePixelsPerUnit = pixelsPerUnit;
        //     AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
        //     
        //     var sprites = AssetDatabase.LoadAllAssetsAtPath(pngPath);
        //
        //     var dict = new Dictionary<GUID, Sprite>();
        //     foreach (var originalSp in originalSprites) {
        //         var sp = (Sprite)sprites.Single(s => s.name == originalSp.name);
        //         dict[originalSp.GetSpriteID()] = sp;
        //     }
        //
        //     return dict;
        // }

        public static Texture2D Sprite2Texture(Sprite sprite) {
            if (Mathf.Approximately(sprite.rect.height, sprite.texture.height)) {
                return sprite.texture;
            }

            var texture = sprite.texture;

            var x = (int)sprite.textureRect.x;
            var y = (int)sprite.textureRect.y;
            var width = Mathf.Max((int)sprite.textureRect.width, 1);
            var height = Mathf.Max((int)sprite.textureRect.height, 1);

            var pixels = texture.GetPixels(x, y, width, height); 

            var ret = new Texture2D(width, height);
            ret.SetPixels(pixels);
            ret.Apply();
    
            return ret;
        }

        public static List<Sprite> CreateAtlas(NativeArray<Color32> image, int width, int height, List<SpriteMetaData> spriteImportData, string folder, bool createAtlas) {

            if (!Directory.Exists(folder)) {
                Directory.CreateDirectory(folder);
            }
            
            var pixels = new Color[image.Length];
            for (var i = 0; i < image.Length; i++) {
                pixels[i] = image[i];
            }
            
            var texture = new Texture2D(width, height);
            texture.SetPixels(pixels);
            texture.Apply();

            var output = new List<Sprite>();
            
            foreach (var spriteMetaData in spriteImportData) {
                var blockWidth = Mathf.Max((int)spriteMetaData.rect.width, 1);
                var blockHeight = Mathf.Max((int)spriteMetaData.rect.height, 1);
                var ps = texture.GetPixels(
                    (int)spriteMetaData.rect.xMin,
                    (int)spriteMetaData.rect.yMin,
                    blockWidth,
                    blockHeight);
                
                var blockTexture = new Texture2D(blockWidth, blockHeight);
                blockTexture.SetPixels(ps);
                blockTexture.Apply();
                
                var blockBuf = blockTexture.EncodeToPNG();

                var name = spriteMetaData.name.Replace("/", "_").Replace("\\", "_");
                var path = Path.Combine(folder, $"{name}.png");
                File.WriteAllBytes(path, blockBuf);
                
                AssetDatabase.Refresh();
                
                var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;

                textureImporter.maxTextureSize = 2048;
                textureImporter.textureType = TextureImporterType.Sprite;
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
                textureImporter.spritePixelsPerUnit = 100f;
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                
                var sp = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                output.Add(sp);
            }

            if (createAtlas) {
                var atlas = new SpriteAtlas();
                AssetDatabase.CreateAsset(atlas, $"{folder}.spriteatlas");
            
                var folderAsset = AssetDatabase.LoadAssetAtPath<Object>(folder);
                var objs = new Object[] { folderAsset };
                atlas.Add(objs);
                
                atlas.SetIncludeInBuild(false);
                
                var packingSettings = atlas.GetPackingSettings();
                packingSettings.enableTightPacking = false;
                atlas.SetPackingSettings(packingSettings);
                
                AssetDatabase.SaveAssets();
            }

            return output;
        }
    }
}