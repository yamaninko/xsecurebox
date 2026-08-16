# Secure Box

Encrypted key and certificate vault: ASP.NET 9 API + Angular portal + PostgreSQL + Redis.

**License:** [MIT](LICENSE)

## Features

- AES-256-GCM with RSA-OAEP wrapping of the data key (X.509 / PFX)
- JWT access tokens, rotating refresh cookie, optional Redis blacklist
- TOTP MFA, RBAC, permission policies
- Key create / retrieve / rotate / revoke, certificate upload
- PostgreSQL audit trail and key-access log
- Login and retrieve rate limits
- `/health/live` and `/health/ready`

## Quick start

Requirements: Docker Compose, OpenSSL.

```bash
cp .env.example .env
# Fill POSTGRES_PASSWORD, REDIS_PASSWORD, JWT_SECRET_KEY,
# ENCRYPTION_KEK (32 characters), and ADMIN_PASSWORD.

./scripts/start.sh
```

Then open **https://localhost** (self-signed certificate).

| Surface | URL |
|---|---|
| Portal | https://localhost |
| API (via nginx) | https://localhost/api |
| API direct | http://127.0.0.1:5002 |
| Health | http://127.0.0.1:5002/health/live |

First login: user `admin` and the `ADMIN_PASSWORD` you set. You will be asked to change the password and enable MFA.

Stop:

```bash
./scripts/stop.sh
```

## Configuration

All secrets come from the environment (see `.env.example`). There are no usable default passwords in Compose.

| Variable | Purpose |
|---|---|
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `REDIS_PASSWORD` | Redis `requirepass` |
| `JWT_SECRET_KEY` | HMAC key, at least 32 characters |
| `ENCRYPTION_KEK` | 32-byte UTF-8 or 32-byte base64 key-encryption key |
| `ADMIN_PASSWORD` | Seeded admin password (must change on first login) |

`ASPNETCORE_ENVIRONMENT=Production` refuses known development JWT/KEK/admin values.

## Tests

```bash
dotnet test src/backend/SecureBox.sln
```

## Docs

- [API](docs/03-api-endpoints.md)
- [Schema](docs/02-database-schema.md)
- [Security checklist](docs/08-security-checklist.md)
- [CI](CI-CD-README.md)
- [Vulnerability reports](SECURITY.md)

## Layout

```
secure-box/
├── src/backend/          # ASP.NET 9 solution
├── src/frontend/          # Angular portal
├── infrastructure/        # nginx, postgres init
├── kubernetes/            # optional k8s manifests
├── scripts/               # start, stop, backup, restore
└── docs/
```

## What this is not

No HSM/KMS, no email/webhooks, no immutable (WORM) audit store. The KEK in your environment is the root of encryption; back it up separately from the database.
