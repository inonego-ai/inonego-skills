# C# 구조·컨벤션 검사 도구

C# 수정 작업에서 구조와 프로젝트 컨벤션을 자동 검사할 때 적용한다. 일반 코드 작업에는 이 문서를 미리 읽지 않는다.

## 도구

[`csharp-tool.ps1`](../scripts/csharp-tool.ps1)은 `.cs` 파일과 디렉터리를 하나 이상 받을 수 있다. 디렉터리는 `bin`/`obj`와 reparse-point 하위를 제외하고 재귀 탐색하며, 명시 파일과 디렉터리 결과가 겹치면 전체 경로 기준으로 중복 제거한다.

```powershell
scripts/csharp-tool.ps1 inspect File.cs
scripts/csharp-tool.ps1 check FileA.cs FileB.cs
scripts/csharp-tool.ps1 review Assets\Scripts
scripts/csharp-tool.ps1 review File.cs Assets\Scripts --format json --timing
```

- `inspect`: 선언·summary·region·Block Header 구조만 확인한다.
- `check`: 컨벤션 진단만 확인한다.
- `review`: 구조와 컨벤션을 함께 확인하며 C# 수정 전후의 기본 명령으로 사용한다.

XML 문서 주석 검사는 `public`의 `<summary>` 누락(`CSHARP-SUMMARY-001`)과, `<param>`·`<typeparam>`·`<returns>` 등 작성된 `///` 주석이 구분선 밖에 남거나 블록이 분리된 경우(`CSHARP-SUMMARY-010`)를 구분한다. 관련 규칙 ID는 의미 순서로 배치되며, 경계 검사를 추가하면서 기존 폭·줄바꿈 규칙 ID는 `011`·`012`·`013`으로 조정했다. `<summary>` 외 태그의 작성 자체를 강제하지 않는다.

## 옵션과 결과

- `--format text|json`: 출력 형식을 선택한다. 기본값은 `text`다.
- `--timing`: 누적 분석·규칙·렌더링 시간과 파일당 평균을 stderr에 출력한다.
- `--no-build`: 현재 source hash의 완전한 wrapper 캐시가 있을 때만 빌드를 생략한다. 캐시가 없거나 실행 필수 산출물이 빠져 있으면 종료 코드 4다.
- 파싱 오류가 있는 파일은 복구된 구조를 `inspect`/`review`에 표시할 수 있지만, 신뢰할 수 없는 AST 기반 컨벤션 진단은 계산하지 않고 종료 코드 2로 처리한다.

종료 코드는 다음과 같다.

- `0`: ERROR 없음
- `1`: 컨벤션 ERROR
- `2`: 파싱·입력 분석 오류
- `3`: 잘못된 명령 또는 인수
- `4`: 도구 빌드·실행 실패

## 적용 방식

한 작업에서 여러 C# 파일을 다루면 파일마다 프로세스를 다시 실행하지 말고 변경 대상 파일이나 공통 디렉터리를 한 번의 호출에 묶는다.

수정 전에는 필요한 경우 `review`로 기존 구조와 위반을 확인하고, 수정 후에는 이번에 변경한 모든 `.cs`를 한 번의 `review`로 재검사한다.

도구 출력은 원본 읽기를 대체하지 않는다. 진단하지 않는 의미적 규칙도 통과한 것으로 간주하지 않고, 수정한 책임 범위에서 [C# 코드 스타일 규칙](../conventions/csharp.md)을 직접 적용한다.
