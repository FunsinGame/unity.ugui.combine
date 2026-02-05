using System;
using System.Collections.Generic;
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
        /// 用于 StructuredBuffer 的数据结构，与 Shader 对齐
        /// </summary>
        internal struct StyleParamData
        {
            public Vector4 faceInfo; // x: 0, y: faceDilate, z: scaleRatioA, w: scaleRatioC
            public Vector4 faceColor; // row 1
            public Vector4 outlineColor; // row 2
            public Vector4 underlayColor; // row 3
            public Vector4 outlineInfo; // x: width, y: softness, z: openOutline, w: openUnderlay
            public Vector4 underlayInfo; // x: offsetX, y: offsetY, z: dilate, w: softness
        }

        private const int INITIAL_SETTING_COUNT = 256;

        private static readonly int ID_StyleParamBuffer = Shader.PropertyToID("_StyleParamBuffer");

        /// <summary>
        /// 默认参数分配
        /// </summary>
        private static readonly TMP_ParamSlot _defaultAlloc = new() { index = 0, isValid = true };

        private ComputeBuffer _styleParamBuffer;
        private StyleParamData[] _bufferData;

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

        public TMP_StyleSettingAllocator()
        {
            Initialize();
            AllocDefaultSetting();
        }

        /// <summary>
        /// 应用纹理更新
        /// </summary>
        public void UpdateBuffer()
        {
            if (_textureNeedsUpdate)
            {
                if (_styleParamBuffer != null)
                {
                    _styleParamBuffer.SetData(_bufferData);
                    Shader.SetGlobalBuffer(ID_StyleParamBuffer, _styleParamBuffer);
                }

                _textureNeedsUpdate = false;
            }
        }

        public void Dispose()
        {
            if (_styleParamBuffer != null)
            {
                _styleParamBuffer.Release();
                _styleParamBuffer = null;
            }

            _bufferData = null;
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
                Debug.LogError($"字体样式设置超过{INITIAL_SETTING_COUNT}个参数限制");
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

        private void Initialize()
        {
            // 初始化 ComputeBuffer (stride = 6 * 16 bytes = 96 bytes)
            _styleParamBuffer = new ComputeBuffer(INITIAL_SETTING_COUNT, 96, ComputeBufferType.Default);
            _bufferData = new StyleParamData[INITIAL_SETTING_COUNT];

            _allocatedSlots = new List<bool>(INITIAL_SETTING_COUNT);
            _refCounts = new List<int>(INITIAL_SETTING_COUNT);
            _slot2SettingHash = new List<int>(INITIAL_SETTING_COUNT);
            _freeSlots = new Queue<int>();

            // 初始化分配状态
            for (int i = 0; i < INITIAL_SETTING_COUNT; i++)
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
        /// 设置字体参数值到纹理和Buffer中
        /// </summary>
        private void WriteSettingData(TMP_ParamSlot slot, FontStyleSettings settings)
        {
            if (!slot.IsValid())
                return;

            int baseIndex = slot.index;
            // 2. 写入 Buffer Data (直接存储原始值，不压缩)
            if (baseIndex < _bufferData.Length)
            {
                _bufferData[baseIndex] = new StyleParamData
                {
                    faceInfo = new Vector4(0, settings.faceDilate, settings.scaleRatioA, settings.scaleRatioC),
                    faceColor = settings.faceColor,
                    outlineColor = settings.outlineColor,
                    underlayColor = settings.underlayColor,
                    outlineInfo = new Vector4(
                        settings.outlineWidth,
                        settings.outlineSoftness,
                        settings.openOutline ? 1 : 0,
                        settings.openUnderlay ? 1 : 0
                    ),
                    underlayInfo = new Vector4(
                        settings.underlayOffset.x,
                        settings.underlayOffset.y,
                        settings.underlayDilate,
                        settings.underlaySoftness
                    ),
                };
            }

            _textureNeedsUpdate = true;
        }
    }
}
