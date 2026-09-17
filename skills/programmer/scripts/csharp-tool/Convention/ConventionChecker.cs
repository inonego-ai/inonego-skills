/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ConventionChecker.cs
수정일 : 2026-09-17

# 설명
각 규칙 그룹을 고정 순서로 실행해 하나의 진단 목록을 만든다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// CSharpFileAnalysis에 모든 기계적 컨벤션 규칙을 적용한다.
    /// </summary>
    // ============================================================
    internal static class ConventionChecker
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 규칙 그룹을 순서대로 실행하고 위치와 ID로 정렬한다.
        /// </summary>
        // ------------------------------------------------------------
        public static List<ConventionDiagnostic> Check(CSharpFileAnalysis analysis)
        {
            List<ConventionDiagnostic> diagnostics = new();

            // 파일 외형과 원본 배치를 먼저 검사해 이후 선언 진단이 같은 구조 기준을 공유하게 한다.
            BlockHeaderRule.Check(analysis, diagnostics);
            UsingRule.Check(analysis, diagnostics);
            BlockFormatRule.Check(analysis, diagnostics);
            IndentRule.Check(analysis, diagnostics);
            SummaryRule.Check(analysis, diagnostics);
            RegionRule.Check(analysis, diagnostics);

            // 선언 자체의 이름·배치·표현 규칙은 구조 검사가 끝난 뒤 같은 분석 결과에 누적한다.
            NamingRule.Check(analysis, diagnostics);
            MemberLayoutRule.Check(analysis, diagnostics);
            ExpressionBodyRule.Check(analysis, diagnostics);
            ControlBlockRule.Check(analysis, diagnostics);
            CompactSyntaxRule.Check(analysis, diagnostics);

            // 호출 순서와 무관하게 외부 출력은 원본 위치와 Rule ID 기준으로 결정적으로 고정한다.
            return diagnostics
                .OrderBy(diagnostic => diagnostic.Range.StartLine)
                .ThenBy(diagnostic => diagnostic.Range.StartColumn)
                .ThenBy(diagnostic => diagnostic.ID, StringComparer.Ordinal)
                .ToList();
        }
    }

}
