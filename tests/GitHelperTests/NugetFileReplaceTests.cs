using GitHelper.Build;
using LagoVista.Core.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GitHelperTests
{
    [TestClass]
    public class NugetFileReplaceTests
    {
        const string FILECONTENTS = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <VersionPrefix>0.8.0</VersionPrefix>
    <TargetFramework>netstandard1.6</TargetFramework>
    <AssemblyName>LagoVista.CloudStorage</AssemblyName>
    <PackageId>LagoVista.CloudStorage</PackageId>
    <NetStandardImplicitPackageVersion>2.0.1</NetStandardImplicitPackageVersion>
    <PackageTargetFallback Condition = "" '$(TargetFramework)' == 'netstandard1.6' "" >$(PackageTargetFallback);dnxcore50;portable-net45+win8</PackageTargetFallback>
    <PackageTargetFallback Condition = "" '$(TargetFramework)' == 'netcoreapp1.0' "" >$(PackageTargetFallback);dnxcore50;portable-net45+win8</PackageTargetFallback>
    <GenerateAssemblyConfigurationAttribute>false</GenerateAssemblyConfigurationAttribute>
    <GenerateAssemblyCompanyAttribute>false</GenerateAssemblyCompanyAttribute>
    <GenerateAssemblyProductAttribute>false</GenerateAssemblyProductAttribute>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include = ""LagoVista.Core"" Version=""1.2.343-alpha1450"" />
    <PackageReference Include = ""LagoVista.IoT.Logging"" Version=""1.2.343-alpha1450"" />
    <PackageReference Include = ""Microsoft.Azure.DocumentDB.Core"" Version=""1.9.1"" />
    <PackageReference Include = ""Newtonsoft.Json"" Version=""11.0.1"" />
    <PackageReference Include = ""WindowsAzure.Storage"" Version=""9.1.0"" />
  </ItemGroup>
  <ItemGroup>
    <Folder Include = ""Properties\"" />
  </ ItemGroup >
</ Project >";

        const string NUSPEC_FILE_CONTENTS = @"<?xml version=""1.0""?>
<package>
  <metadata>
    <id>LagoVista.CloudStorage</id>
    <version>1.2.343-alpha1450</version>
    <authors>Software Logistics, LLC</authors>
    <owners>Software Logistics, LLC</owners>
    <requireLicenseAcceptance>false</requireLicenseAcceptance>
    <projectUrl>http://www.lagovista-iot.com/opensource</projectUrl>
    <iconUrl>http://bytemaster.blob.core.windows.net/icons/LagoVistaIcon.png</iconUrl>
    <description>Provides an abstact mechanism to store object graphs to cloud storage.</description>
    <releaseNotes>Early Unstable Release</releaseNotes>
    <copyright>Copyright 2017</copyright>
    <tags>HomeAutomation IoT LagoVista</tags>
    <dependencies>
    </dependencies>
  </metadata>
  <files>
    <file src = ""readme.txt"" target="""" />
    <file src = ""bin\release\netstandard1.6\*.dll"" target=""lib\netstandard1.6"" />
  </files>
</package>";

        [TestMethod]
        public void ReplaceInStandardLibrary()
        {
            var fileHelper = new Moq.Mock<IFileHelper>();
            fileHelper.Setup(fh => fh.OpenFile(It.IsAny<string>())).Returns(InvokeResult<string>.Create(FILECONTENTS));

            var nugetHelper = new NugetHelpers(new ConsoleWriter(), fileHelper.Object, new SolutionHelper(fileHelper.Object, new ConsoleWriter()));
            nugetHelper.ApplyToCSProject("DONECARE", "1.1.1-alpha32");
        }

        [TestMethod]
        public void ReplaceInNUSPEC()
        {
            var fileHelper = new Moq.Mock<IFileHelper>();
            fileHelper.Setup(fh => fh.OpenFile(It.IsAny<string>())).Returns(InvokeResult<string>.Create(NUSPEC_FILE_CONTENTS));

            var nugetHelper = new NugetHelpers(new ConsoleWriter(), fileHelper.Object, new SolutionHelper(fileHelper.Object, new ConsoleWriter()));
            nugetHelper.ApplyToNuspecFile("DONECARE", "1.1.1-alpha32");
        }

    }
}
