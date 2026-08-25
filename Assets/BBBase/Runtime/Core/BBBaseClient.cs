using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading;
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

        /// <summary>로그인 후 채워지는 게임유저 토큰(레코드 호출 시 Bearer 로 붙음).
        /// 새 토큰이 들어오면(=로그인 성공) 제재 래치를 푼다 — 기간제 제재가 만료된 뒤 다시
        /// 로그인하면 같은 세션에서 Banned 를 또 받을 수 있어야 하기 때문.</summary>
        public string AccessToken
        {
            get => _accessToken;
            set
            {
                _accessToken = value;
                if (!string.IsNullOrEmpty(value)) Interlocked.Exchange(ref _bannedNotified, 0);
            }
        }
        private string _accessToken;

        /// <summary>401 시 액세스 토큰을 회전(refresh)하는 위임. 성공하면 true.
        /// 순환 생성을 피하려 생성자 대신 <see cref="SetAuthHandlers"/> 로 늦게 주입한다.</summary>
        private Func<Task<bool>> _refreshHandler;

        /// <summary>refresh 마저 실패해 세션을 정리해야 할 때 호출되는 위임(세션 clear + 이벤트 방출).</summary>
        private Action _sessionExpiredHandler;

        /// <summary>서버가 403 USER_BANNED 로 거절했을 때 호출되는 위임(expiresAt, reason).
        /// expiresAt 이 null 이면 영구 제재.</summary>
        private Action<DateTime?, string> _bannedHandler;

        /// <summary>Banned 중복 방출 방지 래치. 동시에 여러 요청이 403 을 받아도 정지 안내는 한 번만
        /// 띄워야 한다(refresh single-flight 와 같은 취지). 로그인 성공 시 해제된다.</summary>
        private int _bannedNotified;

        /// <summary>refresh 단일화(single-flight)용 진행 중 작업. 동시에 여러 요청이 401 이 나도
        /// refresh 는 1번만 돈다(회전 토큰 stampede → 멀쩡한 세션 오인 로그아웃 방지).</summary>
        private Task<bool> _refreshInFlight;
        private readonly object _refreshLock = new object();

        public BBBaseClient(BBBaseSettings settings) => _settings = settings;

        /// <summary>Init 시점에 401 자동 refresh / 세션 만료 처리기를 주입한다.</summary>
        public void SetAuthHandlers(Func<Task<bool>> refreshHandler, Action sessionExpiredHandler,
            Action<DateTime?, string> bannedHandler = null)
        {
            _refreshHandler = refreshHandler;
            _sessionExpiredHandler = sessionExpiredHandler;
            _bannedHandler = bannedHandler;
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
                var details = parsed?.Error?.Details;
                if (code == BBBaseErrorCodes.UserBanned) NotifyBanned(details);
                throw new BBBaseException(code, msg, req.responseCode, isNetworkError: false, rawBody: raw,
                    details: details);
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

        /// <summary>
        /// USER_BANNED 를 게임에 1회만 알린다. details.expiresAt 은 영구 제재면 null 로 오고,
        /// 문자열이면 ISO 8601 UTC 로 파싱한다(파싱 실패 시 null = 영구로 보수적 처리하지 않고
        /// 그대로 null 을 넘겨 게임이 "기간 불명"으로 다루게 한다).
        /// </summary>
        private void NotifyBanned(IReadOnlyDictionary<string, object> details)
        {
            if (_bannedHandler == null) return;
            if (Interlocked.Exchange(ref _bannedNotified, 1) == 1) return; // 이미 알림

            DateTime? expiresAt = null;
            string reason = null;
            if (details != null)
            {
                if (details.TryGetValue("expiresAt", out var rawExp) && rawExp != null &&
                    DateTime.TryParse(rawExp.ToString(), CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsedExp))
                {
                    expiresAt = parsedExp;
                }
                if (details.TryGetValue("reason", out var rawReason) && rawReason != null)
                    reason = rawReason.ToString();
            }

            Log($"USER_BANNED 수신 → Banned 방출 (expiresAt={(expiresAt.HasValue ? expiresAt.ToString() : "영구")})");
            _bannedHandler(expiresAt, reason);
        }
    }
}
