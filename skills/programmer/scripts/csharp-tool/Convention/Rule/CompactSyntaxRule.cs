/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : CompactSyntaxRule.cs
수정일 : 2026-09-17

# 설명
여러 줄 선언·호출의 괄호 배치를 검사한다.
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
    /// 여러 줄 선언·호출의 괄호 배치를 검사한다.
    /// </summary>
    // ============================================================
    internal static class CompactSyntaxRule
    {

    #region 검사 흐름

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 줄 괄호 배치를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            CheckMultilineParentheses(analysis, diagnostics);
        }

    #endregion

    #region 괄호 배치

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 줄 선언과 호출의 여닫는 괄호가 각 줄의 첫 토큰인지 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckMultilineParentheses(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            // 선언 괄호는 메서드·생성자·지역 함수·대리자에만 적용해 형식 인수 등 다른 괄호와 섞지 않는다.
            IEnumerable<ParameterListSyntax> parameterLists = analysis.Root
                .DescendantNodes()
                .OfType<ParameterListSyntax>()
                .Where(IsDeclarationParameterList);

            foreach (ParameterListSyntax parameterList in parameterLists)
            {
                CheckParenPair
                (
                    analysis.SourceText,
                    parameterList.OpenParenToken, parameterList.CloseParenToken,
                    diagnostics
                );
            }

            // 호출 괄호는 선언과 별도 집합으로 순회해 같은 배치 규칙을 독립적으로 확인한다.
            IEnumerable<ArgumentListSyntax> argumentLists = analysis.Root
                .DescendantNodes()
                .OfType<ArgumentListSyntax>();

            foreach (ArgumentListSyntax argumentList in argumentLists)
            {
                CheckParenPair
                (
                    analysis.SourceText,
                    argumentList.OpenParenToken, argumentList.CloseParenToken,
                    diagnostics
                );
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 줄 괄호 쌍의 여는 괄호와 닫는 괄호 줄 배치를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckParenPair(SourceText sourceText, SyntaxToken open, SyntaxToken close, List<ConventionDiagnostic> diagnostics)
        {
            int openLine = open.GetLocation().GetLineSpan().StartLinePosition.Line;
            int closeLine = close.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (openLine == closeLine)
            {
                return;
            }

            SyntaxToken firstInside = open.GetNextToken();
            SyntaxToken lastInside = close.GetPreviousToken();
            int firstInsideLine = firstInside.GetLocation().GetLineSpan().StartLinePosition.Line;
            int lastInsideLine = lastInside.GetLocation().GetLineSpan().EndLinePosition.Line;
            bool openIsOwnLine = CSharpSyntaxFacts.IsFirstTokenOnLine(sourceText, open) && firstInsideLine > openLine;
            bool closeIsOwnLine = CSharpSyntaxFacts.IsFirstTokenOnLine(sourceText, close) && lastInsideLine < closeLine;
            if (openIsOwnLine && closeIsOwnLine)
            {
                return;
            }

            Add
            (
                diagnostics, RuleID.CompactParenPlacement,
                open.GetLocation(),
                "여러 줄 선언·호출에서는 `(`와 `)` 줄에 인수나 매개변수를 함께 두지 않습니다."
            );
        }

    #endregion

    #region 구문 보조

        // ----------------------------------------------------------------------
        /// <summary>
        /// 매개변수 목록이 메서드·생성자·지역 함수·대리자 선언에 속하는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsDeclarationParameterList(ParameterListSyntax parameterList)
        {
            return parameterList.Parent is BaseMethodDeclarationSyntax
                or LocalFunctionStatementSyntax
                or DelegateDeclarationSyntax;
        }

    #endregion

    #region 진단 생성

        // ------------------------------------------------------------
        /// <summary>
        /// 간결한 표현 규칙 ERROR 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, Location location, string message)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, SourceRange.FromLocation(location), message));
        }

    #endregion

    }

}
