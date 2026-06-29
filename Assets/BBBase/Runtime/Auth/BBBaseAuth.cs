using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace BBBaseSdk
{
    /// <summary>게임유저 인증 — 게스트/구글/앱인토스 로그인, 토큰 회전, 로그아웃.</summary>
    public class BBBaseAuth
    {
        private readonly BBBaseClient _client;
        private readonly BBBaseSession _session;

        public BBBaseAuth(BBBaseClient client, BBBaseSession session)
        {
            _client = client;
            _session = session;
            // 영속 세션이 복원돼 있으면 클라이언트에도 토큰을 전파
            if (_session.IsLoggedIn) _client.AccessToken = _session.AccessToken;
        }

        public bool IsLoggedIn => _session.IsLoggedIn;
        public string UserId => _session.UserId;
        public string AccessToken => _session.AccessToken;

        /// <summary>게스트 로그인. deviceId 생략 시 <see cref="SystemInfo.deviceUniqueIdentifier"/> 사용.</summary>
        public Task<AuthResult> LoginGuestAsync(string deviceId = null)
        {
            if (string.IsNullOrEmpty(deviceId)) deviceId = SystemInfo.deviceUniqueIdentifier;
            return LoginAsync(BBBaseProvider.Guest, "auth/guest", "deviceId", deviceId);
        }

        /// <summary>구글 계정 로그인. 게임 SDK 가 받은 idToken 을 받은 즉시 전달하라(만료/캐싱 주의).</summary>
        public Task<AuthResult> LoginGoogleAsync(string idToken)
            => LoginAsync(BBBaseProvider.Google, "auth/google", "idToken", idToken);

        /// <summary>앱인토스 로그인.</summary>
        public Task<AuthResult> LoginAppsInTossAsync(string authorizationCode, string referrer = null)
            => LoginAsync(BBBaseProvider.AppsInToss, "auth/apps-in-toss", "authorizationCode", authorizationCode,
                referrer == null ? null : new { referrer });

        private async Task<AuthResult> LoginAsync(
            BBBaseProvider provider, string path, string field, string credential, object extra = null)
        {
            var body = BuildBody(field, credential, extra);
            var data = await _client.SendProjectAsync<AuthResult>("POST", $"/{path}", body);
            _session.Set(provider, data.UserId, data.AccessToken, data.RefreshToken);
            _client.AccessToken = data.AccessToken;
            return data;
        }

        /// <summary>리프레시 토큰으로 액세스 토큰 회전. 보통 401 이후 자동 호출용.</summary>
        public async Task<AuthResult> RefreshAsync()
        {
            if (string.IsNullOrEmpty(_session.RefreshToken))
                throw new BBBaseException(BBBaseErrorCodes.NotLoggedIn, "리프레시 토큰이 없습니다.", 0, false, "");

            var data = await _client.SendProjectAsync<AuthResult>(
                "POST", "/auth/refresh", new { refreshToken = _session.RefreshToken });
            _session.UpdateTokens(data.AccessToken, data.RefreshToken);
            _client.AccessToken = data.AccessToken;
            return data;
        }

        /// <summary>로그아웃 — 서버에 refresh 토큰 무효화 요청 후 로컬 세션 삭제.</summary>
        public async Task LogoutAsync()
        {
            var refresh = _session.RefreshToken;
            _session.Clear();
            _client.AccessToken = null;
            if (!string.IsNullOrEmpty(refresh))
            {
                try { await _client.SendProjectAsync<object>("POST", "/auth/logout", new { refreshToken = refresh }); }
                catch { /* 이미 로컬은 정리됨 — 서버 실패는 무시 */ }
            }
        }

        // ── 계정 링킹 ────────────────────────────────────────────────
        // 현재 로그인된 계정(userId 불변)에 로그인 수단을 추가한다. userId 가 바뀌지 않으므로
        // 세이브(레코드)는 그대로 유지되고, 이후 그 수단으로 어느 기기에서든 같은 계정에 로그인할 수 있다.
        // 모두 로그인 상태에서 호출해야 한다. 그 수단이 이미 다른 계정에 묶여 있으면
        // BBBaseException(code=IDENTITY_ALREADY_LINKED, status=409) 가 던져진다.
        // 상대 계정 id 는 ex.RawBody(JSON) 의 error.details.conflictUserId 에서 읽는다.

        /// <summary>현재 계정에 게스트(다른 기기 등) 신원을 추가.</summary>
        public Task<AccountInfo> LinkGuestAsync(string deviceId = null)
        {
            if (string.IsNullOrEmpty(deviceId)) deviceId = SystemInfo.deviceUniqueIdentifier;
            return LinkAsync(new { provider = "GUEST", deviceId });
        }

        /// <summary>현재 게스트 계정에 구글 계정을 연동(가장 흔한 사용처).</summary>
        public Task<AccountInfo> LinkGoogleAsync(string idToken)
            => LinkAsync(new { provider = "GOOGLE", idToken });

        /// <summary>현재 계정에 앱인토스를 연동.</summary>
        public Task<AccountInfo> LinkAppsInTossAsync(string authorizationCode, string referrer = null)
            => LinkAsync(referrer == null
                ? (object)new { provider = "APPS_IN_TOSS", authorizationCode }
                : new { provider = "APPS_IN_TOSS", authorizationCode, referrer });

        private Task<AccountInfo> LinkAsync(object body)
        {
            EnsureLoggedIn();
            return _client.SendProjectAsync<AccountInfo>("POST", "/auth/link", body, withUserToken: true);
        }

        /// <summary>링크 해제. provider: "GUEST" | "GOOGLE" | "APPS_IN_TOSS".
        /// 마지막 남은 수단은 해제할 수 없다(CANNOT_UNLINK_LAST).</summary>
        public Task<AccountInfo> UnlinkAsync(string provider)
        {
            EnsureLoggedIn();
            return _client.SendProjectAsync<AccountInfo>("DELETE", $"/auth/link/{provider}", null, withUserToken: true);
        }

        /// <summary>내 계정 정보(userId, isGuest, providers).</summary>
        public Task<AccountInfo> GetMeAsync()
        {
            EnsureLoggedIn();
            return _client.SendProjectAsync<AccountInfo>("GET", "/auth/me", null, withUserToken: true);
        }

        private void EnsureLoggedIn()
        {
            if (!_session.IsLoggedIn)
                throw new BBBaseException(BBBaseErrorCodes.NotLoggedIn, "로그인 후 호출하세요.", 0, false, "");
        }

        private static string BuildBody(string field, string credential, object extra)
        {
            // extra 가 있으면 병합해 직렬화, 없으면 단일 필드만
            var dict = new System.Collections.Generic.Dictionary<string, object> { [field] = credential };
            if (extra != null)
                foreach (var p in extra.GetType().GetProperties())
                    dict[p.Name] = p.GetValue(extra);
            return JsonConvert.SerializeObject(dict);
        }
    }

    /// <summary>로그인/회전 응답.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class AuthResult
    {
        [JsonProperty("userId")] public string UserId;
        [JsonProperty("accessToken")] public string AccessToken;
        [JsonProperty("refreshToken")] public string RefreshToken;
    }

    /// <summary>계정 정보 — 링킹/언링크/GetMe 응답.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class AccountInfo
    {
        [JsonProperty("userId")] public string UserId;
        /// <summary>게스트 신원만 가진 계정(아직 소셜 미연동)이면 true.</summary>
        [JsonProperty("isGuest")] public bool IsGuest;
        /// <summary>연결된 로그인 수단 목록("GUEST"/"GOOGLE"/"APPS_IN_TOSS").</summary>
        [JsonProperty("providers")] public string[] Providers;
    }
}
