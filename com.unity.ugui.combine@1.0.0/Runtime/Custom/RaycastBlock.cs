using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEngine.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public class RaycastBlock : Graphic
    {
        protected RaycastBlock()
        {
            useLegacyMeshGeneration = false;
            m_UseCombine = false;
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
        }
    }
}
