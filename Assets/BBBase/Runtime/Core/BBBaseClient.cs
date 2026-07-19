using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace BBBaseSdk
{
    /// <summary>
    /// 저수준 HTTP 클라이언트. 헤더 세팅·envelope 파싱·에러를 <see cref="BBBaseException"/>
    /// 로 변환하는 책임만 진다. 보통은 <see cref="BBBase"/> 파사드를 통해 간접 사용한다.
    /// </summary>
    public class BBBaseClient
    {
        private readonly BBBaseSettings _settings;

        /// <summary>로그인 후 채워지는 게임유저 토큰(레코드 호출 시 Bearer 로 붙음).</summary>
        public string AccessToken { get; set; }

        /// <summary>401 시 액세스 토큰을 회전(refresh)하는 위임. 성공하면 true.
        /// 순환 생성을 피하려 생성자 대신 <see cref="SetAuthHandlers"/> 로 늦게 주입한다.</summary>
        private Func<Task<bool>> _refreshHandler;

        /// <summary>refresh 마저 실패해 세션을 정리해야 할 때 호출되는 위임(세션 clear + 이벤트 방출).</summary>
        private Action _sessionExpiredHandler;

        /// <summary>refresh 단일화(single-flight)용 진행 중 작업. 동시에 여러 요청이 401 이 나도
        /// refresh 는 1번만 돈다(회전 토큰 stampede → 멀쩡한 세션 오인 로그아웃 방지).</summary>
        private Task<bool> _refreshInFlight;
        private readonly object _refreshLock = new object();

        public BBBaseClient(BBBaseSettings settings) => _settings = settings;

        /// <summary>Init 시점에 401 자동 refresh / 세션 만료 처리기를 주입한다.</summary>
        public void SetAuthHandlers(Func<Task<bool>> refreshHandler, Action sessionExpiredHandler)
        {
            _refreshHandler = refreshHandler;
            _sessionExpiredHandler = sessionExpiredHandler;
        }

        private void Log(string msg)
        {
            if (_settings.verboseLogging) Debug.Log($"[BBBase] {msg}");
        }

        /// <summary>프로젝트 스코프 경로(/projects/{pid}/...) 로 요청. 응답 data 를 T 로 역직렬화.</summary>
        public Task<T> SendProjectAsync<T>(string method, string subPath, object body = null, bool withUserToken = false)
            => SendAsync<T>(method, $"/projects/{_settings.ActiveProjectId}{subPath}", body, withUserToken);

        /// <summary>
        /// 임의 경로로 요청. data 가 필요 없으면 T 를 object 로 두고 결과를 무시한다.
        /// 인증 요청(withUserToken)이 401 로 실패하면 액세스 토큰 만료로 보고 refresh 를 1회
        /// 시도한 뒤 새 토큰으로 원 요청을 1회 재시도한다. refresh 마저 실패하면 세션을 자동
        /// 정리하고 <c>BBBase.SessionExpired</c> 를 방출한다(게임은 재로그인만 처리하면 됨).
        /// </summary>
        public async Task<T> SendAsync<T>(string method, string path, object body = null, bool withUserToken = false)
        {
            try
            {
                return await SendOnceAsync<T>(method, path, body, withUserToken);
            }
            catch (BBBaseException ex) when (
                withUserToken && ex.StatusCode == 401 && !string.IsNullOrEmpty(AccessToken) && _refreshHandler != null)
            {
                // 자동 refresh (refresh 호출 자체는 withUserToken=false 라 이 분기를 타지 않아 재귀 없음)
                var refreshed = await RefreshSingleFlightAsync();
                if (refreshed)
                    return await SendOnceAsync<T>(method, path, body, true); // 새 토큰으로 재시도

                // refresh 실패 = 리프레시 토큰도 만료/폐기 → 세션은 이미 정리·방출됨. 원 401 전파.
                throw;
            }
        }

        /// <summary>refresh 를 single-flight 로 1회만 실행하고 성공 여부를 반환한다.
        /// 이미 진행 중이면 그 작업을 공유해 동시 401 stampede 를 막는다.</summary>
        private Task<bool> RefreshSingleFlightAsync()
        {
            lock (_refreshLock)
            {
                if (_refreshInFlight == null || _refreshInFlight.IsCompleted)
                    _refreshInFlight = DoRefreshAsync();
                return _refreshInFlight;
            }
        }

        private async Task<bool> DoRefreshAsync()
        {
            Log("access token 401 → refresh 시도");
            var refreshed = await _refreshHandler();
            if (!refreshed)
            {
                // refresh 실패 = 리프레시 토큰도 만료/폐기 → 세션 정리 + 재로그인 신호
                Log("refresh 실패 → 세션 만료 처리");
                _sessionExpiredHandler?.Invoke();
            }
            return refreshed;
        }

        private async Task<T> SendOnceAsync<T>(string method, string path, object body = null, bool withUserToken = false)
        {
            var url = _settings.NormalizedBaseUrl + path;
            using var req = new UnityWebRequest(url, method);
            req.downloadHandler = new DownloadHandlerBuffer();

            if (body != null)
            {
                var json = body is string s ? s : JsonConvert.SerializeObject(body);
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.SetRequestHeader("Content-Type", "application/json");
            }

            req.SetRequestHeader("X-API-Key", _settings.ActiveApiKey);
            if (withUserToken && !string.IsNullOrEmpty(AccessToken))
                req.SetRequestHeader("Authorization", "Bearer " + AccessToken);

            if (_settings.requestTimeoutSeconds > 0)
                req.timeout = _settings.requestTimeoutSeconds;

            Log($"{method} {path}");
            await req.SendWebRequest();

            // ── 연결 자체가 실패(네트워크/타임아웃) ──
            if (req.result == UnityWebRequest.Result.ConnectionError ||
                req.result == UnityWebRequest.Result.DataProcessingError)
            {
                throw new BBBaseException(BBBaseErrorCodes.NetworkError,
                    $"연결 실패: {req.error}", 0, isNetworkError: true, rawBody: "");
            }

            var raw = req.downloadHandler?.text ?? "";
            ApiResponse<T> parsed = null;
            try { parsed = JsonConvert.DeserializeObject<ApiResponse<T>>(raw); }
            catch { /* 비-JSON 응답(프록시 5xx 등) → 아래 status 기반 처리 */ }

            // ── HTTP 4xx/5xx 또는 success:false ──
            if (req.responseCode >= 400 || (parsed != null && !parsed.Success))
            {
                var code = parsed?.Error?.Code ?? $"HTTP_{req.responseCode}";
                var msg = parsed?.Error?.Message ?? $"요청 실패 ({req.responseCode})";
                throw new BBBaseException(code, msg, req.responseCode, isNetworkError: false, rawBody: raw);
            }

            // 성공 상태인데 본문이 비었거나(204 등) 파싱 불가면 기본값 반환
            if (parsed == null)
            {
                if (string.IsNullOrWhiteSpace(raw)) return default;
                throw new BBBaseException(BBBaseErrorCodes.NetworkError,
                    "응답을 파싱할 수 없습니다.", req.responseCode, false, raw);
            }

            return parsed.Data;
        }
    }
}
