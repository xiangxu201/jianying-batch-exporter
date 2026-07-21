$ErrorActionPreference = 'Stop'
$app = Join-Path $PSScriptRoot 'dist\JianyingBatchExporter.exe'
$log = Join-Path $PSScriptRoot 'dist\startup.log'

if (-not (Test-Path -LiteralPath $app)) {
    Write-Host "Application not found: $app" -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}

try {
    Start-Process -FilePath $app
    Start-Sleep -Seconds 3
    $running = Get-Process -Name 'JianyingBatchExporter' -ErrorAction SilentlyContinue
    if ($null -eq $running) {
        Write-Host 'Jianying Batch Exporter failed to start.' -ForegroundColor Red
        if (Test-Path -LiteralPath $log) {
            Write-Host ''
            Write-Host '===== Startup log =====' -ForegroundColor Yellow
            Get-Content -LiteralPath $log -Encoding UTF8
        }
        Read-Host 'Press Enter to close'
        exit 1
    }
} catch {
    Write-Host "Startup failed: $($_.Exception.Message)" -ForegroundColor Red
    if (Test-Path -LiteralPath $log) {
        Get-Content -LiteralPath $log -Encoding UTF8
    }
    Read-Host 'Press Enter to close'
    exit 1
}
