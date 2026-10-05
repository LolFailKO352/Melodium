# build-installer.ps1
# Skript pro automatické sestavení samostatného .msi instalátoru pro Melodium
param(
    [string]$Configuration = "Release",
    [string]$Architecture = "x64",
    [string]$Version = ""
)

$ErrorActionPreference = "Stop"

# Vždy nastavit pracovní adresář na složku projektu se skriptem
$ProjectDir = $PSScriptRoot
if (-not $ProjectDir) {
    $ProjectDir = (Get-Location).Path
}
Set-Location $ProjectDir

# Pokud verze nebyla zadána, získáme ji z Melodium.csproj
if ([string]::IsNullOrWhiteSpace($Version)) {
    try {
        [xml]$proj = Get-Content "$ProjectDir\Melodium.csproj"
        $verNode = $proj.Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
        if ($verNode) {
            $parts = $verNode.Trim().Split('.')
            while ($parts.Length -lt 4) { $parts += "0" }
            $Version = ($parts[0..3] -join '.')
        } else {
            $Version = "1.6.0.0"
        }
    } catch {
        $Version = "1.6.0.0"
    }
}

Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "  Melodium MSI Installer Builder (WiX 4) " -ForegroundColor Cyan
Write-Host "  Verze balíčku: $Version" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan

# 1. Kontrola / instalace WiX nástroje
if (-not (Get-Command wix -ErrorAction SilentlyContinue)) {
    Write-Host "[1/5] Instaluji WiX Toolset jako globální .NET tool..." -ForegroundColor Yellow
    dotnet tool install --global wix --version 4.0.6
} else {
    Write-Host "[1/5] WiX Toolset je připraven." -ForegroundColor Green
}

# Ujistit se, že máme UI a Util extensions
wix extension add -g WixToolset.UI.wixext/4.0.6 2>$null
wix extension add -g WixToolset.Util.wixext/4.0.6 2>$null

# 2. Ukončit případně běžící instanci
Write-Host "[2/5] Ukončuji běžící instance Melodium..." -ForegroundColor Yellow
$running = Get-Process -Name "Melodium" -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 800
}

# 3. Publikovat self-contained aplikaci
Write-Host "[3/5] Publikuji aplikaci (dotnet publish self-contained)..." -ForegroundColor Yellow
dotnet publish "$ProjectDir\Melodium.csproj" -c $Configuration -r win-$Architecture --self-contained -o "$ProjectDir\publish"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Chyba při dotnet publish!"
}

# Zajistit existenci resources.pri pro WinUI 3 XAML loader
$priCandidates = @(
    "$ProjectDir\bin\$Architecture\$Configuration\net10.0-windows10.0.19041.0\win-$Architecture\Melodium.pri",
    "$ProjectDir\bin\$Configuration\net10.0-windows10.0.19041.0\win-$Architecture\Melodium.pri"
)
foreach ($cand in $priCandidates) {
    if (Test-Path $cand) {
        if (-not (Test-Path "$ProjectDir\publish\resources.pri")) {
            Copy-Item $cand -Destination "$ProjectDir\publish\resources.pri" -Force
        }
        if (-not (Test-Path "$ProjectDir\publish\Melodium.pri")) {
            Copy-Item $cand -Destination "$ProjectDir\publish\Melodium.pri" -Force
        }
        break
    }
}

# 4. Vygenerovat seznam souborů do WiX
Write-Host "[4/5] Generuji WiX komponenty souborů..." -ForegroundColor Yellow
& "$ProjectDir\generate-wix-files.ps1" -PublishDir "$ProjectDir\publish" -OutputFile "$ProjectDir\wix\Files.wxs"

# 5. Zkompilovat finální MSI instalátor
$outputMsi = "Melodium-Setup-$Architecture.msi"
Write-Host "[5/5] Vytvářím finální MSI balíček: $outputMsi..." -ForegroundColor Yellow
wix build -arch $Architecture "$ProjectDir\wix\Package.wxs" "$ProjectDir\wix\Files.wxs" -d ProductVersion=$Version -ext WixToolset.UI.wixext -ext WixToolset.Util.wixext -o "$ProjectDir\$outputMsi"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Chyba při kompilaci WiX MSI balíčku!"
}

$fileSize = [Math]::Round((Get-Item $outputMsi).Length / 1MB, 2)
Write-Host "`nHotovo! Instalační soubor byl úspěšně vytvořen:" -ForegroundColor Green
Write-Host "  -> $outputMsi ($fileSize MB)" -ForegroundColor White
