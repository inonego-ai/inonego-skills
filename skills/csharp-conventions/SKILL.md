---
name: csharp-conventions
description: C# 파일 작성/수정 필수 로드. 읽기 전용 작업 제외. Unity C# 스타일 규칙
user-invocable: false
---

# C# 코드 스타일 규칙

Unity C#. 코드 생성 요청 시 즉시 적용.
가독성을 최우선으로 작성. 논리 단위 사이에 빈 줄을 넣어 시각적으로 구분하고, 의미가 달라지는 지점마다 주석이나 구분선으로 맥락을 전달.

**규칙은 모든 동일한 패턴에 예외 없이 적용한다.** 명시적으로 다룬 예시뿐 아니라 구조가 같은 모든 경우에 동일하게 적용해야 한다. 특정 키워드나 패턴에만 규칙을 적용하고 비슷한 다른 패턴에는 빠뜨리는 실수가 자주 발생한다. 규칙을 적용할 때는 "이 패턴과 구조가 같은 다른 곳은 없는가"를 항상 확인한다.

```csharp
// [파일 헤더] 모든 .cs 파일 최상단에 작성. 항목은 콜론 기준으로 정렬.
// 의존성 · 비고처럼 내용이 없으면 항목 자체를 생략한다.
// ============================================================
// 파일명 : FileName.cs
// 수정일 : YYYY-MM-DD
// 의존성 : 없음  또는  관련 시스템 이름 나열
// ------------------------------------------------------------
// 이 파일이 하는 일을 한두 문장으로 설명한다.
// 두 번째 줄이 필요하면 이어서 작성한다.
// ------------------------------------------------------------
// 특이사항이나 향후 계획이 있을 때만 작성한다. (비고)
// ============================================================

// [using 정렬] System → Unity → 라이브러리 → 프로젝트 (그룹 간 빈 줄)
// [순차적 선언] 하위만 쓰더라도 상위를 모두 선언
using System;
using System.Collections; // Generic만 사용하더라도 생략 불가
using System.Collections.Generic;
using System.Linq;

using UnityEngine;

using External;
using External.Library;

// [중괄호] BSD 스타일 (Allman) — 여는 중괄호를 새 줄에 배치
namespace inonego
{
   // 내부에서도 순차적 선언 원칙 적용
   // 같은/하위 네임스페이스는 namespace 블록 내부에서 상대 이름으로 선언
   using Internal; // inonego.Internal
   using Internal.Data; // inonego.Internal.Data

   // [주석: 클래스/인터페이스] = 구분선 60자 + XML <summary>
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
   // [클래스] MonoBehaviour 아닌 클래스는 [Serializable] 권장
   [Serializable]
   public abstract class Example<TKey, T>
   where TKey : IEquatable<TKey>
   where T : class, IExampleValue
   // 제네릭 제약 조건은 클래스 선언과 같은 인덴트
   {
   // [region 순서] 필드 → 이벤트 → 생성자 → 복제 → 메서드 → 이벤트 핸들러 → 인터페이스 구현 → 기타
   // [region 인덴트] #region은 상위 인덴트 유지, 내부 코드는 한 단계 더 들여쓰기
   // [region 생략] 내용이 없는 region은 선언하지 않음
   // 기능별 커스텀 region 허용 (예: #region 키 설정)

   #region 내부 데이터

      // [구조체 / 열거형] 클래스 내부 = 전용, 외부 = 공용
      [Serializable]
      public struct Point
      {
         // [네이밍] public 필드 = PascalCase
         public int X;
         public int Y;
      }

      // [열거형] 간단하면 한 줄, 많거나 값 지정 또는 플래그면 여러 줄
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

      // [주석: 메서드/프로퍼티] - 구분선 60자 + XML <summary>
      // ------------------------------------------------------------
      /// <summary>
      /// 프로퍼티 설명
      /// </summary>
      // ------------------------------------------------------------
      // [프로퍼티] setter 있으면 get => 또는 get { } 블록 사용
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

      // [필드] 직렬화는 [SerializeField] 또는 [SerializeReference] 사용, 기본값 초기화
      // [필드 배치] 프로퍼티와 backing field 함께 배치 (private은 프로퍼티 아래)
      // [네이밍] private 필드 = camelCase
      [SerializeField]
      private GameObject value = null;

      // ------------------------------------------------------------
      /// <summary>
      /// 읽기 전용 프로퍼티 설명
      /// </summary>
      // ------------------------------------------------------------
      // [프로퍼티] 읽기 전용이고 단일 값 또는 필드를 직접 반환하는 경우
      public bool IsActive => isActive;

      // SerializeField가 없으면 위의 public 프로퍼티와 붙여쓰기
      [SerializeField]
      private bool isActive = false;

      // [프로퍼티] 읽기 전용이지만 표현식인 경우
      public bool HasValue
      {
         get => value != null;
         // 또는 get => Example.Instance.Value; 등등
      }

      // [메서드] 위 프로퍼티 규칙과 동일 — 직접 반환이면 => 사용, 그 외는 블록 사용
      public TKey GetKey() => key;
      public bool HasValue() 
      {
         return value != null;
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

      // [생성자] 기본 생성자: base() 또는 this()
      public Example() : base() {}

      // [생성자] 매개변수 생성자: this() 체이닝
      public Example(GameObject value) : this()
      {
         // [제어문] return / break / continue 만 단문 허용. 그 외는 단일 문장이라도 반드시 중괄호.
         if (this.value != null) return;

         if (value == null)
         {
            throw new ArgumentNullException("값이 null입니다.");
         }

         this.value = value;
      }

   #endregion

   #region 메서드

      // [메서드 주석] 접근 제한자(public/private 등) 무관, 모든 메서드에 반드시 작성
      // ------------------------------------------------------------
      /// <summary>
      /// 메서드 설명
      /// </summary>
      // ------------------------------------------------------------
      // [네이밍] 메서드 = PascalCase
      // [메서드] 선택적 매개변수 기본값 허용
      public void Spawn()
      {
         if (value == null)
         {
            throw new InvalidOperationException("값이 설정되어 있지 않습니다.");
         }

         var spawnable = Instantiate(value);
         var hasError = false;

         if (hasError)
         {
            DespawnInternal(spawnable);
         }

         // [주석] 순서가 있더라도 번호 금지 (// 1. X → // 초기화 O)
         // [주석 변경 이력 금지] 기존 주석에 (변경 내용) 괄호 설명 덧붙이기 금지
         // 기능이 바뀌어 주석이 틀려진 경우에만 현재 상태로 수정.
         OnBeforeSpawn(spawnable);
         spawnable.SetActive(true);

         OnSpawnComplete?.Invoke(this, spawnable);
      }

      // [주석: 구분선 길이 조정] 구분선은 내부 내용(/// 줄)의 시각적 폭보다 길어야 함
      // 최소 60자, 내용이 길면 10자씩 추가 (60 → 70 → 80 → ...)
      // 한글은 1.5칸으로 계산 (예: 한글 4자 = 6칸)
      // [br 규칙] 여러 줄이면 첫 줄 포함 모든 줄에 <br/>. 단일 줄은 <br/> 없음.
      // ----------------------------------------------------------------------
      /// <summary>
      /// <br/> 복잡한 처리 흐름을 담은 메서드로, 람다식을 중첩 메서드로 추출하고
      /// <br/> 매개변수가 많은 경우 여러 줄로 분리하여 작성하는 방식을 보여준다.
      /// </summary>
      // ----------------------------------------------------------------------
      public void ComplexMethodWithLongDescription()
      {
         // [람다] 람다식이 메서드의 파라미터로 사용되는 경우 중첩 메서드로 추출
         bool IsMatch(T item)
         {
            return item != null && Equals(item, value);
         }

         var found = items.FirstOrDefault(IsMatch);

         // [단순화] 긴 변수명은 짧은 지역 변수로 할당 (의미 유지. p X → position O)
         var position = spawnPointObject.transform.position;
         var rotation = spawnPointObject.transform.rotation;
         var scale    = spawnPointObject.transform.localScale;

         // [파라미터] 인라인으로 쓰기 길어지면 여러 줄로 분리
         // [괄호] 파라미터가 여러 줄인 경우 ( ) 를 각각 새 줄에 배치 — 중괄호 Allman 스타일과 동일하게
         LongParameterMethod
         (
            position, rotation, scale,
            "Example Name", true, 100
         );

         // 아래와 같은 경우도 유의하여 적용할 것
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
      /// private 메서드도 주석 필수.
      /// </summary>
      // ------------------------------------------------------------
      // [선언부] 호출과 동일한 규칙 적용
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
