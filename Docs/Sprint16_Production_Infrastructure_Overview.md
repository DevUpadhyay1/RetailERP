# RetailERP Production Infrastructure Overview (Post Sprint 16)

Date: March 20, 2026
Project: RetailERP
Scope: Server, database, networking, cloud infra, CI/CD, security, containers, CDN, monitoring, backup/recovery.

## 1) Executive Recommendation (Best Fit)

For your current stack (.NET 8 + SQL Server + Redis + Docker + GitHub Actions), the best professional setup is:

- Cloud: AWS (Mumbai region `ap-south-1` for India latency)
- App runtime: Amazon ECS with Fargate
- Database: Amazon RDS for SQL Server (Multi-AZ)
- Cache: Amazon ElastiCache for Redis
- Edge + security: CloudFront + AWS WAF + ACM TLS
- Monitoring: CloudWatch Logs + CloudWatch alarms
- Secrets: AWS Secrets Manager
- Backups: RDS PITR + AWS Backup policies + cross-region backup copy

Reason:
- Matches your existing Sprint 16 Docker + CI/CD pattern.
- Removes heavy server management.
- Scales for growth without redesigning your app.

## 2) What Sprint 16 Already Gives You

You already implemented a strong DevOps foundation:

- Test automation:
  - `RetailERP.Tests` (JWT + item onboarding tests)
- CI:
  - `.github/workflows/ci.yml` (restore/build/test/coverage/docker build check)
- CD:
  - `.github/workflows/deploy.yml` (manual deploy, build/push image, SSH deploy)
- Containers:
  - `Dockerfile`
  - `docker-compose.yml`
  - `docker-compose.prod.yml`
- Deploy runbook artifacts:
  - `deploy/.env.production.template`
  - `scripts/deploy_production.ps1`

So Sprint 16 is not “just Docker”; it is your release reliability layer.

## 3) Target Production Architecture (High Functionality)

1. Users -> CloudFront (global CDN)
2. CloudFront -> Application Load Balancer (HTTPS)
3. ALB -> ECS Fargate service (RetailERP app containers)
4. App -> RDS SQL Server (private subnet, Multi-AZ)
5. App -> ElastiCache Redis (private subnet)
6. Logs/metrics -> CloudWatch
7. Secrets -> Secrets Manager
8. Backups -> RDS automated backups + AWS Backup + cross-region copy

## 4) Networking Design

- VPC with 3 subnet layers:
  - Public subnets: ALB only
  - Private app subnets: ECS tasks
  - Private data subnets: RDS + Redis
- Security groups:
  - Allow internet only to ALB 443
  - Allow ALB to app (8080)
  - Allow app to SQL Server (1433) and Redis (6379)
  - No direct public access to DB/Redis
- DNS + TLS:
  - Route53 + ACM certificate for domain HTTPS

## 5) CI/CD Pipeline (Your Implemented + Recommended Upgrade)

Current (implemented in Sprint 16):
- Push/PR triggers CI tests and docker build validation
- Manual deploy workflow builds image to GHCR and deploys using SSH + compose

Professional next upgrade:
- Keep current flow for immediate launch (good for MVP/early production)
- Add environment protections:
  - GitHub Environments (staging/prod approvals)
  - Branch protection + required status checks
- Mid-term:
  - Move from SSH compose deploy to ECS rolling deployment
  - Use image digest pinning in deployment

## 6) Security Baseline

- Keep secrets out of code (`.env.production` / GitHub secrets / secret manager)
- Use per-environment secrets with strict access
- Enforce TLS end-to-end
- Add WAF managed rules (OWASP set)
- Principle of least privilege IAM roles
- Enable audit trails (CloudTrail + app audit logs)
- Keep dependency and container image scanning in pipeline

## 7) Containers and Runtime

- Your Dockerfile is production-ready as multi-stage build.
- Use container health checks and rolling deployments.
- Keep app stateless; persist only via SQL/Redis/object storage.
- Store file uploads in durable storage (S3) in later hardening phase.

## 8) Monitoring and Logging

- Application:
  - Structured logs (Serilog already present in app)
- Platform:
  - Container logs to CloudWatch Logs
  - ECS/ALB/RDS/Redis metrics + alerting thresholds
- Alerts:
  - High error rate
  - High response latency
  - CPU/memory saturation
  - DB storage nearing limit
  - Failed login spikes / WAF blocks anomaly

## 9) Backup and Recovery Strategy

- Database:
  - RDS automated backups + Point-in-Time Restore (PITR)
  - Multi-AZ for HA (availability)
  - Cross-region backup copy for disaster recovery
- Files:
  - If using local volumes today, move to object storage with lifecycle + versioning
- Recovery drills:
  - Monthly restore test into staging
  - Define RPO/RTO targets and test them

## 10) Server Sizing Starter (Practical)

Production starter (normal SME load):
- ECS tasks: 2 tasks minimum, each 1 vCPU / 2 GB RAM
- ALB: 1
- RDS SQL Server: db.m6i.large (or equivalent) Multi-AZ
- Redis: cache.t4g.small (or equivalent)

Scale triggers:
- CPU > 65% sustained
- p95 latency > 400ms
- DB CPU > 70% sustained
- connection pool pressure

## 11) Final Summary

Your project after Sprint 16 is now “deployment-capable”, not just code-complete.

Best next professional path:
1. Launch with your current Sprint 16 pipeline.
2. Deploy on AWS managed architecture (ECS + RDS + Redis + ALB + CloudFront + WAF).
3. Add stronger observability, backup drills, and secret-management hardening.
4. Then optimize cost/performance once real traffic data arrives.

