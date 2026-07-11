namespace BBBaseSdk
{
    /// <summary>
    /// 자주 만나는 BBBase 에러코드 상수. 문자열 하드코딩 대신 이 상수로 분기하라.
    /// 전체 목록은 https://api.bbbase.io/llms.txt 참고. 서버가 새 코드를 추가할 수 있으니
    /// 여기 없는 code 도 그대로 <see cref="BBBaseException.Code"/> 로 들어온다.
    /// </summary>
    public static class BBBaseErrorCodes
    {
        // ── 클라이언트 합성(서버 응답 아님) ──
        public const string NetworkError = "NETWORK_ERROR";
        public const string NotInitialized = "NOT_INITIALIZED";
        public const string NotLoggedIn = "NOT_LOGGED_IN";

        // ── 서버 ──
        public const string UnknownColumn = "UNKNOWN_COLUMN";
        public const string DuplicateValue = "DUPLICATE_VALUE";
        public const string RecordNotFound = "RECORD_NOT_FOUND";
        public const string EntityRecordNotFound = "ENTITY_RECORD_NOT_FOUND";
        public const string ConfigNotFound = "CONFIG_NOT_FOUND";
        public const string RateLimitExceeded = "RATE_LIMIT_EXCEEDED";
        public const string TooManyRequests = "TOO_MANY_REQUESTS";
        public const string LeaderboardScoreNotFound = "LEADERBOARD_SCORE_NOT_FOUND";
        public const string Unauthorized = "UNAUTHORIZED";
        public const string Forbidden = "FORBIDDEN";
        public const string AuthProviderNotConfigured = "AUTH_PROVIDER_NOT_CONFIGURED";
    }
}
