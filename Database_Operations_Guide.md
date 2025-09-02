# Database Operations Guide

## Overview

This guide covers database setup, backup, maintenance, and deployment procedures for the FieldOpsOptimizer production system using PostgreSQL.

## Prerequisites

### Software Requirements
- PostgreSQL 13+ (recommended: PostgreSQL 15)
- .NET 7.0 SDK
- Entity Framework Core Tools (`dotnet ef`)
- PowerShell 5.1+ (for scripts)

### Production Environment Setup
- PostgreSQL server with appropriate hardware specifications
- Database user with necessary permissions
- SSL/TLS certificates for secure connections
- Backup storage location (local or cloud)

## Database Setup

### 1. PostgreSQL Installation and Configuration

#### Ubuntu/Debian
```bash
# Install PostgreSQL
sudo apt update
sudo apt install postgresql postgresql-contrib

# Start and enable PostgreSQL
sudo systemctl start postgresql
sudo systemctl enable postgresql

# Create database and user
sudo -u postgres psql
CREATE DATABASE fieldopsoptimizer_prod;
CREATE USER fieldops WITH ENCRYPTED PASSWORD 'your_secure_password';
GRANT ALL PRIVILEGES ON DATABASE fieldopsoptimizer_prod TO fieldops;
ALTER USER fieldops CREATEDB; -- For migrations
\q
```

#### Windows
```powershell
# Download and install PostgreSQL from https://www.postgresql.org/download/windows/
# Or use Chocolatey
choco install postgresql

# Start PostgreSQL service
Start-Service postgresql-x64-15

# Connect and setup database
psql -U postgres
CREATE DATABASE fieldopsoptimizer_prod;
CREATE USER fieldops WITH ENCRYPTED PASSWORD 'your_secure_password';
GRANT ALL PRIVILEGES ON DATABASE fieldopsoptimizer_prod TO fieldops;
ALTER USER fieldops CREATEDB;
\q
```

### 2. Connection String Configuration

#### Environment Variables (Production)
```bash
export DB_HOST=localhost
export DB_NAME=fieldopsoptimizer_prod
export DB_USER=fieldops
export DB_PASSWORD=your_secure_password
export DB_PORT=5432
```

#### appsettings.Production.json
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=${DB_HOST};Database=${DB_NAME};Username=${DB_USER};Password=${DB_PASSWORD};Port=${DB_PORT:5432};Pooling=true;Connection Idle Lifetime=0;Command Timeout=30;SSL Mode=Require;"
  }
}
```

## Database Deployment

### Using PowerShell Script
```powershell
# Deploy database with migrations
.\scripts\Deploy-Database.ps1 -Environment Production -SeedData

# Deploy with custom connection string
.\scripts\Deploy-Database.ps1 -Environment Production -ConnectionString "Host=myserver;Database=fieldops;..." -SeedData

# Force deployment (skip confirmation prompts)
.\scripts\Deploy-Database.ps1 -Environment Production -Force -Verbose
```

### Manual Deployment
```bash
# Set environment
export ASPNETCORE_ENVIRONMENT=Production

# Navigate to project directory
cd /path/to/FieldOpsOptimizer

# Apply migrations
dotnet ef database update --project src/FieldOpsOptimizer.Infrastructure --startup-project src/FieldOpsOptimizer.Api

# Verify migration status
dotnet ef migrations list --project src/FieldOpsOptimizer.Infrastructure --startup-project src/FieldOpsOptimizer.Api
```

## Backup Procedures

### 1. Full Database Backup
```bash
# Create backup directory
mkdir -p /var/backups/fieldops

# Full backup with compression
pg_dump -h localhost -U fieldops -d fieldopsoptimizer_prod \
  --verbose --clean --no-acl --no-owner \
  --compress=9 --format=custom \
  --file="/var/backups/fieldops/fieldops_$(date +%Y%m%d_%H%M%S).backup"

# SQL format backup (for readability)
pg_dump -h localhost -U fieldops -d fieldopsoptimizer_prod \
  --verbose --clean --no-acl --no-owner \
  --file="/var/backups/fieldops/fieldops_$(date +%Y%m%d_%H%M%S).sql"
```

### 2. PowerShell Backup Script
```powershell
# Create backup
$timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$backupFile = "C:\Backups\FieldOps\fieldops_$timestamp.backup"

# Ensure backup directory exists
New-Item -ItemType Directory -Path "C:\Backups\FieldOps" -Force

# Run pg_dump
pg_dump --host=localhost --username=fieldops --dbname=fieldopsoptimizer_prod --verbose --clean --no-acl --no-owner --compress=9 --format=custom --file=$backupFile

Write-Host "Backup created: $backupFile"
```

### 3. Automated Backup Schedule

#### Linux Cron Job
```bash
# Edit crontab
crontab -e

# Add daily backup at 2 AM
0 2 * * * /path/to/backup-script.sh

# Add weekly full backup on Sunday at 1 AM
0 1 * * 0 /path/to/weekly-backup-script.sh
```

#### Windows Task Scheduler
```powershell
# Create scheduled task for daily backup
$action = New-ScheduledTaskAction -Execute "PowerShell.exe" -Argument "-File C:\Scripts\Backup-Database.ps1"
$trigger = New-ScheduledTaskTrigger -Daily -At "2:00AM"
$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
$principal = New-ScheduledTaskPrincipal -UserId "SYSTEM" -LogonType ServiceAccount

Register-ScheduledTask -TaskName "FieldOpsBackup" -Action $action -Trigger $trigger -Settings $settings -Principal $principal
```

## Restore Procedures

### 1. Full Database Restore
```bash
# Stop application services first
sudo systemctl stop fieldops-api

# Drop and recreate database
sudo -u postgres psql
DROP DATABASE IF EXISTS fieldopsoptimizer_prod;
CREATE DATABASE fieldopsoptimizer_prod;
GRANT ALL PRIVILEGES ON DATABASE fieldopsoptimizer_prod TO fieldops;
\q

# Restore from backup
pg_restore -h localhost -U fieldops -d fieldopsoptimizer_prod \
  --verbose --clean --no-acl --no-owner \
  /var/backups/fieldops/fieldops_20240101_020000.backup

# Restart application services
sudo systemctl start fieldops-api
```

### 2. Point-in-Time Recovery (if WAL archiving is configured)
```bash
# Stop PostgreSQL
sudo systemctl stop postgresql

# Restore base backup
tar -xzf /var/backups/fieldops/base_backup_20240101.tar.gz -C /var/lib/postgresql/15/main/

# Create recovery.conf
echo "restore_command = 'cp /var/backups/fieldops/wal/%f %p'" > /var/lib/postgresql/15/main/recovery.conf
echo "recovery_target_time = '2024-01-01 14:30:00'" >> /var/lib/postgresql/15/main/recovery.conf

# Start PostgreSQL
sudo systemctl start postgresql
```

## Maintenance Tasks

### 1. Regular Maintenance
```sql
-- Connect to database
\c fieldopsoptimizer_prod

-- Analyze table statistics
ANALYZE;

-- Vacuum tables (reclaim space)
VACUUM;

-- Reindex for performance
REINDEX DATABASE fieldopsoptimizer_prod;

-- Check database size
SELECT 
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
    pg_size_pretty(pg_relation_size(schemaname||'.'||tablename)) as table_size,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename) - pg_relation_size(schemaname||'.'||tablename)) as index_size
FROM pg_tables 
WHERE schemaname = 'public'
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC;
```

### 2. Performance Monitoring
```sql
-- Check slow queries
SELECT 
    query,
    mean_exec_time,
    calls,
    total_exec_time,
    rows,
    100.0 * shared_blks_hit / nullif(shared_blks_hit + shared_blks_read, 0) AS hit_percent
FROM pg_stat_statements 
ORDER BY mean_exec_time DESC 
LIMIT 10;

-- Check database activity
SELECT 
    datname,
    numbackends,
    xact_commit,
    xact_rollback,
    blks_read,
    blks_hit,
    tup_returned,
    tup_fetched,
    tup_inserted,
    tup_updated,
    tup_deleted
FROM pg_stat_database 
WHERE datname = 'fieldopsoptimizer_prod';

-- Check index usage
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch
FROM pg_stat_user_indexes 
ORDER BY idx_scan DESC;
```

### 3. Maintenance Script
```bash
#!/bin/bash
# maintenance.sh - Database maintenance script

DB_NAME="fieldopsoptimizer_prod"
DB_USER="fieldops"
DB_HOST="localhost"

echo "Starting database maintenance for $DB_NAME..."

# Vacuum and analyze
psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c "VACUUM ANALYZE;"

# Update statistics
psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c "ANALYZE;"

# Check for unused indexes
psql -h $DB_HOST -U $DB_USER -d $DB_NAME -c "
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan
FROM pg_stat_user_indexes 
WHERE idx_scan = 0
ORDER BY schemaname, tablename;
"

echo "Database maintenance completed."
```

## Migration Management

### 1. Creating New Migrations
```bash
# Create a new migration
dotnet ef migrations add <MigrationName> \
  --project src/FieldOpsOptimizer.Infrastructure \
  --startup-project src/FieldOpsOptimizer.Api \
  --output-dir Data/Migrations

# Review generated migration before applying
code src/FieldOpsOptimizer.Infrastructure/Data/Migrations/<timestamp>_<MigrationName>.cs
```

### 2. Rolling Back Migrations
```bash
# List applied migrations
dotnet ef migrations list --project src/FieldOpsOptimizer.Infrastructure --startup-project src/FieldOpsOptimizer.Api

# Rollback to specific migration
dotnet ef database update <PreviousMigrationName> \
  --project src/FieldOpsOptimizer.Infrastructure \
  --startup-project src/FieldOpsOptimizer.Api

# Remove migration files
dotnet ef migrations remove \
  --project src/FieldOpsOptimizer.Infrastructure \
  --startup-project src/FieldOpsOptimizer.Api
```

### 3. Production Migration Checklist
- [ ] Test migration on staging environment
- [ ] Create full database backup before migration
- [ ] Plan maintenance window
- [ ] Notify users of potential downtime
- [ ] Run migration with monitoring
- [ ] Verify application functionality
- [ ] Monitor performance post-migration

## Monitoring and Alerts

### 1. Database Health Endpoints
- **Full Health Check**: `GET /health`
- **Readiness Probe**: `GET /health/ready`
- **Liveness Probe**: `GET /health/live`

### 2. Key Metrics to Monitor
- Database connection count
- Query performance (slow queries)
- Database size growth
- Index usage statistics
- Backup success/failure
- Connection pool statistics

### 3. Alerting Thresholds
- Database connection failures
- Disk space usage > 80%
- Slow queries > 5 seconds
- Failed backup operations
- High CPU/Memory usage
- Deadlocks or lock timeouts

## Security Best Practices

### 1. Connection Security
- Use SSL/TLS for all database connections
- Implement connection string encryption
- Use least-privilege database users
- Regular password rotation
- Network security (VPN, private networks)

### 2. Access Control
```sql
-- Create application user with limited permissions
CREATE USER fieldops_app WITH ENCRYPTED PASSWORD 'app_password';

-- Grant only necessary permissions
GRANT CONNECT ON DATABASE fieldopsoptimizer_prod TO fieldops_app;
GRANT USAGE ON SCHEMA public TO fieldops_app;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO fieldops_app;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO fieldops_app;

-- For read-only reporting user
CREATE USER fieldops_readonly WITH ENCRYPTED PASSWORD 'readonly_password';
GRANT CONNECT ON DATABASE fieldopsoptimizer_prod TO fieldops_readonly;
GRANT USAGE ON SCHEMA public TO fieldops_readonly;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO fieldops_readonly;
```

### 3. Audit and Compliance
- Enable PostgreSQL logging
- Log all DDL changes
- Monitor privileged operations
- Regular security assessments
- Data retention policies

## Troubleshooting

### Common Issues

#### Connection Problems
```bash
# Check PostgreSQL status
sudo systemctl status postgresql

# Check configuration
sudo -u postgres psql -c "SHOW config_file;"

# Test connection
psql -h localhost -U fieldops -d fieldopsoptimizer_prod -c "SELECT version();"
```

#### Migration Issues
```bash
# Check migration history
dotnet ef migrations list --project src/FieldOpsOptimizer.Infrastructure --startup-project src/FieldOpsOptimizer.Api

# Generate SQL script for review
dotnet ef migrations script --project src/FieldOpsOptimizer.Infrastructure --startup-project src/FieldOpsOptimizer.Api --output migration.sql

# Force migration (use with caution)
dotnet ef database update --force --project src/FieldOpsOptimizer.Infrastructure --startup-project src/FieldOpsOptimizer.Api
```

#### Performance Issues
```sql
-- Check active connections
SELECT count(*) FROM pg_stat_activity WHERE state = 'active';

-- Check blocking queries
SELECT 
    blocked_locks.pid AS blocked_pid,
    blocked_activity.usename AS blocked_user,
    blocking_locks.pid AS blocking_pid,
    blocking_activity.usename AS blocking_user,
    blocked_activity.query AS blocked_statement,
    blocking_activity.query AS blocking_statement
FROM pg_catalog.pg_locks blocked_locks
JOIN pg_catalog.pg_stat_activity blocked_activity ON blocked_activity.pid = blocked_locks.pid
JOIN pg_catalog.pg_locks blocking_locks ON blocking_locks.locktype = blocked_locks.locktype
    AND blocking_locks.database IS NOT DISTINCT FROM blocked_locks.database
    AND blocking_locks.relation IS NOT DISTINCT FROM blocked_locks.relation
    AND blocking_locks.page IS NOT DISTINCT FROM blocked_locks.page
    AND blocking_locks.tuple IS NOT DISTINCT FROM blocked_locks.tuple
    AND blocking_locks.virtualxid IS NOT DISTINCT FROM blocked_locks.virtualxid
    AND blocking_locks.transactionid IS NOT DISTINCT FROM blocked_locks.transactionid
    AND blocking_locks.classid IS NOT DISTINCT FROM blocked_locks.classid
    AND blocking_locks.objid IS NOT DISTINCT FROM blocked_locks.objid
    AND blocking_locks.objsubid IS NOT DISTINCT FROM blocked_locks.objsubid
    AND blocking_locks.pid != blocked_locks.pid
JOIN pg_catalog.pg_stat_activity blocking_activity ON blocking_activity.pid = blocking_locks.pid
WHERE NOT blocked_locks.granted;
```

## Disaster Recovery

### 1. Recovery Procedures
1. **Assess the situation**: Determine the scope of data loss
2. **Stop application**: Prevent further data corruption
3. **Restore from backup**: Use most recent valid backup
4. **Apply incremental changes**: If possible, apply changes since backup
5. **Verify data integrity**: Run consistency checks
6. **Restart application**: Monitor for issues

### 2. Recovery Time Objectives (RTO) / Recovery Point Objectives (RPO)
- **RTO Target**: < 4 hours for complete system recovery
- **RPO Target**: < 1 hour of data loss maximum
- **Backup Frequency**: Daily full backups, hourly incremental
- **Backup Retention**: 30 days local, 90 days archived

### 3. Testing Recovery Procedures
```bash
# Monthly recovery test
# 1. Create test environment
# 2. Restore latest backup
# 3. Verify data integrity
# 4. Test application functionality
# 5. Document any issues
```

## Environment-Specific Configurations

### Development
- Connection pooling: enabled
- Query logging: enabled
- Detailed errors: enabled
- Auto-migrations: optional
- Seeding: enabled

### Staging
- Connection pooling: enabled
- Query logging: limited
- Detailed errors: disabled
- Auto-migrations: disabled
- Seeding: disabled

### Production
- Connection pooling: enabled
- Query logging: errors only
- Detailed errors: disabled
- Auto-migrations: disabled
- Seeding: disabled
- SSL: required
- Connection encryption: required

## Performance Optimization

### 1. Index Management
```sql
-- Monitor index usage
SELECT 
    schemaname,
    tablename,
    indexname,
    idx_scan,
    idx_tup_read,
    idx_tup_fetch,
    pg_size_pretty(pg_relation_size(indexname::regclass)) as index_size
FROM pg_stat_user_indexes 
ORDER BY idx_scan DESC;

-- Find missing indexes
SELECT 
    schemaname,
    tablename,
    seq_scan,
    seq_tup_read,
    idx_scan,
    idx_tup_fetch,
    seq_tup_read / seq_scan AS avg_seq_read
FROM pg_stat_user_tables 
WHERE seq_scan > 0 AND seq_tup_read / seq_scan > 1000
ORDER BY seq_tup_read DESC;
```

### 2. Connection Pool Tuning
```bash
# PostgreSQL configuration (postgresql.conf)
max_connections = 200
shared_buffers = 256MB
effective_cache_size = 1GB
work_mem = 4MB
maintenance_work_mem = 64MB

# Application connection pool (.NET)
Connection Pooling=true;
Connection Idle Lifetime=0;
Connection Pruning Interval=10;
Maximum Pool Size=100;
Minimum Pool Size=5;
```

## Compliance and Auditing

### 1. Enable Audit Logging
```sql
-- Enable statement logging
ALTER SYSTEM SET log_statement = 'all';
ALTER SYSTEM SET log_duration = 'on';
ALTER SYSTEM SET log_connections = 'on';
ALTER SYSTEM SET log_disconnections = 'on';

-- Reload configuration
SELECT pg_reload_conf();
```

### 2. Data Retention
```sql
-- Example: Clean up old logs (implement based on requirements)
DELETE FROM audit_logs WHERE created_at < NOW() - INTERVAL '90 days';

-- Archive old completed jobs
INSERT INTO archived_jobs SELECT * FROM service_jobs 
WHERE status = 'Completed' AND completed_at < NOW() - INTERVAL '1 year';

DELETE FROM service_jobs 
WHERE status = 'Completed' AND completed_at < NOW() - INTERVAL '1 year';
```

## Emergency Contacts and Procedures

### Escalation Matrix
1. **Level 1**: Application logs, basic connectivity checks
2. **Level 2**: Database administrator, performance analysis
3. **Level 3**: Infrastructure team, hardware/network issues
4. **Level 4**: Vendor support, critical system failures

### Emergency Response
1. **Immediate**: Stop application to prevent data corruption
2. **Assessment**: Determine scope and impact
3. **Communication**: Notify stakeholders and users
4. **Recovery**: Execute appropriate recovery procedures
5. **Post-incident**: Document lessons learned and improve procedures

This guide provides comprehensive procedures for managing the FieldOpsOptimizer database in production environments. Regular review and testing of these procedures ensures reliable database operations.
