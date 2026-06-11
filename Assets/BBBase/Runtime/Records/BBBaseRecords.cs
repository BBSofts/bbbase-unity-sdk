using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BBBaseSdk
{
    /// <summary>
    /// 엔티티 레코드 저장/조회/삭제. entityType 은 자유 문자열(user/guild/season 등).
    /// 본인 데이터는 entityType="user", entityId=내 UserId 로 호출 — 이때만 서버가
    /// 소유권(경로 entityId == 토큰 userId)을 강제한다.
    ///
    /// 저장은 단순 덮어쓰기가 아니라 컬럼별 compareMode(NONE/MIN/MAX/INCREMENT)로 병합된다.
    /// 클라이언트는 "현재 기록이 더 좋은지" 비교할 필요 없이 그냥 저장하면 된다.
    /// </summary>
    public class BBBaseRecords
    {
        private readonly BBBaseClient _client;
        private readonly BBBaseSession _session;

        public BBBaseRecords(BBBaseClient client, BBBaseSession session)
        {
            _client = client;
            _session = session;
        }

        // ── 내 레코드(entityType="user", entityId=로그인한 내 UserId) 편의 메서드 ──

        /// <summary>내 유저 레코드 저장. data 는 익명객체/Dictionary/직렬화 가능한 타입.</summary>
        public Task<JObject> SaveMineAsync(object data) => SaveAsync("user", RequireUserId(), data);

        /// <summary>내 유저 레코드 조회(없으면 null).</summary>
        public Task<JObject> LoadMineAsync() => LoadAsync("user", RequireUserId());

        /// <summary>내 유저 레코드를 타입 T 로 조회(없으면 default).</summary>
        public Task<T> LoadMineAsync<T>() => LoadAsync<T>("user", RequireUserId());

        // ── 범용 ──

        /// <summary>레코드 저장(upsert + compareMode 병합). 병합된 최신 레코드를 반환.</summary>
        public async Task<JObject> SaveAsync(string entityType, string entityId, object data)
        {
            var path = $"/entities/{Esc(entityType)}/{Esc(entityId)}/record";
            return await _client.SendProjectAsync<JObject>("PUT", path, new { data }, withUserToken: true);
        }

        /// <summary>레코드 조회. 없으면 null(RECORD_NOT_FOUND 를 null 로 변환).</summary>
        public async Task<JObject> LoadAsync(string entityType, string entityId)
        {
            var path = $"/entities/{Esc(entityType)}/{Esc(entityId)}/record";
            try { return await _client.SendProjectAsync<JObject>("GET", path, withUserToken: true); }
            catch (BBBaseException e) when (IsNotFound(e)) { return null; }
        }

        /// <summary>
        /// 레코드의 <c>data</c>(JSONB)를 타입 T 로 조회. 없으면 default(T).
        /// 예: <c>var p = await Records.LoadAsync&lt;PlayerData&gt;("user", id);</c>
        /// </summary>
        public async Task<T> LoadAsync<T>(string entityType, string entityId)
        {
            var record = await LoadAsync(entityType, entityId);
            var payload = record?["data"];
            return payload == null ? default : payload.ToObject<T>();
        }

        /// <summary>레코드 삭제. 없어도 성공으로 간주.</summary>
        public async Task DeleteAsync(string entityType, string entityId)
        {
            var path = $"/entities/{Esc(entityType)}/{Esc(entityId)}/record";
            try { await _client.SendProjectAsync<object>("DELETE", path, withUserToken: true); }
            catch (BBBaseException e) when (IsNotFound(e)) { /* 이미 없음 */ }
        }

        private string RequireUserId()
        {
            if (!_session.IsLoggedIn)
                throw new BBBaseException(BBBaseErrorCodes.NotLoggedIn,
                    "로그인 후에 호출하세요(BBBase.Auth.Login...).", 0, false, "");
            return _session.UserId;
        }

        private static bool IsNotFound(BBBaseException e) =>
            e.StatusCode == 404 ||
            e.Code == BBBaseErrorCodes.RecordNotFound ||
            e.Code == BBBaseErrorCodes.EntityRecordNotFound;

        private static string Esc(string s) => System.Uri.EscapeDataString(s);
    }
}
