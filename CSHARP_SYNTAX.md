# STAR 코드에 쓰인 C# 문법 사전 (상세판)

> 이 프로젝트에 **실제로 등장하는** C# 문법을, 초심자도 이해하도록 **개념 → 동작 원리 → 문법 → 예시 → 주의점**으로 자세히 설명한다.
> 예시는 전부 이 리포의 실제 코드다. 파이썬을 아는 분을 위해 곳곳에 **🐍 파이썬 비교**를 넣었다.
> (스크립트별 해설은 `SCRIPTS.md` 참고.)

**읽기 전에 — C#의 큰 그림**
- C#은 **정적 타입** 언어다. 변수마다 타입(int, float, string…)이 정해져 있고, 컴파일 때 검사한다. (파이썬은 동적 타입)
- **모든 코드는 `class` 안에** 있어야 한다. 파일에 홀로 떠 있는 함수가 없다.
- 문장 끝에는 **세미콜론 `;`**, 코드 블록은 **중괄호 `{ }`** 로 묶는다. (파이썬의 들여쓰기 대신)
- 실행 순서: `Unity가 MonoBehaviour의 약속된 메서드(Awake→Start→Update→…)를 자동 호출` → 그 안에서 우리 코드가 돈다.

**목차**
1. 선언 키워드 · 2. 접근/수정자 · 3. 자료형 · 4. 제어 흐름 · 5. 연산자
6. 메서드와 매개변수 · 7. 프로퍼티 · 8. 람다와 식 본문 · 9. 델리게이트/이벤트
10. 코루틴/이터레이터 · 11. 객체 생성·초기화 · 12. 문자열 · 13. 형 변환·패턴
14. 지역 함수 · 15. 특성(Attribute) · 16. 예외 처리 · 17. 주석 · 부록

---

## 1. 선언 키워드

### `using` — 네임스페이스 가져오기
**개념**: 다른 곳에 정의된 타입을 **짧은 이름으로** 쓰게 해주는 선언. 파일 맨 위에 모아 둔다.
**동작 원리**: `using UnityEngine;`이 있으면 `UnityEngine.GameObject`를 그냥 `GameObject`로 쓸 수 있다. 없으면 매번 전체 경로를 적어야 한다.
**문법**: `using 네임스페이스이름;`
```csharp
using UnityEngine;               // GameObject, Vector2, Mathf...
using UnityEngine.InputSystem;   // Keyboard.current
using TowardTheStars.Light;      // Beam, IBeamHit (우리 프로젝트 코드)
```
**주의점**: 이름이 겹치면(예: 두 네임스페이스에 `Random`이 둘 다 있음) 컴파일러가 헷갈려 한다. 그럴 땐 전체 경로(`UnityEngine.Random`)로 명시한다.
🐍 파이썬 `import` / `from ... import ...`와 같은 역할.

### `namespace` — 이름 공간
**개념**: 타입들을 **폴더처럼 묶는** 이름 그룹. 이름 충돌을 막는다.
**동작 원리**: `TowardTheStars.Level.MapLoader`와 다른 라이브러리의 `MapLoader`가 있어도, 네임스페이스가 다르면 서로 다른 타입으로 구분된다.
```csharp
namespace TowardTheStars.Objects
{
    public class Mirror : MonoBehaviour { ... }
}
// 전체 이름은 TowardTheStars.Objects.Mirror
```

### `class` — 클래스(참조형)
**개념**: **데이터(필드) + 동작(메서드)** 를 묶은 "객체의 설계도". 게임의 거의 모든 것이 클래스다.
**동작 원리**: `new`로 이 설계도에서 실제 객체(인스턴스)를 찍어낸다. 클래스는 **참조형** — 변수엔 실제 데이터가 아니라 "데이터가 있는 곳의 주소"가 담긴다(뒤 struct와 비교).
**문법**: `[수정자] class 이름 [: 부모/인터페이스] { 멤버들 }`
```csharp
public class GateDetector : MonoBehaviour, IBeamHit
{                                     // MonoBehaviour를 상속 + IBeamHit 구현
    float _acc;                        // 필드(데이터)
    public void Interact(...) { ... }  // 메서드(동작)
}
```
🐍 파이썬 `class`와 개념 동일. `: MonoBehaviour`는 파이썬 `class GateDetector(MonoBehaviour):`의 괄호 상속과 같다. C#은 클래스 **하나만** 상속하고, 인터페이스는 여러 개 구현할 수 있다.

### `struct` — 구조체(값형)
**개념**: 클래스와 비슷하지만 **값 타입**이다. 변수에 **데이터 자체**가 담기고, 대입하면 **통째로 복사**된다.
**동작 원리**: 작고 자주 만들어지는 데이터에 유리 — 힙 할당·가비지 컬렉션(GC) 부담이 없다. 이 프로젝트에선 빛 한 줄기(`Beam`)를 스택에 수만 번 담기 때문에 struct로 만들었다.
```csharp
public struct Beam
{
    public Vector2 origin;
    public Vector2 dir;
    public float intensity;
}
Beam a = new Beam(...);
Beam b = a;          // ★ b는 a의 "복사본" (클래스라면 같은 객체를 가리켰을 것)
```
**주의점**: 복사되므로, 함수에 넘겨 안에서 바꿔도 바깥 원본은 안 변한다. "가벼운 데이터 묶음"에만 쓰고, 상태를 공유해야 하면 class를 쓴다.

### `interface` — 인터페이스(계약)
**개념**: "이런 메서드를 반드시 만들어라"라는 **약속(규약)**. 구현은 없고 서명만 있다.
**동작 원리**: 여러 클래스가 같은 인터페이스를 구현하면, 코드는 그것들을 **한 타입처럼** 다룰 수 있다. `BeamTracer`는 부딪힌 게 거울인지 프리즘인지 몰라도 `IBeamHit.Interact()`만 부르면 된다(다형성).
```csharp
public interface IBeamHit
{
    void Interact(Beam incoming, Vector2 hitCenter, List<Beam> outgoing);
}
// Mirror, Prism, GateDetector가 각자 다르게 Interact를 구현
IBeamHit hit = collider.GetComponent<IBeamHit>();
hit.Interact(...);   // 실제로 어떤 클래스든 알맞은 구현이 불림
```
🐍 파이썬의 `abc.ABC`/덕타이핑과 비슷한 목적. C#은 인터페이스로 이를 강제한다.

### `enum` — 열거형
**개념**: **이름 붙은 상수 묶음**. "가능한 값이 정해진" 상태를 표현할 때.
**동작 원리**: 내부적으론 정수지만, 코드에선 의미 있는 이름으로 쓴다. `switch`와 궁합이 좋다.
```csharp
enum State { Title, Playing, Paused, Ending }   // 0,1,2,3에 이름을 붙인 것
State _state = State.Playing;
```

---

## 2. 접근/수정자

### `public` / (생략 = `private`)
**개념**: 이 멤버를 **누가 볼 수 있는지** 정하는 접근 범위.
- `public`: 어디서든 접근(다른 스크립트, Unity 인스펙터).
- 생략하면 **`private`**: 그 클래스 **안에서만**.
**동작 원리**: 캡슐화 — 밖에서 건드리면 안 되는 내부 상태는 숨기고, 필요한 것만 연다.
```csharp
public float artScale = 2f;   // 인스펙터에 노출 + 다른 코드가 읽기/쓰기
float _acc;                   // GateDetector 내부 전용(누적 광량)
```
🐍 파이썬의 `_이름`(관례상 private)과 달리, C#의 `private`은 **컴파일러가 강제**한다.

### `static` — 정적(타입이 하나를 공유)
**개념**: 객체마다 하나씩이 아니라 **클래스 전체가 딱 하나**를 공유하는 멤버.
**동작 원리**: 인스턴스를 안 만들어도 `클래스이름.멤버`로 접근한다. 전역 플래그·유틸 함수·팩토리에 쓴다.
```csharp
public static bool ControlsLocked;   // 게임 전체가 공유하는 "입력 잠금" 하나
// PlayerController, MirrorInteractor, MapLoader가 모두 같은 이 값을 본다
PlayerController.ControlsLocked = true;   // 인스턴스 없이 접근

public static GameManager Bootstrap(MapLoader loader) { ... }   // 정적 메서드(팩토리)
```
**주의점**: 전역이라 편하지만 남발하면 상태 추적이 어려워진다. 이 프로젝트는 "게임 전체가 하나만 있어야 하는" 잠금 플래그에만 신중히 썼다.
🐍 파이썬의 클래스 변수 / `@staticmethod`와 유사.

### `readonly` — 읽기 전용(런타임 1회 대입)
**개념**: **생성자/초기화에서 한 번만** 값을 넣고, 그 뒤론 못 바꾸는 필드.
**동작 원리**: "참조(주소)"가 고정된다는 뜻. `readonly List`는 리스트를 **다른 리스트로 바꾸지는 못하지만**, 그 리스트에 `Add`/`Clear`는 된다(내용물은 바뀔 수 있음).
```csharp
readonly List<Mirror> _mirrors = new();
_mirrors.Add(mirror);   // OK — 내용 변경
_mirrors.Clear();       // OK
// _mirrors = new List<Mirror>();  ← 컴파일 에러(참조 교체 금지)
```

### `const` — 컴파일 상수
**개념**: **절대 안 변하는 값**. 컴파일 시점에 값이 코드에 박힌다.
**동작 원리**: 숫자·문자열 같은 리터럴에만 쓸 수 있고, 다른 const로 계산해 초기화할 수 있다.
```csharp
const int Z_TERRAIN = 0, Z_PLATFORM = 1, Z_OBJECT = 5, Z_SPAWN = 8;   // 렌더 정렬 순서
const float PLATFORM_THICK = 0.4f;
const float PLATFORM_TOP   = 0.5f;
const float PLATFORM_CY    = PLATFORM_TOP - PLATFORM_THICK * 0.5f;   // 다른 const로 계산 = 0.3
```
**`const` vs `readonly` 정리**: 값 자체가 영원히 불변이고 숫자/문자면 `const`. 객체·컬렉션이거나 런타임에 정해지면 `readonly`.

---

## 3. 자료형

### 기본형 `int` `float` `bool` `string` `void`
**개념**: 값의 종류. C#은 변수마다 타입이 고정된다.
```csharp
int steps = 0;          // 정수
float share = 0.5f;     // 실수 — ★ 접미사 f 필수(안 붙이면 double로 취급돼 에러)
bool passLight = true;  // 참/거짓
string name = "gate";   // 문자열(큰따옴표)
void Build() { ... }    // "반환값 없음" 표시
```
**주의점**: `int / int`는 **정수 나눗셈**이다. `5 / 2 == 2`. 소수가 필요하면 `5f / 2`처럼 실수로.
🐍 파이썬은 변수에 타입을 안 적지만, C#은 항상 적거나 `var`로 추론시킨다.

### `var` — 타입 추론
**개념**: 우변을 보고 **컴파일러가 타입을 자동 결정**. 타입을 안 적어도 되는 게 아니라, **적는 걸 컴파일러에 맡기는** 것.
**동작 원리**: 컴파일 시 확정되므로 여전히 정적 타입이다(런타임에 바뀌지 않음).
```csharp
var go = new GameObject("visual");   // 우변이 GameObject → go는 GameObject
var kb = Keyboard.current;           // Keyboard
```
**주의점**: 우변만으로 타입이 명백할 때만 쓴다(가독성). `var x = GetThing();`처럼 반환 타입이 안 보이면 오히려 헷갈린다.

### 제네릭 `<T>` — 형 매개변수
**개념**: "**어떤 타입이든** 담을 수 있게" 타입을 매개변수로 받는 컨테이너/메서드.
**동작 원리**: `List<Beam>`은 "Beam 전용 리스트"라 다른 타입을 넣으면 컴파일 에러 → 타입 안전 + 형변환 불필요.
```csharp
List<Beam> outgoing;                          // Beam만 담는 리스트
Dictionary<string, StageData> Stages;         // 키=string, 값=StageData
GetComponent<IBeamHit>();                      // T=IBeamHit인 컴포넌트를 찾아 반환
FindObjectsByType<GateDetector>(...);
```
🐍 파이썬 `list[Beam]` 타입힌트와 비슷하지만, C#은 실제 동작에 강제된다.

### 배열 `T[]`
**개념**: 같은 타입 값들의 **고정 길이** 묶음.
```csharp
public int[] Pos;                         // [x, y] 좌표
public string[] stageOrder = { "stage1", "stage2", "stage3", "stage4" };
c[0]   // 인덱스 접근(0부터)
```
**주의점**: 배열은 길이가 고정. 늘었다 줄었다 해야 하면 `List<T>`를 쓴다.

### 튜플 `( , )` — 가벼운 묶음
**개념**: 여러 값을 이름표를 달아 **임시로 묶는** 값형.
**동작 원리**: 클래스를 따로 안 만들고 "쌍/삼중"을 다룰 때. 분해(deconstruction)로 한 번에 풀 수 있다.
```csharp
Stack<(Beam beam, int depth)> _stack;      // 빔 + 재귀 깊이를 한 쌍으로
HashSet<(int, int)> transmit;              // (x, y) 좌표 집합
var (beam, depth) = stack.Pop();           // 한 번에 두 변수로 분해
```
🐍 파이썬 튜플 `(a, b)`, `a, b = pop()`과 거의 같다.

### 널 허용 값형 `T?`
**개념**: 원래 null이 될 수 없는 값형(int, Vector2 등)에 **"없음(null)"을 허용**.
**동작 원리**: 내부적으로 "값 + 값이 있나 없나 플래그"를 함께 든다. `.HasValue`로 확인, `.Value`로 꺼낸다.
```csharp
Vector2? prefabScale = null;               // 스케일이 "지정됐을 수도, 안 됐을 수도"
if (prefabScale.HasValue)
    obj.localScale = new Vector3(prefabScale.Value.x, prefabScale.Value.y, 1f);
```
**주의점**: `.HasValue` 확인 없이 `.Value`를 꺼내면 예외. 그래서 항상 `if (…HasValue)`로 감싼다.

---

## 4. 제어 흐름

### `if` / `else if` / `else`
**개념**: 조건에 따라 실행 갈래를 나눈다.
**동작 원리**: 위에서부터 조건을 검사, 처음 참인 블록만 실행. 블록이 한 줄이면 `{ }` 생략 가능.
```csharp
if (mapFile == null) { Debug.LogError(...); return; }   // 가드 후 조기 종료

if (cx < zoneCenterX) left -= inset;      // 한 줄이면 중괄호 생략 가능
else                  right += inset;
```
**주의점**: 조건은 **반드시 `bool`** 이어야 한다. 파이썬처럼 `if (someObject):`(참 같은 값)은 안 된다 → `if (obj != null)`로 명시.

### `switch` / `case` / `break` / `default`
**개념**: **한 값**을 여러 경우로 깔끔히 분기.
**동작 원리**: 값이 맞는 `case`로 점프해 실행하다 `break`에서 빠져나온다. 어디에도 안 맞으면 `default`.
```csharp
switch (_state)
{
    case State.Title:
        if (kb.anyKey.wasPressedThisFrame) StartGameFromTitle();
        break;                                   // ★ 각 case 끝에 필수
    case State.Playing:
        if (kb.escapeKey.wasPressedThisFrame) EnterPause();
        break;
    case State.Paused:
        ...
        break;
    // GridMap.DirToVector의 예:
    default:  return Vector2.zero;               // 그 외 전부
}
```
**주의점**: C#은 `break`를 빼먹으면 컴파일 에러(다음 case로 흘러내리지 않게 막아줌).

### `for` — 횟수 반복
**개념**: "초기화 ; 조건 ; 증감" 세 부분으로 도는 반복.
```csharp
for (int i = 0; i < h; i++)                 // i=0,1,…,h-1 (사다리 조각 h개)
    ...;
for (int i = transform.childCount - 1; i >= 0; i--)   // 역순 — 삭제하며 순회할 때 안전
    ...;
```
**주의점**: 리스트를 순회하며 요소를 지울 땐 **역순 for**가 안전하다(앞에서 지우면 인덱스가 밀림).

### `foreach` — 컬렉션 순회
**개념**: 배열·리스트·딕셔너리의 각 요소를 하나씩 꺼내 반복. 인덱스가 필요 없을 때 간결.
```csharp
foreach (var c in s.GateOpenZone) { ... }       // 각 셀
foreach (var kv in s.Terrain)                    // 딕셔너리: kv.Key(열), kv.Value(높이)
    if (!int.TryParse(kv.Key, out int x)) continue;
```
🐍 파이썬 `for c in list:` / `for k, v in d.items():`와 같다.
**주의점**: `foreach` 도는 중에 그 컬렉션에 Add/Remove하면 예외. 바꿔야 하면 `for`나 복사본을 쓴다.

### `while` — 조건 반복
**개념**: 조건이 참인 동안 계속.
```csharp
while (stack.Count > 0)     // 스택이 빌 때까지 빔을 하나씩 처리
{
    var (beam, depth) = stack.Pop();
    ...
    foreach (var o in outgoing) stack.Push((o, depth + 1));   // 처리 중 새 빔 추가
}
```
**주의점**: 조건이 영영 참이면 무한 루프. 이 코드는 스택이 반드시 줄도록(또는 `depth > maxDepth`로 컷) 설계돼 있다.

### `continue` — 이번 반복만 건너뛰기
```csharp
foreach (var p in s.Platforms)
{
    if (p.Missing || p.Cells == null) continue;   // 이 발판은 스킵, 다음 발판으로
    ...
}
```

### `return` — 값 반환 / 조기 종료
**개념**: 함수를 끝낸다. 값 있는 함수면 값을 돌려주고, `void`면 그냥 흐름을 끊는다("가드 패턴").
```csharp
if (_transitioning) return;             // 조건 안 맞으면 즉시 나감(중첩 if를 줄이는 가드)
public Beam Emit() => new Beam(...);    // 값 반환(식 본문)
```

---

## 5. 연산자

### 산술 `+ - * / %`
```csharp
float w = maxX - minX + 1f;
float share = incoming.intensity / outDirs.Count;   // 나눗셈
int idx = ... ; steps % 360;                        // % 나머지
```
**주의점**: 앞서 말한 정수 나눗셈(`int/int`) 주의.

### 비교 `== != < > <= >=`
**개념**: 두 값을 비교해 `bool`을 준다.
```csharp
if (depth > maxDepth) continue;
if (mapFile == null) ...
if (hit.collider != null) ...
```
**주의점**: 클래스(참조형)에 `==`를 쓰면 보통 "같은 객체냐"를 본다. Unity 오브젝트의 `== null`은 "파괴됐냐"까지 봐주게 특별히 오버라이드돼 있다.

### 논리 `&&`(그리고) `||`(또는) `!`(부정)
**개념**: 여러 조건을 조합. **`||` = "또는"**.
**동작 원리 — 단락 평가(short-circuit)**: `&&`는 앞이 거짓이면 뒤를 **아예 안 본다**. `||`는 앞이 참이면 뒤를 안 본다. → 널 검사 후 접근을 한 줄에 안전하게 쓸 수 있다.
```csharp
if (inset > 0f && s.Grid != null) ...            // 둘 다 참일 때만 실행
if (_transitioning || ControlsLocked) return;    // 하나라도 참이면 나감
if (!m.Fixed) _mirrors.Add(mirror);              // 고정이 "아니면"

// 단락 평가 활용: g가 null이면 뒤(g.IsOpen)를 안 봐서 예외가 안 남
if (g != null && g.IsOpen) return true;
```
🐍 파이썬 `and` `or` `not` ↔ C# `&&` `||` `!`.

### 대입 `=` 과 복합대입 `+= -= *=`
```csharp
angleDeg = 90f;               // 대입
_acc += incoming.intensity;   // _acc = _acc + …
remaining -= step;
sr.color = ...;
```
**주의점**: 비교는 `==`(등호 두 개), 대입은 `=`(하나). 헷갈리면 버그.

### 증감 `++ --`
```csharp
_ladderCount++;                // +1 (사다리 겹침 수)
for (...; i++) ...
```

### 삼항 `조건 ? A : B`
**개념**: 조건에 따라 **두 값 중 하나를 고르는 식**. `if/else`를 한 줄 값으로.
```csharp
var col = m.Fixed ? C_MirrorFix : C_Mirror;   // 고정이면 회색, 아니면 하늘색
var mp  = m.Fixed ? mirrorFixedPrefab : mirrorPrefab;
SpriteRenderer sr = gateDoorPrefab != null
    ? InstantiateGateDoor(...)      // prefab 있으면
    : Visual(...);                  // 없으면
```
🐍 파이썬 `A if 조건 else B`와 같다(순서만 다름).

### 널 조건 `?.` — 안전한 멤버 접근
**개념**: 앞이 `null`이면 **예외 없이 전체를 null**로 만든다.
**동작 원리**: `a?.b`는 "a가 null이면 null, 아니면 a.b". 값형 결과는 널 허용형으로 감싸진다.
```csharp
if (s.Source?.Pos == null) return;   // Source가 null이어도 예외 없이 검사
OnStateChanged?.Invoke(open);        // 구독자가 없으면(null) 호출 자체를 건너뜀
stage.Mirrors?.Count               // Mirrors가 null이면 이 식은 null
```

### 널 병합 `??` 과 널 병합 대입 `??=`
**개념**:
- `a ?? b` : a가 null이면 b를, 아니면 a를.
- `a ??= b` : a가 null일 때**만** b를 대입(한 번만 초기화하는 지연 캐싱에 유용).
```csharp
Debug.Log($"거울 {stage.Mirrors?.Count ?? 0}");   // null이면 0으로

_gates ??= Object.FindObjectsByType<GateDetector>(...);
// _gates가 아직 null일 때만 검색해 캐시 → 매 프레임 재검색을 피함
```

### 형변환 캐스트 `(타입)식`
**개념**: 값을 **다른 타입으로 명시 변환**.
```csharp
Vector2 me = transform.position;              // Vector3 → Vector2 (암시적, 안전)
((Vector2)m.transform.position - me).sqrMagnitude   // 명시적 캐스트 후 거리 계산
```
**주의점**: 안 맞는 캐스트는 런타임 예외. 참조형은 `is`/`as`로 안전하게 검사하는 게 낫다(13절).

---

## 6. 메서드와 매개변수

### 메서드 선언(= 파이썬 `def`)
**개념**: 동작을 담는 함수. **반환형 + 이름 + (매개변수)**.
```csharp
float SurfaceBelow(StageData s, int col, float y)   // float를 돌려줌
{
    ...
    return best;
}
void Build() { ... }   // void = 반환값 없음
```
🐍 파이썬 `def surface_below(self, s, col, y):` ↔ C#은 `def` 대신 **반환형**을 앞에 쓰고, `self`가 없다(대신 `this`가 암묵).

### 기본값 매개변수
**개념**: 인자를 생략하면 쓸 **기본값**. 호출을 짧게 해준다.
```csharp
GameObject Decor(string name, Vector2 pos, Color col, int order, Vector2 scale,
                 float rotZ = 0f, GameObject prefab = null, bool fitToScale = false) { ... }

Decor("spawn", pos, C_Spawn, Z_SPAWN, scale);           // 뒤 3개 생략(기본값 사용)
Decor("decoy", pos, C_Decoy, Z_OBJECT, scale, 45f);     // rotZ만 지정
```
**주의점**: 기본값 매개변수는 **뒤쪽에** 몰아야 한다(앞에 두면 생략 규칙이 꼬임).

### 이름 붙인 인자 `이름: 값`
**개념**: 어떤 매개변수인지 **이름으로 지정**. 기본값이 여러 개일 때 일부만 건너뛰거나, 읽기 좋게.
```csharp
Visual(go.transform, mp, col, Z_OBJECT, size, -m.AngleDeg,
       prefabRotZ: -m.AngleDeg + artOffset);   // prefabScale·fitToScale은 기본값
SolidDecor(..., terrainPrefab, fitToScale: true);
```
🐍 파이썬의 키워드 인자 `func(x=1)`와 같다.

### `out` 매개변수 — 결과를 "돌려받는" 인자
**개념**: 함수가 **여러 값을 반환**하게 하는 장치. 흔히 "성공했나? + 결과값"을 함께 받는 `Try...` 패턴.
**동작 원리**: `out` 변수는 함수 안에서 **반드시 대입**된다. 호출부는 그 자리에서 변수를 선언할 수 있다.
```csharp
if (!int.TryParse(kv.Key, out int x)) continue;
//         파싱 성공 여부(bool 반환)  ↑ 성공 시 결과가 x에 담김

if (s.Terrain.TryGetValue(col.ToString(), out int t) && t >= 0) ...
//   딕셔너리에 키가 있으면 true + 값이 t에
```
🐍 파이썬은 그냥 튜플로 여러 값을 반환하지만, C#은 `out`을 자주 쓴다(특히 `Try...` 메서드).

### 메서드 오버로드
**개념**: **같은 이름**, **다른 매개변수 조합**의 메서드를 여러 개 두면, 호출 형태에 맞는 게 자동 선택된다.
```csharp
// 색 사각형 버전
SpriteRenderer Visual(Transform parent, Color col, int order, Vector2 scale, float rotZ = 0f)
// 프리팹 버전(GameObject 매개변수가 추가로 있음)
SpriteRenderer Visual(Transform parent, GameObject prefab, Color col, int order, Vector2 scale, ...)
```
**주의점**: 반환형만 다르고 매개변수가 같으면 오버로드가 안 된다(구분 기준은 매개변수).

---

## 7. 프로퍼티

### 자동 프로퍼티 `{ get; set; }` / `{ get; private set; }`
**개념**: 겉보기엔 변수처럼 쓰지만 내부적으론 접근 메서드가 생기는 "속성". `private set`이면 **밖은 읽기만**.
```csharp
public bool IsOpen { get; private set; }   // 밖에서는 gate.IsOpen 읽기만, 값 변경은 클래스 안에서만
IsOpen = open;    // 클래스 내부에서만 대입 가능
```
**왜 필드 대신 프로퍼티?** 나중에 "읽을 때 계산" 같은 로직을 넣어도 사용부 코드를 안 바꿔도 되고, 읽기/쓰기 권한을 따로 줄 수 있다.
🐍 파이썬 `@property`와 목적이 같다.

### 식 본문 프로퍼티 `=> 값` (읽기 전용)
```csharp
public float AngleDeg => angleDeg;          // 내부 필드를 읽기 전용으로 노출
public bool IsTransitioning => _transitioning;
```

### 전체 getter 블록(지연 초기화 예)
```csharp
static Font UIFont
{
    get
    {
        if (_uiFont == null)                       // 처음 접근할 때 한 번만 로드
            _uiFont = Font.CreateDynamicFontFromOSFont(...);
        return _uiFont;
    }
}
```

---

## 8. 람다와 식 본문 `=>`

`=>` 기호는 두 뜻으로 쓰인다. 헷갈리기 쉬우니 구분한다.

### (a) 식 본문 멤버 — 메서드/프로퍼티를 **한 줄로**
```csharp
void LateUpdate() => Trace();                 // { Trace(); } 와 동일
void Awake() => _cam = GetComponent<Camera>();
public Beam Emit() => new Beam(transform.position, direction, intensity);
int[] EffectiveSpawn(StageData s)
    => (_reverseEntry && s.ExitSpawn != null) ? s.ExitSpawn : s.Spawn;
```

### (b) 람다식 — 이름 없는 함수
**개념**: 그 자리에서 만드는 **작은 함수**. 델리게이트/이벤트에 넘긴다.
**동작 원리**: `(매개변수) => 식/블록`. 이 프로젝트는 주로 **메서드 그룹**(이미 있는 메서드 이름 자체)을 넘기는 형태를 쓴다.
```csharp
det.OnStateChanged += door.SetOpen;   // door.SetOpen 메서드를 콜백으로 연결(메서드 그룹)
// 람다로 쓰면: det.OnStateChanged += (open) => door.SetOpen(open);
```
🐍 파이썬 `lambda x: ...`와 같은 개념(C#은 화살표 `=>`).

---

## 9. 델리게이트 / 이벤트

### 델리게이트 `System.Action` / `Action<T>`
**개념**: **"함수를 담는 변수"**. 나중에 실행하거나 콜백으로 전달한다.
**동작 원리**: `Action`은 "인자 없고 반환 없는 함수", `Action<bool>`은 "bool 하나 받는 함수"를 담는 타입.
```csharp
public System.Action OnGameComplete;       // 마지막 스테이지 클리어 시 부를 콜백
public event Action<bool> OnStateChanged;   // 문 여닫이에 상태(bool)를 전달
```

### 이벤트 구독 `+=` 과 발동 `?.Invoke()`
**개념**: `+=`로 "이 일이 생기면 이 함수도 불러줘"라고 등록, `Invoke()`로 실제로 전부 부른다.
**동작 원리**: 구독자가 여럿이면 등록된 모두가 호출된다. 아무도 없으면 델리게이트는 null이라 `?.`로 안전하게 건너뛴다.
```csharp
det.OnStateChanged += door.SetOpen;   // 수광부 상태가 바뀌면 문을 여닫이
...
OnStateChanged?.Invoke(open);         // 구독자(door.SetOpen) 실행. 없으면 무시
if (open) OnOpen?.Invoke();
```
**왜 쓰나**: 수광부(GateDetector)는 문(GateDoor)의 존재를 몰라도 된다. "상태 바뀜"만 방송하고, 관심 있는 쪽이 구독한다 → **느슨한 결합**.
🐍 파이썬엔 언어 차원의 event가 없어 콜백 리스트로 흉내 내지만, C#은 `event`로 문법화돼 있다.

---

## 10. 코루틴 / 이터레이터

### `IEnumerator` + `yield return` (코루틴)
**개념**: **여러 프레임에 걸쳐** 진행되는 동작을 한 함수처럼 쓰는 Unity 장치.
**동작 원리**: `StartCoroutine(...)`으로 시작하면, `yield return`을 만날 때마다 **멈췄다가** 조건이 끝나면 그 다음 줄부터 재개된다. 게임을 멈추지 않고 "페이드 → 빌드 → 페이드"를 순서대로 진행할 수 있다.
```csharp
IEnumerator Transition(string next, bool reverse)
{
    _transitioning = true;
    PlayerController.ControlsLocked = true;
    yield return _fader.Fade(0f, 1f);   // 페이드 아웃이 끝날 때까지 대기(그동안 게임은 진행)
    stageKey = next;
    Build();
    yield return null;                  // 딱 한 프레임 대기(빛/카메라 정착)
    yield return _fader.Fade(1f, 0f);   // 페이드 인 대기
    PlayerController.ControlsLocked = false;
    _transitioning = false;
}
StartCoroutine(Transition("stage2", false));   // 실행 시작
```
- `yield return null;` → "다음 프레임까지 한 번 쉼".
- `yield return 다른코루틴;` → "그 코루틴이 끝날 때까지 쉼".

### `yield break` — 이터레이터 즉시 종료
```csharp
static IEnumerator FadeIn(CanvasGroup cg)
{
    if (cg == null) yield break;   // 할 게 없으면 즉시 끝(코루틴의 return)
    ...
}
```

### `IEnumerable<T>` + `yield return` (이터레이터 메서드)
**개념**: 값들을 **하나씩 지연 생성**해 흘려보내는 함수. `foreach`로 소비.
**동작 원리**: 호출 즉시 전부 만들지 않고, `foreach`가 하나 달라 할 때마다 `yield return`으로 하나씩 준다(메모리 절약).
```csharp
public IEnumerable<int[]> AllWalls()
{
    foreach (var kv in Extra)
    {
        if (!kv.Key.StartsWith("wall")) continue;
        if (kv.Value is JArray arr)
            foreach (var cell in arr)
                yield return cell.ToObject<int[]>();   // 벽 셀을 하나씩 흘려보냄
    }
}
// 쓰는 쪽: foreach (var c in s.AllWalls()) { ... }
```
🐍 파이썬 **제너레이터**(`yield`)와 정확히 같은 개념.

---

## 11. 객체 생성 · 초기화

### `new` — 인스턴스 생성
**개념**: 클래스/구조체의 **실제 객체를 만든다**(생성자 호출).
```csharp
var go = new GameObject("visual");
new Beam(hitCenter, Reflect(incoming.dir), incoming.intensity);
static readonly Color C_Wall = new(0.2f, 0.2f, 0.24f);   // 타입 생략형 new() — 좌변 타입으로 추론
readonly List<Mirror> _mirrors = new();
```

### 객체 초기화자 `{ 필드 = 값, … }`
**개념**: 생성과 동시에 속성들을 채운다(생성자에 다 없어도 됨).
```csharp
new PhysicsMaterial2D("PlayerSlip") { friction = 0f, bounciness = 0f };
new Texture2D(1, 1) { filterMode = FilterMode.Point };
```

### 배열/컬렉션 초기화자 `{ … }`
```csharp
public string[] stageOrder = { "stage1", "stage2", "stage3", "stage4" };
new[] { "Malgun Gothic", "맑은 고딕", "Gulim", "Dotum" }   // 요소로 타입 추론한 배열
```

### `this` — 현재 인스턴스 가리키기
**개념**: 지금 이 객체 자신. **매개변수와 필드 이름이 겹칠 때** 필드 쪽을 명시.
```csharp
public void Init(float solutionAngle, bool isFixed, float visualAngleOffset = 0f)
{
    this.solutionAngle = solutionAngle;   // this.필드 = 매개변수
    this.isFixed = isFixed;
}
```
🐍 파이썬 `self`에 해당하지만, C#은 매개변수에 자동으로 안 붙고 필요할 때만 `this`를 쓴다.

---

## 12. 문자열

### 보간 문자열 `$"...{식}..."`
**개념**: 문자열 안에 `{ }`로 변수·식을 직접 끼워 넣는다.
**동작 원리**: 앞에 `$`를 붙이면 `{x}` 안이 값으로 치환된다.
```csharp
$"terrain_{x}_{y}"                       // x=3,y=0 → "terrain_3_0"
$"[MapLoader] 스테이지 '{stageKey}' 없음 (가능: {keys})"
$"seg_{i}"
$"완료 — 거울 {stage.Mirrors?.Count ?? 0}"   // 식도 가능
```
🐍 파이썬 f-string `f"{x}"`와 같다.

---

## 13. 형 변환 · 패턴

### 형식 패턴 `식 is 타입 변수`
**개념**: "이 값이 그 타입이냐" 검사 + **맞으면 그 타입 변수로 즉시 받기**.
```csharp
if (kv.Value is JArray arr)             // JArray면 true + arr로 캐스팅해 사용
    foreach (var cell in arr) ...
```
**동작 원리**: 검사와 캐스팅을 한 번에 → 별도 캐스트 줄이 필요 없고 안전.

### 명시적 캐스트 `(타입)식`
```csharp
Vector2 me = transform.position;                 // Vector3 → Vector2 (암시, 안전)
((Vector2)m.transform.position - me).sqrMagnitude   // 명시적 캐스트
```
**언제 무엇을?** 참조형을 다른 타입으로 볼 땐 `is`(안전)를, 숫자/벡터처럼 변환이 정의된 값형엔 `( )` 캐스트를 쓴다.

---

## 14. 지역 함수

**개념**: 메서드 **안에** 정의하는 도우미 함수. 그 메서드의 지역 변수를 그대로 쓸 수 있다.
**동작 원리**: 바깥의 `gates`를 **캡처**해서, 인자로 안 넘겨도 접근한다. 그 메서드 밖에서는 안 보인다(캡슐화).
```csharp
void EnsureUnsolvedStart(BeamTracer tracer)
{
    var gates = Object.FindObjectsByType<GateDetector>(FindObjectsSortMode.None);

    bool AnyOpen()                       // 지역 함수 — 위의 gates를 그대로 사용
    {
        foreach (var g in gates) if (g != null && g.IsOpen) return true;
        return false;
    }

    for (int attempt = 0; attempt < 20 && AnyOpen(); attempt++)
    {
        foreach (var mir in _mirrors) if (mir != null) mir.RandomizeFromSolution(mirrorRandomSteps);
        tracer.Trace();
    }
}
```
🐍 파이썬의 함수 안 함수(중첩 함수)와 같다.

---

## 15. 특성(Attribute) `[ ... ]`

**개념**: 코드(클래스·필드·메서드) 위에 붙이는 **메타데이터 꼬리표**. Unity나 직렬화기가 이걸 읽어 동작을 바꾼다. 실행 로직은 아니다.

| 특성 | 역할 | 예시 |
|---|---|---|
| `[SerializeField]` | private 필드도 **인스펙터에 노출 + 저장** | `[SerializeField] float angleDeg;` |
| `[Header("…")]` | 인스펙터에 **제목 구분선** | `[Header("아트 표시 배율")]` |
| `[Range(a,b)]` | 인스펙터에서 **슬라이더 범위** 제한 | `[Range(0.05f, 1f)] public float jumpCutMultiplier;` |
| `[RequireComponent(typeof(T))]` | 이 컴포넌트를 붙이면 **T도 자동 추가** | `[RequireComponent(typeof(Rigidbody2D))]` |
| `[ContextMenu("…")]` | 컴포넌트 **우클릭 메뉴**에 메서드 등록 | `[ContextMenu("Build")] public void Build()` |
| `[JsonProperty("…")]` | **JSON 키 ↔ C# 필드** 매핑(Newtonsoft) | `[JsonProperty("angle_deg")] public float AngleDeg;` |
| `[JsonExtensionData]` | 매핑 안 된 JSON 키를 **통째로 수집** | `[JsonExtensionData] public Dictionary<string, JToken> Extra;` |

🐍 파이썬 **데코레이터**(`@staticmethod`)와 표기가 비슷하지만, 특성은 "붙여만 두고 남이 읽는 표식"이고 데코레이터는 함수를 감싸 동작을 바꾼다는 점이 다르다.

---

## 16. 예외 처리 `try` / `catch`

**개념**: 실패할 수 있는 코드를 `try`로 감싸고, 오류가 터지면 `catch`가 잡아 **프로그램이 죽지 않게** 수습한다.
**동작 원리**: `try` 안에서 예외가 발생하면 즉시 `catch`로 점프. `catch (타입 e)`의 `e`에 오류 정보가 담긴다.
```csharp
UnifiedData data;
try
{
    data = JsonConvert.DeserializeObject<UnifiedData>(mapFile.text);   // 잘못된 JSON이면 예외
}
catch (System.Exception e)
{
    Debug.LogError($"[MapLoader] 파싱 실패: {e.Message}");   // 로그만 남기고
    return;                                                  // 안전하게 종료(크래시 방지)
}
```
🐍 파이썬 `try/except`와 같다(`except` → `catch`).

---

## 17. 주석

**개념**: 컴파일러가 무시하는 설명 글. 이 프로젝트는 **"왜 이렇게 했는지"**(의도·규약)를 주석으로 많이 남긴다.
```csharp
// 한 줄 주석 — 규약/의도 설명
public float gateExitInset = 0.5f;   // 줄 끝에도 가능

/* 여러 줄 주석
   도 되지만 이 코드는 // 위주로 쓴다 */
```

---

## 부록 A. 자주 헷갈리는 짝

| 짝 | 차이 |
|---|---|
| `==` vs `=` | **비교** vs **대입** — 가장 흔한 실수 |
| `&&` vs `&` | 논리 AND(단락 평가) vs 비트 AND — 이 코드는 비트 연산 안 씀 |
| `?.` vs `.` | 앞이 null이면 건너뜀(안전) vs 그냥 접근(null이면 예외) |
| `??` vs `?:` | **null 대체** vs 일반 **조건 선택** |
| `const` vs `readonly` | 컴파일 상수(숫자·문자) vs 런타임 1회 대입(객체·컬렉션) |
| `struct` vs `class` | **값형**(복사, GC 없음 — Beam) vs **참조형**(공유 — 대부분) |
| `=>` (식 본문) vs `=>` (람다) | 멤버를 한 줄로 vs 이름 없는 함수 |
| `out` vs `return` | 여러 값을 인자로 돌려받음(Try 패턴) vs 값 하나를 반환 |
| 배열 `T[]` vs `List<T>` | 길이 고정 vs 늘었다 줄었다(Add/Remove) |
| `void` vs 값 반환 | 돌려줄 게 없음 vs 결과를 돌려줌 |

## 부록 B. 파이썬 → C# 빠른 변환

| 파이썬 | C# |
|---|---|
| `def f(x):` | `반환형 f(타입 x) { }` |
| `class A(B):` | `class A : B { }` |
| `self` | `this` (필요할 때만) |
| `if x: … elif … else:` | `if (x) { } else if (…) { } else { }` |
| `for x in xs:` | `foreach (var x in xs) { }` |
| `and / or / not` | `&& / \|\| / !` |
| `A if c else B` | `c ? A : B` |
| `f"{x}"` | `$"{x}"` |
| `try/except E as e:` | `try { } catch (E e) { }` |
| `lambda x: x+1` | `x => x + 1` |
| `yield`(제너레이터) | `yield return`(이터레이터) |
| 튜플 `a, b = t` | `var (a, b) = t;` |
| `None` | `null` |
| 키워드 인자 `f(x=1)` | 이름 붙인 인자 `f(x: 1)` |

> 가장 큰 사고방식 차이: **C#은 타입을 미리 정하고 컴파일러가 검사한다**(정적 타입). 그래서 코드가 길지만, 실행 전에 많은 실수를 잡아준다.
