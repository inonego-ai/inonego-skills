/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : FileReport.cs
수정일 : 2026-09-17

# 설명
파일 하나의 분석 결과와 컨벤션 진단을 보고 단위로 묶는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 파일 하나의 분석 결과와 진단을 보고 단위로 묶는다.
    /// </summary>
    // ============================================================
    internal sealed class FileReport
    {
        public CSharpFileAnalysis Analysis            = null!;
        public List<ConventionDiagnostic> Diagnostics = new();
    }

}
