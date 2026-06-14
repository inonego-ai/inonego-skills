/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : Timer.cs
수정일 : 2026-04-30

# 설명
float 기반 타이머. Tick(deltaTime) 호출로 시간을 진행시킨다.
MonoBehaviour 비종속 — MonoBehaviour.Update() 등에서 Tick()을 직접 호출하여 사용한다.
========================================================================= BLOCK_HEADER_END */

using System;

using UnityEngine;

using TValue = System.Single;

namespace inonego.Xeri.Utility
{
    // ============================================================
    /// <summary>
    /// 타이머 상태.
    /// </summary>
    // ============================================================
    public enum TimerState { Ready, Run, Pause }

    // ============================================================
    /// <summary>
    /// 타이머 종료 이벤트 인수.
    /// </summary>
    // ============================================================
    [Serializable]
    public struct TimerEndEventArgs
    {
        // NONE
    }

    // ============================================================
    /// <summary>
    /// float 기반 타이머.
    /// </summary>
    // ============================================================
    [Serializable]
    public partial class Timer : ITimer, IReadOnlyTimer
    {

    #region 필드

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머 지속 시간.
        /// </summary>
        // ------------------------------------------------------------
        public TValue Duration
        {
            get => duration;
            set
            {
                var next = value;

                if (!IsRunning && !IsPaused)
                {
                    throw new InvalidOperationException("Duration은 Run 또는 Pause 상태일 때만 변경할 수 있습니다.");
                }

                if (next < 0.0f)
                {
                    throw new InvalidTimeException("Duration은 0 이상이어야 합니다.");
                }

                duration = next;

                elapsedTime = Mathf.Clamp(elapsedTime, 0.0f, duration);
            }
        }
        [SerializeField]
        private TValue duration = default;

        // ------------------------------------------------------------
        /// <summary>
        /// Start 시 사용할 duration 값. Stop/Reset 후에도 유지된다.
        /// </summary>
        // ------------------------------------------------------------
        [SerializeField]
        internal TValue cached = default;
        TValue ITimer.cached
        {
            get => cached; 
            set => cached = value;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 타이머가 경과한 시간.
        /// <br/> 중지 후에도 Reset 또는 Start 전까지 값이 유지된다.
        /// </summary>
        // ------------------------------------------------------------
        public TValue ElapsedTime
        {
            get => elapsedTime;
            set
            {
                var next = value;

                if (!IsRunning && !IsPaused)
                {
                    throw new InvalidOperationException("ElapsedTime은 Run 또는 Pause 상태일 때만 변경할 수 있습니다.");
                }

                if (next < 0.0f)
                {
                    throw new InvalidTimeException("ElapsedTime은 0 이상이어야 합니다.");
                }

                elapsedTime = Mathf.Clamp(next, 0.0f, duration);
            }
        }
        [SerializeField]
        private TValue elapsedTime = default;

        // ------------------------------------------------------------
        /// <summary>
        /// <br/> 타이머의 남은 시간.
        /// <br/> 중지 후에도 Reset 또는 Start 전까지 값이 유지된다.
        /// </summary>
        // ------------------------------------------------------------
        public TValue RemainingTime
        {
            get => duration - elapsedTime;
            set
            {
                var next = value;

                if (!IsRunning && !IsPaused)
                {
                    throw new InvalidOperationException("RemainingTime은 Run 또는 Pause 상태일 때만 변경할 수 있습니다.");
                }

                if (next < 0.0f)
                {
                    throw new InvalidTimeException("RemainingTime은 0 이상이어야 합니다.");
                }

                elapsedTime = Mathf.Clamp(duration - next, 0.0f, duration);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 경과 시간 비율 (0.0 ~ 1.0). duration이 0이면 NaN을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public TValue ElapsedTime01 => ElapsedTime / duration;

        // ------------------------------------------------------------
        /// <summary>
        /// 잔여 시간 비율 (0.0 ~ 1.0). duration이 0이면 NaN을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public TValue RemainingTime01 => RemainingTime / duration;

    #endregion

    #region 상태

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 타이머 상태.
        /// </summary>
        // ------------------------------------------------------------
        public TimerState Current
        {
            get => current;
            protected set
            {
                var (prev, next) = (current, value);

                if (prev == next) return;

                current = next;

                OnStateChange?.Invoke(this, new(prev, next));
            }
        }
        [SerializeField]
        private TimerState current = TimerState.Ready;

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머가 실행 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsRunning => current == TimerState.Run;

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머가 일시정지 중인지 여부.
        /// </summary>
        // ------------------------------------------------------------
        public bool IsPaused  => current == TimerState.Pause;

    #endregion

    #region 이벤트

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머가 종료될 때 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        public event EventHandler<TimerEndEventArgs> OnEnd = null;

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머 상태가 변경될 때 발생한다.
        /// </summary>
        // ------------------------------------------------------------
        public event ValueChangeEventHandler<TimerState> OnStateChange = null;

    #endregion

    #region 메서드

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머를 업데이트한다. 매 프레임 또는 틱마다 호출한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Tick(TValue deltaTime)
        {
            if (!IsRunning) return;

            elapsedTime = Mathf.Clamp(elapsedTime + deltaTime, 0.0f, duration);

            if (elapsedTime >= duration)
            {
                OnEnd?.Invoke(this, new());

                Stop();
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머 작동을 시작한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Start(TValue duration)
        {
            if (IsRunning || IsPaused)
            {
                throw new AlreadyRunningException();
            }

            Current = TimerState.Run;

            try
            {
                (Duration, ElapsedTime) = (duration, default);
            }
            catch
            {
                Current = TimerState.Ready;

                throw;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머 작동을 중지한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Stop()
        {
            if (IsRunning || IsPaused)
            {
                Current = TimerState.Ready;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머 작동을 일시정지한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Pause()
        {
            if (IsRunning)
            {
                Current = TimerState.Pause;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머 작동을 재개한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Resume()
        {
            if (IsPaused)
            {
                Current = TimerState.Run;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 타이머를 리셋한다.
        /// </summary>
        // ------------------------------------------------------------
        public void Reset()
        {
            if (IsRunning || IsPaused)
            {
                throw new FailedToResetException();
            }

            (duration, elapsedTime) = (default, default);
        }

    #endregion

    }
}
