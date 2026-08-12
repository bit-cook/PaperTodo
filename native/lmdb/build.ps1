param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$ForceRebuild
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$buildDirectory = Join-Path $root "obj/win-x64-cloud"

$output = Join-Path $root "bin/win-x64/papertodo_lmdb.dll"
$staticOutput = Join-Path $root "bin/win-x64/papertodo_lmdb_static.lib"
if (-not $ForceRebuild -and
    (Test-Path -LiteralPath $output -PathType Leaf) -and
    (Test-Path -LiteralPath $staticOutput -PathType Leaf)) {
    Write-Output "Precompiled LMDB DLL and Native AOT static library found, skipping CMake build."
    exit 0
}

$precompiledBackups = @{}
if ($ForceRebuild) {
    foreach ($candidate in @($output, $staticOutput)) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            $backup = "$candidate.precompiled"
            Copy-Item -LiteralPath $candidate -Destination $backup -Force
            Remove-Item -LiteralPath $candidate -Force
            $precompiledBackups[$candidate] = $backup
        }
    }
}

try {
    if (-not (Get-Command cmake -ErrorAction SilentlyContinue)) {
        throw "CMake is required to build PaperTodo's native LMDB library."
    }

    # Use the newest Visual Studio generator installed on the runner. windows-latest currently
    # advances independently (for example VS 2026), so pinning a generator makes releases brittle.
    cmake -S $root -B $buildDirectory -A x64
    if ($LASTEXITCODE -ne 0) {
        throw "Configuring PaperTodo's native LMDB library failed."
    }

    cmake --build $buildDirectory --config $Configuration --target papertodo_lmdb papertodo_lmdb_static
    if ($LASTEXITCODE -ne 0) {
        throw "Building PaperTodo's native LMDB library failed."
    }

    if (-not (Test-Path -LiteralPath $output -PathType Leaf)) {
        throw "The native LMDB build completed without producing $output."
    }
    if (-not (Test-Path -LiteralPath $staticOutput -PathType Leaf)) {
        throw "The native LMDB build completed without producing $staticOutput."
    }
}
catch {
    foreach ($candidate in $precompiledBackups.Keys) {
        $backup = $precompiledBackups[$candidate]
        if (Test-Path -LiteralPath $backup -PathType Leaf) {
            Copy-Item -LiteralPath $backup -Destination $candidate -Force
        }
    }
    throw
}
finally {
    foreach ($backup in $precompiledBackups.Values) {
        if (Test-Path -LiteralPath $backup -PathType Leaf) {
            Remove-Item -LiteralPath $backup -Force
        }
    }
}
