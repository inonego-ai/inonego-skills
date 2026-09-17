/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SummaryRule.cs
수정일 : 2026-09-17

# 설명
존재하는 XML summary의 구분선, 설명 폭과 br 형식 규칙을 검사한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 선언의 XML summary 형식을 기계적으로 검사한다.
    /// </summary>
    // ============================================================
    internal static class SummaryRule
    {

    #region 검사 흐름

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 선언의 summary 존재와 구분선 및 설명 줄 형식을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            foreach (DeclarationDescriptor declaration in AnalysisTreeTraversal.FlattenDeclarations(analysis.Declarations))
            {
                CheckDeclaration(declaration, diagnostics);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 하나의 summary 존재 여부와 세부 형식을 단계별로 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckDeclaration(DeclarationDescriptor declaration, List<ConventionDiagnostic> diagnostics)
        {
            SummaryDescriptor? summary = declaration.Summary;

            // summary 자체는 선택 사항이며, 존재하는 경우에만 형식과 내용을 검사한다.
            if (summary == null || !summary.Exists)
            {
                return;
            }

            // summary 태그 줄에는 설명을 붙이지 않고 기존 블록 형식으로만 작성한다.
            if (summary.HasInlineTagContent)
            {
                Add
                (
                    diagnostics, RuleID.SummaryBlockLayout,
                    summary.Range, declaration.Name,
                    "`<summary>`와 `</summary>` 태그 줄에는 설명을 함께 쓰지 않습니다.",
                    "태그를 각각 독립 줄에 두고 설명은 그 사이 `/// ...` 줄에 작성"
                );
                return;
            }

            // summary가 존재하면 내용 유무를 확인한 뒤 구분선·폭·br 형식을 서로 독립적으로 검사한다.
            if (summary.DescriptionLines.Count == 0)
            {
                Add(diagnostics, RuleID.SummaryEmpty, summary.Range, declaration.Name, "XML `<summary>`에는 현재 역할을 설명하는 내용을 작성해야 합니다.", string.Empty);
            }

            char expectedMarker = IsTypeLike(declaration.Kind) ? '=' : '-';
            CheckSeparators(declaration.Name, summary, expectedMarker, diagnostics);
            CheckDescriptionWidth(declaration.Name, summary, expectedMarker, diagnostics);
            CheckBreakUsage(declaration.Name, summary, diagnostics);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// summary 구분선의 인접성, 문자 종류와 위아래 대칭을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckSeparators(string declarationName, SummaryDescriptor summary, char expectedMarker, List<ConventionDiagnostic> diagnostics)
        {
            bool hasDetachedSeparator = summary.TopSeparator.Length > 0 && !summary.TopSeparatorAdjacent
                || summary.BottomSeparator.Length > 0 && !summary.BottomSeparatorAdjacent;
            if (hasDetachedSeparator)
            {
                Add(diagnostics, RuleID.SummarySeparatorSpacing, summary.Range, declarationName, "summary 구분선은 `<summary>` 바로 위와 `</summary>` 바로 아래에 둬야 합니다.", string.Empty);
            }

            bool hasWrongMarker = !HasSeparatorMarker(summary.TopSeparator, expectedMarker)
                || !HasSeparatorMarker(summary.BottomSeparator, expectedMarker);
            if (hasWrongMarker)
            {
                Add(diagnostics, RuleID.SummarySeparatorType, summary.Range, declarationName, $"summary 위아래 구분선은 `{expectedMarker}`를 사용해야 합니다.", string.Empty);
            }

            bool hasBothSeparators = summary.TopSeparator.Length > 0 && summary.BottomSeparator.Length > 0;
            if (hasBothSeparators && !string.Equals(summary.TopSeparator, summary.BottomSeparator, StringComparison.Ordinal))
            {
                Add(diagnostics, RuleID.SummarySeparatorMismatch, summary.Range, declarationName, "summary 위아래 구분선은 같은 원문이어야 합니다.", string.Empty);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 설명 폭과 60·70·80 구분선 길이 규칙을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckDescriptionWidth(string declarationName, SummaryDescriptor summary, char expectedMarker, List<ConventionDiagnostic> diagnostics)
        {
            double maxWidth = GetMaxVisualWidth(summary.RawDescriptionLines);
            if (maxWidth >= 80.0)
            {
                Add(diagnostics, RuleID.SummaryWidth, summary.Range, declarationName, "summary 설명 줄의 시각적 폭은 80칸 미만이어야 합니다.", "설명 문장을 여러 줄로 나눕니다.");
                return;
            }

            int expectedLength = maxWidth < 60.0 ? 60 : maxWidth < 70.0 ? 70 : 80;
            bool hasWrongLength = GetSeparatorLength(summary.TopSeparator) != expectedLength
                || GetSeparatorLength(summary.BottomSeparator) != expectedLength;
            if (hasWrongLength)
            {
                Add(diagnostics, RuleID.SummarySeparatorLength, summary.Range, declarationName, $"summary 구분선 길이는 {expectedLength}자여야 합니다.", $"{expectedLength}자 `{expectedMarker}` 구분선");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 한 줄·여러 줄 설명의 br 사용 규칙을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckBreakUsage(string declarationName, SummaryDescriptor summary, List<ConventionDiagnostic> diagnostics)
        {
            bool singleLineHasBreak = summary.DescriptionLines.Count == 1
                && summary.DescriptionLineHasBreak.Count == 1
                && summary.DescriptionLineHasBreak[0];
            if (singleLineHasBreak)
            {
                Add(diagnostics, RuleID.SummarySingleBreak, summary.Range, declarationName, "한 줄 summary 설명에는 `<br/>`를 쓰지 않습니다.", string.Empty);
                return;
            }

            if (summary.DescriptionLines.Count <= 1)
            {
                return;
            }

            for (int index = 0; index < summary.DescriptionLines.Count; index++)
            {
                bool hasBreak = index < summary.DescriptionLineHasBreak.Count && summary.DescriptionLineHasBreak[index];
                if (!hasBreak)
                {
                    Add(diagnostics, RuleID.SummaryMultiBreak, summary.Range, declarationName, "여러 줄 summary는 모든 설명 줄을 `/// <br/>`로 시작해야 합니다.", string.Empty);
                    return;
                }
            }
        }

    #endregion

    #region 형식 판정

        // ------------------------------------------------------------
        /// <summary>
        /// 형식 선언용 등호 구분선을 써야 하는 선언인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsTypeLike(string kind)
        {
            return kind is "class" or "struct" or "interface" or "enum"
                or "record" or "record struct" or "delegate";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구분선이 원하는 반복 문자로 구성됐는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool HasSeparatorMarker(string separator, char marker)
        {
            if (!separator.StartsWith("// ", StringComparison.Ordinal) || separator.Length <= 3)
            {
                return false;
            }

            for (int index = 3; index < separator.Length; index++)
            {
                if (separator[index] != marker)
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// `// ` 뒤 반복 문자의 길이를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int GetSeparatorLength(string separator)
        {
            return separator.StartsWith("// ", StringComparison.Ordinal) ? separator.Length - 3 : 0;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 설명 줄 중 가장 큰 시각적 폭을 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static double GetMaxVisualWidth(IReadOnlyList<string> lines)
        {
            double maximum = 0.0;
            foreach (string line in lines)
            {
                maximum = Math.Max(maximum, GetVisualWidth(line));
            }

            return maximum;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// summary 원문 한 줄의 시각적 폭을 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static double GetVisualWidth(string line)
        {
            double width = 0.0;
            foreach (char character in line)
            {
                bool isKoreanSyllable = character >= '\uAC00' && character <= '\uD7A3';
                width += isKoreanSyllable ? 1.5 : 1.0;
            }

            return width;
        }

    #endregion

    #region 공통 탐색과 진단

        // ------------------------------------------------------------
        /// <summary>
        /// summary 규칙 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, SourceRange range, string declarationName, string message, string expected)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, range, message, declarationName, expected));
        }

    #endregion

    }

}
