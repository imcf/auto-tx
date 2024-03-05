<#
.DESCRIPTION
This script is intended to be executed as a "post-build" command in Visual Studio.
It reverts changes made to specific files during the build process, ensuring they
remain consistent with their pre-build state.

Currently, it targets the "BuildDetails.cs" file within the "ATxCommon"
directory of the specified solution.
#>

[CmdletBinding(DefaultParameterSetName="build")]
Param(
    [Parameter(Mandatory=$True, ParameterSetName="build")]
    [Parameter(Mandatory=$True, ParameterSetName="gentemplate")]
    [string] $SolutionDir
)

try {
    git checkout --  "$($SolutionDir)\ATxCommon\BuildDetails.cs"
} catch {
    Write-Error "Git checkout failed. Is Git installed? File was not reset: $($SolutionDir)\ATxCommon\BuildDetails.cs"
}
