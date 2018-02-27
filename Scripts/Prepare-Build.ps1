[CmdletBinding()]
Param(
    [Parameter(Mandatory=$True)][string] $SolutionDir,
    [Parameter(Mandatory=$True)][string] $ConfigurationName
)

$CsTemplate = @"
public static class BuildDetails
{{
    public const string GitCommitName = "{0}";
    public const string GitBranch = "{1}";
    public const string GitMajor = "{2}";
    public const string GitMinor = "{3}";
    public const string GitPatch = "{4}";
    public const string BuildDate = "{5}";
    public const string GitCommit = "{6}";
}}
"@

function Write-BuildDetails {
    Param (
        [Parameter(Mandatory=$True)]
        [String]$Target,

        [Parameter(Mandatory=$True)]
        [Array]$Desc,

        [Parameter(Mandatory=$True)]
        [String]$Branch,

        [Parameter(Mandatory=$True)]
        [String]$Date
    )

    $CommitName = "$($Desc[0]).$($Desc[1])-$($Desc[2])-$($Desc[3])"
    $Commit = $Desc[3].Substring(1)
    Write-Output "Generating [$($Target)]..."
    Write-Output " > $($CommitName)"
    $Code = $CsTemplate -f `
        $CommitName, `
        $Branch, `
        $Desc[0], `
        $Desc[1], `
        $Desc[2], `
        $Date, `
        $Commit
    Write-Verbose $Code
    Out-File -FilePath $Target -Encoding ASCII -InputObject $Code
}

function Parse-GitDescribe([string]$CommitName) {
    Write-Verbose "Parsing 'git describe' result [$($CommitName)]..."
    try {
        $Items = $CommitName.Split('-').Split('.')
        if ($Items.Length -ne 4) { throw }
    }
    catch {
        throw "Can't parse commit name [$($CommitName)]!"
    }
    Write-Verbose "Just some $($Items[2]) commits after the last tag."
    Return $Items
}


$ErrorActionPreference = "Stop"

$oldpwd = pwd
cd $SolutionDir -ErrorAction Stop

try {
    $CommitName = & git describe --tags --long --match "[0-9].[0-9]"
    if (-Not $?) { throw }
    $GitStatus = & git status --porcelain
    if (-Not $?) { throw }
    $GitBranch = & git symbolic-ref --short HEAD
    if (-Not $?) { throw }

    $DescItems = Parse-GitDescribe $CommitName

    if ($GitStatus.Length -gt 0) {
        $StatusWarning = "  <--  WARNING, repository has uncommitted changes!"
        $CommitName += "-unclean"
    }
}
catch {
    $CommitName = "commit unknown"
    $GitBranch = "branch unknown"
    Write-Output "$(">"*8) Running git failed, using default values! $("<"*8)"
}


$Date = Get-Date -Format 'yyyy-MM-dd HH:mm:ss'


$BCommit = "$($SolutionDir)\Resources\BuildCommit.txt"
$BuildDate = "$($SolutionDir)\Resources\BuildDate.txt"
$BuildConfig = "$($SolutionDir)\Resources\BuildConfiguration.txt"
$BuildDetailsCS = "$($SolutionDir)\Resources\BuildDetails.cs"


$Date | Out-File $BuildDate
$CommitName | Out-File $BCommit
$ConfigurationName | Out-File $BuildConfig

Write-Output $(
    "build-config: [$($ConfigurationName)]"
    "build-date:   [$($Date)]"
    "git-branch:   [$($GitBranch)]"
    "git-describe: [$($CommitName)]$($StatusWarning)"
)

Write-BuildDetails $BuildDetailsCS $DescItems $GitBranch $Date 

cd $oldpwd