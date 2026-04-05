#!/bin/bash
dotnet test Launchbox.Tests/Launchbox.Tests.csproj --filter "FullyQualifiedName~WinUILauncher" /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura /p:EnableWindowsTargeting=true /p:GenerateAppxPackageOnBuild=false /p:AppxPackage=false /p:AppxGeneratePriEnabled=false
