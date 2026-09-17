/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : MemberLayoutRule.cs
수정일 : 2026-09-17

# 설명
명백한 프로퍼티 대응 필드 인접 배치를 검사한다.
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
    /// 타입 내부의 확정 가능한 멤버 배치 규칙을 검사한다.
    /// </summary>
    // ============================================================
    internal static class MemberLayoutRule
    {

    #region 검사 흐름

        // ------------------------------------------------------------
        /// <summary>
        /// 각 타입의 프로퍼티-필드 쌍 인접 배치를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            IEnumerable<DeclarationDescriptor> typeDeclarations = AnalysisTreeTraversal
                .FlattenDeclarations(analysis.Declarations)
                .Where(IsTypeDeclaration);

            foreach (DeclarationDescriptor type in typeDeclarations)
            {
                CheckType(type, diagnostics);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 타입 하나의 직접 하위 멤버 배치를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckType(DeclarationDescriptor type, List<ConventionDiagnostic> diagnostics)
        {
            List<DeclarationDescriptor> members = type.Children.OrderBy(member => member.StartPosition).ToList();

            // 직접 대응 후보를 먼저 모은 뒤 하나의 필드를 여러 프로퍼티가 공유하는 모호한 관계는 자동 pair에서 제외한다.
            Dictionary<DeclarationDescriptor, DeclarationDescriptor> pairByProperty = new();
            foreach (DeclarationDescriptor property in members.Where(IsPropertyDeclaration))
            {
                if (TryFindBackingField(property, members, out DeclarationDescriptor? field) && field != null)
                {
                    pairByProperty.Add(property, field);
                }
            }

            HashSet<DeclarationDescriptor> sharedFields = pairByProperty.Values
                .GroupBy(field => field)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .ToHashSet();

            foreach ((DeclarationDescriptor property, DeclarationDescriptor field) in pairByProperty)
            {
                if (sharedFields.Contains(field))
                {
                    continue;
                }

                int propertyIndex = members.IndexOf(property);
                int fieldIndex = members.IndexOf(field);
                bool sameRegion = ReferenceEquals(property.Region, field.Region);
                if (sameRegion && fieldIndex == propertyIndex + 1)
                {
                    continue;
                }

                if (sameRegion && Math.Abs(fieldIndex - propertyIndex) == 1)
                {
                    Add(diagnostics, RuleID.MemberBackingBelow, property, "private 대응 필드는 프로퍼티 바로 아래에 둬야 합니다.");
                }
                else
                {
                    Add(diagnostics, RuleID.MemberPairAdjacency, property, "프로퍼티와 직접 대응하는 필드는 같은 책임 구간에서 서로 붙여 둬야 합니다.");
                }
            }

        }

    #endregion

    #region 대응 필드 판정

        // ------------------------------------------------------------
        /// <summary>
        /// 프로퍼티 getter가 직접 반환하는 private 필드를 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool TryFindBackingField(DeclarationDescriptor propertyDescriptor, IReadOnlyList<DeclarationDescriptor> members, out DeclarationDescriptor? fieldDescriptor)
        {
            fieldDescriptor = null;
            PropertyDeclarationSyntax property = (PropertyDeclarationSyntax)propertyDescriptor.Node;

            // getter가 직접 반환하는 식에서만 대응 필드 이름을 확정한다.
            string fieldName = GetDirectGetterFieldName(property);
            if (fieldName.Length == 0)
            {
                return false;
            }

            // 같은 타입의 직접 하위 멤버 중 단일 private field만 후보로 인정한다.
            DeclarationDescriptor? candidate = FindBackingFieldCandidate(members, fieldName);
            if (candidate == null)
            {
                return false;
            }

            // getter가 private 필드를 직접 반환하면 setter 구현 방식과 관계없이 대응 backing field로 본다.
            // setter가 helper를 통해 값을 갱신하더라도 배치 책임은 같은 필드 쌍에 있다.
            fieldDescriptor = candidate;
            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 직접 하위 멤버에서 지정 이름의 단일 private 필드 후보를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static DeclarationDescriptor? FindBackingFieldCandidate(IReadOnlyList<DeclarationDescriptor> members, string fieldName)
        {
            foreach (DeclarationDescriptor member in members)
            {
                if (member.Kind != "field" || member.Accessibility != "private")
                {
                    continue;
                }

                if (member.Node is not FieldDeclarationSyntax field || field.Declaration.Variables.Count != 1)
                {
                    continue;
                }

                VariableDeclaratorSyntax variable = field.Declaration.Variables[0];
                if (variable.Identifier.ValueText == fieldName)
                {
                    return member;
                }
            }

            return null;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// getter가 다른 연산 없이 직접 반환하는 필드 이름을 추출한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetDirectGetterFieldName(PropertyDeclarationSyntax property)
        {
            // 프로퍼티 자체가 expression-bodied면 그 식에서 바로 필드 참조 여부를 확인한다.
            if (property.ExpressionBody != null)
            {
                return GetDirectFieldName(property.ExpressionBody.Expression);
            }

            // accessor 프로퍼티는 getter의 expression body 또는 단일 return 문만 직접 반환으로 인정한다.
            AccessorDeclarationSyntax? getter = property.AccessorList?.Accessors.FirstOrDefault(IsGetter);
            if (getter == null)
            {
                return string.Empty;
            }

            ExpressionSyntax? expression = getter.ExpressionBody?.Expression;
            if (expression == null && getter.Body?.Statements.Count == 1)
            {
                expression = (getter.Body.Statements[0] as ReturnStatementSyntax)?.Expression;
            }

            return expression == null ? string.Empty : GetDirectFieldName(expression);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 접근자가 getter인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsGetter(AccessorDeclarationSyntax accessor)
        {
            return accessor.Keyword.IsKind(SyntaxKind.GetKeyword);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 식이 필드 이름 또는 this.필드 자체를 직접 가리키는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetDirectFieldName(ExpressionSyntax expression)
        {
            return expression switch
            {
                IdentifierNameSyntax value                                              => value.Identifier.ValueText,
                MemberAccessExpressionSyntax value when value.Expression is ThisExpressionSyntax
                                                                                        => value.Name.Identifier.ValueText,
                _                                                                       => string.Empty,
            };
        }

    #endregion

    #region 공통 탐색과 진단

        // ------------------------------------------------------------
        /// <summary>
        /// 멤버 배치 검사를 수행할 타입 선언인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsTypeDeclaration(DeclarationDescriptor declaration)
        {
            return declaration.Kind is "class" or "struct" or "record" or "record struct";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 멤버 배치에서 대응 필드 검사를 수행할 프로퍼티 선언인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsPropertyDeclaration(DeclarationDescriptor declaration)
        {
            return declaration.Kind == "property" && declaration.Node is PropertyDeclarationSyntax;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 멤버 배치 ERROR 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, DeclarationDescriptor declaration, string message)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, declaration.Range, message, declaration.Name));
        }

    #endregion

    }

}
