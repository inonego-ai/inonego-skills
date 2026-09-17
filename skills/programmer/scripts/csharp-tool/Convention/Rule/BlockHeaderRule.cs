/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : BlockHeaderRule.cs
수정일 : 2026-09-17

# 설명
Block Header의 존재와 파일명 및 형식 손상을 검사한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 파일 Block Header의 기계적으로 확정 가능한 규칙을 검사한다.
    /// </summary>
    // ============================================================
    internal static class BlockHeaderRule
    {
        // ------------------------------------------------------------
        /// <summary>
        /// Block Header 누락과 파일명 불일치 및 손상을 진단한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            BlockHeader header = analysis.BlockHeader;

            // 헤더가 없거나 시작/끝 표식이 깨진 경우에는 세부 필드보다 구조 상태를 먼저 확정한다.
            if (!header.Exists)
            {
                diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderMissing, SourceRange.FromLine(1), "Block Header가 없습니다."));
                if (header.Malformed)
                {
                    diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderMalformed, header.Range, "Block Header 시작/끝 표식이 올바르게 닫히지 않았습니다."));
                }

                return;
            }

            if (header.Malformed)
            {
                diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderMalformed, header.Range, "Block Header 시작/끝 표식이 올바르게 닫히지 않았습니다."));
            }

            // 존재하는 헤더는 파일 선두 배치가 맞는지 확인한 뒤 내부 필드 검사를 진행한다.
            if (header.Range.StartLine != 1 || header.Range.StartColumn != 1)
            {
                diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderPosition, header.Range, "Block Header는 파일의 첫 줄 첫 위치에서 시작해야 합니다."));
            }

            // 파일명·수정일·설명은 서로 다른 실패 원인이므로 각 필드의 존재와 형식을 독립적으로 진단한다.
            if (header.FileName.Length == 0)
            {
                diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderRequiredField, header.Range, "Block Header에 `파일명`을 작성해야 합니다."));
            }
            else if (!string.Equals(header.FileName, analysis.FileName, StringComparison.Ordinal))
            {
                diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderFileName, header.Range, $"Block Header 파일명 `{header.FileName}`이 실제 파일명 `{analysis.FileName}`과 다릅니다."));
            }

            if (header.ModifiedDate.Length == 0)
            {
                diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderRequiredField, header.Range, "Block Header에 `수정일`을 작성해야 합니다."));
            }
            else if (!DateOnly.TryParseExact(header.ModifiedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderDateFormat, header.Range, "Block Header 수정일은 `YYYY-MM-DD` 형식의 실제 날짜여야 합니다."));
            }

            if (header.Description.Length == 0)
            {
                diagnostics.Add(ConventionDiagnostic.Create(RuleID.HeaderRequiredField, header.Range, "Block Header의 `# 설명` 내용을 작성해야 합니다."));
            }
        }
    }

}
