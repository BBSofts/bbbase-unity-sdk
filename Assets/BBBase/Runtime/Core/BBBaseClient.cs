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

        public BBBaseClient(BBBaseSettings settings) => _settings = settings;

        private void Log(string msg)
        {
            if (_settings.verboseLogging) Debug.Log($"[BBBase] {msg}");
        }

        /// <summary>프로젝트 스코프 경로(/projects/{pid}/...) 로 요청. 응답 data 를 T 로 역직렬화.</summary>
        public Task<T> SendProjectAsync<T>(string method, string subPath, object body = null, bool withUserToken = false)
            => SendAsync<T>(method, $"/projects/{_settings.projectId}{subPath}", body, withUserToken);

        /// <summary>임의 경로로 요청. data 가 필요 없으면 T 를 object 로 두고 결과를 무시한다.</summary>
        public async Task<T> SendAsync<T>(string method, string path, object body = null, bool withUserToken = false)
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

            req.SetRequestHeader("X-API-Key", _settings.apiKey);
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
