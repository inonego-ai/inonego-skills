# Xeri 패키지 지도

각 패키지의 직접 기반, 문서 시작점, Runtime 구성과 저장소 고유 규칙 위치다. 원칙과 절차는 [Xeri 스킬](../SKILL.md)에 있다. 구체 타입 목록은 두지 않으며, 기능은 스킬의 기능 탐색 절차로 찾는다.

## Xeri Core — `com.inonego.xeri`

- **직접 기반:** 없음(Unity 패키지와 ThirdParty만 사용).
- **문서:** `Documentation~/index.md`의 "목적별 찾기" 표, `Documentation~/modules/index.md`, 설계 원칙은 `Documentation~/concepts/`(architecture, ownership-and-lifetime, integration-patterns, extension-contracts).
- **Runtime:** 최상위 폴더가 책임 단위다. 폴더 아래 `README.md`가 있으면 그 모듈의 문서다.
- **저장소 규칙:** 저장소의 `.agents/` 지침과 프로젝트 스킬(테스트·에디터 구조)을 따른다.

## Xeri UI — `com.inonego.xeri.ui`

- **직접 기반:** Xeri Core.
- **문서:** `Documentation~/index.md`의 "하고 싶은 작업별 안내" 표, `Documentation~/core/`, `window/`, `architecture/`.
- **Runtime:** 최상위 폴더가 책임 단위다(Core, Window, Tray, Bar, Diagnostics, 렌더 파이프라인별 폴더 등).
- **경계:** 범용 Drag/Drop과 Picker는 Xeri Core 소유다. UI 패키지에서 다시 만들지 않는다.
- **저장소 규칙:** 저장소의 `.agents/AGENTS.md`와 프로젝트 스킬(테스트·에디터 구조)을 따른다.

## Xeri Fabric — `com.inonego.xeri.fabric`

- **직접 기반:** Xeri Core.
- **문서:** `Documentation~/index.md`의 모듈·가이드 목록, `Documentation~/concepts/architecture.md`(구조와 의존 방향), `modules/`(IO, Data, Localization, Workspace).
- **Runtime:** 최상위 폴더가 책임 단위다(IO, Data, Localization, Workspace).

## Xeri Game — `com.inonego.xeri.game`

- **직접 기반:** Xeri Core, Xeri UI, Xeri Fabric. Xeri 계열에서 의존이 가장 많으므로 새 시스템이나 재설계에서는 탐색 에이전트 위임을 기본으로 한다.
- **어셈블리 경계:** 기능 탐색 범위와 어셈블리 참조는 별개다. 저장소의 `.agents/AGENTS.md` 규칙대로 기본 Runtime 어셈블리는 Xeri Core만 참조하고, UI·Fabric 연동은 별도 어셈블리에 둔다.
- **문서:** `Documentation~/index.md`, `Documentation~/foundation/`(Entity·Spawn·Actor·World·State Machine), `Documentation~/modules/`(Action: Ability·Execution·Targeting·Delivery·Operation 등), `Documentation~/guides/`.
- **Runtime:** `Runtime/Foundation/`(기반 수명 계약), `Runtime/Modules/`(기능 모듈), 그 밖의 최상위 폴더.
- **checkout 차이:** 브랜치·worktree마다 들어 있는 모듈과 문서가 다를 수 있다. 탐색은 작업 중인 checkout 기준으로 한다.
