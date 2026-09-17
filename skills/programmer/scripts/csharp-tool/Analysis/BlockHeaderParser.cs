/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : BlockHeaderParser.cs
수정일 : 2026-09-17

# 설명
파일 선두의 Block Header 형식과 핵심 항목을 추출한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// SourceText에서 Block Header를 찾아 구조 정보로 변환한다.
    /// </summary>
    // ============================================================
    internal static class BlockHeaderParser
    {

    #region 헤더 해석

        private const string startMarker       = "/* BLOCK_HEADER_BEGIN";
        private const string endMarker         = "BLOCK_HEADER_END */";
        private const string expectedStartLine = "/* BLOCK_HEADER_BEGIN =======================================================================";
        private const string expectedEndLine   = "========================================================================= BLOCK_HEADER_END */";

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 선두 주석에서 Block Header 항목을 추출한다.
        /// </summary>
        // ------------------------------------------------------------
        public static BlockHeader Parse(SourceText sourceText)
        {
            string text = sourceText.ToString();
            int start = text.IndexOf(startMarker, StringComparison.Ordinal);
            int end = text.IndexOf(endMarker, StringComparison.Ordinal);
            BlockHeader header = new();

            // 시작/끝 마커의 조합으로 헤더 부재와 손상 상태를 먼저 구분한다.
            if (start < 0 && end < 0)
            {
                return header;
            }

            if (start < 0 || end < start)
            {
                header.Malformed = true;
                return header;
            }

            // 유효한 범위가 확보된 뒤에만 원본 위치와 고정 표식의 정확한 형태를 기록한다.
            int endExclusive = end + endMarker.Length;
            header.Exists = true;
            header.Range = CreateRange(sourceText, start, endExclusive);
            int startLineIndex = sourceText.Lines.GetLineFromPosition(start).LineNumber;
            int endLineIndex = sourceText.Lines.GetLineFromPosition(end).LineNumber;
            string actualStartLine = sourceText.Lines[startLineIndex].ToString();
            string actualEndLine = sourceText.Lines[endLineIndex].ToString();
            bool hasExpectedBorder = string.Equals(actualStartLine, expectedStartLine, StringComparison.Ordinal)
                && string.Equals(actualEndLine, expectedEndLine, StringComparison.Ordinal);
            if (!hasExpectedBorder)
            {
                header.Malformed = true;
            }

            // 경계 검증과 본문 해석을 분리해 형식 손상 여부와 필드 추출 결과를 함께 보존한다.
            ParseBody(text.Substring(start, endExclusive - start), header);
            return header;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Block Header 본문에서 필드와 절 설명을 추출한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void ParseBody(string body, BlockHeader header)
        {
            string[] lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            string section = string.Empty;
            StringBuilder description = new();
            StringBuilder constraints = new();

            // 한 번의 순회에서 고정 필드와 현재 활성 절을 함께 해석해 원본 순서를 그대로 유지한다.
            foreach (string sourceLine in lines)
            {
                string line = sourceLine.Trim();

                // 파일명과 수정일은 어느 절에 있든 헤더의 고정 필드로 우선 해석한다.
                if (line.StartsWith("파일명", StringComparison.Ordinal))
                {
                    header.FileName = ReadValue(line);
                    continue;
                }

                if (line.StartsWith("수정일", StringComparison.Ordinal))
                {
                    header.ModifiedDate = ReadValue(line);
                    continue;
                }

                // 절 제목을 만나면 이후 일반 줄이 어느 설명 버퍼로 들어갈지 전환한다.
                if (line == "# 설명")
                {
                    section = "description";
                    continue;
                }

                if (line.StartsWith("# 특이사항", StringComparison.Ordinal)
                    || line.StartsWith("# 제약사항", StringComparison.Ordinal))
                {
                    section = "constraints";
                    continue;
                }

                // 그 밖의 절 제목을 만나면 앞 절의 내용 수집을 끝내 알려지지 않은 절이 설명에 섞이지 않게 한다.
                if (line.StartsWith("# ", StringComparison.Ordinal))
                {
                    section = string.Empty;
                    continue;
                }

                // 경계 표식과 빈 줄은 구조만 나타내므로 실제 설명 본문에는 포함하지 않는다.
                bool isStructuralLine = line.Length == 0
                    || line.StartsWith("/*", StringComparison.Ordinal)
                    || line.Contains("BLOCK_HEADER_END", StringComparison.Ordinal);
                if (isStructuralLine)
                {
                    continue;
                }

                // 남은 일반 줄은 현재 활성 절에만 누적해 절 사이 내용이 섞이지 않게 한다.
                if (section == "description")
                {
                    AppendLine(description, line);
                }
                else if (section == "constraints")
                {
                    AppendLine(constraints, line);
                }
            }

            header.Description = description.ToString();
            header.Constraints = constraints.ToString();
        }

    #endregion

    #region 원본 문자열 처리

        // ------------------------------------------------------------
        /// <summary>
        /// 콜론 오른쪽 값을 공백을 제거해 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static string ReadValue(string line)
        {
            int separator = line.IndexOf(':');
            return separator < 0 ? string.Empty : line[(separator + 1)..].Trim();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 줄 절을 줄바꿈을 보존해 누적한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void AppendLine(StringBuilder builder, string line)
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append(line);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 문자 위치 범위를 1-based 줄과 열로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static SourceRange CreateRange(SourceText sourceText, int start, int end)
        {
            LinePosition startPosition = sourceText.Lines.GetLinePosition(start);
            LinePosition endPosition = sourceText.Lines.GetLinePosition(Math.Min(end, sourceText.Length));
            return new(startPosition.Line + 1, startPosition.Character + 1, endPosition.Line + 1, endPosition.Character + 1);
        }

    #endregion

    }

}
