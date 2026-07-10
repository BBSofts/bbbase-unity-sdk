using UnityEngine;

namespace BBBaseSdk
{
    /// <summary>
    /// BBBase SDK 진입점. 앱 시작 시 <see cref="Init()"/> 를 한 번 호출한 뒤
    /// <c>BBBase.Auth</c> / <c>BBBase.Records</c> / <c>BBBase.Leaderboards</c> 로 호출한다.
    ///
    /// 예)
    /// <code>
    /// BBBase.Init();
    /// await BBBase.Auth.LoginGuestAsync();
    /// await BBBase.Records.SaveMineAsync(new { best_time = 4.35, stars = 120 });
    /// var me = await BBBase.Records.LoadMineAsync();
    /// var top = await BBBase.Leaderboards.GetTopEntriesAsync("LEADERBOARD_ID", 10);
    /// </code>
    /// </summary>
    public static class BBBase
    {
        private static BBBaseClient _client;
        private static BBBaseSession _session;

        public static BBBaseSettings Settings { get; private set; }
        public static BBBaseAuth Auth { get; private set; }
        public static BBBaseRecords Records { get; private set; }
        public static BBBaseLeaderboards Leaderboards { get; private set; }
        public static BBBaseLeagues Leagues { get; private set; }
        public static BBBaseMails Mails { get; private set; }
        public static BBBaseLogs Logs { get; private set; }

        public static bool IsInitialized => _client != null;

        /// <summary>현재 로그인 상태(편의 접근자).</summary>
        public static bool IsLoggedIn => Auth != null && Auth.IsLoggedIn;

        /// <summary>BBBase 가 발급한 내 게임유저 ID(미로그인이면 null).</summary>
        public static string UserId => Auth?.UserId;

        /// <summary>
        /// Resources/BBBaseSettings.asset 을 로드해 SDK 를 초기화한다.
        /// 메뉴 <b>BBBase ▸ Settings</b> 로 에셋을 먼저 만들어 두어야 한다.
        /// </summary>
        public static void Init()
        {
            var settings = BBBaseSettings.LoadFromResources();
            if (settings == null)
            {
                Debug.LogError("[BBBase] 설정 에셋을 찾을 수 없습니다. 메뉴 'BBBase ▸ Settings' 로 " +
                               "Resources/BBBaseSettings.asset 을 생성하세요.");
                return;
            }
            Init(settings);
        }

        /// <summary>명시적 설정으로 초기화(테스트/멀티환경용).</summary>
        public static void Init(BBBaseSettings settings)
        {
            if (settings == null || !settings.IsValid)
            {
                Debug.LogError("[BBBase] 설정이 비어있습니다. baseUrl/projectId/apiKey 를 확인하세요.");
                return;
            }

            Settings = settings;
            _session = new BBBaseSession(settings.persistSession);
            _client = new BBBaseClient(settings);
            Auth = new BBBaseAuth(_client, _session);
            Records = new BBBaseRecords(_client, _session);
            Leaderboards = new BBBaseLeaderboards(_client);
            Leagues = new BBBaseLeagues(_client, _session);
            Mails = new BBBaseMails(_client, _session);
            Logs = new BBBaseLogs(_client);

            if (settings.verboseLogging)
                Debug.Log($"[BBBase] initialized. env={settings.ActiveEnvironmentName}, " +
                          $"project={settings.ActiveProjectId}, restoredSession={_session.IsLoggedIn}");
        }
    }
}
