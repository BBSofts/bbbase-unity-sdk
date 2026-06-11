using UnityEditor;
using UnityEngine;

namespace BBBaseSdk.Editor
{
    /// <summary>BBBaseSettings 인스펙터 — 입력 누락 경고와 빠른 링크를 보여준다.</summary>
    [CustomEditor(typeof(BBBaseSettings))]
    public class BBBaseSettingsInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "대시보드(https://bbbase.io)에서 발급한 값을 입력하세요.\n" +
                "API Key 는 공개 취급이지만 소스 형상관리에 커밋하지 않는 것을 권장합니다.",
                MessageType.Info);

            DrawDefaultInspector();

            var s = (BBBaseSettings)target;
            if (string.IsNullOrEmpty(s.projectId) || string.IsNullOrEmpty(s.apiKey))
                EditorGUILayout.HelpBox("projectId / apiKey 가 비어 있습니다. 입력 전엔 로그인/호출이 실패합니다.",
                    MessageType.Warning);

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("대시보드 열기"))
                    Application.OpenURL("https://bbbase.io");
                if (GUILayout.Button("Unity 퀵스타트"))
                    Application.OpenURL("https://api.bbbase.io/quickstart/unity");
            }
        }
    }
}
