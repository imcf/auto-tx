$PackageDir = Get-ChildItem -Directory -Name |
    Where-Object {$_ -match 'build_*'} |
    Select-Object -Last 1

$CurDir = Get-Location

Write-Host -NoNewLine "Installing package ["
Write-Host -NoNewLine $PackageDir -Fore Green
Write-Host  "] ..."
Write-Host  ""

cd $PackageDir
./Install-Service.ps1

cd $CurDir