using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI.CombineRender;

namespace TMPro.CombineRender
{
    public static class TMP_CombineRenderManager
    {
        internal const string SUPPORT_SHADER_NAME = "TextMeshPro/Mobile/Distance Field";

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static bool OpenCombine = true;
#else
        public static bool OpenCombine = true;
#endif
        public static bool IsOpen => UnityEngine.UI.CombineRender.CombineRenderManager.IsOpen && OpenCombine;
        private static TMP_StyleSettingAllocator _styleSettingAllocator;
        private static TMP_FontSettingAllocator _fontSettingAllocator;

        static TMP_CombineRenderManager()
        {
            Initialize();
            Canvas.willRenderCanvases += OnPreRender;
        }

        private static void Initialize()
        {
            if (_styleSettingAllocator == null)
            {
                _styleSettingAllocator = new TMP_StyleSettingAllocator();
            }

            if (_fontSettingAllocator == null)
            {
                _fontSettingAllocator = new TMP_FontSettingAllocator();
            }
        }

        private static void OnPreRender()
        {
            if (_styleSettingAllocator != null)
            {
                _styleSettingAllocator.UpdateBuffer();
            }

            if (_fontSettingAllocator != null)
            {
                _fontSettingAllocator.UpdateBuffer();
            }
        }

        internal static bool AllocParamSlot(
            [NotNull] Material renderMat,
            [NotNull] TMP_FontAsset fontAsset,
            ref TMP_ParamSlot styleSlot,
            ref TMP_ParamSlot fontSlot,
            out bool updateMesh
        )
        {
            updateMesh = false;
            if (styleSlot.IsValid())
            {
                var settingHash = _styleSettingAllocator.FindSettingHash(styleSlot);
                var newSetting = ExtractStyleSettings(renderMat);
                if (settingHash != newSetting.GetHashCode())
                {
                    FreeSlot(ref styleSlot);
                    styleSlot = _styleSettingAllocator.AllocSlot(in newSetting);
                    updateMesh = true;
                }
            }
            else
            {
                var newSetting = ExtractStyleSettings(renderMat);
                styleSlot = _styleSettingAllocator.AllocSlot(in newSetting);
                updateMesh = true;
            }

            if (!styleSlot.IsValid())
            {
                return false;
            }

            if (fontSlot.IsValid())
            {
                var settingHash = _fontSettingAllocator.FindSettingHash(fontSlot);
                var newSetting = ExtractFontSettings(fontAsset);
                if (settingHash != newSetting.GetHashCode())
                {
                    fontSlot = _fontSettingAllocator.AllocSlot(in newSetting);
                    updateMesh = true;
                }
            }
            else
            {
                var fontSetting = ExtractFontSettings(fontAsset);
                fontSlot = _fontSettingAllocator.AllocSlot(in fontSetting);
                updateMesh = true;
            }

            if (!fontSlot.IsValid())
            {
                _styleSettingAllocator.FreeSlot(ref styleSlot);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 释放参数槽位
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void FreeSlot(ref TMP_ParamSlot slot)
        {
            _styleSettingAllocator?.FreeSlot(ref slot);
        }

        /// <summary>
        /// 提取字体设置
        /// </summary>
        internal static FontSettings ExtractFontSettings(TMP_FontAsset fontAsset)
        {
            return new FontSettings
            {
                sdfScale = fontAsset.atlasPadding + 1,
                weightNormal = fontAsset.normalStyle,
                weightBold = fontAsset.boldStyle,
            };
        }

        /// <summary>
        /// 提取样式设置
        /// </summary>
        internal static FontStyleSettings ExtractStyleSettings(Material fontMaterial)
        {
            var material = fontMaterial;

            return new FontStyleSettings
            {
                faceColor = GetMaterialColor(material, ShaderUtilities.ID_FaceColor, Color.white),
                faceDilate = GetMaterialFloat(material, ShaderUtilities.ID_FaceDilate, 0f),

                outlineColor = GetMaterialColor(
                    material,
                    ShaderUtilities.ID_OutlineColor,
                    Color.black
                ),
                outlineWidth = GetMaterialFloat(material, ShaderUtilities.ID_OutlineWidth, 0),
                outlineSoftness = GetMaterialFloat(
                    material,
                    ShaderUtilities.ID_OutlineSoftness,
                    0f
                ),

                underlayColor = GetMaterialColor(
                    material,
                    ShaderUtilities.ID_UnderlayColor,
                    Color.clear
                ),
                underlayOffset = new Vector2(
                    GetMaterialFloat(material, ShaderUtilities.ID_UnderlayOffsetX, 0f),
                    GetMaterialFloat(material, ShaderUtilities.ID_UnderlayOffsetY, 0f)
                ),
                underlaySoftness = GetMaterialFloat(
                    material,
                    ShaderUtilities.ID_UnderlaySoftness,
                    0f
                ),
                underlayDilate = GetMaterialFloat(material, ShaderUtilities.ID_UnderlayDilate, 0f),

                scaleRatioA = GetMaterialFloat(material, ShaderUtilities.ID_ScaleRatio_A, 1.0f),
                //scaleRatioB = GetMaterialFloat(material, ShaderUtilities.ID_ScaleRatio_B, 1.0f),
                scaleRatioC = GetMaterialFloat(material, ShaderUtilities.ID_ScaleRatio_C, 1.0f),

                openOutline = GetMaterialDefined(material, "OUTLINE_ON"),
                openUnderlay = GetMaterialDefined(material, "UNDERLAY_ON"),
                //openUnderlayInner = GetMaterialDefined(material, "UNDERLAY_INNER"),
            };
        }

        private static Color GetMaterialColor(Material material, int propertyID, Color defaultValue)
        {
            if (material == null || !material.HasProperty(propertyID))
            {
                return defaultValue;
            }

            return material.GetColor(propertyID);
        }

        private static float GetMaterialFloat(Material material, int propertyID, float defaultValue)
        {
            if (material == null || !material.HasProperty(propertyID))
            {
                return defaultValue;
            }

            return material.GetFloat(propertyID);
        }

        private static bool GetMaterialDefined(Material material, string keyword)
        {
            if (material == null)
            {
                return false;
            }

            return material.IsKeywordEnabled(keyword);
        }
    }
}
