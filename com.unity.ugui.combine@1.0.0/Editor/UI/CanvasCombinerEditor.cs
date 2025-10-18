using UnityEngine;
using UnityEngine.UI.CombineRender;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(CanvasCombiner), true)]
    [CanEditMultipleObjects]
    /// </summary>
    public class CanvasCombinerEditor : Editor
    {
        SerializedProperty m_UseCanvasCombineProp;

        CanvasCombiner _script;

        private void OnEnable()
        {
            _script = target as CanvasCombiner;
            m_UseCanvasCombineProp = serializedObject.FindProperty("_useCombiner");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(m_UseCanvasCombineProp, new GUIContent("Use Combine"));
            if (EditorGUI.EndChangeCheck())
            {
                _script.UseCombiner = m_UseCanvasCombineProp.boolValue;
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
