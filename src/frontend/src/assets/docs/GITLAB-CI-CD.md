# GitLab CI/CD Pipeline Documentation

Secure Box projesi için GitLab CI/CD pipeline yapılandırması ve kullanım rehberi.

## 📋 İçindekiler

1. [Pipeline Genel Bakış](#pipeline-genel-bakış)
2. [Pipeline Stages](#pipeline-stages)
3. [Variables ve Secrets](#variables-ve-secrets)
4. [Pipeline Kullanımı](#pipeline-kullanımı)
5. [Troubleshooting](#troubleshooting)

## 🎯 Pipeline Genel Bakış

### Pipeline Flow

```
┌─────────────┐
│   COMMIT    │
└──────┬──────┘
       │
   ┌───▼───┐
   │ BUILD │ ─── API Docker Image
   │       │ ─── Portal Docker Image
   └───┬───┘
       │
   ┌───▼───┐
   │  TEST │ ─── API Unit Tests
   │       │ ─── Portal Tests & Linting
   └───┬───┘
       │
   ┌───▼───┐
   │  PUSH │ ─── Harbor Registry
   │       │ ─── Tag: commit-sha, branch, latest
   └───┬───┘
       │
   ┌───▼───┐
   │ DEPLOY│ ─── Dev Environment (manual)
   │       │ ─── Production (manual)
   └───┬───┘
       │
   ┌───▼───┐
   │CLEANUP│ ─── Docker pruning
   │       │ ─── Resource cleanup
   └───────┘
```

### Stages Açıklaması

| Stage | Açıklama | Tetiklenme |
|-------|----------|------------|
| `build` | Docker image'larını build eder | Otomatik (her commit) |
| `test` | Unit test ve lint kontrolleri | Otomatik (her commit) |
| `push` | Harbor registry'ye push | Otomatik (main/develop) |
| `deploy` | Kubernetes'e deployment | Manuel |
| `cleanup` | Kaynak temizliği | Otomatik |

## 🔧 Pipeline Stages

### 1. Build Stage

**Jobs:**
- `build:api` - Backend API Docker image
- `build:portal` - Frontend Portal Docker image

**Özellikleri:**
- ✅ Docker BuildKit kullanımı
- ✅ Multi-stage build
- ✅ Build cache
- ✅ Artifact olarak kaydetme (1 saat)
- ✅ Metadata injection (build date, VCS ref, version)

**Artifacts:**
- `api-image.tar.gz` - API Docker image
- `portal-image.tar.gz` - Portal Docker image

**Örnek:**
```yaml
build:api:
  stage: build
  script:
    - docker build -t $API_IMAGE:$CI_COMMIT_SHORT_SHA .
    - docker save $API_IMAGE | gzip > api-image.tar.gz
  artifacts:
    paths:
      - api-image.tar.gz
    expire_in: 1 hour
```

### 2. Test Stage

**Jobs:**
- `test:api` - .NET unit tests
- `test:portal` - Angular tests ve ESLint

**Özellikleri:**
- ✅ Paralel test execution
- ✅ Code coverage reporting
- ✅ JUnit XML reports
- ✅ Linting checks
- ✅ Test artifacts (7 gün)

**Test Reports:**
- JUnit XML formatında
- Coverage reports (Cobertura)
- GitLab UI'da görüntülenebilir

**Örnek:**
```yaml
test:api:
  stage: test
  script:
    - dotnet test --verbosity normal
  artifacts:
    reports:
      junit: **/TestResults/*.xml
      coverage_report:
        coverage_format: cobertura
        path: **/coverage.cobertura.xml
```

### 3. Push Stage

**Jobs:**
- `push:api` - API image Harbor'a push
- `push:portal` - Portal image Harbor'a push

**Image Tags:**
- `{commit-sha}` - Her commit için unique tag
- `{branch-name}` - Branch adı (main, develop)
- `latest` - Sadece main branch için

**Özellikleri:**
- ✅ Harbor authentication
- ✅ Multi-tag push
- ✅ Image verification
- ✅ Retry on failure (2x)

**Örnek:**
```yaml
push:api:
  stage: push
  script:
    - docker login $GTECH_REPO_URL
    - docker push $API_IMAGE:$CI_COMMIT_SHORT_SHA
    - docker push $API_IMAGE:$CI_COMMIT_REF_SLUG
    - |
      if [ "$CI_COMMIT_REF_NAME" == "main" ]; then
        docker tag $API_IMAGE:$CI_COMMIT_SHORT_SHA $API_IMAGE:latest
        docker push $API_IMAGE:latest
      fi
```

### 4. Deploy Stage

**Jobs:**
- `deploy:dev` - Development environment
- `deploy:production` - Production environment

**Özellikleri:**
- ✅ Manual trigger (güvenlik için)
- ✅ Environment management
- ✅ Rollout status monitoring
- ✅ Smoke tests
- ✅ Rollback capability

**Deployment Flow:**
1. kubectl configuration
2. Namespace creation
3. Harbor secret creation
4. Database deployment
5. Application deployment
6. Health checks
7. Smoke tests

**Örnek:**
```yaml
deploy:production:
  stage: deploy
  environment:
    name: production
    url: https://securebox.example.com
  script:
    - kubectl apply -f kubernetes/base/
    - kubectl rollout status deployment/securebox-api
  when: manual
  only:
    - main
```

### 5. Cleanup Stage

**Jobs:**
- `cleanup:dev` - Dev environment temizliği
- `cleanup:production` - Production temizliği (disabled)
- `cleanup:docker` - Docker resource pruning

**Özellikleri:**
- ✅ Automatic Docker cleanup
- ✅ Manual environment cleanup
- ✅ Safety checks

## 🔐 Variables ve Secrets

### GitLab CI/CD Variables

GitLab UI'da ayarlanması gereken değişkenler:

**Settings > CI/CD > Variables**

#### Harbor Registry

| Variable | Type | Protected | Masked | Açıklama |
|----------|------|-----------|--------|----------|
| `GTECH_REPO_URL` | Variable | ✅ | ❌ | Harbor registry URL |
| `GTECH_REPO_USERNAME` | Variable | ✅ | ✅ | Harbor username (robot account) |
| `GTECH_REPO_TOKEN` | Variable | ✅ | ✅ | Harbor access token |

#### Kubernetes

| Variable | Type | Protected | Masked | Açıklama |
|----------|------|-----------|--------|----------|
| `KUBECONFIG_DEV` | File | ✅ | ❌ | Dev cluster kubeconfig (base64) |
| `KUBECONFIG_PROD` | File | ✅ | ❌ | Prod cluster kubeconfig (base64) |

### Variable Hazırlama

#### Harbor Robot Account Oluşturma

1. Harbor UI'da login olun
2. Project > Robot Accounts > New Robot Account
3. Name: `securebox-ci`
4. Expiration: 1 year
5. Permissions: Pull & Push
6. Token'ı kopyalayın

```bash
# GitLab Variables'a ekle:
GTECH_REPO_URL=harbor.example.com
GTECH_REPO_USERNAME=robot$securebox-ci
GTECH_REPO_TOKEN=<generated-token>
```

#### Kubeconfig Hazırlama

```bash
# Kubeconfig'i base64'e çevir
cat ~/.kube/config | base64 -w 0

# macOS için
cat ~/.kube/config | base64

# Output'u GitLab Variable olarak ekle
# Variable type: File
# Name: KUBECONFIG_DEV veya KUBECONFIG_PROD
```

### Environment Variables

Pipeline içinde kullanılan diğer değişkenler:

```yaml
variables:
  DOCKER_DRIVER: overlay2
  DOCKER_TLS_CERTDIR: "/certs"
  DOCKER_BUILDKIT: "1"
  API_IMAGE: "${GTECH_REPO_URL}/securebox/api"
  PORTAL_IMAGE: "${GTECH_REPO_URL}/securebox/portal"
  KUBE_NAMESPACE: "securebox"
```

## 🚀 Pipeline Kullanımı

### Otomatik Tetikleme

Pipeline otomatik olarak şu durumlarda çalışır:

**1. Branch Push:**
```bash
# Main branch
git push origin main
# → build → test → push → (deploy: manual)

# Develop branch
git push origin develop
# → build → test → push → (deploy:dev manual)

# Feature branch
git push origin feature/new-feature
# → build → test (push yok)
```

**2. Merge Request:**
```bash
# MR oluşturulduğunda
# → build → test
```

### Manuel Deployment

#### Development Deployment

1. GitLab UI'da pipeline'ı aç
2. `deploy:dev` job'unu bul
3. ▶️ Play butonuna tıkla
4. Deployment tamamlanana kadar bekle

```bash
# CLI ile (GitLab CLI gerekli)
glab ci run --branch develop
```

#### Production Deployment

1. GitLab UI'da pipeline'ı aç (main branch)
2. `deploy:production` job'unu bul
3. ⚠️ Production deployment - dikkatli ol!
4. ▶️ Play butonuna tıkla
5. Onay ver
6. Deployment'ı izle

**Production Deployment Checklist:**
- [ ] Tüm test'ler geçti mi?
- [ ] Staging'de test edildi mi?
- [ ] Database migration'lar hazır mı?
- [ ] Rollback planı var mı?
- [ ] Monitoring aktif mi?

### Pipeline İptali

```bash
# UI'dan: Pipeline sayfasında Cancel butonu

# CLI ile
glab ci cancel <pipeline-id>
```

### Retry

```bash
# Failed job'ları retry et
# UI'dan: Job sayfasında Retry butonu

# CLI ile
glab ci retry <pipeline-id>
```

## 📊 Pipeline Monitoring

### Pipeline Durumu

```bash
# Son pipeline'ları listele
glab ci list

# Pipeline detaylarını görüntüle
glab ci view <pipeline-id>

# Pipeline loglarını görüntüle
glab ci trace <job-id>

# Pipeline'ı izle (real-time)
glab ci trace <job-id> --follow
```

### GitLab UI

**Pipeline View:**
- Project > CI/CD > Pipelines
- Pipeline grafik görünümü
- Job durumları
- Artifacts
- Test reports

**Environment View:**
- Project > Deployments > Environments
- Development ve Production ortamları
- Deployment history
- Rollback özelliği

### Badges

Pipeline status badge'ini README'ye ekleyin:

```markdown
[![Pipeline Status](https://gitlab.com/<username>/<project>/badges/main/pipeline.svg)](https://gitlab.com/<username>/<project>/-/commits/main)

[![Coverage](https://gitlab.com/<username>/<project>/badges/main/coverage.svg)](https://gitlab.com/<username>/<project>/-/commits/main)
```

## 🧪 Local Testing

Pipeline'ı local'de test etmek için:

### GitLab Runner Local

```bash
# GitLab Runner kur
curl -L https://packages.gitlab.com/install/repositories/runner/gitlab-runner/script.deb.sh | sudo bash
sudo apt-get install gitlab-runner

# Pipeline'ı local'de çalıştır
gitlab-runner exec docker build:api
gitlab-runner exec docker test:api
```

### Docker Build Testi

```bash
# API build test
cd src/backend/SecureBox.API
docker build -t test-api -f Dockerfile ..

# Portal build test
cd src/frontend
docker build -t test-portal .
```

### Kubernetes Manifest Validation

```bash
# YAML syntax check
yamllint kubernetes/base/*.yaml

# Kubernetes validation
kubectl apply --dry-run=client -f kubernetes/base/

# Kustomize validation
kubectl kustomize kubernetes/base/ | kubectl apply --dry-run=client -f -
```

## 🐛 Troubleshooting

### Build Failures

**Problem:** Docker build hatası

**Çözüm:**
```bash
# Build logs'ları kontrol et
glab ci trace <job-id>

# Local'de test et
cd src/backend/SecureBox.API
docker build --no-cache -t test-api -f Dockerfile ..

# Build context'i kontrol et
docker build --progress=plain -t test-api -f Dockerfile ..
```

### Test Failures

**Problem:** Test'ler başarısız

**Çözüm:**
```bash
# Test reports'ları kontrol et
# GitLab UI > Pipeline > Tests

# Local'de test et
cd src/backend
dotnet test --verbosity detailed

cd src/frontend
npm test
```

### Push Failures

**Problem:** Harbor authentication hatası

**Çözüm:**
```bash
# Credentials'ları test et
docker login $GTECH_REPO_URL -u $GTECH_REPO_USERNAME -p $GTECH_REPO_TOKEN

# GitLab variables'ı kontrol et
# Settings > CI/CD > Variables

# Robot account permissions
# Harbor UI > Project > Robot Accounts > Check permissions
```

### Deployment Failures

**Problem:** Kubernetes deployment hatası

**Çözüm:**
```bash
# Job logs'ları kontrol et
glab ci trace <job-id>

# Kubeconfig'i test et
kubectl cluster-info

# Namespace'i kontrol et
kubectl get all -n securebox

# Pod logs
kubectl logs -l app=securebox-api -n securebox

# Events
kubectl get events -n securebox --sort-by='.lastTimestamp'
```

### Image Pull Failures

**Problem:** "ImagePullBackOff" hatası

**Çözüm:**
```bash
# Secret'i kontrol et
kubectl get secret harbor-registry-secret -n securebox -o yaml

# Secret'i yeniden oluştur
kubectl delete secret harbor-registry-secret -n securebox
kubectl create secret docker-registry harbor-registry-secret \
  --docker-server=$GTECH_REPO_URL \
  --docker-username=$GTECH_REPO_USERNAME \
  --docker-password=$GTECH_REPO_TOKEN \
  --namespace=securebox
```

### Runner Issues

**Problem:** "This job is stuck because the project doesn't have any runners"

**Çözüm:**
```bash
# GitLab Runner status
# Settings > CI/CD > Runners

# Shared runners'ı aktif et
# Settings > CI/CD > Runners > Enable shared runners

# Kendi runner'ınızı ekleyin
gitlab-runner register
```

## 📈 Best Practices

### 1. Branch Strategy

```
main (production)
  ↓ manual deploy
  └─ Kubernetes Production

develop (staging)
  ↓ manual deploy
  └─ Kubernetes Development

feature/* (development)
  ↓ build + test only
  └─ No deployment
```

### 2. Image Tagging

- Her commit için unique tag (commit SHA)
- Branch name tag (main, develop)
- `latest` sadece main branch için
- Semantic versioning (v1.2.3) release'ler için

### 3. Manual Deployments

- Development: Manual (test sonrası)
- Production: Manual (approval gerektiren)
- Rollback: Her zaman hazır

### 4. Security

- ✅ Protected variables kullan
- ✅ Masked sensitive values
- ✅ Robot accounts (Harbor)
- ✅ RBAC (Kubernetes)
- ✅ Limited token expiration

### 5. Performance

- ✅ Docker layer caching
- ✅ Artifact caching (npm, nuget)
- ✅ Parallel jobs
- ✅ Resource cleanup

## 📚 İleri Seviye

### Multi-Environment

```yaml
.deploy_template: &deploy_template
  stage: deploy
  image: bitnami/kubectl:latest
  script:
    - kubectl apply -f kubernetes/overlays/$ENVIRONMENT/

deploy:dev:
  <<: *deploy_template
  variables:
    ENVIRONMENT: dev
  environment:
    name: development

deploy:prod:
  <<: *deploy_template
  variables:
    ENVIRONMENT: prod
  environment:
    name: production
```

### Dynamic Environments

```yaml
review:
  stage: deploy
  script:
    - kubectl create namespace review-$CI_COMMIT_REF_SLUG
    - kubectl apply -f kubernetes/base/ -n review-$CI_COMMIT_REF_SLUG
  environment:
    name: review/$CI_COMMIT_REF_NAME
    url: http://review-$CI_COMMIT_REF_SLUG.example.com
    on_stop: stop_review
  only:
    - merge_requests
```

### Notifications

```yaml
notify:slack:
  stage: .post
  script:
    - |
      curl -X POST $SLACK_WEBHOOK_URL \
        -H 'Content-Type: application/json' \
        -d "{\"text\":\"Pipeline $CI_PIPELINE_STATUS: $CI_PROJECT_NAME ($CI_COMMIT_REF_NAME)\"}"
  when: always
```

## 📞 Destek

- **GitLab Docs:** https://docs.gitlab.com/ee/ci/
- **Kubernetes Docs:** [kubernetes/README.md](../kubernetes/README.md)
- **Project Issues:** GitLab Issues

---

**Pipeline ile başarılı deployment'lar! 🚀**

