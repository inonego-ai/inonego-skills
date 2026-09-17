/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : TextReportWriter.cs
수정일 : 2026-09-17

# 설명
inspect, check, review 결과를 사람이 읽기 쉬운 text 형식으로 렌더링한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Microsoft;
using Microsoft.CodeAnalysis;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 공통 분석 결과와 진단을 기본 text 출력 계약으로 변환한다.
    /// </summary>
    // ============================================================
    internal static class TextReportWriter
    {

    #region 보고서 조립

        // ------------------------------------------------------------
        /// <summary>
        /// 명령 종류에 맞는 text 보고서를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public static string Write(IReadOnlyList<FileReport> reports, string command)
        {
            StringBuilder builder = new();

            bool showFullPath = reports.Count > 1;

            // 파일별 출력 계약은 단일 입력과 다중 입력에서 동일하게 유지하고 파일 사이만 한 줄로 구분한다.
            for (int index = 0; index < reports.Count; index++)
            {
                if (index > 0)
                {
                    builder.AppendLine();
                    builder.AppendLine();
                }

                WriteFile(builder, reports[index], command, showFullPath);
            }

            // 다중 입력은 파일별 진단 외에 전체 상태를 한 줄로 제공해 에이전트가 재검사 범위를 빠르게 판단하게 한다.
            if (reports.Count > 1)
            {
                int errors = reports.Sum(report => report.Diagnostics.Count(item => item.Severity == ConventionSeverity.Error));
                int warnings = reports.Sum(report => report.Diagnostics.Count(item => item.Severity == ConventionSeverity.Warn));
                int parseErrors = reports.Sum(report => report.Analysis.ParseDiagnostics.Count);
                builder.AppendLine();
                builder.AppendLine();
                builder.AppendLine($"TOTAL files={reports.Count} errors={errors} warnings={warnings} parse-errors={parseErrors}");
            }

            return builder.ToString().TrimEnd();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 하나의 구조·진단·파싱 오류를 명령 종류에 맞게 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteFile(StringBuilder builder, FileReport report, string command, bool showFullPath)
        {
            CSharpFileAnalysis analysis = report.Analysis;
            string displayPath = showFullPath ? analysis.FilePath : analysis.FileName;

            // inspect/review는 공통 구조 색인을 먼저 출력해 이후 진단 위치를 원본에서 찾을 수 있게 한다.
            if (command is "inspect" or "review")
            {
                WriteStructure(builder, analysis, command == "review", displayPath);
            }

            // check/review만 convention 섹션을 추가하고 inspect는 구조 정보에만 집중한다.
            if (command is "check" or "review")
            {
                if (command == "check")
                {
                    builder.AppendLine($"FILE {displayPath}");
                }
                else
                {
                    builder.AppendLine();
                    builder.AppendLine("CONVENTION");
                }

                WriteDiagnostics(builder, report.Diagnostics);
            }

            WriteParseDiagnostics(builder, displayPath, analysis.ParseDiagnostics);
        }

    #endregion

    #region 구조 출력

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 머리말과 선언·region 구조를 렌더링한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteStructure(StringBuilder builder, CSharpFileAnalysis analysis, bool reviewMode, string displayPath)
        {
            builder.AppendLine($"FILE {displayPath} [1-{analysis.SourceText.Lines.Count}]");
            builder.AppendLine();

            // 파일 메타데이터를 먼저 보여준 뒤 실제 선언 골격으로 내려가도록 출력 순서를 고정한다.
            WriteBlockHeader(builder, analysis.BlockHeader, reviewMode);
            builder.AppendLine();

            if (reviewMode)
            {
                builder.AppendLine("STRUCTURE");
            }

            foreach (DeclarationDescriptor declaration in analysis.Declarations.OrderBy(item => item.StartPosition))
            {
                WriteTopLevelDeclaration(builder, declaration, 0, analysis.Regions);
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Block Header의 현재 메타데이터만 구조 출력에 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteBlockHeader(StringBuilder builder, BlockHeader header, bool reviewMode)
        {
            builder.AppendLine(reviewMode ? "BLOCK HEADER" : "Block Header");
            if (!header.Exists)
            {
                builder.AppendLine("  없음");
                return;
            }

            if (header.ModifiedDate.Length > 0)
            {
                builder.AppendLine($"  수정일: {header.ModifiedDate}");
            }

            if (header.Description.Length > 0)
            {
                builder.AppendLine($"  설명: {FlattenText(header.Description)}");
            }

            if (header.Constraints.Length > 0)
            {
                builder.AppendLine($"  특이사항: {FlattenText(header.Constraints)}");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 최상위 namespace와 형식 선언을 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteTopLevelDeclaration(StringBuilder builder, DeclarationDescriptor declaration, int depth, IReadOnlyList<RegionNode> regions)
        {
            if (declaration.Kind == "namespace")
            {
                builder.AppendLine($"Namespace: {declaration.Name}");
                foreach (DeclarationDescriptor child in declaration.Children.OrderBy(item => item.StartPosition))
                {
                    WriteDeclaration(builder, child, depth, regions);
                }

                return;
            }

            WriteDeclaration(builder, declaration, depth, regions);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언 머리부와 summary 및 직접 하위 구조를 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteDeclaration(StringBuilder builder, DeclarationDescriptor declaration, int depth, IReadOnlyList<RegionNode> regions)
        {
            string indent = new(' ', depth * 2);
            string[] headerLines = declaration.Header.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
            for (int index = 0; index < headerLines.Length; index++)
            {
                string suffix = index == 0 ? $" [{declaration.Range.StartLine}-{declaration.Range.EndLine}]" : string.Empty;
                builder.AppendLine($"{indent}{headerLines[index].Trim()}{suffix}");
            }

            WriteSummary(builder, declaration.Summary, depth + 1);
            WriteOwnerContents(builder, declaration, depth + 1, regions);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// summary 설명이 있을 때만 표시한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteSummary(StringBuilder builder, SummaryDescriptor? summary, int depth)
        {
            if (summary == null || summary.DescriptionLines.Count == 0)
            {
                return;
            }

            string indent = new(' ', depth * 2);
            if (summary.DescriptionLines.Count == 1)
            {
                builder.AppendLine($"{indent}요약: {summary.DescriptionLines[0]}");
                return;
            }

            builder.AppendLine($"{indent}요약:");
            foreach (string line in summary.DescriptionLines)
            {
                builder.AppendLine($"{indent}  {line}");
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언이 직접 소유하는 region과 하위 선언을 원본 순서로 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteOwnerContents(StringBuilder builder, DeclarationDescriptor owner, int depth, IReadOnlyList<RegionNode> rootRegions)
        {
            List<(int Position, DeclarationDescriptor? Declaration, RegionNode? Region)> items = new();

            // region 밖 직접 자식은 owner 바로 아래에 남겨 region 유무 때문에 선언이 누락되지 않게 한다.
            foreach (DeclarationDescriptor child in owner.Children)
            {
                if (GetTopRegionForOwner(child, owner) == null)
                {
                    items.Add((child.StartPosition, child, null));
                }
            }

            // owner가 직접 소유하는 최상위 region을 같은 위치 목록에 합쳐 원본 순서를 복원한다.
            foreach (RegionNode region in GetDirectOwnerRegions(rootRegions, owner))
            {
                items.Add((region.StartPosition, null, region));
            }

            // 선언과 region을 하나의 원본 위치 축으로 정렬한 뒤 각 종류의 렌더러에 위임한다.
            foreach ((int _, DeclarationDescriptor? declaration, RegionNode? region) in items.OrderBy(item => item.Position))
            {
                if (declaration != null)
                {
                    WriteDeclaration(builder, declaration, depth, rootRegions);
                }
                else if (region != null)
                {
                    WriteRegion(builder, region, owner, depth, rootRegions);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// owner가 직접 소유하는 최상위 region을 원본 순서로 반환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static List<RegionNode> GetDirectOwnerRegions(IReadOnlyList<RegionNode> rootRegions, DeclarationDescriptor owner)
        {
            return AnalysisTreeTraversal.FlattenRegions(rootRegions)
                .Where(region => ReferenceEquals(region.Owner, owner))
                .Where(region => !ReferenceEquals(region.Parent?.Owner, owner))
                .OrderBy(region => region.StartPosition)
                .ToList();
        }

        // ------------------------------------------------------------
        /// <summary>
        /// region과 직접 선언 및 하위 region을 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteRegion(StringBuilder builder, RegionNode region, DeclarationDescriptor owner, int depth, IReadOnlyList<RegionNode> rootRegions)
        {
            string indent = new(' ', depth * 2);
            builder.AppendLine($"{indent}#region {region.Name} [{region.StartLine}-{region.EndLine}]");
            List<(int Position, DeclarationDescriptor? Declaration, RegionNode? Region)> items = new();

            // 현재 region에 직접 속한 선언만 추가해 하위 region의 선언이 중복 출력되지 않게 한다.
            foreach (DeclarationDescriptor child in owner.Children)
            {
                if (ReferenceEquals(child.Region, region))
                {
                    items.Add((child.StartPosition, child, null));
                }
            }

            // 같은 owner를 유지하는 하위 region도 위치 목록에 합쳐 중첩 구조를 원본 순서대로 보존한다.
            foreach (RegionNode childRegion in region.Children.Where(item => ReferenceEquals(item.Owner, owner)))
            {
                items.Add((childRegion.StartPosition, null, childRegion));
            }

            // 선언과 하위 region을 같은 위치 축으로 정렬해 실제 소스의 읽기 순서와 구조 출력을 맞춘다.
            foreach ((int _, DeclarationDescriptor? declaration, RegionNode? childRegion) in items.OrderBy(item => item.Position))
            {
                if (declaration != null)
                {
                    WriteDeclaration(builder, declaration, depth + 1, rootRegions);
                }
                else if (childRegion != null)
                {
                    WriteRegion(builder, childRegion, owner, depth + 1, rootRegions);
                }
            }
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 선언이 owner 바로 아래에서 속하는 최상위 region을 찾는다.
        /// </summary>
        // ------------------------------------------------------------
        private static RegionNode? GetTopRegionForOwner(DeclarationDescriptor declaration, DeclarationDescriptor owner)
        {
            RegionNode? region = declaration.Region;
            if (region == null || !ReferenceEquals(region.Owner, owner))
            {
                return null;
            }

            while (region.Parent != null && ReferenceEquals(region.Parent.Owner, owner))
            {
                region = region.Parent;
            }

            return region;
        }

    #endregion

    #region 진단 출력

        // ------------------------------------------------------------
        /// <summary>
        /// 컨벤션 진단과 합계를 text 형식으로 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteDiagnostics(StringBuilder builder, IReadOnlyList<ConventionDiagnostic> diagnostics)
        {
            if (diagnostics.Count == 0)
            {
                builder.AppendLine("  진단 없음");
            }

            // 각 진단은 위치와 이유를 먼저 보여주고, 기대 형태는 존재할 때만 보조 정보로 붙인다.
            foreach (ConventionDiagnostic diagnostic in diagnostics)
            {
                string severity = diagnostic.Severity.ToString().ToUpperInvariant();
                string range = FormatRange(diagnostic.Range);
                builder.AppendLine($"{severity} {diagnostic.ID} {range}");
                builder.AppendLine($"  {diagnostic.Message}");
                if (diagnostic.Expected.Length > 0)
                {
                    builder.AppendLine($"  기대 형태: {diagnostic.Expected}");
                }

                builder.AppendLine();
            }

            int errors = diagnostics.Count(item => item.Severity == ConventionSeverity.Error);
            int warnings = diagnostics.Count(item => item.Severity == ConventionSeverity.Warn);
            builder.AppendLine($"SUMMARY errors={errors} warnings={warnings}");
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Roslyn 파싱 오류를 파일명과 함께 출력한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void WriteParseDiagnostics(StringBuilder builder, string fileName, IReadOnlyList<Diagnostic> diagnostics)
        {
            foreach (Diagnostic diagnostic in diagnostics)
            {
                FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
                int line = span.StartLinePosition.Line + 1;
                int column = span.StartLinePosition.Character + 1;

                builder.AppendLine();
                builder.AppendLine($"PARSE ERROR {fileName} L{line}:C{column} {diagnostic.Id} {diagnostic.GetMessage()}");
            }
        }

    #endregion

    #region 공통 렌더링 helper

        // ------------------------------------------------------------
        /// <summary>
        /// 진단 줄 범위를 단일 줄 또는 줄 범위 문자열로 만든다.
        /// </summary>
        // ------------------------------------------------------------
        private static string FormatRange(SourceRange range)
        {
            return range.StartLine == range.EndLine
                ? $"L{range.StartLine}"
                : $"L{range.StartLine}-{range.EndLine}";
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 여러 줄 설명을 한 줄 출력용 텍스트로 바꾼다.
        /// </summary>
        // ------------------------------------------------------------
        private static string FlattenText(string value)
        {
            return value.Replace("\r\n", " / ", StringComparison.Ordinal).Replace("\n", " / ", StringComparison.Ordinal);
        }

    #endregion

    }

}
