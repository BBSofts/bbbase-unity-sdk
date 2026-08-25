using System;
using System.Collections.Generic;

namespace BBBaseSdk
{
    /// <summary>
    /// BBBase 호출이 실패하면 던져진다. 게임 코드는 <see cref="Code"/> 로 분기한다.
    /// 네트워크 단절·타임아웃은 <see cref="IsNetworkError"/> 가 true 이고 Code 는
    /// <see cref="BBBaseErrorCodes.NetworkError"/> 다.
    /// </summary>
    public class BBBaseException : Exception
    {
        /// <summary>서버 error.code (예: "DUPLICATE_VALUE") 또는 클라이언트 합성 코드.</summary>
        public string Code { get; }

        /// <summary>HTTP 상태코드. 네트워크 오류면 0.</summary>
        public long StatusCode { get; }

        /// <summary>연결 실패/타임아웃 등 응답을 못 받은 경우.</summary>
        public bool IsNetworkError { get; }

        /// <summary>서버가 돌려준 원문(envelope JSON 또는 빈 문자열).</summary>
        public string RawBody { get; }

        /// <summary>
        /// 서버 error.details(없으면 null). 코드별 부가 정보가 담긴다 —
        /// USER_BANNED 면 expiresAt(영구 제재는 null)·reason 이 들어 있다.
        /// </summary>
        public IReadOnlyDictionary<string, object> Details { get; }

        public BBBaseException(string code, string message, long statusCode, bool isNetworkError, string rawBody,
            IReadOnlyDictionary<string, object> details = null)
            : base(message)
        {
            Code = code;
            StatusCode = statusCode;
            IsNetworkError = isNetworkError;
            RawBody = rawBody;
            Details = details;
        }

        public override string ToString() =>
            $"BBBaseException(code={Code}, status={StatusCode}, network={IsNetworkError}): {Message}";
    }
}
