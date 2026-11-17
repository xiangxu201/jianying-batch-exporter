[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath($PSScriptRoot)
$projectFile = Join-Path $projectRoot 'src\JianyingBatchExporter\JianyingBatchExporter.csproj'
$releaseRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'release'))
$packageName = '批量处理助手-免安装版-win-x64'
$packageDirectory = [IO.Path]::GetFullPath((Join-Path $releaseRoot $packageName))
$zipPath = [IO.Path]::GetFullPath((Join-Path $releaseRoot ($packageName + '.zip')))
$hashPath = $zipPath + '.sha256.txt'

if (-not $packageDirectory.StartsWith($releaseRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
    throw "Invalid package directory: $packageDirectory"
}

New-Item -ItemType Directory -Path $releaseRoot -Force | Out-Null
if (Test-Path -LiteralPath $packageDirectory) {
    Remove-Item -LiteralPath $packageDirectory -Recurse -Force
}
foreach ($path in @($zipPath, $hashPath)) {
    if (Test-Path -LiteralPath $path) {
        Remove-Item -LiteralPath $path -Force
    }
}

dotnet publish $projectFile `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -o $packageDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$sourceExecutable = Join-Path $packageDirectory 'JianyingBatchExporter.exe'
$portableExecutable = Join-Path $packageDirectory '批量处理助手.exe'
if (-not (Test-Path -LiteralPath $sourceExecutable)) {
    throw "Published executable not found: $sourceExecutable"
}
Move-Item -LiteralPath $sourceExecutable -Destination $portableExecutable

$requiredFiles = @(
    $portableExecutable,
    (Join-Path $packageDirectory 'watermark.png'),
    (Join-Path $packageDirectory '使用说明.html'),
    (Join-Path $packageDirectory '免安装版说明.txt'),
    (Join-Path $packageDirectory 'tools\ffmpeg.exe'),
    (Join-Path $packageDirectory 'tools\FFmpeg.LICENSE.txt'),
    (Join-Path $packageDirectory 'tools\FFmpeg.SOURCE.txt'),
    (Join-Path $packageDirectory 'tools\jy-draftc.exe'),
    (Join-Path $packageDirectory 'tools\jy-draftc.LICENSE.txt')
)
foreach ($path in $requiredFiles) {
    if (-not (Test-Path -LiteralPath $path)) {
        throw "Portable package is incomplete: $path"
    }
}

Compress-Archive -LiteralPath $packageDirectory -DestinationPath $zipPath -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $zipPath -Algorithm SHA256
Set-Content -LiteralPath $hashPath -Encoding UTF8 -Value ($hash.Hash + '  ' + [IO.Path]::GetFileName($zipPath))

$packageSize = [Math]::Round((Get-ChildItem -LiteralPath $packageDirectory -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
$zipSize = [Math]::Round((Get-Item -LiteralPath $zipPath).Length / 1MB, 1)
Write-Host ''
Write-Host '免安装包发布完成' -ForegroundColor Green
Write-Host "文件夹：$packageDirectory"
Write-Host "压缩包：$zipPath"
Write-Host "体积：文件夹 $packageSize MB；ZIP $zipSize MB"
Write-Host "SHA256：$($hash.Hash)"
