using System;
using System.Collections.Generic;
using System.IO;
using PDNWrapper;
using UnityEngine;
using Unity.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor.AssetImporters;
using UnityEditor.U2D.Animation;
using UnityEditor.U2D.Common;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace UnityEditor.U2D.PSD
{
    public class PSDImporter
    {
        public class UniqueNameGenerator
        {
            List<int> m_NameHash = new List<int>();

            public bool ContainHash(int i)
            {
                return m_NameHash.Contains(i);
            }

            public void AddHash(int i)
            {
                m_NameHash.Add(i);
            }
            
            public string GetUniqueName(string name)
            {
                return PSDImporter.GetUniqueName(name, m_NameHash);
            }
        }
        
        class GameObjectCreationFactory : UniqueNameGenerator
        {

            public GameObject CreateGameObject(string name, params System.Type[] components)
            {
                var newName = GetUniqueName(name);
                return new GameObject(newName, components);
            }
        }

        TextureImporterSettings m_TextureImporterSettings = new TextureImporterSettings() {
            mipmapEnabled = true,
            mipmapFilter = TextureImporterMipFilter.BoxFilter,
            sRGBTexture = true,
            borderMipmap = false,
            mipMapsPreserveCoverage = false,
            alphaTestReferenceValue = 0.5f,
            readable = false,

#if ENABLE_TEXTURE_STREAMING
            streamingMipmaps = true,
#endif

            fadeOut = false,
            mipmapFadeDistanceStart = 1,
            mipmapFadeDistanceEnd = 3,

            convertToNormalMap = false,
            heightmapScale = 0.25F,
            normalMapFilter = 0,

            generateCubemap = TextureImporterGenerateCubemap.AutoCubemap,
            cubemapConvolution = 0,

            seamlessCubemap = false,

            npotScale = TextureImporterNPOTScale.ToNearest,

            spriteMode = (int)SpriteImportMode.Multiple,
            spriteExtrude = 1,
            spriteMeshType = SpriteMeshType.Tight,
            spriteAlignment = (int)SpriteAlignment.Center,
            spritePivot = new Vector2(0.5f, 0.5f),
            spritePixelsPerUnit = 100.0f,
            spriteBorder = new Vector4(0.0f, 0.0f, 0.0f, 0.0f),

            alphaSource = TextureImporterAlphaSource.FromInput,
            alphaIsTransparency = true,
            spriteTessellationDetail = -1.0f,

            textureType = TextureImporterType.Sprite,
            textureShape = TextureImporterShape.Texture2D,

            filterMode = FilterMode.Bilinear,
            aniso = 1,
            mipmapBias = 0.0f,
            wrapModeU = TextureWrapMode.Repeat,
            wrapModeV = TextureWrapMode.Repeat,
            wrapModeW = TextureWrapMode.Repeat,
        };
        
        List<SpriteMetaData> m_SpriteImportData = new List<SpriteMetaData>(); // we use index 0 for single sprite and the rest for multiple sprites
        List<SpriteMetaData> m_MosaicSpriteImportData = new List<SpriteMetaData>();
        List<SpriteMetaData> m_RigSpriteImportData = new List<SpriteMetaData>();

        List<TextureImporterPlatformSettings> m_PlatformSettings = new List<TextureImporterPlatformSettings>();
        bool m_MosaicLayers = true;
        Vector2 m_DocumentPivot = Vector2.zero;
        SpriteAlignment m_DocumentAlignment = SpriteAlignment.BottomCenter;
        bool m_ImportHiddenLayers = false;
        int m_ImportedTextureWidth;
        int m_ImportedTextureHeight;
        Vector2Int m_DocumentSize;

        bool m_KeepDupilcateSpriteName = false;

        SpriteCategoryList m_SpriteCategoryList = new SpriteCategoryList() {categories = new List<SpriteCategory>()};
        GameObjectCreationFactory m_GameObjectFactory = new GameObjectCreationFactory();

        internal SpriteCategoryList spriteCategoryList { get { return m_SpriteCategoryList; } set { m_SpriteCategoryList = value; } }

        int m_TextureActualWidth;
        internal int textureActualWidth
        {
            get { return m_TextureActualWidth; }
            private set { m_TextureActualWidth = value; }
        }

        int m_TextureActualHeight;
        internal int textureActualHeight
        {
            get { return m_TextureActualHeight; }
            private set { m_TextureActualHeight = value; }
        }

        string m_SpritePackingTag = "";

        bool m_ResliceFromLayer = false;
        bool m_CharacterMode = true;

        List<PSDLayer> m_MosaicPSDLayers = new List<PSDLayer>();

        bool m_GenerateGOHierarchy = true;

        string m_TextureAssetName = null;

        string m_PrefabAssetName = null;

        string m_SpriteLibAssetName = null;

        SecondarySpriteTexture[] m_SecondarySpriteTextures;

        private string _exportRoot;
        private string _gameObjectName;
        private bool _createAtlas;
        private NativeArray<Color32> _psdPixels;
        private Vector2Int _docSize = Vector2Int.zero;
        internal Document PsdDoc { get; private set; }

        public void LoadPsdDocument(string psdPath) {
            var fileStream = new FileStream(psdPath, FileMode.Open, FileAccess.Read);
            try {
                PsdDoc = PaintDotNet.Data.PhotoshopFileType.PsdLoad.Load(fileStream);

                ValidatePSDLayerId(PsdDoc);
            }
            finally {
                fileStream.Close();
            }
        }
        
        public void ImportPsd(string exportRoot, string exportName, bool createAtlas) {
            if (PsdDoc == null) return;
            
            _exportRoot = exportRoot;
            _gameObjectName = exportName;
            _createAtlas = createAtlas;
            
            const string progressBarTitle = "PSD Importer";
            var progressBarInfo = $"Importing PSD File...";
            EditorUtility.DisplayProgressBar(progressBarTitle, progressBarInfo, 0);
            
            m_DocumentSize = new Vector2Int(PsdDoc.width, PsdDoc.height);
            var singleSpriteMode = m_TextureImporterSettings.textureType == TextureImporterType.Sprite && m_TextureImporterSettings.spriteMode != (int)SpriteImportMode.Multiple;
            EnsureSingleSpriteExist();
            
            EditorUtility.DisplayProgressBar(progressBarTitle, progressBarInfo, 0.2f);

            if (m_TextureImporterSettings.textureType != TextureImporterType.Sprite ||
                m_MosaicLayers == false || singleSpriteMode) {
                _psdPixels = new NativeArray<Color32>(PsdDoc.width * PsdDoc.height, Allocator.Persistent);
                try {
                    var spriteImportData = GetSpriteImportData();
                    FlattenImageTask.Execute(PsdDoc.Layers, m_ImportHiddenLayers, PsdDoc.width, PsdDoc.height, _psdPixels);

                    int spriteCount = spriteDataCount;
                    int spriteIndexStart = 1;

                    if (spriteImportData.Count <= 0 || spriteImportData[0] == null)
                    {
                        spriteImportData.Add(new SpriteMetaData());
                    }
                    spriteImportData[0].name = exportName + "_1";
                    spriteImportData[0].alignment = (SpriteAlignment)m_TextureImporterSettings.spriteAlignment;
                    spriteImportData[0].border = m_TextureImporterSettings.spriteBorder;
                    spriteImportData[0].pivot = m_TextureImporterSettings.spritePivot;
                    spriteImportData[0].rect = new Rect(0, 0, PsdDoc.width, PsdDoc.height);
                    if (singleSpriteMode) {
                        spriteCount = 1;
                        spriteIndexStart = 0;
                    }
                    textureActualWidth = PsdDoc.width;
                    textureActualHeight = PsdDoc.height;

                    _docSize.x = PsdDoc.width;
                    _docSize.y = PsdDoc.height;
                    
                    RegisterAssets(_psdPixels, _docSize.x, _docSize.y);
                } finally {
                    _psdPixels.Dispose();
                }
            } else {
                ImportFromLayers(PsdDoc);
            }
            
            if (_psdPixels.IsCreated)
                _psdPixels.Dispose();

            var layers = GetPSDLayers();
            foreach (var l in layers) {
                l.Dispose();
            }
            layers.Clear();

            EditorUtility.DisplayProgressBar(progressBarTitle, progressBarInfo, 1f);
            EditorUtility.ClearProgressBar();
        }

        static void ValidatePSDLayerId(List<BitmapLayer> layers, UniqueNameGenerator uniqueNameGenerator) {
            for (var i = 0; i < layers.Count; ++i) {
                if (uniqueNameGenerator.ContainHash(layers[i].LayerID)) {
                    var importWarning = string.Format("Layer {0}: LayerId is not unique. Mapping will be done by Layer's name.", layers[i].Name);
                    var layerName = uniqueNameGenerator.GetUniqueName(layers[i].Name);
                    if (layerName != layers[i].Name)
                        importWarning += "\nLayer names are not unique. Please ensure they are unique to for SpriteRect to be mapped back correctly.";
                    layers[i].LayerID = layerName.GetHashCode();
                    Debug.LogWarning(importWarning);
                }
                else {
                    uniqueNameGenerator.AddHash(layers[i].LayerID);
                }
                if (layers[i].ChildLayer != null) {
                    ValidatePSDLayerId(layers[i].ChildLayer, uniqueNameGenerator);
                }
            }
        }

        void ValidatePSDLayerId(Document doc) {
            UniqueNameGenerator uniqueNameGenerator = new UniqueNameGenerator();

            ValidatePSDLayerId(doc.Layers, uniqueNameGenerator);
        }
        
        string GetUniqueSpriteName(string name, List<int> namehash) {
            if (m_KeepDupilcateSpriteName)
                return name;
            return GetUniqueName(name, namehash);
        }

        void ImportFromLayers(Document doc) {
            _psdPixels = default(NativeArray<Color32>);

            List<int> layerIndex = new List<int>();
            List<int> spriteNameHash = new List<int>();
            var oldPsdLayers = GetPSDLayers();
            try {
                var psdLayers = new List<PSDLayer>();
                ExtractLayerTask.Execute(psdLayers, doc.Layers, m_ImportHiddenLayers);
                var removedLayersSprite = oldPsdLayers.Where(x => psdLayers.FirstOrDefault(y => y.layerID == x.layerID) == null).Select(z => z.spriteID).ToArray();
                for (var i = 0; i < psdLayers.Count; ++i) {
                    var j = 0;
                    var psdLayer = psdLayers[i];
                    for (; j < oldPsdLayers.Count; ++j) {
                        if (psdLayer.layerID == oldPsdLayers[j].layerID) {
                            psdLayer.spriteID = oldPsdLayers[j].spriteID;
                            psdLayer.spriteName = oldPsdLayers[j].spriteName;
                            psdLayer.mosaicPosition = oldPsdLayers[j].mosaicPosition;
                            break;
                        }
                    }
                }

                var expectedBufferLength = doc.width * doc.height;
                var layerBuffers = new List<NativeArray<Color32>>();
                for (var i = 0; i < psdLayers.Count; ++i) {
                    var l = psdLayers[i];
                    if (l.texture.IsCreated && l.texture.Length == expectedBufferLength) {
                        layerBuffers.Add(l.texture);
                        layerIndex.Add(i);
                    }
                }

                RectInt[] spritedata;
                int width, height;
                int padding = 4;
                Vector2Int[] uvTransform;
                ImagePacker.Pack(layerBuffers.ToArray(), doc.width, doc.height, padding, out _psdPixels, out width, out height, out spritedata, out uvTransform);
                var spriteImportData = GetSpriteImportData();
                if (spriteImportData.Count <= 0 || shouldResliceFromLayer) {
                    var newSpriteMeta = new List<SpriteMetaData>();

                    for (var i = 0; i < spritedata.Length && i < layerIndex.Count; ++i) {
                        var spriteSheet = spriteImportData.FirstOrDefault(x => x.spriteID == psdLayers[layerIndex[i]].spriteID);
                        if (spriteSheet == null) {
                            spriteSheet = new SpriteMetaData();
                            spriteSheet.border = Vector4.zero;
                            spriteSheet.alignment = (SpriteAlignment)m_TextureImporterSettings.spriteAlignment;
                            spriteSheet.pivot = m_TextureImporterSettings.spritePivot;
                        }

                        psdLayers[layerIndex[i]].spriteName = GetUniqueSpriteName(psdLayers[layerIndex[i]].name, spriteNameHash);
                        spriteSheet.name = psdLayers[layerIndex[i]].spriteName;
                        spriteSheet.rect = new Rect(spritedata[i].x, spritedata[i].y, spritedata[i].width, spritedata[i].height);
                        spriteSheet.uvTransform = uvTransform[i];

                        psdLayers[layerIndex[i]].spriteID = spriteSheet.spriteID;
                        psdLayers[layerIndex[i]].mosaicPosition = spritedata[i].position;
                        newSpriteMeta.Add(spriteSheet);
                    }
                    spriteImportData.Clear();
                    spriteImportData.AddRange(newSpriteMeta);
                } else {
                    spriteImportData.RemoveAll(x => removedLayersSprite.Contains(x.spriteID));

                    // First look for any user created SpriteRect and add those into the name hash
                    foreach (var spriteData in spriteImportData) {
                        var psdLayer = psdLayers.FirstOrDefault(x => x.spriteID == spriteData.spriteID);
                        if (psdLayer == null)
                            spriteNameHash.Add(spriteData.name.GetHashCode());
                    }

                    foreach (var spriteData in spriteImportData) {
                        var psdLayer = psdLayers.FirstOrDefault(x => x.spriteID == spriteData.spriteID);
                        if (psdLayer == null)
                            spriteData.uvTransform = new Vector2Int((int)spriteData.rect.position.x, (int)spriteData.rect.position.y);
                        // If it is user created rect or the name has been changed before
                        // add it into the spriteNameHash and we don't copy it over from the layer
                        if (psdLayer == null || psdLayer.spriteName != spriteData.name)
                            spriteNameHash.Add(spriteData.name.GetHashCode());

                        // If the sprite name has not been changed, we ensure the new
                        // layer name is still unique and use it as the sprite name
                        if (psdLayer != null && psdLayer.spriteName == spriteData.name)
                        {
                            psdLayer.spriteName = GetUniqueSpriteName(psdLayer.name, spriteNameHash);
                            spriteData.name = psdLayer.spriteName;
                        }
                    }

                    //Update names for those user has not changed and add new sprite rect based on PSD file.
                    for (var k = 0; k < layerIndex.Count; ++k) {
                        var i = layerIndex[k];
                        var spriteSheet = spriteImportData.FirstOrDefault(x => x.spriteID == psdLayers[i].spriteID);
                        var inOldLayer = oldPsdLayers.FindIndex(x => x.layerID == psdLayers[i].layerID) != -1;
                        if (spriteSheet == null && !inOldLayer) {
                            spriteSheet = new SpriteMetaData();
                            spriteImportData.Add(spriteSheet);
                            spriteSheet.rect = new Rect(spritedata[k].x, spritedata[k].y, spritedata[k].width, spritedata[k].height);
                            spriteSheet.border = Vector4.zero;
                            spriteSheet.alignment = (SpriteAlignment)m_TextureImporterSettings.spriteAlignment;
                            spriteSheet.pivot = m_TextureImporterSettings.spritePivot;
                            psdLayers[i].spriteName = GetUniqueSpriteName(psdLayers[i].name, spriteNameHash);
                            spriteSheet.name = psdLayers[i].spriteName;
                        } else if (spriteSheet != null) {
                            var r = spriteSheet.rect;
                            r.position = spriteSheet.rect.position - psdLayers[i].mosaicPosition + spritedata[k].position;
                            spriteSheet.rect = r;
                        }

                        if (spriteSheet != null) {
                            spriteSheet.uvTransform = uvTransform[k];
                            psdLayers[i].spriteID = spriteSheet.spriteID;
                            psdLayers[i].mosaicPosition = spritedata[k].position;
                        }
                    }
                }

                foreach (var l in oldPsdLayers) {
                    l.Dispose();
                }
                oldPsdLayers.Clear();

                oldPsdLayers.AddRange(psdLayers);
                m_ImportedTextureHeight = textureActualHeight = height;
                m_ImportedTextureWidth = textureActualWidth = width;
                
                // var generatedTexture = ImportTexture(output, width, height, 0, spriteImportData.Count);
                //
                // if (generatedTexture.texture)
                // {
                //     m_ImportedTextureHeight = generatedTexture.texture.height;
                //     m_ImportedTextureWidth = generatedTexture.texture.width;
                // }

                _docSize.x = width;
                _docSize.y = height;
                
                RegisterAssets(_psdPixels, _docSize.x, _docSize.y);
            }
            catch (Exception e) {
                Dispose();
            }
        }

        public void Dispose() {
            if (_psdPixels.IsCreated)
                _psdPixels.Dispose();

            var layers = GetPSDLayers();
            foreach (var l in layers) {
                l.Dispose();
            }
            layers.Clear();
            
            if (PsdDoc != null) {
                PsdDoc.Dispose();
                PsdDoc = null;
            }
        }

        void EnsureSingleSpriteExist()
        {
            if (m_SpriteImportData.Count <= 0)
                m_SpriteImportData.Add(new SpriteMetaData()); // insert default for single sprite mode
        }

        void RegisterAssets(NativeArray<Color32> pixels, int width, int height) {
            var spriteImportData = GetSpriteImportData();
            
            // Create atlas
            var sprites = TextureUtils.CreateAtlas(pixels, width, height, spriteImportData, Path.Combine(_exportRoot, _gameObjectName), _createAtlas);
            
            // Create prefab
            CreatePrefab(_gameObjectName, sprites);
        }

        private void CreatePrefab(string name, List<Sprite> sprites) {
            if (sprites == null || sprites.Count == 0) return;
            
            var spriteImportData = GetSpriteImportData();
            var root = new GameObject {
                name = name
            };

            var rectTransform = root.AddComponent<RectTransform>();

            var psdLayers = GetPSDLayers();
            for (var i = 0; i < psdLayers.Count; i++) {
                BuildGroupGameObject(psdLayers, i, root.transform);
            }
            
            for (var i = 0; i < psdLayers.Count; ++i) {
                var l = psdLayers[i];
                if (l.gameObject == null) continue;

                var go = l.gameObject;
                go.AddComponent<RectTransform>();
                
                var sprite = sprites.FirstOrDefault(x => x.name == l.spriteName);
                var spriteMetaData = spriteImportData.FirstOrDefault(x => x.spriteID == l.spriteID);
                if (sprite != null && spriteMetaData != null) {
                    var uvTransform = spriteMetaData.uvTransform;
                    var outlineOffset = new Vector2(
                        spriteMetaData.rect.x - uvTransform.x + (spriteMetaData.pivot.x * spriteMetaData.rect.width),
                        spriteMetaData.rect.y - uvTransform.y + (spriteMetaData.pivot.y * spriteMetaData.rect.height)) * definitionScale / sprite.pixelsPerUnit;
            
                    var pos = new Vector3(outlineOffset.x, outlineOffset.y, 0);
                    pos = new Vector3(pos.x * sprite.pixelsPerUnit - rectTransform.sizeDelta.x / 2, pos.y * sprite.pixelsPerUnit - rectTransform.sizeDelta.y / 2, 0);
            
                    go.transform.position = pos;
            
                    var image = go.AddComponent<Image>();
                    image.sprite = sprite;
                    image.raycastTarget = false;
                    
                    var color = image.color;
                    color.a *= l.opacity / 255f;
                    image.color = color;
                    
                    image.SetNativeSize();
                }
            }
            
            OnGameObjectProcessor(root);

            PrefabUtility.SaveAsPrefabAsset(root, Path.Combine(_exportRoot, $"{name}.prefab"));
            
            Object.DestroyImmediate(root);
        }

        private void OnGameObjectProcessor(GameObject root) {
            var interfaceType = typeof(PSDGameObjectProcessor);
            PSDGameObjectProcessor processor = new PSDGameObjectProcessor();
            
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in assemblies) {
                foreach (var type in assembly.GetTypes()) {
                    if (type.IsClass && interfaceType.IsAssignableFrom(type)) {
                        processor = (PSDGameObjectProcessor)Activator.CreateInstance(type);
                        goto End;
                    }
                }
            }
            
            End: ;
            
            processor.OnPSDGameObjectCreated(root);
        }

        bool SpriteIsMainFromSpriteLib(string spriteId, out string categoryName)
        {
            categoryName = "";
            if (m_SpriteCategoryList.categories != null)
            {
                foreach (var category in m_SpriteCategoryList.categories)
                {
                    var index = category.labels.FindIndex(x => x.spriteId == spriteId);
                    if (index == 0)
                    {
                        categoryName = category.name;
                        return true;
                    }
                    if (index > 0)
                        return false;
                }
            }
            return true;
        }

        void BuildGroupGameObject(List<PSDLayer> psdGroup, int index, Transform root) {
            var layer = psdGroup[index];
            var spriteData = GetSpriteImportData().FirstOrDefault(x => x.spriteID == layer.spriteID);
            
            if (layer.gameObject == null) {
                if (m_GenerateGOHierarchy || (!layer.spriteID.Empty() && layer.isGroup == false)) {
                    // Determine if need to create GameObject i.e. if the sprite is not in a SpriteLib or if it is the first one
                    string categoryName;
                    var b = SpriteIsMainFromSpriteLib(layer.spriteID.ToString(), out categoryName);
                    var goName = string.IsNullOrEmpty(categoryName) ? spriteData  != null ? spriteData.name : layer.name : categoryName;
                    if (b)
                        layer.gameObject = m_GameObjectFactory.CreateGameObject(goName);
                }
                if (layer.parentIndex >= 0 && m_GenerateGOHierarchy) {
                    BuildGroupGameObject(psdGroup, layer.parentIndex, root);
                    root = psdGroup[layer.parentIndex].gameObject.transform;
                }

                if (layer.gameObject != null) {
                    layer.gameObject.transform.SetParent(root);
                    layer.gameObject.transform.SetAsFirstSibling();
                }
            }
        }

        bool shouldResliceFromLayer
        {
            get { return m_ResliceFromLayer && m_MosaicLayers && spriteImportMode == SpriteImportMode.Multiple; }
        }

        float definitionScale
        {
            get
            {
                float definitionScaleW = m_ImportedTextureWidth / (float)textureActualWidth;
                float definitionScaleH = m_ImportedTextureHeight / (float)textureActualHeight;
                return Mathf.Min(definitionScaleW, definitionScaleH);
            }
        }

        static string SanitizeName(string name)
        {
            string newName = null;
            // We can't create asset name with these name.
            if ((name.Length == 2 && name[0] == '.' && name[1] == '.')
                || (name.Length == 1 && name[0] == '.')
                || (name.Length == 1 && name[0] == '/'))
                newName += name + "_";

            if (!string.IsNullOrEmpty(newName))
            {
                Debug.LogWarning(string.Format("File contains layer with invalid name for generating asset. {0} is renamed to {1}", name, newName));
                return newName;
            }
            return name;
        }

        static string GetUniqueName(string name, List<int> stringHash, bool logNewNameGenerated = false, UnityEngine.Object context = null)
        {
            string uniqueName = string.Copy(SanitizeName(name));
            int index = 1;
            while (true)
            {
                int hash = uniqueName.GetHashCode();
                var p = stringHash.Where(x => x == hash);
                if (!p.Any())
                {
                    stringHash.Add(hash);
                    if (logNewNameGenerated && name != uniqueName)
                        Debug.Log(string.Format("Asset name {0} is changed to {1} to ensure uniqueness", name, uniqueName), context);
                    return uniqueName;
                }
                uniqueName = string.Format("{0}_{1}", name, index);
                ++index;
            }
        }

        // ISpriteEditorDataProvider interface
        internal SpriteImportMode spriteImportMode
        {
            get
            {
                return m_TextureImporterSettings.textureType != TextureImporterType.Sprite ?
                    SpriteImportMode.None :
                    (SpriteImportMode)m_TextureImporterSettings.spriteMode;
            }
        }

        internal int spriteDataCount
        {
            get
            {
                var spriteImportData = GetSpriteImportData();
                if (mosaicMode)
                    return spriteImportData.Count;
                if (spriteImportMode != SpriteImportMode.Multiple)
                    return 1;
                return spriteImportData.Count - 1;
            }
        }

        List<SpriteMetaData> GetSpriteImportData()
        {
            return mosaicMode ? m_MosaicSpriteImportData : m_SpriteImportData;
        }

        internal List<PSDLayer> GetPSDLayers()
        {
            return mosaicMode ? m_MosaicPSDLayers : null;
        }

        bool mosaicMode
        {
            get { return spriteImportMode == SpriteImportMode.Multiple && m_MosaicLayers; }
        }

        internal SecondarySpriteTexture[] secondaryTextures
        {
            get { return m_SecondarySpriteTextures; }
            set { m_SecondarySpriteTextures = value; }
        }

        internal void ReadTextureSettings(TextureImporterSettings dest)
        {
            m_TextureImporterSettings.CopyTo(dest);
        }
    }
}
