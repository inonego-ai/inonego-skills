---
name: dev-unity
description: Use when working on Unity projects.
---

# Unity 개발

Unity 관련 작업을 할 때 사용한다.

## 대상과 증거 확인

- 프로젝트, 작업 사본, 패키지, 씬, 프리팹, 자산, 데이터의 정확한 이름과 실제 경로를 먼저 확인한다.
- 코드·데이터·씬·프리팹·문서의 상태를 서로 다른 근거로 취급한다.
- 자산이나 데이터에서 실제 소유자·실행 주체와 런타임 소비 경로까지 추적한다.
- 정적 확인, 컴파일, Test Runner, 에디터 새로 고침, 플레이 모드, 시각 확인, 직접 실행을 구분해 보고한다.
- 씬이나 프리팹 저장 뒤에는 자동 직렬화로 요청 밖 변경이 섞였는지 변경 차이를 확인하고 사용자 변경을 임의로 되돌리지 않는다.
- UI 작업은 실제 렌더링 경계와 입력 소유권을 확인하고, 상태 전환의 양방향과 입력 위치가 바뀌지 않은 경우도 검토한다.

## 테스트 권한

아래 테스트 배치·작성 규칙은 사용자가 테스트를 만들거나 추가하라고 명시하고 `programmer`와 `test-design`에서 작업 범위를 확인한 뒤에만 적용한다.
테스트 실행도 요청 또는 합의된 검증 범위에 포함된 경우에만 수행한다.

## 파일 / 폴더 구조

### 구조 적용 규칙

asmdef는 이미 생성 완료된 구조를 우선 사용한다. 기반이 없으면 승인된 새 에디터·테스트 코드를 추가하기 전에 asmdef/asmref를 선언할 수 있는 환경인지 확인한다.
기존 테스트 assembly 기반이 있으면 새 에디터·테스트 코드를 추가할 때는 asmref만 추가한다.
패키지, 샘플, 외부 PackageCache, 생성 코드, 임시 프로젝트처럼 asmdef/asmref를 추가하면 안 되거나 의미가 없는 위치가 있을 수 있다.
assembly 구성이 불가능하거나 프로젝트 정책상 금지되어 있으면 테스트 파일을 억지로 추가하지 않는다. 해당 제약을 사용자에게 먼저 알리고, 가능한 검증 방식이나 필요한 구조 변경을 정리한다.
모든 폴더는 내용이 생길 때 만든다 — 미리 생성 금지.

### 기존 asmdef 위치

```text
Tests/
├── EDIT/   -> {RuntimeEditModeTestAssembly}.asmdef
├── PLAY/   -> {RuntimePlayModeTestAssembly}.asmdef
└── Editor/ -> {EditorEditModeTestAssembly}.asmdef
```

한 폴더에 asmdef 하나. 새 어셈블리가 필요하면 폴더를 분리해서 추가한다.

### 소스 모듈 옆 폴더 패턴 (asmref)

```text
Runtime/{모듈}/
├── {소스}.cs
├── TEST/
│   ├── EDIT/   -> {RuntimeEditModeTestAssembly}.asmref
│   ├── PLAY/   -> {RuntimePlayModeTestAssembly}.asmref
│   └── Editor/ -> {EditorEditModeTestAssembly}.asmref
└── Editor/     -> {EditorAssembly}.asmref
```

**에디터 코드는 Play Mode 참조 불가** → `TEST/Editor/`에 Play asmref 없음.

### asmref 규칙

| 파일 경로 | 파일명 | reference 값 |
|---|---|---|
| `TEST/EDIT/` | `{RuntimeEditModeTestAssembly}.asmref` | `{RuntimeEditModeTestAssembly}` |
| `TEST/PLAY/` | `{RuntimePlayModeTestAssembly}.asmref` | `{RuntimePlayModeTestAssembly}` |
| `TEST/Editor/` | `{EditorEditModeTestAssembly}.asmref` | `{EditorEditModeTestAssembly}` |
| `Editor/` | `Editor.asmref` | `{EditorAssembly}` |

**파일 내용 형식 (GUID 사용 금지, 이름 직접 사용):**

```json
{ "reference": "{어셈블리명}" }
```

### Unity Test Runner 설정

패키지 테스트가 Test Runner에 표시되려면 사용 프로젝트의 `Packages/manifest.json`에 추가:

```json
{
  "testables": ["{package-name}"]
}
```

## 테스트 작성 규칙

### 테스트 작성 전 확인

테스트를 작성하기 전에 다음 순서로 배치 가능 여부와 대상 파일을 먼저 결정한다.

1. 위 파일/폴더 구조 규칙을 기준으로 테스트 assembly, asmref, 테스트 파일 위치가 유효한지 확인한다.
2. assembly 구성이 불가능하거나 프로젝트 정책상 금지되어 있으면 테스트 파일을 억지로 추가하지 말고 사용자에게 제약을 알린다.
3. 대상 클래스나 모듈에 대응하는 기존 테스트 파일이 있는지 확인하고, 가능하면 해당 파일을 사용한다.
4. 테스트가 Unity Test Runner에 노출되는지 확인한다. 패키지 테스트라면 `Packages/manifest.json`의 `testables` 설정 필요 여부도 확인한다.

### 테스트 파일 단위

테스트 파일은 대상 코드의 책임 단위와 기존 프로젝트의 테스트 구성에 맞춘다.

- 기본은 클래스 단위 또는 기존 프로젝트가 쓰는 테스트 파일 단위를 따른다.
- 특정 클래스의 기능을 기능별 테스트 파일로 쪼개서 새 파일을 여러 개 만드는 방식은 지양한다.
- 새 테스트 파일은 대응되는 테스트 파일이 없거나, 기존 파일에 넣으면 책임 경계가 실제로 흐려질 때만 만든다.
- 한 클래스의 기능을 여러 테스트 파일로 나눠야 한다고 판단되면, 사용자에게 명확히 확인받기 전에는 분리하지 않는다.

### 네임스페이스

테스트 파일은 다음 두 형식 중 하나를 사용한다.

- **모듈 단위** — `{프로젝트 루트}.TEST.{그룹}._{모듈명}`
- **그룹 단위** — `{프로젝트 루트}.TEST.{그룹}`

`{그룹}` 은 폴더 경로 1:1 매핑이 아니라 런타임 네임스페이스와 같이 **의미 단위로 묶은 모듈 그룹**이다.
세그먼트에 `_` 를 붙이는 이유는 동일 이름의 클래스/타입과 충돌 시 컴파일 오류가 나기 때문이다.

### 어떤 형식을 쓸까

- **모듈 단위 (기본):** 단순한 모듈, 모듈마다 독립된 네임스페이스 트리가 필요할 때.
- **그룹 단위:** 여러 모듈이 강하게 관련돼 있어 모듈별로 분리하면 오히려 트리가 산만해질 때, 또는 그룹 내 헬퍼/픽스처를 공유하는 게 자연스러울 때. 즉 **그룹 자체가 복잡**해서 모듈 단위로 또 나눌 필요가 없는 경우.

테스트 러너에서 의미 단위로 그룹 트리가 형성되도록 한다.
대상 모듈이 부모 네임스페이스에 속하면 자동 접근 가능하므로 별도 using 선언은 불필요하다.

### 헤더 블럭 — 테스트 구성

블럭 헤더에 `# 테스트 구성` 섹션을 추가하여 카테고리 코드를 정의한다.

```text
# 테스트 구성
 E: 기본 기능 ...
 S: 상태/컨텍스트 ...
 X: 예외 처리 ...
```

카테고리 코드는 모듈 특성에 맞게 자유롭게 정의한다.
단순한 모듈은 카테고리 1개로도 충분하며, 복잡한 모듈은 여러 카테고리로 분리한다.

### Region 구성

테스트 메서드를 카테고리 코드 기반으로 region 분리한다.

```csharp
#region 헬퍼
// 더미 클래스, 헬퍼 메서드
#region 픽스처
// SetUp / TearDown
#region E-1: 설명
// 카테고리 + 번호 + 설명
#region E-2: 설명
#region E-3-1: 설명
// 세부 분류 시 점 추가
#region E-3-2: 설명
#region S-1-1: 설명
#region X-1: 설명
```

번호는 테스트 작성 순이 아니라 **논리적 흐름** 순서로 매긴다.
헬퍼/테스트가 아닌 코드는 별도 region 으로 분리한다.

### 메서드 명명

`TEST_{ClassName}_{시나리오}` 형식. 번호 없음, 한글 시나리오 가능.

```csharp
[Test] public void TEST_{ClassName}_{Scenario1}_{Expected}() { ... }
[Test] public void TEST_{ClassName}_{Scenario2}_{Expected}() { ... }
```

번호를 메서드명에 넣지 않는 이유:

- 새 테스트 추가 시 기존 메서드명 재정렬 부담
- 메서드명 자체가 시나리오 설명이 되도록 (Test Runner / 스택 트레이스에서 식별 용이)

번호는 region 트리에서만 의미를 갖게 한다.

### 정적 상태 격리

정적 레지스트리/싱글톤 등 **테스트 간 상태 누수 가능성이 있는 시스템**을 다루는 경우, `[SetUp]` 과 `[TearDown]` 에서 테스트 상태를 명시적으로 준비하고 정리한다.
타입별 독립 상태라도 같은 타입의 다른 테스트와는 격리해야 한다.
초기화와 정리 방식은 대상 시스템의 공개 API, 수명주기, Unity 객체 소유권에 맞게 선택한다.

### 새 테스트 추가

대상 테스트 파일을 정한 뒤에는 테스트를 단순히 파일 끝에 붙이지 않는다.

1. 시나리오에 맞는 **카테고리** 결정
2. 해당 카테고리 region 이 이미 있으면 그 안 또는 직후에 끼움
3. 카테고리 번호 재정렬 (필요 시 — 예: 새 E-3 끼우면 기존 E-3 → E-4)
4. 새 카테고리가 필요하면 헤더의 `# 테스트 구성` 섹션도 갱신
