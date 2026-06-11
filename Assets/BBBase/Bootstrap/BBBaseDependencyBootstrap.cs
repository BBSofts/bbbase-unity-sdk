using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace BBBaseSdk.Bootstrap
{
    /// <summary>
    /// BBBase SDK 가 의존하는 Newtonsoft Json 패키지를 자동으로 설치한다.
    ///
    /// ⚠️ 이 스크립트는 <b>Newtonsoft 에도, BBBase.Runtime 에도 의존하지 않는</b> 독립 에디터
    /// 어셈블리(BBBase.Bootstrap.Editor)에 들어있다. 의존성이 빠진 상태에서도 반드시 컴파일·
    /// 실행돼야 패키지를 끌어올 수 있기 때문이다(의존하면 그 자체가 컴파일 실패 → 영영 안 돔).
    ///
    /// 임포트 직후 1회 실행되어 manifest 에 패키지가 없으면 추가한다. 고객은 별도 조치 불필요.
    /// </summary>
    [InitializeOnLoad]
    internal static class BBBaseDependencyBootstrap
    {
        private const string PackageName = "com.unity.nuget.newtonsoft-json";
        private const string PackageVersion = "3.2.1";
        private const string CheckedKey = "bbbase.dependency.checked"; // 세션당 1회

        private static ListRequest _listRequest;
        private static AddRequest _addRequest;

        static BBBaseDependencyBootstrap()
        {
            // 같은 에디터 세션에서 재컴파일마다 돌지 않도록 가드(설치 후 즉시 재확인 방지)
            if (SessionState.GetBool(CheckedKey, false)) return;
            _listRequest = Client.List(offlineMode: true, includeIndirectDependencies: false);
            EditorApplication.update += PollList;
        }

        private static void PollList()
        {
            if (_listRequest == null || !_listRequest.IsCompleted) return;
            EditorApplication.update -= PollList;
            SessionState.SetBool(CheckedKey, true);

            if (_listRequest.Status == StatusCode.Success)
            {
                foreach (var pkg in _listRequest.Result)
                    if (pkg.name == PackageName) return; // 이미 설치됨 — 끝
            }

            Debug.Log($"[BBBase] 의존성 '{PackageName}' 이 없어 자동 설치를 시작합니다…");
            _addRequest = Client.Add($"{PackageName}@{PackageVersion}");
            EditorApplication.update += PollAdd;
        }

        private static void PollAdd()
        {
            if (_addRequest == null || !_addRequest.IsCompleted) return;
            EditorApplication.update -= PollAdd;

            if (_addRequest.Status == StatusCode.Success)
                Debug.Log($"[BBBase] 의존성 설치 완료: {_addRequest.Result.packageId}. 곧 재컴파일됩니다.");
            else
                Debug.LogError($"[BBBase] '{PackageName}' 자동 설치 실패: {_addRequest.Error?.message}\n" +
                               $"Package Manager 에서 'Add package by name…' 으로 직접 추가하세요.");
        }
    }
}
