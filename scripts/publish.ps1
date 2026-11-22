<#
.SYNOPSIS
  IIS / Plesk'e yüklenecek yayın paketini hazırlar.

.DESCRIPTION
  1) Çözümü Release modunda derler ve testleri (istenirse) çalıştırır.
  2) Web projesini framework-dependent olarak (varsayılan win-x64) publish\CihazServis klasörüne yayınlar.
  3) Veritabanı için idempotent migration betiğini publish\veritabani.sql olarak üretir
     (uygulama kullanıcısının tablo oluşturma yetkisi yoksa DBA bu betiği çalıştırır).
  4) Hepsini publish\CihazServis.zip olarak paketler.

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File scripts\publish.ps1
  powershell -ExecutionPolicy Bypass -File scripts\publish.ps1 -TestleriAtla
#>
param(
    [switch]$TestleriAtla,
    # Sunucunun işletim sistemi/işlemcisi; yalnız ona ait yerel kütüphaneler pakete girer.
    [string]$Platform = 'win-x64',
    # Çıktı klasörü (depo köküne göre).
    [string]$Cikti = 'publish'
)

$ErrorActionPreference = 'Stop'
$kok = Split-Path -Parent $PSScriptRoot
$cikti = Join-Path $kok $Cikti
$uygulama = Join-Path $cikti 'CihazServis'
$web = Join-Path $kok 'src\CihazServis.Web\CihazServis.Web.csproj'

function Invoke-Adim([string]$ad, [scriptblock]$is) {
    Write-Host "==> $ad" -ForegroundColor Cyan
    & $is
    if ($LASTEXITCODE -ne 0) { throw "$ad başarısız (çıkış kodu $LASTEXITCODE)." }
}

if (Test-Path $cikti) { Remove-Item $cikti -Recurse -Force }
New-Item -ItemType Directory -Path $cikti | Out-Null

Push-Location $kok
try {
    Invoke-Adim 'Araçlar geri yükleniyor' { dotnet tool restore }
    Invoke-Adim 'Derleme (Release)' { dotnet build -c Release }
    if (-not $TestleriAtla) {
        Invoke-Adim 'Testler' { dotnet test tests\CihazServis.Tests -c Release --no-build }
    }
    Invoke-Adim 'Yayınlama' { dotnet publish $web -c Release -r $Platform --self-contained false -o $uygulama }
    Invoke-Adim 'Migration betiği' {
        dotnet ef migrations script --idempotent --configuration Release --no-build `
            --project src\CihazServis.Infrastructure --startup-project src\CihazServis.Web `
            -o (Join-Path $cikti 'veritabani.sql')
    }
}
finally {
    Pop-Location
}

# Yerel geliştirme ayarı sunucuya gitmez.
Get-ChildItem $uygulama -Filter 'appsettings.Development.json' | Remove-Item -Force

$zip = Join-Path $cikti 'CihazServis.zip'
Compress-Archive -Path (Join-Path $uygulama '*'), (Join-Path $cikti 'veritabani.sql') -DestinationPath $zip
Write-Host "Hazır: $zip" -ForegroundColor Green
