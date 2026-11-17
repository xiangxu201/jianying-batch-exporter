$ErrorActionPreference = 'Stop'

$appCandidates = @(
    (Join-Path $PSScriptRoot 'release\批量处理助手-免安装版-win-x64\批量处理助手.exe'),
    (Join-Path $PSScriptRoot 'dist\JianyingBatchExporter.exe')
)
$app = $appCandidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1

if ($null -eq $app) {
    Write-Host 'Application not found. Run 发布免安装包.cmd first.' -ForegroundColor Red
    Read-Host 'Press Enter to close'
    exit 1
}

$log = Join-Path (Split-Path -Parent $app) 'startup.log'
try {
    $process = Start-Process -FilePath $app -PassThru
    Start-Sleep -Seconds 3
    if ($process.HasExited) {
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
