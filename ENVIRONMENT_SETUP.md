# Environment Setup Guide

## 🔐 Security Configuration

This project uses environment variables to securely manage sensitive configuration like passwords and API tokens.

### Quick Setup

1. **Copy the environment template:**
   ```bash
   cp .env.example .env
   ```

2. **Edit the .env file with your secure values:**
   ```bash
   # Use a text editor to update .env with real passwords
   notepad .env  # Windows
   ```

3. **Generate secure passwords:**
   ```powershell
   # PowerShell - Generate random passwords
   $REDIS_PASSWORD = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | % {[char]$_})
   $POSTGRES_PASSWORD = -join ((65..90) + (97..122) + (48..57) | Get-Random -Count 32 | % {[char]$_})
   
   Write-Host "REDIS_PASSWORD=$REDIS_PASSWORD"
   Write-Host "POSTGRES_PASSWORD=$POSTGRES_PASSWORD"
   ```

### Required Environment Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `REDIS_PASSWORD` | Redis authentication password | `your_secure_redis_password` |
| `POSTGRES_PASSWORD` | PostgreSQL database password | `your_secure_db_password` |
| `RABBITMQ_DEFAULT_PASS` | RabbitMQ management password | `your_secure_rabbitmq_password` |
| `GRAFANA_ADMIN_PASSWORD` | Grafana admin interface password | `your_secure_grafana_password` |
| `SEQ_ADMIN_PASSWORD` | Seq logging admin password | `your_secure_seq_password` |

### Starting Services

After setting up your `.env` file:

```powershell
# Start core services
.\start-services.ps1 -Core

# Start all services
.\start-services.ps1 -All
```

## ⚠️ Security Notes

- **Never commit `.env` files** - they contain sensitive passwords
- **Use strong, unique passwords** for each service
- **Rotate passwords regularly** in production environments
- **Use secrets management** for production deployments

## 🔍 Troubleshooting

If you see environment variable errors:
1. Ensure your `.env` file exists and contains all required variables
2. Check that Docker Compose can read the `.env` file
3. Verify no spaces around the `=` in your `.env` file format
