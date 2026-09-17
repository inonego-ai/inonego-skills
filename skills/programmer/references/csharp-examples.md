# C# 코딩 규칙 예시

이 문서는 `conventions/csharp.md`의 규칙을 한 파일에 적용한 참고 예시다.
규칙 판단은 항상 원본 문서를 우선하고, 아래 코드는 전체 적용 형태를 보여주는 용도로만 사용한다.

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

        // ============================================================
        /// <summary>
        /// 좌표 데이터 예시.
        /// </summary>
        // ============================================================
        [Serializable]
        public struct Point
        {
            public int X;
            public int Y;
        }

        // ============================================================
        /// <summary>
        /// 상태 값 예시.
        /// </summary>
        // ============================================================
        public enum State { Idle, Running, Dead }

        // ============================================================
        /// <summary>
        /// 부착 위치 값 예시.
        /// </summary>
        // ============================================================
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

                OnValueChange?.Invoke
                (
                    this,
                    new()
                    {
                        Value = 0,
                    }
                );
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

        // ============================================================
        /// <summary>
        /// 값 변경 이벤트 인자 예시.
        /// </summary>
        // ============================================================
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
        public Example()
        {
            // NONE
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 매개변수 생성자.
        /// </summary>
        // ------------------------------------------------------------
        public Example(GameObject value)
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
        }

    #endregion

    #region 상태 처리

        // ----------------------------------------------------------------------
        /// <summary>
        /// <br/> 복잡한 처리 흐름을 담은 메서드로, 이름이 유용한 조건은 지역 함수로 분리하고
        /// <br/> 단순 호출은 한 줄로 두고, 의미 묶음이 있으면 그룹 단위로 나누며
        /// <br/> 객체 초기화자 같은 복합 구조도 필요한 범위에서 여러 줄로 표현한다.
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

            // 개별 인수는 단순하지만 서로 다른 의미 묶음을 보여주기 위해 그룹 단위로 나눈다.
            LongParameterMethod
            (
                position, rotation, scale,
                "Example Name", true, 100
            );

            // 상태 변경 알림은 이전/다음 상태를 한 객체로 묶어 구독자에게 전달한다.
            OnStateChanged?.Invoke
            (
                this, new()
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