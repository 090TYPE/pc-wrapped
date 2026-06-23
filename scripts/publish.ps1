#requires -version 5
$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet publish src/PcWrapped -c Release -r win-x64 --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
    $exe = Join-Path $root "dist\PcWrapped.exe"
    if (-not (Test-Path $exe)) { throw "PcWrapped.exe not found in dist" }
    $mb = [math]::Round((Get-Item $exe).Length / 1MB, 1)
    Write-Output "Published: $exe ($mb MB)"
}
finally { Pop-Location }
