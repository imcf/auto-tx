try {
    $BuildDate = Get-Content "BuildDate.txt" -EA Stop
}
catch {
    Write-Host "Error reading build-date, stopping."
    Exit
}

$BaseDir = $BuildDate -replace ':','-' -replace ' ','_'
$BaseDir = "build_" + $BaseDir

Write-Host "Creating package: [$($BaseDir)]"

if (Test-Path $BaseDir) {
    Remove-Item -Recurse -Force $BaseDir
}

$dir = New-Item -ItemType Container -Name $BaseDir
$dir = New-Item -ItemType Container -Path $BaseDir -Name "AutoTx"

Copy-Item -Recurse -Force -Path "Mail-Templates" -Destination $dir.FullName
Copy-Item -Recurse -Force -Path "..\bin\Debug\*" -Destination $dir.FullName
# provide an up-to-date version of the example config file:
Copy-Item -Force -Path "configuration-example.xml" -Destination $dir.FullName

Copy-Item -Force -Path "configuration-example.xml" -Destination "$($BaseDir)\configuration.xml"
Copy-Item -Force -Path "status-example.xml" -Destination "$($BaseDir)\status.xml"
Copy-Item -Force -Path "BuildDate.txt" -Destination "$($BaseDir)\service.log"

Copy-Item -Force -Path "ScriptsConfig.ps1" -Destination $BaseDir
Copy-Item -Force -Path "Install-Service.ps1" -Destination $BaseDir