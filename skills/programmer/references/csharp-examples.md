# C# 코딩 규칙 예시

이 문서는 `csharp.md`의 규칙을 한 파일에 적용한 참고 예시다. 규칙 판단은 항상 `conventions/csharp.md`를 우선한다.

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
