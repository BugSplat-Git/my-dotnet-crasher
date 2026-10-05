param(
    [string] $projectDir,
    [string] $outDir
)

if (!$projectDir)
{
    Write-Host "Please provide a value for command line argument -projectDir"
    Exit 1
}

if (!$outDir)
{
    Write-Host "Please provide a value for command line argument -outDir"
    Exit 1
}

try
{
    . $projectDir\Scripts\env.ps1
}
catch
{
    $BUGSPLAT_CLIENT_ID = $Env:BUGSPLAT_CLIENT_ID
    $BUGSPLAT_CLIENT_SECRET = $Env:BUGSPLAT_CLIENT_SECRET
}

if (!$BUGSPLAT_CLIENT_ID)
{
    Write-Host 'Please add "$BUGSPLAT_CLIENT_ID={id}" to .\Scripts\env.ps1 or add BUGSPLAT_CLIENT_ID as an env variable'

    Exit 1
}

if (!$BUGSPLAT_CLIENT_SECRET)
{
    Write-Host 'Please add "$BUGSPLAT_CLIENT_SECRET={secret}" to .\Scripts\env.ps1  or add BUGSPLAT_CLIENT_SECRET as an env variable'

    Exit 1
}

$appPath = $projectDir + "App.xaml.cs"
$appSource = Get-Content -Raw $appPath

# Only an uncommented line counts
if ($appSource -notmatch '(?m)^[ \t]*public const string Database = "(.*)";')
{
    Write-Host "Please set 'public const string Database = `"{your BugSplat database}`";' in ..\App.xaml.cs"
    Exit 1
}
$database = $matches[1]

if ($appSource -notmatch 'const string Application = "(.*)";')
{
    Write-Host "Please set 'public const string Application = `"{your application name}`";' in ..\App.xaml.cs"
    Exit 1
}
$appName = $matches[1]

if ($appSource -notmatch 'const string Version = "(.*)";')
{
    Write-Host "Please set 'public const string Version = `"{your application version}`";' in ..\App.xaml.cs"
    Exit 1
}
$appVersion = $matches[1]

$symbolUploadPath = $projectDir + "..\..\Tools\symbol-upload-windows.exe"
# A trailing backslash would escape the closing quote around -d
$outDir = $outDir.TrimEnd('\')
# Every module the app ships, so any frame in a crash can be symbolicated
$files = "*.{pdb,exe,dll}"
Write-Host "Running symbol-upload-windows.exe -b $database -a `"$appName`" -v `"$appVersion`" -i $BUGSPLAT_CLIENT_ID -s ****** -d `"$outDir`" -f `"$files`""
# Run it directly (not Start-Process) so a failed upload fails the build
& $symbolUploadPath -b $database -a $appName -v $appVersion -i $BUGSPLAT_CLIENT_ID -s $BUGSPLAT_CLIENT_SECRET -d $outDir -f $files
Exit $LASTEXITCODE
