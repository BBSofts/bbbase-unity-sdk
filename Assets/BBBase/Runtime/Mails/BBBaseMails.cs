using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BBBaseSdk
{
    /// <summary>
    /// 우편함(메일) — 게임유저 토큰으로 내 우편을 조회/읽음/수령한다(API 키 + 로그인 필요).
    /// 발송(개인/전체발송)은 운영자가 대시보드/CLI 로 한다 — 게임 클라는 수령만 한다.
    ///
    /// 보상 수령은 서버가 원자적으로 지급한다: <see cref="ClaimAsync"/> 한 번이면 서버가 수령표시와
    /// 재화 지급을 한 트랜잭션에서 처리한다. 재수령해도 재화는 늘지 않는다(멱등, AlreadyClaimed=true).
    /// 수령 후 최신 잔액은 <c>BBBase.Records.LoadMineAsync()</c> 로 다시 읽으면 된다.
    /// </summary>
    public class BBBaseMails
    {
        private readonly BBBaseClient _client;
        private readonly BBBaseSession _session;

        public BBBaseMails(BBBaseClient client, BBBaseSession session)
        {
            _client = client;
            _session = session;
        }

        /// <summary>
        /// 내 우편함(개인 + 전체발송, 만료 제외). 기본은 미수령만.
        /// includeClaimed=true 면 이미 수령한 우편도 포함한다.
        /// </summary>
        public async Task<Mail[]> GetMailboxAsync(bool includeClaimed = false, int limit = 50)
        {
            RequireLogin();
            var path = $"/mailbox?includeClaimed={(includeClaimed ? "true" : "false")}&limit={limit}";
            var arr = await _client.SendProjectAsync<Mail[]>("GET", path, withUserToken: true);
            return arr ?? System.Array.Empty<Mail>();
        }

        /// <summary>우편 읽음 표시(멱등). 보상 지급과는 무관하다(수령은 ClaimAsync).</summary>
        public Task<JObject> MarkReadAsync(string mailId)
        {
            RequireLogin();
            return _client.SendProjectAsync<JObject>("POST", $"/mailbox/{Esc(mailId)}/read", withUserToken: true);
        }

        /// <summary>
        /// 보상 수령(원자적·멱등). 게임의 "수령" 버튼에서 이 한 번만 호출하면 서버가 재화까지 지급한다.
        /// Claimed=true 면 이번에 지급됨, AlreadyClaimed=true 면 이미 받은 우편(재화 불변).
        /// </summary>
        public Task<MailClaimResult> ClaimAsync(string mailId)
        {
            RequireLogin();
            return _client.SendProjectAsync<MailClaimResult>("POST", $"/mailbox/{Esc(mailId)}/claim", withUserToken: true);
        }

        /// <summary>수령 가능한 우편을 모두 수령. 결과는 { ClaimedCount, Totals }.</summary>
        public Task<MailClaimAllResult> ClaimAllAsync()
        {
            RequireLogin();
            return _client.SendProjectAsync<MailClaimAllResult>("POST", "/mailbox/claim-all", withUserToken: true);
        }

        private void RequireLogin()
        {
            if (!_session.IsLoggedIn)
                throw new BBBaseException(BBBaseErrorCodes.NotLoggedIn,
                    "로그인 후에 호출하세요(BBBase.Auth.Login...).", 0, false, "");
        }

        private static string Esc(string s) => System.Uri.EscapeDataString(s);
    }

    /// <summary>우편 한 통. Attachments 는 보상 { 컬럼명: 수량 }(없으면 빈 dict).</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Mail
    {
        [JsonProperty("id")] public string Id;
        [JsonProperty("audience")] public string Audience;   // USER / ALL
        [JsonProperty("title")] public string Title;
        [JsonProperty("body")] public string Body;
        [JsonProperty("attachments")] public Dictionary<string, long> Attachments = new Dictionary<string, long>();
        [JsonProperty("expiresAt")] public string ExpiresAt;
        [JsonProperty("createdAt")] public string CreatedAt;
        [JsonProperty("read")] public bool Read;
        [JsonProperty("claimed")] public bool Claimed;
    }

    /// <summary>단건 수령 결과.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class MailClaimResult
    {
        /// <summary>이번 호출로 실제 지급됐는지.</summary>
        [JsonProperty("claimed")] public bool Claimed;
        /// <summary>이미 수령한 우편이었는지(재화 불변).</summary>
        [JsonProperty("alreadyClaimed")] public bool AlreadyClaimed;
        /// <summary>이 우편의 보상 { 컬럼명: 수량 }.</summary>
        [JsonProperty("attachments")] public Dictionary<string, long> Attachments = new Dictionary<string, long>();
    }

    /// <summary>전체 수령 결과.</summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class MailClaimAllResult
    {
        /// <summary>이번에 새로 수령한 우편 수.</summary>
        [JsonProperty("claimedCount")] public int ClaimedCount;
        /// <summary>지급된 보상 합계 { 컬럼명: 합계 }.</summary>
        [JsonProperty("totals")] public Dictionary<string, long> Totals = new Dictionary<string, long>();
    }
}
