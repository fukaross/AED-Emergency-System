# 本地演示一键配置与启动（Windows PowerShell 7，仅承诺本机实际验证过的路径）
#
# 流程：
#   [1/5] 配置与演示数据校验（node demo/tools/verify-demo.mjs）
#   [2/5] 前端依赖与构建产物检查（按需安装 pnpm 依赖、构建 Admin 静态产物与小程序 local-demo 包）
#   [3/5] 初始化 SQLite 演示数据（init-demo-data 到指定空目录，或 -SkipInit 复用已有库）
#   [4/5] 启动本地演示后端（http://127.0.0.1:5150，上传目录指向 DataRoot\uploads）
#   [5/5] 管理员 bootstrap（兼容 409 已初始化）→ 普通登录自检 → 打印三端上手面板
#
# 安全约定（不提供绕过开关）：
#   - 不自动删除任何已有数据库或目录；目标目录非空且未传 -SkipInit 时直接失败并给出可操作提示。
#   - 不写入固定弱口令：管理员密码必须由使用者传入（SecureString 交互输入或 -AdminPassword，至少 8 位）。
#   - 引导密钥与会话哈希密钥仅存在于当前进程环境变量，不落盘、不打印。
#   - 不向任何外部地址发送密钥或请求。
#
# 用法：
#   pwsh -File scripts/start-local-demo.ps1 -PrepareOnly                     # 校验 + 前端构建准备 + 初始化数据
#   pwsh -File scripts/start-local-demo.ps1 -AdminPassword '<至少8位强密码>' # 一键完成准备 + 启动 + 初始化管理员
#   pwsh -File scripts/start-local-demo.ps1 -AdminPassword '<至少8位强密码>' -SkipInit
# 退出码：0 成功；1 前置条件/命令失败；2 参数或路径不合法。

[CmdletBinding()]
param(
    [string]$DataRoot = (Join-Path ([System.IO.Path]::GetTempPath()) 'aed-local-demo'),
    [string]$AdminUsername = 'admin',
    [string]$AdminPassword,
    [ValidateSet('local-demo', 'mock')]
    [string]$MiniProgramMode = 'local-demo',
    [switch]$PrepareOnly,
    [switch]$SkipInit,
    [switch]$SkipFrontendBuild,
    [switch]$RebuildFrontends,
    [switch]$NoWait
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$backendProject = Join-Path $repoRoot 'backend/src/AedBackend.Api'
$adminDir = Join-Path $repoRoot 'admin'
$miniprogramDir = Join-Path $repoRoot 'miniprogram'
$adminIndexHtml = Join-Path $repoRoot 'backend/src/AedBackend.Api/wwwroot/admin/index.html'
$mpVendorJs = Join-Path $repoRoot 'miniprogram/dist/build/mp-weixin/common/vendor.js'
$mpBuildMetadata = Join-Path (Split-Path -Parent (Split-Path -Parent (Split-Path -Parent $mpVendorJs))) 'mp-weixin-build-config.json'
$apiBase = 'http://127.0.0.1:5150'
$supportedNodeVersionMessage = '请安装 Node.js 22.13+ 或 24+。'

function Fail([string]$message, [int]$code = 1) {
    Write-Host "[失败] $message" -ForegroundColor Red
    exit $code
}

function Require-Command([string]$name, [string]$hint) {
    if (-not (Get-Command $name -ErrorAction SilentlyContinue)) { Fail "缺少命令 $name。$hint" 1 }
}

function Test-SupportedNodeVersion([string]$version) {
    if ($version -notmatch '^v?(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$') {
        return $false
    }

    $major = [int]$Matches.major
    $minor = [int]$Matches.minor
    return $major -ge 24 -or ($major -eq 22 -and $minor -ge 13)
}

function Test-MiniProgramBuiltConfiguration(
    [string]$vendorPath,
    [string]$metadataPath,
    [string]$expectedMode,
    [string]$expectedApiBase
) {
    if (-not (Test-Path -LiteralPath $vendorPath)) { return $false }
    $content = Get-Content -LiteralPath $vendorPath -Raw -ErrorAction SilentlyContinue
    if (-not $content) { return $false }
    if (-not (Test-Path -LiteralPath $metadataPath)) { return $false }

    try {
        $metadata = Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json -ErrorAction Stop
    }
    catch {
        return $false
    }

    $hasExpectedMode = switch ($expectedMode) {
        'local-demo' { $content.Contains('("local-demo")') }
        'wechat' { $content.Contains('("wechat")') }
        'mock' { $content.Contains('("mock")') -or $content.Contains('按 mock') }
    }
    return $hasExpectedMode `
        -and $metadata.schemaVersion -eq 1 `
        -and $metadata.mode -eq $expectedMode `
        -and $metadata.apiBaseUrl -eq $expectedApiBase.TrimEnd('/')
}

Write-Host "== AED 本地演示一键准备与启动 ==" -ForegroundColor Cyan
Write-Host "仓库根目录: $repoRoot"
Write-Host "数据目录  : $DataRoot"
Write-Host "小程序档位: $MiniProgramMode"

# 0) 前置检查
Require-Command 'dotnet' '请安装 .NET 10 SDK。'
Require-Command 'node' $supportedNodeVersionMessage
$nodeVersion = (& node --version).Trim()
if ($LASTEXITCODE -ne 0 -or -not (Test-SupportedNodeVersion $nodeVersion)) {
    Fail "当前 Node.js 版本不受支持：$nodeVersion。$supportedNodeVersionMessage" 1
}
Write-Host "Node.js 版本检查通过：$nodeVersion"
if (-not $SkipFrontendBuild) {
    Require-Command 'pnpm' '请安装 pnpm 10+（corepack enable pnpm）。'
}
foreach ($required in @('config/site.json', 'demo/roles.json', 'backend/AedBackend.sln')) {
    if (-not (Test-Path (Join-Path $repoRoot $required))) { Fail "缺少必需文件 $required（请在仓库根执行本脚本）。" 2 }
}
if (-not [System.IO.Path]::IsPathRooted($DataRoot)) { Fail "-DataRoot 必须是绝对路径。" 2 }
if (-not $PrepareOnly) {
    if (-not $AdminPassword) {
        $secure = Read-Host -AsSecureString "请设置本地管理员密码（至少 8 位，仅用于本机演示，不会写入仓库）"
        $AdminPassword = [System.Net.NetworkCredential]::new('', $secure).Password
    }
    if ([string]::IsNullOrWhiteSpace($AdminPassword) -or $AdminPassword.Length -lt 8) {
        Fail '管理员密码不能为空且长度不能少于 8 位（不允许弱口令/空口令）。' 2
    }
}

# 1) 配置校验：站点配置 + 演示数据一致性与距离性质（不联网、不写文件）
Write-Host "`n[1/5] 配置与演示数据校验：node demo/tools/verify-demo.mjs"
Push-Location $repoRoot
try {
    & node 'demo/tools/verify-demo.mjs'
    if ($LASTEXITCODE -ne 0) { Fail '配置/演示数据校验失败（见上方 FAIL 明细），未做任何写入。' 1 }
    Write-Host '[1/5] 配置与演示数据校验通过' -ForegroundColor Green
}
finally { Pop-Location }

# 2) 前端依赖与构建产物准备（确保 /admin/ 静态托管与小程序导入包开箱即用）
if (-not $SkipFrontendBuild) {
    Write-Host "`n[2/5] 检查并准备前端构建产物（Admin SPA + 小程序 $MiniProgramMode 包）"
    if (-not (Test-Path -LiteralPath (Join-Path $adminDir 'node_modules'))) {
        Write-Host '  正在安装 Admin 依赖：pnpm --dir admin install --frozen-lockfile'
        & pnpm --dir $adminDir install --frozen-lockfile
        if ($LASTEXITCODE -ne 0) { Fail 'Admin 依赖安装失败。' 1 }
    }
    if ($RebuildFrontends -or (-not (Test-Path -LiteralPath $adminIndexHtml))) {
        Write-Host '  正在构建 Admin 静态产物：pnpm --dir admin build'
        & pnpm --dir $adminDir build
        if ($LASTEXITCODE -ne 0 -or (-not (Test-Path -LiteralPath $adminIndexHtml))) {
            Fail 'Admin 构建失败，未生成 backend/src/AedBackend.Api/wwwroot/admin/index.html。' 1
        }
    }
    else {
        Write-Host '  Admin 静态产物已就绪（backend/src/AedBackend.Api/wwwroot/admin）'
    }

    if (-not (Test-Path -LiteralPath (Join-Path $miniprogramDir 'node_modules'))) {
        Write-Host '  正在安装小程序依赖：pnpm --dir miniprogram install --frozen-lockfile'
        & pnpm --dir $miniprogramDir install --frozen-lockfile
        if ($LASTEXITCODE -ne 0) { Fail '小程序依赖安装失败。' 1 }
    }
    if ($RebuildFrontends -or (-not (Test-MiniProgramBuiltConfiguration `
        -vendorPath $mpVendorJs -metadataPath $mpBuildMetadata `
        -expectedMode $MiniProgramMode -expectedApiBase $apiBase))) {
        Write-Host "  正在构建小程序（VITE_APP_MODE=$MiniProgramMode）：pnpm --dir miniprogram build:mp-weixin"
        $prevMode = $env:VITE_APP_MODE
        $prevBase = $env:VITE_API_BASE_URL
        try {
            $env:VITE_APP_MODE = $MiniProgramMode
            $env:VITE_API_BASE_URL = $apiBase
            & pnpm --dir $miniprogramDir build:mp-weixin
            if ($LASTEXITCODE -ne 0) { Fail '小程序构建失败。' 1 }
        }
        finally {
            $env:VITE_APP_MODE = $prevMode
            $env:VITE_API_BASE_URL = $prevBase
        }
    }
    else {
        Write-Host "  小程序构建产物已就绪（miniprogram/dist/build/mp-weixin，档位=$MiniProgramMode，API=$apiBase）"
    }
    Write-Host '[2/5] 前端准备完成' -ForegroundColor Green
}
else {
    Write-Host "`n[2/5] 已按 -SkipFrontendBuild 跳过前端检查与构建"
}

# 3) 演示数据初始化（只初始化数据；不启动服务、不发 HTTP、不创建管理员）
$dbPath = Join-Path $DataRoot 'aed.db'
$uploadsPath = Join-Path $DataRoot 'uploads'
if (-not $SkipInit) {
    Write-Host "`n[3/5] 初始化 SQLite 演示数据：init-demo-data --data-root $DataRoot"
    if (Test-Path -LiteralPath $DataRoot) {
        $existing = @(Get-ChildItem -LiteralPath $DataRoot -Force)
        if ($existing.Count -gt 0) {
            Fail "目标数据目录已存在且非空：$DataRoot。若要直接复用现有数据库启动，请追加 -SkipInit；若要全新初始化，请指定新的空目录 -DataRoot '<新路径>'（本脚本不会删除或覆盖已有数据）。" 2
        }
    }
    & dotnet run --project $backendProject -- init-demo-data --data-root $DataRoot
    if ($LASTEXITCODE -ne 0) { Fail 'init-demo-data 失败（见上方诊断），未启动服务。' 1 }
    if (-not (Test-Path -LiteralPath $dbPath)) { Fail "init-demo-data 返回成功但未找到数据库：$dbPath" 1 }
    Write-Host "[3/5] 演示数据初始化完成：$dbPath" -ForegroundColor Green
}
else {
    if (-not (Test-Path -LiteralPath $dbPath)) {
        Fail "已指定 -SkipInit，但未找到已有数据库：$dbPath。请去掉 -SkipInit 先执行初始化。" 2
    }
    New-Item -ItemType Directory -Force -Path $uploadsPath | Out-Null
    Write-Host "`n[3/5] 已按 -SkipInit 复用现有演示数据库：$dbPath" -ForegroundColor Green
}

if ($PrepareOnly) {
    Write-Host "`n[完成] 已按 -PrepareOnly 完成全部前置准备（未启动服务、未创建管理员）。" -ForegroundColor Green
    Write-Host "下一步直接启动："
    Write-Host "  pwsh -File scripts/start-local-demo.ps1 -DataRoot `"$DataRoot`" -AdminPassword '<至少8位强密码>' -SkipInit"
    exit 0
}

# 4) 启动后端（开发环境 + 受限演示模式 + 本机回环 + 上传目录归入 DataRoot）
$adminKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
$sessionSeed = [System.Text.Encoding]::UTF8.GetBytes("$env:COMPUTERNAME|$DataRoot|$AdminUsername")
$sessionKeyBytes = [System.Text.Encoding]::UTF8.GetBytes($AdminPassword)
$sessionSecret = [Convert]::ToBase64String(
    [System.Security.Cryptography.HMACSHA256]::HashData($sessionKeyBytes, $sessionSeed)
)

$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:AED_DATA_PROVIDER = 'Sqlite'
$env:AED_SQLITE_PATH = $dbPath
$env:WeChat__UploadsDirectory = $uploadsPath
$env:AED_API_PUBLIC_BASE_URL = $apiBase
$env:AED_PUSH_ADMIN_KEY = $adminKey
$env:AED_ADMIN_SESSION_HASH_SECRET = $sessionSecret
$env:Demo__Enabled = 'true'

Write-Host "`n[4/5] 启动本地演示后端：$apiBase（仅本机回环；密钥仅驻留进程内存，不打印）"
$backendProcess = Start-Process -FilePath 'dotnet' `
    -ArgumentList @('run', '--project', $backendProject, '--launch-profile', 'http') `
    -PassThru -NoNewWindow
try {
    $ready = $false
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        Start-Sleep -Seconds 1
        if ($backendProcess.HasExited) { Fail "后端进程提前退出（exit=$($backendProcess.ExitCode)），请检查上方日志。" 1 }
        try {
            $health = Invoke-RestMethod -Uri "$apiBase/health" -TimeoutSec 2
            if ($health -and $health.code -eq 0) { $ready = $true; break }
        }
        catch { }
    }
    if (-not $ready) { Fail "等待 $apiBase/health 超时（60s）。" 1 }
    Write-Host '[4/5] 后端已就绪（GET /health = 200）' -ForegroundColor Green

    # 5) 管理员 bootstrap（兼容 -SkipInit 下的 409 已初始化）+ 普通登录自检
    Write-Host "`n[5/5] 管理员初始化与登录自检：POST /api/admin/auth/bootstrap → /api/admin/auth/login"
    $bootstrapBody = @{ username = $AdminUsername; password = $AdminPassword } | ConvertTo-Json
    try {
        $null = Invoke-RestMethod -Method Post -Uri "$apiBase/api/admin/auth/bootstrap" `
            -ContentType 'application/json' -Headers @{ 'X-Aed-Admin-Key' = $adminKey } -Body $bootstrapBody
        Write-Host "  bootstrap 成功（已创建超级管理员：$AdminUsername）"
    }
    catch {
        $statusCode = $null
        if ($_.Exception.Response) { $statusCode = [int]$_.Exception.Response.StatusCode }
        if ($statusCode -eq 409) {
            Write-Host "  超级管理员已存在（HTTP 409 / code 2019），跳过重复创建"
        }
        else {
            Fail "管理员 bootstrap 失败（HTTP $statusCode）：$($_.ErrorDetails.Message)" 1
        }
    }

    $loginBody = @{ username = $AdminUsername; password = $AdminPassword; deviceId = 'local-admin-device' } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "$apiBase/api/admin/auth/login" `
        -ContentType 'application/json' -Body $loginBody
    $sessionAdminKey = if ($login.data -and $login.data.adminKey) { $login.data.adminKey } else { $login.adminKey }
    if (-not $sessionAdminKey) { Fail '普通管理员登录未返回 data.adminKey，请检查后端日志。' 1 }

    $me = Invoke-RestMethod -Method Get -Uri "$apiBase/api/admin/auth/me" `
        -Headers @{ 'X-Aed-Admin-Key' = $sessionAdminKey }
    Write-Host "[5/5] 管理员登录自检通过（账号=$($me.data.username)，角色=$($me.data.role)；adminKey 不打印）" -ForegroundColor Green

    Write-Host "`n======================================================================" -ForegroundColor Cyan
    Write-Host "  AED 急救协作系统（本地演示环境）已全部就绪！" -ForegroundColor Green
    Write-Host "======================================================================" -ForegroundColor Cyan
    Write-Host "  1. 管理端 Web SPA : $apiBase/admin/"
    Write-Host "     登录账号       : $AdminUsername（密码为你刚才设置的密码）"
    Write-Host "  2. 后端 API / 文档: $apiBase/swagger  |  健康检查: $apiBase/health"
    Write-Host "  3. 微信小程序导入 : $miniprogramDir\dist\build\mp-weixin"
    Write-Host "     使用方法       : 打开微信开发者工具 → 导入上述目录（选测试号/游客模式）"
    Write-Host "     角色一键切换   : 点击小程序左下角悬浮徽标，可在以下 3 个演示角色间切换："
    Write-Host "                      - demo-requester   （演示甲 · 求助人）"
    Write-Host "                      - demo-rescuer     （演示乙 · 东区认证急救员）"
    Write-Host "                      - demo-unqualified （演示丙 · 待认证志愿者）"
    Write-Host "  4. 数据与上传目录 : $DataRoot"
    Write-Host "     下次复用重启   : pwsh -File scripts/start-local-demo.ps1 -DataRoot `"$DataRoot`" -AdminPassword '<你的密码>' -SkipInit"
    Write-Host "======================================================================" -ForegroundColor Cyan

    if ($NoWait) {
        Write-Host "`n已按 -NoWait 完成全链路启动与自检，即将停止后端进程。"
    }
    else {
        Write-Host "`n按 Ctrl+C 结束本脚本并停止后端（不会删除数据目录）。"
        Wait-Process -Id $backendProcess.Id
    }
}
finally {
    if ($backendProcess -and -not $backendProcess.HasExited) {
        Write-Host '正在停止后端进程（不删除数据目录）...'
        Stop-Process -Id $backendProcess.Id -Force -ErrorAction SilentlyContinue
    }
}
