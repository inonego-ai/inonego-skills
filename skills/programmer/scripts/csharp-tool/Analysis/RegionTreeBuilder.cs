/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RegionTreeBuilder.cs
수정일 : 2026-09-17

# 설명
#region 지시문을 중첩 트리로 만들고 선언과 소유 범위를 연결한다.

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
    /// 전처리 region 지시문을 원본 위치 기준으로 연결한다.
    /// </summary>
    // ============================================================
    internal static class RegionTreeBuilder
    {

    #region region 트리 구성

        // ------------------------------------------------------------
        /// <summary>
        /// 파일의 region 트리를 만들고 선언의 소속 region을 지정한다.
        /// </summary>
        // ------------------------------------------------------------
        public static List<RegionNode> Build(CompilationUnitSyntax root, SourceText sourceText, IReadOnlyList<DeclarationDescriptor> declarations)
        {
            // 지시문 중첩 구조를 먼저 확정해야 선언 소유권과 무관하게 region 경계를 보존할 수 있다.
            List<RegionNode> roots = BuildRegionTree(root, sourceText);

            // 완성된 region 트리에 선언 소유권과 선언별 innermost region을 후속 단계로 연결한다.
            ConnectDeclarations(roots, declarations);
            return roots;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// #region/#endregion 지시문을 원본 순서의 중첩 트리로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static List<RegionNode> BuildRegionTree(CompilationUnitSyntax root, SourceText sourceText)
        {
            List<RegionNode> roots = new();
            Stack<RegionNode> stack = new();
            IEnumerable<DirectiveTriviaSyntax> directives = GetOrderedDirectives(root);

            foreach (DirectiveTriviaSyntax directive in directives)
            {
                if (directive is RegionDirectiveTriviaSyntax regionDirective)
                {
                    RegionNode region = CreateRegion(regionDirective, sourceText);
                    AttachRegion(region, roots, stack);
                    stack.Push(region);
                    continue;
                }

                if (directive is EndRegionDirectiveTriviaSyntax endDirective && stack.Count > 0)
                {
                    CloseRegion(stack.Pop(), endDirective, sourceText);
                }
            }

            // 닫히지 않은 region은 EOF까지 범위를 남겨 구문 오류가 있어도 가능한 구조를 제공한다.
            while (stack.Count > 0)
            {
                RegionNode region = stack.Pop();
                region.EndPosition = root.Span.End;
                region.EndLine = sourceText.Lines.Count;
            }

            return roots;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// region 처리 대상 지시문을 원본 순서로 열거한다.
        /// </summary>
        // ------------------------------------------------------------
        private static IEnumerable<DirectiveTriviaSyntax> GetOrderedDirectives(CompilationUnitSyntax root)
        {
            return root.DescendantTrivia(descendIntoTrivia: true)
                .Select(trivia => trivia.GetStructure())
                .OfType<DirectiveTriviaSyntax>()
                .OrderBy(directive => directive.SpanStart);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 시작 region 지시문에서 기본 위치 정보를 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static RegionNode CreateRegion(RegionDirectiveTriviaSyntax directive, SourceText sourceText)
        {
            RegionNode region = new();
            region.Name = GetRegionName(directive);
            region.StartPosition = directive.SpanStart;
            region.StartLine = GetLine(sourceText, directive.SpanStart);
            return region;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 새 region을 현재 부모 또는 최상위 목록에 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AttachRegion(RegionNode region, List<RegionNode> roots, Stack<RegionNode> stack)
        {
            if (stack.Count == 0)
            {
                roots.Add(region);
                return;
            }

            region.Parent = stack.Peek();
            region.Parent.Children.Add(region);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 종료 지시문으로 region의 원본 끝 위치를 확정한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void CloseRegion(RegionNode region, EndRegionDirectiveTriviaSyntax directive, SourceText sourceText)
        {
            region.EndPosition = directive.Span.End;
            region.EndLine = GetLine(sourceText, directive.SpanStart);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// #region 지시문에서 제목 부분만 추출한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string GetRegionName(RegionDirectiveTriviaSyntax directive)
        {
            string text = directive.ToString().Trim();
            return text.StartsWith("#region", StringComparison.Ordinal) ? text[7..].Trim() : string.Empty;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 문자 위치의 1-based 원본 줄 번호를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static int GetLine(SourceText sourceText, int position)
        {
            return sourceText.Lines.GetLineFromPosition(Math.Min(position, sourceText.Length)).LineNumber + 1;
        }

    #endregion

    #region 선언 소유권 연결

        // ------------------------------------------------------------
        /// <summary>
        /// region과 선언 사이의 소유 관계를 양방향으로 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ConnectDeclarations(IReadOnlyList<RegionNode> roots, IReadOnlyList<DeclarationDescriptor> declarations)
        {
            List<DeclarationDescriptor> allDeclarations = AnalysisTreeTraversal.FlattenDeclarations(declarations).ToList();

            // 각 region은 자신 전체를 가장 좁게 감싸는 선언을 소유자로 선택한다.
            foreach (RegionNode region in AnalysisTreeTraversal.FlattenRegions(roots))
            {
                region.Owner = FindOwner(region, allDeclarations);
            }

            // 각 선언은 자신의 전체 범위를 포함하는 가장 안쪽 region에 연결한다.
            foreach (DeclarationDescriptor declaration in allDeclarations)
            {
                declaration.Region = FindInnermostRegion(declaration, roots);
            }
        }


        // ------------------------------------------------------------
        /// <summary>
        /// region 전체를 가장 좁게 감싸는 실행 또는 선언 소유자를 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static DeclarationDescriptor? FindOwner(RegionNode region, IReadOnlyList<DeclarationDescriptor> declarations)
        {
            DeclarationDescriptor? owner = null;
            int bestLength = int.MaxValue;

            foreach (DeclarationDescriptor declaration in declarations)
            {
                bool canOwn = CanOwnRegion(declaration.Kind);
                bool startsBeforeRegion = declaration.StartPosition < region.StartPosition;
                bool endsAfterRegion = declaration.EndPosition > region.EndPosition;
                if (!canOwn || !startsBeforeRegion || !endsAfterRegion)
                {
                    continue;
                }

                // 후보가 여러 개면 region을 가장 좁게 감싸는 선언이 실제 직접 소유자다.
                int length = declaration.EndPosition - declaration.StartPosition;
                if (length < bestLength)
                {
                    bestLength = length;
                    owner = declaration;
                }
            }

            return owner;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언이 region을 직접 소유할 수 있는 종류인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool CanOwnRegion(string kind)
        {
            return kind is "class" or "struct" or "interface" or "enum" or "record" or "record struct"
                or "method" or "constructor" or "static constructor" or "destructor"
                or "operator" or "conversion operator" or "local function";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 범위를 포함하는 가장 안쪽 region을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static RegionNode? FindInnermostRegion(DeclarationDescriptor declaration, IReadOnlyList<RegionNode> roots)
        {
            RegionNode? result = null;
            foreach (RegionNode region in AnalysisTreeTraversal.FlattenRegions(roots))
            {
                bool startsBeforeDeclaration = region.StartPosition < declaration.StartPosition;
                bool endsAfterDeclaration = region.EndPosition > declaration.EndPosition;
                if (!startsBeforeDeclaration || !endsAfterDeclaration)
                {
                    continue;
                }

                // 포함 region이 여러 개면 시작 위치가 가장 안쪽에 가까운 region을 선택한다.
                if (result == null || region.StartPosition >= result.StartPosition)
                {
                    result = region;
                }
            }

            return result;
        }

    #endregion

    }

}
