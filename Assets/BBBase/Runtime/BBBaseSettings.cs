using UnityEngine;

namespace BBBaseSdk
{
    /// <summary>
    /// BBBase 연결 설정. <c>Assets/Resources/BBBaseSettings.asset</c> 로 저장되어
    /// 런타임에 자동 로드된다(메뉴: <b>BBBase ▸ Settings</b> 로 생성/편집).
    ///
    /// API_KEY 는 게임 클라이언트에 임베드되는 공개 취급 키지만, 그래도 소스 형상관리에
    /// 커밋하지 않는 것을 권장한다(키 로테이션 용이). .gitignore 에 추가하라.
    /// </summary>
    public class BBBaseSettings : ScriptableObject
    {
        /// <summary>Resources 폴더에서 찾는 에셋 이름(확장자 제외).</summary>
        public const string ResourceName = "BBBaseSettings";

        [Tooltip("예: https://api.bbbase.io")]
        public string baseUrl = "https://api.bbbase.io";

        [Tooltip("대시보드(https://bbbase.io)에서 발급한 프로젝트 ID")]
        public string projectId = "";

        [Tooltip("대시보드에서 발급한 게임 클라이언트용 API 키")]
        public string apiKey = "";

        [Header("동작 옵션")]
        [Tooltip("요청 타임아웃(초). 0 이면 Unity 기본값.")]
        public int requestTimeoutSeconds = 15;

        [Tooltip("로그인 토큰을 PlayerPrefs 에 저장해 앱 재시작 후 세션 복원")]
        public bool persistSession = true;

        [Tooltip("SDK 내부 디버그 로그 출력")]
        public bool verboseLogging = false;

        private static BBBaseSettings _cached;

        /// <summary>Resources 에서 설정을 로드(1회 캐시). 없으면 null.</summary>
        public static BBBaseSettings LoadFromResources()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<BBBaseSettings>(ResourceName);
            return _cached;
        }

        public bool IsValid =>
            !string.IsNullOrEmpty(baseUrl) &&
            !string.IsNullOrEmpty(projectId) &&
            !string.IsNullOrEmpty(apiKey);

        /// <summary>끝의 슬래시를 제거한 baseUrl.</summary>
        public string NormalizedBaseUrl =>
            string.IsNullOrEmpty(baseUrl) ? "" : baseUrl.TrimEnd('/');
    }
}
