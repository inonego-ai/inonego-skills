/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RegionNode.cs
수정일 : 2026-09-17

# 설명
#region 트리의 한 노드와 선언 소유 관계 및 원본 위치를 나타낸다.

========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// region 트리의 한 노드와 선언 소유 관계를 나타낸다.
    /// </summary>
    // ============================================================
    internal sealed class RegionNode
    {
        public string Name                  = string.Empty;
        public int StartLine                = 0;
        public int EndLine                  = 0;
        public int StartPosition            = 0;
        public int EndPosition              = 0;
        public RegionNode? Parent           = null;
        public List<RegionNode> Children    = new();
        public DeclarationDescriptor? Owner = null;
    }

}
