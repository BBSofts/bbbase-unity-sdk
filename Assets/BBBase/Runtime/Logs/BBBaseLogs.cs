using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace BBBaseSdk
{
    /// <summary>
    /// 로그 수집 — API 키만으로 임의 이벤트 로그를 남긴다(로그인/게임유저 토큰 불필요).
    /// 로그인 실패처럼 "인증 전/실패 시점" 이벤트를 쌓는 게 주 용도 — 일반 레코드 저장은
    /// 게임유저 토큰이 필요해서 로그인 전/실패 상황을 못 잡는다.
    ///
    /// fire-and-forget 로 쓰길 권장한다: 반환 Task 를 await 하지 않아도 되고, 전송이 실패해도
    /// 게임 흐름(로그인 재시도 등)을 막지 않는다.
    ///
    /// ⚠️ 서버는 이 로그를 "신뢰할 수 없는 제보"로 취급한다(API 키는 공개 취급). 게임 상태·과금에
    ///    반영하지 말고 디버깅·통계 참고용으로만 쓴다.
    ///
    /// 예)
    /// <code>
    /// try { await BBBase.Auth.GoogleAsync(idToken); }
    /// catch (BBBaseException e) {
    ///     _ = BBBase.Logs.SendAsync(level: "error", category: "login_fail",
    ///         message: e.Message, data: new { code = e.Code, provider = "google" });
    /// }
    /// </code>
    /// </summary>
    public class BBBaseLogs
    {
        private readonly BBBaseClient _client;

        public BBBaseLogs(BBBaseClient client)
        {
            _client = client;
        }

        /// <summary>
        /// 로그 1건 전송. 모든 인자는 선택이며 null 이면 페이로드에서 제외된다.
        /// platform 을 생략하면 <see cref="Application.platform"/> 으로 자동 채운다.
        /// 로그인 여부와 무관하게 API 키만으로 동작한다. 반환은 무시해도 된다(fire-and-forget).
        /// </summary>
        /// <param name="level">"debug"|"info"|"warn"|"error" (생략 시 서버가 info).</param>
        /// <param name="category">그룹핑 키. 예: "login_fail".</param>
        /// <param name="message">사람이 읽는 메시지.</param>
        /// <param name="platform">플랫폼. 생략 시 Application.platform.</param>
        /// <param name="data">자유 커스텀 필드(익명 객체/Dictionary 등).</param>
        public Task<LogResult> SendAsync(
            string level = null,
            string category = null,
            string message = null,
            string platform = null,
            object data = null)
        {
            var payload = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(level)) payload["level"] = level;
            if (!string.IsNullOrEmpty(category)) payload["category"] = category;
            if (!string.IsNullOrEmpty(message)) payload["message"] = message;
            payload["platform"] = string.IsNullOrEmpty(platform) ? Application.platform.ToString() : platform;
            if (data != null) payload["data"] = data;

            return _client.SendProjectAsync<LogResult>("POST", "/logs", payload, withUserToken: false);
        }
    }

    /// <summary>로그 전송 결과(서버가 부여한 id·시각).</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class LogResult
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("createdAt")] public string CreatedAt;
    }
}
