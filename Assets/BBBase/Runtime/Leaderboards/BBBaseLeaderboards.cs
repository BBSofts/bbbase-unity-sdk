using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BBBaseSdk
{
    /// <summary>
    /// 등록형 리더보드 조회(API 키). 정의 등록은 운영자가 대시보드/CLI 로 미리 한다.
    /// 점수는 레코드 저장 시 전용 테이블에 자동 동기화된다.
    /// </summary>
    public class BBBaseLeaderboards
    {
        private readonly BBBaseClient _client;

        public BBBaseLeaderboards(BBBaseClient client) => _client = client;

        /// <summary>Top-N 순위. 응답 필드 모양은 서버 버전에 따라 다를 수 있어 원문(JToken)도 함께 보존.</summary>
        public async Task<JToken> GetTopAsync(string leaderboardId, int limit = 10, int offset = 0)
        {
            var path = $"/leaderboards/{Esc(leaderboardId)}/ranks?limit={limit}&offset={offset}";
            return await _client.SendProjectAsync<JToken>("GET", path);
        }

        /// <summary>Top-N 을 <see cref="RankEntry"/> 리스트로 파싱. 응답이 배열이거나 {ranks:[...]} 형태 모두 처리.</summary>
        public async Task<RankEntry[]> GetTopEntriesAsync(string leaderboardId, int limit = 10, int offset = 0)
        {
            var token = await GetTopAsync(leaderboardId, limit, offset);
            var arr = token as JArray ?? token?["ranks"] as JArray ?? token?["entries"] as JArray;
            return arr?.ToObject<RankEntry[]>() ?? System.Array.Empty<RankEntry>();
        }

        /// <summary>특정 엔티티(보통 내 userId)의 순위. 점수 없으면 null.</summary>
        public async Task<RankEntry> GetRankAsync(string leaderboardId, string entityId)
        {
            var path = $"/leaderboards/{Esc(leaderboardId)}/ranks/{Esc(entityId)}";
            try
            {
                var token = await _client.SendProjectAsync<JToken>("GET", path);
                return token?.ToObject<RankEntry>();
            }
            catch (BBBaseException e) when (e.StatusCode == 404 ||
                                            e.Code == BBBaseErrorCodes.LeaderboardScoreNotFound)
            {
                return null;
            }
        }

        private static string Esc(string s) => System.Uri.EscapeDataString(s);
    }

    /// <summary>리더보드 순위 한 줄. 서버 응답에 따라 일부 필드는 비어있을 수 있다.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class RankEntry
    {
        [JsonProperty("rank")] public int Rank;
        [JsonProperty("entityId")] public string EntityId;
        [JsonProperty("score")] public double Score;
    }
}
