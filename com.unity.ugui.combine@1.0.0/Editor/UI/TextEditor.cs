using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.UI
{
    [CustomEditor(typeof(Text), true)]
    [CanEditMultipleObjects]
    /// <summary>
    /// Custom Editor for the Text Component.
    /// Extend this class to write a custom editor for a component derived from Text.
    /// </summary>
    public class TextEditor : GraphicEditor
    {
        SerializedProperty m_Text;
        SerializedProperty m_FontData;
        SerializedProperty m_UseCanvasCombineProp;

        Text m_TextCmpt;

        protected override void OnEnable()
        {
            base.OnEnable();

            m_TextCmpt = target as Text;
            m_Text = serializedObject.FindProperty("m_Text");
            m_FontData = serializedObject.FindProperty("m_FontData");
            m_UseCanvasCombineProp = serializedObject.FindProperty("m_UseCombine");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(m_Text);
            EditorGUILayout.PropertyField(m_FontData);

            AppearanceControlsGUI();
            RaycastControlsGUI();
            MaskableControlsGUI();

            EditorGUILayout.PropertyField(m_UseCanvasCombineProp, new GUIContent("Use Canvas Combine"));

            serializedObject.ApplyModifiedProperties();
        }
    }
}
