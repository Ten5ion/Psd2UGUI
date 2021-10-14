using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    public class TextureUtils
    {
        public static Dictionary<GUID, Sprite> CreateAtlas(Sprite[] originalSprites, int atlasSize, string pngPath, float pixelsPerUnit)
        {
            if (originalSprites.Length == 0)
                return null;
            
            Rect[] rects;
            var atlas = new Texture2D(atlasSize, atlasSize);
            var textureArray = new Texture2D[originalSprites.Length];
            for (var i = 0; i < originalSprites.Length; ++i) {
                textureArray[i] = Sprite2Texture(originalSprites[i]);
            }
            rects = atlas.PackTextures(textureArray, 2, atlasSize);
            var spriteMetaDatas = new List<UnityEditor.SpriteMetaData>();

            for (var i = 0; i < rects.Length; i++) {
                var smd = new UnityEditor.SpriteMetaData {
                    name = originalSprites[i].name,
                    rect = new Rect(rects[i].xMin * atlas.width, rects[i].yMin * atlas.height, rects[i].width * atlas.width, rects[i].height * atlas.height),
                    pivot = new Vector2(0.5f, 0.5f),
                    alignment = (int)SpriteAlignment.Center
                };
                spriteMetaDatas.Add(smd);
            }

            byte[] buf = atlas.EncodeToPNG();

            File.WriteAllBytes(pngPath, buf);
            AssetDatabase.Refresh();

            var textureImporter = AssetImporter.GetAtPath(pngPath) as TextureImporter;

            textureImporter.maxTextureSize = atlasSize;
            textureImporter.spritesheet = spriteMetaDatas.ToArray();
            textureImporter.textureType = TextureImporterType.Sprite;
            textureImporter.spriteImportMode = SpriteImportMode.Multiple;
            textureImporter.spritePivot = new Vector2(0.5f, 0.5f);
            textureImporter.spritePixelsPerUnit = pixelsPerUnit;
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
            
            var sprites = AssetDatabase.LoadAllAssetsAtPath(pngPath);

            var dict = new Dictionary<GUID, Sprite>();
            foreach (var originalSp in originalSprites) {
                var sp = (Sprite)sprites.Single(s => s.name == originalSp.name);
                dict[originalSp.GetSpriteID()] = sp;
            }

            return dict;
        }

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
    }
}