using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BBBaseSdk
{
    /// <summary>
    /// 리그(티어 사다리 + 주기 승격/강등) 조회(API 키). 리그 정의·승강 규칙은 운영자가
    /// 대시보드/CLI 로 미리 등록한다. 점수는 Records 로 점수 컬럼(기본 league_points)을
    /// 저장하면 자동 반영되고, league_tier/league_cohort 는 서버가 관리한다(클라가 쓰지 않음).
    /// </summary>
    public class BBBaseLeagues
    {
        private readonly BBBaseClient _client;
        private readonly BBBaseSession _session;

        public BBBaseLeagues(BBBaseClient client, BBBaseSession session)
        {
            _client = client;
            _session = session;
        }

        // ── 내 현황(로그인한 내 UserId) 편의 메서드 ──

        /// <summary>내 리그 현황(없으면 null). 티어/순위/점수.</summary>
        public Task<LeagueStatus> GetMyStatusAsync(string leagueId) =>
            GetStatusAsync(leagueId, RequireUserId());

        /// <summary>내 그룹(티어 풀=티어, 코호트=방) 안의 랭킹 top-N.</summary>
        public Task<RankEntry[]> GetMyRanksAsync(string leagueId, int limit = 30, int offset = 0) =>
            GetRanksAsync(leagueId, RequireUserId(), limit, offset);

        /// <summary>
        /// 내 지난 사이클 결과(승급 연출)를 본 뒤 확인 처리(seen=true) — 다음 조회부터 안 뜨게.
        /// 승급 애니메이션을 보여준 직후 호출한다.
        /// </summary>
        public Task<JObject> AcknowledgeResultAsync(string leagueId) =>
            AcknowledgeResultAsync(leagueId, RequireUserId());

        // ── 범용 ──

        /// <summary>
        /// 특정 엔티티의 리그 현황. 점수가 아직 없으면 null.
        /// </summary>
        public async Task<LeagueStatus> GetStatusAsync(string leagueId, string entityId)
        {
            var path = $"/leagues/{Esc(leagueId)}/me/{Esc(entityId)}";
            try
            {
                var token = await _client.SendProjectAsync<JToken>("GET", path);
                return token?.ToObject<LeagueStatus>();
            }
            catch (BBBaseException e) when (e.StatusCode == 404 ||
                                            e.Code == BBBaseErrorCodes.LeaderboardScoreNotFound)
            {
                return null;
            }
        }

        /// <summary>entityId 의 현재 티어(또는 방) 안의 랭킹 top-N(원본 {items,page})을 <see cref="RankEntry"/> 리스트로 파싱.</summary>
        public async Task<RankEntry[]> GetRanksAsync(string leagueId, string entityId, int limit = 30, int offset = 0)
        {
            var token = await GetRanksRawAsync(leagueId, entityId, limit, offset);
            var arr = token as JArray ?? token?["items"] as JArray ?? token?["ranks"] as JArray ?? token?["entries"] as JArray;
            return arr?.ToObject<RankEntry[]>() ?? System.Array.Empty<RankEntry>();
        }

        /// <summary><see cref="GetRanksAsync"/> 의 원본 응답(JToken) 버전.</summary>
        public async Task<JToken> GetRanksRawAsync(string leagueId, string entityId, int limit = 30, int offset = 0)
        {
            var path = $"/leagues/{Esc(leagueId)}/ranks/{Esc(entityId)}?limit={limit}&offset={offset}";
            return await _client.SendProjectAsync<JToken>("GET", path);
        }

        /// <summary>특정 엔티티의 지난 사이클 결과 확인 처리(seen=true). 결과가 없으면 no-op.</summary>
        public async Task<JObject> AcknowledgeResultAsync(string leagueId, string entityId)
        {
            var path = $"/leagues/{Esc(leagueId)}/me/{Esc(entityId)}/ack";
            return await _client.SendProjectAsync<JObject>("POST", path);
        }

        private string RequireUserId()
        {
            if (!_session.IsLoggedIn)
                throw new BBBaseException(BBBaseErrorCodes.NotLoggedIn,
                    "로그인 후에 호출하세요(BBBase.Auth.Login...).", 0, false, "");
            return _session.UserId;
        }

        private static string Esc(string s) => System.Uri.EscapeDataString(s);
    }

    /// <summary>리그 내 내 현황. 코호트 모델이 아니면 <see cref="Cohort"/> 는 null.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class LeagueStatus
    {
        [JsonProperty("leagueId")] public string LeagueId;
        [JsonProperty("entityId")] public string EntityId;
        [JsonProperty("tier")] public int Tier;
        [JsonProperty("cohort")] public string Cohort;
        [JsonProperty("rank")] public int Rank;
        [JsonProperty("score")] public double Score;
        [JsonProperty("total")] public int Total;
        [JsonProperty("percentile")] public double Percentile;
        /// <summary>지난 사이클 결과(승급/강등/순위). 없으면 null. 연출 후 AcknowledgeResultAsync 로 확인 처리.</summary>
        [JsonProperty("lastResult")] public LeagueResult LastResult;
    }

    /// <summary>
    /// 지난 승강 사이클 결과 스냅샷(승급 연출용). <see cref="Change"/> 는 "promote"/"demote"/"stay".
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class LeagueResult
    {
        [JsonProperty("period")] public int Period;
        [JsonProperty("tierFrom")] public int TierFrom;
        [JsonProperty("tierTo")] public int TierTo;
        [JsonProperty("change")] public string Change;   // promote / demote / stay
        [JsonProperty("rank")] public int Rank;          // 마감 시 그룹 내 순위
        [JsonProperty("groupSize")] public int GroupSize;
        [JsonProperty("prevRank")] public int? PrevRank; // 직전 사이클 순위(없으면 null)
        [JsonProperty("seen")] public bool Seen;

        /// <summary>승급했는지(연출 트리거 편의).</summary>
        public bool IsPromotion => Change == "promote";
        /// <summary>강등됐는지.</summary>
        public bool IsDemotion => Change == "demote";
    }
}
