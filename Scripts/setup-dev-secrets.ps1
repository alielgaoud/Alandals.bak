# ============================================================
#  إعداد أسرار التطوير (User Secrets) — يعمل مرة واحدة فقط
#
#  يقرأ الإعدادات الحقيقية من تاريخ Git (قبل نقلها خارج
#  appsettings.json) ويحفظها في مخزن User Secrets المحلي
#  الذي لا يُرفع إلى Git إطلاقاً (%APPDATA%\Microsoft\UserSecrets).
#
#  التشغيل من مجلد الحل D:\Backend :
#     powershell -ExecutionPolicy Bypass -File Scripts\setup-dev-secrets.ps1
# ============================================================

$ErrorActionPreference = "Stop"

# آخر كومِت يحتوي appsettings.json بالقيم الحقيقية (قبل نقل الأسرار)
$sourceCommit = "cf14976"

$projectDir = (Resolve-Path (Join-Path $PSScriptRoot "..\Andalos.API")).Path
Write-Host "==> المشروع: $projectDir" -ForegroundColor Cyan

Write-Host "==> قراءة appsettings.json من الكومِت $sourceCommit ..." -ForegroundColor Cyan
$raw = git show "${sourceCommit}:Andalos.API/appsettings.json"
if ($LASTEXITCODE -ne 0) { throw "فشل الوصول للكومِت $sourceCommit — تأكد أنك داخل مجلد المستودع" }
$json = ($raw -join "`n") | ConvertFrom-Json

function Set-Secret {
    param([string]$Key, [string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -like "SET_IN_*") {
        Write-Host "  (تخطي) $Key" -ForegroundColor DarkGray
        return
    }
    dotnet user-secrets set "$Key" "$Value" --project "$projectDir" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "فشل حفظ: $Key" }
    Write-Host "  ✔ $Key" -ForegroundColor Green
}

Write-Host "==> حفظ الأسرار في User Secrets ..." -ForegroundColor Cyan
Set-Secret "ConnectionStrings:DefaultConnection" $json.ConnectionStrings.DefaultConnection
Set-Secret "JwtSettings:SecretKey"               $json.JwtSettings.SecretKey
Set-Secret "VapidDetails:PrivateKey"             $json.VapidDetails.PrivateKey
Set-Secret "VapidDetails:PublicKey"              $json.VapidDetails.PublicKey
Set-Secret "VapidDetails:Subject"                $json.VapidDetails.Subject

Write-Host ""
Write-Host "==> التحقق — الأسرار المخزنة:" -ForegroundColor Cyan
dotnet user-secrets list --project "$projectDir"

Write-Host ""
Write-Host "✅ تم! شغّل المشروع (F5) وسيعمل مباشرة — appsettings.json لم يعد يحمل أي أسرار" -ForegroundColor Green
