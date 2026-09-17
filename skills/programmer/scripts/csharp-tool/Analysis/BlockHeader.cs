/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : BlockHeader.cs
수정일 : 2026-09-17

# 설명
파싱한 Block Header의 필드 값과 원본 범위를 나타낸다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 파싱한 Block Header의 필드 값과 원본 범위를 나타낸다.
    /// </summary>
    // ============================================================
    internal sealed class BlockHeader
    {
        public bool Exists         = false;
        public bool Malformed      = false;
        public string FileName     = string.Empty;
        public string ModifiedDate = string.Empty;
        public string Description  = string.Empty;
        public string Constraints  = string.Empty;
        public SourceRange Range   = SourceRange.FromLine(1);
    }

}
