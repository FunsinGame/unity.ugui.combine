using System;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.TextMeshPro")]

namespace UnityEngine.UI.CombineRender
{
    public static class CombineRenderManager
    {
        internal static readonly string KEY_ClipRect = "UNIFIELD_CLIPRECT";

        private static Shader _defaultShader;
        private static bool _isOpen = true;
        private static bool _initialized = false;
        private static ClipRectAlloctor _clipRectAlloctor;

        internal static Shader DefaultShader => _defaultShader;

        public static bool IsOpen
        {
            get
            {
#if UNITY_EDITOR
                // 若未启用图集模式，则不启用运行时合批
                if (
                    UnityEditor.EditorSettings.spritePackerMode
                    != UnityEditor.SpritePackerMode.AlwaysOnAtlas
                )
                {
                    return false;
                }
#endif
                return _isOpen && Application.isPlaying;
            }
            set
            {
                if (_isOpen != value)
                {
                    _isOpen = value;
                    MarkAllGraphicsDirty();
                }
            }
        }

        static CombineRenderManager()
        {
            Initialize();
            Canvas.willRenderCanvases += OnPreRender;
        }

        private static void OnPreRender()
        {
            if (_clipRectAlloctor != null)
            {
                _clipRectAlloctor.OnPreRender();
            }
        }

        private static void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _defaultShader = Shader.Find("UI/Default-Unified");
            _clipRectAlloctor = new ClipRectAlloctor();

            _initialized = true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static int GetClipRectIndex(RectMask2D mask, Rect clipRect, Vector2Int softness)
        {
            return _clipRectAlloctor.GetClipRectIndex(mask, clipRect, softness);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FreeClipRect(RectMask2D mask)
        {
            _clipRectAlloctor?.FreeClipRect(mask);
        }

        private static void MarkAllGraphicsDirty()
        {
            GraphicRegistry.instance.MarkAllGraphicsDirty();
            ClipperRegistry.instance.ReForceClip();
        }

        public static string GetComponentPath(Transform transform)
        {
            if (transform == null)
            {
                return "";
            }
            var path = new System.Text.StringBuilder(transform.name);
            var parent = transform.parent;
            while (parent != null)
            {
                path.Insert(0, parent.name + "/");
                parent = parent.parent;
            }
            return path.ToString();
        }
    }
}
