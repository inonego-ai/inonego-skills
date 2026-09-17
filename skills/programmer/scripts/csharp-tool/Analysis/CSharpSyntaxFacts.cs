/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : CSharpSyntaxFacts.cs
수정일 : 2026-09-17

# 설명
여러 분석·검사 단계가 공유하는 구문 구조의 확정 가능한 사실을 제공한다.
========================================================================= BLOCK_HEADER_END */

using System;
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
    /// 규칙 의미와 무관한 공통 C# 구문 사실을 판정한다.
    /// </summary>
    // ============================================================
    internal static class CSharpSyntaxFacts
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 구문 노드가 한 줄에 완전히 포함되는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool IsSingleLine(SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            return span.StartLinePosition.Line == span.EndLinePosition.Line;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 두 토큰이 같은 원본 줄에 있는지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool IsSingleLine(SyntaxToken first, SyntaxToken last)
        {
            int startLine = first.GetLocation().GetLineSpan().StartLinePosition.Line;
            int endLine = last.GetLocation().GetLineSpan().EndLinePosition.Line;
            return startLine == endLine;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 한 줄 앞쪽의 연속 공백 개수를 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static int GetLeadingSpaceCount(string line)
        {
            int count = 0;
            while (count < line.Length && line[count] == ' ')
            {
                count++;
            }

            return count;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 토큰 앞의 같은 줄 내용이 공백뿐인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool IsFirstTokenOnLine(SourceText sourceText, SyntaxToken token)
        {
            int lineIndex = token.GetLocation().GetLineSpan().StartLinePosition.Line;
            TextLine line = sourceText.Lines[lineIndex];
            int column = token.SpanStart - line.Start;
            return line.ToString()[..Math.Max(0, column)].All(char.IsWhiteSpace);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 접근자가 본문 없는 자동 접근자인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        public static bool IsAutoAccessor(AccessorDeclarationSyntax accessor)
        {
            bool hasBody = accessor.Body != null || accessor.ExpressionBody != null;
            bool hasSemicolon = accessor.SemicolonToken.IsKind(SyntaxKind.SemicolonToken);
            return !hasBody && hasSemicolon;
        }
    }

}
