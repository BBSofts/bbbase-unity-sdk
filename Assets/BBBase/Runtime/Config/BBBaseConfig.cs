using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace BBBaseSdk
{
    /// <summary>
    /// 공용 Config(Remote Config) — 프로젝트 전역 설정값을 API 키만으로 읽는다(로그인 불필요).
    /// 필수 업데이트(최소 요구 버전)·원격 기능 플래그·서버 튜닝값 등에 쓴다. 값은 운영자만
    /// 대시보드/CLI 로 바꾸고, 게임 클라는 읽기 전용이다.
    ///
    /// 읽기는 서버에서 5분 캐시되므로 빠르다. 로그인 화면 이전에 호출해 강제 업데이트를
    /// 판정할 수 있다(일반 레코드 API 는 게임유저 토큰이 필요해 로그인 전엔 못 쓴다).
    ///
    /// 예)
    /// <code>
    /// var cfg = await BBBase.Config.GetAsync&lt;ForceUpdate&gt;("force_update");
    /// if (cfg != null &amp;&amp; IsOlder(Application.version, cfg.minVersion))
    ///     ShowForceUpdatePopup(cfg.message, cfg.storeUrl);
    /// </code>
    /// </summary>
    public class BBBaseConfig
    {
        private readonly BBBaseClient _client;

        public BBBaseConfig(BBBaseClient client)
        {
            _client = client;
        }

        /// <summary>
        /// key 의 <c>value</c> 를 타입 T 로 조회. 키가 없으면(CONFIG_NOT_FOUND/404) default(T).
        /// 예: <c>var f = await BBBase.Config.GetAsync&lt;ForceUpdate&gt;("force_update");</c>
        /// </summary>
        public async Task<T> GetAsync<T>(string key)
        {
            var entry = await GetRawAsync(key);
            var value = entry?["value"];
            return value == null ? default : value.ToObject<T>();
        }

        /// <summary>
        /// key 원본 응답을 조회. 성공 시 <c>{ key, value, updatedAt }</c> JObject,
        /// 키가 없으면 null. 게임유저 토큰 없이 API 키만으로 동작한다(로그인 전 호출 가능).
        /// </summary>
        public async Task<JObject> GetRawAsync(string key)
        {
            var path = $"/configs/{System.Uri.EscapeDataString(key)}";
            try { return await _client.SendProjectAsync<JObject>("GET", path, withUserToken: false); }
            catch (BBBaseException e) when (IsNotFound(e)) { return null; }
        }

        private static bool IsNotFound(BBBaseException e) =>
            e.StatusCode == 404 || e.Code == BBBaseErrorCodes.ConfigNotFound;
    }
}
