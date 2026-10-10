# Build signed MSIX + out-of-box EXE installer for the CmdPal extension.
#
# 流程：
#   1. 确保自签名代码签名证书（cert/HardwareMonitor.pfx + .cer，与清单 Publisher CN=HardwareMonitor 一致）
#   2. dotnet build 生成已签名 MSIX（AppPackages\<Name>_<Version>_<Platform>\）
#   3. Inno Setup 编译安装器（/DPlatform=x64|arm64），安装器负责导入证书 + Add-AppxPackage
#
# 前置条件：.NET 10 SDK、Windows SDK 10.0.26100（signtool + UAP 平台）、Inno Setup 6
param(
    [string]$Configuration = "Release",
    [string[]]$Platforms = @("x64"),
    [switch]$SkipInstaller
)

$ErrorActionPreference = "Stop"
$env:Path = "C:\Program Files\dotnet;$env:Path"

$RootDir    = $PSScriptRoot
$ProjectDir = Join-Path $RootDir "HardwareMonitorExtension"
$ProjectFile = Join-Path $ProjectDir "HardwareMonitorExtension.csproj"
$CertDir    = Join-Path $RootDir "cert"
$CertSubject = "CN=HardwareMonitor"   # 必须与 Package.appxmanifest 的 Publisher 完全一致
$PfxPassword = "HardwareMonitor"

$Inno = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1

if (-not $SkipInstaller -and -not $Inno) { throw 'Inno Setup 6 not found' }

# ---------- 1. 自签名证书（幂等：已存在则复用，保证跨版本升级不需要重新导入证书） ----------
$pfx = Join-Path $CertDir "HardwareMonitor.pfx"
$cer = Join-Path $CertDir "HardwareMonitor.cer"
if (-not (Test-Path $pfx)) {
    New-Item -ItemType Directory -Force $CertDir | Out-Null
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $CertSubject } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom `
            -Subject $CertSubject `
            -KeyAlgorithm RSA -KeyLength 2048 `
            -KeyUsage DigitalSignature `
            -FriendlyName "HardwareMonitor CmdPal signing" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @(
                "2.5.29.37={text}1.3.6.1.5.5.7.3.3,1.3.6.1.4.1.311.10.3.13",  # Code Signing + Lifetime Signing
                "2.5.29.19={text}"                                             # End Entity
            )
    }
    $pwd2 = ConvertTo-SecureString -String $PfxPassword -AsPlainText -Force
    # 用 .NET 导出（Export-PfxDocument 在部分 PowerShell 版本不可用）
    [System.IO.File]::WriteAllBytes($pfx, $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Pfx, $PfxPassword))
    Export-Certificate   -Cert $cert -FilePath $cer -Type CERT             | Out-Null
    Write-Host "Created signing cert $($cert.Thumbprint) -> $pfx"
} else {
    Write-Host "Reusing existing signing cert -> $pfx"
}

# ---------- 2. signtool（Windows SDK 优先，nuget BuildTools 兜底） ----------
$signtool = @(
    (Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1).FullName,
    (Get-ChildItem "$env:USERPROFILE\.nuget\packages\microsoft.windows.sdk.buildtools\*\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1).FullName
) | Where-Object { $_ } | Select-Object -First 1
if (-not $signtool) { throw 'signtool.exe not found (install Windows SDK 10.0.26100)' }
Write-Host "signtool: $signtool"

foreach ($Platform in $Platforms) {
    $runtime = "win-$Platform"

    # 从 csproj 读取 AppxPackageVersion，精确匹配产物，避免陈旧包目录干扰
    $version = ((Select-String -Path $ProjectFile -Pattern '<AppxPackageVersion>(.*?)</AppxPackageVersion>').Matches[0].Groups[1].Value)
    Write-Host "=== MSIX $Platform ($version) ==="

    & dotnet build $ProjectFile `
        --configuration $Configuration `
        --runtime $runtime `
        --self-contained true `
        -p:Platform=$Platform `
        -p:UapAppxPackageBuildMode=SideloadOnly `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageSigningEnabled=true `
        -p:PackageCertificateKeyFile=$pfx `
        -p:PackageCertificatePassword=$PfxPassword
    if ($LASTEXITCODE -ne 0) { throw "build failed for $Platform" }

    $msix = Get-ChildItem (Join-Path $ProjectDir "AppPackages") -Recurse -Filter "*.msix" |
        Where-Object { $_.Name -eq "HardwareMonitorExtension_${version}_$Platform.msix" } |
        Select-Object -First 1
    if (-not $msix) { throw "msix not found for $Platform ($version)" }
    Write-Host "MSIX: $($msix.FullName) ($([math]::Round($msix.Length/1MB,1)) MB)"

    if ($SkipInstaller) { continue }

    Write-Host "=== Installer $Platform ==="
    & $Inno "/DPlatform=$Platform" (Join-Path $RootDir "setup.iss")
    if ($LASTEXITCODE -ne 0) { throw "Inno failed for $Platform" }
}

Get-ChildItem (Join-Path $RootDir "bin\Release\installer") -ErrorAction SilentlyContinue |
    Select-Object Name, Length
