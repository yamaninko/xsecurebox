# 🚀 Secure Box CI/CD Guide

Bu proje **GitLab CI/CD** ve **GitHub Actions** için tam CI/CD pipeline'larına sahiptir.

## 📁 Dosya Yapısı

```
secure-box/
├── .gitlab-ci.yml                    # GitLab CI/CD Pipeline
├── .github/
│   └── workflows/
│       ├── build-and-push.yml        # GitHub: Build & Push
│       └── deploy-to-k8s.yml         # GitHub: Deploy to K8s
├── kubernetes/                        # Kubernetes manifests
│   ├── base/                         # Base resources
│   └── overlays/                     # Environment overlays
├── scripts/
│   ├── deploy-to-k8s.sh             # Otomatik deployment
│   └── cleanup-k8s.sh               # Cleanup script
└── docs/
    ├── GITLAB-CI-CD.md              # GitLab CI/CD dökümanı
    ├── CI-CD-COMPARISON.md          # Platform karşılaştırması
    └── workflows/README.md          # GitHub Actions dökümanı
```

## 🎯 Hızlı Başlangıç

### Seçenek 1: GitLab CI/CD

#### 1. GitLab Variables Ayarla

**Settings > CI/CD > Variables**

```bash
# Harbor Registry
GTECH_REPO_URL          = harbor.example.com
GTECH_REPO_USERNAME     = robot$securebox
GTECH_REPO_TOKEN        = <your-token>

# Kubernetes
KUBECONFIG_DEV          = <base64-encoded-kubeconfig>
KUBECONFIG_PROD         = <base64-encoded-kubeconfig>
```

#### 2. Pipeline'ı Tetikle

```bash
# Otomatik tetikleme
git push origin main

# Pipeline stages:
# 1. build:api, build:portal
# 2. test:api, test:portal
# 3. push:api, push:portal
# 4. deploy:dev (manuel), deploy:production (manuel)
# 5. cleanup:docker
```

#### 3. Manuel Deploy

GitLab UI > CI/CD > Pipelines > Select Pipeline > `deploy:production` > Play ▶️

### Seçenek 2: GitHub Actions

#### 1. GitHub Secrets Ayarla

**Settings > Secrets and variables > Actions**

```bash
# Harbor Registry
GTECH_REPO_URL          = harbor.example.com
GTECH_REPO_USERNAME     = robot$securebox
GTECH_REPO_TOKEN        = <your-token>

# Kubernetes
KUBECONFIG              = <base64-encoded-kubeconfig>
```

#### 2. Pipeline'ı Tetikle

```bash
# Otomatik tetikleme
git push origin main

# Workflows:
# 1. Build and Push to Harbor (otomatik)
# 2. Deploy to Kubernetes (workflow_run trigger veya manuel)
```

#### 3. Manuel Deploy

GitHub UI > Actions > Deploy to Kubernetes > Run workflow > Select environment

## 🔄 Pipeline Akışı

### GitLab CI/CD

```
┌─────────┐
│  COMMIT │
└────┬────┘
     │
┌────▼────┐
│  BUILD  │ ─── Docker images (API + Portal)
└────┬────┘
     │
┌────▼────┐
│  TEST   │ ─── Unit tests + Coverage
└────┬────┘
     │
┌────▼────┐
│  PUSH   │ ─── Harbor Registry (main/develop)
└────┬────┘
     │
┌────▼────┐
│ DEPLOY  │ ─── Kubernetes (manuel approval)
└────┬────┘
     │
┌────▼────┐
│ CLEANUP │ ─── Docker pruning
└─────────┘
```

### GitHub Actions

```
┌─────────┐
│  COMMIT │
└────┬────┘
     │
┌────▼────────────────┐
│ Build and Push      │
│ ├─ Build API        │
│ ├─ Build Portal     │
│ └─ Push to Harbor   │
└────┬────────────────┘
     │
┌────▼────────────────┐
│ Deploy to K8s       │ (otomatik veya manuel)
│ ├─ Setup K8s        │
│ ├─ Deploy DB        │
│ ├─ Deploy App       │
│ └─ Smoke Tests      │
└─────────────────────┘
```

## 📋 Pipeline Features

### GitLab CI/CD

✅ **5 Stage Pipeline**
- Build (API + Portal)
- Test (Unit tests + Coverage)
- Push (Harbor registry)
- Deploy (Dev + Production)
- Cleanup (Docker pruning)

✅ **Features**
- Built-in test reporting
- Coverage tracking
- Environment management
- Manual approval
- Rollback support
- Artifact caching

### GitHub Actions

✅ **2 Workflow System**
- Build and Push (otomatik)
- Deploy to K8s (triggered/manuel)

✅ **Features**
- GitHub Actions marketplace
- Workflow templates
- Environment protection
- Build caching
- Auto rollback on failure

## 🔐 Secrets Hazırlama

### Harbor Robot Account

1. Harbor UI'da login
2. Project > Robot Accounts > New
3. Name: `securebox-ci`
4. Permissions: Pull & Push
5. Token'ı kopyala

```bash
GTECH_REPO_URL=harbor.example.com
GTECH_REPO_USERNAME=robot$securebox-ci
GTECH_REPO_TOKEN=<generated-token>
```

### Kubeconfig

```bash
# Base64 encode
cat ~/.kube/config | base64 -w 0  # Linux
cat ~/.kube/config | base64       # macOS

# GitLab: Variable olarak ekle (type: File)
# GitHub: Secret olarak ekle (type: Secret)
```

## 🚀 Deployment

### Otomatik Script ile (Önerilen)

```bash
# Harbor credentials ayarla
export GTECH_REPO_URL="harbor.example.com"
export GTECH_REPO_USERNAME="robot\$securebox"
export GTECH_REPO_TOKEN="your-token"

# Deploy
./scripts/deploy-to-k8s.sh

# Output:
# ✅ Namespace created
# ✅ Harbor secret created
# ✅ Databases deployed
# ✅ Application deployed
# ✅ Smoke tests passed
# 🌐 Application URL: http://<loadbalancer-ip>
```

### Manuel Deployment

```bash
# 1. Namespace oluştur
kubectl apply -f kubernetes/base/namespace.yaml

# 2. Harbor secret oluştur
kubectl create secret docker-registry harbor-registry-secret \
  --docker-server=$GTECH_REPO_URL \
  --docker-username=$GTECH_REPO_USERNAME \
  --docker-password=$GTECH_REPO_TOKEN \
  --namespace=securebox

# 3. Database'leri deploy et
kubectl apply -f kubernetes/base/postgres-deployment.yaml
kubectl apply -f kubernetes/base/mongodb-deployment.yaml
kubectl apply -f kubernetes/base/redis-deployment.yaml
kubectl apply -f kubernetes/base/rabbitmq-deployment.yaml

# 4. Application'ı deploy et
sed -i "s|\${GTECH_REPO_URL}|$GTECH_REPO_URL|g" kubernetes/base/api-deployment.yaml
sed -i "s|\${GTECH_REPO_URL}|$GTECH_REPO_URL|g" kubernetes/base/portal-deployment.yaml

kubectl apply -f kubernetes/base/api-deployment.yaml
kubectl apply -f kubernetes/base/portal-deployment.yaml
kubectl apply -f kubernetes/base/nginx-deployment.yaml

# 5. Durumu kontrol et
kubectl get pods -n securebox
kubectl get svc -n securebox
```

## 📊 Monitoring

### Pipeline Status

**GitLab:**
```bash
# Pipeline listesi
glab ci list

# Pipeline detayları
glab ci view <pipeline-id>

# Logs
glab ci trace <job-id>
```

**GitHub:**
```bash
# Pipeline listesi
gh run list

# Pipeline detayları
gh run view <run-id>

# Logs
gh run view <run-id> --log
```

### Kubernetes

```bash
# Pod durumu
kubectl get pods -n securebox

# Logs
kubectl logs -f -l app=securebox-api -n securebox

# Service status
kubectl get svc -n securebox

# HPA status
kubectl get hpa -n securebox
```

## 🧪 Test

### Local Pipeline Test

**GitLab:**
```bash
# GitLab Runner ile local test
gitlab-runner exec docker build:api
gitlab-runner exec docker test:api
```

**GitHub:**
```bash
# Act ile local test (GitHub Actions emulator)
act -j build-api
act -j deploy
```

### Docker Build Test

```bash
# API
cd src/backend/SecureBox.API
docker build -t test-api -f Dockerfile ..

# Portal
cd src/frontend
docker build -t test-portal .
```

### Kubernetes Manifest Test

```bash
# Syntax check
kubectl apply --dry-run=client -f kubernetes/base/

# Validation
kubectl kustomize kubernetes/base/ | kubectl apply --dry-run=client -f -
```

## 📚 Detaylı Dokümantasyon

| Dokümantasyon | Açıklama |
|---------------|----------|
| [GITLAB-CI-CD.md](docs/GITLAB-CI-CD.md) | GitLab CI/CD tam rehber |
| [workflows/README.md](.github/workflows/README.md) | GitHub Actions rehber |
| [CI-CD-COMPARISON.md](docs/CI-CD-COMPARISON.md) | Platform karşılaştırması |
| [KUBERNETES-DEPLOYMENT.md](KUBERNETES-DEPLOYMENT.md) | K8s deployment rehber |
| [kubernetes/README.md](kubernetes/README.md) | K8s manifests rehber |

## 🎯 Hangi Platform?

### GitLab CI/CD Seç Eğer:

✅ GitLab zaten kullanıyorsanız  
✅ Tek platform çözümü istiyorsanız  
✅ Built-in test reporting önemliyse  
✅ Self-hosted runner tercih ediyorsanız  
✅ Enterprise features gerekiyorsa  

### GitHub Actions Seç Eğer:

✅ GitHub zaten kullanıyorsanız  
✅ Marketplace actions'lardan faydalanmak istiyorsanız  
✅ Open source proje (unlimited free)  
✅ Daha fazla free tier minutes (2000 vs 400)  
✅ Community support önemliyse  

**Her iki platform için de full support! 🎉**

## 🐛 Troubleshooting

### Build Hatası

```bash
# Logs kontrol
# GitLab: glab ci trace <job-id>
# GitHub: gh run view <run-id> --log

# Local test
docker build --no-cache -t test-api .
```

### Push Hatası

```bash
# Credentials test
docker login $GTECH_REPO_URL

# Harbor UI > Robot Accounts > Permissions kontrol
```

### Deploy Hatası

```bash
# Kubeconfig test
kubectl cluster-info

# Pod status
kubectl get pods -n securebox
kubectl describe pod <pod-name> -n securebox

# Logs
kubectl logs <pod-name> -n securebox
```

### Image Pull Hatası

```bash
# Secret kontrol
kubectl get secret harbor-registry-secret -n securebox

# Secret yenile
kubectl delete secret harbor-registry-secret -n securebox
kubectl create secret docker-registry harbor-registry-secret \
  --docker-server=$GTECH_REPO_URL \
  --docker-username=$GTECH_REPO_USERNAME \
  --docker-password=$GTECH_REPO_TOKEN \
  --namespace=securebox
```

## 🧹 Cleanup

```bash
# Otomatik cleanup
./scripts/cleanup-k8s.sh

# Manuel cleanup
kubectl delete namespace securebox

# Sadece app (database'ler kalır)
kubectl delete deployment securebox-api securebox-portal nginx -n securebox
```

## 📈 Best Practices

1. ✅ **Protected branches** - main ve develop için
2. ✅ **Manual approval** - Production deployment için
3. ✅ **Rollback strategy** - Her deployment için hazır
4. ✅ **Smoke tests** - Deploy sonrası health check
5. ✅ **Resource cleanup** - Her build sonrası
6. ✅ **Secrets rotation** - Düzenli token yenileme
7. ✅ **Monitoring** - Pipeline ve K8s monitoring
8. ✅ **Documentation** - Pipeline değişiklikleri dokümante

## 🎉 Özet

Secure Box projesi production-ready CI/CD pipeline'larına sahip:

✅ **GitLab CI/CD** - Tam özellikli 5-stage pipeline  
✅ **GitHub Actions** - Marketplace entegrasyonlu 2-workflow sistem  
✅ **Harbor Registry** - Private Docker registry  
✅ **Kubernetes** - Auto-scaling deployment  
✅ **Monitoring** - Built-in health checks  
✅ **Documentation** - Kapsamlı rehberler  

**Başarılı deployment'lar! 🚀**

---

**📞 Destek:**
- GitLab CI/CD: [docs/GITLAB-CI-CD.md](docs/GITLAB-CI-CD.md)
- GitHub Actions: [.github/workflows/README.md](.github/workflows/README.md)
- Kubernetes: [KUBERNETES-DEPLOYMENT.md](KUBERNETES-DEPLOYMENT.md)

