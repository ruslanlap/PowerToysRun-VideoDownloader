<#PSScriptInfo
.VERSION 0.87.0
.GUID 58d7b8e8-fa18-485d-baaf-4c413181280b
.AUTHOR Henrik Lau Eriksson
.COMPANYNAME
.COPYRIGHT
.TAGS PowerToys Run Plugins Pack
.LICENSEURI
.PROJECTURI https://github.com/hlaueriksson/Community.PowerToys.Run.Plugin.Templates
.ICONURI
.EXTERNALMODULEDEPENDENCIES
.REQUIREDSCRIPTS
.EXTERNALSCRIPTDEPENDENCIES
.RELEASENOTES
#>

<#
    .Synopsis
    Packs the plugins into release archives.

    .Description
    Builds the solution in Release configuration,
    copies the output files into plugin folders,
    packs the plugin folders into release archives.

    .Example
    .\pack.ps1

    .Link
    https://github.com/hlaueriksson/Community.PowerToys.Run.Plugin.Templates
#>

# Clean
Get-ChildItem -Path "." -Directory -Include "bin", "obj" -Recurse | Remove-Item -Recurse -Force

$dependencies = @("PowerToys.Common.UI.*", "PowerToys.ManagedCommon.*", "PowerToys.Settings.UI.Lib.*", "Wox.Infrastructure.*", "Wox.Plugin.*")

# Plugins
$folders = Get-ChildItem -Recurse -Filter "plugin.json" | Where-Object { $_.FullName -notlike "*\bin\*" } | ForEach-Object { $_.Directory } | Sort-Object -Unique

Write-Output "Pack:"
foreach ($folder in $folders) {
    Write-Output "- $($folder.Name)"

    $name = $($folder.Name.Split(".")[-1])

    # Version
    $json = Get-Content -Path (Join-Path $folder.FullName "plugin.json") -Raw | ConvertFrom-Json
    $version = $json.Version
    Write-Output "Version: $version"

    # Platforms
    [xml]$csproj = Get-Content -Path (Join-Path $folder.FullName "*.csproj")
    $targetFramework = $csproj.Project.PropertyGroup.TargetFramework
    $platforms = "$($csproj.Project.PropertyGroup.Platforms)".Trim() -split ";"

    foreach ($platform in $platforms)
    {
        Write-Output "Platform: $platform"

        # Build
        dotnet build $folder -c Release /p:TF_BUILD=true /p:Platform=$platform

        if (!$?) {
            # Build FAILED.
            Exit $LastExitCode
        }

        $output = "$folder\bin\$platform\Release\$targetFramework\"
        $destination = "$folder\bin\$platform\$name"
        $zip = "$folder\bin\$platform\$name-$version-$($platform.ToLower()).zip"

        Copy-Item -Path $output -Destination $destination -Recurse -Exclude $dependencies
        Compress-Archive -Path $destination -DestinationPath $zip
    }
}
