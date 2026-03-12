Push-Location $PSScriptRoot

try {
    # ensure any previous instances (app or mpv) are terminated
    & .\kill.ps1

    # run dotnet from parent without changing location stack
    dotnet run --project ".."
}
finally {
    # sempre volta para o diretório original
    Pop-Location
}