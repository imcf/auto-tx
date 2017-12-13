[CmdletBinding()]
Param(
    [ValidateSet("Debug", "Release")][String] $Target = "Debug"
)


$ResourceDir = "..\AutoTx\Resources"
$TemplateDir = "$($ResourceDir)\Mail-Templates"
$BinariesDir = "..\AutoTx\bin\$($Target)"

try {
    $BuildDate = Get-Content "$($ResourceDir)\BuildDate.txt" -EA Stop
}
catch {
    Write-Host "Error reading build-date, stopping."
    Exit
}

$PkgDir = $BuildDate -replace ':','-' -replace ' ','_'
$PkgDir = "build_" + $PkgDir

Write-Host "Creating package [$($PkgDir)] using binaries from [$($BinariesDir)]"

if (Test-Path $PkgDir) {
    Write-Host "Removing existing package dir..."
    Remove-Item -Recurse -Force $PkgDir
}

$dir = New-Item -ItemType Container -Name $PkgDir
$dir = New-Item -ItemType Container -Path $PkgDir -Name "AutoTx"
$tgt = $dir.FullName

Copy-Item -Recurse "$TemplateDir" $tgt
Copy-Item -Recurse "$($BinariesDir)\*" $tgt
# provide an up-to-date version of the example config file:
Copy-Item "$($ResourceDir)\configuration-example.xml" $tgt

Copy-Item "$($ResourceDir)\configuration-example.xml" "$($PkgDir)\configuration.xml"
Copy-Item "$($ResourceDir)\status-example.xml" "$($PkgDir)\status.xml"
Copy-Item "$($ResourceDir)\BuildDate.txt" "$($PkgDir)\service.log"

Copy-Item "ScriptsConfig.ps1" $PkgDir
Copy-Item "Install-Service.ps1" $PkgDir