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

$programPath = $projectDir + "Program.cs"
$programSource = Get-Content -Raw $programPath

# The database, application and version passed to the BugSplat constructor in Program.cs.
# Only an uncommented line counts.
if ($programSource -notmatch '(?m)^[ \t]*var bugsplat = new BugSplatDotNet\.BugSplat\("([^"]*)", "([^"]*)", "([^"]*)"\)')
{
    Write-Host "Please set your database, application and version in 'new BugSplatDotNet.BugSplat(`"{database}`", `"{application}`", `"{version}`")' in ..\Program.cs"
    Exit 1
}
$database = $matches[1]
$appName = $matches[2]
$appVersion = $matches[3]

$symbolUploadPath = $projectDir + "..\..\Tools\symbol-upload-windows.exe"
# A trailing backslash would escape the closing quote around -d
$outDir = $outDir.TrimEnd('\')
Write-Host "Running symbol-upload-windows.exe -b $database -a `"$appName`" -v `"$appVersion`" -i $BUGSPLAT_CLIENT_ID -s ****** -d `"$outDir`" -f `"*.{pdb,exe,dll}`""
# Run it directly (not Start-Process) so a failed upload fails the build
& $symbolUploadPath -b $database -a $appName -v $appVersion -i $BUGSPLAT_CLIENT_ID -s $BUGSPLAT_CLIENT_SECRET -d $outDir -f "*.{pdb,exe,dll}"
Exit $LASTEXITCODE
