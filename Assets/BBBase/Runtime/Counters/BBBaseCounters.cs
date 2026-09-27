using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BBBaseSdk
{
    /// <summary>
    /// 공유 카운터 — 여러 유저가 함께 올리는 숫자(로비 현황판, 커뮤니티 목표, 길드 기여도,
    /// 난이도별 시도/클리어 수). 카운터 정의(구간·그룹·증가폭 상한·유저당 상한·공개여부)는
    /// 운영자가 대시보드/CLI 로 미리 등록한다.
    ///
    /// ⚠️ 클라는 값을 쓸 수 없고 <b>증가량(+delta)만 요청</b>한다 — 레코드에 직접 숫자를 쓰던 방식과
    ///    달리 임의 값(999999)·음수·삭제가 원천 차단된다. 실제 몇이 됐는지는 서버가 정하고,
    ///    증가 응답의 <see cref="CounterValue.Value"/> 로 돌려준다.
    ///
    /// 값은 카운터의 구간(window)별로 따로 쌓인다. DAILY 면 카운터 타임존 자정에 새 구간이
    /// 시작되고 지난 구간 값은 이력으로 남는다(운영자만 조회). 읽기에는 5초 캐시가 있어 방금
    /// 올린 값이 아주 잠깐 늦게 보일 수 있다(증가 시 즉시 무효화하므로 보통은 바로 반영된다).
    ///
    /// 예) 판이 끝날 때 +1, 로비에서 현황 표시
    /// <code>
    /// await BBBase.Counters.IncrementAsync("today_attempts", "easy");
    /// var snapshot = await BBBase.Counters.GetValueAsync("today_attempts");
    /// attemptsLabel.text = $"오늘 시도 {snapshot.Total}";
    /// </code>
    /// </summary>
    public class BBBaseCounters
    {
        private readonly BBBaseClient _client;
        private readonly BBBaseSession _session;

        public BBBaseCounters(BBBaseClient client, BBBaseSession session)
        {
            _client = client;
            _session = session;
        }

        /// <summary>
        /// 카운터를 <paramref name="delta"/> 만큼 올린다. <paramref name="group"/> 은 카운터에 그룹이
        /// 정의돼 있으면 필수이고, 없으면 null 로 둔다(그룹 없는 카운터에 group 을 보내면 400 INVALID_INPUT).
        /// 반환은 증가 후 현재 구간 값.
        ///
        /// <b>게임유저 토큰이 필수</b>다(유저당 상한 판정·제재 적용). 로그인 전에 호출하면 서버에
        /// 가기 전에 <c>NOT_LOGGED_IN</c> <see cref="BBBaseException"/> 이 난다 — 값 읽기
        /// (<see cref="GetValueAsync"/>)는 로그인 없이도 된다.
        ///
        /// 주요 실패(<see cref="BBBaseException.Code"/>):
        /// <list type="bullet">
        /// <item><c>COUNTER_LIMIT_EXCEEDED</c>(429) — 이 유저가 이번 구간에 더할 수 있는 총량을 다 썼다.
        /// <b>재시도하지 말 것</b>(다음 구간 전까지 계속 실패한다). "오늘은 여기까지"를 UI 로 안내하라.
        /// details 에 perUserLimit / windowKey 가 온다.</item>
        /// <item><c>INVALID_COUNTER_DELTA</c>(400) — delta 가 카운터의 maxDelta 를 넘었다(정의 확인).</item>
        /// <item><c>COUNTER_NOT_FOUND</c>(404) — 이름 오타이거나 운영자가 아직 등록하지 않았다.</item>
        /// <item><c>USER_BANNED</c>(403) — 제재된 계정.</item>
        /// </list>
        /// </summary>
        public Task<CounterValue> IncrementAsync(string counterName, string group = null, int delta = 1)
        {
            RequireLogin();
            var path = $"/counters/{Esc(counterName)}/incr";
            var body = new Dictionary<string, object> { ["delta"] = delta };
            if (!string.IsNullOrEmpty(group)) body["group"] = group;
            return _client.SendProjectAsync<CounterValue>("POST", path, body, withUserToken: true);
        }

        /// <summary>
        /// 현재 구간의 값을 읽는다(API 키만 — 로그인 전 로비에서도 호출 가능).
        /// <paramref name="group"/> 을 주면 그 그룹만, 생략하면 정의된 모든 그룹이 함께 온다.
        /// 그룹을 쓰지 않는 카운터는 <see cref="CounterSnapshot.Values"/> 가 group=null 한 줄이다.
        ///
        /// visibility=OPERATOR 로 등록된 카운터는 게임 키로 못 읽는다(403 FORBIDDEN).
        /// </summary>
        public Task<CounterSnapshot> GetValueAsync(string counterName, string group = null)
        {
            var path = $"/counters/{Esc(counterName)}/value";
            if (!string.IsNullOrEmpty(group)) path += $"?group={Esc(group)}";
            return _client.SendProjectAsync<CounterSnapshot>("GET", path, withUserToken: false);
        }

        private void RequireLogin()
        {
            if (!_session.IsLoggedIn)
                throw new BBBaseException(BBBaseErrorCodes.NotLoggedIn,
                    "로그인 후에 호출하세요(BBBase.Auth.Login...).", 0, false, "");
        }

        private static string Esc(string s) => System.Uri.EscapeDataString(s);
    }

    /// <summary>증가(<see cref="BBBaseCounters.IncrementAsync"/>) 결과 — 증가 후 현재 구간 값.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class CounterValue
    {
        [JsonProperty("name")] public string Name;
        /// <summary>그룹을 쓰지 않는 카운터면 null.</summary>
        [JsonProperty("group")] public string Group;
        /// <summary>"ALL_TIME" / "DAILY" / "WEEKLY" / "MONTHLY".</summary>
        [JsonProperty("window")] public string Window;
        /// <summary>구간 식별자(DAILY 면 "2026-09-20"). 이 값이 바뀌면 새 구간이 시작된 것.</summary>
        [JsonProperty("windowKey")] public string WindowKey;
        /// <summary>증가 후 현재 구간 값(서버가 정한 값 — 클라가 쓰지 않는다).</summary>
        [JsonProperty("value")] public long Value;
    }

    /// <summary>읽기(<see cref="BBBaseCounters.GetValueAsync"/>) 결과 — 현재 구간의 그룹별 값과 합계.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class CounterSnapshot
    {
        [JsonProperty("name")] public string Name;
        [JsonProperty("window")] public string Window;
        [JsonProperty("windowKey")] public string WindowKey;
        /// <summary>구간 경계 기준 타임존(IANA, 예 "Asia/Seoul").</summary>
        [JsonProperty("timezone")] public string Timezone;
        /// <summary>그룹별 값. 그룹을 쓰지 않는 카운터는 Group=null 한 줄.</summary>
        [JsonProperty("values")] public CounterGroupValue[] Values;
        /// <summary>모든 그룹의 합(group 을 지정해 읽었다면 그 그룹 값).</summary>
        [JsonProperty("total")] public long Total;

        /// <summary>특정 그룹의 값(없으면 0). group=null 이면 그룹 미사용 카운터의 값.</summary>
        public long ValueOf(string group)
        {
            if (Values == null) return 0;
            foreach (var v in Values)
                if (v.Group == group) return v.Value;
            return 0;
        }
    }

    /// <summary>한 그룹의 현재 구간 값.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class CounterGroupValue
    {
        /// <summary>그룹을 쓰지 않는 카운터면 null.</summary>
        [JsonProperty("group")] public string Group;
        [JsonProperty("value")] public long Value;
    }
}
