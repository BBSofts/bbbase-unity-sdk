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
}
