namespace BBBaseSdk
{
    /// <summary>게임유저 로그인 방식. 발급되는 토큰·userId 체계는 모두 동일하다.</summary>
    public enum BBBaseProvider
    {
        /// <summary>로그인 수단 불명(미로그인 또는 세션 손상). 이 값이면 특정 provider 로 자동
        /// 로그인하지 말고 반드시 로그인 수단 선택 UI 를 띄울 것 — Guest 로 기본값을 두면 실제
        /// 구글/앱인토스 유저가 게스트 계정으로 조용히 갈아타는 사고가 난다.</summary>
        Unknown = 0,
        /// <summary>게스트 — 기기 식별자(deviceId)로 로그인.</summary>
        Guest,
        /// <summary>구글 계정 로그인 — 게임 SDK 가 받은 idToken 으로 로그인(Play Games 아님).</summary>
        Google,
        /// <summary>앱인토스 — authorizationCode 로 로그인.</summary>
        AppsInToss
    }
}
