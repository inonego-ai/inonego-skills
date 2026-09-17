/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RegionRule.cs
수정일 : 2026-09-17

# 설명
region 지시문 들여쓰기와 정확한 빈 줄, 빈 내용과 빈 제목을 검사한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// region의 기계적 배치와 공백 규칙을 검사한다.
    /// </summary>
    // ============================================================
    internal static class RegionRule
    {

    #region 검사 흐름

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 region의 공백, 들여쓰기, 빈 region과 제목을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            List<RegionNode> regions = AnalysisTreeTraversal.FlattenRegions(analysis.Regions)
                .OrderBy(region => region.StartPosition)
                .ToList();

            foreach (RegionNode region in regions)
            {
                CheckRegion(analysis, region, regions, diagnostics);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// region 하나의 공백, 경계, 들여쓰기, 내용과 제목 규칙을 순서대로 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckRegion(CSharpFileAnalysis analysis, RegionNode region, IReadOnlyList<RegionNode> regions, List<ConventionDiagnostic> diagnostics)
        {
            CheckInnerSpacing(analysis, region, diagnostics);
            CheckAfterEndSpacing(analysis, region, diagnostics);
            CheckOwnerBoundarySpacing(analysis, region, regions, diagnostics);
            CheckIndentation(analysis, region, diagnostics);
            CheckEmpty(analysis, region, diagnostics);
            CheckName(region, diagnostics);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// region 제목이 비어 있는지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckName(RegionNode region, List<ConventionDiagnostic> diagnostics)
        {
            if (region.Name.Length == 0)
            {
                Add(diagnostics, RuleID.RegionEmptyName, region.StartLine, "region 제목은 비워 두지 않습니다.");
            }
        }

    #endregion

    #region 공백과 형식 경계

        // ------------------------------------------------------------
        /// <summary>
        /// region 시작 다음과 종료 직전의 빈 줄이 정확히 한 줄인지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckInnerSpacing(CSharpFileAnalysis analysis, RegionNode region, List<ConventionDiagnostic> diagnostics)
        {
            int firstContent = FindNextNonBlankLine(analysis.SourceText, region.StartLine, region.EndLine - 1);
            if (firstContent > 0 && firstContent != region.StartLine + 2)
            {
                Add(diagnostics, RuleID.RegionAfterStartSpacing, region.StartLine, "`#region` 다음에는 빈 줄을 정확히 한 줄 둬야 합니다.");
            }

            int lastContent = FindPreviousNonBlankLine(analysis.SourceText, region.EndLine - 2, region.StartLine);
            if (lastContent > 0 && lastContent != region.EndLine - 2)
            {
                Add(diagnostics, RuleID.RegionBeforeEndSpacing, region.EndLine, "`#endregion` 앞에는 빈 줄을 정확히 한 줄 둬야 합니다.");
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// region 종료 뒤 실제 코드나 다음 region이 이어질 때 빈 줄을 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckAfterEndSpacing(CSharpFileAnalysis analysis, RegionNode region, List<ConventionDiagnostic> diagnostics)
        {
            int boundary = region.Parent?.EndLine ?? GetOwnerCloseLine(region.Owner);
            if (boundary <= region.EndLine)
            {
                return;
            }

            int next = FindNextNonBlankLine(analysis.SourceText, region.EndLine, boundary - 1);
            if (next <= 0 || next >= boundary)
            {
                return;
            }

            string nextText = analysis.SourceText.Lines[next - 1].ToString().TrimStart();
            if (nextText.StartsWith("#endregion", StringComparison.Ordinal))
            {
                return;
            }

            if (next != region.EndLine + 2)
            {
                Add(diagnostics, RuleID.RegionAfterEndSpacing, region.EndLine, "`#endregion` 뒤에 코드나 다른 region이 이어지면 빈 줄을 정확히 한 줄 둬야 합니다.");
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 형식의 첫 region과 마지막 region이 중괄호와 한 줄 떨어졌는지 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckOwnerBoundarySpacing(CSharpFileAnalysis analysis, RegionNode region, IReadOnlyList<RegionNode> regions, List<ConventionDiagnostic> diagnostics)
        {
            if (region.Owner == null || region.Parent?.Owner == region.Owner)
            {
                return;
            }

            List<RegionNode> ownerRegions = regions
                .Where(item => item.Owner == region.Owner && item.Parent?.Owner != region.Owner)
                .OrderBy(item => item.StartPosition)
                .ToList();
            if (ownerRegions.Count == 0)
            {
                return;
            }

            CheckFirstRegionBoundary(analysis, region, ownerRegions, diagnostics);
            CheckLastRegionBoundary(analysis, region, ownerRegions, diagnostics);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 소유 선언의 여는 중괄호와 첫 region 사이 경계를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckFirstRegionBoundary(CSharpFileAnalysis analysis, RegionNode region, IReadOnlyList<RegionNode> ownerRegions, List<ConventionDiagnostic> diagnostics)
        {
            if (!ReferenceEquals(ownerRegions[0], region))
            {
                return;
            }

            if (!TryGetOwnerBraces(region.Owner!, out SyntaxToken open, out _))
            {
                return;
            }

            int openLine = open.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            int first = FindNextNonBlankLine(analysis.SourceText, openLine, region.StartLine);
            if (first == region.StartLine && region.StartLine == openLine + 2)
            {
                return;
            }

            Add
            (
                diagnostics, RuleID.RegionTypeSpacing,
                region.StartLine,
                "형식의 여는 중괄호 다음에는 빈 줄 한 줄 뒤 첫 region이 바로 시작해야 합니다."
            );
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 마지막 region과 소유 선언의 닫는 중괄호 사이 경계를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckLastRegionBoundary(CSharpFileAnalysis analysis, RegionNode region, IReadOnlyList<RegionNode> ownerRegions, List<ConventionDiagnostic> diagnostics)
        {
            if (!ReferenceEquals(ownerRegions[^1], region))
            {
                return;
            }

            if (!TryGetOwnerBraces(region.Owner!, out _, out SyntaxToken close))
            {
                return;
            }

            int closeLine = close.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            int previous = FindPreviousNonBlankLine(analysis.SourceText, closeLine - 1, region.EndLine);
            if (previous == region.EndLine && closeLine == region.EndLine + 2)
            {
                return;
            }

            Add
            (
                diagnostics, RuleID.RegionBeforeTypeEndSpacing,
                region.EndLine,
                "마지막 region 다음에는 빈 줄 한 줄 뒤 형식의 닫는 중괄호가 바로 와야 합니다."
            );
        }

    #endregion

    #region 들여쓰기와 내용

        // ------------------------------------------------------------
        /// <summary>
        /// region 지시문이 소유 선언의 중괄호와 같은 들여쓰기인지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckIndentation(CSharpFileAnalysis analysis, RegionNode region, List<ConventionDiagnostic> diagnostics)
        {
            if (region.Owner == null || !TryGetOwnerBraces(region.Owner, out SyntaxToken open, out _))
            {
                return;
            }

            int ownerLine = open.GetLocation().GetLineSpan().StartLinePosition.Line;
            int expected = CSharpSyntaxFacts.GetLeadingSpaceCount(analysis.SourceText.Lines[ownerLine].ToString());
            int startIndent = CSharpSyntaxFacts.GetLeadingSpaceCount(analysis.SourceText.Lines[region.StartLine - 1].ToString());
            int endIndent = CSharpSyntaxFacts.GetLeadingSpaceCount(analysis.SourceText.Lines[region.EndLine - 1].ToString());
            if (startIndent != expected || endIndent != expected)
            {
                Add(diagnostics, RuleID.RegionIndent, region.StartLine, "`#region`과 `#endregion`은 둘러싼 형식 중괄호와 같은 들여쓰기에 둬야 합니다.");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// region 안에 코드 토큰이 전혀 없는지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckEmpty(CSharpFileAnalysis analysis, RegionNode region, List<ConventionDiagnostic> diagnostics)
        {
            if (!HasCodeToken(analysis, region))
            {
                Add(diagnostics, RuleID.RegionEmpty, region.StartLine, "내용이 없는 region은 선언하지 않습니다.");
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// region 시작·종료 directive 사이에 실제 코드 토큰이 있는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool HasCodeToken(CSharpFileAnalysis analysis, RegionNode region)
        {
            foreach (SyntaxToken token in analysis.Root.DescendantTokens())
            {
                bool startsInside = token.SpanStart > region.StartPosition;
                bool endsInside = token.Span.End < region.EndPosition;
                if (startsInside && endsInside)
                {
                    return true;
                }
            }

            return false;
        }

    #endregion

    #region 소유 구조 탐색

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 종류에 맞는 본문 중괄호 토큰을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool TryGetOwnerBraces(DeclarationDescriptor declaration, out SyntaxToken open, out SyntaxToken close)
        {
            switch (declaration.Node)
            {
                case BaseTypeDeclarationSyntax type:
                    open = type.OpenBraceToken;
                    close = type.CloseBraceToken;
                    return !open.IsMissing && !close.IsMissing;
                case BaseMethodDeclarationSyntax method when method.Body != null:
                    open = method.Body.OpenBraceToken;
                    close = method.Body.CloseBraceToken;
                    return true;
                case LocalFunctionStatementSyntax local when local.Body != null:
                    open = local.Body.OpenBraceToken;
                    close = local.Body.CloseBraceToken;
                    return true;
                default:
                    open = default;
                    close = default;
                    return false;
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 소유 선언의 닫는 중괄호 줄을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int GetOwnerCloseLine(DeclarationDescriptor? owner)
        {
            return owner != null && TryGetOwnerBraces(owner, out _, out SyntaxToken close)
                ? close.GetLocation().GetLineSpan().StartLinePosition.Line + 1
                : 0;
        }

    #endregion

    #region 원본 줄 탐색

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 범위에서 다음 비어 있지 않은 1-based 줄을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static int FindNextNonBlankLine(SourceText sourceText, int afterLine, int lastLine)
        {
            for (int line = afterLine + 1; line <= lastLine && line <= sourceText.Lines.Count; line++)
            {
                if (!string.IsNullOrWhiteSpace(sourceText.Lines[line - 1].ToString()))
                {
                    return line;
                }
            }

            return 0;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정 범위에서 이전 비어 있지 않은 1-based 줄을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static int FindPreviousNonBlankLine(SourceText sourceText, int beforeLine, int firstLine)
        {
            for (int line = beforeLine; line >= firstLine && line > 0; line--)
            {
                if (!string.IsNullOrWhiteSpace(sourceText.Lines[line - 1].ToString()))
                {
                    return line;
                }
            }

            return 0;
        }

    #endregion

    #region 진단 생성

        // ------------------------------------------------------------
        /// <summary>
        /// region 규칙 ERROR 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, int line, string message)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, SourceRange.FromLine(line), message));
        }

    #endregion

    }

}
