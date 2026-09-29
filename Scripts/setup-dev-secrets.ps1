# ============================================================
#  Dev secrets setup (User Secrets) - run once
#
#  Reads real values from git history (the last commit that had
#  them inside appsettings.json) and stores them in the LOCAL
#  User Secrets store - never committed to git.
#
#  Usage (from the solution folder D:\Backend):
#     powershell -ExecutionPolicy Bypass -File Scripts\setup-dev-secrets.ps1
#
#  Safe to re-run: keys already present in the store are NOT
#  overwritten (your manual values, e.g. local connection
#  string, are kept).
#
#  NOTE: this file is intentionally English/ASCII only -
#  Windows PowerShell 5.1 misreads UTF-8 .ps1 files without
#  BOM under non-UTF8 system codepages.
# ============================================================

$ErrorActionPreference = "Stop"

# Last commit that contained the real values in appsettings.json
$sourceCommit = "cf14976"

$projectDir = (Resolve-Path (Join-Path $PSScriptRoot "..\Andalos.API")).Path
Write-Host "==> Project: $projectDir" -ForegroundColor Cyan

Write-Host "==> Reading appsettings.json from commit $sourceCommit ..." -ForegroundColor Cyan
$raw = git show "${sourceCommit}:Andalos.API/appsettings.json"
if ($LASTEXITCODE -ne 0) { throw "Cannot read commit $sourceCommit - make sure you run this from inside the repository folder" }
$json = ($raw -join "`n") | ConvertFrom-Json

# Existing secrets - so we never overwrite manual values
$existing = ""
try { $existing = (dotnet user-secrets list --project $projectDir) -join "`n" } catch {}

function Set-Secret {
    param([string]$Key, [string]$Value)
    if ([string]::IsNullOrWhiteSpace($Value) -or $Value -like "SET_IN_*") {
        Write-Host "  (skip, empty) $Key" -ForegroundColor DarkGray
        return
    }
    if ($existing -match [regex]::Escape($Key)) {
        Write-Host "  (already set - kept your value) $Key" -ForegroundColor Yellow
        return
    }
    dotnet user-secrets set "$Key" "$Value" --project "$projectDir" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Failed to save: $Key" }
    Write-Host "  [OK] $Key" -ForegroundColor Green
}

Write-Host "==> Saving secrets to the User Secrets store ..." -ForegroundColor Cyan
Set-Secret "ConnectionStrings:DefaultConnection" $json.ConnectionStrings.DefaultConnection
Set-Secret "JwtSettings:SecretKey"               $json.JwtSettings.SecretKey
Set-Secret "VapidDetails:PrivateKey"             $json.VapidDetails.PrivateKey
Set-Secret "VapidDetails:PublicKey"              $json.VapidDetails.PublicKey
Set-Secret "VapidDetails:Subject"                $json.VapidDetails.Subject

Write-Host ""
Write-Host "==> Current secret store:" -ForegroundColor Cyan
dotnet user-secrets list --project "$projectDir"

Write-Host ""
Write-Host "DONE - press F5. appsettings.json no longer holds any secrets." -ForegroundColor Green
