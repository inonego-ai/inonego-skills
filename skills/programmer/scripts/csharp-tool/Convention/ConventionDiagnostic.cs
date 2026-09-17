/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : ConventionDiagnostic.cs
수정일 : 2026-09-17

# 설명
컨벤션 검사에서 생성하는 진단 데이터와 심각도를 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 컨벤션 진단의 심각도 수준이다.
    /// </summary>
    // ============================================================
    internal enum ConventionSeverity
    {
        Error,
        Warn,
    }

    // ============================================================
    /// <summary>
    /// 한 컨벤션 규칙 위반의 위치와 설명을 보관한다.
    /// </summary>
    // ============================================================
    internal sealed class ConventionDiagnostic
    {
        public string ID                     = string.Empty;
        public ConventionSeverity Severity   = ConventionSeverity.Error;
        public SourceRange Range             = SourceRange.FromLine(1);
        public string Message                = string.Empty;
        public string RelatedDeclarationName = string.Empty;
        public string Expected               = string.Empty;

        // ----------------------------------------------------------------------
        /// <summary>
        /// 공통 진단 필드를 한 곳에서 초기화해 Rule별 생성 보일러플레이트를 줄인다.
        /// </summary>
        // ----------------------------------------------------------------------
        public static ConventionDiagnostic Create(string id, SourceRange range, string message, string relatedDeclarationName = "", string expected = "")
        {
            return new()
            {
                ID                     = id,
                Range                  = range,
                Message                = message,
                RelatedDeclarationName = relatedDeclarationName,
                Expected               = expected,
            };
        }
    }

}
