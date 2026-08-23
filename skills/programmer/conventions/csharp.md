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
- 서로 다른 최상위 그룹 사이에는 빈 줄을 한 줄 둔다.
- 하위 네임스페이스를 선언하면 실제로 존재하는 상위 네임스페이스를 중간 단계까지 모두 함께 선언한다.
- `A.B.C`를 선언하면 `A` → `A.B` → `A.B.C` 순서로 배치한다.
- 순차 선언을 위해 포함한 상위·중간 네임스페이스는 해당 파일에서 형식을 직접 사용하지 않더라도 제거하지 않는다.
- IDE 자동 정리, formatter, analyzer의 미사용 using 제거 제안보다 순차 선언 규칙을 우선한다.
- 이 규칙을 `System.Collections` 한 사례에만 적용하지 않고 모든 네임스페이스 계층에 동일하게 적용한다.
- 같은 프로젝트의 하위 네임스페이스는 namespace 블록 내부에서 상대 이름으로 선언할 수 있다.

```csharp
/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : Example.cs
수정일 : YYYY-MM-DD

# 설명
코드 스타일 규칙을 한 파일에 적용한 예시다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using UnityEngine;
using UnityEngine.EventSystems;

using inonego;
using inonego.Xeri;
using inonego.Xeri.UI;
```

위 예시는 형식을 직접 사용하는 using만 추린 최소 목록이 아니다.
하위 네임스페이스까지 이어지는 계층을 상위 단계부터 빠짐없이 선언하는 형식을 보여준다.
`System.Collections.Generic`을 선언하면 중간 단계인 `System.Collections`도 유지한다.

### 중괄호와 들여쓰기

- 중괄호는 BSD 스타일(Allman)을 사용한다.
- 여는 중괄호는 새 줄에 둔다.
- 기본 들여쓰기는 공백 4칸이다.
- 제네릭 제약 조건은 클래스/메서드 선언과 같은 들여쓰기 레벨에 둔다.

### 빈 스코프

- 중괄호가 선언·실행 스코프를 만들고 그 안에 선언이나 실행 문장이 없으면 완전히 비워 두지 않고 `// NONE`을 작성한다.
- 한 줄 `{ }`로 축약하지 않는다.
- 객체·컬렉션 초기화자처럼 선언·실행 스코프가 아닌 중괄호에는 적용하지 않는다.

```csharp
public interface IExampleValue
{
    // NONE
}

private void Execute()
{
    // NONE
}
```

### 주석과 XML summary

- 클래스, 인터페이스, 프로퍼티, 이벤트, 메서드는 XML `<summary>`를 작성한다.
- private 메서드도 summary를 작성한다.
- `<summary>` 앞뒤에는 같은 길이의 구분선을 둔다.
- 클래스/인터페이스 구분선은 `=`를 사용하고, 멤버 구분선은 `-`를 사용한다.
- 구분선 길이는 `// ` 뒤에 반복되는 `=` 또는 `-`의 개수를 뜻한다.
- 코드 들여쓰기는 제외하고 `///`부터 시작하는 summary 본문 줄의 시각적 폭을 계산한다. `/// `, `<br/>`, 공백, 문장 부호는 폭에 포함한다.
- 한글은 한 글자당 1.5칸, 나머지 문자는 한 글자당 1칸으로 계산한다.
- summary 본문에서 가장 긴 줄의 시각적 폭을 `W`라고 할 때 구분선 길이 `N`은 `N = max(60, (floor(W / 10) + 1) × 10)`으로 계산한다.
- 따라서 `W` 값이 42·59이면 60개, 60·61·69이면 70개, 70이면 80개를 사용한다. 구분선은 가장 긴 summary 본문 줄보다 항상 길게 둔다.
- 반복 문자의 개수를 눈대중이나 토큰 추정으로 새로 만들지 않는다. 아래 60·70·80자 표본 중 필요한 길이를 그대로 복사한다.
- 가장 긴 summary 본문 줄보다 긴 표본 중 가장 짧은 것을 선택하고 위아래에 같은 원문을 복사한다.
- 80자 표본으로도 부족하면 새 구분선을 만들지 않고 summary 문장을 여러 줄로 나눈다.
- summary 설명이 한 줄이면 `<br/>`를 쓰지 않는다.
- summary 설명이 여러 줄이면 첫 줄을 포함한 모든 설명 줄을 `/// <br/>`로 시작한다.
- 주석은 현재 상태의 의도와 작동 방식을 설명한다. 변경 이력이나 작업 기록을 남기지 않는다.

60자 표본:

```csharp
// ============================================================
// ------------------------------------------------------------
```

70자 표본:

```csharp
// ======================================================================
// ----------------------------------------------------------------------
```

80자 표본:

```csharp
// ================================================================================
// --------------------------------------------------------------------------------
```

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

### 파일 블록 머리말

C# 파일에 쓰기 작업이 발생하면 다음 블록 머리말을 유지한다.

- 블록 머리말이 없으면 추가한다.
- 파일의 역할이나 제약이 달라졌으면 설명과 수정일을 현재 상태에 맞게 갱신한다.
- 변경 이력을 적지 않고 현재 파일의 역할과 실제 제약만 설명한다.
- 읽기 전용 작업에서는 추가하거나 수정하지 않는다.

```csharp
/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FileName.cs
수정일 : YYYY-MM-DD

# 설명
파일이 맡는 현재 역할을 설명한다.

# 특이사항, 제약사항
현재 구조에서 유지해야 할 제약을 설명한다.
========================================================================= BLOCK_HEADER_END */
```

### region

- 기본 배치 순서는 내부 데이터 → 필드 → 이벤트 → 생성자 → 복제 → 메서드 → 이벤트 핸들러 → 인터페이스 구현 → 기타다.
- 단순한 클래스는 기본 배치 순서를 따른다.
- region은 단순히 코드를 접기 위한 표시가 아니라, 파일 안에서 함께 변경·관리되는 책임 단위를 드러내도록 사용한다.
- 클래스가 크거나 기능 경계가 명확하면 책임과 기능별 region으로 세분화할 수 있다.
- 각 region은 서로 응집된 하나의 책임을 담되, 파일 전체를 몇 개의 지나치게 큰 region으로 뭉치지 않는다.
- region 제목은 한글을 우선하고, `메서드`, `기타`, `유틸`처럼 책임이 드러나지 않는 일반적인 제목은 사용하지 않는다.
- 개별 메서드마다 region을 만들지 않는다.
- 모든 메서드를 하나의 `#region 메서드` 안에 넣지 않는다.
- `Bind–Unbind`, `Init–Release`, `Subscribe–Unsubscribe`처럼 생명주기나 동작이 대응되는 멤버는 같은 책임 region 안에서 대칭 관계가 드러나도록 배치한다.
- 특정 공개 메서드만을 지원하는 헬퍼는 해당 공개 메서드의 책임 region 안에 배치한다. 여러 책임에서 공유하는 헬퍼만 별도의 내부 처리 region으로 분리한다.
- 기능별 region 내부에서도 가능한 범위에서 기본 배치 순서를 유지한다.
- region을 먼저 만들고 멤버를 억지로 끼워 넣지 않는다. 멤버의 책임이 region과 맞지 않으면 클래스의 책임과 멤버 위치를 함께 다시 검토한다.
- `#region`과 `#endregion`은 둘러싼 형식 선언과 그 형식의 여는·닫는 중괄호와 같은 들여쓰기에 둔다.
- region 내부 멤버는 형식 내부 멤버의 일반 들여쓰기를 유지한다. region 때문에 추가로 들여쓰지 않는다.
- 형식의 여는 중괄호와 첫 `#region` 사이에 빈 줄을 한 줄 둔다.
- `#region` 다음과 `#endregion` 앞에 빈 줄을 한 줄씩 둔다.
- `#endregion` 다음에 다른 region이나 코드가 이어지면 사이에 빈 줄을 한 줄 둔다.
- 마지막 `#endregion`과 형식의 닫는 중괄호 사이에 빈 줄을 한 줄 둔다.
- 내용이 없는 region은 선언하지 않는다.

```csharp
namespace inonego
{
    public sealed class Example
    {

    #region 필드

        private int value = 0;

    #endregion

    #region 연결

        public void Bind()
        {
            // NONE
        }

        public void Unbind()
        {
            // NONE
        }

    #endregion

    #region 초기화와 정리

        public void Initialize()
        {
            // NONE
        }

        public void Release()
        {
            // NONE
        }

    #endregion

    }
}
```

### 명명

- 이름의 의미와 변경 범위는 `programmer`의 명명 판단 절차로 정한 뒤 C# 표기를 적용한다.
- 형식, 구조체, 열거형과 그 멤버, 대리자, 프로퍼티, 이벤트, 메서드는 PascalCase를 사용한다.
- 인터페이스는 `I`로 시작하는 PascalCase를 사용한다.
- 매개변수, 지역 변수, private 필드는 camelCase를 사용하고 public 필드는 PascalCase를 사용한다.
- 형식 이름은 도메인의 대상과 역할을, 메서드 이름은 실제 동작을, bool 이름은 상태나 질의를 드러낸다.
- 비동기 작업을 나타내며 `Task` 또는 `ValueTask`를 반환하는 메서드는 `Async` 접미사를 사용한다.
- 프로젝트에 같은 개념을 표현하는 용어가 있으면 그 용어와 표기를 이어간다.

### 타입과 멤버 배치

- MonoBehaviour가 아닌 클래스는 `[Serializable]` 사용을 권장한다.
- 클래스 내부 구조체/열거형은 전용 타입으로, 클래스 외부 구조체/열거형은 공용 타입으로 둔다.
- 프로퍼티와 backing field는 함께 배치한다.
- backing field가 private이면 public 프로퍼티 아래에 둔다.
- 직렬화 필드는 `[SerializeField]` 또는 `[SerializeReference]`를 사용하고 기본값을 명시한다.

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
/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : Example.cs
수정일 : YYYY-MM-DD

# 설명
코드 스타일 규칙을 한 파일에 적용한 예시다.
========================================================================= BLOCK_HEADER_END */

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
        // NONE
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
        public Example() : base()
        {
            // NONE
        }

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

    #region 생성과 정리

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

        // ------------------------------------------------------------
        /// <summary>
        /// 생성한 객체를 정리한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Despawn()
        {
            // 생성된 객체의 외부 공개 상태를 종료하고 정리한다.
            // NONE
        }

    #endregion

    #region 상태 처리

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
            // NONE
        }

    #endregion

    }
}
```
