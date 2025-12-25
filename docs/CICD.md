# CI/CD Pipeline Guide

## Overview

The Field Operations Optimizer uses GitHub Actions for comprehensive CI/CD automation with three main workflows:

1. **CI Pipeline** (`ci.yml`) - Automated build, test, and quality checks
2. **Docker Build** (`docker-build.yml`) - Container image builds and publishing
3. **CD Pipeline** (`cd-deploy.yml`) - Environment-specific deployments

## Continuous Integration (CI)

### Automatic Triggers
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop`

### Pipeline Stages

#### 1. Build and Test
```bash
- Restore dependencies
- Build solution (Release configuration)
- Run 249 tests with code coverage
- Upload test results and coverage reports
```

#### 2. Code Quality
```bash
- Run dotnet format verification
- Check for compiler warnings (TreatWarningsAsErrors enforced)
```

#### 3. Security Scan
```bash
- Scan for vulnerable NuGet packages
- Check transitive dependencies
```

### Manual Trigger
```bash
gh workflow run ci.yml
```

### PR Integration
The CI pipeline automatically:
- Comments test results on pull requests
- Adds code coverage summary
- Shows pass/fail status for all checks

## Docker Build Pipeline

### Automatic Triggers
- Push to `main` branch
- Version tags (`v*.*.*`)
- Manual workflow dispatch

### Built Images
1. **API Image**: `ghcr.io/<username>/field-ops-optimizer-api`
2. **Web Image**: `ghcr.io/<username>/field-ops-optimizer-web`

### Features
- Multi-stage builds for minimal image size
- Multi-platform support (amd64, arm64)
- Security scanning with Trivy
- SLSA provenance attestation
- Build cache optimization

### Manual Trigger
```bash
gh workflow run docker-build.yml
```

### Using Built Images
```bash
# Pull images
docker pull ghcr.io/<username>/field-ops-optimizer-api:latest
docker pull ghcr.io/<username>/field-ops-optimizer-web:latest

# Run containers
docker run -p 8080:8080 ghcr.io/<username>/field-ops-optimizer-api:latest
docker run -p 80:80 ghcr.io/<username>/field-ops-optimizer-web:latest
```

## Continuous Deployment (CD)

### Environment Configuration

#### Development
- **URL**: https://dev.fieldopsoptimizer.com
- **Approval**: None required
- **Tests**: Smoke tests

#### Staging
- **URL**: https://staging.fieldopsoptimizer.com
- **Approval**: One reviewer
- **Tests**: Integration + smoke tests

#### Production
- **URL**: https://fieldopsoptimizer.com
- **Approval**: Two reviewers required
- **Tests**: Smoke tests + monitoring
- **Backup**: Pre-deployment backup created
- **Rollback**: Automatic on failure

### Deployment Steps

1. **Trigger Deployment**
```bash
# Using GitHub CLI
gh workflow run cd-deploy.yml \
  -f environment=staging \
  -f version=v1.2.3

# Or use GitHub UI: Actions → CD - Deploy → Run workflow
```

2. **Approve Deployment** (for staging/production)
   - Go to Actions tab
   - Find the deployment run
   - Click "Review deployments"
   - Approve or reject

3. **Monitor Deployment**
   - Check logs in Actions tab
   - Verify smoke tests pass
   - Monitor application health endpoints

### Rollback

If deployment fails:
- Automatic rollback is triggered
- Previous version is restored
- Notification sent to team

Manual rollback:
```bash
gh workflow run cd-deploy.yml \
  -f environment=production \
  -f version=<previous-version>
```

## Local Development

### Building Docker Images Locally

```bash
# Build API
docker build \
  -f src/FieldOpsOptimizer.Api/Dockerfile \
  -t field-ops-api:local \
  .

# Build Web
docker build \
  -f src/FieldOpsOptimizer.Web/Dockerfile \
  -t field-ops-web:local \
  .

# Test locally
docker run -p 8080:8080 field-ops-api:local
docker run -p 80:80 field-ops-web:local
```

### Running Tests Locally

```bash
# All tests
dotnet test

# With coverage
dotnet test \
  --collect:"XPlat Code Coverage" \
  --results-directory ./TestResults

# Release configuration (matches CI)
dotnet test --configuration Release
```

### Code Quality Checks

```bash
# Format check
dotnet format --verify-no-changes

# Security scan
dotnet list package --vulnerable --include-transitive
```

## GitHub Repository Setup

### 1. Enable GitHub Packages
- Go to repository Settings
- Enable Packages for container images

### 2. Configure Environments
- Settings → Environments → New environment
- Create: `development`, `staging`, `production`

**Development:**
- No protection rules

**Staging:**
- Required reviewers: 1
- Deployment branches: `main`

**Production:**
- Required reviewers: 2
- Deployment branches: `main` only
- Wait timer: 5 minutes (optional)

### 3. Branch Protection Rules
- Settings → Branches → Add rule
- Branch name pattern: `main`
- Enable:
  - Require pull request reviews (2 approvals)
  - Require status checks to pass (CI pipeline)
  - Require branches to be up to date

### 4. Secrets (Optional)
No additional secrets required - workflows use `GITHUB_TOKEN` automatically.

For external deployments, add:
```bash
# Settings → Secrets → Actions
DEPLOY_SSH_KEY       # SSH key for deployment servers
DEPLOY_HOST          # Deployment server hostname
KUBERNETES_CONFIG    # Kubeconfig for k8s deployments (if needed)
```

## Monitoring and Troubleshooting

### View Workflow Runs
```bash
# List recent runs
gh run list --workflow=ci.yml

# View specific run
gh run view <run-id>

# View logs
gh run view <run-id> --log
```

### Common Issues

#### Build Failures
1. Check build logs in Actions tab
2. Verify dependencies are up to date
3. Run build locally: `dotnet build --configuration Release`

#### Test Failures
1. Review test logs
2. Run tests locally: `dotnet test`
3. Check for environment-specific issues

#### Docker Build Failures
1. Verify Dockerfile syntax
2. Check build context with `.dockerignore`
3. Test build locally: `docker build -f <Dockerfile> .`

#### Deployment Failures
1. Check deployment logs
2. Verify smoke tests
3. Review environment configuration
4. Check application health endpoints

### Performance Optimization

**Build Cache:**
- CI pipeline uses dependency caching
- Docker builds use layer caching
- Reduces build time by ~50%

**Parallel Execution:**
- Tests run in parallel across three projects
- Security scans run in parallel with quality checks

**Artifact Management:**
- Test results retained for 90 days
- Docker images tagged with multiple identifiers
- Coverage reports available for analysis

## Best Practices

### Version Tagging
```bash
# Create version tag
git tag -a v1.2.3 -m "Release v1.2.3"
git push origin v1.2.3

# This triggers Docker build with versioned images
```

### Pull Request Workflow
1. Create feature branch from `develop`
2. Make changes and commit
3. Push branch and create PR to `develop`
4. CI runs automatically
5. Review test results and coverage
6. Address any failures
7. Get approval and merge
8. CI runs on `develop`
9. Create PR from `develop` to `main`
10. Deploy to production after merge

### Deployment Strategy
1. **Development**: Auto-deploy on every merge to `develop`
2. **Staging**: Manual deploy for release candidates
3. **Production**: Manual deploy with approval gates

## Metrics and Reporting

### Code Coverage
- Target: >80% coverage
- Tracked per project (Domain, Application, Infrastructure)
- Reported on every PR

### Build Metrics
- Build time: ~2 minutes
- Test execution: ~30 seconds
- Docker build: ~5 minutes

### Security Scanning
- NuGet vulnerabilities: 0 tolerance
- Container vulnerabilities: Critical/High flagged

## Additional Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Docker Best Practices](https://docs.docker.com/develop/dev-best-practices/)
- [.NET Testing Guide](https://learn.microsoft.com/en-us/dotnet/core/testing/)
