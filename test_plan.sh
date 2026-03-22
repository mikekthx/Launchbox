dotnet build -p:EnableWindowsTargeting=true -p:GenerateAppxPackageOnBuild=false -p:AppxPackage=false -p:AppxGeneratePriEnabled=false
dotnet test --no-build --filter "FullyQualifiedName~Launchbox.Tests.PathSecurityTests"
