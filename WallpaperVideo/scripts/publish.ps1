Push-Location $PSScriptRoot

# stop any running processes before publishing
& .\kill.ps1

Push-Location ..
dotnet publish -c Release
Pop-Location
Pop-Location