/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : DeclarationDescriptor.cs
수정일 : 2026-09-17

# 설명
C# 선언의 구조 특성과 원본 Roslyn 노드 참조를 기술한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft;
using Microsoft.CodeAnalysis;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// C# 선언 하나의 구조 특성과 원본 Roslyn 노드 참조를 기술한다.
    /// </summary>
    // ============================================================
    internal sealed class DeclarationDescriptor
    {
        public string Kind                          = string.Empty;
        public string Name                          = string.Empty;
        public string Header                        = string.Empty;
        public string Accessibility                 = string.Empty;
        public List<string> Modifiers               = new();
        public List<string> Attributes              = new();
        public SourceRange Range                    = SourceRange.FromLine(1);
        public int StartPosition                    = 0;
        public int EndPosition                      = 0;
        public DeclarationDescriptor? Parent        = null;
        public RegionNode? Region                   = null;
        public SummaryDescriptor? Summary           = null;
        public SyntaxNode Node                      = null!;
        public List<DeclarationDescriptor> Children = new();
    }

}
