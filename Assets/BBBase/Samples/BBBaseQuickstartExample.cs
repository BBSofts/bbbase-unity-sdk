using UnityEngine;

namespace BBBaseSdk.Samples
{
    /// <summary>
    /// 복붙용 BBBase 최소 예제. 빈 GameObject 에 붙이면 시작 시:
    /// 게스트 로그인 → 내 기록 불러오기 → 저장 → 랭킹 조회 순으로 실행한다.
    ///
    /// 사전: 메뉴 'BBBase ▸ Settings' 로 baseUrl/projectId/apiKey 를 입력해 둘 것.
    /// </summary>
    public class BBBaseQuickstartExample : MonoBehaviour
    {
        [Tooltip("운영자가 등록한 리더보드 ID (비우면 랭킹 조회 생략)")]
        public string leaderboardId = "";

        private async void Start()
        {
            BBBase.Init();
            if (!BBBase.IsInitialized) return;

            // 0) 세션 만료 구독(선택이지만 권장). 액세스 토큰 만료(1시간)는 SDK 가 자동 refresh
            //    하므로 게임이 신경 쓸 필요 없다. 리프레시 토큰까지 만료돼(오래 미접속) 자동
            //    복구가 불가능할 때만 이 이벤트가 오며, 이때 provider 별 재로그인을 띄운다.
            BBBase.SessionExpired += OnSessionExpired;

            try
            {
                // 1) 게스트 로그인 (기기 식별자 자동). 구글이면 LoginGoogleAsync(idToken).
                await BBBase.Auth.LoginGuestAsync();
                Debug.Log($"로그인 OK. userId={BBBase.UserId}");

                // 2) 내 기록 불러오기 (없으면 null)
                var me = await BBBase.Records.LoadMineAsync();
                Debug.Log(me == null ? "신규 유저" : $"내 기록: {me["data"]}");

                // 3) 이번 판 기록 저장 — best_time 은 서버에서 MIN 병합(더 빠를 때만 갱신)
                await BBBase.Records.SaveMineAsync(new { best_time = 4.35, stars = 120 });
                Debug.Log("저장 완료");

                // 4) 랭킹 Top 10
                if (!string.IsNullOrEmpty(leaderboardId))
                {
                    var top = await BBBase.Leaderboards.GetTopEntriesAsync(leaderboardId, 10);
                    foreach (var r in top)
                        Debug.Log($"#{r.Rank} {r.EntityId} = {r.Score}");
                }
            }
            catch (BBBaseException e)
            {
                // 항상 e.Code 로 분기한다(메시지는 사람용).
                switch (e.Code)
                {
                    case BBBaseErrorCodes.DuplicateValue: Debug.LogWarning("중복 값(닉네임 등)"); break;
                    case BBBaseErrorCodes.UnknownColumn: Debug.LogError("스키마에 없는 컬럼 — 운영자가 먼저 정의 필요"); break;
                    case BBBaseErrorCodes.RateLimitExceeded: Debug.LogWarning("호출 한도 초과 — 잠시 후 재시도"); break;
                    default: Debug.LogError($"BBBase 오류: {e.Code} / {e.Message}"); break;
                }
            }
        }

        /// <summary>세션이 완전히 만료돼 자동 복구가 불가능할 때 호출된다(재로그인 필요).</summary>
        private async void OnSessionExpired(BBBaseProvider provider)
        {
            Debug.LogWarning($"세션 만료 — 재로그인 필요 (provider={provider})");
            switch (provider)
            {
                case BBBaseProvider.Google: break;       // 구글 재인증 → LoginGoogleAsync(newIdToken)
                case BBBaseProvider.AppsInToss: break;   // 앱인토스 재인증 → LoginAppsInTossAsync(code)
                default:
                    await BBBase.Auth.LoginGuestAsync();  // 게스트: 같은 기기면 같은 계정 복구
                    break;
            }
        }
    }
}
