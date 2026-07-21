using UnityEngine;

namespace BBBaseSdk
{
    /// <summary>현재 로그인 세션. 토큰을 보관하고, 옵션에 따라 PlayerPrefs 로 영속화한다.</summary>
    public class BBBaseSession
    {
        private const string KeyUserId = "bbbase.userId";
        private const string KeyAccess = "bbbase.accessToken";
        private const string KeyRefresh = "bbbase.refreshToken";
        private const string KeyProvider = "bbbase.provider";

        private readonly bool _persist;

        /// <summary>BBBase 가 발급한 게임유저 ID(uuid). 직접 만들지 말 것.</summary>
        public string UserId { get; private set; }
        public string AccessToken { get; private set; }
        public string RefreshToken { get; private set; }

        /// <summary>마지막으로 로그인한 방식(재로그인 UI 분기에 유용).</summary>
        public BBBaseProvider Provider { get; private set; }

        public bool IsLoggedIn => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(UserId);

        public BBBaseSession(bool persist)
        {
            _persist = persist;
            if (_persist) Restore();
        }

        public void Set(BBBaseProvider provider, string userId, string accessToken, string refreshToken)
        {
            Provider = provider;
            UserId = userId;
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            if (_persist) Save();
        }

        public void UpdateTokens(string accessToken, string refreshToken)
        {
            AccessToken = accessToken;
            RefreshToken = refreshToken;
            if (_persist) Save();
        }

        public void Clear()
        {
            UserId = AccessToken = RefreshToken = null;
            Provider = BBBaseProvider.Unknown;
            if (_persist)
            {
                PlayerPrefs.DeleteKey(KeyUserId);
                PlayerPrefs.DeleteKey(KeyAccess);
                PlayerPrefs.DeleteKey(KeyRefresh);
                PlayerPrefs.DeleteKey(KeyProvider);
                PlayerPrefs.Save();
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(KeyUserId, UserId ?? "");
            PlayerPrefs.SetString(KeyAccess, AccessToken ?? "");
            PlayerPrefs.SetString(KeyRefresh, RefreshToken ?? "");
            PlayerPrefs.SetString(KeyProvider, Provider.ToString());
            PlayerPrefs.Save();
        }

        private void Restore()
        {
            UserId = PlayerPrefs.GetString(KeyUserId, "");
            AccessToken = PlayerPrefs.GetString(KeyAccess, "");
            RefreshToken = PlayerPrefs.GetString(KeyRefresh, "");
            if (System.Enum.TryParse(PlayerPrefs.GetString(KeyProvider, "Unknown"), out BBBaseProvider p))
                Provider = p;
            else
                Provider = BBBaseProvider.Unknown;
        }
    }
}
