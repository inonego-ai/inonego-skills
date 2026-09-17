/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : JSONReportWriter.cs
수정일 : 2026-09-17

# 설명
공통 분석 결과와 컨벤션 진단을 JSON 형식으로 직렬화한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;

using Microsoft;
using Microsoft.CodeAnalysis;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// text 출력과 같은 분석 사실을 JSON으로 직렬화한다.
    /// </summary>
    // ============================================================
    internal static class JSONReportWriter
    {

    #region 보고서 직렬화

        // ------------------------------------------------------------
        /// <summary>
        /// 분석 결과와 진단을 안정적인 JSON 구조로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        public static string Write(IReadOnlyList<FileReport> reports, string command)
        {
            // 단일·다중 입력이 같은 JSON 계약을 사용하도록 루트는 항상 파일 배열과 전체 합계를 가진다.
            Dictionary<string, object?> root = new(StringComparer.Ordinal)
            {
                ["command"]   = command,
                ["fileCount"] = reports.Count,
                ["files"]     = reports.Select(BuildFile).ToList(),
                ["summary"]   = BuildSummary(reports),
            };
            JsonSerializerOptions serializerOption = new()
            {
                WriteIndented = true,
            };
            return JsonSerializer.Serialize(root, serializerOption);
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 파일 하나의 분석 결과와 진단을 JSON용 사전으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Dictionary<string, object?> BuildFile(FileReport report)
        {
            CSharpFileAnalysis analysis = report.Analysis;
            return new(StringComparer.Ordinal)
            {
                ["file"]             = analysis.FileName,
                ["path"]             = analysis.FilePath,
                ["lineCount"]        = analysis.SourceText.Lines.Count,
                ["header"]           = new Dictionary<string, object?>
                {
                    ["exists"]       = analysis.BlockHeader.Exists,
                    ["malformed"]    = analysis.BlockHeader.Malformed,
                    ["fileName"]     = analysis.BlockHeader.FileName,
                    ["modifiedDate"] = analysis.BlockHeader.ModifiedDate,
                    ["description"]  = analysis.BlockHeader.Description,
                    ["constraints"]  = analysis.BlockHeader.Constraints,
                },
                ["declarations"]     = analysis.Declarations.Select(BuildDeclaration).ToList(),
                ["regions"]          = analysis.Regions.Select(BuildRegion).ToList(),
                ["diagnostics"]      = report.Diagnostics.Select(BuildDiagnostic).ToList(),
                ["parseDiagnostics"] = analysis.ParseDiagnostics.Select(BuildParseDiagnostic).ToList(),
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 모든 파일의 진단 수를 JSON 전체 합계로 집계한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Dictionary<string, object?> BuildSummary(IReadOnlyList<FileReport> reports)
        {
            return new(StringComparer.Ordinal)
            {
                ["errors"]      = reports.Sum(report => report.Diagnostics.Count(item => item.Severity == ConventionSeverity.Error)),
                ["warnings"]    = reports.Sum(report => report.Diagnostics.Count(item => item.Severity == ConventionSeverity.Warn)),
                ["parseErrors"] = reports.Sum(report => report.Analysis.ParseDiagnostics.Count),
            };
        }

    #endregion

    #region 항목 직렬화

        // ----------------------------------------------------------------------
        /// <summary>
        /// DeclarationDescriptor를 순환 참조 없는 JSON용 사전으로 변환한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static Dictionary<string, object?> BuildDeclaration(DeclarationDescriptor declaration)
        {
            Dictionary<string, object?> result = new(StringComparer.Ordinal)
            {
                ["kind"]          = declaration.Kind,
                ["name"]          = declaration.Name,
                ["declaration"]   = declaration.Header,
                ["accessibility"] = declaration.Accessibility,
                ["startLine"]     = declaration.Range.StartLine,
                ["endLine"]       = declaration.Range.EndLine,
                ["region"]        = declaration.Region?.Name,
                ["summary"]       = declaration.Summary?.DescriptionLines.ToList(),
                ["children"]      = declaration.Children.Select(BuildDeclaration).ToList(),
            };
            return result;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// RegionNode를 중첩 관계를 보존한 JSON용 사전으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Dictionary<string, object?> BuildRegion(RegionNode region)
        {
            return new(StringComparer.Ordinal)
            {
                ["name"]      = region.Name,
                ["startLine"] = region.StartLine,
                ["endLine"]   = region.EndLine,
                ["owner"]     = region.Owner?.Name,
                ["children"]  = region.Children.Select(BuildRegion).ToList(),
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 컨벤션 진단을 JSON용 사전으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Dictionary<string, object?> BuildDiagnostic(ConventionDiagnostic diagnostic)
        {
            return new(StringComparer.Ordinal)
            {
                ["id"]                 = diagnostic.ID,
                ["severity"]           = diagnostic.Severity.ToString().ToLowerInvariant(),
                ["startLine"]          = diagnostic.Range.StartLine,
                ["endLine"]            = diagnostic.Range.EndLine,
                ["message"]            = diagnostic.Message,
                ["relatedDeclaration"] = diagnostic.RelatedDeclarationName.Length == 0 ? null : diagnostic.RelatedDeclarationName,
                ["expected"]           = diagnostic.Expected.Length == 0 ? null : diagnostic.Expected,
            };
        }

        // ------------------------------------------------------------
        /// <summary>
        /// Roslyn 파싱 오류를 JSON용 사전으로 변환한다.
        /// </summary>
        // ------------------------------------------------------------
        private static Dictionary<string, object?> BuildParseDiagnostic(Diagnostic diagnostic)
        {
            FileLinePositionSpan span = diagnostic.Location.GetLineSpan();
            return new(StringComparer.Ordinal)
            {
                ["id"]      = diagnostic.Id,
                ["line"]    = span.StartLinePosition.Line + 1,
                ["column"]  = span.StartLinePosition.Character + 1,
                ["message"] = diagnostic.GetMessage(),
            };
        }

    #endregion

    }

}
