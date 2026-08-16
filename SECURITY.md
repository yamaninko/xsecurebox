# Security policy

## Reporting a vulnerability

Do **not** open a public GitHub issue for security problems.

Email the maintainers privately, or use GitHub **Security Advisories**
(Security → Report a vulnerability).

Please include:

- Affected version / commit
- Reproduction steps
- Impact (auth bypass, secret disclosure, etc.)

We aim to acknowledge reports within 7 days.

## Scope

This is a secrets-management application. High-severity examples:

- Unauthorized retrieve of encrypted keys
- JWT or refresh-token bypass
- Default or hardcoded credentials in a release
- Path or SSRF that exposes private keys

## Hardening notes for operators

- Never commit `.env`. Copy `.env.example` and generate unique values.
- Set `JWT_SECRET_KEY` (≥32 chars), `ENCRYPTION_KEK` (exactly 32 bytes or 32-byte base64), `ADMIN_PASSWORD`, `POSTGRES_PASSWORD`, and `REDIS_PASSWORD`.
- Production (`ASPNETCORE_ENVIRONMENT=Production`) refuses known development secrets.
- Local TLS is self-signed. Use a real certificate in production.
- The application KEK wraps certificate private keys. Losing it makes stored secrets unrecoverable.
