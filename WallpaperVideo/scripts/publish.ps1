Push-Location $PSScriptRoot

try {
    & .\kill.ps1
    dotnet publish "..\WallpaperVideo.csproj" -c Release
}
finally {
    Pop-Location
}