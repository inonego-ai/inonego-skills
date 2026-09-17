/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : UsingRule.cs
수정일 : 2026-09-17

# 설명
일반 namespace using의 큰 그룹 순서와 계층 선언 규칙을 검사한다.
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
    /// using 그룹과 상위 네임스페이스 순차 선언 규칙을 검사한다.
    /// </summary>
    // ============================================================
    internal static class UsingRule
    {

    #region 검사 흐름

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 및 namespace 내부 using 목록마다 규칙을 적용한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Check(CSharpFileAnalysis analysis, List<ConventionDiagnostic> diagnostics)
        {
            CheckScope(analysis, analysis.Root.Usings, diagnostics);
            foreach (BaseNamespaceDeclarationSyntax namespaceDeclaration in analysis.Root.DescendantNodes().OfType<BaseNamespaceDeclarationSyntax>())
            {
                CheckScope(analysis, namespaceDeclaration.Usings, diagnostics);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 하나의 using 선언 구간에서 순서와 공백 및 계층을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckScope(CSharpFileAnalysis analysis, SyntaxList<UsingDirectiveSyntax> usingList, List<ConventionDiagnostic> diagnostics)
        {
            // 모든 using을 대상으로 큰 그룹 순서와 그룹 경계 공백을 먼저 검사한다.
            CheckGroupLayout(analysis, usingList, diagnostics);

            // 상위 namespace 계층 규칙은 alias/static/global을 제외한 일반 using에만 적용한다.
            List<UsingDirectiveSyntax> normalUsings = usingList.Where(IsNormalNamespaceUsing).ToList();
            Dictionary<string, int> indexByName = BuildIndexByName(normalUsings);
            CheckParentChain(normalUsings, indexByName, diagnostics);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 일반 using 이름을 원본 순서 인덱스로 매핑한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Dictionary<string, int> BuildIndexByName(IReadOnlyList<UsingDirectiveSyntax> usingList)
        {
            Dictionary<string, int> indexByName = new(StringComparer.Ordinal);
            for (int index = 0; index < usingList.Count; index++)
            {
                string name = GetName(usingList[index]);
                if (!indexByName.ContainsKey(name))
                {
                    indexByName.Add(name, index);
                }
            }

            return indexByName;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 각 일반 using의 모든 상위 namespace가 존재하고 먼저 배치됐는지 검사한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void CheckParentChain(IReadOnlyList<UsingDirectiveSyntax> usingList, IReadOnlyDictionary<string, int> indexByName, List<ConventionDiagnostic> diagnostics)
        {
            for (int index = 0; index < usingList.Count; index++)
            {
                UsingDirectiveSyntax current = usingList[index];
                string name = GetName(current);
                string[] parts = name.Split('.', StringSplitOptions.RemoveEmptyEntries);

                // A.B.C라면 A와 A.B를 차례로 만들어 존재 여부와 원본 순서를 함께 확인한다.
                for (int partCount = 1; partCount < parts.Length; partCount++)
                {
                    string parent = string.Join('.', parts.Take(partCount));
                    if (!indexByName.TryGetValue(parent, out int parentIndex))
                    {
                        Add(diagnostics, RuleID.UsingParentMissing, current, $"`{name}`의 상위 네임스페이스 `{parent}` using이 없습니다.");
                    }
                    else if (parentIndex > index)
                    {
                        Add(diagnostics, RuleID.UsingParentOrder, current, $"상위 네임스페이스 `{parent}` using을 `{name}`보다 먼저 선언해야 합니다.");
                    }
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 using 지시문의 큰 그룹 순서와 그룹 사이 빈 줄을 검사한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CheckGroupLayout(CSharpFileAnalysis analysis, SyntaxList<UsingDirectiveSyntax> usingList, List<ConventionDiagnostic> diagnostics)
        {
            int previousGroup = -1;
            UsingDirectiveSyntax? previousUsing = null;

            foreach (UsingDirectiveSyntax current in usingList)
            {
                string name = GetName(current);
                if (name.Length == 0)
                {
                    continue;
                }

                // 현재 그룹이 앞 그룹보다 작아지면 System → Unity → 나머지 큰 순서가 역전된 것이다.
                int group = GetGroup(name);
                if (previousGroup > group)
                {
                    Add(diagnostics, RuleID.UsingGroupOrder, current, $"using `{name}`의 그룹 순서가 System → Unity → 나머지 순서를 따르지 않습니다.");
                }

                // 그룹이 바뀌는 경계에서만 정확히 한 줄의 빈 줄을 요구한다.
                bool changesGroup = previousUsing != null && previousGroup != group;
                if (changesGroup && CountBlankLinesBetween(analysis, previousUsing!, current) != 1)
                {
                    Add(diagnostics, RuleID.UsingGroupSpacing, current, "서로 다른 using 그룹 사이에는 빈 줄을 정확히 한 줄 둬야 합니다.");
                }

                previousGroup = group;
                previousUsing = current;
            }
        }

    #endregion

    #region 분류와 원본 간격

        // ----------------------------------------------------------------------
        /// <summary>
        /// 별칭과 static/global using이 아닌 일반 namespace using인지 확인한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static bool IsNormalNamespaceUsing(UsingDirectiveSyntax directive)
        {
            bool hasAlias = directive.Alias != null;
            bool isStatic = directive.StaticKeyword.IsKind(SyntaxKind.StaticKeyword);
            bool isGlobal = directive.GlobalKeyword.IsKind(SyntaxKind.GlobalKeyword);
            bool hasModifier = hasAlias || isStatic || isGlobal;
            bool hasNamespaceName = directive.Name != null;
            return !hasModifier && hasNamespaceName;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// using 이름에서 global 별칭 접두어를 제거해 비교용 문자열을 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetName(UsingDirectiveSyntax directive)
        {
            string name = directive.Name?.ToString() ?? string.Empty;
            return name.StartsWith("global::", StringComparison.Ordinal) ? name[8..] : name;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// System, Unity, 나머지의 큰 using 그룹 번호를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int GetGroup(string name)
        {
            if (name == "System" || name.StartsWith("System.", StringComparison.Ordinal))
            {
                return 0;
            }

            if (IsUnityNamespace(name))
            {
                return 1;
            }

            return 2;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// namespace 이름이 확정 가능한 Unity 계열 루트에 속하는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsUnityNamespace(string name)
        {
            bool isUnityRoot = name == "Unity" || name.StartsWith("Unity.", StringComparison.Ordinal);
            bool isEngineRoot = name == "UnityEngine" || name.StartsWith("UnityEngine.", StringComparison.Ordinal);
            bool isEditorRoot = name == "UnityEditor" || name.StartsWith("UnityEditor.", StringComparison.Ordinal);
            return isUnityRoot || isEngineRoot || isEditorRoot;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 두 using 선언 사이의 완전히 빈 줄 개수를 계산한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int CountBlankLinesBetween(CSharpFileAnalysis analysis, UsingDirectiveSyntax previous, UsingDirectiveSyntax current)
        {
            int previousLine = previous.GetLocation().GetLineSpan().EndLinePosition.Line;
            int currentLine = current.GetLocation().GetLineSpan().StartLinePosition.Line;
            int blankLines = 0;
            for (int line = previousLine + 1; line < currentLine; line++)
            {
                if (string.IsNullOrWhiteSpace(analysis.SourceText.Lines[line].ToString()))
                {
                    blankLines++;
                }
            }

            return blankLines;
        }

    #endregion

    #region 진단 생성

        // ------------------------------------------------------------
        /// <summary>
        /// using 규칙 ERROR 진단을 추가한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void Add(List<ConventionDiagnostic> diagnostics, string id, SyntaxNode node, string message)
        {
            diagnostics.Add(ConventionDiagnostic.Create(id, SourceRange.FromLocation(node.GetLocation()), message));
        }

    #endregion

    }

}
