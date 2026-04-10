# 탐색 툴 규칙

실수가 잦은 규칙 모음. **MUST** 규칙은 구속력이 있으며 무조건 수행해야한다.

---

## 워크플로우

작업 시 폴더 / 파일 목록 조회 > 경로 탐색 > 경로 변환 순으로 확인하고 이후 Serena 툴을 사용한다.

### 폴더 / 파일 목록 조회

- **MUST NOT** `find` 명령어 사용

- **MUST** 공백·한글 포함 경로도 따옴표로 감싸서 `ls` 사용  
유니티에서 스크립트를 탐색할때 폴더 + cs 파일 포함 / meta 파일 제외하는 방향으로 명령어 구성

```bash
ls -p "K:\경로\폴더 이름" | grep -E '(\.cs$|/$)' | grep -v '\.meta'
```

---

### 경로 탐색

- **MUST** ls 출력으로부터 하위 폴더의 내부 경로를 추측해서 사용하지 않는다.  
— 필요하면 해당 폴더를 다시 탐색하고 정확한 구조를 확인하도록 한다.

- 출력으로 경로 조합 시 폴더 구조를 그대로 유지 — 중간 폴더 생략·재구성 금지  
  ls 예시 입력 `…/A/B/C`, 출력 `Foo.cs` → 상대 경로 `A/B/C/Foo.cs` (O) / `A/C/Foo.cs` (X)  
  Glob 예시 출력 `K:\Project\A\B\C\Foo.cs` → 상대 경로 `A/B/C/Foo.cs` (O) / `A/C/Foo.cs` (X)

- **MUST NOT** 공백 앞에 `\` 이스케이프 삽입 — `"K:\경로\폴더\ 이름"` 형식 금지  
  공백이 포함된 경로는 **전체를 따옴표로 감싸는 것으로 충분**하다: `"K:\경로\폴더 이름"`  
  한글 + 공백 혼합 폴더명도 동일하게 적용: `"K:\경로\인터페이스 및 추상 클래스"`

---

### 경로 변환

- **탐색 툴**  
  `get_symbols_overview`, `find_symbol`, `search_for_pattern` 등  
  프로젝트 루트 기준 **상대 경로** — `Assets/Scripts/Battle/BattleManager.cs`
- **기본 파일 툴** (`Read`, `Grep`, `Glob`, `Bash`)  
  **OS 절대 경로** — `K:\Project\Assets\Scripts\Battle\BattleManager.cs`

같은 파일이라도 툴에 따라 경로 형식이 다르다 — 혼용하면 FileNotFoundError 발생.  

### 범위 제어

툴 출력이 잘리거나 외부에 저장되면 범위가 너무 넓었다는 신호다.  
잘린 출력을 파싱하지 말고 더 좁은 범위로 재호출한다.

---

## Serena 툴 종류

### `get_symbols_overview`

- **MUST** `depth=99` 전달 (기본값은 0)
- **MUST** `.cs` 파일 경로 전달 — 디렉터리 불가 (ValueError 발생)  
  디렉터리만 아는 경우 → `ls`로 `.cs` 파일 목록 먼저 구한 뒤 파일별 호출  
  (경로 조합 시 파일 목록 조회 규칙 유의)

### `find_symbol`

- 파라미터는 `name_path_pattern`이며, `name_path`가 **아니다**
- **MUST** 파일 또는 디렉터리를 알고 있으면 반드시 `relative_path` 전달 — 레포 전체 검색 금지
- 레포 전체 검색이 불가피한 경우 → `max_matches`(예: 20)를 전달해 출력을 제한한다

### `find_referencing_symbols`

- **MUST** `get_symbols_overview`에서 반환된 정확한 `name_path` 전달 — 추측 금지
- 광범위하게 참조되는 타입은 `include_kinds`로 범위를 제한한다

### `search_for_pattern`

- **MUST** `paths_include_glob` / `paths_exclude_glob` / `relative_path`로 범위를 좁힌다  
— 레포 전체 검색 금지
- 심볼 이름을 알고 있으면 `find_symbol` / `find_referencing_symbols`를 우선 사용한다