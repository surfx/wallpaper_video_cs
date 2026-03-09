Push-Location $PSScriptRoot

# ensure any previous instances (app or mpv) are terminated
& .\kill.ps1

Push-Location ..
dotnet run
Pop-Location
Pop-Location