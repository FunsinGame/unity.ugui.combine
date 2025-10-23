using TMPro.CombineRender;
using UnityEngine;
using UnityEngine.UI;

namespace TMPro
{
    public partial class TMP_SubMeshUI
    {
        private TMP_ParamSlot _styleParamSlot;
        private TMP_ParamSlot _fontParamSlot;
        private int m_SubIndex = -1;

        internal bool isSupportUnifiedRendering;

        internal override CombineType CombineType => CombineType.TMP;

        internal bool TryCombineEx(Texture newTexture)
        {
            if (!IsActive())
            {
                return false;
            }

            if (!TMP_CombineRenderManager.IsOpen || !isSupportUnifiedRendering)
            {
                return false;
            }

            if (!TryCombine(newTexture))
            {
                return false;
            }

            if (
                !TMP_CombineRenderManager.AllocParamSlot(
                    m_sharedMaterial,
                    fontAsset,
                    ref _styleParamSlot,
                    ref _fontParamSlot,
                    out bool updateMesh
                )
            )
            {
                return false;
            }

            if (updateMesh)
            {
                UpdateMeshWithParameterIndex();
            }
            return true;
        }

        internal void UpdateMeshInfo()
        {
            if (textComponent.textInfo == null || textComponent.textInfo.meshInfo == null)
                return;

            if (m_SubIndex >= 1 && m_SubIndex < textComponent.textInfo.meshInfo.Length)
            {
                var meshInfo = textComponent.textInfo.meshInfo[m_SubIndex];
                if (meshInfo.uvs2 != null && meshInfo.mesh.vertexCount > 0)
                {
                    for (int v = 0; v < meshInfo.mesh.vertexCount; v++)
                    {
                        meshInfo.uvs2[v] = new Vector4((int)CombineType, m_ClipRectIndex, CombineMaterialIndex.slotIndex);
                        meshInfo.uvs1[v].z = _styleParamSlot.index;
                        meshInfo.uvs1[v].w = _fontParamSlot.index;
                    }
                }
            }
        }

        internal void UpdateMeshWithParameterIndex()
        {
            if (textComponent.textInfo == null || textComponent.textInfo.meshInfo == null)
                return;

            if (m_SubIndex >= 1 && m_SubIndex < textComponent.textInfo.meshInfo.Length)
            {
                var meshInfo = textComponent.textInfo.meshInfo[m_SubIndex];
                if (meshInfo.uvs1 != null && meshInfo.mesh.vertexCount > 0)
                {
                    for (int v = 0; v < meshInfo.mesh.vertexCount; v++)
                    {
                        meshInfo.uvs1[v].z = _styleParamSlot.index;
                        meshInfo.uvs1[v].w = _fontParamSlot.index;
                    }

                    textComponent.UpdateVertexData(m_SubIndex, TMP_VertexDataUpdateFlags.Uv1);
                }
            }
        }

        protected override void UpdateCombineVertexData()
        {
            if (textComponent.textInfo == null || textComponent.textInfo.meshInfo == null)
                return;

            if (m_SubIndex >= 1 && m_SubIndex < textComponent.textInfo.meshInfo.Length)
            {
                var meshInfo = textComponent.textInfo.meshInfo[m_SubIndex];
                if (meshInfo.uvs2 != null && meshInfo.mesh.vertexCount > 0)
                {
                    for (int v = 0; v < meshInfo.mesh.vertexCount; v++)
                    {
                        meshInfo.uvs2[v] = new Vector4((int)CombineType, m_ClipRectIndex, CombineMaterialIndex.slotIndex);
                    }

                    textComponent.UpdateVertexData(m_SubIndex, TMP_VertexDataUpdateFlags.Uv2);
                }
            }
        }
    }
}
