using UnityEngine.UI.CombineRender;

namespace UnityEngine.UI
{
    public enum CombineType
    {
        None = 0,
        Image = 1,
        Text = 2,
        TMP = 3,
    }

    public abstract partial class Graphic
    {
        [SerializeField]
        protected bool m_UseCombine = true;

        protected int m_ClipRectIndex = 0;
        protected Material m_CombineMaterial;
        protected Texture m_LastCombineTexture = null;
        protected CanvasCombiner m_Combiner;

        internal MaterialIndex CombineMaterialIndex = CanvasCombiner.InvalidIndex;

        internal bool UsedCombineMaterial { get; set; }
        internal virtual CombineType CombineType => CombineType.None;

        public bool UseCanvasCombine
        {
            get => m_UseCombine;
            set
            {
                if (value != m_UseCombine)
                {
                    m_UseCombine = value;
                    SetAllDirty();
                }
            }
        }

        public CanvasCombiner CanvasCombiner
        {
            get
            {
                if (m_Combiner == null)
                {
                    CacheCanvasCombiner();
                }
                return m_Combiner;
            }
        }

        protected void CacheCanvasCombiner()
        {
            if (m_UseCombine == false || CombineType == CombineType.None || CombineRenderManager.IsOpen == false || canvas == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(canvas.gameObject) != null)
            {
                return;
            }
#endif
            var go = canvas.gameObject;
            m_Combiner = go.GetComponent<CanvasCombiner>();
            if (m_Combiner == null)
            {
                m_Combiner = go.AddComponent<CanvasCombiner>();
            }
        }

        protected virtual bool TryCombine(Texture newTexture)
        {
            if (
                IsActive() == false
                || CombineType == CombineType.None
                || m_UseCombine == false
                || CombineRenderManager.IsOpen == false
            )
            {
                return false;
            }

            var combiner = CanvasCombiner;
            if (combiner == null || combiner.UseCombiner == false)
            {
                return false;
            }

            if (newTexture == m_LastCombineTexture && m_CombineMaterial != null)
            {
                return true;
            }

            FreeCombine();

            if (newTexture != null)
            {
                CombineMaterialIndex = combiner.AllocMaterial(newTexture);
                if (CombineMaterialIndex != CanvasCombiner.InvalidIndex)
                {
                    m_CombineMaterial = combiner.GetMaterial(CombineMaterialIndex.matIndex);
                    m_LastCombineTexture = newTexture;
                    UpdateCombineVertexData();
                    return true;
                }
            }
            return false;
        }

        internal void FreeCombine()
        {
            CanvasCombiner?.FreeMaterial(CombineMaterialIndex);
            ClearData();
        }

        protected void ClearData()
        {
            CombineMaterialIndex = CanvasCombiner.InvalidIndex;
            m_CombineMaterial = null;
            m_LastCombineTexture = null;
            UsedCombineMaterial = false;
        }

        protected virtual void UpdateCombineVertexData()
        {
            if (!m_VertsDirty)
            {
                UpdateGeometry();
            }
        }
    }
}
