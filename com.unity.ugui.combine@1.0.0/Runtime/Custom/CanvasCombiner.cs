using System;
using System.Collections.Generic;

namespace UnityEngine.UI.CombineRender
{
    internal struct MaterialIndex : IEquatable<MaterialIndex>
    {
        public int matIndex;
        public int slotIndex;

        public bool IsValid()
        {
            return matIndex >= 0 && slotIndex >= 0;
        }

        public static bool operator ==(MaterialIndex a, MaterialIndex b)
        {
            return a.matIndex == b.matIndex && a.slotIndex == b.slotIndex;
        }

        public static bool operator !=(MaterialIndex a, MaterialIndex b)
        {
            return a.matIndex != b.matIndex || a.slotIndex != b.slotIndex;
        }

        public override bool Equals(object obj)
        {
            return obj is MaterialIndex other && Equals(other);
        }

        public bool Equals(MaterialIndex other)
        {
            return matIndex == other.matIndex && slotIndex == other.slotIndex;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (matIndex * 397) ^ slotIndex;
            }
        }
    }
    
    [RequireComponent(typeof(Canvas))]
    public partial class CanvasCombiner : MonoBehaviour
    {
        internal const int MAX_MATERIAL_COUNT = 4;
        internal static readonly MaterialIndex InvalidIndex = new() { matIndex = -1, slotIndex = -1 };

        [SerializeField]
        private bool _useCombiner = true;
        private List<MaterialEntry> _matEntries = new();
        private Canvas _canvas;

        public bool UseCombiner
        {
            get => _useCombiner;
            set
            {
                if (_useCombiner != value)
                {
                    _useCombiner = value;
                    if (_canvas != null)
                    {
                        GraphicRegistry.instance.MarkGraphicsDirtyByCanvas(_canvas);
                        var clippers = _canvas.GetComponentsInChildren<IClipper>();
                        foreach (var item in clippers)
                        {
                            item.SetForceClip();
                        }
                    }
                }
            }
        }

        private void Awake()
        {
            _canvas = GetComponent<Canvas>();

#if UNITY_EDITOR
            CanvasRenderer.onRequestRebuild += OnRequestRebuild;
#endif
        }

        private void OnDestroy()
        {
#if UNITY_EDITOR
            CanvasRenderer.onRequestRebuild -= OnRequestRebuild;
#endif

            for (int matIndex = 0; matIndex < _matEntries.Count; matIndex++)
            {
                MaterialEntry materialEntry = _matEntries[matIndex];
                materialEntry.ClearSlots();

                if (materialEntry.Material != null)
                {
                    GameObject.Destroy(materialEntry.Material);
                }
            }
            _matEntries.Clear();
        }

        /// <summary>
        /// 为texture分配材质
        /// </summary>
        internal MaterialIndex AllocMaterial(Texture texture)
        {
            if (texture == null)
            {
                return InvalidIndex;
            }

            int freeSlotIndex = -1;
            int freeMatIndex = -1;
            for (int matIndex = _matEntries.Count - 1; matIndex >= 0; matIndex--)
            {
                MaterialEntry matEntry = _matEntries[matIndex];
                for (int slotIndex = 0; slotIndex < matEntry.Slots.Length; slotIndex++)
                {
                    TextureSlot slot = matEntry.Slots[slotIndex];
                    if (slot == null)
                    {
                        freeMatIndex = matIndex;
                        freeSlotIndex = slotIndex;
                        break;
                    }
                    else if (slot.texture == texture)
                    {
                        slot.refCount++;
                        return new MaterialIndex() { matIndex = matIndex, slotIndex = slotIndex };
                    }
                }
            }

            if (freeMatIndex < 0 && _matEntries.Count >= MAX_MATERIAL_COUNT)
            {
                return InvalidIndex;
            }

            {
                MaterialEntry materialEntry;
                if (freeMatIndex < 0)
                {
                    materialEntry = new MaterialEntry() { GameObject = this.gameObject };
                    materialEntry.Index = _matEntries.Count;

                    freeMatIndex = materialEntry.Index;
                    _matEntries.Add(materialEntry);
                }
                else
                {
                    materialEntry = _matEntries[freeMatIndex];
                }

                if (freeSlotIndex < 0)
                {
                    freeSlotIndex = 0;
                }

                TextureSlot slot = TextureSlot.pool.Get();
                slot.texture = texture;
                slot.refCount = 1;
                materialEntry.Slots[freeSlotIndex] = slot;
                materialEntry.UpateUsedSlotCount();
                materialEntry.UpdateMaterialTexture(materialEntry.Material);
            }

            return new MaterialIndex() { matIndex = freeMatIndex, slotIndex = freeSlotIndex };
        }

        /// <summary>
        /// 释放材质
        /// </summary>
        internal void FreeMaterial(MaterialIndex materialIndex)
        {
            if (!materialIndex.IsValid())
            {
                return;
            }

            if (materialIndex.matIndex >= 0 && materialIndex.matIndex < _matEntries.Count)
            {
                MaterialEntry matEntry = _matEntries[materialIndex.matIndex];
                if (materialIndex.slotIndex >= 0 && materialIndex.slotIndex < matEntry.Slots.Length)
                {
                    var slot = matEntry.Slots[materialIndex.slotIndex];
                    if (slot != null)
                    {
                        slot.refCount--;

                        if (slot.refCount <= 0)
                        {
                            TextureSlot.pool.Release(slot);
                            matEntry.Slots[materialIndex.slotIndex] = null;
                            matEntry.UpateUsedSlotCount();
                        }
                    }
                }
            }
        }

        internal Material GetMaterial(int matIndex)
        {
            if (matIndex >= 0 && matIndex < _matEntries.Count)
            {
                return _matEntries[matIndex].Material;
            }
            return null;
        }

        internal void UpdateMaterialTexture(int matIndex, Material usedMat)
        {
            if (matIndex >= 0 && matIndex < _matEntries.Count)
            {
                var entry = _matEntries[matIndex];
                if (entry != null)
                {
                    entry.UpdateMaterialTexture(usedMat);
                }
            }
        }

        internal bool IsBuildinSprite(Sprite sprite)
        {
            string spriteName = sprite.texture.name;
            if (spriteName == "UISprite" || spriteName == "UIMask" || spriteName == "Checkmark")
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal void UpdateStencilMaterial(MaterialIndex materialIndex, Material stencilMat)
        {
            if (materialIndex != InvalidIndex && stencilMat != null
                && stencilMat.shader == CombineRenderManager.DefaultShader)
            {
                if (materialIndex.matIndex >= 0 && materialIndex.matIndex < _matEntries.Count)
                {
                    MaterialEntry matEntry = _matEntries[materialIndex.matIndex];
                    matEntry.UpdateMaterialTexture(stencilMat);
                }
            }
        }

#if UNITY_EDITOR
        private void OnRequestRebuild()
        {
            foreach (var item in _matEntries)
            {
                if (item != null && item.Material != null)
                {
                    item.UpdateMaterialTexture(item.Material);
                }
            }
        }
#endif
    }
}