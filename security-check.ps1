# Field Operations Optimizer - Security Validation Script
# This script checks for potential security vulnerabilities

param(
    [Parameter(HelpMessage="Perform comprehensive security scan")]
    [switch]$Comprehensive,
    
    [Parameter(HelpMessage="Check git history for leaked passwords")]
    [switch]$GitHistory,
    
    [Parameter(HelpMessage="Validate current configuration")]
    [switch]$Config
)

Write-Host "🛡️  Field Operations Optimizer - Security Validation" -ForegroundColor Green
Write-Host "=====================================================" -ForegroundColor Green
Write-Host ""

$issues = @()
$warnings = @()

# Function to add security issue
function Add-SecurityIssue {
    param($Message, $Severity = "HIGH")
    $script:issues += @{ Message = $Message; Severity = $Severity }
    Write-Host "❌ [SECURITY ISSUE] $Message" -ForegroundColor Red
}

# Function to add security warning
function Add-SecurityWarning {
    param($Message)
    $script:warnings += $Message
    Write-Host "⚠️  [WARNING] $Message" -ForegroundColor Yellow
}

# Function to add security success
function Add-SecuritySuccess {
    param($Message)
    Write-Host "✅ $Message" -ForegroundColor Green
}

Write-Host "🔍 Running security checks..." -ForegroundColor Cyan
Write-Host ""

# Check 1: .env file existence and permissions
Write-Host "1. Environment Configuration Security" -ForegroundColor Blue
if (Test-Path ".env") {
    Add-SecuritySuccess ".env file exists"
    
    # Check if .env has secure passwords
    $envContent = Get-Content ".env" -Raw
    if ($envContent -match "your_secure_.*_password" -or $envContent -match "placeholder") {
        Add-SecurityIssue ".env file contains placeholder passwords - use generate-secure-env.ps1"
    } else {
        Add-SecuritySuccess ".env file appears to contain real passwords"
    }
    
    # Check for common weak passwords
    $weakPatterns = @("password123", "admin", "test", "123456", "fieldops_redis_password")
    foreach ($pattern in $weakPatterns) {
        if ($envContent -match [regex]::Escape($pattern)) {
            Add-SecurityIssue ".env file contains weak/leaked password: $pattern"
        }
    }
} else {
    Add-SecurityIssue ".env file missing - services will fail to start securely"
}

# Check 2: .env.example exists (but not with real passwords)
Write-Host ""
Write-Host "2. Template Security" -ForegroundColor Blue
if (Test-Path ".env.example") {
    Add-SecuritySuccess ".env.example template exists"
    $exampleContent = Get-Content ".env.example" -Raw
    $realPasswordPatterns = @('[A-Za-z0-9]{20,}', '[!@#$%^&*]{2,}')
    $hasRealPasswords = $false
    foreach ($pattern in $realPasswordPatterns) {
        if ($exampleContent -match $pattern) {
            $hasRealPasswords = $true
            break
        }
    }
    if ($hasRealPasswords) {
        Add-SecurityWarning ".env.example may contain real passwords instead of placeholders"
    } else {
        Add-SecuritySuccess ".env.example contains only placeholder values"
    }
} else {
    Add-SecurityWarning ".env.example template missing"
}

# Check 3: .gitignore configuration
Write-Host ""
Write-Host "3. Version Control Security" -ForegroundColor Blue
if (Test-Path ".gitignore") {
    $gitignoreContent = Get-Content ".gitignore" -Raw
    if ($gitignoreContent -match "\.env") {
        Add-SecuritySuccess ".gitignore properly excludes .env files"
    } else {
        Add-SecurityIssue ".gitignore does not exclude .env files - passwords may be committed!"
    }
} else {
    Add-SecurityWarning ".gitignore file missing"
}

# Check 4: Git status for uncommitted .env
if (Get-Command git -ErrorAction SilentlyContinue) {
    $gitStatus = git status --porcelain 2>$null
    if ($gitStatus -match "\.env") {
        Add-SecurityIssue ".env file is staged for commit - this would leak passwords!"
    } else {
        Add-SecuritySuccess ".env file is not staged for commit"
    }
}

# Check 5: Source code for hardcoded passwords
Write-Host ""
Write-Host "4. Source Code Security" -ForegroundColor Blue
$codeFiles = Get-ChildItem -Recurse -Include "*.cs", "*.json", "*.yml", "*.yaml", "*.ps1" | 
    Where-Object { $_.FullName -notmatch "bin\\"|$_.FullName -notmatch "obj\\"|$_.FullName -notmatch "node_modules\\"|$_.FullName -notmatch "\.git\\"|$_.Name -ne "generate-secure-env.ps1"|$_.Name -ne "security-check.ps1" }

$hardcodedPatterns = @(
    "fieldops_redis_password",
    "fieldops_password", 
    "fieldops_admin",
    "password.*=.*['\"][^'\"]{8,}['\"]",
    "Password.*=.*['\"][^'\"]{8,}['\"]"
)

$foundHardcoded = $false
foreach ($file in $codeFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content) {
        foreach ($pattern in $hardcodedPatterns) {
            if ($content -match $pattern) {
                Add-SecurityIssue "Hardcoded password found in: $($file.Name)"
                $foundHardcoded = $true
                break
            }
        }
    }
}

if (-not $foundHardcoded) {
    Add-SecuritySuccess "No hardcoded passwords found in source code"
}

# Check 6: Docker Compose environment variable usage
Write-Host ""
Write-Host "5. Docker Configuration Security" -ForegroundColor Blue
$dockerFiles = Get-ChildItem -Name "docker-compose*.yml"
$usesEnvVars = $true
foreach ($dockerFile in $dockerFiles) {
    $content = Get-Content $dockerFile -Raw
    # Check for hardcoded passwords (not environment variables)
    if ($content -match 'password.*:.*[^$\{]') {
        $usesEnvVars = $false
        Add-SecurityIssue "Docker file $dockerFile may contain hardcoded passwords"
    }
}

if ($usesEnvVars) {
    Add-SecuritySuccess "Docker Compose files properly use environment variables"
}

# Check 7: Git history (if requested)
if ($GitHistory -or $Comprehensive) {
    Write-Host ""
    Write-Host "6. Git History Security" -ForegroundColor Blue
    if (Get-Command git -ErrorAction SilentlyContinue) {
        Write-Host "   Checking git history for leaked passwords..." -ForegroundColor Gray
        $gitLog = git log --all --grep="password" --grep="secret" --grep="token" --oneline 2>$null
        if ($gitLog) {
            Add-SecurityWarning "Git history contains commits mentioning passwords/secrets"
            Write-Host "   Commits found:" -ForegroundColor Gray
            $gitLog | ForEach-Object { Write-Host "     $_" -ForegroundColor Gray }
        } else {
            Add-SecuritySuccess "No obvious password-related commits found in git history"
        }
    }
}

# Summary
Write-Host ""
Write-Host "📊 SECURITY ASSESSMENT SUMMARY" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan

if ($issues.Count -eq 0) {
    Write-Host "🎉 No critical security issues found!" -ForegroundColor Green
} else {
    Write-Host "🚨 Found $($issues.Count) security issue(s):" -ForegroundColor Red
    $issues | ForEach-Object { Write-Host "   • $($_.Message)" -ForegroundColor Red }
}

if ($warnings.Count -gt 0) {
    Write-Host ""
    Write-Host "⚠️  Found $($warnings.Count) warning(s):" -ForegroundColor Yellow
    $warnings | ForEach-Object { Write-Host "   • $_" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "🛡️  SECURITY RECOMMENDATIONS:" -ForegroundColor Yellow
Write-Host "   • Run ./generate-secure-env.ps1 to create secure passwords" -ForegroundColor Gray
Write-Host "   • Never commit .env files to version control" -ForegroundColor Gray  
Write-Host "   • Rotate passwords regularly (every 90 days minimum)" -ForegroundColor Gray
Write-Host "   • Use Azure Key Vault or similar for production" -ForegroundColor Gray
Write-Host "   • Enable 2FA on all service accounts where possible" -ForegroundColor Gray

if ($issues.Count -gt 0) {
    Write-Host ""
    Write-Host "❌ Security validation FAILED. Please address the issues above." -ForegroundColor Red
    exit 1
} else {
    Write-Host ""
    Write-Host "✅ Security validation PASSED!" -ForegroundColor Green
    exit 0
}
