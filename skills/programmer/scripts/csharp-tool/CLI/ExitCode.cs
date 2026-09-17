/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ExitCode.cs
수정일 : 2026-09-17

# 설명
C# 분석 도구의 프로세스 종료 코드를 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 도구 실행 결과를 호출자에게 전달하는 종료 코드다.
    /// </summary>
    // ============================================================
    internal enum ExitCode
    {
        Success         = 0,
        ConventionError = 1,
        AnalysisError   = 2,
        InvalidArgument = 3,
        ToolFailure     = 4,
    }

}
