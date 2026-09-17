/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SourceRange.cs
수정일 : 2026-09-17

# 설명
원본 C# 파일의 1-based 줄과 열 범위를 보관한다.
========================================================================= BLOCK_HEADER_END */

using Microsoft;
using Microsoft.CodeAnalysis;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 원본 파일에서 진단과 선언이 차지하는 위치 범위다.
    /// </summary>
    // ============================================================
    internal readonly struct SourceRange
    {
        public readonly int StartLine;
        public readonly int StartColumn;
        public readonly int EndLine;
        public readonly int EndColumn;

        // ------------------------------------------------------------
        /// <summary>
        /// 줄과 열 위치로 범위를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public SourceRange(int startLine, int startColumn, int endLine, int endColumn) : this()
        {
            StartLine = startLine;
            StartColumn = startColumn;
            EndLine = endLine;
            EndColumn = endColumn;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Roslyn 위치를 1-based 범위로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static SourceRange FromLocation(Location location)
        {
            FileLinePositionSpan span = location.GetLineSpan();
            return new(span.StartLinePosition.Line + 1, span.StartLinePosition.Character + 1, span.EndLinePosition.Line + 1, span.EndLinePosition.Character + 1);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 시작 줄만 사용하는 단일 지점 범위를 만든다.
        /// </summary>
        // ------------------------------------------------------------
        public static SourceRange FromLine(int line)
        {
            return new(line, 1, line, 1);
        }
    }

}
