using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEditor.U2D.Common;
using UnityEditor.U2D.Animation;
using System;
using UnityEditor.U2D.Sprites;
using UnityEngine.U2D;

namespace UnityEditor.U2D.PSD
{
    internal abstract class PSDDataProvider
    {
        public PSDImporter dataProvider;
    }

    internal class TextureDataProvider : PSDDataProvider, ITextureDataProvider
    {
        Texture2D m_ReadableTexture;
        Texture2D m_OriginalTexture;

        PSDImporter textureImporter { get { return (PSDImporter)dataProvider.targetObject; } }

        public Texture2D texture
        {
            get
            {
                if (m_OriginalTexture == null)
                    m_OriginalTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureImporter.assetPath);
                return m_OriginalTexture;
            }
        }

        public Texture2D previewTexture
        {
            get { return texture; }
        }

        public Texture2D GetReadableTexture2D()
        {
            if (m_ReadableTexture == null)
            {
                m_ReadableTexture = InternalEditorBridge.CreateTemporaryDuplicate(texture, texture.width, texture.height);
                if (m_ReadableTexture != null)
                    m_ReadableTexture.filterMode = texture.filterMode;
            }
            return m_ReadableTexture;
        }

        public void GetTextureActualWidthAndHeight(out int width, out int height)
        {
            width = dataProvider.textureActualWidth;
            height = dataProvider.textureActualHeight;
        }
    }

    internal class SecondaryTextureDataProvider : PSDDataProvider, ISecondaryTextureDataProvider
    {
        public SecondarySpriteTexture[] textures
        {
            get { return dataProvider.secondaryTextures; }
            set { dataProvider.secondaryTextures = value; }
        }
    }

    internal class SpriteOutlineDataProvider : PSDDataProvider, ISpriteOutlineDataProvider
    {
        public List<Vector2[]> GetOutlines(GUID guid)
        {
            var sprite = ((SpriteMetaData)dataProvider.GetSpriteData(guid));
            Assert.IsNotNull(sprite, string.Format("Sprite not found for GUID:{0}", guid.ToString()));

            var outline = sprite.spriteOutline;
            if (outline != null)
                return outline.Select(x => x.outline).ToList();
            return new List<Vector2[]>();
        }

        public void SetOutlines(GUID guid, List<Vector2[]> data)
        {
            var sprite = dataProvider.GetSpriteDataFromAllMode(guid);
            if (sprite != null)
                ((SpriteMetaData)sprite).spriteOutline = data.Select(x => new SpriteOutline() {outline = x}).ToList();
        }

        public float GetTessellationDetail(GUID guid)
        {
            return ((SpriteMetaData)dataProvider.GetSpriteData(guid)).tessellationDetail;
        }

        public void SetTessellationDetail(GUID guid, float value)
        {
            var sprite = dataProvider.GetSpriteDataFromAllMode(guid);
            if (sprite != null)
                ((SpriteMetaData)sprite).tessellationDetail = value;
        }
    }
    internal class SpriteLibraryDataProvider : PSDDataProvider, ISpriteLibDataProvider
    {
        public SpriteCategoryList GetSpriteCategoryList()
        {
            return dataProvider.spriteCategoryList;
        }

        public void SetSpriteCategoryList(SpriteCategoryList spriteCategoryList)
        {
            dataProvider.spriteCategoryList = spriteCategoryList;
        }
    }
}
