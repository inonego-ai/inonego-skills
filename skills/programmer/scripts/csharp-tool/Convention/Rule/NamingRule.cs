/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : NamingRule.cs
수정일 : 2026-09-17

# 설명
기계적으로 확인 가능한 PascalCase와 camelCase 표기를 검사한다.
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
    /// 선언 종류별 식별자 표기 규칙을 검사한다.
    /// </summary>
    // ============================================================
    internal static class NamingRule
    {

    #region 검사 흐름

        // ------------------------------------------------------------
        /// <summary>
        /// 선언, 필드, 매개변수와 지역 변수의 표기를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            // 구조 트리에 이미 수집된 형식·멤버·필드 이름을 먼저 검사한다.
            CheckDeclarationNames(analysis, diagnostics);

            // 매개변수와 지역 변수는 선언 트리에 모두 나타나지 않으므로 원본 SyntaxTree에서 별도로 검사한다.
            CheckParameterAndLocalNames(analysis, diagnostics);

            // field-like event는 한 선언에 여러 이벤트 이름을 가질 수 있어 변수 단위로 따로 검사한다.
            CheckEventFieldNames(analysis, diagnostics);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 트리에 수집된 형식·멤버·필드 이름을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckDeclarationNames(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            foreach (DeclarationDescriptor declaration in AnalysisTreeTraversal.FlattenDeclarations(analysis.Declarations))
            {
                if (declaration.Kind == "interface")
                {
                    if (!IsInterfaceName(declaration.Name))
                    {
                        Add(diagnostics, RuleID.NameInterface, declaration.Range, declaration.Name, "인터페이스는 `I`로 시작하는 PascalCase를 사용해야 합니다.");
                    }
                }
                else if (IsPascalDeclaration(declaration) && !IsPascalCase(declaration.Name))
                {
                    Add(diagnostics, RuleID.NamePascal, declaration.Range, declaration.Name, "해당 선언 이름은 PascalCase를 사용해야 합니다.");
                }

                if (declaration.Kind == "field" && declaration.Node is FieldDeclarationSyntax field)
                {
                    CheckFieldNames(declaration, field, diagnostics);
                }

            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 일반 PascalCase 규칙을 적용할 선언인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsPascalDeclaration(DeclarationDescriptor declaration)
        {
            if (declaration.Node is EventFieldDeclarationSyntax || FollowsExternalNameContract(declaration))
            {
                return false;
            }

            return declaration.Kind is "class" or "struct" or "enum" or "record" or "record struct"
                or "delegate" or "enum member" or "property" or "event" or "method" or "local function";
        }

        // --------------------------------------------------------------------------------
        /// <summary>
        /// override 또는 명시적 인터페이스 구현처럼 외부 API 이름을 바꿀 수 없는 선언인지 확인한다.
        /// </summary>
        // --------------------------------------------------------------------------------
        private static bool FollowsExternalNameContract(DeclarationDescriptor declaration)
        {
            if (declaration.Accessibility == "explicit")
            {
                return true;
            }

            return declaration.Node is MemberDeclarationSyntax member
                && member.Modifiers.Any(SyntaxKind.OverrideKeyword);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 매개변수와 여러 지역 변수 구문 형태의 camelCase 표기를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckParameterAndLocalNames(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            foreach (ParameterSyntax parameter in analysis.Root.DescendantNodes().OfType<ParameterSyntax>())
            {
                CheckLocalName(parameter.Identifier, "매개변수", diagnostics);
            }

            foreach (VariableDeclaratorSyntax variable in analysis.Root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (variable.FirstAncestorOrSelf<BaseFieldDeclarationSyntax>() != null)
                {
                    continue;
                }

                CheckLocalName(variable.Identifier, "지역 변수", diagnostics);
            }

            foreach (ForEachStatementSyntax forEach in analysis.Root.DescendantNodes().OfType<ForEachStatementSyntax>())
            {
                CheckLocalName(forEach.Identifier, "foreach 지역 변수", diagnostics);
            }

            foreach (CatchDeclarationSyntax catchDeclaration in analysis.Root.DescendantNodes().OfType<CatchDeclarationSyntax>())
            {
                if (!catchDeclaration.Identifier.IsKind(SyntaxKind.None))
                {
                    CheckLocalName(catchDeclaration.Identifier, "catch 지역 변수", diagnostics);
                }
            }

            foreach (SingleVariableDesignationSyntax designation in analysis.Root.DescendantNodes().OfType<SingleVariableDesignationSyntax>())
            {
                CheckLocalName(designation.Identifier, "지역 변수", diagnostics);
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// field-like event 선언의 모든 이벤트 이름을 PascalCase로 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckEventFieldNames(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            foreach (EventFieldDeclarationSyntax eventField in analysis.Root.DescendantNodes().OfType<EventFieldDeclarationSyntax>())
            {
                if (eventField.Modifiers.Any(SyntaxKind.OverrideKeyword))
                {
                    continue;
                }

                foreach (VariableDeclaratorSyntax variable in eventField.Declaration.Variables)
                {
                    string name = variable.Identifier.ValueText;
                    if (!IsPascalCase(name))
                    {
                        Add(diagnostics, RuleID.NamePascal, SourceRange.FromLocation(variable.Identifier.GetLocation()), name, "이벤트는 PascalCase를 사용해야 합니다.");
                    }
                }
            }
        }

    #endregion

    #region 개별 이름 검사

        // ------------------------------------------------------------
        /// <summary>
        /// 지역 변수 토큰 하나의 camelCase 표기를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckLocalName(SyntaxToken identifier, string role, List<ConventionDiagnostic> diagnostics)
        {
            string name = identifier.ValueText;
            if (!IsCamelCase(name))
            {
                Add(diagnostics, RuleID.NameCamel, SourceRange.FromLocation(identifier.GetLocation()), name, $"{role}는 camelCase를 사용해야 합니다.");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 필드 접근 수준에 따라 camelCase 또는 PascalCase를 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckFieldNames(DeclarationDescriptor declaration, FieldDeclarationSyntax field, List<ConventionDiagnostic> diagnostics)
        {
            bool isConstant = field.Modifiers.Any(SyntaxKind.ConstKeyword);
            foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
            {
                string name = variable.Identifier.ValueText;
                if (isConstant && IsScreamingSnakeCase(name))
                {
                    continue;
                }

                if (declaration.Accessibility == "private" && !IsCamelCase(name))
                {
                    Add(diagnostics, RuleID.NameCamel, SourceRange.FromLocation(variable.Identifier.GetLocation()), name, "private 필드는 camelCase를 사용해야 하며 const는 SCREAMING_SNAKE_CASE도 허용합니다.");
                }
                else if (declaration.Accessibility == "public" && !IsPascalCase(name))
                {
                    Add(diagnostics, RuleID.NamePublicField, SourceRange.FromLocation(variable.Identifier.GetLocation()), name, "public 필드는 PascalCase를 사용해야 하며 const는 SCREAMING_SNAKE_CASE도 허용합니다.");
                }
                else if (IsFlexibleFieldAccessibility(declaration.Accessibility) && !IsCamelCase(name) && !IsPascalCase(name))
                {
                    Add(diagnostics, RuleID.NameFlexibleField, SourceRange.FromLocation(variable.Identifier.GetLocation()), name, "protected/internal 계열 필드는 camelCase 또는 PascalCase를 사용해야 하며 const는 SCREAMING_SNAKE_CASE도 허용합니다.");
                }
            }
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// camelCase와 PascalCase를 모두 허용하는 필드 접근 수준인지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsFlexibleFieldAccessibility(string accessibility)
        {
            return accessibility is "protected" or "internal" or "protected internal" or "private protected";
        }

    #endregion

    #region 표기 판정

        // ------------------------------------------------------------
        /// <summary>
        /// 식별자가 PascalCase 시작 조건을 만족하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsPascalCase(string name)
        {
            string coreName = RemoveUnderscores(name);
            return IsUnderscoreOnly(name, coreName) || IsPascalCore(coreName);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 식별자가 camelCase 표기 조건을 만족하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsCamelCase(string name)
        {
            string coreName = RemoveUnderscores(name);
            return IsUnderscoreOnly(name, coreName) || IsCamelCore(coreName);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 이름이 하나 이상의 밑줄로만 구성돼 casing 본체가 없는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsUnderscoreOnly(string name, string coreName)
        {
            return name.Length > 0 && coreName.Length == 0 && name.All(character => character == '_');
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 인터페이스 이름이 밑줄을 제외했을 때 I 다음 PascalCase 형태인지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsInterfaceName(string name)
        {
            string coreName = RemoveUnderscores(name);
            return coreName.Length >= 2 && coreName[0] == 'I' && IsPascalCore(coreName[1..]);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 밑줄을 제외한 이름이 PascalCase 조건을 만족하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsPascalCore(string name)
        {
            return name.Length > 0 && char.IsUpper(name[0]) && name.All(char.IsLetterOrDigit);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 밑줄을 제외한 이름이 camelCase 조건을 만족하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsCamelCore(string name)
        {
            return name.Length > 0 && char.IsLower(name[0]) && name.All(char.IsLetterOrDigit);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// const 이름이 밑줄을 제외했을 때 모두 대문자·숫자로 구성됐는지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsScreamingSnakeCase(string name)
        {
            string coreName = RemoveUnderscores(name);
            bool hasLetter = coreName.Any(char.IsLetter);
            bool hasOnlyUpperOrDigit = coreName.All(character => char.IsDigit(character) || char.IsUpper(character));
            return coreName.Length > 0 && hasLetter && hasOnlyUpperOrDigit;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// casing 판정에서 제외할 모든 밑줄을 제거한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string RemoveUnderscores(string name)
        {
            return name.Replace("_", string.Empty, StringComparison.Ordinal);
        }

    #endregion

    #region 공통 탐색과 진단

        // ------------------------------------------------------------
        /// <summary>
        /// 명명 규칙 ERROR 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, SourceRange range, string name, string message)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, range, message, name));
        }

    #endregion

    }

}
