using UnityEditor;
using UnityEngine;

namespace LipsyncLight
{
    /// <summary>
    /// LipSync Light Setup コンポーネントのカスタムインスペクター。
    /// Inspector から直接 EditorWindow を開けるようにする。
    /// </summary>
    [CustomEditor(typeof(LipsyncLightSetup))]
    internal class LipsyncLightSetupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("LipSync Light ウィンドウを開く", GUILayout.Height(28)))
                LipsyncLightWindow.Open();

            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(
                "このコンポーネントは LipSync Light の設定を保持します。\n" +
                "上のボタンから設定ウィンドウを開いて編集してください。",
                MessageType.Info);
        }
    }
}
