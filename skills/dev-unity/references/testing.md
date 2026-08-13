# Unity 테스트 배치와 작성 규칙

사용자가 Unity 테스트 생성·수정·삭제를 명시적으로 요청하고 `programmer`와 `test-design`에서 작업 범위를 확인한 뒤에만 적용한다.
테스트 실행도 요청 또는 합의된 검증 범위에 포함된 경우에만 수행한다.

## 파일과 폴더 구조

### 구조 적용 규칙

asmdef는 이미 생성 완료된 구조를 우선 사용한다. 기반이 없으면 승인된 새 에디터·테스트 코드를 추가하기 전에 asmdef/asmref를 선언할 수 있는 환경인지 확인한다.
기존 테스트 assembly 기반이 있으면 새 에디터·테스트 코드를 추가할 때는 asmref만 추가한다.
패키지, 샘플, 외부 PackageCache, 생성 코드, 임시 프로젝트처럼 asmdef/asmref를 추가하면 안 되거나 의미가 없는 위치가 있을 수 있다.
assembly 구성이 불가능하거나 프로젝트 정책상 허용되지 않으면 테스트 파일을 억지로 추가하지 않는다. 해당 제약을 사용자에게 먼저 알리고, 가능한 검증 방식이나 필요한 구조 변경을 정리한다.
모든 폴더는 실제 내용이 생길 때 만든다.

### 기존 asmdef 위치

```text
Tests/
├── EDIT/   -> {RuntimeEditModeTestAssembly}.asmdef
├── PLAY/   -> {RuntimePlayModeTestAssembly}.asmdef
└── Editor/ -> {EditorEditModeTestAssembly}.asmdef
```

한 폴더에 asmdef 하나를 둔다. 새 어셈블리가 필요하면 폴더를 분리해서 추가한다.

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

GUID 대신 어셈블리 이름을 직접 사용한다.

```json
{ "reference": "{어셈블리명}" }
```

### Unity Test Runner 설정

패키지 테스트가 Test Runner에 표시되어야 한다면 사용 프로젝트의 `Packages/manifest.json`에서 `testables` 설정을 확인한다.

```json
{
  "testables": ["{package-name}"]
}
```

## 테스트 작성

### 작성 전 확인

1. 테스트 assembly, asmref와 테스트 파일 위치가 현재 프로젝트에서 유효한지 확인한다.
2. assembly 구성이 불가능하거나 프로젝트 정책상 허용되지 않으면 테스트 파일을 추가하지 않고 제약을 알린다.
3. 대상 클래스나 모듈에 대응하는 기존 테스트 파일이 있는지 확인하고, 가능하면 그 파일을 사용한다.
4. 테스트가 Unity Test Runner에 노출되는지 확인한다. 패키지 테스트라면 `testables` 설정 필요 여부도 확인한다.

### 테스트 파일 단위

- 기본은 클래스 단위 또는 기존 프로젝트가 쓰는 테스트 파일 단위를 따른다.
- 특정 클래스의 기능을 기능별 파일 여러 개로 나누지 않는다.
- 새 테스트 파일은 대응 파일이 없거나 기존 파일에 넣으면 책임 경계가 실제로 흐려질 때만 만든다.
- 한 클래스의 테스트를 여러 파일로 분리해야 한다면 사용자에게 확인받는다.

### 네임스페이스

- 모듈 단위: `{프로젝트 루트}.TEST.{그룹}._{모듈명}`
- 그룹 단위: `{프로젝트 루트}.TEST.{그룹}`

`{그룹}`은 폴더 경로의 일대일 복사가 아니라 런타임 네임스페이스처럼 의미 단위로 묶은 모듈 그룹이다.
세그먼트의 `_`는 동일 이름의 형식과 네임스페이스가 충돌하는 경우를 구분한다.

단순한 모듈은 모듈 단위를 기본으로 사용한다.
여러 모듈이 강하게 연결되어 헬퍼나 픽스처를 공유하는 편이 자연스러우면 그룹 단위를 사용한다.

### 테스트 구성 머리말

파일 블록 머리말의 `# 테스트 구성` 섹션에 카테고리 코드를 정의한다.

```text
# 테스트 구성
 E: 기본 기능 ...
 S: 상태와 문맥 ...
 X: 예외 처리 ...
```

카테고리 수와 의미는 모듈의 실제 계약에 맞춘다.

### region 구성

테스트 메서드는 카테고리 코드와 논리적 흐름을 기준으로 region을 나눈다.

```csharp
#region 헬퍼
#region 픽스처
#region E-1: 설명
#region E-2: 설명
#region E-3-1: 설명
#region E-3-2: 설명
#region S-1-1: 설명
#region X-1: 설명
```

번호는 작성 순서가 아니라 논리적 흐름 순서로 매긴다.
헬퍼와 픽스처처럼 테스트가 아닌 코드는 별도 region으로 분리한다.

### 메서드 명명

`TEST_{ClassName}_{시나리오}` 형식을 사용한다. 번호는 넣지 않고 한글 시나리오를 사용할 수 있다.
번호는 region 트리에서만 관리하고, 메서드명은 Test Runner와 스택 트레이스에서 시나리오를 직접 설명하게 한다.

### 정적 상태 격리

정적 레지스트리나 싱글톤처럼 테스트 사이에 상태가 누출될 수 있는 시스템은 `[SetUp]`과 `[TearDown]`에서 상태를 준비하고 정리한다.
초기화와 정리 방식은 대상 시스템의 공개 API, 수명주기와 Unity 객체 소유권에 맞춘다.

### 새 테스트 추가

1. 시나리오에 맞는 카테고리를 정한다.
2. 기존 카테고리 region이 있으면 해당 논리적 위치에 추가한다.
3. 필요한 경우 카테고리 번호를 재정렬한다.
4. 새 카테고리가 필요하면 머리말의 `# 테스트 구성`도 함께 갱신한다.
