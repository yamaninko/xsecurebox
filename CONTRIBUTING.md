# Contributing

## Development setup

1. Copy `.env.example` to `.env` and fill every value.
2. `./scripts/start.sh` (generates a local TLS cert if missing).
3. Open https://localhost (self-signed; browser warning is expected).

Required tools: Docker Compose, .NET 9 SDK, Node.js 20+ (frontend only).

## Tests

```bash
dotnet test src/backend/SecureBox.sln
```

Frontend production build:

```bash
cd src/frontend && npm ci && npx ng build --configuration=production
```

## Pull requests

- Keep changes focused.
- Do not commit `.env`, TLS private keys, or `src/frontend/dist`.
- Do not add GitLab CI or vendor-specific registry names.
- New API behavior should have an automated test when practical.

## Code of conduct

Be respectful. Harassment is not acceptable.
