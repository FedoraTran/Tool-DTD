<#
.SYNOPSIS
  Build plugin + trainer. Kết quả: DTDTrainer.exe ở ngay thư mục này (plugin được nhúng sẵn trong exe).
  Mọi thứ của mod nằm trong Tool-DTD\runtime; build KHÔNG ghi gì vào thư mục game.

.PARAMETER Install
  Chép luôn plugin vừa build vào runtime\BepInEx\plugins\DTDMod.
  Không bắt buộc: khi mở, trainer tự cập nhật plugin trong runtime từ bản nhúng trong exe.
#>
param(
    [switch]$Install
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$staging = Join-Path $root "src\DTDTrainer\obj\publish"

if (Get-Process DTDTrainer -ErrorAction SilentlyContinue) { throw "Hay dong DTDTrainer.exe truoc khi build" }

Write-Host "== Build plugin" -ForegroundColor Cyan
dotnet build "$root\src\DTDMod.Plugin\DTDMod.Plugin.csproj" -c Release -nologo -v q
if ($LASTEXITCODE -ne 0) { throw "Build plugin that bai" }

if (Get-Command gcc -ErrorAction SilentlyContinue) {
    Write-Host "== Build DTDLoader" -ForegroundColor Cyan
    $runtimeDir = Join-Path $root "runtime"
    if (-not (Test-Path $runtimeDir)) { New-Item -ItemType Directory -Force $runtimeDir | Out-Null }
    gcc -O2 -shared -s -o (Join-Path $runtimeDir "DTDLoader.dll") (Join-Path $root "src\DTDLoader\dtdloader.c") -lkernel32
    if ($LASTEXITCODE -ne 0) { throw "Build DTDLoader that bai" }
}

Write-Host "== Publish trainer" -ForegroundColor Cyan
dotnet publish "$root\src\DTDTrainer\DTDTrainer.csproj" -c Release -nologo -v q -o $staging
if ($LASTEXITCODE -ne 0) { throw "Publish trainer that bai" }
Copy-Item "$staging\DTDTrainer.exe" "$root\DTDTrainer.exe" -Force

if ($Install) {
    $target = Join-Path $root "runtime\BepInEx\plugins\DTDMod"
    New-Item -ItemType Directory -Force $target | Out-Null
    Copy-Item "$root\src\DTDMod.Plugin\bin\Release\net6.0\DTDMod.dll" $target -Force
    Write-Host "Da chep plugin vao $target" -ForegroundColor Green
}

Write-Host "== Xong: $root\DTDTrainer.exe" -ForegroundColor Green
