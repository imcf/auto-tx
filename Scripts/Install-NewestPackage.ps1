# Helper script to locate the latest AutoTx installation package (expected to be
# in a subdirectory of this script with a name like `build_2019-04-23_12-34-56`)
# and call the installer script from *within* that package.

# Make sure to run from the directory containing the script itself:
$BaseDir = $(Split-Path $MyInvocation.MyCommand.Path)
Push-Location $BaseDir


$PackageDir = Get-ChildItem -Directory -Name |
    Where-Object {$_ -match 'build_[0-9]{4}-[0-9]{2}-[0-9]{2}_'} |
    Sort-Object |
    Select-Object -Last 1


Write-Host -NoNewLine "Installing package ["
Write-Host -NoNewLine $PackageDir -Fore Green
Write-Host  "] ..."
Write-Host  ""

cd $PackageDir
./Install-Service.ps1

# Return to the original location before the script was called:
Pop-Location