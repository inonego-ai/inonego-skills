/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : CSharpFileAnalysis.cs
수정일 : 2026-09-17

# 설명
한 C# 파일을 한 번 파싱해 얻은 공통 분석 결과를 묶는다.
========================================================================= BLOCK_HEADER_END */

using System;
using System.Collections;
using System.Collections.Generic;

using Microsoft;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// Inspector와 Checker가 공유하는 파일 단위 분석 결과다.
    /// </summary>
    // ============================================================
    internal sealed class CSharpFileAnalysis
    {
        public string FilePath                          = string.Empty;
        public string FileName                          = string.Empty;
        public SourceText SourceText                    = null!;
        public SyntaxTree SyntaxTree                    = null!;
        public CompilationUnitSyntax Root               = null!;
        public List<Diagnostic> ParseDiagnostics        = new();
        public BlockHeader BlockHeader                  = new();
        public List<DeclarationDescriptor> Declarations = new();
        public List<RegionNode> Regions                 = new();
    }

}
