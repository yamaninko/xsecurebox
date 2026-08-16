# CI/CD

Secure Box uses **GitHub Actions**.

```
.github/workflows/
  build-and-push.yml   # test, then build and push images
  deploy-to-k8s.yml    # deploy after a successful build
```

## Required GitHub secrets

| Secret | Purpose |
|---|---|
| `REGISTRY_URL` | Container registry host (GHCR, Docker Hub, or Harbor) |
| `REGISTRY_USERNAME` | Registry user |
| `REGISTRY_TOKEN` | Registry token |
| `KUBECONFIG` | Base64-encoded kubeconfig (deploy workflow) |

## Local images

```bash
docker compose -f docker-compose.yml up -d
```

## Kubernetes images

Set `REGISTRY_URL` and apply manifests. Image placeholders use `${REGISTRY_URL}/securebox/api` and `${REGISTRY_URL}/securebox/portal`.
