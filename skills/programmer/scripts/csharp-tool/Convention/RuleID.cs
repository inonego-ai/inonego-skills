/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : RuleID.cs
수정일 : 2026-09-17

# 설명
외부 출력에 안정적으로 노출할 C# 컨벤션 규칙 ID를 정의한다.
========================================================================= BLOCK_HEADER_END */

namespace Inonego.Skills.CSharpTool
{

    // ============================================================
    /// <summary>
    /// 컨벤션 검사기가 사용하는 안정적인 규칙 식별자 모음이다.
    /// </summary>
    // ============================================================
    internal static class RuleID
    {
        internal const string HeaderMissing              = "CSHARP-HEADER-001";
        internal const string HeaderFileName             = "CSHARP-HEADER-002";
        internal const string HeaderMalformed            = "CSHARP-HEADER-003";
        internal const string HeaderRequiredField        = "CSHARP-HEADER-004";
        internal const string HeaderDateFormat           = "CSHARP-HEADER-005";
        internal const string HeaderPosition             = "CSHARP-HEADER-006";
        internal const string UsingGroupOrder            = "CSHARP-USING-001";
        internal const string UsingParentMissing         = "CSHARP-USING-002";
        internal const string UsingGroupSpacing          = "CSHARP-USING-003";
        internal const string UsingParentOrder           = "CSHARP-USING-004";
        internal const string FormatAllman               = "CSHARP-FORMAT-001";
        internal const string FormatIndent               = "CSHARP-FORMAT-002";
        internal const string FormatConstraintIndent     = "CSHARP-FORMAT-003";
        internal const string FormatEmptyScopeNone       = "CSHARP-FORMAT-004";
        internal const string FormatEmptyScopeCompact    = "CSHARP-FORMAT-005";
        internal const string FormatBlockSpacing         = "CSHARP-FORMAT-006";
        internal const string SummarySeparatorType       = "CSHARP-SUMMARY-001";
        internal const string SummarySeparatorLength     = "CSHARP-SUMMARY-002";
        internal const string SummarySeparatorMismatch   = "CSHARP-SUMMARY-003";
        internal const string SummarySingleBreak         = "CSHARP-SUMMARY-004";
        internal const string SummaryMultiBreak          = "CSHARP-SUMMARY-005";
        internal const string SummaryWidth               = "CSHARP-SUMMARY-006";
        internal const string SummaryEmpty               = "CSHARP-SUMMARY-007";
        internal const string SummarySeparatorSpacing    = "CSHARP-SUMMARY-008";
        internal const string SummaryBlockLayout         = "CSHARP-SUMMARY-009";
        internal const string RegionTypeSpacing          = "CSHARP-REGION-001";
        internal const string RegionAfterStartSpacing    = "CSHARP-REGION-002";
        internal const string RegionBeforeEndSpacing     = "CSHARP-REGION-003";
        internal const string RegionAfterEndSpacing      = "CSHARP-REGION-004";
        internal const string RegionBeforeTypeEndSpacing = "CSHARP-REGION-005";
        internal const string RegionIndent               = "CSHARP-REGION-006";
        internal const string RegionEmpty                = "CSHARP-REGION-007";
        internal const string RegionEmptyName            = "CSHARP-REGION-008";
        internal const string MemberPairAdjacency        = "CSHARP-MEMBER-001";
        internal const string MemberBackingBelow         = "CSHARP-MEMBER-002";
        internal const string NamePascal                 = "CSHARP-NAME-001";
        internal const string NameInterface              = "CSHARP-NAME-002";
        internal const string NameCamel                  = "CSHARP-NAME-003";
        internal const string NamePublicField            = "CSHARP-NAME-004";
        internal const string NameFlexibleField          = "CSHARP-NAME-005";
        internal const string PropertyLogicBlock         = "CSHARP-PROPERTY-001";
        internal const string MethodLogicBlock           = "CSHARP-METHOD-001";
        internal const string ControlBraces              = "CSHARP-CONTROL-001";
        internal const string CompactParenPlacement      = "CSHARP-COMPACT-001";
    }

}
