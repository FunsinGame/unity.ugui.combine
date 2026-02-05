using System;
using System.Collections.Generic;
using UnityEngine;

namespace TMPro.CombineRender
{
    internal struct FontSettings
    {
        public float sdfScale;
        public float weightNormal;
        public float weightBold;

        private int? _hashCode;

        public bool Equals(in FontStyleSettings other)
        {
            return other.GetHashCode() == GetHashCode();
        }

        public override int GetHashCode()
        {
            if (_hashCode.HasValue)
                return _hashCode.Value;

            var hashCode = 1750498125;
            hashCode = hashCode * -1521134295 + sdfScale.GetHashCode();
            hashCode = hashCode * -1521134295 + weightNormal.GetHashCode();
            hashCode = hashCode * -1521134295 + weightBold.GetHashCode();
            _hashCode = hashCode;
            return hashCode;
        }
    }

    internal struct FontParamData
    {
        public float gradientScale;
        public float weightNormal;
        public float weightBold;
        public float padding; // Pad to 16 bytes
    }

    internal class TMP_FontSettingAllocator : IDisposable
    {
        private const int INITIAL_SETTING_COUNT = 32;

        private ComputeBuffer _fontParamBuffer;
        private FontParamData[] _bufferData;

        private List<bool> _allocatedSlots;
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

        public TMP_FontSettingAllocator()
        {
            InitializeTexture();
        }

        /// <summary>
        /// 应用纹理更新
        /// </summary>
        public void UpdateBuffer()
        {
            if (_textureNeedsUpdate)
            {
                if (_fontParamBuffer != null)
                {
                    _fontParamBuffer.SetData(_bufferData);
                    Shader.SetGlobalBuffer("_FontParamBuffer", _fontParamBuffer);
                }

                _textureNeedsUpdate = false;
            }
        }

        public void Dispose()
        {
            if (_fontParamBuffer != null)
            {
                _fontParamBuffer.Release();
                _fontParamBuffer = null;
            }

            _bufferData = null;
            _allocatedSlots?.Clear();
            _freeSlots?.Clear();
            _slot2SettingHash?.Clear();
            _settingHash2Slot?.Clear();
            _textureNeedsUpdate = false;
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
        public TMP_ParamSlot AllocSlot(in FontSettings settings)
        {
            int hashcode = settings.GetHashCode();
            int slotIndex = -1;
            if (_settingHash2Slot.TryGetValue(hashcode, out slotIndex) && _allocatedSlots[slotIndex])
            {
                return new TMP_ParamSlot() { index = slotIndex, isValid = true };
            }

            if (_freeSlots.Count > 0)
            {
                slotIndex = _freeSlots.Dequeue();
            }
            else
            {
                Debug.LogError($"字体设置超过{INITIAL_SETTING_COUNT}个参数限制");
                return TMP_ParamSlot.Invalid;
            }

            var slot = new TMP_ParamSlot() { index = slotIndex, isValid = true };
            _allocatedSlots[slotIndex] = true;
            _slot2SettingHash[slotIndex] = hashcode;
            _settingHash2Slot.Add(hashcode, slotIndex);

            WriteSettingData(slot, settings);
            return slot;
        }

        private void InitializeTexture()
        {
            _bufferData = new FontParamData[INITIAL_SETTING_COUNT];
            _fontParamBuffer = new ComputeBuffer(INITIAL_SETTING_COUNT, 16); // 16 bytes stride

            _allocatedSlots = new List<bool>(INITIAL_SETTING_COUNT);
            _slot2SettingHash = new List<int>(INITIAL_SETTING_COUNT);
            _freeSlots = new Queue<int>();

            // 初始化分配状态
            for (int i = 0; i < INITIAL_SETTING_COUNT; i++)
            {
                _allocatedSlots.Add(false);
                _slot2SettingHash.Add(0);
                _freeSlots.Enqueue(i);
            }

            _textureNeedsUpdate = false;
        }

        /// <summary>
        /// 设置字体参数值到纹理中
        /// </summary>
        private void WriteSettingData(TMP_ParamSlot slot, FontSettings settings)
        {
            if (!slot.IsValid())
                return;

            int baseIndex = slot.index;
            if (baseIndex < _bufferData.Length)
            {
                _bufferData[baseIndex] = new FontParamData
                {
                    gradientScale = settings.sdfScale,
                    weightNormal = settings.weightNormal,
                    weightBold = settings.weightBold,
                    padding = 0,
                };
            }

            _textureNeedsUpdate = true;
        }
    }
}
