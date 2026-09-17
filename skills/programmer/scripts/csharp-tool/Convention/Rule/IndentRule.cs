/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : IndentRule.cs
수정일 : 2026-09-17

# 설명
구문 구조에 따른 코드·주석·제네릭 제약 들여쓰기를 검사한다.
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
    /// C# 구문 구조에 맞는 정확한 4칸 들여쓰기를 검사한다.
    /// </summary>
    // ============================================================
    internal static class IndentRule
    {

    #region 코드 들여쓰기

        // ------------------------------------------------------------
        /// <summary>
        /// 코드, 주석과 제네릭 제약의 구조 들여쓰기를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            CheckIndentation(analysis, diagnostics);
            CheckCommentIndentation(analysis, diagnostics);
            CheckConstraintIndentation(analysis, diagnostics);
        }


        // ----------------------------------------------------------------------
        /// <summary>
        /// 각 코드 줄의 첫 토큰이 구문 구조에서 요구되는 정확한 4칸 깊이에 있는지 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckIndentation(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            HashSet<int> checkedLines = new();

            // 같은 줄에 여러 구문이 걸려도 최초 진단만 남기도록 하나의 줄 집합을 전체 단계가 공유한다.
            CheckLeadingTabs(analysis, diagnostics, checkedLines);
            CheckDeclarationIndentation(analysis, diagnostics, checkedLines);
            CheckStatementIndentation(analysis, diagnostics, checkedLines);
            CheckInitializerIndentation(analysis, diagnostics, checkedLines);
            CheckDelimiterIndentation(analysis, diagnostics, checkedLines);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// using, namespace, 멤버와 enum 멤버의 선언 위치를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckDeclarationIndentation(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            // using은 파일/namespace에서 다른 선언과 소유 깊이가 달라 먼저 독립적으로 확인한다.
            foreach (UsingDirectiveSyntax directive in GetNodes<UsingDirectiveSyntax>(analysis.Root))
            {
                CheckNodeIndent(analysis, directive, diagnostics, checkedLines);
            }

            // block namespace는 선언 위치와 함께 여닫는 중괄호도 같은 바깥 깊이를 유지해야 한다.
            foreach (BaseNamespaceDeclarationSyntax declaration in GetNodes<BaseNamespaceDeclarationSyntax>(analysis.Root))
            {
                CheckNodeIndent(analysis, declaration, diagnostics, checkedLines);
                if (declaration is not NamespaceDeclarationSyntax blockNamespace)
                {
                    continue;
                }

                CheckDelimiterIndent(analysis, blockNamespace.OpenBraceToken, declaration.Parent, diagnostics, checkedLines);
                CheckDelimiterIndent(analysis, blockNamespace.CloseBraceToken, declaration.Parent, diagnostics, checkedLines);
            }

            // 형식과 멤버는 특성 줄까지 같은 선언 깊이로 묶어 검사한다.
            foreach (MemberDeclarationSyntax member in GetNodes<MemberDeclarationSyntax>(analysis.Root))
            {
                if (member is BaseNamespaceDeclarationSyntax)
                {
                    continue;
                }

                CheckNodeIndent(analysis, member, diagnostics, checkedLines);
                CheckAttributeIndentation(analysis, member, diagnostics, checkedLines);
            }

            // enum 멤버는 일반 MemberDeclarationSyntax 계층 밖에 있어 마지막에 별도로 확인한다.
            foreach (EnumMemberDeclarationSyntax member in GetNodes<EnumMemberDeclarationSyntax>(analysis.Root))
            {
                CheckNodeIndent(analysis, member, diagnostics, checkedLines);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 멤버 선언에 직접 붙은 특성 목록의 들여쓰기를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckAttributeIndentation(CSharpFileAnalysis analysis, MemberDeclarationSyntax member, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            int expected = CountIndentContainers(member.Parent, member) * 4;
            foreach (AttributeListSyntax attribute in member.ChildNodes().OfType<AttributeListSyntax>())
            {
                CheckTokenIndent(analysis, attribute.GetFirstToken(), expected, diagnostics, checkedLines);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 실행 문장, 접근자, 분기 절과 인수·매개변수의 들여쓰기를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckStatementIndentation(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            // 실행 문장과 접근자는 각각 소유 block/accessor list 안에서 한 단계 안쪽에 놓인다.
            foreach (StatementSyntax statement in GetNodes<StatementSyntax>(analysis.Root))
            {
                CheckNodeIndent(analysis, statement, diagnostics, checkedLines);
            }

            foreach (AccessorDeclarationSyntax accessor in GetNodes<AccessorDeclarationSyntax>(analysis.Root))
            {
                CheckNodeIndent(analysis, accessor, diagnostics, checkedLines);
            }

            // else/catch/finally는 본문 문장이 아니라 절 자체의 기준 깊이를 따로 가진다.
            foreach (ElseClauseSyntax clause in GetNodes<ElseClauseSyntax>(analysis.Root))
            {
                CheckNodeIndent(analysis, clause, diagnostics, checkedLines);
            }

            foreach (CatchClauseSyntax clause in GetNodes<CatchClauseSyntax>(analysis.Root))
            {
                CheckNodeIndent(analysis, clause, diagnostics, checkedLines);
            }

            foreach (FinallyClauseSyntax clause in GetNodes<FinallyClauseSyntax>(analysis.Root))
            {
                CheckNodeIndent(analysis, clause, diagnostics, checkedLines);
            }

            // switch label은 section이 소유한 깊이를 기준으로 맞춘다.
            foreach (SwitchSectionSyntax section in GetNodes<SwitchSectionSyntax>(analysis.Root))
            {
                CheckSwitchLabelIndentation(analysis, section, diagnostics, checkedLines);
            }

            // 여러 줄 호출과 선언의 요소는 실제 소유 표현식·선언보다 한 단계 안쪽에 둔다.
            foreach (ArgumentSyntax argument in GetNodes<ArgumentSyntax>(analysis.Root))
            {
                CheckDelimitedElementIndent(analysis, argument, diagnostics, checkedLines);
            }

            foreach (ParameterSyntax parameter in GetNodes<ParameterSyntax>(analysis.Root))
            {
                CheckDelimitedElementIndent(analysis, parameter, diagnostics, checkedLines);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// switch section에 속한 모든 label을 section 깊이와 비교한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckSwitchLabelIndentation(CSharpFileAnalysis analysis, SwitchSectionSyntax section, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            int expected = CountIndentContainers(section.Parent, section) * 4;
            foreach (SwitchLabelSyntax label in section.Labels)
            {
                CheckTokenIndent(analysis, label.GetFirstToken(), expected, diagnostics, checkedLines);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 인수·매개변수를 실제 소유 표현식 또는 선언보다 한 단계 안쪽으로 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckDelimitedElementIndent(CSharpFileAnalysis analysis, SyntaxNode element, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            SyntaxNode? list = element.Parent;
            if (list?.Parent == null)
            {
                CheckNodeIndent(analysis, element, diagnostics, checkedLines);
                return;
            }

            int expected = GetOwnerIndent(analysis, list.Parent) + 4;
            CheckTokenIndent(analysis, element.GetFirstToken(), expected, diagnostics, checkedLines);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 여러 줄 괄호 목록을 소유하는 표현식·선언의 실제 시작 들여쓰기를 반환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static int GetOwnerIndent(CSharpFileAnalysis analysis, SyntaxNode owner)
        {
            SyntaxToken first = owner.GetFirstToken();
            int lineIndex = first.GetLocation().GetLineSpan().StartLinePosition.Line;
            return CSharpSyntaxFacts.GetLeadingSpaceCount(analysis.SourceText.Lines[lineIndex].ToString());
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 객체·컬렉션 initializer의 요소와 중괄호 들여쓰기를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckInitializerIndentation(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            foreach (InitializerExpressionSyntax initializer in GetNodes<InitializerExpressionSyntax>(analysis.Root))
            {
                foreach (ExpressionSyntax expression in initializer.Expressions)
                {
                    CheckNodeIndent(analysis, expression, diagnostics, checkedLines);
                }

                CheckDelimiterIndent(analysis, initializer.OpenBraceToken, initializer.Parent, diagnostics, checkedLines);
                CheckDelimiterIndent(analysis, initializer.CloseBraceToken, initializer.Parent, diagnostics, checkedLines);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 여러 줄 구조의 중괄호와 괄호가 소유 구조의 기준 깊이에 있는지 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckDelimiterIndentation(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            // 선언·실행 block의 중괄호는 소유 구문 머리부와 같은 기준 깊이에 놓는다.
            foreach (BaseTypeDeclarationSyntax type in GetNodes<BaseTypeDeclarationSyntax>(analysis.Root))
            {
                CheckDelimiterIndent(analysis, type.OpenBraceToken, type.Parent, diagnostics, checkedLines);
                CheckDelimiterIndent(analysis, type.CloseBraceToken, type.Parent, diagnostics, checkedLines);
            }

            foreach (BlockSyntax block in GetNodes<BlockSyntax>(analysis.Root))
            {
                CheckDelimiterIndent(analysis, block.OpenBraceToken, block.Parent, diagnostics, checkedLines);
                CheckDelimiterIndent(analysis, block.CloseBraceToken, block.Parent, diagnostics, checkedLines);
            }

            // 접근자와 switch는 일반 BlockSyntax 외부에 자체 구분 토큰을 가져 별도로 확인한다.
            foreach (AccessorListSyntax accessorList in GetNodes<AccessorListSyntax>(analysis.Root))
            {
                CheckDelimiterIndent(analysis, accessorList.OpenBraceToken, accessorList.Parent, diagnostics, checkedLines);
                CheckDelimiterIndent(analysis, accessorList.CloseBraceToken, accessorList.Parent, diagnostics, checkedLines);
            }

            foreach (SwitchStatementSyntax statement in GetNodes<SwitchStatementSyntax>(analysis.Root))
            {
                CheckDelimiterIndent(analysis, statement.OpenBraceToken, statement.Parent, diagnostics, checkedLines);
                CheckDelimiterIndent(analysis, statement.CloseBraceToken, statement.Parent, diagnostics, checkedLines);
            }

            // 여러 줄 괄호는 한 줄 구문과 달리 괄호 자체가 독립 줄의 기준 깊이를 가져야 한다.
            foreach (ArgumentListSyntax list in GetNodes<ArgumentListSyntax>(analysis.Root))
            {
                if (CSharpSyntaxFacts.IsSingleLine(list))
                {
                    continue;
                }

                CheckDelimitedListIndent(analysis, list, list.OpenParenToken, list.CloseParenToken, diagnostics, checkedLines);
            }

            foreach (ParameterListSyntax list in GetNodes<ParameterListSyntax>(analysis.Root))
            {
                if (CSharpSyntaxFacts.IsSingleLine(list))
                {
                    continue;
                }

                CheckDelimitedListIndent(analysis, list, list.OpenParenToken, list.CloseParenToken, diagnostics, checkedLines);
            }
        }

    #endregion

    #region 주석 들여쓰기

        // ----------------------------------------------------------------------
        /// <summary>
        /// 줄 선두 일반 주석과 XML 주석이 다음 코드 구조와 같은 깊이에 있는지 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckCommentIndentation(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            if (analysis.Root.FullSpan.End == 0)
            {
                return;
            }

            HashSet<int> checkedLines = new();
            int previousReportedLine = -2;
            int previousReportedActual = -1;
            int previousReportedExpected = -1;

            for (int lineIndex = 0; lineIndex < analysis.SourceText.Lines.Count; lineIndex++)
            {
                if (!TryGetCommentIndentation(analysis, lineIndex, out int actual, out int expected))
                {
                    continue;
                }

                if (actual == expected || !checkedLines.Add(lineIndex))
                {
                    continue;
                }

                bool repeatsPrevious = lineIndex == previousReportedLine + 1
                    && actual == previousReportedActual
                    && expected == previousReportedExpected;
                if (repeatsPrevious)
                {
                    continue;
                }

                string message = $"주석은 설명하는 코드 구조와 같은 깊이인 공백 {expected}칸에 둬야 합니다.";
                AddLineDiagnostic(diagnostics, lineIndex, message, $"공백 {expected}칸");
                previousReportedLine = lineIndex;
                previousReportedActual = actual;
                previousReportedExpected = expected;
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 한 줄이 실제 주석인지 확인하고 해당 주석의 실제·기대 들여쓰기를 계산한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool TryGetCommentIndentation(CSharpFileAnalysis analysis, int lineIndex, out int actual, out int expected)
        {
            actual = 0;
            expected = 0;

            TextLine line = analysis.SourceText.Lines[lineIndex];
            string text = line.ToString();
            if (!text.TrimStart().StartsWith("//", StringComparison.Ordinal))
            {
                return false;
            }

            actual = CSharpSyntaxFacts.GetLeadingSpaceCount(text);
            int commentPosition = line.Start + actual;
            SyntaxTrivia trivia = analysis.Root.FindTrivia(commentPosition, findInsideTrivia: true);
            if (!IsLineCommentTrivia(trivia))
            {
                return false;
            }

            int probe = Math.Min(line.EndIncludingLineBreak, analysis.Root.FullSpan.End - 1);
            SyntaxToken nextToken = analysis.Root.FindToken(probe, findInsideTrivia: false);
            if (!TryGetCommentIndent(nextToken, out expected))
            {
                return false;
            }

            return IsTokenIndentCorrect(analysis, nextToken);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 줄 선두 `//`가 실제 일반 주석 또는 XML 문서 주석 trivia인지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsLineCommentTrivia(SyntaxTrivia trivia)
        {
            return trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) || trivia.IsKind(SyntaxKind.DocumentationCommentExteriorTrivia);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 주석 다음 토큰에서 주석이 놓일 구조 깊이를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool TryGetCommentIndent(SyntaxToken token, out int expected)
        {
            if (TryGetClosingDelimiterIndent(token, out int delimiterIndent))
            {
                expected = delimiterIndent + 4;
                return true;
            }

            return TryGetTokenIndent(token, out expected);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 주석이 참조하는 다음 코드 토큰 자체가 올바른 구조 깊이에 있는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsTokenIndentCorrect(CSharpFileAnalysis analysis, SyntaxToken token)
        {
            if (!TryGetTokenIndent(token, out int expected))
            {
                return true;
            }

            int lineIndex = token.GetLocation().GetLineSpan().StartLinePosition.Line;
            return CSharpSyntaxFacts.GetLeadingSpaceCount(analysis.SourceText.Lines[lineIndex].ToString()) == expected;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 코드 토큰 자체가 놓일 구조상 들여쓰기 깊이를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool TryGetTokenIndent(SyntaxToken token, out int expected)
        {
            expected = 0;
            if (token.RawKind == 0)
            {
                return false;
            }

            if (TryGetClosingDelimiterIndent(token, out expected))
            {
                return true;
            }

            foreach (SyntaxNode node in token.Parent?.AncestorsAndSelf() ?? Enumerable.Empty<SyntaxNode>())
            {
                if (node.GetFirstToken() != token)
                {
                    continue;
                }

                if (IsDirectIndentNode(node))
                {
                    expected = CountIndentContainers(node.Parent, node) * 4;
                    return true;
                }

                if (node is ExpressionSyntax expression && expression.Parent is InitializerExpressionSyntax)
                {
                    expected = CountIndentContainers(expression.Parent, expression) * 4;
                    return true;
                }
            }

            return false;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 첫 토큰 위치 자체가 구조 들여쓰기 판정 대상인 노드인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsDirectIndentNode(SyntaxNode node)
        {
            return node switch
            {
                MemberDeclarationSyntax     => true,
                StatementSyntax             => true,
                AccessorDeclarationSyntax   => true,
                ElseClauseSyntax            => true,
                CatchClauseSyntax           => true,
                FinallyClauseSyntax         => true,
                SwitchLabelSyntax           => true,
                ArgumentSyntax              => true,
                ParameterSyntax             => true,
                _                           => false,
            };
        }

    #endregion

    #region 구조 판정과 진단

        // ------------------------------------------------------------
        /// <summary>
        /// 전체 구문 트리에서 지정한 Roslyn 노드 종류만 열거한다.
        /// </summary>
        // ------------------------------------------------------------
        private static IEnumerable<T> GetNodes<T>(CompilationUnitSyntax root)
        where T : SyntaxNode
        {
            return root.DescendantNodes().OfType<T>();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 닫는 구분 토큰이 속한 구조의 바깥쪽 들여쓰기를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool TryGetClosingDelimiterIndent(SyntaxToken token, out int expected)
        {
            expected = 0;
            SyntaxNode? parent = token.Parent;
            bool isClosing = parent switch
            {
                NamespaceDeclarationSyntax item            => token == item.CloseBraceToken,
                BaseTypeDeclarationSyntax item             => token == item.CloseBraceToken,
                BlockSyntax item                           => token == item.CloseBraceToken,
                AccessorListSyntax item                    => token == item.CloseBraceToken,
                SwitchStatementSyntax item                 => token == item.CloseBraceToken,
                InitializerExpressionSyntax item           => token == item.CloseBraceToken,

                ArgumentListSyntax item                    => token == item.CloseParenToken,
                BracketedArgumentListSyntax item           => token == item.CloseBracketToken,
                ParameterListSyntax item                   => token == item.CloseParenToken,
                BracketedParameterListSyntax item          => token == item.CloseBracketToken,
                _                                          => false,
            };
            if (!isClosing)
            {
                return false;
            }

            expected = CountIndentContainers(parent?.Parent, parent) * 4;
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 코드 줄 선두에 탭이 사용됐는지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckLeadingTabs(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            for (int lineIndex = 0; lineIndex < analysis.SourceText.Lines.Count; lineIndex++)
            {
                string line = analysis.SourceText.Lines[lineIndex].ToString();
                if (!line.TakeWhile(char.IsWhiteSpace).Any(character => character == '\t'))
                {
                    continue;
                }

                if (!checkedLines.Add(lineIndex))
                {
                    continue;
                }

                AddLineDiagnostic(diagnostics, lineIndex, "들여쓰기에 탭을 사용하지 않습니다.", "탭 없이 공백만 사용");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구문 노드의 첫 토큰을 부모 구조의 정확한 깊이와 비교한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckNodeIndent(CSharpFileAnalysis analysis, SyntaxNode node, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            int expected = CountIndentContainers(node.Parent, node) * 4;
            CheckTokenIndent(analysis, node.GetFirstToken(), expected, diagnostics, checkedLines);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 구조의 여닫는 토큰을 해당 구조 머리부와 같은 깊이로 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckDelimiterIndent(CSharpFileAnalysis analysis, SyntaxToken token, SyntaxNode? parent, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            if (token.IsMissing)
            {
                return;
            }

            int expected = CountIndentContainers(parent, null) * 4;
            CheckTokenIndent(analysis, token, expected, diagnostics, checkedLines);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 여러 줄 인수·매개변수 목록의 괄호를 실제 소유 표현식·선언과 같은 깊이에 둔다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckDelimitedListIndent(CSharpFileAnalysis analysis, SyntaxNode list, SyntaxToken open, SyntaxToken close, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            if (list.Parent == null)
            {
                return;
            }

            int expected = GetOwnerIndent(analysis, list.Parent);
            CheckTokenIndent(analysis, open, expected, diagnostics, checkedLines);
            CheckTokenIndent(analysis, close, expected, diagnostics, checkedLines);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 한 토큰이 줄의 첫 코드일 때 실제 공백 수와 기대 공백 수를 비교한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckTokenIndent(CSharpFileAnalysis analysis, SyntaxToken token, int expected, List<ConventionDiagnostic> diagnostics, HashSet<int> checkedLines)
        {
            if (token.IsMissing || !CSharpSyntaxFacts.IsFirstTokenOnLine(analysis.SourceText, token))
            {
                return;
            }

            int lineIndex = token.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (!checkedLines.Add(lineIndex))
            {
                return;
            }

            string line = analysis.SourceText.Lines[lineIndex].ToString();
            int actual = CSharpSyntaxFacts.GetLeadingSpaceCount(line);
            if (actual != expected)
            {
                string message = $"이 줄의 구조상 들여쓰기는 공백 {expected}칸이어야 합니다.";
                AddLineDiagnostic(diagnostics, lineIndex, message, $"공백 {expected}칸");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 현재 노드부터 바깥쪽으로 실제 코드 블록의 들여쓰기 단계를 센다.
        /// </summary>
        // ------------------------------------------------------------
        private static int CountIndentContainers(SyntaxNode? node, SyntaxNode? child)
        {
            int level = 0;
            for (SyntaxNode? current = node; current != null; child = current, current = current.Parent)
            {
                if (IsIndentContainer(current))
                {
                    level++;
                    continue;
                }

                if (IsUnbracedControlBody(current, child))
                {
                    level++;
                }
            }

            return level;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 자체 본문이나 요소 목록으로 한 단계 안쪽 들여쓰기를 만드는 노드인지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsIndentContainer(SyntaxNode node)
        {
            return node switch
            {
                NamespaceDeclarationSyntax         => true,
                BaseTypeDeclarationSyntax          => true,
                BlockSyntax                        => true,
                AccessorListSyntax                 => true,
                SwitchStatementSyntax              => true,
                SwitchSectionSyntax                => true,
                InitializerExpressionSyntax        => true,
                ArgumentListSyntax                 => true,
                BracketedArgumentListSyntax        => true,
                ParameterListSyntax                => true,
                BracketedParameterListSyntax       => true,
                _                                  => false,
            };
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 중괄호 없이 허용된 제어문 본문이 한 단계 더 들여써져야 하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsUnbracedControlBody(SyntaxNode current, SyntaxNode? child)
        {
            return current switch
            {
                IfStatementSyntax statement                 => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                ElseClauseSyntax clause                     => IsUnbracedElseBody(clause, child),
                ForStatementSyntax statement                => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                ForEachStatementSyntax statement            => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                ForEachVariableStatementSyntax statement    => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                WhileStatementSyntax statement              => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                DoStatementSyntax statement                 => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                UsingStatementSyntax statement              => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                LockStatementSyntax statement               => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                FixedStatementSyntax statement              => ReferenceEquals(statement.Statement, child) && statement.Statement is not BlockSyntax,
                _                                           => false,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// else 본문이 중괄호와 else-if가 아닌 단문인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsUnbracedElseBody(ElseClauseSyntax clause, SyntaxNode? child)
        {
            bool ownsChild = ReferenceEquals(clause.Statement, child);
            bool hasBlock = clause.Statement is BlockSyntax;
            bool isElseIf = clause.Statement is IfStatementSyntax;
            return ownsChild && !hasBlock && !isElseIf;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// where 제약 절이 부모 선언과 같은 들여쓰기인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckConstraintIndentation(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            foreach (TypeParameterConstraintClauseSyntax clause in analysis.Root.DescendantNodes().OfType<TypeParameterConstraintClauseSyntax>())
            {
                SyntaxNode? declaration = clause.Parent;
                if (declaration == null)
                {
                    continue;
                }

                int clauseLine = clause.GetLocation().GetLineSpan().StartLinePosition.Line;
                int declarationLine = declaration.GetLocation().GetLineSpan().StartLinePosition.Line;
                int clauseIndent = CSharpSyntaxFacts.GetLeadingSpaceCount(analysis.SourceText.Lines[clauseLine].ToString());
                int declarationIndent = CSharpSyntaxFacts.GetLeadingSpaceCount(analysis.SourceText.Lines[declarationLine].ToString());
                if (clauseIndent != declarationIndent)
                {
                    Add(diagnostics, RuleID.FormatConstraintIndent, clause.GetLocation(), "제너릭 제약 조건은 선언과 같은 들여쓰기 레벨에 둬야 합니다.");
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 줄 단위 들여쓰기 ERROR 진단을 공통 형식으로 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AddLineDiagnostic(List<ConventionDiagnostic> diagnostics, int lineIndex, string message, string expected)
        {
            SourceRange range = SourceRange.FromLine(lineIndex + 1);
            diagnostics.Add(ConventionDiagnostic.Create(RuleID.FormatIndent, range, message, string.Empty, expected));
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 들여쓰기 규칙 ERROR 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, Location location, string message)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, SourceRange.FromLocation(location), message));
        }

    #endregion

    }

}
