using System;
using UnityEngine.Pool;
using UnityEngine.Rendering;

namespace UnityEngine.UI.CombineRender
{
    internal sealed class MaterialEntry
    {
        public const int MAX_SLOT_COUNT = 8;
        internal static int[] slotPropertyIds = new int[]
        {
            //Shader.PropertyToID("_MainTex"), // UGUI会修改材质球中的这个属性，使用时需要注意
            Shader.PropertyToID("_MainTex1"),
            Shader.PropertyToID("_MainTex2"),
            Shader.PropertyToID("_MainTex3"),
            Shader.PropertyToID("_MainTex4"),
            Shader.PropertyToID("_MainTex5"),
            Shader.PropertyToID("_MainTex6"),
            Shader.PropertyToID("_MainTex7"),
            Shader.PropertyToID("_MainTex8"),
        };

        private TextureSlot[] _usedSlots = new TextureSlot[MAX_SLOT_COUNT];
        private int _usedSlotCount = 0;
        private Material _material;

        public GameObject GameObject { get; set; }

        public Material Material
        {
            get
            {
                if (!_material)
                {
                    Shader shader = CombineRenderManager.DefaultShader;
                    if (shader == null)
                    {
                        return null;
                    }

                    _material = new Material(shader);
                    _material.name = GameObject.name + "_" + Index.ToString();
                    _material.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
                }

                return _material;
            }
        }

        public TextureSlot[] Slots => _usedSlots;

        public int Index { get; internal set; }

        /// <summary>
        /// 更新材质的槽位纹理
        /// </summary>
        public void UpdateMaterialTexture(Material material)
        {
            if (material == null)
            {
                return;
            }

            var blackTexture = Texture2D.blackTexture;
            for (int i = 0; i < Slots.Length; i++)
            {
                TextureSlot slot = Slots[i];
                if (slot != null && slot.texture)
                {
                    material.SetTexture(slotPropertyIds[i], slot.texture);
                }
                else
                {
                    material.SetTexture(slotPropertyIds[i], blackTexture);
                }
            }
        }

        internal void ClearSlots()
        {
            for (int i = 0; i < Slots.Length; i++)
            {
                TextureSlot slot = Slots[i];
                if (null != slot)
                {
                    TextureSlot.pool.Release(slot);
                    Slots[i] = null;
                }
            }
            _usedSlotCount = 0;
        }

        internal void UpateUsedSlotCount()
        {
            _usedSlotCount = 0;
            for (int i = 0; i < _usedSlots.Length; i++)
            {
                if (_usedSlots[i] != null)
                {
                    _usedSlotCount++;
                }
            }
        }
    }
}
