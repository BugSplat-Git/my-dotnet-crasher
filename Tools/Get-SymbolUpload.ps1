# Downloads symbol-upload-windows.exe from the BugSplat-Git/symbol-upload GitHub release into this
# folder. The exe is too large to check in, so builds fetch it here (see Directory.Build.targets).
# To upgrade, change $Version and $Sha256 together; the release lists the asset's digest
# (gh api repos/BugSplat-Git/symbol-upload/releases/tags/<version>).
$Version = "v11.0.0"
$Sha256 = "045160d29bc8ed75ab2a3f467bc2bed7c94113fcfc9e832086aeb54d0fb05fb3"

$ErrorActionPreference = "Stop"

$exePath = Join-Path $PSScriptRoot "symbol-upload-windows.exe"

# .NET's SHA256 rather than Get-FileHash, which fails to autoload when pwsh 7 launches this.
function Get-Sha256($path) {
    $sha = [Security.Cryptography.SHA256]::Create()
    $stream = [IO.File]::OpenRead($path)
    try { [BitConverter]::ToString($sha.ComputeHash($stream)).Replace("-", "") }
    finally { $stream.Dispose(); $sha.Dispose() }
}

# The exe receives the BugSplat client secret, so check the pinned digest on every build, not only
# after a download: a cached copy that was replaced or corrupted is downloaded again.
function Test-Current {
    (Test-Path $exePath) -and ((Get-Sha256 $exePath) -eq $Sha256)
}

# Parallel builds (/m) can run this from several projects at once, so only one process checks and
# downloads at a time
$mutex = New-Object System.Threading.Mutex($false, "Global\BugSplatSymbolUploadDownload")
try {
    try { [void]$mutex.WaitOne() }
    catch [System.Threading.AbandonedMutexException] { } # a build killed mid-download; we own it now
    if (Test-Current) { exit 0 }

    $url = "https://github.com/BugSplat-Git/symbol-upload/releases/download/$Version/symbol-upload-windows.exe"
    Write-Host "Downloading symbol-upload-windows.exe $Version from $url"

    # Windows PowerShell 5.1 defaults to TLS 1.0 and renders a progress bar that slows the download badly
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $ProgressPreference = "SilentlyContinue"

    $tempPath = "$exePath.download"
    Invoke-WebRequest -Uri $url -OutFile $tempPath -UseBasicParsing

    # The tag could be moved to a different asset, so only accept the exe this script pins
    $actual = Get-Sha256 $tempPath
    if ($actual -ne $Sha256) {
        Remove-Item -Force $tempPath
        throw "symbol-upload-windows.exe $Version has SHA-256 $actual, expected $Sha256"
    }
    Move-Item -Force $tempPath $exePath
}
finally {
    $mutex.ReleaseMutex()
    $mutex.Dispose()
}
