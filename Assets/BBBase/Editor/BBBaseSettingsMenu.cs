using System.IO;
using UnityEditor;
using UnityEngine;

namespace BBBaseSdk.Editor
{
    /// <summary>
    /// 메뉴 <b>BBBase ▸ Settings</b> — Resources/BBBaseSettings.asset 을 생성/선택한다.
    /// 고객은 임포트 후 이 메뉴만 누르면 인스펙터에서 baseUrl/projectId/apiKey 를 입력하면 된다.
    /// </summary>
    public static class BBBaseSettingsMenu
    {
        private const string ResourcesDir = "Assets/Resources";
        private const string AssetPath = ResourcesDir + "/" + BBBaseSettings.ResourceName + ".asset";

        [MenuItem("BBBase/Settings", priority = 0)]
        public static void OpenOrCreateSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<BBBaseSettings>(AssetPath);
            if (settings == null)
            {
                if (!Directory.Exists(ResourcesDir))
                    Directory.CreateDirectory(ResourcesDir);

                settings = ScriptableObject.CreateInstance<BBBaseSettings>();
                AssetDatabase.CreateAsset(settings, AssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"[BBBase] 설정 에셋 생성: {AssetPath}");
            }

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);
        }

        [MenuItem("BBBase/Open Quickstart (Unity)", priority = 20)]
        public static void OpenQuickstart() =>
            Application.OpenURL("https://api.bbbase.io/quickstart/unity");

        [MenuItem("BBBase/Open Dashboard", priority = 21)]
        public static void OpenDashboard() =>
            Application.OpenURL("https://bbbase.io");
    }
}
