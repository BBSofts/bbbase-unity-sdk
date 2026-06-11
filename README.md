# BBBase Unity SDK

BBBase BaaS 공식 Unity SDK. 게스트/소셜 로그인, 레코드 저장·조회(compareMode 병합),
리더보드 조회를 **async/await** 로 제공합니다.

> 이 레포는 [BBSofts/BBBase](https://github.com/BBSofts/BBBase) 모노레포의 `sdk/unity/` 에서
> 자동 동기화됩니다(원본은 모노레포). 직접 수정하지 마세요.

## 설치 — 둘 중 하나

### 1) `.unitypackage` 임포트 (권장 — 가장 간단)

1. [Releases](https://github.com/BBSofts/bbbase-unity-sdk/releases/latest) 에서
   `BBBase.unitypackage` 다운로드
2. Unity 에서 **Assets ▸ Import Package ▸ Custom Package** 로 임포트
3. 메뉴 **BBBase ▸ Settings** → `Resources/BBBaseSettings.asset` 생성 → `Base Url`/`Project Id`/`Api Key` 입력

### 2) UPM (Package Manager — git URL)

Unity **Window ▸ Package Manager ▸ + ▸ Add package from git URL** 에 입력:

```
https://github.com/BBSofts/bbbase-unity-sdk.git?path=/Assets/BBBase
```

> 두 방법 모두 의존성 **Newtonsoft Json**(`com.unity.nuget.newtonsoft-json`)이 필요합니다.
> `.unitypackage` 는 임포트 직후 에디터 부트스트랩이 자동 설치하고, UPM 은 `package.json` 의
> `dependencies` 로 함께 설치됩니다.

## 사용

```csharp
using BBBaseSdk;

async void Start()
{
    BBBase.Init();                                            // Resources 설정 로드
    await BBBase.Auth.LoginGuestAsync();                      // 게스트 로그인
    await BBBase.Records.SaveMineAsync(new { best_time = 4.35, stars = 120 });
    var me  = await BBBase.Records.LoadMineAsync();           // 없으면 null
    var top = await BBBase.Leaderboards.GetTopEntriesAsync("LB_ID", 10);
}
```

## 더 알아보기

- 퀵스타트: https://api.bbbase.io/quickstart/unity
- 연동 규약(전체): https://api.bbbase.io/llms.txt
- 대시보드: https://bbbase.io

## 라이선스

© BBSofts. 자세한 내용은 별도 안내.
