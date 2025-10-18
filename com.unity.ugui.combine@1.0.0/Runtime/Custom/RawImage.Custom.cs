namespace UnityEngine.UI
{
    public partial class RawImage
    {
        internal override CombineType CombineType => CombineType.Image;

        public override Material material
        {
            get
            {
                UsedCombineMaterial = false;    

                if (m_Material != null)
                    return m_Material;

                if (TryCombine(texture))
                {
                    UsedCombineMaterial = true;
                    return m_CombineMaterial;
                }
               
                return defaultMaterial;
            }
            set
            {
                if (m_Material == value)
                    return;

                m_Material = value;
                SetMaterialDirty();
            }
        }
    }
}