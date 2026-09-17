/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ControlBlockRule.cs
수정일 : 2026-09-17

# 설명
제어문에서 허용된 단문 외에는 중괄호를 사용하도록 검사한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 제어문 본문의 중괄호 생략 허용 범위를 검사한다.
    /// </summary>
    // ============================================================
    internal static class ControlBlockRule
    {
        // ------------------------------------------------------------
        /// <summary>
        /// if와 반복·동기화 제어문의 단문 중괄호 규칙을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            // if/else는 else-if 체인만 예외로 허용해야 하므로 다른 제어문보다 먼저 별도 처리한다.
            foreach (IfStatementSyntax statement in analysis.Root.DescendantNodes().OfType<IfStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
                if (statement.Else != null)
                {
                    CheckStatement(statement.Else, statement.Else.Statement, true, diagnostics);
                }
            }

            // 일반 반복문은 return/break/continue 한 줄 외에는 모두 블록 본문을 요구한다.
            foreach (ForStatementSyntax statement in analysis.Root.DescendantNodes().OfType<ForStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
            }

            foreach (ForEachStatementSyntax statement in analysis.Root.DescendantNodes().OfType<ForEachStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
            }

            foreach (ForEachVariableStatementSyntax statement in analysis.Root.DescendantNodes().OfType<ForEachVariableStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
            }

            foreach (WhileStatementSyntax statement in analysis.Root.DescendantNodes().OfType<WhileStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
            }

            foreach (DoStatementSyntax statement in analysis.Root.DescendantNodes().OfType<DoStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
            }

            // 리소스·동기화 제어문도 본문 생략이 흐름을 숨기므로 반복문과 같은 기준으로 검사한다.
            foreach (UsingStatementSyntax statement in analysis.Root.DescendantNodes().OfType<UsingStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
            }

            foreach (LockStatementSyntax statement in analysis.Root.DescendantNodes().OfType<LockStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
            }

            foreach (FixedStatementSyntax statement in analysis.Root.DescendantNodes().OfType<FixedStatementSyntax>())
            {
                CheckStatement(statement, statement.Statement, false, diagnostics);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 한 제어문 본문이 허용된 단문 또는 블록인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckStatement(SyntaxNode owner, StatementSyntax statement, bool allowElseIf, List<ConventionDiagnostic> diagnostics)
        {
            bool isAllowedSimpleStatement = statement is BlockSyntax
                or ReturnStatementSyntax
                or BreakStatementSyntax
                or ContinueStatementSyntax;
            bool isElseIf = allowElseIf && statement is IfStatementSyntax;
            if (isAllowedSimpleStatement || isElseIf)
            {
                return;
            }

            diagnostics.Add(ConventionDiagnostic.Create(RuleID.ControlBraces, SourceRange.FromLocation(owner.GetLocation()), "return, break, continue 외 단문 제어문 본문은 중괄호를 사용해야 합니다."));
        }
    }

}
