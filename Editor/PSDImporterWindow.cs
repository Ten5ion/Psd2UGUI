using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PDNWrapper;
using Unity.Collections;
using UnityEngine;

namespace UnityEditor.U2D.PSD
{
    public class PSDImporterWindow : EditorWindow
    {
        private PSDImporter _psdImporter;
        private string _psdPath = null;
        private string _exportRoot = "Assets/";
        private string _exportName = null;
        private bool _createAtlas = false;

        private Texture2D _visibilityOnIcon;
        private Texture2D _visibilityOffIcon;
        private GUIStyle _visibilityButtonStyle;
        private GUIStyle _disableLabelStyle;
        private GUIStyle _disableFoldoutStyle;

        private Dictionary<BitmapLayer, bool> _foldStates = new Dictionary<BitmapLayer, bool>();

        private Dictionary<int, Texture2D> _layerTextures = new Dictionary<int, Texture2D>();
        private List<Texture2D> _previewTextures = new List<Texture2D>();
        private Dictionary<int, BitmapLayer> _bitmapLayerMap = new Dictionary<int, BitmapLayer>();

        private Vector2 _previewSize = new Vector2();

        private const float PreviewSize = 750f;
        
        private Vector2 _scrollPos;

        [MenuItem ("Tools/PSDImporter")]
        public static void ShowWindow() {
            var window = EditorWindow.GetWindow<PSDImporterWindow>();
            window.minSize = new Vector2(900, 750);
            window.titleContent = new GUIContent("PSDImporter");

            if (window._psdImporter == null) {
                window.Init();
            }
        }

        private void Init() {
            _visibilityOnIcon = EditorGUIUtility.FindTexture("animationvisibilitytoggleon");
            _visibilityOffIcon = EditorGUIUtility.FindTexture("animationvisibilitytoggleoff");

            _visibilityButtonStyle = new GUIStyle("IconButton");
            _visibilityButtonStyle.contentOffset = new Vector2(_visibilityButtonStyle.contentOffset.x,
                _visibilityButtonStyle.contentOffset.y + 2);

            _disableLabelStyle = new GUIStyle("PR DisabledLabel");
            
            _disableFoldoutStyle = new GUIStyle(EditorStyles.foldout) {
                normal = {
                    textColor = _disableLabelStyle.normal.textColor
                },
                onNormal = {
                    textColor = _disableLabelStyle.normal.textColor
                }
            };
        }

        private void OnGUI() {
            if (GUILayout.Button("Open a PSD File", GUILayout.Width(130), GUILayout.Height(30))) {
                _psdPath = EditorUtility.OpenFilePanel("Import PSD", "", "psd");
                _exportName = null;
                _foldStates.Clear();

                if (!string.IsNullOrEmpty(_psdPath)) {
                    if (_psdImporter != null) {
                        _psdImporter.Dispose();
                    }
                    _psdImporter = new PSDImporter();
                    _psdImporter.LoadPsdDocument(_psdPath);
                    InitBitmapLayerMap();
                    LoadPsdLayers();
                    UpdatePreview();
                }
            }
            
            if (string.IsNullOrEmpty(_psdPath)) return;

            _exportRoot = EditorGUILayout.TextField("Output Directory", _exportRoot);

            if (string.IsNullOrEmpty(_exportName)) {
                _exportName = Path.GetFileNameWithoutExtension(_psdPath);
            }
            _exportName = EditorGUILayout.TextField("Output Name", _exportName);

            _createAtlas = EditorGUILayout.Toggle("Create Atlas", _createAtlas);

            EditorGUILayout.BeginHorizontal();
            {
                EditorGUILayout.BeginVertical(GUILayout.Width(PreviewSize));
                {
                    GUILayout.Box(" ", GUILayout.Width(_previewSize.x), GUILayout.Height(_previewSize.y));
                    DrawPreview(GUILayoutUtility.GetLastRect());
                    DrawLayerInfos();
                }
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.BeginVertical(GUILayout.Width(PreviewSize));
                {
                    if (GUILayout.Button("Export", GUILayout.Width(130), GUILayout.Height(30))) {
                        _psdImporter.ImportPsd(_exportRoot, _exportName, _createAtlas);
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void OnDestroy() {
            _psdImporter.Dispose();
        }

        private void DrawPreview(Rect rect) {
            foreach (var texture in _previewTextures) {
                GUI.Label(rect, texture);
            }
            
            GUILayout.Label("Preview", "PreMiniLabel");
        }

        private void LoadPsdLayers() {
            _layerTextures.Clear();
            
            var psdLayers = new List<PSDLayer>();
            
            var document = _psdImporter.PsdDoc;
            ExtractLayerTask.Execute(psdLayers, document.Layers, true);
            
            var textureBuffLen = document.width * document.height;
            
            foreach (var psdLayer in psdLayers) {
                if (psdLayer.texture.Length != textureBuffLen) continue;
                
                var texture = new Texture2D(document.width, document.height);
                texture.SetPixels32(psdLayer.texture.ToArray());

                if (_bitmapLayerMap.TryGetValue(psdLayer.layerID, out var bitmapLayer)) {
                    var opacity = Convert.ToInt32(bitmapLayer.Opacity);
                    if (opacity < 255) {
                        var alpha = opacity / 255f;
                        for (var i = 0; i < texture.width; i++) {
                            for (var j = 0; j < texture.height; j++) {
                                var pixel = texture.GetPixel(i, j);
                                pixel.a *= alpha;
                                texture.SetPixel(i, j, pixel);
                            }
                        }
                    }
                }
                
                texture.Apply();
                
                _layerTextures.Add(psdLayer.layerID, texture);
            }
            
            _previewSize = new Vector2(document.width, document.height);
            var wr = PreviewSize / document.width;
            var hr = PreviewSize / document.height;
            var scale = Mathf.Min(wr, hr);
            
            _previewSize.x = document.width * scale;
            _previewSize.y = document.height * scale;
            
            foreach (var psdLayer in psdLayers) {
                psdLayer.Dispose();
            }
            psdLayers.Clear();
        }

        private void InitBitmapLayerMap() {
            _bitmapLayerMap.Clear();

            void AddLayer(BitmapLayer layer) {
                _bitmapLayerMap.Add(layer.LayerID, layer);

                foreach (var bitmapLayer in layer.ChildLayer) {
                    AddLayer(bitmapLayer);
                }
            }
            
            var layers = _psdImporter.PsdDoc.Layers;
            foreach (var bitmapLayer in layers) {
                AddLayer(bitmapLayer);
            }
        }

        private void UpdatePreview() {
            _previewTextures.Clear();

            void DrawTexture(BitmapLayer layer) {
                if (layer.Visible) {
                    if (layer.IsGroup) {
                        for (var i = layer.ChildLayer.Count - 1; i >= 0; i--) {
                            var childLayer = layer.ChildLayer[i];
                            DrawTexture(childLayer);
                        }
                    }
                    else {
                        var exist = _layerTextures.TryGetValue(layer.LayerID, out var texture);
                        if (exist) {
                            _previewTextures.Add(texture);
                        }
                    }
                }
            }
            
            var layers = _psdImporter.PsdDoc.Layers;
            for (var i = layers.Count - 1; i >= 0; i--) {
                var bitmapLayer = layers[i];
                DrawTexture(bitmapLayer);
            }
        }

        private void DrawLayerInfos() {
            if (_psdImporter == null) return;
            
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.Width(PreviewSize), GUILayout.Height(300));

            var document = _psdImporter.PsdDoc;
            if (document != null) {
                EditorGUILayout.BeginVertical();
                for (var i = document.Layers.Count - 1; i >= 0; i--) {
                    var layer = document.Layers[i];
                    DrawLayerInfo(layer, 0);
                }
                EditorGUILayout.EndVertical();
            }
            
            EditorGUILayout.EndScrollView();
        }

        private void DrawLayerInfo(BitmapLayer layer, int indent) {
            EditorGUILayout.BeginHorizontal();
            
            GUILayout.Space(20 * indent);

            var toggleIcon = layer.Visible ? _visibilityOnIcon : _visibilityOffIcon;
            if (GUILayout.Button(toggleIcon, _visibilityButtonStyle)) {
                layer.Visible = !layer.Visible;
                UpdatePreview();
            }
            
            if (layer.IsGroup) {
                var exist = _foldStates.TryGetValue(layer, out var foldout);
                if (!exist) {
                    foldout = false;
                }
                
                var foldoutStyle = layer.Visible ? EditorStyles.foldout : _disableFoldoutStyle;
                foldout = EditorGUILayout.Foldout(foldout, layer.Name, true, foldoutStyle);
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                if (foldout) {
                    for (var i = layer.ChildLayer.Count - 1; i >= 0; i--) {
                        var childLayer = layer.ChildLayer[i];
                        DrawLayerInfo(childLayer, indent + 1);
                    }
                }

                _foldStates[layer] = foldout;
            }
            else {
                GUILayout.Label(layer.Name, layer.Visible ? EditorStyles.label : _disableLabelStyle);
                
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}