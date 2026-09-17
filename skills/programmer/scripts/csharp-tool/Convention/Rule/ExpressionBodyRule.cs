/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ExpressionBodyRule.cs
수정일 : 2026-09-17

# 설명
expression-bodied 프로퍼티와 메서드의 명백한 상태 변경 여부를 검사한다.
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
    /// expression body 안의 명백한 상태 변경만 기계적으로 검사한다.
    /// </summary>
    // ============================================================
    internal static class ExpressionBodyRule
    {

    #region 검사 흐름

        // ----------------------------------------------------------------------
        /// <summary>
        /// 읽기 전용 프로퍼티와 expression-bodied 메서드의 표현 규칙을 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            foreach (PropertyDeclarationSyntax property in analysis.Root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
            {
                CheckProperty(property, diagnostics);
            }

            foreach (MethodDeclarationSyntax method in analysis.Root.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                if (method.ExpressionBody != null && !IsSimpleMethodExpression(method.ExpressionBody.Expression))
                {
                    Add(diagnostics, RuleID.MethodLogicBlock, method.Identifier.GetLocation(), method.Identifier.ValueText, "대입·증감처럼 상태를 직접 변경하는 expression body 메서드는 블록 본문을 사용합니다.");
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 프로퍼티 하나의 직접 반환과 로직 expression body를 구분한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckProperty(PropertyDeclarationSyntax property, List<ConventionDiagnostic> diagnostics)
        {
            // expression-bodied 프로퍼티는 상태 변경 없이 한 식으로 자연스럽게 읽히는 범위를 허용한다.
            if (property.ExpressionBody != null)
            {
                if (!IsSimpleValueExpression(property.ExpressionBody.Expression))
                {
                    Add(diagnostics, RuleID.PropertyLogicBlock, property.Identifier.GetLocation(), property.Identifier.ValueText, "상태 변경이나 복잡한 실행 흐름이 있는 프로퍼티는 get 블록을 사용합니다.");
                }

                return;
            }

            // block getter는 허용한다. expression body 사용 여부는 가독성에 따라 선택한다.
        }

    #endregion

    #region 표현 판정

        // ------------------------------------------------------------
        /// <summary>
        /// expression body 안에 구문상 명백한 상태 변경이 없는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsSimpleValueExpression(ExpressionSyntax expression)
        {
            IEnumerable<SyntaxNode> nodes = expression.DescendantNodesAndSelf
            (
                node => node is not AnonymousFunctionExpressionSyntax
            );
            return !nodes.Any(IsStateMutationNode);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 메서드 expression body도 같은 상태 변경 기준으로 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsSimpleMethodExpression(ExpressionSyntax expression)
        {
            return IsSimpleValueExpression(expression);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 대입 또는 증감처럼 표현식 안에서 상태를 직접 변경하는 구문인지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsStateMutationNode(SyntaxNode node)
        {
            if (node is AssignmentExpressionSyntax assignment)
            {
                bool isInitializerAssignment = assignment.Parent is InitializerExpressionSyntax;
                return !isInitializerAssignment;
            }

            return node switch
            {
                PrefixUnaryExpressionSyntax prefix     => prefix.IsKind(SyntaxKind.PreIncrementExpression)
                                                        || prefix.IsKind(SyntaxKind.PreDecrementExpression),
                PostfixUnaryExpressionSyntax postfix   => postfix.IsKind(SyntaxKind.PostIncrementExpression)
                                                        || postfix.IsKind(SyntaxKind.PostDecrementExpression),
                _                                      => false,
            };
        }

    #endregion

    #region 진단 생성

        // ------------------------------------------------------------
        /// <summary>
        /// 프로퍼티·메서드 표현 규칙 ERROR 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, Location location, string name, string message)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, SourceRange.FromLocation(location), message, name));
        }

    #endregion

    }

}
