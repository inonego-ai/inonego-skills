/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : SummaryParser.cs
수정일 : 2026-09-17

# 설명
선언 앞 XML summary와 구분선 정보를 추출한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 선언 trivia에서 XML summary 내용을 추출한다.
    /// </summary>
    // ============================================================
    internal static class SummaryParser
    {

    #region 요약 연결과 해석

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 트리 전체에 XML summary 정보를 연결한다.
        /// </summary>
        // ------------------------------------------------------------
        public static void Attach(IReadOnlyList<DeclarationDescriptor> declarations, SourceText sourceText)
        {
            foreach (DeclarationDescriptor declaration in declarations)
            {
                AttachRecursive(declaration, sourceText);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 하나와 모든 하위 선언의 XML summary를 분석한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AttachRecursive(DeclarationDescriptor declaration, SourceText sourceText)
        {
            declaration.Summary = Parse(declaration.Node, sourceText);
            foreach (DeclarationDescriptor child in declaration.Children)
            {
                AttachRecursive(child, sourceText);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언의 leading trivia에서 가장 가까운 summary를 파싱한다.
        /// </summary>
        // ------------------------------------------------------------
        private static SummaryDescriptor? Parse(SyntaxNode node, SourceText sourceText)
        {
            SyntaxTriviaList trivia = node.GetLeadingTrivia();
            if (trivia.Count == 0)
            {
                return null;
            }

            string text = trivia.ToFullString();
            List<(string Line, int Offset)> lines = SplitLines(text);

            // 선언 직전 trivia에서 마지막으로 완결된 summary 쌍을 선택해 더 먼 주석과 잘못 연결하지 않는다.
            int open = -1;
            int close = -1;
            string openingContent = string.Empty;
            string closingContent = string.Empty;
            bool hasInlineTagContent = false;
            for (int index = 0; index < lines.Count; index++)
            {
                string line = lines[index].Line.TrimStart();
                if (line.StartsWith("/// <summary>", StringComparison.Ordinal))
                {
                    open = index;
                    close = -1;
                    openingContent = line["/// <summary>".Length..].Trim();
                    closingContent = string.Empty;
                    hasInlineTagContent = openingContent.Length > 0;

                    int sameLineClose = openingContent.IndexOf("</summary>", StringComparison.Ordinal);
                    if (sameLineClose >= 0)
                    {
                        close = index;
                        openingContent = openingContent[..sameLineClose].Trim();
                    }

                    continue;
                }

                if (open < 0 || !line.StartsWith("///", StringComparison.Ordinal))
                {
                    continue;
                }

                int closeTag = line.IndexOf("</summary>", StringComparison.Ordinal);
                if (closeTag < 0)
                {
                    continue;
                }

                close = index;
                closingContent = line[3..closeTag].Trim();
                hasInlineTagContent |= !string.Equals(line, "/// </summary>", StringComparison.Ordinal);
            }

            if (open < 0 || close < open)
            {
                return null;
            }

            // Inspector는 태그를 제거한 설명을 쓰고 Checker는 원문 폭과 `<br/>` 형식을 보므로 두 표현을 함께 보존한다.
            SummaryDescriptor summary = new();
            summary.Exists = true;
            summary.HasInlineTagContent = hasInlineTagContent;
            AddDescriptionLine(summary, openingContent);
            for (int index = open + 1; index < close; index++)
            {
                AddDescriptionLine(summary, lines[index].Line.TrimStart());
            }

            AddDescriptionLine(summary, closingContent);

            // 구분선 원문과 바로 붙어 있는지 여부를 따로 저장해 출력 정규화와 형식 판정을 섞지 않는다.
            summary.TopSeparator = FindSeparator(lines, open - 1, -1);
            summary.BottomSeparator = FindSeparator(lines, close + 1, 1);
            summary.TopSeparatorAdjacent = open > 0 && IsSeparator(lines[open - 1].Line.Trim());
            summary.BottomSeparatorAdjacent = close + 1 < lines.Count && IsSeparator(lines[close + 1].Line.Trim());
            int triviaStart = node.FullSpan.Start;
            int startPosition = triviaStart + lines[open].Offset;
            int endPosition = triviaStart + lines[close].Offset + lines[close].Line.Length;
            LinePosition start = sourceText.Lines.GetLinePosition(Math.Min(startPosition, sourceText.Length));
            LinePosition end = sourceText.Lines.GetLinePosition(Math.Min(endPosition, sourceText.Length));
            summary.Range = new(start.Line + 1, start.Character + 1, end.Line + 1, end.Character + 1);
            return summary;
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// summary 설명 후보 한 줄을 정규화해 원문 폭과 br 사용 여부를 보존한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void AddDescriptionLine(SummaryDescriptor summary, string source)
        {
            string raw = source.TrimStart();
            if (raw.Length == 0)
            {
                return;
            }

            string content = raw.StartsWith("///", StringComparison.Ordinal)
                ? raw[3..].TrimStart()
                : raw;
            bool hasBreak = content.StartsWith("<br/>", StringComparison.Ordinal);
            if (hasBreak)
            {
                content = content[5..].TrimStart();
            }

            if (content.Length == 0)
            {
                return;
            }

            summary.RawDescriptionLines.Add(raw);
            summary.DescriptionLines.Add(content);
            summary.DescriptionLineHasBreak.Add(hasBreak);
        }

    #endregion

    #region 원본 줄과 구분선

        // ------------------------------------------------------------
        /// <summary>
        /// summary 주변에서 가장 가까운 구분선 주석을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static string FindSeparator(IReadOnlyList<(string Line, int Offset)> lines, int start, int step)
        {
            for (int index = start; index >= 0 && index < lines.Count; index += step)
            {
                string line = lines[index].Line.Trim();
                if (line.Length == 0)
                {
                    continue;
                }

                if (IsSeparator(line))
                {
                    return line;
                }

                break;
            }

            return string.Empty;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 한 줄이 summary 구분선 주석인지 확인한다.
        /// </summary>
        // ------------------------------------------------------------
        private static bool IsSeparator(string line)
        {
            if (!line.StartsWith("// ", StringComparison.Ordinal) || line.Length <= 3)
            {
                return false;
            }

            char marker = line[3];
            if (marker != '-' && marker != '=')
            {
                return false;
            }

            for (int index = 3; index < line.Length; index++)
            {
                if (line[index] != marker)
                {
                    return false;
                }
            }

            return true;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 문자열을 원본 오프셋과 함께 줄 단위로 분리한다.
        /// </summary>
        // ------------------------------------------------------------
        private static List<(string Line, int Offset)> SplitLines(string text)
        {
            List<(string Line, int Offset)> lines = new();
            int start = 0;
            for (int index = 0; index <= text.Length; index++)
            {
                if (index != text.Length && text[index] != '\r' && text[index] != '\n')
                {
                    continue;
                }

                lines.Add((text[start..index], start));
                bool hasCarriageReturn = index < text.Length && text[index] == '\r';
                bool hasFollowingLineFeed = index + 1 < text.Length && text[index + 1] == '\n';
                if (hasCarriageReturn && hasFollowingLineFeed)
                {
                    index++;
                }

                start = index + 1;
            }

            return lines;
        }

    #endregion

    }

}
