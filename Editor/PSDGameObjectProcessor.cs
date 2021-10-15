using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.U2D.PSD
{
    public class PSDGameObjectProcessor
    {
        private Vector2Int _designSize = Vector2Int.zero;

        public Vector2Int DesignSize {
            get {
                if (_designSize == Vector2Int.zero) {
                    _designSize = OnDesignSize();
                }
                return _designSize;
            }
        }

        protected GameObject _root;
        
        public virtual Vector2Int OnDesignSize() {
            return new Vector2Int(2436, 1125);
        }

        public virtual void OnPSDGameObjectCreated(GameObject root) {
            _root = root;

            var canvasGroup = _root.GetComponent<CanvasGroup>();
            if (canvasGroup == null) {
                _root.AddComponent<CanvasGroup>();
            }
            
            // AddCanvas();
            
            UpdateNormalObjectSize(_root.transform);
            
            _root.transform.position = Vector3.zero;
            
            UpdateAllComponents(_root.transform, true);
        }

        protected void AddCanvas() {
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var canvasScaler = _root.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            canvasScaler.referenceResolution = OnDesignSize();
            _root.AddComponent<CanvasGroup>();
        }

        private void UpdateNormalObjectSize(Transform transform) {
            var childPositions = new List<Tuple<Transform, Vector3>>();
            
            for (var i = 0; i < transform.childCount; ++i) {
                var child = transform.GetChild(i);
                UpdateNormalObjectSize(child);
                childPositions.Add(new Tuple<Transform, Vector3>(child, child.position));
            }

            if (!IsImageOrSprite(transform)) {
                var rt = transform.GetComponent<RectTransform>();
                var calcRect = GetRect(rt.transform);
                if (calcRect.HasValue) {
                    rt.position = calcRect.Value.center;
                    rt.sizeDelta = calcRect.Value.size;
                    
                    foreach (var child in childPositions) {
                        child.Item1.position = child.Item2;
                    }
                }
            }
        }
        
        protected Rect? GetRect(Transform transform) {
            Rect? rect = null;
            
            for (var i = 0; i < transform.transform.childCount; ++i) {
                var child = transform.GetChild(i);
                var rt = child.GetComponent<RectTransform>();
                if (rt == null) continue;
                
                var childRect = Rect.MinMaxRect(rt.offsetMin.x, rt.offsetMin.y, rt.offsetMax.x, rt.offsetMax.y);
                
                if (rect == null) {
                    rect = childRect;
                } else {
                    rect = Rect.MinMaxRect(
                        Mathf.Min(rect.Value.xMin, childRect.xMin),
                        Mathf.Min(rect.Value.yMin, childRect.yMin),
                        Mathf.Max(rect.Value.xMax, childRect.xMax),
                        Mathf.Max(rect.Value.yMax, childRect.yMax));
                }
            }
            
            return rect;
        }

        private bool IsImageOrSprite(Transform transform) {
            if (transform.GetComponent<Image>() != null) {
                return true;
            }

            if (transform.GetComponent<SpriteRenderer>() != null) {
                return true;
            }

            return false;
        }

        private void UpdateAllComponents(Transform transform, bool isRoot = false) {
            for (var i = 0; i < transform.childCount; ++i) {
                var child = transform.GetChild(i);
                UpdateAllComponents(child, false);
            }

            var name = transform.gameObject.name;
            var rt = transform.GetComponent<RectTransform>();
            
            if (isRoot && name.EndsWith("_panel")) {
                rt.anchorMin = new Vector2(0, 0);
                rt.anchorMax = new Vector2(1, 1);
                SetRectTransformRect(rt, 0, 0, 0, 0);
            }
            else if (name.Equals("bg")) {
                var aspectRatioFitter = transform.gameObject.AddComponent<AspectRatioFitter>();
                aspectRatioFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                aspectRatioFitter.aspectRatio = (float)DesignSize.x / (float)DesignSize.y;
            }
        }
        
        private void SetRectTransformLeft(RectTransform rt, float left) {
            rt.offsetMin = new Vector2(left, rt.offsetMin.y);
        }
 
        private void SetRectTransformRight(RectTransform rt, float right) {
            rt.offsetMax = new Vector2(-right, rt.offsetMax.y);
        }
 
        private void SetRectTransformTop(RectTransform rt, float top) {
            rt.offsetMax = new Vector2(rt.offsetMax.x, -top);
        }
 
        private void SetRectTransformBottom(RectTransform rt, float bottom) {
            rt.offsetMin = new Vector2(rt.offsetMin.x, bottom);
        }

        private void SetRectTransformRect(RectTransform rt, float left, float right, float top, float bottom) {
            SetRectTransformLeft(rt, left);
            SetRectTransformRight(rt, right);
            SetRectTransformTop(rt, top);
            SetRectTransformBottom(rt, bottom);
        }
    }
}