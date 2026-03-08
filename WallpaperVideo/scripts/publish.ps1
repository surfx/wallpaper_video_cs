Push-Location $PSScriptRoot
Push-Location ..
dotnet publish -c Release
Pop-Location
Pop-Location