[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)][string] $ProjectDir
)

function Write-BuildDetails {
    Param (
        [Parameter(Mandatory=$True)]
        [String]$Target,

        [Parameter(Mandatory=$True)]
        [String]$CommitName,

        [Parameter(Mandatory=$True)]
        [String]$Branch,

        [Parameter(Mandatory=$True)]
        [String]$Date
    )

    Write-Output "Generating [$($Target)]..."
    Write-Output $("
    public static class BuildDetails
    {
        public const string GitCommitName = `"$($CommitName)`";
        public const string GitBranch = `"$($Branch)`";
        public const string BuildDate = `"$($Date)`";
    }") > $Target
}

$ErrorActionPreference = "Stop"

$oldpwd = pwd
cd $ProjectDir -ErrorAction Stop

try {
    $CommitName = & git describe --tags
    if (-Not $?) { throw }
    $GitStatus = & git status --porcelain
    if (-Not $?) { throw }
    $GitBranch = & git symbolic-ref --short HEAD
    if (-Not $?) { throw }

    if ($GitStatus.Length -gt 0) {
        Write-Output "NOTE: repository has uncommitted changes!"
        $CommitName = "$($CommitName)-unclean"
    }
}
catch {
    $CommitName = "commit unknown"
    $GitStatus = "status unknown"
    $GitBranch = "branch unknown"
    Write-Output "$(">"*8) Running git failed, using default values! $("<"*8)"
}


$Date = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'


$BCommit = "$($ProjectDir)\Resources\BuildCommit.txt"
$BuildDate = "$($ProjectDir)\Resources\BuildDate.txt"
$BuildDetailsCS = "$($ProjectDir)\..\Resources\BuildDetails.cs"

Write-Output $CommitName > $BCommit
Write-Output $Date > $BuildDate

Write-Output $Date
Write-Output $CommitName
Write-Output $GitBranch

Write-BuildDetails $BuildDetailsCS $CommitName $GitBranch $Date 

cd $oldpwd