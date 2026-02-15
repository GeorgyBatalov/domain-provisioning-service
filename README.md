# DomainProvisioningService

Control-plane orchestrator for custom domains with Let's Encrypt certificates for Tunnel2 (xtunnel).

## Overview

DomainProvisioningService is a **separate microservice** responsible for managing the full lifecycle of custom domains:
- DNS/CNAME validation
- ACME HTTP-01 challenge orchestration (Let's Encrypt)
- Certificate issuance and renewal
- Certificate deployment to HAProxy via HAProxyDomainAgent
- Rate limiting and retry/backoff logic

## Architecture

### Clean Architecture Layers

```
DomainProvisioningService.Domain        - Domain models, enums, interfaces
DomainProvisioningService.Application   - Business logic, workers, services
DomainProvisioningService.Infrastructure - External integrations (Cabinet API, HAProxy Agent, DNS, ACME)
DomainProvisioningService.Worker         - Host application (Background workers)
```

### Key Responsibilities

**What this service does:**
- ✅ Poll Cabinet API for CustomDomains in pending statuses
- ✅ Validate CNAME records (DNS check with backoff)
- ✅ HTTP preflight probe before ACME
- ✅ ACME certificate issuance (Certes library)
- ✅ Save certificates to CertificateStore (PostgreSQL)
- ✅ Deploy certificates to HAProxyDomainAgent (HTTP API)
- ✅ Renewal background job (30 days before expiration)
- ✅ Rate limiting (Let's Encrypt limits)
- ✅ Retry/backoff for failed operations

**What this service does NOT do:**
- ❌ CRUD operations on CustomDomain (Cabinet API owns this)
- ❌ Directly apply certificates to HAProxy (HAProxyDomainAgent does this)
- ❌ User authentication or authorization (Cabinet API)
- ❌ Routing traffic (ProxyEntry)

## Dependencies

- **Cabinet API** - Source of Truth for CustomDomain entities (read-only polling)
- **CertificateStore** - PostgreSQL database (read/write)
- **HAProxyDomainAgent** - Sidecar for applying certs to HAProxy (HTTP API)
- **Let's Encrypt** - ACME API for certificate issuance
- **DNS Resolvers** - 8.8.8.8, 1.1.1.1 for CNAME validation

## Technology Stack

- .NET 9.0
- ASP.NET Core Worker Service
- Certes (ACME client)
- DnsClient.NET (DNS resolution)
- Entity Framework Core 8.0 (CertificateStore access)
- Npgsql (PostgreSQL)
- Polly (Retry/resilience)
- Serilog (Structured logging)

## Configuration

See `src/DomainProvisioningService.Worker/appsettings.json` for configuration schema.

Key settings:
- `CabinetApi:BaseUrl` - Cabinet API endpoint
- `HAProxyAgent:BaseUrl` - HAProxyDomainAgent endpoint
- `CertificateStore:ConnectionString` - PostgreSQL connection
- `LetsEncrypt:DirectoryUrl` - Staging or Production ACME endpoint
- `Workers:*` - Polling intervals, timeouts

## Development

### Build

```bash
dotnet build src/DomainProvisioningService.sln
```

### Run locally

```bash
cd src/DomainProvisioningService.Worker
dotnet run
```

### Run tests

```bash
dotnet test src/DomainProvisioningService.Tests
```

### Docker

```bash
docker build -t domain-provisioning-service:latest .
docker run -p 8080:8080 domain-provisioning-service:latest
```

## Background Workers

### 1. DomainVerificationWorker
- **Trigger**: Every 30 seconds
- **Query**: CustomDomains with status = `PendingDns`
- **Action**: DNS CNAME check → update status to `PendingHttpProbe` or `Failed`

### 2. HttpProbeWorker
- **Trigger**: Every 30 seconds
- **Query**: CustomDomains with status = `PendingHttpProbe`
- **Action**: HTTP GET probe → update status to `Issuing` or `Failed`

### 3. AcmeIssuanceWorker
- **Trigger**: Every 60 seconds
- **Query**: CustomDomains with status = `Issuing`
- **Action**: ACME HTTP-01 challenge → save cert to CertificateStore → deploy via Agent → update status to `Active`

### 4. RenewalWorker
- **Trigger**: Every 6 hours
- **Query**: CertificateStore where `CertificateNotAfter < NOW() + 30 days`
- **Action**: Re-run ACME issuance → deploy new cert → update CertificateStore

## Flow Diagram

```
1. Cabinet API creates CustomDomain (status: PendingDns)
         ↓
2. DomainVerificationWorker polls Cabinet API
         ↓
3. DNS CNAME check (DnsClient.NET)
   ✅ CNAME verified → PendingHttpProbe
   ❌ Failed → Failed (lastErrorCode)
         ↓
4. HttpProbeWorker polls Cabinet API
         ↓
5. HTTP GET /{canonicalHost} probe
   ✅ Probe success → Issuing
   ❌ Failed → Failed
         ↓
6. AcmeIssuanceWorker polls Cabinet API
         ↓
7. ACME HTTP-01:
   - Create order (Certes)
   - Create challenge in CertificateStore (token, keyAuth)
   - LE validates via HAProxyDomainAgent (GET /.well-known/acme-challenge/{token})
   - Download certificate
   - Save to CertificateStore
   - POST /internal/v1/certificates/apply → HAProxyDomainAgent
   - Update CustomDomain (status: Active, certificateNotAfter)
         ↓
8. RenewalWorker (background job every 6h)
   - Find expiring certs
   - Re-run ACME process
   - Zero downtime (HAProxy Runtime API via Agent)
```

## Monitoring & Observability

- **Health checks**: `/health` endpoint
- **Metrics**: App.Metrics + Prometheus
  - `domain_verification_total{result="success|failed"}`
  - `acme_issuance_total{result="success|failed"}`
  - `renewal_total{result="success|failed"}`
  - `certificate_expiry_days` (gauge)
- **Logging**: Structured JSON logs (Serilog)

## See Also

- [CUSTOM_DOMAINS_SPEC.md](../CUSTOM_DOMAINS_SPEC.md) - Architecture specification
- [CUSTOM_DOMAINS_IMPLEMENTATION_PLAN.md](../CUSTOM_DOMAINS_IMPLEMENTATION_PLAN.md) - Implementation plan
- [LETSENCRYPT_SIDECAR_ARCHITECTURE.md](../LETSENCRYPT_SIDECAR_ARCHITECTURE.md) - HAProxy sidecar details
