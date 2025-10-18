using TMPro.CombineRender;
using UnityEngine;
using UnityEngine.UI;

namespace TMPro
{
    public partial class TextMeshProUGUI
    {
        private bool _isSupportUnifiedRendering = true;

        internal override CombineType CombineType => CombineType.TMP;

        public bool IsSupportUnifiedRendering
        {
            get { return _isSupportUnifiedRendering; }
        }

        private void CheckSupportUnifiedRendering()
        {
            _isSupportUnifiedRendering = m_sharedMaterial != null && m_sharedMaterial.shader != null
                    && m_sharedMaterial.shader.name == CombineRender.TMP_CombineRenderManager.SUPPORT_SHADER_NAME;
        }

        internal bool TryCombineEx(Texture newTexture)
        {
            if (!IsActive())
            {
                return false;
            }

            if (!TMP_CombineRenderManager.IsOpen || !_isSupportUnifiedRendering)
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
                    font,
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

        internal void UpdateMeshWithParameterIndex()
        {
            if (this.textInfo == null || this.textInfo.meshInfo == null)
                return;

            if (this.textInfo.meshInfo.Length > 0)
            {
                var meshInfo = this.textInfo.meshInfo[0];
                if (meshInfo.uvs1 != null && meshInfo.mesh.vertexCount > 0)
                {
                    for (int v = 0; v < meshInfo.mesh.vertexCount; v++)
                    {
                        meshInfo.uvs1[v].z = _styleParamSlot.index;
                        meshInfo.uvs1[v].w = _fontParamSlot.index;
                    }

                    if (IsActive())
                        this.UpdateVertexData(TMP_VertexDataUpdateFlags.Uv1);
                }
            }
        }

        protected override void UpdateCombineVertexData()
        {
            if (this.textInfo == null || this.textInfo.meshInfo == null)
                return;

            if (this.textInfo.meshInfo.Length > 0)
            {
                var meshInfo = this.textInfo.meshInfo[0];
                if (meshInfo.uvs2 != null && meshInfo.mesh.vertexCount > 0)
                {
                    for (int v = 0; v < meshInfo.mesh.vertexCount; v++)
                    {
                        meshInfo.uvs2[v] = new Vector4((int)CombineType, m_ClipRectIndex, CombineMaterialIndex.slotIndex);
                    }

                    if (IsActive())
                    {
                        this.UpdateVertexData(TMP_VertexDataUpdateFlags.Uv2);
                    }
                }
            }
        }

        public override void SetClipRect(Rect clipRect, bool validRect, int clipIndex)
        {
            int preClipIndex = m_ClipRectIndex;
            m_ClipRectIndex = clipIndex;

            if (UsedCombineMaterial && CanvasCombiner.UseCombiner && clipIndex >= 0)
            {
                if (preClipIndex != m_ClipRectIndex)
                {
                    UpdateCombineVertexData();
                }
            }
            else
            {
                if (validRect)
                    canvasRenderer.EnableRectClipping(clipRect);
                else
                    canvasRenderer.DisableRectClipping();
            }
        }
    }
}