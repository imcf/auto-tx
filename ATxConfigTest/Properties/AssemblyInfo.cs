﻿using System.Reflection;
using System.Runtime.InteropServices;
using ATxCommon;

// General Information about an assembly is controlled through the following 
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("AutoTx ConfigTest")]
[assembly: AssemblyDescription("AutoTx Configuration Validator")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("IMCF, Biozentrum, University of Basel")]
[assembly: AssemblyProduct("AutoTx")]
[assembly: AssemblyCopyright("© University of Basel 2018")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible 
// to COM components.  If you need to access a type in this assembly from 
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("1efe21ab-b31e-43ef-95c3-72b9addfabb4")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version 
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Build and Revision Numbers 
// by using the '*' as shown below:
// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion(BuildDetails.GitMajor + "." +
                           BuildDetails.GitMinor + "." +
                           BuildDetails.GitPatch + ".0")]
[assembly: AssemblyFileVersion(BuildDetails.GitMajor + "." +
                               BuildDetails.GitMinor + "." +
                               BuildDetails.GitPatch + ".0")]

[assembly: AssemblyInformationalVersion(BuildDetails.BuildDate +
                                        " " + BuildDetails.GitCommit +
                                        " (" + BuildDetails.GitBranch + ")")]

