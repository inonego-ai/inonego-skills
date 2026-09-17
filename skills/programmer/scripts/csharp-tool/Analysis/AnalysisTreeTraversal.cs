/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : AnalysisTreeTraversal.cs
수정일 : 2026-09-17

# 설명
분석 결과의 선언 트리와 region 트리를 원본 순서로 평면 순회한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 분석 트리의 공통 깊이 우선 순회 순서를 제공한다.
    /// </summary>
    // ============================================================
    internal static class AnalysisTreeTraversal
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 선언 트리를 부모 다음 자식 순서로 평면 열거한다.
        /// </summary>
        // ------------------------------------------------------------
        public static IEnumerable<DeclarationDescriptor> FlattenDeclarations(IEnumerable<DeclarationDescriptor> declarations)
        {
            foreach (DeclarationDescriptor declaration in declarations)
            {
                yield return declaration;

                foreach (DeclarationDescriptor child in FlattenDeclarations(declaration.Children))
                {
                    yield return child;
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// region 트리를 부모 다음 자식 순서로 평면 열거한다.
        /// </summary>
        // ------------------------------------------------------------
        public static IEnumerable<RegionNode> FlattenRegions(IEnumerable<RegionNode> regions)
        {
            foreach (RegionNode region in regions)
            {
                yield return region;

                foreach (RegionNode child in FlattenRegions(region.Children))
                {
                    yield return child;
                }
            }
        }
    }

}