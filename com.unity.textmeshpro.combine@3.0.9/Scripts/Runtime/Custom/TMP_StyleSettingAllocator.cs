using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace TMPro.CombineRender
{
    /// <summary>
    /// 字体参数设置，包含所有可能的字体样式参数
    /// </summary>
    [System.Serializable]
    internal struct FontStyleSettings
    {
        public Color faceColor;
        public float faceDilate;

        public Color outlineColor;
        public float outlineWidth;
        public float outlineSoftness;

        public Color underlayColor;
        public Vector2 underlayOffset;
        public float underlaySoftness;
        public float underlayDilate;

        public float scaleRatioA;

        //public float scaleRatioB;
        public float scaleRatioC;

        public bool openOutline;
        public bool openUnderlay;
        //public bool openUnderlayInner;

        private int? _hashCode;

        public static readonly FontStyleSettings defaultSettings = new FontStyleSettings()
        {
            faceColor = Color.white,
            faceDilate = 0.0f,
            outlineColor = Color.black,
            outlineWidth = 0.0f,
            outlineSoftness = 0.0f,
            underlayColor = Color.clear,
            underlayOffset = Vector2.zero,
            underlaySoftness = 0.0f,
            underlayDilate = 0.0f,
            scaleRatioA = 1.0f,
            //scaleRatioB = 1.0f,
            scaleRatioC = 1.0f,
            openOutline = false,
            openUnderlay = false,
            //openUnderlayInner = false,
        };

        public bool Equals(in FontStyleSettings other)
        {
            return other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            if (_hashCode.HasValue)
                return _hashCode.Value;

            var hashCode = 1750498125;
            hashCode = hashCode * -1521134295 + faceColor.GetHashCode();
            hashCode = hashCode * -1521134295 + faceDilate.GetHashCode();
            hashCode = hashCode * -1521134295 + outlineColor.GetHashCode();
            hashCode = hashCode * -1521134295 + outlineWidth.GetHashCode();
            hashCode = hashCode * -1521134295 + underlayColor.GetHashCode();
            hashCode = hashCode * -1521134295 + underlayOffset.GetHashCode();
            hashCode = hashCode * -1521134295 + underlaySoftness.GetHashCode();
            hashCode = hashCode * -1521134295 + underlayDilate.GetHashCode();
            hashCode = hashCode * -1521134295 + outlineSoftness.GetHashCode();
            hashCode = hashCode * -1521134295 + scaleRatioA.GetHashCode();
            //hashCode = hashCode * -1521134295 + scaleRatioB.GetHashCode();
            hashCode = hashCode * -1521134295 + scaleRatioC.GetHashCode();
            hashCode = hashCode * -1521134295 + openOutline.GetHashCode();
            hashCode = hashCode * -1521134295 + openUnderlay.GetHashCode();
            //hashCode = hashCode * -1521134295 + openUnderlayInner.GetHashCode();

            _hashCode = hashCode;
            return hashCode;
        }
    }

    /// <summary>
    /// 参数纹理槽位分配记录
    /// </summary>
    public struct TMP_ParamSlot
    {
        public static readonly TMP_ParamSlot Invalid = new TMP_ParamSlot() { index = -1, isValid = false };

        public int index; // 在参数纹理中的索引
        public bool isValid;

        public bool IsValid()
        {
            return index >= 0 && isValid;
        }
    }

    /// <summary>
    /// TMP参数纹理管理器，负责分配和管理字体参数在纹理中的存储
    /// </summary>
    internal class TMP_StyleSettingAllocator : IDisposable
    {
        /// <summary>
        /// 初始纹理宽度
        /// </summary>
        private const int INITIAL_TEXTURE_WIDTH = 256;

        /// <summary>
        /// 初始纹理高度
        /// </summary>
        private const int INITIAL_TEXTURE_HEIGHT = 8;

        private static readonly int ID_StyleParamTex = Shader.PropertyToID("_StyleParamTex");

        private static readonly int[] _rowOffset = new int[INITIAL_TEXTURE_HEIGHT]
        {
            0,
            INITIAL_TEXTURE_WIDTH,
            INITIAL_TEXTURE_WIDTH * 2,
            INITIAL_TEXTURE_WIDTH * 3,
            INITIAL_TEXTURE_WIDTH * 4,
            INITIAL_TEXTURE_WIDTH * 5,
            INITIAL_TEXTURE_WIDTH * 6,
            INITIAL_TEXTURE_WIDTH * 7,
        };

        /// <summary>
        /// 默认参数分配
        /// </summary>
        private static readonly TMP_ParamSlot _defaultAlloc = new TMP_ParamSlot() { index = 0, isValid = true };

        private Texture2D _styleParameterTexture;
        private Color[] _textureData;
        private List<bool> _allocatedSlots;
        private List<int> _refCounts; // 每个槽位的引用计数
        private Queue<int> _freeSlots;
        private bool _textureNeedsUpdate;

        /// <summary>
        /// 槽位到材质参数hashcode的映射
        /// </summary>
        private List<int> _slot2SettingHash;

        /// <summary>
        /// 材质参数hashcode到槽位的映射
        /// </summary>
        private readonly Dictionary<int, int> _settingHash2Slot = new();

        public Texture2D Texture => _styleParameterTexture;

        public TMP_StyleSettingAllocator()
        {
            InitializeTexture();
            AllocDefaultSetting();
        }

        /// <summary>
        /// 应用纹理更新
        /// </summary>
        public void UpdateTexture()
        {
            if (_textureNeedsUpdate && _styleParameterTexture != null)
            {
                _styleParameterTexture.SetPixels(_textureData);
                _styleParameterTexture.Apply(false, false);
                _textureNeedsUpdate = false;
                Shader.SetGlobalTexture(ID_StyleParamTex, _styleParameterTexture);
            }
        }

        public void Dispose()
        {
            if (_styleParameterTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_styleParameterTexture);
                _styleParameterTexture = null;
            }

            _textureData = null;
            _allocatedSlots?.Clear();
            _refCounts?.Clear();
            _freeSlots?.Clear();
        }

        public int FindSettingHash(TMP_ParamSlot slot)
        {
            if (!slot.IsValid() || slot.index >= _slot2SettingHash.Count)
                return 0;

            return _slot2SettingHash[slot.index];
        }

        /// <summary>
        /// 分配一个新的字体参数槽位
        /// </summary>
        public TMP_ParamSlot AllocSlot(in FontStyleSettings settings)
        {
            int hashcode = settings.GetHashCode();
            int slotIndex = -1;
            if (_settingHash2Slot.TryGetValue(hashcode, out slotIndex) && _allocatedSlots[slotIndex])
            {
                _refCounts[slotIndex]++;
                return new TMP_ParamSlot() { index = slotIndex, isValid = true };
            }

            if (_freeSlots.Count > 0)
            {
                slotIndex = _freeSlots.Dequeue();
            }
            else
            {
                Debug.LogError($"字体样式设置超过{INITIAL_TEXTURE_WIDTH}个参数限制");
                return TMP_ParamSlot.Invalid;
            }

            var slot = new TMP_ParamSlot() { index = slotIndex, isValid = true };
            _allocatedSlots[slotIndex] = true;
            _refCounts[slotIndex] = 1;
            _slot2SettingHash[slotIndex] = hashcode;
            _settingHash2Slot.Add(hashcode, slotIndex);

            WriteSettingData(slot, settings);
            return slot;
        }

        /// <summary>
        /// 减少槽位引用计数，当引用计数为0时自动释放
        /// </summary>
        public void FreeSlot(ref TMP_ParamSlot slot)
        {
            // 无效或默认槽位不处理
            if (!slot.IsValid() || slot.index == 0)
                return;

            if (slot.index >= _refCounts.Count || !_allocatedSlots[slot.index])
                return;

            _refCounts[slot.index]--;
            slot.isValid = false;

            // 当引用计数为0时释放槽位
            if (_refCounts[slot.index] <= 0)
            {
                _allocatedSlots[slot.index] = false;
                _refCounts[slot.index] = 0;

                int hashCode = _slot2SettingHash[slot.index];
                _slot2SettingHash[slot.index] = 0;
                _settingHash2Slot.Remove(hashCode);

                _freeSlots.Enqueue(slot.index);
            }
        }

        private void InitializeTexture()
        {
            _styleParameterTexture = new Texture2D(INITIAL_TEXTURE_WIDTH, INITIAL_TEXTURE_HEIGHT, TextureFormat.RGBA32, false, true);
            _styleParameterTexture.name = "TMP_StyleParameterTexture";
            _styleParameterTexture.filterMode = FilterMode.Point;
            _styleParameterTexture.wrapMode = TextureWrapMode.Clamp;

            _textureData = new Color[INITIAL_TEXTURE_WIDTH * INITIAL_TEXTURE_HEIGHT];
            _allocatedSlots = new List<bool>(INITIAL_TEXTURE_WIDTH);
            _refCounts = new List<int>(INITIAL_TEXTURE_WIDTH);
            _slot2SettingHash = new List<int>(INITIAL_TEXTURE_WIDTH);
            _freeSlots = new Queue<int>();

            // 初始化分配状态
            for (int i = 0; i < INITIAL_TEXTURE_WIDTH; i++)
            {
                _allocatedSlots.Add(false);
                _refCounts.Add(0);
                _slot2SettingHash.Add(0);

                if (i > 0) // 保留索引0给默认设置
                    _freeSlots.Enqueue(i);
            }

            _textureNeedsUpdate = false;
        }

        private void AllocDefaultSetting()
        {
            // 在索引0处设置默认字体参数
            int index = 0;
            _allocatedSlots[index] = true;
            _refCounts[index] = int.MaxValue;
            _slot2SettingHash[index] = FontStyleSettings.defaultSettings.GetHashCode();
            _settingHash2Slot.Add(FontStyleSettings.defaultSettings.GetHashCode(), index);
            WriteSettingData(_defaultAlloc, FontStyleSettings.defaultSettings);
        }

        /// <summary>
        /// 设置字体参数值到纹理中
        /// </summary>
        private void WriteSettingData(TMP_ParamSlot slot, FontStyleSettings settings)
        {
            if (!slot.IsValid())
                return;

            int baseIndex = slot.index;

            SetTexelColor(baseIndex + _rowOffset[0], new Color(0, ClampTo01(settings.faceDilate), settings.scaleRatioA, settings.scaleRatioC));
            SetTexelColor(baseIndex + _rowOffset[1], settings.faceColor);
            SetTexelColor(baseIndex + _rowOffset[2], settings.outlineColor);
            SetTexelColor(baseIndex + _rowOffset[3], settings.underlayColor);
            SetTexelColor(baseIndex + _rowOffset[4], new Color(settings.outlineWidth, settings.outlineSoftness, settings.openOutline ? 1 : 0, settings.openUnderlay ? 1 : 0));
            SetTexelColor(
                baseIndex + _rowOffset[5],
                new Color(
                    ClampTo01(settings.underlayOffset.x),
                    ClampTo01(settings.underlayOffset.y),
                    ClampTo01(settings.underlayDilate),
                    settings.underlaySoftness
                )
            );

            _textureNeedsUpdate = true;

            // 将-1~1范围的值转换为0~1范围
            float ClampTo01(float v)
            {
                return Mathf.Clamp01((v + 1f) * 0.5f);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetTexelColor(int index, Color color)
        {
            if (index >= 0 && index < _textureData.Length)
            {
                _textureData[index] = color;
            }
        }
    }
}
