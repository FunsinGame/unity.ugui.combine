using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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

    internal class TMP_FontSettingAllocator : IDisposable
    {
        /// <summary>
        /// 初始纹理宽度
        /// </summary>
        private const int INITIAL_TEXTURE_WIDTH = 32;

        /// <summary>
        /// 初始纹理高度
        /// </summary>
        private const int INITIAL_TEXTURE_HEIGHT = 1;

        private static readonly int ID_FontParamTex = Shader.PropertyToID("_FontParamTex");

        private Texture2D _parameterTexture;
        private Color[] _textureData;
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

        public Texture2D Texture => _parameterTexture;

        public TMP_FontSettingAllocator()
        {
            InitializeTexture();
        }

        /// <summary>
        /// 应用纹理更新
        /// </summary>
        public void UpdateTexture()
        {
            if (_textureNeedsUpdate && _parameterTexture != null)
            {
                _parameterTexture.SetPixels(_textureData);
                _parameterTexture.Apply(false, false);
                _textureNeedsUpdate = false;
                Shader.SetGlobalTexture(ID_FontParamTex, _parameterTexture);
            }
        }

        public void Dispose()
        {
            if (_parameterTexture != null)
            {
                UnityEngine.Object.DestroyImmediate(_parameterTexture);
                _parameterTexture = null;
            }

            _textureData = null;
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
                Debug.LogError($"字体设置超过{INITIAL_TEXTURE_WIDTH}个参数限制");
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
            _parameterTexture = new Texture2D(INITIAL_TEXTURE_WIDTH, INITIAL_TEXTURE_HEIGHT, TextureFormat.RGBA32, false, true);
            _parameterTexture.name = "TMP_FontParameterTexture";
            _parameterTexture.filterMode = FilterMode.Point;
            _parameterTexture.wrapMode = TextureWrapMode.Clamp;

            _textureData = new Color[INITIAL_TEXTURE_WIDTH * INITIAL_TEXTURE_HEIGHT];
            _allocatedSlots = new List<bool>(INITIAL_TEXTURE_WIDTH);
            _slot2SettingHash = new List<int>(INITIAL_TEXTURE_WIDTH);
            _freeSlots = new Queue<int>();

            // 初始化分配状态
            for (int i = 0; i < INITIAL_TEXTURE_WIDTH; i++)
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
            SetTexelColor(baseIndex, new Color(settings.sdfScale / 255f, EncodeFloat(settings.weightNormal), EncodeFloat(settings.weightBold)));
            _textureNeedsUpdate = true;

            float EncodeFloat(float value)
            {
                // 将[-3, 3]转换到[0, 1]范围内
                return Mathf.Clamp01((value + 3f) / 6f);
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
