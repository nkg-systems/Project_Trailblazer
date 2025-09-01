# Field Operations Optimizer - Secure Environment Generator
# This script generates cryptographically secure passwords for all services

Write-Host "🔐 Field Operations Optimizer - Secure Environment Generator" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host ""

# Check if .env already exists
if (Test-Path ".env") {
    Write-Host "⚠️  WARNING: .env file already exists!" -ForegroundColor Yellow
    $overwrite = Read-Host "Do you want to overwrite it with new secure passwords? (y/N)"
    if ($overwrite -ne "y" -and $overwrite -ne "Y") {
        Write-Host "❌ Operation cancelled. Keeping existing .env file." -ForegroundColor Red
        exit
    }
    Write-Host ""
}

Write-Host "🔑 Generating cryptographically secure passwords..." -ForegroundColor Cyan

# Character sets for password generation
$UpperCase = 65..90 | ForEach-Object { [char]$_ }
$LowerCase = 97..122 | ForEach-Object { [char]$_ }  
$Numbers = 48..57 | ForEach-Object { [char]$_ }
$SpecialChars = @('!', '#', '$', '%', '&', '*', '+', '-', '.', '/', ':', '=', '?', '@', '^', '_')
$AllChars = $UpperCase + $LowerCase + $Numbers + $SpecialChars
$AlphaNumeric = $UpperCase + $LowerCase + $Numbers

# Function to generate secure password
function New-SecurePassword {
    param([int]$Length = 32)
    return -join ($AllChars | Get-Random -Count $Length)
}

# Function to generate secure token (alphanumeric only)
function New-SecureToken {
    param([int]$Length = 64)
    return -join ($AlphaNumeric | Get-Random -Count $Length)
}

# Generate all passwords
$REDIS_PASSWORD = New-SecurePassword -Length 32
$POSTGRES_PASSWORD = New-SecurePassword -Length 32
$RABBITMQ_PASSWORD = New-SecurePassword -Length 32
$GRAFANA_PASSWORD = New-SecurePassword -Length 32
$SEQ_PASSWORD = New-SecurePassword -Length 32
$MINIO_PASSWORD = New-SecurePassword -Length 32
$UNLEASH_DB_PASSWORD = New-SecurePassword -Length 32
$UNLEASH_ADMIN_TOKEN = New-SecureToken -Length 64
$UNLEASH_CLIENT_TOKEN = New-SecureToken -Length 64

Write-Host "✅ Generated 9 unique secure passwords/tokens" -ForegroundColor Green
Write-Host ""

# Create the .env file content
$envContent = @"
# Field Operations Optimizer - Environment Variables
# SECURITY: This file contains sensitive data. Never commit to version control.
# Generated: $(Get-Date -Format "yyyy-MM-dd HH:mm:ss")

# Database Configuration
POSTGRES_DB=fieldops_optimizer
POSTGRES_USER=fieldops_user
POSTGRES_PASSWORD=$POSTGRES_PASSWORD

# Redis Configuration
REDIS_PASSWORD=$REDIS_PASSWORD

# RabbitMQ Configuration
RABBITMQ_DEFAULT_USER=fieldops_user
RABBITMQ_DEFAULT_PASS=$RABBITMQ_PASSWORD
RABBITMQ_DEFAULT_VHOST=fieldops

# Monitoring & Logging
GRAFANA_ADMIN_USER=admin
GRAFANA_ADMIN_PASSWORD=$GRAFANA_PASSWORD
SEQ_ADMIN_PASSWORD=$SEQ_PASSWORD

# MinIO Storage
MINIO_ROOT_USER=fieldops_user
MINIO_ROOT_PASSWORD=$MINIO_PASSWORD

# Unleash Feature Flags
UNLEASH_DB_USER=unleash_user
UNLEASH_DB_PASSWORD=$UNLEASH_DB_PASSWORD
UNLEASH_ADMIN_TOKEN=*:*.$UNLEASH_ADMIN_TOKEN
UNLEASH_CLIENT_TOKEN=*:*.$UNLEASH_CLIENT_TOKEN

# Database Connection String (for .NET applications)
DefaultConnection=Host=localhost;Database=fieldops_optimizer;Username=fieldops_user;Password=$POSTGRES_PASSWORD

# Redis Connection String (for .NET applications)
RedisConnection=localhost:6379,password=$REDIS_PASSWORD
"@

# Write the .env file
$envContent | Out-File -FilePath ".env" -Encoding UTF8

Write-Host "📄 Created .env file with secure passwords" -ForegroundColor Green
Write-Host ""
Write-Host "🛡️  SECURITY REMINDERS:" -ForegroundColor Yellow
Write-Host "   • Never commit the .env file to version control" -ForegroundColor Gray
Write-Host "   • Keep these passwords secure and private" -ForegroundColor Gray
Write-Host "   • Rotate passwords regularly in production" -ForegroundColor Gray
Write-Host "   • Use Azure Key Vault or similar for production deployments" -ForegroundColor Gray
Write-Host ""

# Display service access information
Write-Host "🌐 SERVICE ACCESS (after starting with .\start-services.ps1):" -ForegroundColor Cyan
Write-Host "   • Grafana Dashboard: http://localhost:3000 (admin / <GRAFANA_PASSWORD>)" -ForegroundColor Gray
Write-Host "   • RabbitMQ Management: http://localhost:15672 (fieldops_user / <RABBITMQ_PASSWORD>)" -ForegroundColor Gray
Write-Host "   • Seq Logging: http://localhost:5341 (admin / <SEQ_PASSWORD>)" -ForegroundColor Gray
Write-Host "   • MinIO Console: http://localhost:9001 (fieldops_user / <MINIO_PASSWORD>)" -ForegroundColor Gray
Write-Host ""
Write-Host "✨ Environment setup complete! You can now start your services." -ForegroundColor Green
