[CmdletBinding()]
param(
    [ValidateSet('mock', 'local-demo', 'wechat')]
    [string]$MiniProgramMode = 'mock',

    [string]$ApiBaseUrl = 'http://127.0.0.1:5150',

    [ValidateSet('Json', 'Sqlite')]
    [string]$DataProvider = 'Sqlite',

    [ValidateSet('Local', 'Oss')]
    [string]$UploadProvider = 'Local',

    [switch]$NonInteractive,
    [switch]$Force,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$sitePath = Join-Path $repoRoot 'config/site.json'
$backendLocalPath = Join-Path $repoRoot 'backend/src/AedBackend.Api/appsettings.Development.local.json'
$miniProgramEnvPath = Join-Path $repoRoot 'miniprogram/.env.local'
$adminEnvPath = Join-Path $repoRoot 'admin/.env.local'

function Fail([string]$message) {
    throw $message
}

function Get-NestedValue($source, [string[]]$path) {
    $current = $source
    foreach ($key in $path) {
        if ($null -eq $current) {
            return ''
        }

        $current = $current.$key
    }

    if ($null -eq $current) {
        return ''
    }

    return [string]$current
}

function Read-Value([string]$label, [string]$defaultValue) {
    if ($NonInteractive) {
        return $defaultValue
    }

    $suffix = if ([string]::IsNullOrWhiteSpace($defaultValue)) {
        ''
    } else {
        " [$defaultValue]"
    }

    $value = Read-Host "$label$suffix"
    if ([string]::IsNullOrWhiteSpace($value)) {
        return $defaultValue
    }

    return $value.Trim()
}

function Read-Secret([string]$label, [string]$defaultValue) {
    if (-not [string]::IsNullOrWhiteSpace($defaultValue)) {
        return $defaultValue
    }

    if ($NonInteractive) {
        return ''
    }

    $secure = Read-Host "$label（留空跳过）" -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Read-YesNo([string]$label, [bool]$defaultValue) {
    if ($NonInteractive) {
        return $defaultValue
    }

    $hint = if ($defaultValue) { 'Y/n' } else { 'y/N' }
    $answer = Read-Host "$label [$hint]"
    if ([string]::IsNullOrWhiteSpace($answer)) {
        return $defaultValue
    }

    return $answer.Trim().ToLowerInvariant() -in @('y', 'yes')
}

function Read-Double([string]$label, [double]$defaultValue) {
    if ($NonInteractive) {
        return $defaultValue
    }

    while ($true) {
        $raw = Read-Host "$label [$defaultValue]"
        if ([string]::IsNullOrWhiteSpace($raw)) {
            return $defaultValue
        }

        $parsed = 0.0
        if ([double]::TryParse(
                $raw,
                [Globalization.NumberStyles]::Float,
                [Globalization.CultureInfo]::InvariantCulture,
                [ref]$parsed)) {
            return $parsed
        }

        Write-Host '请输入有效数字。' -ForegroundColor Yellow
    }
}

function New-RandomSecret {
    $bytes = New-Object byte[] 32
    [Security.Cryptography.RandomNumberGenerator]::Fill($bytes)
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Normalize-ApiBaseUrl([string]$value) {
    $candidate = ($value ?? '').Trim()
    $uri = $null
    $isValid = [Uri]::TryCreate($candidate, [UriKind]::Absolute, [ref]$uri)
    if (-not $isValid -or $uri.Scheme -notin @('http', 'https') -or [string]::IsNullOrWhiteSpace($uri.Host)) {
        Fail "API 地址必须是完整的 http(s) 地址：$candidate"
    }

    if ($uri.Host -match '(^|\.)api\.example\.com$') {
        Fail 'API 地址仍是示例占位地址，请填写真实后端地址。'
    }

    return $candidate.TrimEnd('/')
}

function Write-GeneratedFile([string]$path, [string]$content) {
    if ((Test-Path -LiteralPath $path -PathType Leaf) -and (-not $Force)) {
        if ($NonInteractive) {
            Write-Host "[跳过] 已存在（使用 -Force 才覆盖）：$path" -ForegroundColor Yellow
            return
        }

        if (-not (Read-YesNo "文件已存在，覆盖它？ $path" $false)) {
            Write-Host "[跳过] $path" -ForegroundColor Yellow
            return
        }
    }

    if ($DryRun) {
        Write-Host "[预览] 将写入 $path" -ForegroundColor Cyan
        return
    }

    $directory = Split-Path -Parent $path
    New-Item -ItemType Directory -Path $directory -Force | Out-Null
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [IO.File]::WriteAllText($path, $content, $utf8NoBom)
    Write-Host "[写入] $path" -ForegroundColor Green
}

if (-not (Test-Path -LiteralPath $sitePath -PathType Leaf)) {
    Fail "找不到 $sitePath。请从项目根目录执行此脚本。"
}

$siteDocument = Get-Content -LiteralPath $sitePath -Raw -Encoding utf8 | ConvertFrom-Json
if ($null -eq $siteDocument.site) {
    Fail 'config/site.json 缺少 site 节点。'
}

$site = $siteDocument.site
$siteOrganizationName = Read-Value '组织/园区名称' ([string]$site.organizationName)
$siteAppTitle = Read-Value '小程序标题' ([string]$site.appTitle)
$siteAdminTitle = Read-Value '管理端标题' ([string]$site.adminTitle)
$siteSearchCity = Read-Value '地图搜索城市' ([string]$site.map.searchCity)
$siteLatitude = Read-Double '地图中心纬度' ([double]$site.map.center.latitude)
$siteLongitude = Read-Double '地图中心经度' ([double]$site.map.center.longitude)
$siteRadius = Read-Double '服务半径（公里）' ([double]$site.serviceRadiusKm)
$siteRescuePhone = Read-Value '求助电话（可留空）' ([string]$site.rescuePhone)

$campuses = @(
    @($site.campuses) | ForEach-Object {
        [ordered]@{
            code = [string]$_.code
            name = [string]$_.name
            center = [ordered]@{
                latitude = [double]$_.center.latitude
                longitude = [double]$_.center.longitude
            }
        }
    }
)

if (Read-YesNo '是否重新录入校区列表？' $false) {
    $campuses = @()
    do {
        $code = Read-Value '校区 code（小写字母/数字/短横线）' ''
        if ([string]::IsNullOrWhiteSpace($code)) {
            if ($campuses.Count -eq 0) {
                Fail '至少需要一个校区。'
            }
            break
        }

        if ($code -notmatch '^[a-z0-9]([a-z0-9-]*[a-z0-9])?$') {
            Write-Host 'code 格式不合法。' -ForegroundColor Yellow
            continue
        }

        if (@($campuses | Where-Object { $_.code -eq $code }).Count -gt 0) {
            Write-Host 'code 不能重复。' -ForegroundColor Yellow
            continue
        }

        $name = Read-Value '校区名称' $code
        $latitude = Read-Double '校区纬度' $siteLatitude
        $longitude = Read-Double '校区经度' $siteLongitude
        $campuses += [ordered]@{
            code = $code
            name = $name
            center = [ordered]@{
                latitude = $latitude
                longitude = $longitude
            }
        }
    } while (Read-YesNo '继续添加校区？' $true)
}

if ($siteRadius -le 0) {
    Fail '服务半径必须大于 0。'
}

$apiBase = Normalize-ApiBaseUrl (Read-Value '后端 API 地址' $ApiBaseUrl)

$existingBackend = $null
if (Test-Path -LiteralPath $backendLocalPath -PathType Leaf) {
    $existingBackend = Get-Content -LiteralPath $backendLocalPath -Raw -Encoding utf8 | ConvertFrom-Json
}

$existingAdminKey = Get-NestedValue $existingBackend @('WeChat', 'AdminKey')
$existingSessionSecret = Get-NestedValue $existingBackend @('Admin', 'SessionHashSecret')
$existingAppId = Get-NestedValue $existingBackend @('WeChat', 'AppId')
$existingAppSecret = Get-NestedValue $existingBackend @('WeChat', 'AppSecret')
$existingTemplateId = Get-NestedValue $existingBackend @('WeChat', 'SubscribeTemplateId')
$existingOssEndpoint = Get-NestedValue $existingBackend @('Upload', 'Oss', 'Endpoint')
$existingOssBucket = Get-NestedValue $existingBackend @('Upload', 'Oss', 'BucketName')
$existingOssAccessKey = Get-NestedValue $existingBackend @('Upload', 'Oss', 'AccessKeyId')
$existingOssSecret = Get-NestedValue $existingBackend @('Upload', 'Oss', 'AccessKeySecret')
$existingOssPublicBaseUrl = Get-NestedValue $existingBackend @('Upload', 'Oss', 'PublicBaseUrl')
$existingOssPrefix = Get-NestedValue $existingBackend @('Upload', 'Oss', 'ObjectKeyPrefix')

$adminKey = if ([string]::IsNullOrWhiteSpace($env:AED_PUSH_ADMIN_KEY)) {
    if ([string]::IsNullOrWhiteSpace($existingAdminKey)) { New-RandomSecret } else { $existingAdminKey }
} else {
    $env:AED_PUSH_ADMIN_KEY
}
$sessionSecret = if ([string]::IsNullOrWhiteSpace($env:AED_ADMIN_SESSION_HASH_SECRET)) {
    if ([string]::IsNullOrWhiteSpace($existingSessionSecret)) { New-RandomSecret } else { $existingSessionSecret }
} else {
    $env:AED_ADMIN_SESSION_HASH_SECRET
}

$appId = Read-Secret '微信 AppID' ($(if ($env:WECHAT_APP_ID) { $env:WECHAT_APP_ID } else { $existingAppId }))
$appSecret = Read-Secret '微信 AppSecret' ($(if ($env:WECHAT_APP_SECRET) { $env:WECHAT_APP_SECRET } else { $existingAppSecret }))
$templateId = Read-Value '微信订阅消息模板 ID（可留空）' ($(if ($env:WECHAT_SUBSCRIBE_TEMPLATE_ID) { $env:WECHAT_SUBSCRIBE_TEMPLATE_ID } else { $existingTemplateId }))
$tencentMapKey = Read-Secret '腾讯地图 Key（可留空）' $env:VITE_TENCENT_MAP_KEY
$amapKey = Read-Secret '高德地图 Key（可留空）' $env:VITE_AMAP_KEY
$amapSecurityCode = Read-Secret '高德地图安全密钥（可留空）' $env:VITE_AMAP_SECURITY_CODE

$uploadProvider = $UploadProvider
if ($uploadProvider -eq 'Oss') {
    $ossEndpoint = Read-Value 'OSS Endpoint' ($(if ($env:AED_OSS_ENDPOINT) { $env:AED_OSS_ENDPOINT } else { $existingOssEndpoint }))
    $ossBucket = Read-Value 'OSS BucketName' ($(if ($env:AED_OSS_BUCKET_NAME) { $env:AED_OSS_BUCKET_NAME } else { $existingOssBucket }))
    $ossAccessKey = Read-Secret 'OSS AccessKeyId' ($(if ($env:AED_OSS_ACCESS_KEY_ID) { $env:AED_OSS_ACCESS_KEY_ID } else { $existingOssAccessKey }))
    $ossSecret = Read-Secret 'OSS AccessKeySecret' ($(if ($env:AED_OSS_ACCESS_KEY_SECRET) { $env:AED_OSS_ACCESS_KEY_SECRET } else { $existingOssSecret }))
    $ossPublicBaseUrl = Read-Value 'OSS PublicBaseUrl' ($(if ($env:AED_OSS_PUBLIC_BASE_URL) { $env:AED_OSS_PUBLIC_BASE_URL } else { $existingOssPublicBaseUrl }))
    $ossPrefix = Read-Value 'OSS ObjectKeyPrefix（可留空）' ($(if ($env:AED_OSS_OBJECT_KEY_PREFIX) { $env:AED_OSS_OBJECT_KEY_PREFIX } else { $existingOssPrefix }))
} else {
    $ossEndpoint = ''
    $ossBucket = ''
    $ossAccessKey = ''
    $ossSecret = ''
    $ossPublicBaseUrl = ''
    $ossPrefix = ''
}

$siteOutput = [ordered]@{
    schemaVersion = 1
    site = [ordered]@{
        organizationName = $siteOrganizationName
        appTitle = $siteAppTitle
        adminTitle = $siteAdminTitle
        coordinateSystem = 'gcj02'
        map = [ordered]@{
            center = [ordered]@{
                latitude = $siteLatitude
                longitude = $siteLongitude
            }
            searchCity = $siteSearchCity
        }
        campuses = $campuses
        serviceRadiusKm = $siteRadius
        rescuePhone = $siteRescuePhone
    }
}

$backendOutput = [ordered]@{
    WeChat = [ordered]@{
        AppId = $appId
        AppSecret = $appSecret
        SubscribeTemplateId = $templateId
        AdminKey = $adminKey
        DefaultMiniProgramState = 'trial'
        PublicBaseUrl = $apiBase
    }
    Admin = [ordered]@{
        SessionHashSecret = $sessionSecret
    }
    Data = [ordered]@{
        Provider = $DataProvider
        SqlitePath = 'data/aed.db'
    }
    Upload = [ordered]@{
        Provider = $uploadProvider
        Oss = [ordered]@{
            Endpoint = $ossEndpoint
            BucketName = $ossBucket
            AccessKeyId = $ossAccessKey
            AccessKeySecret = $ossSecret
            PublicBaseUrl = $ossPublicBaseUrl
            ObjectKeyPrefix = $ossPrefix
        }
    }
    Demo = [ordered]@{
        Enabled = ($MiniProgramMode -eq 'local-demo')
    }
}

$miniProgramOutput = @"
# Generated by scripts/setup-project.ps1. This file is ignored by Git.
VITE_APP_MODE=$MiniProgramMode
VITE_API_BASE_URL=$apiBase
VITE_APP_ENV=development
VITE_RESTORE_AUTH_SESSION=false
VITE_TENCENT_MAP_KEY=$tencentMapKey
VITE_UPLOAD_MAX_SIZE=5242880
VITE_WECHAT_ALERT_TEMPLATE_ID=$templateId
"@

$adminOutput = @"
# Generated by scripts/setup-project.ps1. This file is ignored by Git.
VITE_AMAP_KEY=$amapKey
VITE_AMAP_SECURITY_CODE=$amapSecurityCode
"@

Write-GeneratedFile $sitePath (($siteOutput | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
Write-GeneratedFile $backendLocalPath (($backendOutput | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
Write-GeneratedFile $miniProgramEnvPath $miniProgramOutput
Write-GeneratedFile $adminEnvPath $adminOutput

$missing = @()
if ($MiniProgramMode -eq 'wechat') {
    if ([string]::IsNullOrWhiteSpace($appId)) { $missing += 'WECHAT_APP_ID / WeChat:AppId' }
    if ([string]::IsNullOrWhiteSpace($appSecret)) { $missing += 'WECHAT_APP_SECRET / WeChat:AppSecret' }
    if ($apiBase -match '^https?://127\.0\.0\.1|^https?://localhost') { $missing += 'wechat 模式仍使用本机 API 地址' }
}
if ($uploadProvider -eq 'Oss') {
    foreach ($item in @(
        @('AED_OSS_ENDPOINT', $ossEndpoint),
        @('AED_OSS_BUCKET_NAME', $ossBucket),
        @('AED_OSS_ACCESS_KEY_ID', $ossAccessKey),
        @('AED_OSS_ACCESS_KEY_SECRET', $ossSecret),
        @('AED_OSS_PUBLIC_BASE_URL', $ossPublicBaseUrl)
    )) {
        if ([string]::IsNullOrWhiteSpace($item[1])) {
            $missing += $item[0]
        }
    }
}

Write-Host ''
if ($DryRun) {
    Write-Host '预览完成：未写入任何文件。' -ForegroundColor Cyan
} else {
    Write-Host '初始化配置已生成。' -ForegroundColor Green
}
Write-Host "运行模式：$MiniProgramMode"
Write-Host "API 地址：$apiBase"
Write-Host '敏感配置仅写入被 .gitignore 排除的本地文件，不会写入 README 或仓库文档。'
Write-Host '接下来可运行：'
Write-Host '  pwsh -File scripts/start-local-demo.ps1 -PrepareOnly'
Write-Host '  pnpm --dir miniprogram typecheck'
if ($missing.Count -gt 0) {
    Write-Host ''
    Write-Host '仍需补齐：' -ForegroundColor Yellow
    $missing | ForEach-Object { Write-Host "  - $_" -ForegroundColor Yellow }
}
