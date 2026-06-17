using UnityEngine;

namespace BBBaseSdk
{
    /// <summary>
    /// BBBase 연결 설정. <c>Assets/Resources/BBBaseSettings.asset</c> 로 저장되어
    /// 런타임에 자동 로드된다(메뉴: <b>BBBase ▸ Settings</b> 로 생성/편집).
    ///
    /// 개발용/라이브용은 BBBase 에서 각각 별도 프로젝트(projectId + apiKey)로 만든다.
    /// 둘 다 같은 서버(baseUrl)를 쓰므로 baseUrl 은 공통 1개, 환경별로는 projectId/apiKey
    /// 쌍만 다르다. <see cref="environment"/> 가 둘 중 어느 쌍을 쓸지 결정한다.
    ///
    /// API_KEY 는 게임 클라이언트에 임베드되는 공개 취급 키지만, 그래도 소스 형상관리에
    /// 커밋하지 않는 것을 권장한다(키 로테이션 용이). .gitignore 에 추가하라.
    /// </summary>
    public class BBBaseSettings : ScriptableObject
    {
        /// <summary>Resources 폴더에서 찾는 에셋 이름(확장자 제외).</summary>
        public const string ResourceName = "BBBaseSettings";

        /// <summary>활성 환경 선택.</summary>
        public enum BBBaseEnvironment
        {
            /// <summary>에디터/Development Build 면 개발용, 릴리스 빌드면 라이브용 (Debug.isDebugBuild).</summary>
            Auto,
            /// <summary>빌드 종류와 무관하게 개발용 프로젝트 강제.</summary>
            Development,
            /// <summary>빌드 종류와 무관하게 라이브용 프로젝트 강제.</summary>
            Production,
        }

        [Tooltip("개발용·라이브용 프로젝트가 공유하는 BBBase API 서버. 보통 그대로 둔다. 예: https://api.bbbase.io")]
        public string baseUrl = "https://api.bbbase.io";

        [Tooltip("Auto: 에디터/Development Build=개발용, 릴리스=라이브용. Development/Production: 강제 고정")]
        public BBBaseEnvironment environment = BBBaseEnvironment.Auto;

        [Header("개발용 프로젝트")]
        [Tooltip("대시보드(https://bbbase.io)에서 만든 개발용 프로젝트 ID")]
        public string devProjectId = "";

        [Tooltip("개발용 프로젝트의 게임 클라이언트용 API 키")]
        public string devApiKey = "";

        [Header("라이브용 프로젝트")]
        [Tooltip("대시보드에서 만든 라이브(운영)용 프로젝트 ID")]
        public string prodProjectId = "";

        [Tooltip("라이브용 프로젝트의 게임 클라이언트용 API 키")]
        public string prodApiKey = "";

        [Header("동작 옵션")]
        [Tooltip("요청 타임아웃(초). 0 이면 Unity 기본값.")]
        public int requestTimeoutSeconds = 15;

        [Tooltip("로그인 토큰을 PlayerPrefs 에 저장해 앱 재시작 후 세션 복원")]
        public bool persistSession = true;

        [Tooltip("SDK 내부 디버그 로그 출력")]
        public bool verboseLogging = false;

        [Header("Legacy (단일 환경)")]
        [Tooltip("구버전 호환용. 환경별 값이 비어 있으면 이 값으로 폴백한다. 신규 설정은 위의 개발용/라이브용 쌍을 쓰고 이 두 칸은 비워 둔다.")]
        public string projectId = "";

        [Tooltip("구버전 호환용 API 키 폴백.")]
        public string apiKey = "";

        private static BBBaseSettings _cached;

        /// <summary>Resources 에서 설정을 로드(1회 캐시). 없으면 null.</summary>
        public static BBBaseSettings LoadFromResources()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<BBBaseSettings>(ResourceName);
            return _cached;
        }

        /// <summary>현재 활성 환경이 라이브(운영)인지.</summary>
        public bool IsProduction
        {
            get
            {
                switch (environment)
                {
                    case BBBaseEnvironment.Development: return false;
                    case BBBaseEnvironment.Production: return true;
                    default: return !Debug.isDebugBuild;
                }
            }
        }

        /// <summary>현재 환경에 해당하는 projectId (환경별 값이 비면 legacy 로 폴백).</summary>
        public string ActiveProjectId
        {
            get
            {
                var v = IsProduction ? prodProjectId : devProjectId;
                return string.IsNullOrEmpty(v) ? projectId : v;
            }
        }

        /// <summary>현재 환경에 해당하는 apiKey (환경별 값이 비면 legacy 로 폴백).</summary>
        public string ActiveApiKey
        {
            get
            {
                var v = IsProduction ? prodApiKey : devApiKey;
                return string.IsNullOrEmpty(v) ? apiKey : v;
            }
        }

        /// <summary>사람이 읽을 현재 환경 이름(로그용).</summary>
        public string ActiveEnvironmentName => IsProduction ? "production" : "development";

        public bool IsValid =>
            !string.IsNullOrEmpty(baseUrl) &&
            !string.IsNullOrEmpty(ActiveProjectId) &&
            !string.IsNullOrEmpty(ActiveApiKey);

        /// <summary>끝의 슬래시를 제거한 baseUrl.</summary>
        public string NormalizedBaseUrl =>
            string.IsNullOrEmpty(baseUrl) ? "" : baseUrl.TrimEnd('/');
    }
}
