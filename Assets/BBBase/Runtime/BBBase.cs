using System;
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
        public static BBBaseConfig Config { get; private set; }

        /// <summary>
        /// 액세스·리프레시 토큰이 모두 만료돼 SDK 가 세션을 자동 정리했을 때 방출.
        /// 게임은 이 이벤트만 구독해 provider 별 재로그인 UI 를 띄우면 된다.
        /// provider 가 <c>BBBaseProvider.Unknown</c> 이면 로그인 수단 불명이므로 특정 방식으로
        /// 자동 로그인하지 말고 사용자에게 로그인 수단 선택 UI 를 보여줄 것(잘못된 계정으로의 조용한 전환 방지).
        /// 평소 401(액세스 토큰 만료)은 SDK 가 조용히 refresh 하므로 게임이 신경 쓸 필요 없다.
        /// </summary>
        public static event Action<BBBaseProvider> SessionExpired;

        /// <summary>
        /// 운영자가 이 계정을 제재했을 때 방출(서버 403 USER_BANNED). 동시 요청이 여러 번 403 을
        /// 받아도 한 번만 방출된다. expiresAt 이 null 이면 영구 제재이고, reason 은 운영자 메모(null 가능).
        ///
        /// 게임은 이 이벤트를 구독해 플레이를 중단하고 정지 안내를 띄운다 — 제재 집행은 서버가 하므로
        /// (모든 요청이 403) 저장·랭킹·보상은 이미 막혀 있고, 화면 전환만 게임의 몫이다.
        ///
        /// SDK 는 토큰을 지우지 않는다: 서버가 제재 중에도 auth/me 는 열어두므로(계정 상태 조회용)
        /// 세션을 지우면 그 조회 경로까지 막히고, 기간제 제재가 풀렸을 때 재로그인 없이 복구돼야 한다.
        ///
        /// <code>
        /// BBBase.Banned += (expiresAt, reason) => {
        ///     Time.timeScale = 0f;
        ///     banPopup.Show(expiresAt, reason);   // expiresAt == null → 영구
        /// };
        /// </code>
        /// </summary>
        public static event Action<DateTime?, string> Banned;

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
            // 401 자동 refresh + 세션 만료 처리기를 client 에 주입
            _client.SetAuthHandlers(
                refreshHandler: async () =>
                {
                    try { await Auth.RefreshAsync(); return true; }
                    catch { return false; }
                },
                sessionExpiredHandler: () =>
                {
                    var provider = Auth.HandleSessionExpired();
                    SessionExpired?.Invoke(provider);
                },
                bannedHandler: (expiresAt, reason) => Banned?.Invoke(expiresAt, reason));
            Records = new BBBaseRecords(_client, _session);
            Leaderboards = new BBBaseLeaderboards(_client);
            Leagues = new BBBaseLeagues(_client, _session);
            Mails = new BBBaseMails(_client, _session);
            Logs = new BBBaseLogs(_client);
            Config = new BBBaseConfig(_client);

            if (settings.verboseLogging)
                Debug.Log($"[BBBase] initialized. env={settings.ActiveEnvironmentName}, " +
                          $"project={settings.ActiveProjectId}, restoredSession={_session.IsLoggedIn}");
        }
    }
}
