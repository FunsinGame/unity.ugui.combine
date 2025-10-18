namespace UnityEngine.UI
{
    public abstract partial class MaskableGraphic
    {
        /// <summary>
        /// See IClippable.SetClipRect
        /// </summary>
        public virtual void SetClipRect(Rect clipRect, bool validRect, int clipIndex)
        {
            int preClipIndex = m_ClipRectIndex;
            m_ClipRectIndex = clipIndex;
            if (UsedCombineMaterial && CanvasCombiner.UseCombiner && clipIndex >= 0)
            {
                if (preClipIndex != m_ClipRectIndex)
                {
                    SetVerticesDirty();
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