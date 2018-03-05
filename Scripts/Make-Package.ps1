# helper script to create a package from an AutoTx build that can be used from
# the included "Install-Service.ps1" script as well as with the AutoTx-Updater

# set our requirements:
#Requires -version 5.1

$ResourceDir = "..\Resources"


function Highlight([string]$Message, [string]$Color = "Cyan", $Indent = $False) {
    if ($Indent) {
        Write-Host -NoNewline "    "
    }
    Write-Host -NoNewline "["
    Write-Host -NoNewline -F $Color $Message
    Write-Host -NoNewline "]"
    if ($Indent) {
        Write-Host
    }
}

function RelToAbs([string]$RelPath) {
    Join-Path -Resolve $(Get-Location) $RelPath
}


$ErrorActionPreference = "Stop"


try {
    $BuildDate = Get-Content "$($ResourceDir)\BuildDate.txt" -EA Stop
}
catch {
    Write-Host "Error reading build-date, stopping."
    Exit
}
try {
    $BuildConfiguration = Get-Content "$($ResourceDir)\BuildConfiguration.txt" -EA Stop
}
catch {
    Write-Host "Error reading build configuration, stopping."
    Exit
}


$PkgDir = $BuildDate -replace ':','-' -replace ' ','_'
$PkgDir = "build_" + $PkgDir
$BinariesDirService = RelToAbs "..\ATxService\bin\$($BuildConfiguration)"
$BinariesDirTrayApp = RelToAbs "..\ATxTray\bin\$($BuildConfiguration)"
$BinariesDirCfgTest = RelToAbs "..\ATxConfigTest\bin\$($BuildConfiguration)"

Write-Host -NoNewline "Creating package "
Highlight $PkgDir "Red"
Write-Host " using binaries from:"
Highlight $BinariesDirService "Green" $True
Highlight $BinariesDirTrayApp "Green" $True
Highlight $BinariesDirCfgTest "Green" $True
Write-Host

if (Test-Path $PkgDir) {
    Write-Host "Removing existing package dir [$($PkgDir)]...`n"
    Remove-Item -Recurse -Force $PkgDir
}


$dir = New-Item -ItemType Container -Force -Path "$($PkgDir)\AutoTx"
$tgt = $dir.FullName
New-Item -ItemType Container -Force -Path "$($PkgDir)\AutoTx\conf" | Out-Null

Copy-Item -Exclude *.pdb -Recurse "$($BinariesDirService)\*" $tgt
Copy-Item -Exclude *.pdb -Recurse "$($BinariesDirTrayApp)\*" $tgt -EA Ignore
Copy-Item -Exclude *.pdb -Recurse "$($BinariesDirCfgTest)\*" $tgt -EA Ignore
# provide an up-to-date version of the example config file:
$example = New-Item -ItemType Container -Path $PkgDir -Name "conf-example"
Copy-Item "$($ResourceDir)\conf\config.common.xml" $example
Copy-Item "$($ResourceDir)\conf\host-specific.template.xml" $example

Copy-Item "$($ResourceDir)\BuildDate.txt" "$($PkgDir)\AutoTx.log"
Copy-Item "$($ResourceDir)\BuildConfiguration.txt" $($PkgDir)
try {
    $CommitRefFile = "$($ResourceDir)\BuildCommit.txt"
    Copy-Item $CommitRefFile $($PkgDir)
    $BuildCommit = Get-Content $CommitRefFile
}
catch {
    Write-Host "Error getting commit reference from git!"
    $BuildCommit = "<UNKNOWN>"
}


Copy-Item "ScriptsConfig.ps1" $PkgDir
Copy-Item "Install-Service.ps1" $PkgDir

Write-Host -NoNewline "Done creating package "
Highlight $PkgDir
Write-Host
Highlight "configuration: $($BuildConfiguration)" -Indent $True
Highlight "commit: $($BuildCommit)" -Indent $True
Write-Host

Write-Host -NoNewline "Location: "
Highlight "$(RelToAbs $PkgDir)"
Write-Host