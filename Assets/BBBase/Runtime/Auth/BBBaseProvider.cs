namespace BBBaseSdk
{
    /// <summary>게임유저 로그인 방식. 발급되는 토큰·userId 체계는 모두 동일하다.</summary>
    public enum BBBaseProvider
    {
        /// <summary>게스트 — 기기 식별자(deviceId)로 로그인.</summary>
        Guest,
        /// <summary>구글 계정 로그인 — 게임 SDK 가 받은 idToken 으로 로그인(Play Games 아님).</summary>
        Google,
        /// <summary>앱인토스 — authorizationCode 로 로그인.</summary>
        AppsInToss
    }
}
