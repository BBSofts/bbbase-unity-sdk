# BBBase Unity SDK

BBBase BaaS 공식 Unity SDK. 게스트/소셜 로그인, 레코드 저장·조회(compareMode 병합),
리더보드 조회를 **async/await** 로 제공한다. REST 직접 호출 대신 임포트 후 설정값만 입력하면 된다.

## 설치

1. `BBBase.unitypackage` 임포트 (Assets ▸ Import Package ▸ Custom Package).
2. 메뉴 **BBBase ▸ Settings** → `Resources/BBBaseSettings.asset` 생성 → 인스펙터에
   `baseUrl` / `projectId` / `apiKey` 입력 (대시보드 https://bbbase.io 에서 발급).

> **의존성(Newtonsoft Json)은 자동 설치된다.** 임포트 직후 SDK 의 에디터 부트스트랩이
> `com.unity.nuget.newtonsoft-json` 이 없으면 자동으로 추가한다(콘솔에 설치 로그가 찍힌다).
> 별도 조치 불필요. 만약 자동 설치가 실패하면 Package Manager ▸ `+` ▸ *Add package by name* ▸
> `com.unity.nuget.newtonsoft-json` 로 직접 추가하면 된다.
>
> UPM(git URL)로 설치하면 `package.json` 의 `dependencies` 로 Newtonsoft 가 함께 설치된다.

## 사용

```csharp
using BBBaseSdk;

async void Start()
{
    BBBase.Init();                                   // Resources 설정 로드

    await BBBase.Auth.LoginGuestAsync();             // 게스트 (기기 식별자 자동)
    // await BBBase.Auth.LoginGoogleAsync(idToken);  // 구글 (게임 SDK 가 받은 idToken)
    // await BBBase.Auth.LoginAppsInTossAsync(code); // 앱인토스

    await BBBase.Records.SaveMineAsync(new { best_time = 4.35, stars = 120 });
    var me  = await BBBase.Records.LoadMineAsync();                 // JObject (없으면 null)
    var top = await BBBase.Leaderboards.GetTopEntriesAsync("LB_ID", 10);
}
```

타입 매핑 조회:

```csharp
[System.Serializable] class PlayerData { public float best_time; public int stars; }
var p = await BBBase.Records.LoadMineAsync<PlayerData>();
```

## 핵심 규칙

- **저장은 덮어쓰기가 아님** — 컬럼별 compareMode(`NONE`/`MIN`/`MAX`/`INCREMENT`)로 서버가 병합.
  클라이언트는 비교 없이 그냥 저장하면 더 좋은 기록일 때만 갱신된다.
- **에러는 `BBBaseException.Code` 로 분기** (`BBBaseErrorCodes` 상수). 메시지는 사람용.
- **userId 는 BBBase 가 발급** — 직접 만들지 말 것. 본인 레코드는 `entityType="user"` + 내 `UserId`.
- 컬럼/리더보드/유니크 제약/프로바이더 client ID 는 **운영자가 대시보드·CLI 로 사전 정의**.

## 더 알아보기

- 퀵스타트: https://api.bbbase.io/quickstart/unity
- 연동 규약(전체): https://api.bbbase.io/llms.txt
- 정확한 엔드포인트·필드: https://api.bbbase.io/docs-json
