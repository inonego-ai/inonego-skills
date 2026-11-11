param()

$ErrorActionPreference = 'Stop'
$tool = Join-Path $PSScriptRoot '..\..\csharp-tool.ps1'
$directory = Join-Path ([IO.Path]::GetTempPath()) ('inonego-csharp-summary-regression-' + $PID)
$separator = '// ' + ('-' * 60)
$typeSeparator = '// ' + ('=' * 60)
$scenarios = @(
    @{ Name = 'ValidSummaryOnly'; Boundary = $false; Missing = $false; Declaration = @"
        $separator
        /// <summary>
        /// 값을 반환한다.
        /// </summary>
        $separator
"@ },
    @{ Name = 'ValidAllDocumentationInside'; Boundary = $false; Missing = $false; Declaration = @"
        $separator
        /// <summary>
        /// 값을 반환한다.
        /// </summary>
        /// <param name="value">값.</param>
        /// <returns>값.</returns>
        /// <remarks>
        /// 이 메서드는 값을 그대로 전달한다.
        /// </remarks>
        $separator
"@ },
    @{ Name = 'InvalidParamAfterBottom'; Boundary = $true; Missing = $false; Declaration = @"
        $separator
        /// <summary>
        /// 값을 반환한다.
        /// </summary>
        $separator
        /// <param name="value">값.</param>
"@ },
    @{ Name = 'InvalidReturnsAfterBottom'; Boundary = $true; Missing = $false; Declaration = @"
        $separator
        /// <summary>
        /// 값을 반환한다.
        /// </summary>
        $separator
        /// <returns>값.</returns>
"@ },
    @{ Name = 'InvalidTypeparamAfterBottom'; Boundary = $true; Missing = $false; Declaration = @"
        $separator
        /// <summary>
        /// 값을 반환한다.
        /// </summary>
        $separator
        /// <typeparam name="T">형식.</typeparam>
"@ },
    @{ Name = 'InvalidDocsAfterComment'; Boundary = $true; Missing = $false; Declaration = @"
        $separator
        /// <summary>
        /// 값을 반환한다.
        /// </summary>
        $separator
        // 구분선 이후 일반 주석.
        /// <param name="value">값.</param>
"@ },
    @{ Name = 'InvalidRemarksBeforeTop'; Boundary = $true; Missing = $false; Declaration = @"
        /// <remarks>구분선 밖의 설명.</remarks>
        $separator
        /// <summary>
        /// 값을 반환한다.
        /// </summary>
        $separator
"@ },
    @{ Name = 'InvalidDuplicateBottom'; Boundary = $true; Missing = $false; Declaration = @"
        $separator
        /// <summary>
        /// 값을 반환한다.
        /// </summary>
        $separator
        $separator
"@ },
    @{ Name = 'InvalidBrokenSummary'; Boundary = $true; Missing = $false; Declaration = @"
        $separator
        /// <summary>
        /// 값을 반환한다.
        // 일반 주석이 XML 문서를 나눈다.
        /// </summary>
        $separator
"@ },
    @{ Name = 'InvalidMissingPublicSummary'; Boundary = $false; Missing = $true; Declaration = @"
        $separator
        /// <param name="value">값.</param>
        $separator
"@ },
    @{ Name = 'ValidConstructorDocumentation'; Boundary = $false; Missing = $false; Member = @'
        public Fixture(int value)
        {
            _ = value;
        }
'@; Declaration = @"
        $separator
        /// <summary>
        /// 입력값을 받아 객체를 초기화한다.
        /// </summary>
        /// <param name="value">초기 입력값.</param>
        $separator
"@ },
    @{ Name = 'InvalidConstructorParamAfterBottom'; Boundary = $true; Missing = $false; Member = @'
        public Fixture(int value)
        {
            _ = value;
        }
'@; Declaration = @"
        $separator
        /// <summary>
        /// 입력값을 받아 객체를 초기화한다.
        /// </summary>
        $separator
        /// <param name="value">초기 입력값.</param>
"@ },
    @{ Name = 'ValidPropertySummary'; Boundary = $false; Missing = $false; Member = '        public int Value => 42;'; Declaration = @"
        $separator
        /// <summary>
        /// 예시 값을 제공한다.
        /// </summary>
        $separator
"@ },
    @{ Name = 'InvalidPropertyRemarksAfterBottom'; Boundary = $true; Missing = $false; Member = '        public int Value => 42;'; Declaration = @"
        $separator
        /// <summary>
        /// 예시 값을 제공한다.
        /// </summary>
        $separator
        /// <remarks>예시 프로퍼티의 부가 설명.</remarks>
"@ }
)

New-Item -ItemType Directory -Path $directory -Force | Out-Null
try
{
    $paths = @()
    foreach ($scenario in $scenarios)
    {
        $fileName = $scenario.Name + '.cs'
        $member = if ($scenario.ContainsKey('Member')) { $scenario.Member } else { '        public int Method(int value) => value;' }
        $source = @"
/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : $fileName
수정일 : 2026-11-11

# 설명
XML 문서 주석 경계 검증용 입력.
========================================================================= BLOCK_HEADER_END */

namespace Sample
{
    $typeSeparator
    /// <summary>
    /// XML 문서 주석 검사 입력.
    /// </summary>
    $typeSeparator
    public sealed class Fixture
    {
$($scenario.Declaration)
$member
    }
}
"@
        $path = Join-Path $directory $fileName
        [IO.File]::WriteAllText($path, $source, [Text.UTF8Encoding]::new($false))
        $paths += $path
    }

    $output = & $tool review @paths --format json
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 1)
    {
        throw "Expected convention errors across negative fixtures; received exit code $exitCode."
    }

    $report = ($output | Out-String) | ConvertFrom-Json
    foreach ($scenario in $scenarios)
    {
        $name = $scenario.Name
        $file = @($report.files | Where-Object { [IO.Path]::GetFileName($_.path) -eq ($name + '.cs') })
        if ($file.Count -ne 1)
        {
            throw "Result missing: $name"
        }

        $diagnostics = @($file[0].diagnostics)
        $hasBoundary = @($diagnostics | Where-Object { $_.id -eq 'CSHARP-SUMMARY-010' }).Count -gt 0
        $hasMissing = @($diagnostics | Where-Object { $_.id -eq 'CSHARP-SUMMARY-001' }).Count -gt 0
        if ($hasBoundary -ne $scenario.Boundary -or $hasMissing -ne $scenario.Missing)
        {
            throw "Unexpected diagnostics: $name boundary=$hasBoundary missing=$hasMissing ids=$($diagnostics.id -join ',')"
        }

        if (-not $scenario.Boundary -and -not $scenario.Missing -and $diagnostics.Count -ne 0)
        {
            throw "False positive: $name ids=$($diagnostics.id -join ',')"
        }

        Write-Output "PASS $name (boundary=$hasBoundary; missing=$hasMissing)"
    }

    Write-Output "SUMMARY $($scenarios.Count)/$($scenarios.Count) regression cases passed"
}
finally
{
    if (Test-Path -LiteralPath $directory)
    {
        Remove-Item -LiteralPath $directory -Recurse -Force
    }
}

exit 0
