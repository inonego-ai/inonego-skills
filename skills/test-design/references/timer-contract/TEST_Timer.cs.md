/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TEST_Timer.cs
수정일 : 2026-05-08

# 설명
Timer 핵심 기능 유닛 테스트.
Unity Test Runner (Edit Mode) 에서 실행한다.

# 테스트 구성
 E: 기본 기능 (생성/Start/Pause/Resume/Stop)
 T: Tick / 완료
 R: Reset
 X: 예외 처리 (Start/Reset 검증, Setter 검증)
 P: Setter 프로퍼티 (Duration/ElapsedTime/RemainingTime)
 V: 이벤트 (OnEnd/OnStateChange)
========================================================================= BLOCK_HEADER_END */

using System;

using NUnit;
using NUnit.Framework;

namespace inonego.Xeri.TEST.Utility._Timer
{

    using inonego.Xeri.Utility;

// ============================================================
/// <summary>
/// Timer 핵심 기능 테스트.
/// </summary>
// ============================================================
public class TEST_Timer
{

#region E-1: 기본 생성

    [Test]
    public void TEST_Timer_기본_생성_초기값()
    {
        var timer = new Timer();

        Assert.AreEqual(0.0f, timer.Duration);
        Assert.AreEqual(0.0f, timer.ElapsedTime);
        Assert.AreEqual(0.0f, timer.RemainingTime);
        Assert.AreEqual(TimerState.Ready, timer.Current);
        Assert.IsFalse(timer.IsRunning);
        Assert.IsFalse(timer.IsPaused);
    }

#endregion

#region E-2: Start / Pause / Resume / Stop

    [Test]
    public void TEST_Timer_상태_변경_Start_Pause_Resume_Stop()
    {
        var timer = new Timer();
        float duration = 5.0f;

        // 시작
        timer.Start(duration);
        Assert.AreEqual(duration, timer.Duration);
        Assert.AreEqual(0.0f, timer.ElapsedTime);
        Assert.AreEqual(duration, timer.RemainingTime);
        Assert.AreEqual(TimerState.Run, timer.Current);
        Assert.IsTrue(timer.IsRunning);

        // 일시정지
        timer.Pause();
        Assert.AreEqual(TimerState.Pause, timer.Current);
        Assert.IsFalse(timer.IsRunning);
        Assert.IsTrue(timer.IsPaused);

        // 재개
        timer.Resume();
        Assert.AreEqual(TimerState.Run, timer.Current);
        Assert.IsTrue(timer.IsRunning);
        Assert.IsFalse(timer.IsPaused);

        // 중지
        timer.Stop();
        Assert.AreEqual(TimerState.Ready, timer.Current);
        Assert.IsFalse(timer.IsRunning);
        Assert.IsFalse(timer.IsPaused);
    }

#endregion

#region T-1: Tick 및 완료

    [Test]
    public void TEST_Timer_Tick_경과_완료_오버플로우()
    {
        var timer = new Timer();
        timer.Start(3.0f);

        // 1초 경과
        timer.Tick(1.0f);
        Assert.AreEqual(1.0f, timer.ElapsedTime);
        Assert.AreEqual(2.0f, timer.RemainingTime);
        Assert.IsTrue(timer.IsRunning);

        // 2초 경과
        timer.Tick(1.0f);
        Assert.AreEqual(2.0f, timer.ElapsedTime);
        Assert.AreEqual(1.0f, timer.RemainingTime);
        Assert.IsTrue(timer.IsRunning);

        // 완료
        timer.Tick(1.0f);
        Assert.AreEqual(3.0f, timer.ElapsedTime);
        Assert.AreEqual(0.0f, timer.RemainingTime);
        Assert.AreEqual(TimerState.Ready, timer.Current);
        Assert.IsFalse(timer.IsRunning);

        // 오버플로우 (Duration 초과)
        timer.Start(2.0f);
        timer.Tick(5.0f);
        Assert.AreEqual(2.0f, timer.ElapsedTime,  "ElapsedTime은 Duration으로 clamp되어야 합니다");
        Assert.AreEqual(0.0f, timer.RemainingTime, "RemainingTime은 0이 되어야 합니다");
        Assert.AreEqual(TimerState.Ready, timer.Current, "오버플로우 시 타이머가 완료되어야 합니다");
        Assert.IsFalse(timer.IsRunning, "오버플로우 시 타이머가 중지되어야 합니다");
    }

#endregion

#region R-1: Reset

    [Test]
    public void TEST_Timer_Reset_초기화()
    {
        var timer = new Timer();
        timer.Start(5.0f);
        timer.Tick(2.0f);
        timer.Stop();

        timer.Reset();

        Assert.AreEqual(0.0f, timer.Duration);
        Assert.AreEqual(0.0f, timer.ElapsedTime);
        Assert.AreEqual(0.0f, timer.RemainingTime);
        Assert.AreEqual(TimerState.Ready, timer.Current);
    }

#endregion

#region X-1: Start / Reset 예외

    [Test]
    public void TEST_Timer_Start_Reset_예외_상황()
    {
        var timer = new Timer();

        // 음수 Duration으로 Start 시도
        Assert.Throws<Timer.InvalidTimeException>(() => timer.Start(-1.0f));

        // 이미 실행 중일 때 Start 시도
        timer.Start(5.0f);
        Assert.Throws<Timer.AlreadyRunningException>(() => timer.Start(3.0f));

        // 실행 중일 때 Reset 시도
        Assert.Throws<Timer.FailedToResetException>(() => timer.Reset());

        // 중지 후 Reset - 정상 동작
        timer.Stop();
        timer.Reset();
    }

#endregion

#region P-1: Setter 기본 동작

    [Test]
    public void TEST_Timer_Setter_Duration_ElapsedTime_RemainingTime_clamp()
    {
        var timer = new Timer();
        timer.Start(10.0f);

        // Duration setter
        timer.Duration = 15.0f;
        Assert.AreEqual(15.0f, timer.Duration);
        Assert.AreEqual(0.0f,  timer.ElapsedTime);
        Assert.AreEqual(15.0f, timer.RemainingTime);

        // ElapsedTime setter
        timer.ElapsedTime = 5.0f;
        Assert.AreEqual(5.0f,  timer.ElapsedTime);
        Assert.AreEqual(10.0f, timer.RemainingTime);

        // ElapsedTime clamp (Duration 초과)
        timer.ElapsedTime = 20.0f;
        Assert.AreEqual(15.0f, timer.ElapsedTime);
        Assert.AreEqual(0.0f,  timer.RemainingTime);

        // RemainingTime setter
        timer.RemainingTime = 8.0f;
        Assert.AreEqual(8.0f, timer.RemainingTime);
        Assert.AreEqual(7.0f, timer.ElapsedTime);

        // RemainingTime clamp (Duration 초과)
        timer.RemainingTime = 20.0f;
        Assert.AreEqual(15.0f, timer.RemainingTime);
        Assert.AreEqual(0.0f,  timer.ElapsedTime);
    }

#endregion

#region X-2: Setter 상태 검증

    [Test]
    public void TEST_Timer_Setter_Ready상태_예외_Pause상태_정상()
    {
        var timer = new Timer();

        // Ready 상태에서 setter 호출 시 예외
        Assert.Throws<InvalidOperationException>(() => timer.Duration      = 5.0f);
        Assert.Throws<InvalidOperationException>(() => timer.ElapsedTime   = 2.0f);
        Assert.Throws<InvalidOperationException>(() => timer.RemainingTime = 3.0f);

        // Pause 상태에서 setter 호출 시 정상 동작
        timer.Start(5.0f);
        timer.Pause();
        timer.Duration      = 10.0f;
        timer.ElapsedTime   = 2.0f;
        timer.RemainingTime = 3.0f;
        Assert.AreEqual(10.0f, timer.Duration);
        Assert.AreEqual(7.0f,  timer.ElapsedTime);
        Assert.AreEqual(3.0f,  timer.RemainingTime);
    }

#endregion

#region X-3: Setter 값 검증

    [Test]
    public void TEST_Timer_Setter_음수_값_예외()
    {
        var timer = new Timer();
        timer.Start(5.0f);

        Assert.Throws<Timer.InvalidTimeException>(() => timer.Duration      = -1.0f);
        Assert.Throws<Timer.InvalidTimeException>(() => timer.ElapsedTime   = -1.0f);
        Assert.Throws<Timer.InvalidTimeException>(() => timer.RemainingTime = -1.0f);
    }

#endregion

#region V-1: OnEnd 이벤트

    [Test]
    public void TEST_Timer_OnEnd_완료_시_발화()
    {
        var timer = new Timer();
        bool endEventFired      = false;
        Timer endEventSender    = null;
        TimerEndEventArgs endEventArgs = default;

        timer.OnEnd += (sender, e) =>
        {
            endEventFired  = true;
            endEventSender = sender as Timer;
            endEventArgs   = e;
        };

        // 아직 완료되지 않은 상태
        timer.Start(2.0f);
        timer.Tick(1.0f);
        Assert.IsFalse(endEventFired, "아직 완료되지 않았으므로 이벤트가 발생하지 않아야 합니다");

        // 완료
        timer.Tick(1.0f);
        Assert.IsTrue(endEventFired,        "타이머 완료 시 OnEnd 이벤트가 발생해야 합니다");
        Assert.AreEqual(timer, endEventSender, "이벤트 발신자는 타이머 자신이어야 합니다");
        Assert.AreEqual(TimerState.Ready, timer.Current, "완료 후 상태는 Ready여야 합니다");
    }

#endregion

#region V-2: OnStateChange 이벤트

    [Test]
    public void TEST_Timer_OnStateChange_상태_전이_시_발화()
    {
        var timer = new Timer();
        bool stateChangeEventFired = false;
        Timer stateChangeSender    = null;
        ValueChangeEventArgs<TimerState> stateChangeEventArgs = default;

        void Reset()
        {
            stateChangeEventFired  = false;
            stateChangeSender      = null;
            stateChangeEventArgs   = default;
        }

        timer.OnStateChange += (sender, e) =>
        {
            stateChangeEventFired  = true;
            stateChangeSender      = sender as Timer;
            stateChangeEventArgs   = e;
        };

        // 시작
        timer.Start(5.0f);
        Assert.IsTrue(stateChangeEventFired, "시작 시 OnStateChange 이벤트가 발생해야 합니다");
        Assert.AreEqual(timer,             stateChangeSender,              "이벤트 발신자는 타이머 자신이어야 합니다");
        Assert.AreEqual(TimerState.Ready,  stateChangeEventArgs.Previous, "이전 상태는 Ready여야 합니다");
        Assert.AreEqual(TimerState.Run,    stateChangeEventArgs.Current,  "현재 상태는 Run이어야 합니다");

        Reset();

        // 일시정지
        timer.Pause();
        Assert.IsTrue(stateChangeEventFired, "일시정지 시 OnStateChange 이벤트가 발생해야 합니다");
        Assert.AreEqual(TimerState.Run,   stateChangeEventArgs.Previous, "이전 상태는 Run이어야 합니다");
        Assert.AreEqual(TimerState.Pause, stateChangeEventArgs.Current,  "현재 상태는 Pause여야 합니다");

        Reset();

        // 재개
        timer.Resume();
        Assert.IsTrue(stateChangeEventFired, "재개 시 OnStateChange 이벤트가 발생해야 합니다");
        Assert.AreEqual(TimerState.Pause, stateChangeEventArgs.Previous, "이전 상태는 Pause여야 합니다");
        Assert.AreEqual(TimerState.Run,   stateChangeEventArgs.Current,  "현재 상태는 Run이어야 합니다");

        Reset();

        // 중지
        timer.Stop();
        Assert.IsTrue(stateChangeEventFired, "중지 시 OnStateChange 이벤트가 발생해야 합니다");
        Assert.AreEqual(TimerState.Run,   stateChangeEventArgs.Previous, "이전 상태는 Run이어야 합니다");
        Assert.AreEqual(TimerState.Ready, stateChangeEventArgs.Current,  "현재 상태는 Ready여야 합니다");
    }

#endregion

}

}
