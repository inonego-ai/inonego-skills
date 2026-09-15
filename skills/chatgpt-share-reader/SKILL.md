---
name: chatgpt-share-reader
description: "ChatGPT `chatgpt.com/share/...` 공유 대화를 읽거나 요약·인계·검토할 때 사용. 공유 HTML에 포함된 내부 도구·시스템·추론 기록을 실제 대화 본문과 혼동하지 않고 사용자에게 표시되는 대화 메시지만 보수적으로 추출한다."
---

# ChatGPT 공유 대화 판독

ChatGPT 공유 링크에서 실제 대화 본문을 안전하게 읽기 위한 절차다. 공유 페이지의 직렬화 데이터에는 화면 대화 외의 내부 실행 기록이 섞일 수 있으므로 원시 노드 수나 `linear_conversation` 전체를 그대로 본문으로 취급하지 않는다.

## 기본 절차

1. 입력이 `https://chatgpt.com/share/<id>` 형식인지 확인한다.
2. `scripts/read_chatgpt_share.py`를 우선 사용해 공유 HTML과 대화 데이터를 읽는다.
3. 사용자가 전체 판독을 명시하지 않았다면 기본적으로 일부 대화 메시지만 읽는다.
4. 제목, 공유 ID, 원본 대화 ID와 추출된 화면 표시 메시지를 확인한다.
5. 필요한 범위만 요약하거나 후속 작업의 컨텍스트로 사용한다.
6. 파서가 구조를 인식하지 못하거나 결과가 의심스러우면 실제 렌더링 DOM을 보조 검증으로 사용한다.

## 본문 판정 계약

`linear_conversation`은 내부 직렬화 그래프의 선형 기록이지 곧바로 UI에 표시되는 대화 메시지 목록이 아니다. 다음 기준을 통과한 메시지만 기본 본문으로 취급한다.

- `user`: 화면에서 숨김 처리되지 않았고 실제 텍스트 또는 사용자 콘텐츠가 있는 메시지.
- `assistant`: `recipient=all`, 텍스트 콘텐츠, 화면 숨김이 아닌 메시지 중 최종 응답을 우선한다.
- 한 사용자 요청 흐름이 최종 응답 없이 중단됐으면 마지막으로 화면에 표시된 진행 안내 하나만 `kind=progress`로 보존한다.
- `system`, `tool`, 추론·생각 기록, 도구 수신자 메시지는 제외한다.
- `The output of this plugin was redacted.` 같은 내부 마스킹 문구는 본문으로 취급하지 않는다.

## 부분 읽기 우선

사용자가 `적당히`, `대충`, `되는지 확인`, `앞부분만`처럼 전체 판독을 요구하지 않았다면 전체 대화를 복원하지 않는다.

기본 호출은 다음처럼 앞부분의 화면 표시 메시지 8개만 읽는다.

```bash
python scripts/read_chatgpt_share.py <share-url> --head 8
```

필요에 따라 다음을 사용한다.

```bash
python scripts/read_chatgpt_share.py <share-url> --tail 8
python scripts/read_chatgpt_share.py <share-url> --all
python scripts/read_chatgpt_share.py <url1> <url2> --head 6 --jobs 4
```

여러 URL은 동시 요청 수를 제한해 병렬로 가져올 수 있지만, 대화 하나의 내부 해석은 순차적으로 처리한다. 단일 대화를 병렬로 해석하기 위한 다중 프로세스 처리는 추가하지 않는다.

## 정확성 검증

다음 중 하나라도 맞지 않으면 추출 결과를 확정적으로 설명하지 않는다.

- HTTP 요청이 성공하지 않음.
- 공유 ID를 확인할 수 없음.
- 대화 데이터 또는 `linear_conversation`을 찾지 못함.
- 화면에 표시되는 사용자 메시지를 하나도 찾지 못함.
- 제목이나 원본 대화 ID가 기대한 링크와 모순됨.
- 결과에 도구 수신자 메시지, 추론 기록, 숨김 메시지 또는 내부 마스킹 문구가 섞임.
