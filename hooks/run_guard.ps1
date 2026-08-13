$utf8 = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = $utf8
[Console]::OutputEncoding = $utf8
$OutputEncoding = $utf8

$payloadText = [Console]::In.ReadToEnd()
$guardOutput = $payloadText | python -X utf8 (Join-Path $PSScriptRoot "inonego_guard.py")
$guardExitCode = $LASTEXITCODE

if ($null -ne $guardOutput) {
    [Console]::Out.Write(($guardOutput -join [Environment]::NewLine))
}
exit $guardExitCode
