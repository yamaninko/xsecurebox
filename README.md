# XSecureBox — Open Source Self-Hosted Secrets Manager & Key Vault

[![License: MIT](https://img.shields.io/badge/License-MIT-22c55e.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9-512BD4.svg)](https://dotnet.microsoft.com/)
[![Angular](https://img.shields.io/badge/Angular-19-DD0031.svg)](https://angular.dev/)
[![Docker](https://img.shields.io/badge/Docker-Compose-2496ED.svg)](https://docs.docker.com/compose/)
[![GitHub stars](https://img.shields.io/github/stars/yamaninko/xsecurebox?style=social)](https://github.com/yamaninko/xsecurebox)

<p align="center">
  <img src="docs/assets/xsecurebox-banner.jpg" alt="XSecureBox — self-hosted secrets manager and certificate vault" width="100%">
</p>

**XSecureBox** is a **self-hosted secrets manager** and **certificate-backed key vault**. Store API keys, passwords, certificates, and other secrets encrypted at rest. Run it on your own Docker host — no SaaS, no vendor lock-in.

**Keywords:** open source secrets manager · self-hosted key vault · certificate encryption · AES-256-GCM · TOTP MFA · ASP.NET 9 · Angular · Docker Compose

[Quick start](#quick-start) · [How encryption works](#how-encryption-works) · [Security](#security-model) · [Türkçe](#türkçe)

---

## What is XSecureBox?

XSecureBox is an **open-source alternative** for teams that want a **private vault** instead of a cloud password manager.

| You need | XSecureBox does |
|---|---|
| Store API keys and passwords encrypted | Yes — AES-256-GCM, DEK wrapped with an RSA certificate |
| Upload X.509 / PFX certificates | Yes |
| Retrieve a secret with an audit trail | Yes — password re-entry for people, scoped tokens for services |
| MFA and roles | Yes — TOTP + RBAC |
| Run it yourself | Yes — Docker Compose or Kubernetes |
| Hardware HSM / KMS | Not yet — your env KEK is the root of trust |

It is **not** a drop-in HashiCorp Vault clone. It is a smaller, full-stack vault: API + Angular portal + PostgreSQL + Redis.

---

## Features

- **Envelope encryption** — random AES-256-GCM data key per secret, wrapped with RSA-OAEP (SHA-256) from your certificate
- **Certificate lifecycle** — upload PEM/PFX, revoke, expire (background sweep)
- **Key lifecycle** — create, list, retrieve, rotate, revoke, tags, environments (DEV/TEST/UAT/PROD)
- **Auth** — JWT access token in memory, **httpOnly refresh cookie**, login lockout, TOTP MFA
- **Authorization** — Admin / Client / Service roles and permission policies; API clients use OAuth2 client-credentials + scopes (`keys:retrieve`)
- **Audit** — every create/retrieve/revoke written to PostgreSQL
- **Ops** — `/health/live`, `/health/ready`, rate limits, `scripts/backup.sh` / `restore.sh`
- **No default passwords in the repo** — Compose fails if `.env` is empty

---

## Quick start

**Requirements:** Docker Compose, OpenSSL.

```bash
git clone https://github.com/yamaninko/xsecurebox.git
cd xsecurebox
cp .env.example .env
```

Fill every value in `.env`:

```bash
openssl rand -base64 24   # POSTGRES_PASSWORD / REDIS_PASSWORD / ADMIN_PASSWORD
openssl rand -base64 48   # JWT_SECRET_KEY  (32+ characters)
# ENCRYPTION_KEK must be exactly 32 UTF-8 characters, or 32-byte base64
```

```bash
./scripts/start.sh
```

Open **https://localhost** (self-signed certificate — accept the browser warning).

| Surface | URL |
|---|---|
| Portal | https://localhost |
| API | https://localhost/api |
| Health | http://127.0.0.1:5002/health/live |

First login: user `admin` and your `ADMIN_PASSWORD`. You will change the password and enable TOTP MFA.

Stop: `./scripts/stop.sh`

---

## Private Ethereum integrity (institutional VMs)

XSecureBox can seal every secret on a **private Ethereum VM** you control. The chain never sees plaintext or the KEK — only:

- `keccak256(ciphertext || iv || tag)`
- algorithm id (`AES256-GCM+RSA-OAEP-SHA256`)
- this installation’s `systemId`

**Create** registers the hash in `SecureBoxRegistry`. **Retrieve** recomputes the hash and asks the ETH node(s) to confirm before decryption. Tampering with the database ciphertext fails verification.

Local Docker starts one Anvil node (`eth-1`, chain id `4242`). In the plant, point `Ethereum__RpcUrls__0`, `__1`, … at your dockerized geth/besu validators and set `Ethereum__Quorum` to how many must agree.

Contract: [`contracts/SecureBoxRegistry.sol`](contracts/SecureBoxRegistry.sol)

---

## How encryption works

```
plaintext
   │
   ├─ random 256-bit DEK
   ├─ AES-256-GCM  →  ciphertext + IV + tag   (stored)
   └─ RSA-OAEP wrap(DEK, cert public key)     (stored)
```

The certificate **private key** is stored only if you upload a PFX/PEM with a key, encrypted with `ENCRYPTION_KEK`. Lose the KEK and you cannot decrypt existing secrets. Back the KEK up **separately** from the database.

---

## Security model

- Production (`ASPNETCORE_ENVIRONMENT=Production`) **refuses** known development JWT/KEK/admin values
- Refresh token is **not** returned in JSON — only an `sb_refresh` httpOnly cookie
- Retrieve requires the user's password (humans) or `keys:read` / `keys:retrieve` scope (API clients)
- Report vulnerabilities privately — see [SECURITY.md](SECURITY.md)

---

## Configuration

| Variable | Purpose |
|---|---|
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `REDIS_PASSWORD` | Redis `requirepass` |
| `JWT_SECRET_KEY` | HMAC signing key, ≥ 32 characters |
| `ENCRYPTION_KEK` | 32-byte UTF-8 **or** 32-byte base64 |
| `ADMIN_PASSWORD` | Seeded admin password (must change on first login) |

---

## Tech stack

- **Backend:** ASP.NET 9, EF Core, PostgreSQL, Redis, JWT, BCrypt
- **Frontend:** Angular, Angular Material
- **Edge:** Nginx (TLS 1.2 / 1.3), Docker Compose
- **Optional:** Kubernetes manifests under `kubernetes/`

---

## Tests

```bash
dotnet test src/backend/SecureBox.sln
```

---

## Documentation

- [API endpoints](docs/03-api-endpoints.md)
- [Database schema](docs/02-database-schema.md)
- [Security checklist](docs/08-security-checklist.md)
- [CI/CD](CI-CD-README.md)
- [Contributing](CONTRIBUTING.md)

---

## Who is this for?

- Teams that need a **self-hosted secrets manager** on their own VPS or lab
- Developers who want an **open source key vault** they can read and fork
- Operators who prefer PostgreSQL they already know over a black-box SaaS

---

## License

[MIT](LICENSE) — free to use, modify, and ship.

---

## Türkçe

**XSecureBox**, kendi sunucunuzda çalışan **açık kaynak secrets manager** ve **anahtar / sertifika kasası**dır. API anahtarları, parolalar ve sertifikalar AES-256-GCM ile şifrelenir; DEK, yüklediğiniz RSA sertifikasıyla sarılarak saklanır.

```bash
git clone https://github.com/yamaninko/xsecurebox.git
cd xsecurebox && cp .env.example .env   # değerleri doldurun
./scripts/start.sh
```

Portal: **https://localhost** — kullanıcı `admin`, şifre `.env` içindeki `ADMIN_PASSWORD`.

Arama terimleri: açık kaynak secrets manager, self-hosted key vault, sertifika şifreleme, kendi sunucunda parola kasası, Docker secrets yönetimi.
