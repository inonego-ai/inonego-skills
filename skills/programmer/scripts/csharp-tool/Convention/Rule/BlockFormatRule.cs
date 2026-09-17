/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : BlockFormatRule.cs
수정일 : 2026-09-17

# 설명
Allman 중괄호, 실행 블록 경계 공백과 빈 선언·실행 스코프 형식을 검사한다.
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
    /// 중괄호와 빈 스코프처럼 의미 해석이 필요 없는 형식 규칙을 검사한다.
    /// </summary>
    // ============================================================
    internal static class BlockFormatRule
    {

    #region 검사 흐름과 Allman

        // ------------------------------------------------------------
        /// <summary>
        /// Allman 중괄호와 빈 스코프 규칙을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            CheckAllman(analysis, diagnostics);
            CheckBlockSpacing(analysis, diagnostics);
            CheckEmptyScopes(analysis, diagnostics);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 적용 대상 여는 중괄호가 이전 토큰과 같은 줄에 있는지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckAllman(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            IEnumerable<SyntaxToken> openBraceTokens = analysis.Root
                .DescendantTokens()
                .Where(token => token.IsKind(SyntaxKind.OpenBraceToken));

            foreach (SyntaxToken token in openBraceTokens)
            {
                if (ShouldIgnoreBrace(token))
                {
                    continue;
                }

                SyntaxToken previous = token.GetPreviousToken();
                if (previous.RawKind == 0)
                {
                    continue;
                }

                int currentLine = token.GetLocation().GetLineSpan().StartLinePosition.Line;
                int previousLine = previous.GetLocation().GetLineSpan().EndLinePosition.Line;
                if (currentLine == previousLine)
                {
                    Add(diagnostics, RuleID.FormatAllman, token.GetLocation(), "여는 중괄호는 새 줄에 둬야 합니다.");
                }
            }
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// 코드 블록이 아닌 보간 구문, 한 줄 자동 프로퍼티와 compact enum 중괄호를 제외한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private static bool ShouldIgnoreBrace(SyntaxToken token)
        {
            if (token.Parent is InterpolationSyntax)
            {
                return true;
            }

            if (token.Parent is BlockSyntax block && block.Parent is AnonymousFunctionExpressionSyntax)
            {
                return true;
            }

            bool isAutoProperty = token.Parent is AccessorListSyntax accessorList
                && accessorList.Accessors.Count > 0
                && accessorList.Accessors.All(accessor => accessor.SemicolonToken.IsKind(SyntaxKind.SemicolonToken));
            if (isAutoProperty)
            {
                return true;
            }

            return IsCompactEnumBrace(token);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 명시 값이나 멤버별 특성이 없는 단순 enum이 한 줄 compact 형태인지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsCompactEnumBrace(SyntaxToken token)
        {
            if (token.Parent is not EnumDeclarationSyntax declaration || token != declaration.OpenBraceToken)
            {
                return false;
            }

            if (!CSharpSyntaxFacts.IsSingleLine(declaration) || declaration.Members.Count == 0)
            {
                return false;
            }

            return declaration.Members.All(member => member.AttributeLists.Count == 0 && member.EqualsValue == null);
        }

    #endregion

    #region 실행 블록 간격

        // ----------------------------------------------------------------------
        /// <summary>
        /// 블록형 실행문 뒤에 같은 스코프의 다음 독립 문장이 오면 빈 줄 한 줄을 요구한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckBlockSpacing(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            // 일반 block의 statement는 같은 실행 깊이에서 순서대로 이어진다.
            foreach (BlockSyntax block in analysis.Root.DescendantNodes().OfType<BlockSyntax>())
            {
                CheckStatementSpacing(analysis, block.Statements, diagnostics);
            }

            // switch section도 독립 statement 목록을 가지므로 같은 경계 규칙을 적용한다.
            foreach (SwitchSectionSyntax section in analysis.Root.DescendantNodes().OfType<SwitchSectionSyntax>())
            {
                CheckStatementSpacing(analysis, section.Statements, diagnostics);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 인접 statement 중 앞 문장이 닫는 중괄호로 끝날 때 사이의 빈 줄 수를 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckStatementSpacing(CSharpFileAnalysis analysis, SyntaxList<StatementSyntax> statements, List<ConventionDiagnostic> diagnostics)
        {
            for (int index = 0; index < statements.Count - 1; index++)
            {
                StatementSyntax current = statements[index];
                StatementSyntax next = statements[index + 1];

                // 앞 statement가 실제 블록 닫힘으로 끝날 때만 독립 실행 단계 경계로 취급한다.
                SyntaxToken close = current.GetLastToken();
                if (!close.IsKind(SyntaxKind.CloseBraceToken))
                {
                    continue;
                }

                // 다음 statement 앞의 주석까지 첫 내용으로 보아 블록과 다음 단계 사이 시각적 간격을 계산한다.
                int closeLine = close.GetLocation().GetLineSpan().StartLinePosition.Line;
                int nextLine = next.GetLocation().GetLineSpan().StartLinePosition.Line;
                int firstContentLine = FindFirstContentLine(analysis.SourceText, closeLine + 1, nextLine);
                int blankLineCount = firstContentLine - closeLine - 1;
                if (blankLineCount == 1)
                {
                    continue;
                }

                Add
                (
                    diagnostics, RuleID.FormatBlockSpacing,
                    close.GetLocation(),
                    "닫는 중괄호 뒤에 같은 스코프의 다음 독립 문장이 이어지면 빈 줄을 정확히 한 줄 둬야 합니다."
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 원본 줄 범위에서 첫 비어 있지 않은 줄을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static int FindFirstContentLine(SourceText sourceText, int firstLine, int lastLine)
        {
            for (int line = firstLine; line <= lastLine && line < sourceText.Lines.Count; line++)
            {
                if (!string.IsNullOrWhiteSpace(sourceText.Lines[line].ToString()))
                {
                    return line;
                }
            }

            return lastLine;
        }

    #endregion

    #region 빈 스코프

        // ----------------------------------------------------------------------
        /// <summary>
        /// 선언 및 실행 빈 스코프에 // NONE이 있고 한 줄 축약이 아닌지 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckEmptyScopes(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            foreach ((SyntaxToken Open, SyntaxToken Close) scope in GetEmptyScopes(analysis.Root))
            {
                // 빈 스코프라도 의도를 남길 수 있도록 본문에는 정확한 // NONE 주석을 요구한다.
                string content = analysis.SourceText.ToString(TextSpan.FromBounds(scope.Open.Span.End, scope.Close.SpanStart));
                bool hasConditionalDirective = HasConditionalDirective(content);
                if (!hasConditionalDirective && !HasNoneMarker(content))
                {
                    Add(diagnostics, RuleID.FormatEmptyScopeNone, scope.Open.GetLocation(), "빈 선언·실행 스코프에는 `// NONE` 한 줄을 작성해야 합니다.");
                }

                // 빈 스코프도 일반 block과 같은 Allman 구조를 유지하므로 한 줄 축약은 별도로 금지한다.
                int openLine = scope.Open.GetLocation().GetLineSpan().StartLinePosition.Line;
                int closeLine = scope.Close.GetLocation().GetLineSpan().StartLinePosition.Line;
                if (openLine == closeLine)
                {
                    Add(diagnostics, RuleID.FormatEmptyScopeCompact, scope.Open.GetLocation(), "빈 스코프를 한 줄 `{ }`로 축약하지 않습니다.");
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 빈 스코프 본문에 정확한 `// NONE` 주석 줄이 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool HasNoneMarker(string content)
        {
            string[] lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            return lines.Any(line => string.Equals(line.Trim(), "// NONE", StringComparison.Ordinal));
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 현재 파싱 심볼에서 비활성화된 코드가 있을 수 있는 조건부 컴파일 지시문을 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool HasConditionalDirective(string content)
        {
            string[] lines = content.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            return lines.Any(line => line.TrimStart().StartsWith("#if", StringComparison.Ordinal));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 문법 트리에서 비어 있는 선언·실행 스코프의 중괄호 쌍을 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static IEnumerable<(SyntaxToken Open, SyntaxToken Close)> GetEmptyScopes(CompilationUnitSyntax root)
        {
            // 실행 block과 선언 block은 비어 있음을 나타내는 Roslyn 컬렉션이 달라 종류별로 열거한다.
            foreach (BlockSyntax block in root.DescendantNodes().OfType<BlockSyntax>())
            {
                if (block.Parent is AnonymousFunctionExpressionSyntax || block.Statements.Count > 0)
                {
                    continue;
                }

                yield return (block.OpenBraceToken, block.CloseBraceToken);
            }

            foreach (TypeDeclarationSyntax type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (type.Members.Count > 0)
                {
                    continue;
                }

                yield return (type.OpenBraceToken, type.CloseBraceToken);
            }

            foreach (EnumDeclarationSyntax type in root.DescendantNodes().OfType<EnumDeclarationSyntax>())
            {
                if (type.Members.Count > 0)
                {
                    continue;
                }

                yield return (type.OpenBraceToken, type.CloseBraceToken);
            }

            foreach (NamespaceDeclarationSyntax declaration in root.DescendantNodes().OfType<NamespaceDeclarationSyntax>())
            {
                if (declaration.Members.Count > 0)
                {
                    continue;
                }

                yield return (declaration.OpenBraceToken, declaration.CloseBraceToken);
            }

            foreach (SwitchStatementSyntax statement in root.DescendantNodes().OfType<SwitchStatementSyntax>())
            {
                if (statement.Sections.Count > 0)
                {
                    continue;
                }

                yield return (statement.OpenBraceToken, statement.CloseBraceToken);
            }
        }

    #endregion

    #region 진단 생성

        // ------------------------------------------------------------
        /// <summary>
        /// 형식 규칙 ERROR 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, Location location, string message)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, SourceRange.FromLocation(location), message));
        }

    #endregion

    }

}
