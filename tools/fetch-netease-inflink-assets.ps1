# 下载 InfLink-rs 与 BetterNCM 安装包到 Assets（供发布内置）

$ErrorActionPreference = "Stop"

$dest = Join-Path $PSScriptRoot "..\src\PixelBar.App\Assets\NetEaseInfLink"

New-Item -ItemType Directory -Force -Path $dest | Out-Null



$betterNcmUrl = "https://github.com/std-microblock/BetterNCM-Installer/releases/download/1.2.0/betterncm_installer.exe"

$betterNcmPath = Join-Path $dest "betterncm_installer.exe"

Write-Host "Downloading BetterNCM installer..."

Invoke-WebRequest -Uri $betterNcmUrl -OutFile $betterNcmPath -UseBasicParsing



$infLinkUrl = "https://github.com/apoint123/inflink-rs/releases/latest/download/InfLink-rs.plugin"

$infLinkPath = Join-Path $dest "InfLink-rs.plugin"

Write-Host "Downloading InfLink-rs.plugin..."

Invoke-WebRequest -Uri $infLinkUrl -OutFile $infLinkPath -UseBasicParsing -MaximumRedirection 10



Write-Host "Done. Files written to $dest"


