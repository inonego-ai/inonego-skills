/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SummaryDescriptor.cs
수정일 : 2026-09-17

# 설명
XML summary의 원문·구분선·줄바꿈 특성을 기술한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 선언에 연결된 XML summary의 형식 특성을 기술한다.
    /// </summary>
    // ============================================================
    internal sealed class SummaryDescriptor
    {
        public bool Exists                        = false;
        public bool HasInlineTagContent           = false;
        public List<string> RawDescriptionLines   = new();
        public List<string> DescriptionLines      = new();
        public List<bool> DescriptionLineHasBreak = new();
        public string TopSeparator                = string.Empty;
        public string BottomSeparator             = string.Empty;
        public bool TopSeparatorAdjacent          = false;
        public bool BottomSeparatorAdjacent       = false;
        public SourceRange Range                  = SourceRange.FromLine(1);
    }

}
