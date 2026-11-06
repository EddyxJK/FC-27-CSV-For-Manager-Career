param(
    [string]$NodePath = "",
    [string]$NamesJson = ""
)

$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($NodePath)) {
    $nodeCommand = Get-Command node -ErrorAction Stop
    $NodePath = $nodeCommand.Source
}
$nodeVersion = (& $NodePath --version).Trim()
if ($LASTEXITCODE -ne 0 -or [int]($nodeVersion -replace '^v([0-9]+).*$', '$1') -lt 24) {
    throw "Se requiere Node.js 24 o posterior. Versión encontrada: $nodeVersion"
}

$buildRoot = Join-Path $project '.build'
$assets = Join-Path $buildRoot 'assets'
$parserDir = Join-Path $assets 'parser'
$runtimeDir = Join-Path $assets 'runtime'
$distDir = Join-Path $project 'dist'
$payload = Join-Path $buildRoot 'payload.zip'

if (Test-Path $buildRoot) { Remove-Item -LiteralPath $buildRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $parserDir, $runtimeDir, $distDir | Out-Null
Copy-Item -Force (Join-Path $project 'src/parse-fc27-career.mjs') $parserDir
Copy-Item -Force $NodePath (Join-Path $runtimeDir 'node.exe')
Copy-Item -Force (Join-Path $project 'package.json') (Join-Path $parserDir 'package.json')

Push-Location $parserDir
try {
    $npmCommand = Get-Command npm.cmd -ErrorAction Stop
    & $npmCommand.Source install --omit=dev --no-audit --no-fund
    if ($LASTEXITCODE -ne 0) { throw 'No se pudieron instalar las dependencias de npm.' }
}
finally { Pop-Location }

if (-not [string]::IsNullOrWhiteSpace($NamesJson)) {
    if (-not (Test-Path $NamesJson -PathType Leaf)) { throw "No encontré el diccionario indicado: $NamesJson" }
    Copy-Item -Force $NamesJson (Join-Path $parserDir 'playernames-fc27.json')
    Write-Warning 'El diccionario quedará incorporado al ejecutable. No publiques ese ejecutable ni el diccionario si contienen datos que no quieras compartir.'
}

Compress-Archive -Path (Join-Path $assets 'parser'), (Join-Path $assets 'runtime') -DestinationPath $payload -CompressionLevel Optimal

$compilerCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $compiler) { throw 'No encontré el compilador C# incluido con .NET Framework para Windows.' }

$outputExe = Join-Path $distDir 'FC27 Carrera CSV.exe'
$resourceArg = "$payload,FC27CarreraCsv.Payload.zip"
& $compiler /nologo /target:winexe "/out:$outputExe" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    "/resource:$resourceArg" (Join-Path $project 'src/Launcher.cs')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $outputExe)) { throw 'No se pudo compilar el ejecutable.' }

Write-Host "Aplicación creada: $outputExe"
