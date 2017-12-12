$PackageDir = Get-ChildItem -Directory -Name |
    Where-Object {$_ -match 'build_[0-9]{4}-[0-9]{2}-[0-9]{2}_'} |
    Sort-Object |
    Select-Object -Last 1

$CurDir = Get-Location

Write-Host -NoNewLine "Installing package ["
Write-Host -NoNewLine $PackageDir -Fore Green
Write-Host  "] ..."
Write-Host  ""

cd $PackageDir
./Install-Service.ps1

cd $CurDir