using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

namespace UnityEditor.UI
{

	/// <summary>
	/// UIEffect editor.
	/// </summary>
	[CustomEditor(typeof(UIEffectCapturedImage))]
	[CanEditMultipleObjects]
	public class UIEffectCapturedImageEditor : RawImageEditor
	{

		/// <summary>
		/// Implement this function to make a custom inspector.
		/// </summary>
		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			//================
			// Basic properties.
			//================
			EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Texture"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Color"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("m_RaycastTarget"));

			//================
			// Capturing effect.
			//================
			GUILayout.Space(10);
			EditorGUILayout.LabelField("Capturing Effect", EditorStyles.boldLabel);
			UIEffectEditor.DrawEffectProperties(serializedObject);

			//================
			// Advanced option.
			//================
			GUILayout.Space(10);
			EditorGUILayout.LabelField("Advanced Option", EditorStyles.boldLabel);

			var current = target as UIEffectCapturedImage;
			int w, h;

			// Desampling rate.
			using (new EditorGUILayout.HorizontalScope())
			{
				var sp = serializedObject.FindProperty("m_DesamplingRate");
				EditorGUILayout.PropertyField(sp);
				current.GetDesamplingSize((UIEffectCapturedImage.DesamplingRate)sp.intValue, out w, out h);
				GUILayout.Label(string.Format("{0}x{1}", w, h), EditorStyles.miniLabel);
			}


			// Reduction rate.
			using (new EditorGUILayout.HorizontalScope())
			{
				var sp = serializedObject.FindProperty("m_ReductionRate");
				EditorGUILayout.PropertyField(sp);
				current.GetDesamplingSize((UIEffectCapturedImage.DesamplingRate)sp.intValue, out w, out h);
				GUILayout.Label(string.Format("{0}x{1}", w, h), EditorStyles.miniLabel);
			}

			// Filter Mode.
			var spFilterMode = serializedObject.FindProperty("m_FilterMode");
			EditorGUILayout.PropertyField(spFilterMode);

			serializedObject.ApplyModifiedProperties();

			if (GUILayout.Button("Update Texture"))
			{
				bool enable = current.enabled;
				current.enabled = false;
				current.UpdateTexture();

				EditorApplication.delayCall += () =>
				{
					current.enabled = enable;
				};
			}
		}
	}
}