param
(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]] $Arguments
)

$ErrorActionPreference = 'Stop'
$projectDirectory = Join-Path $PSScriptRoot 'csharp-tool'
$projectPath = Join-Path $projectDirectory 'CSharpTool.csproj'
$noBuild = $false
$toolArguments = New-Object System.Collections.Generic.List[string]
foreach ($argument in $Arguments)
{
    if ($argument -eq '--no-build')
    {
        $noBuild = $true
        continue
    }

    $toolArguments.Add($argument)
}

$sourceFiles = Get-ChildItem -Path $projectDirectory -Filter '*.cs' -File -Recurse |
    Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' } |
    Sort-Object FullName |
    ForEach-Object { $_.FullName }
$files = @($projectPath) + @($sourceFiles)
$stream = New-Object System.IO.MemoryStream
try
{
    foreach ($file in $files)
    {
        $relative = $file.Substring($projectDirectory.Length).Replace('\', '/').ToLowerInvariant()
        $nameBytes = [System.Text.Encoding]::UTF8.GetBytes($relative + "`n")
        $stream.Write($nameBytes, 0, $nameBytes.Length)
        $contentBytes = [System.IO.File]::ReadAllBytes($file)
        $stream.Write($contentBytes, 0, $contentBytes.Length)
    }

    $stream.Position = 0
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try
    {
        $hashBytes = $sha.ComputeHash($stream)
    }
    finally
    {
        $sha.Dispose()
    }
}
finally
{
    $stream.Dispose()
}

$sourceHash = -join ($hashBytes | ForEach-Object { $_.ToString('x2') })
$localAppData = [Environment]::GetFolderPath('LocalApplicationData')
$cacheBase = Join-Path $localAppData 'inonego-skills\tools\csharp-tool'
$cacheDirectory = Join-Path $cacheBase $sourceHash
$dllPath = Join-Path $cacheDirectory 'CSharpTool.dll'
$requiredCacheFiles = @(
    'CSharpTool.dll',
    'CSharpTool.deps.json',
    'CSharpTool.runtimeconfig.json',
    'Microsoft.CodeAnalysis.dll',
    'Microsoft.CodeAnalysis.CSharp.dll'
)

function Test-CacheComplete
{
    param([string] $Directory)

    foreach ($name in $requiredCacheFiles)
    {
        $path = Join-Path $Directory $name
        if (-not (Test-Path -LiteralPath $path -PathType Leaf))
        {
            return $false
        }

        if ((Get-Item -LiteralPath $path).Length -le 0)
        {
            return $false
        }
    }

    return $true
}

if (-not (Test-CacheComplete $cacheDirectory))
{
    if ($noBuild)
    {
        [Console]::Error.WriteLine("C# tool cache is missing or incomplete for source hash $sourceHash.")
        exit 4
    }

    New-Item -ItemType Directory -Path $cacheBase -Force | Out-Null
    $temporaryDirectory = Join-Path $cacheBase ($sourceHash + '.tmp-' + $PID)
    if (Test-Path -LiteralPath $temporaryDirectory)
    {
        Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
    }

    try
    {
        $publishOutput = & dotnet publish $projectPath -c Release -o $temporaryDirectory --nologo 2>&1
        $publishExitCode = $LASTEXITCODE
        if ($publishExitCode -ne 0 -or -not (Test-CacheComplete $temporaryDirectory))
        {
            foreach ($line in $publishOutput)
            {
                [Console]::Error.WriteLine([string] $line)
            }

            if ($publishExitCode -eq 0)
            {
                [Console]::Error.WriteLine('C# tool publish output is incomplete.')
            }

            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
            exit 4
        }

        if (Test-CacheComplete $cacheDirectory)
        {
            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force
        }
        else
        {
            if (Test-Path -LiteralPath $cacheDirectory)
            {
                Remove-Item -LiteralPath $cacheDirectory -Recurse -Force
            }

            try
            {
                Move-Item -LiteralPath $temporaryDirectory -Destination $cacheDirectory
            }
            catch
            {
                if (Test-CacheComplete $cacheDirectory)
                {
                    Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
                }
                else
                {
                    throw
                }
            }
        }
    }
    catch
    {
        if (Test-Path -LiteralPath $temporaryDirectory)
        {
            Remove-Item -LiteralPath $temporaryDirectory -Recurse -Force -ErrorAction SilentlyContinue
        }

        [Console]::Error.WriteLine($_.Exception.ToString())
        exit 4
    }
}

try
{
    & dotnet $dllPath @toolArguments
    $toolExitCode = $LASTEXITCODE
}
catch
{
    [Console]::Error.WriteLine($_.Exception.ToString())
    exit 4
}

if ($null -eq $toolExitCode)
{
    [Console]::Error.WriteLine('C# tool process did not return an exit code.')
    exit 4
}

if ($toolExitCode -lt 0 -or $toolExitCode -gt 4)
{
    Remove-Item -LiteralPath $cacheDirectory -Recurse -Force -ErrorAction SilentlyContinue
    [Console]::Error.WriteLine("C# tool process returned unexpected exit code $toolExitCode; the cache was discarded.")
    exit 4
}

exit $toolExitCode