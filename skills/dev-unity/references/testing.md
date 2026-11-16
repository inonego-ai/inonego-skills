# Unity 테스트 배치와 작성 규칙

루트 `AGENTS.md`의 테스트 권한 정책에 따라 Unity 테스트 변경이 승인된 경우에만 적용한다. 실행 권한과 검증 범위도 루트 `AGENTS.md`를 따른다.

## 파일과 폴더 구조

- asmdef는 이미 만들어진 구조를 우선 사용한다. 기반이 없으면 새 코드를 추가하기 전에 asmdef/asmref를 선언할 수 있는 환경인지 확인하고, 기존 테스트 assembly가 있으면 asmref만 추가한다.
- 패키지, 샘플, 외부 PackageCache, 생성 코드, 임시 프로젝트처럼 asmdef/asmref가 불가능하거나 의미 없는 위치가 있다. assembly 구성이 불가능하거나 정책상 허용되지 않으면 테스트 파일을 억지로 추가하지 말고 제약을 먼저 알린 뒤 가능한 검증 방식이나 필요한 구조 변경을 정리한다.
- 폴더는 실제 내용이 생길 때 만든다.

### 기존 asmdef 위치

```text
Tests/
├── EDIT/   -> {RuntimeEditModeTestAssembly}.asmdef
├── PLAY/   -> {RuntimePlayModeTestAssembly}.asmdef
└── Editor/ -> {EditorEditModeTestAssembly}.asmdef
```

한 폴더에 asmdef 하나를 두며, 새 어셈블리가 필요하면 폴더를 분리한다.

### 소스 모듈 옆 폴더 패턴

```text
Runtime/{모듈}/
├── {소스}.cs
├── TEST/
│   ├── EDIT/   -> {RuntimeEditModeTestAssembly}.asmref
│   ├── PLAY/   -> {RuntimePlayModeTestAssembly}.asmref
│   └── Editor/ -> {EditorEditModeTestAssembly}.asmref
└── Editor/     -> {EditorAssembly}.asmref
```

에디터 코드는 Play Mode 어셈블리를 참조하지 않으므로 `TEST/Editor/`에 Play asmref를 두지 않는다.

### asmref 규칙

| 파일 경로 | 파일명 | reference 값 |
|---|---|---|
| `TEST/EDIT/` | `{RuntimeEditModeTestAssembly}.asmref` | `{RuntimeEditModeTestAssembly}` |
| `TEST/PLAY/` | `{RuntimePlayModeTestAssembly}.asmref` | `{RuntimePlayModeTestAssembly}` |
| `TEST/Editor/` | `{EditorEditModeTestAssembly}.asmref` | `{EditorEditModeTestAssembly}` |
| `Editor/` | `Editor.asmref` | `{EditorAssembly}` |

GUID 대신 어셈블리 이름을 직접 쓴다: `{ "reference": "{어셈블리명}" }`

패키지 테스트가 Test Runner에 보여야 하면 사용 프로젝트 `Packages/manifest.json`의 `"testables": ["{package-name}"]`를 확인한다.

## 테스트 작성

### 작성 전 확인

1. 테스트 assembly, asmref, 파일 위치가 현재 프로젝트에서 유효한지 확인한다(불가능하면 파일을 추가하지 않고 제약을 알린다).
2. 대상에 대응하는 기존 테스트 파일이 있으면 그 파일을 쓴다.
3. 테스트가 Test Runner에 노출되는지 확인한다(패키지는 `testables` 필요 여부도).

### 파일 단위와 구성

- 파일은 검증 계약과 준비 환경의 응집도를 기준으로 구성하고 기존 프로젝트의 유효한 배치를 우선한다. 한 클래스의 기능을 기능별 파일 여러 개로 나누지 않으며, 새 파일은 대응 파일이 없거나 기존 파일에 넣으면 책임 경계가 실제로 흐려질 때만 만든다. 분리가 사용자가 정한 범위나 공개 구조를 실질적으로 바꿀 때만 사용자에게 확인한다.
- 검증 계약별로 묶고 불필요한 카테고리 번호·region·파일 분리를 강제하지 않는다. 기존 머리말·region은 실제 구성과 일치하게 관리하고 필요하지 않으면 새로 만들지 않는다.

### 네임스페이스

- 모듈 단위: `{프로젝트 루트}.TEST.{그룹}._{모듈명}`
- 그룹 단위: `{프로젝트 루트}.TEST.{그룹}`

`{그룹}`은 폴더 경로의 일대일 복사가 아니라 런타임 네임스페이스처럼 의미 단위로 묶은 모듈 그룹이다. 세그먼트의 `_`는 같은 이름의 형식과 네임스페이스 충돌을 구분한다. 단순한 모듈은 모듈 단위를, 여러 모듈이 헬퍼·픽스처를 공유하는 편이 자연스러우면 그룹 단위를 쓴다.

### 메서드 명명

`TEST_{ClassName}_{시나리오}` 형식이며 번호는 넣지 않고 한글 시나리오를 쓸 수 있다. 기존 region 번호는 실제 구성과 맞을 때만 유지한다.

### 정적 상태 격리

정적 레지스트리·싱글톤처럼 테스트 사이에 상태가 새는 시스템은 `[SetUp]`/`[TearDown]`에서 준비·정리하며 방식은 공개 API, 수명주기, Unity 객체 소유권에 맞춘다.

### 새 테스트 추가

기존 검증과 중복되지 않는 실제 계약이 있을 때만 추가한다. 기존 파일이 계약별 구성을 쓰면 그 위치에 배치하고 머리말·region을 실제 내용에 맞춰 갱신한다.
