# kill.ps1 - terminate the WallpaperVideo app and any mpv instances

# kill the main application if it's running
Get-Process -Name WallpaperVideo -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

# kill any mpv processes launched by the app
Get-Process -Name mpv -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue