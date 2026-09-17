/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : CSharpFileAnalyzer.cs
수정일 : 2026-09-17

# 설명
단일 C# 파일을 한 번 파싱하고 공통 구조 분석 결과를 생성한다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 파일 읽기부터 Roslyn 구조 수집까지 한 번의 분석으로 수행한다.
    /// </summary>
    // ============================================================
    internal static class CSharpFileAnalyzer
    {
        // ------------------------------------------------------------
        /// <summary>
        /// 지정한 C# 파일의 공통 분석 결과를 생성한다.
        /// </summary>
        // ------------------------------------------------------------
        public static CSharpFileAnalysis Analyze(string filePath)
        {
            // 원본 텍스트와 SyntaxTree를 한 번만 만들고 모든 후속 분석이 같은 구문 정보를 공유한다.
            string text = File.ReadAllText(filePath);
            SourceText sourceText = SourceText.From(text, Encoding.UTF8);
            CSharpParseOptions parseOption = new(LanguageVersion.Latest, DocumentationMode.Parse, SourceCodeKind.Regular);
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceText, parseOption, filePath);
            CompilationUnitSyntax root = syntaxTree.GetCompilationUnitRoot();

            // 파싱 진단을 구조 builder보다 먼저 확정해 손상된 복구 AST가 도구 실패로 승격되지 않게 한다.
            CSharpFileAnalysis analysis = new();
            analysis.FilePath    = filePath;
            analysis.FileName    = Path.GetFileName(filePath);
            analysis.SourceText  = sourceText;
            analysis.SyntaxTree  = syntaxTree;
            analysis.Root        = root;
            analysis.BlockHeader = BlockHeaderParser.Parse(sourceText);

            IEnumerable<Diagnostic> parseErrors = syntaxTree
                .GetDiagnostics()
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            analysis.ParseDiagnostics.AddRange(parseErrors);

            // 정상 구문은 구조 수집 실패를 도구 결함으로 드러내고, 파싱 오류가 있으면 복구 구조만 best-effort로 수집한다.
            if (analysis.ParseDiagnostics.Count == 0)
            {
                PopulateStructure(analysis);
            }
            else
            {
                TryPopulateRecoveredStructure(analysis);
            }

            return analysis;
        }

        // ------------------------------------------------------------
        /// <summary>
        /// 정상 구문 트리의 선언·summary·region 구조를 완전하게 수집한다.
        /// </summary>
        // ------------------------------------------------------------
        private static void PopulateStructure(CSharpFileAnalysis analysis)
        {
            List<DeclarationDescriptor> declarations = DeclarationTreeBuilder.Build(analysis.Root, analysis.SourceText);
            SummaryParser.Attach(declarations, analysis.SourceText);
            List<RegionNode> regions = RegionTreeBuilder.Build(analysis.Root, analysis.SourceText, declarations);
            analysis.Declarations.AddRange(declarations);
            analysis.Regions.AddRange(regions);
        }

        // ----------------------------------------------------------------------
        /// <summary>
        /// 파싱 오류가 있는 복구 구문 트리에서 얻을 수 있는 구조만 best-effort로 수집한다.
        /// </summary>
        // ----------------------------------------------------------------------
        private static void TryPopulateRecoveredStructure(CSharpFileAnalysis analysis)
        {
            List<DeclarationDescriptor> declarations = new();
            try
            {
                declarations = DeclarationTreeBuilder.Build(analysis.Root, analysis.SourceText);
                analysis.Declarations.AddRange(declarations);
            }
            catch
            {
                // NONE
            }

            try
            {
                SummaryParser.Attach(declarations, analysis.SourceText);
            }
            catch
            {
                // NONE
            }

            try
            {
                List<RegionNode> regions = RegionTreeBuilder.Build(analysis.Root, analysis.SourceText, declarations);
                analysis.Regions.AddRange(regions);
            }
            catch
            {
                // NONE
            }
        }
    }

}
