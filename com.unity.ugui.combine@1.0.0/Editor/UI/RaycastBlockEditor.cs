using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(RaycastBlock), true)]
    [CanEditMultipleObjects]
    public class RaycastBlockEditor : GraphicEditor
    {
        public override void OnInspectorGUI() {
            GUILayout.Space(20);
        }
    }
}
