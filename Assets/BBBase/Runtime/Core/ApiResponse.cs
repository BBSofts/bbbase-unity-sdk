using System.Collections.Generic;
using Newtonsoft.Json;

namespace BBBaseSdk
{
    /// <summary>
    /// BBBase 응답 봉투(envelope). 모든 API 는 이 형태로 응답한다.
    ///   { "success": true,  "data": { ... } }
    ///   { "success": false, "error": { "code": "...", "message": "..." } }
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class ApiResponse<T>
    {
        [JsonProperty("success")] public bool Success;
        [JsonProperty("data")] public T Data;
        [JsonProperty("error")] public ApiError Error;
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ApiError
    {
        /// <summary>분기 기준 — 항상 이 code 로 분기하라(message 는 사람용, 바뀔 수 있음).</summary>
        [JsonProperty("code")] public string Code;
        [JsonProperty("message")] public string Message;

        /// <summary>
        /// 코드별 부가 정보(없으면 null).
        /// 예) USER_BANNED → {"expiresAt": "2026-08-27T...Z" 또는 null, "reason": "..."}
        ///     IDENTITY_ALREADY_LINKED → {"conflictUserId": "...", "conflictLastLoginAt": "..."}
        /// </summary>
        [JsonProperty("details")] public Dictionary<string, object> Details;
    }
}
