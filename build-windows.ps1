param(
    [string]$NodePath = "",
    [string]$NamesJson = "",
    [string]$CareerNamesJson = "",
    [string]$DependenciesDirectory = "",
    [string]$GameDirectory = "",
    [string]$PythonPath = "",
    [switch]$SkipGameData
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
Copy-Item -Force (Join-Path $project 'src/build-career-names.mjs') $parserDir
Copy-Item -Force $NodePath (Join-Path $runtimeDir 'node.exe')
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

if (-not $SkipGameData -and ([string]::IsNullOrWhiteSpace($NamesJson) -or [string]::IsNullOrWhiteSpace($CareerNamesJson))) {
    $pythonArgs = @()
    if ([string]::IsNullOrWhiteSpace($PythonPath)) {
        $pythonLauncher = Get-Command py.exe -ErrorAction SilentlyContinue
        if ($pythonLauncher) {
            $PythonPath = $pythonLauncher.Source
            $pythonArgs = @('-3')
        }
        else {
            $pythonCommand = Get-Command python.exe -ErrorAction SilentlyContinue
            if (-not $pythonCommand) { throw 'Para generar los nombres automáticamente instala Python 3, o compila con -SkipGameData para omitirlos.' }
            $PythonPath = $pythonCommand.Source
        }
    }
    $pythonVersion = (& $PythonPath @pythonArgs --version 2>&1 | Out-String).Trim()
    if ($LASTEXITCODE -ne 0 -or $pythonVersion -notmatch '^Python 3\.') {
        throw 'No pude ejecutar Python 3. Instálalo y vuelve a compilar, o usa -SkipGameData para omitir la extracción de nombres.'
    }

    $gameDbDir = Join-Path $buildRoot 'game-db'
    New-Item -ItemType Directory -Force -Path $gameDbDir | Out-Null
    $extractor = Join-Path $project 'src/extract-fc27-databases.py'
    $careerNamesLocal = Join-Path $buildRoot 'career-names-fc27.json'
    $playerNamesLocal = Join-Path $buildRoot 'playernames-fc27.json'
    $foundCareerNames = $false
    if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
        $GameDirectory = $env:FC27_GAME_DIR
        if ([string]::IsNullOrWhiteSpace($GameDirectory) -or -not (Test-Path -LiteralPath $GameDirectory -PathType Container)) {
            $gameCandidates = @('C:\Program Files (x86)\Steam\steamapps\common\EA SPORTS FC 27',
                'C:\Program Files\EA Games\EA SPORTS FC 27')
            foreach ($drive in 'D','E','F','G','H','I','J') {
                $gameCandidates += "${drive}:\SteamLibrary\steamapps\common\EA SPORTS FC 27"
                $gameCandidates += "${drive}:\Program Files (x86)\Steam\steamapps\common\EA SPORTS FC 27"
            }
            $GameDirectory = $gameCandidates | Where-Object { Test-Path -LiteralPath $_ -PathType Container } | Select-Object -First 1
        }
        if ([string]::IsNullOrWhiteSpace($GameDirectory)) {
            throw 'No encontré la instalación de FC27. Instálalo o compila indicando -GameDirectory "C:\ruta\EA SPORTS FC 27".'
        }
    }
    $casOrder = @('cas_08.cas') + (1..10 | ForEach-Object { "cas_{0:D2}.cas" -f $_ } | Where-Object { $_ -ne 'cas_08.cas' })

    foreach ($casName in $casOrder) {
        $extractArgs = @($pythonArgs) + @($extractor, '--cas', $casName, '--out', $gameDbDir)
        if (-not [string]::IsNullOrWhiteSpace($GameDirectory)) { $extractArgs += @('--game-dir', $GameDirectory) }
        & $PythonPath @extractArgs
        if ($LASTEXITCODE -ne 0) { continue }

        foreach ($gameDb in (Get-ChildItem -LiteralPath $gameDbDir -Filter '*.fifa_ng_db' -File)) {
            $helperArgs = @((Join-Path $parserDir 'build-career-names.mjs'), '--game-db', $gameDb.FullName,
                '--out', $careerNamesLocal, '--names-out', $playerNamesLocal)
            & $NodePath @helperArgs 2>$null
            if ($LASTEXITCODE -eq 0) {
                $foundCareerNames = $true
                break
            }
        }
        if ($foundCareerNames) { break }
    }

    if (-not $foundCareerNames) {
        throw 'No pude encontrar las listas de nombres de Carrera en la instalación de FC27. Indica -GameDirectory o entrega -NamesJson y -CareerNamesJson; -SkipGameData compila sin diccionarios.'
    }
    if ([string]::IsNullOrWhiteSpace($NamesJson)) { $NamesJson = $playerNamesLocal }
    if ([string]::IsNullOrWhiteSpace($CareerNamesJson)) { $CareerNamesJson = $careerNamesLocal }
}

if (-not [string]::IsNullOrWhiteSpace($NamesJson)) {
    if (-not (Test-Path $NamesJson -PathType Leaf)) { throw "No encontré el diccionario indicado: $NamesJson" }
    Copy-Item -Force $NamesJson (Join-Path $parserDir 'playernames-fc27.json')
    Write-Warning 'El diccionario quedará incorporado al ejecutable. No publiques ese ejecutable ni el diccionario si contienen datos que no quieras compartir.'
}

if (-not [string]::IsNullOrWhiteSpace($CareerNamesJson)) {
    if (-not (Test-Path $CareerNamesJson -PathType Leaf)) { throw "No encontré el diccionario indicado: $CareerNamesJson" }
    Copy-Item -Force $CareerNamesJson (Join-Path $parserDir 'career-names-fc27.json')
    Write-Warning 'El diccionario de nombres de carrera quedará incorporado al ejecutable. No publiques el ejecutable ni este archivo de datos del juego en GitHub.'
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
