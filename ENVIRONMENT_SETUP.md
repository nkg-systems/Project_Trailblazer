# Environment Setup Guide

## 🔐 Security Configuration

This project uses environment variables to securely manage sensitive configuration like passwords and API tokens.

### Quick Setup

#### 🚀 **Option 1: Automated Setup (Recommended)**
```powershell
# Generate secure .env file automatically
.\generate-secure-env.ps1
```

#### 🔧 **Option 2: Manual Setup**
1. **Copy the environment template:**
   ```bash
   cp .env.example .env
   ```

2. **Edit the .env file with your secure values:**
   ```bash
   # Use a text editor to update .env with real passwords
   notepad .env  # Windows
   ```

3. **Validate your setup:**
   ```powershell
   # Run security validation
   .\security-check.ps1
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

## 🛠️ Security Tools

### Password Generation
```powershell
# Generate new secure passwords for all services
.\generate-secure-env.ps1
```

### Security Validation
```powershell
# Quick security check
.\security-check.ps1

# Comprehensive security scan
.\security-check.ps1 -Comprehensive

# Check git history for leaked passwords
.\security-check.ps1 -GitHistory
```

### Password Rotation
```powershell
# Generate new passwords (existing .env will be backed up)
.\generate-secure-env.ps1

# Update running services with new passwords
.\start-services.ps1 -Stop
.\start-services.ps1 -All
```

## 🔍 Troubleshooting

If you see environment variable errors:
1. Ensure your `.env` file exists and contains all required variables
2. Check that Docker Compose can read the `.env` file
3. Verify no spaces around the `=` in your `.env` file format
4. Run `./security-check.ps1` to validate your configuration
