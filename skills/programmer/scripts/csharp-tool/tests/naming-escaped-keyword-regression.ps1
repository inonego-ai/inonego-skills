param()

$ErrorActionPreference = 'Stop'
$tool = Join-Path $PSScriptRoot '..\..\csharp-tool.ps1'
$directory = Join-Path ([IO.Path]::GetTempPath()) ('inonego-csharp-naming-regression-' + $PID)
$separator = '// ' + ('-' * 60)
$typeSeparator = '// ' + ('=' * 60)
$scenarios = @(
    @{ Name = 'ValidEscapedKeywordMethod'; Pascal = $false; Member = '        public Fixture @new() => new Fixture();' },
    @{ Name = 'ValidEscapedKeywordProperty'; Pascal = $false; Member = '        public int @default => 0;' },
    @{ Name = 'InvalidLowercaseMethod'; Pascal = $true; Member = '        public int value() => 0;' },
    @{ Name = 'InvalidEscapedNonKeywordMethod'; Pascal = $true; Member = '        public int @value() => 0;' }
)

New-Item -ItemType Directory -Path $directory -Force | Out-Null
try
{
    $paths = @()
    foreach ($scenario in $scenarios)
    {
        $fileName = $scenario.Name + '.cs'
        $source = @"
/* BLOCK_HEADER_BEGIN =======================================================================
파일명 : $fileName
수정일 : 2026-11-19

# 설명
이스케이프한 키워드 이름 검증용 입력.
========================================================================= BLOCK_HEADER_END */

namespace Sample
{
    $typeSeparator
    /// <summary>
    /// 이름 규칙 검사 입력.
    /// </summary>
    $typeSeparator
    public sealed class Fixture
    {
        $separator
        /// <summary>
        /// 검사 대상 멤버.
        /// </summary>
        $separator
$($scenario.Member)
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
        $hasPascal = @($diagnostics | Where-Object { $_.id -eq 'CSHARP-NAME-001' }).Count -gt 0
        if ($hasPascal -ne $scenario.Pascal)
        {
            throw "Unexpected diagnostics: $name pascal=$hasPascal ids=$($diagnostics.id -join ',')"
        }

        if (-not $scenario.Pascal -and $diagnostics.Count -ne 0)
        {
            throw "False positive: $name ids=$($diagnostics.id -join ',')"
        }

        Write-Output "PASS $name (pascal=$hasPascal)"
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
