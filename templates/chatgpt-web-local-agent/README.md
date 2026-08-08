# ChatGPT Web 로컬 에이전트 부트스트랩 템플릿

이 디렉터리는 ChatGPT Web에서 연결된 로컬 컴퓨터를 Codex와 비슷한 방식으로 다루기 위한 부트스트랩 템플릿을 보관한다.

이 파일들은 `skills/` 아래의 스킬이 아니다. 일반 skill discovery 과정에서 자동으로 읽거나 로드하지 않는다.

## 파일

- `CHATGPT_WEB_BOOTSTRAP.template.md`
  - 로컬 작업 세션의 공통 초기화 절차 템플릿
  - 전역/프로젝트 지침 발견, 스킬 frontmatter 탐색, 스킬 지연 로드, 검증 절차를 정의한다.
- `CUSTOM_INSTRUCTIONS.template.md`
  - ChatGPT Web 맞춤 지침에 넣을 짧은 부트스트랩 포인터 템플릿

## 사용 방법

1. `CHATGPT_WEB_BOOTSTRAP.template.md`를 로컬의 별도 에이전트 컨텍스트 디렉터리에 복사한다.
2. `<GLOBAL_AGENT_INSTRUCTIONS>`, `<GLOBAL_SKILL_ROOT_*>` 같은 placeholder를 현재 환경의 실제 경로로 바꾼다.
3. `CUSTOM_INSTRUCTIONS.template.md`의 `<설치된_CHATGPT_WEB_BOOTSTRAP_경로>`를 실제 부트스트랩 경로로 바꾼다.
4. 해당 맞춤 지침을 ChatGPT Web의 사용자 맞춤 지침에 넣는다.

실사용 부트스트랩은 이 marketplace 저장소 내부에 두지 않는 것을 권장한다. 이 저장소에는 재사용 가능한 템플릿만 보관하여, 실제 Web bootstrap이 자기 자신의 템플릿을 지침이나 스킬로 중복 발견하지 않도록 한다.
