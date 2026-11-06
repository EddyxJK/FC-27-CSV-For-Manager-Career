param(
    [string]$NodePath = "",
    [string]$PythonPath = "",
    [string]$DependenciesDirectory = ""
)

$ErrorActionPreference = 'Stop'
$project = $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($NodePath)) {
    $nodeCommand = Get-Command node.exe -ErrorAction Stop
    $NodePath = $nodeCommand.Source
}
$nodeVersion = (& $NodePath --version).Trim()
if ($LASTEXITCODE -ne 0 -or [int]($nodeVersion -replace '^v([0-9]+).*$', '$1') -lt 24) {
    throw "Se requiere Node.js 24 o posterior. Versión encontrada: $nodeVersion"
}

if ([string]::IsNullOrWhiteSpace($PythonPath)) {
    $pythonLauncher = Get-Command py.exe -ErrorAction SilentlyContinue
    if ($pythonLauncher) { $PythonPath = $pythonLauncher.Source }
    else {
        $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
        if (-not $pythonCommand) { throw 'Para compilar desde el código fuente se necesita Python 3.' }
        $PythonPath = $pythonCommand.Source
    }
}
$pythonArgs = @()
if ([IO.Path]::GetFileName($PythonPath) -ieq 'py.exe') { $pythonArgs = @('-3') }
$pythonInfo = & $PythonPath @pythonArgs -c "import sys; print(sys.executable); print(sys.version_info.major * 100 + sys.version_info.minor); print(sys.base_prefix)"
if ($LASTEXITCODE -ne 0 -or $pythonInfo.Count -lt 3) { throw 'No pude ejecutar Python 3 para preparar el ejecutable.' }
$pythonExe = $pythonInfo[0].Trim()
$pythonTag = $pythonInfo[1].Trim()
$pythonRoot = $pythonInfo[2].Trim()

$buildRoot = Join-Path $project '.build'
$assets = Join-Path $buildRoot 'assets'
$parserDir = Join-Path $assets 'parser'
$runtimeDir = Join-Path $assets 'runtime'
$pythonRuntime = Join-Path $runtimeDir 'python'
$distDir = Join-Path $project 'dist'
$releaseDir = Join-Path $distDir 'FC27 Carrera CSV'
$releaseZip = Join-Path $distDir 'FC27-Carrera-CSV-portable-v1.0.0.zip'

if (Test-Path $buildRoot) { Remove-Item -LiteralPath $buildRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $parserDir, $runtimeDir, $pythonRuntime, $distDir | Out-Null
Copy-Item -Force (Join-Path $project 'src/parse-fc27-career.mjs') $parserDir
Copy-Item -Force (Join-Path $project 'src/build-career-names.mjs') $parserDir
Copy-Item -Force (Join-Path $project 'src/extract-fc27-databases.py') $parserDir
Copy-Item -Force $NodePath (Join-Path $runtimeDir 'node.exe')
Copy-Item -Force (Join-Path $project 'LICENSE') (Join-Path $parserDir 'PROJECT-LICENSE.txt')
Copy-Item -Force (Join-Path $project 'THIRD_PARTY_NOTICES.txt') $parserDir
Copy-Item -Force $pythonExe $pythonRuntime
Get-ChildItem -LiteralPath $pythonRoot -File -Filter '*.dll' | Copy-Item -Destination $pythonRuntime
Copy-Item -Recurse -Force (Join-Path $pythonRoot 'DLLs') $pythonRuntime
Get-ChildItem -LiteralPath (Join-Path $pythonRoot 'Lib') -Force |
    Where-Object { $_.Name -ne 'site-packages' } |
    Copy-Item -Destination (Join-Path $pythonRuntime 'Lib') -Recurse -Force
$pythonPth = "python$pythonTag._pth"
@('.', 'DLLs', 'Lib') | Set-Content -LiteralPath (Join-Path $pythonRuntime $pythonPth) -Encoding ascii
if (Test-Path (Join-Path $pythonRoot 'LICENSE.txt')) {
    Copy-Item -Force (Join-Path $pythonRoot 'LICENSE.txt') (Join-Path $pythonRuntime 'LICENSE.txt')
}
Copy-Item -Force (Join-Path $project 'package.json') (Join-Path $parserDir 'package.json')

if (-not [string]::IsNullOrWhiteSpace($DependenciesDirectory)) {
    if (-not (Test-Path (Join-Path $DependenciesDirectory 'eafc-db-kit/package.json') -PathType Leaf)) {
        throw "La carpeta indicada no contiene las dependencias instaladas de eafc-db-kit: $DependenciesDirectory"
    }
    Copy-Item -Recurse -Force $DependenciesDirectory (Join-Path $parserDir 'node_modules')
}
else {
    Push-Location $parserDir
    try {
        $npmCommand = Get-Command npm.cmd -ErrorAction Stop
        & $npmCommand.Source install --omit=dev --no-audit --no-fund
        if ($LASTEXITCODE -ne 0) { throw 'No se pudieron instalar las dependencias de npm.' }
    }
    finally { Pop-Location }
}

$pythonCheck = & (Join-Path $pythonRuntime 'python.exe') -c "import argparse, ctypes, os, struct, zlib; print('Python runtime OK')"
if ($LASTEXITCODE -ne 0) { throw "El Python portátil no pudo iniciar: $pythonCheck" }

$compilerCandidates = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
)
$compiler = $compilerCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $compiler) { throw 'No encontré el compilador C# incluido con .NET Framework para Windows.' }

$launcherExe = Join-Path $releaseDir 'FC27 Carrera CSV.exe'
if (Test-Path -LiteralPath $releaseDir) { Remove-Item -LiteralPath $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null
Copy-Item -Recurse -Force (Join-Path $assets 'parser') $releaseDir
Copy-Item -Recurse -Force (Join-Path $assets 'runtime') $releaseDir
@'
FC27 Carrera CSV

Extrae aquí el ZIP completo y abre "FC27 Carrera CSV.exe".
La primera vez necesita EA SPORTS FC 27 instalado para preparar localmente los nombres.
'@ | Set-Content -LiteralPath (Join-Path $releaseDir 'LEEME.txt') -Encoding utf8
& $compiler /nologo /target:winexe "/out:$launcherExe" `
    /reference:System.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.IO.Compression.dll /reference:System.IO.Compression.FileSystem.dll `
    (Join-Path $project 'src/Launcher.cs')
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $launcherExe)) { throw 'No se pudo compilar el lanzador de Windows.' }
if (Test-Path -LiteralPath $releaseZip) { Remove-Item -LiteralPath $releaseZip -Force }
Compress-Archive -Path $releaseDir -DestinationPath $releaseZip -CompressionLevel Optimal

Write-Host "Paquete portable listo para Releases: $releaseZip"
Write-Host "Lanzador: $launcherExe"
Write-Host 'No publiques solo el lanzador: el ZIP completo incluye las dependencias necesarias y no contiene datos de EA.'
