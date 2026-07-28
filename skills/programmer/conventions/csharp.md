# C# 코드 스타일 규칙

Unity C#.

이 문서는 `규칙 설명`과 `예시 코드`로 나뉜다.
실제 판단 기준은 `규칙 설명`을 우선한다.
`예시 코드`는 적용 형태를 보여주기 위한 샘플이며, 예시 안의 숫자나 주석보다 위 규칙 설명이 우선한다.

## 규칙 설명

### 최우선 적용

- 이 문서의 규칙은 C# 일반 관습, IDE 자동 정리, formatter, analyzer 제안보다 우선한다.
- C# 파일을 수정한 뒤 최종 응답 전 `using`, XML summary 구분선, `<br/>`, 메서드 내부 흐름 주석, region 구성을 다시 확인한다.
- 예시로 나온 패턴과 같은 구조는 파일 전체에 동일하게 적용한다.

### using

- 정렬 순서는 `System` → `Unity` → 외부 라이브러리 → 프로젝트 네임스페이스다.
- 그룹 사이에는 빈 줄을 둔다.
- 하위 네임스페이스를 쓰면 상위 네임스페이스도 함께 선언한다.
- 순차 선언 규칙 때문에 필요한 using은 사용하지 않는 것처럼 보여도 제거하지 않는다.
- 같은 프로젝트의 하위 네임스페이스는 namespace 블록 내부에서 상대 이름으로 선언할 수 있다.

```csharp
using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using External;
using External.Library;
```

### 중괄호와 들여쓰기

- 중괄호는 BSD 스타일(Allman)을 사용한다.
- 여는 중괄호는 새 줄에 둔다.
- 기본 들여쓰기는 공백 4칸이다.
- 제네릭 제약 조건은 클래스/메서드 선언과 같은 들여쓰기 레벨에 둔다.

### 주석과 XML summary

- 클래스, 인터페이스, 프로퍼티, 이벤트, 메서드는 XML `<summary>`를 작성한다.
- private 메서드도 summary를 작성한다.
- `<summary>` 앞뒤에는 같은 길이의 구분선을 둔다.
- 클래스/인터페이스 구분선은 `=`를 사용하고, 멤버 구분선은 `-`를 사용한다.
- 구분선은 최소 60자다.
- summary 내부 `///` 줄의 시각적 폭이 60자를 넘으면 구분선을 10자 단위로 늘린다.
- 한글은 1.5칸으로 계산한다. 예: 한글 4자 = 6칸.
- summary 설명이 한 줄이면 `<br/>`를 쓰지 않는다.
- summary 설명이 여러 줄이면 첫 줄을 포함한 모든 설명 줄을 `/// <br/>`로 시작한다.
- 주석은 현재 상태의 의도와 작동 방식을 설명한다. 변경 이력이나 작업 기록을 남기지 않는다.

### 메서드 내부 흐름 주석

- 메서드 내부의 핵심 처리 흐름에는 `//` 주석을 작성한다.
- 주석은 코드가 "무엇을 하는지"보다 "왜 이 단계가 필요한지", "어떤 상태 전환이 일어나는지", "다음 단계와 어떻게 연결되는지"를 설명한다.
- 비자명한 처리 단계가 2개 이상 이어지면 단계마다 짧은 흐름 주석을 둔다.
- 조건 분기, 조기 반환, 상태 변경, 리소스 생성/해제, 이벤트 호출, 외부 API 호출, 예외 처리에는 의도 주석을 우선 작성한다.
- 단순 대입, 단순 반환, 이름만으로 의도가 분명한 한 줄 코드는 주석을 생략할 수 있다.
- 주석은 순서 번호를 쓰지 않는다. 순서가 있더라도 `// 입력 검증`, `// 이전 값 해제`, `// 변경 이벤트 전파`처럼 의미 있는 이름을 쓴다.

```csharp
// 입력 검증: 이후 흐름은 값이 존재한다는 전제에서 동작한다.
if (value == null)
{
    throw new InvalidOperationException("값이 설정되어 있지 않습니다.");
}

// 생성 직후에는 아직 활성화하지 않고, 후속 초기화가 끝난 뒤 공개한다.
var spawnable = Instantiate(value);

// 실패 시 생성된 객체를 남기지 않도록 같은 흐름 안에서 정리한다.
if (hasError)
{
    DespawnInternal(spawnable);
}

// 외부 구독자가 활성화 직전 상태를 조정할 수 있도록 먼저 알린다.
OnBeforeSpawn(spawnable);
spawnable.SetActive(true);
```

```csharp
// ------------------------------------------------------------
/// <summary>
/// 한 줄 설명.
/// </summary>
// ------------------------------------------------------------
```

```csharp
// ----------------------------------------------------------------------
/// <summary>
/// <br/> 여러 줄 설명은 첫 줄을 포함한 모든 설명 줄에 br 태그를 붙인다.
/// <br/> 구분선은 설명 줄보다 짧지 않게 10자 단위로 늘린다.
/// </summary>
// ----------------------------------------------------------------------
```

### region

- 기본 배치 순서는 내부 데이터 → 필드 → 이벤트 → 생성자 → 복제 → 메서드 → 이벤트 핸들러 → 인터페이스 구현 → 기타다.
- 단순한 클래스는 기본 배치 순서를 따른다.
- 클래스가 크거나 기능 경계가 명확하면 기능별 region으로 더 세분화할 수 있다.
- 기능별 region 내부에서도 가능한 범위에서 기본 배치 순서를 유지한다.
- `#region`은 상위 들여쓰기 레벨을 유지하고, 내부 코드는 한 단계 더 들여쓴다.
- 내용이 없는 region은 선언하지 않는다.

### 타입과 멤버 배치

- MonoBehaviour가 아닌 클래스는 `[Serializable]` 사용을 권장한다.
- 클래스 내부 구조체/열거형은 전용 타입으로, 클래스 외부 구조체/열거형은 공용 타입으로 둔다.
- 프로퍼티와 backing field는 함께 배치한다.
- backing field가 private이면 public 프로퍼티 아래에 둔다.
- 직렬화 필드는 `[SerializeField]` 또는 `[SerializeReference]`를 사용하고 기본값을 명시한다.
- public 필드는 PascalCase를 사용한다.
- private 필드는 camelCase를 사용한다.
- 메서드는 PascalCase를 사용한다.

### 프로퍼티와 메서드

- 읽기 전용 프로퍼티가 단일 값 또는 필드를 직접 반환하면 `=>`를 사용한다.
- 로직이 있는 프로퍼티는 `get { }` 또는 `set { }` 블록을 사용한다.
- 메서드도 단순 직접 반환이면 `=>`를 사용할 수 있고, 로직이 있으면 블록을 사용한다.
- 선택적 매개변수 기본값은 허용한다.

### 생성자

- 기본 생성자는 `base()` 또는 `this()`를 명시한다.
- 매개변수 생성자는 필요한 경우 `this()` 체이닝을 사용한다.

### 제어문

- `return`, `break`, `continue`만 단문을 허용한다.
- 그 외 제어문은 단일 문장이라도 중괄호를 사용한다.

### 람다와 지역 함수

- 람다식이 메서드 파라미터로 들어가고 내용이 길어지면 중첩 메서드나 지역 함수로 추출한다.
- 긴 변수명은 의미가 유지되는 짧은 지역 변수로 할당해 호출부를 단순화한다.

### 여러 줄 파라미터

- 호출 또는 선언의 파라미터가 길어지면 여러 줄로 분리한다.
- 여러 줄 파라미터에서는 `(`와 `)`를 각각 별도 줄에 둔다.
- 괄호 배치는 Allman 스타일과 같은 방식으로 맞춘다.

## 예시 코드

아래 코드는 규칙 적용 예시다.
구체적인 판단은 위의 `규칙 설명`을 따른다.

```csharp
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using External;
using External.Library;

namespace inonego
{
    using Internal;
    using Internal.Data;

    // ============================================================
    /// <summary>
    /// 인터페이스 설명
    /// </summary>
    // ============================================================
    public interface IExampleValue
    {

    }

    // ============================================================
    /// <summary>
    /// 클래스 설명
    /// </summary>
    // ============================================================
    [Serializable]
    public abstract class Example<TKey, T>
    where TKey : IEquatable<TKey>
    where T : class, IExampleValue
    {
    #region 내부 데이터

        [Serializable]
        public struct Point
        {
            public int X;
            public int Y;
        }

        public enum State { Idle, Running, Dead }

        public enum AttachmentType
        {
            None = 0,
            Head = 1,
            Body = 2,
            Hand = 3,
        }

    #endregion

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 프로퍼티 설명
        /// </summary>
        // ------------------------------------------------------------
        public GameObject Value
        {
            get => value;
            set
            {
                var (prev, next) = (this.value, value);

                if (prev == next) return;

                if (prev != null)
                {
                    // prev에 대한 해제 작업
                }

                this.value = next;

                if (next != null)
                {
                    // next에 대한 초기화 작업
                }

                OnValueChange?.Invoke(this, new ValueChangeEventArgs { Value = 0 });
            }
        }

        [SerializeField]
        private GameObject value = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 읽기 전용 프로퍼티 설명
        /// </summary>
        // ------------------------------------------------------------
        public bool IsActive => isActive;

        [SerializeField]
        private bool isActive = false;

        // ------------------------------------------------------------
        /// <summary>
        /// 표현식이 있는 읽기 전용 프로퍼티 설명
        /// </summary>
        // ------------------------------------------------------------
        public bool HasValue
        {
            get => value != null;
        }

    #endregion

    #region 이벤트

        [Serializable]
        public struct ValueChangeEventArgs
        {
            public int Value;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이벤트 설명
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<ValueChangeEventArgs> OnValueChange = null;

    #endregion

    #region 생성자

        // ------------------------------------------------------------
        /// <summary>
        /// 기본 생성자.
        /// </summary>
        // ------------------------------------------------------------
        public Example() : base() {}

        // ------------------------------------------------------------
        /// <summary>
        /// 매개변수 생성자.
        /// </summary>
        // ------------------------------------------------------------
        public Example(GameObject value) : this()
        {
            if (this.value != null) return;

            if (value == null)
            {
                throw new ArgumentNullException("값이 null입니다.");
            }

            this.value = value;
        }

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 메서드 설명.
        /// </summary>
        // ------------------------------------------------------------
        public void Spawn()
        {
            // 입력 검증: 이후 흐름은 값이 존재한다는 전제에서 동작한다.
            if (value == null)
            {
                throw new InvalidOperationException("값이 설정되어 있지 않습니다.");
            }

            // 생성 직후에는 아직 활성화하지 않고, 후속 초기화가 끝난 뒤 공개한다.
            var spawnable = Instantiate(value);
            var hasError = false;

            // 실패 시 생성된 객체를 남기지 않도록 같은 흐름 안에서 정리한다.
            if (hasError)
            {
                DespawnInternal(spawnable);
            }

            // 외부 구독자가 활성화 직전 상태를 조정할 수 있도록 먼저 알린다.
            OnBeforeSpawn(spawnable);
            spawnable.SetActive(true);

            // 활성화 완료 후 최종 상태를 외부에 전파한다.
            OnSpawnComplete?.Invoke(this, spawnable);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 복잡한 처리 흐름을 담은 메서드로, 람다식을 중첩 메서드로 추출하고
        /// <br/> 매개변수가 많은 경우 여러 줄로 분리하여 작성하는 방식을 보여준다.
        /// </summary>
        // ----------------------------------------------------------------------
        public void ComplexMethodWithLongDescription()
        {
            // 비교 기준을 지역 함수로 고정해 검색 조건을 호출부와 분리한다.
            bool IsMatch(T item)
            {
                return item != null && Equals(item, value);
            }

            var found = items.FirstOrDefault(IsMatch);

            // 긴 접근 경로를 짧은 지역 변수로 정리해 이후 호출부를 읽기 쉽게 만든다.
            var position = spawnPointObject.transform.position;
            var rotation = spawnPointObject.transform.rotation;
            var scale    = spawnPointObject.transform.localScale;

            // 파라미터가 길어지는 호출은 괄호를 분리해 값 묶음을 명확히 보여준다.
            LongParameterMethod
            (
                position, rotation, scale,
                "Example Name", true, 100
            );

            // 상태 변경 알림은 이전/다음 상태를 한 객체로 묶어 구독자에게 전달한다.
            OnStateChanged?.Invoke
            (
                this, new StateChangedEventArgs
                {
                    Previous = State.Idle,
                    Next     = State.Running,
                }
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// private 메서드도 summary를 작성한다.
        /// </summary>
        // ------------------------------------------------------------
        private void LongParameterMethod
        (
            Vector3 position, Quaternion rotation, Vector3 scale,
            string name, bool isActive, int count
        )
        {
            // 구현 내용
        }

    #endregion

    }
}
```
