# CI/CD Platform Karşılaştırması

Secure Box projesi için GitLab CI/CD ve GitHub Actions platformlarının karşılaştırması.

## 📊 Genel Karşılaştırma

| Özellik | GitLab CI/CD | GitHub Actions |
|---------|--------------|----------------|
| **Yapılandırma Dosyası** | `.gitlab-ci.yml` | `.github/workflows/*.yml` |
| **Syntax** | YAML | YAML |
| **Runner** | GitLab Runner (self-hosted veya shared) | GitHub-hosted veya self-hosted |
| **Paralel Jobs** | ✅ Stages ile otomatik | ✅ Matrix strategy ile |
| **Caching** | ✅ Built-in cache | ✅ actions/cache ile |
| **Artifacts** | ✅ Built-in | ✅ actions/upload-artifact ile |
| **Environments** | ✅ Built-in environment tracking | ✅ Environment protection rules |
| **Manual Approval** | ✅ `when: manual` | ✅ Environment approval |
| **Secrets Management** | ✅ Variables (masked, protected) | ✅ Secrets (encrypted) |
| **Docker Support** | ✅ Native DinD support | ✅ Docker available |
| **Free Tier** | 400 CI minutes/month | 2000 minutes/month |

## 🔍 Detaylı Karşılaştırma

### 1. Pipeline Yapısı

#### GitLab CI/CD

```yaml
stages:
  - build
  - test
  - deploy

build:api:
  stage: build
  script:
    - docker build -t api .
  artifacts:
    paths:
      - api-image.tar.gz
```

**Avantajları:**
- ✅ Stage'ler otomatik sıralanır
- ✅ Aynı stage'deki job'lar paralel çalışır
- ✅ Tek dosyada tüm pipeline
- ✅ YAML anchor ve extend ile DRY
- ✅ Built-in caching ve artifacts

**Dezavantajları:**
- ❌ Tek dosya büyüyebilir
- ❌ Include ile modülerleştirme gerekli
- ❌ Marketplace yok (custom scripts)

#### GitHub Actions

```yaml
name: Build and Push
on:
  push:
    branches: [main]

jobs:
  build-api:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: docker/build-push-action@v5
        with:
          push: true
          tags: api:latest
```

**Avantajları:**
- ✅ Marketplace ile binlerce hazır action
- ✅ Modüler yapı (multiple workflows)
- ✅ Matrix strategy ile kolay kombinasyonlar
- ✅ Reusable workflows
- ✅ Workflow templates

**Dezavantajları:**
- ❌ Jobs arasında dependency manuel
- ❌ Artifact upload/download action gerekli
- ❌ Cache için action gerekli

### 2. Docker Build & Push

#### GitLab CI/CD

```yaml
build:api:
  stage: build
  image: docker:24-dind
  services:
    - docker:24-dind
  script:
    - docker login $REGISTRY
    - docker build -t $IMAGE .
    - docker push $IMAGE
```

**Avantajları:**
- ✅ Native Docker-in-Docker support
- ✅ Basit ve anlaşılır
- ✅ Service container support

#### GitHub Actions

```yaml
- name: Build and push
  uses: docker/build-push-action@v5
  with:
    context: .
    push: true
    tags: ${{ env.IMAGE }}
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

**Avantajları:**
- ✅ Optimize edilmiş build action
- ✅ GitHub Actions cache entegrasyonu
- ✅ BuildKit optimization

### 3. Kubernetes Deployment

#### GitLab CI/CD

```yaml
deploy:production:
  stage: deploy
  image: bitnami/kubectl:latest
  script:
    - kubectl apply -f k8s/
    - kubectl rollout status deployment/app
  environment:
    name: production
    url: https://app.example.com
  when: manual
```

**Avantajları:**
- ✅ Built-in environment tracking
- ✅ Deployment history
- ✅ Environment URL tracking
- ✅ Rollback UI support

#### GitHub Actions

```yaml
- name: Deploy to K8s
  run: |
    kubectl apply -f k8s/
    kubectl rollout status deployment/app
  env:
    KUBECONFIG: ${{ secrets.KUBECONFIG }}
```

**Avantajları:**
- ✅ Basit ve direkt
- ✅ Environment protection rules
- ✅ Required reviewers

### 4. Testing & Coverage

#### GitLab CI/CD

```yaml
test:api:
  stage: test
  script:
    - dotnet test
  coverage: '/Total\s+\|\s+(\d+\.?\d*)%/'
  artifacts:
    reports:
      junit: results.xml
      coverage_report:
        coverage_format: cobertura
        path: coverage.xml
```

**Avantajları:**
- ✅ Built-in test report parsing
- ✅ Built-in coverage tracking
- ✅ MR'da test sonuçları
- ✅ Coverage badge otomatik

#### GitHub Actions

```yaml
- name: Run tests
  run: dotnet test
- name: Upload coverage
  uses: codecov/codecov-action@v3
  with:
    files: coverage.xml
```

**Avantajları:**
- ✅ Codecov entegrasyonu
- ✅ Test result actions

### 5. Secrets Management

#### GitLab CI/CD

```yaml
# GitLab UI: Settings > CI/CD > Variables

script:
  - echo "$GTECH_REPO_TOKEN" | docker login -u "$GTECH_REPO_USERNAME" --password-stdin
```

**Özellikler:**
- ✅ Protected (sadece protected branch'ler)
- ✅ Masked (loglarda gizlenir)
- ✅ Environment-specific variables
- ✅ File type variables

#### GitHub Actions

```yaml
# GitHub UI: Settings > Secrets > Actions

- name: Login
  env:
    TOKEN: ${{ secrets.GTECH_REPO_TOKEN }}
  run: echo "$TOKEN" | docker login ...
```

**Özellikler:**
- ✅ Repository secrets
- ✅ Organization secrets
- ✅ Environment secrets
- ✅ Dependabot secrets

### 6. Caching

#### GitLab CI/CD

```yaml
cache:
  key: ${CI_COMMIT_REF_SLUG}
  paths:
    - .cache/
    - node_modules/
```

**Özellikler:**
- ✅ Built-in caching
- ✅ S3-compatible storage
- ✅ Branch-specific cache
- ✅ Distributed cache

#### GitHub Actions

```yaml
- uses: actions/cache@v3
  with:
    path: ~/.cache
    key: ${{ runner.os }}-cache-${{ hashFiles('**/package-lock.json') }}
```

**Özellikler:**
- ✅ Action-based caching
- ✅ GitHub-hosted cache (10GB)
- ✅ Hash-based keys
- ✅ Restore keys

## 🎯 Hangi Platform?

### GitLab CI/CD Tercih Edin Eğer:

✅ **Tek platform** çözümü istiyorsanız (SCM + CI/CD + Container Registry)  
✅ **Built-in features** tercih ediyorsanız (test reports, coverage, environments)  
✅ **Self-hosted runners** kullanmak istiyorsanız  
✅ **Enterprise features** gerekiyorsa (compliance, security scanning)  
✅ **Unified DevOps** platformu istiyorsanız  
✅ **On-premise** deployment yapıyorsanız  

**Örnek Kullanım:**
- Enterprise şirketler
- Self-hosted GitLab kullananlar
- Tek platform tercih edenler
- Security ve compliance önemli

### GitHub Actions Tercih Edin Eğer:

✅ **Marketplace** ile binlerce hazır action istiyorsanız  
✅ **Open source** projeniz varsa (unlimited free minutes)  
✅ **GitHub** zaten kullanıyorsanız  
✅ **Community support** önemliyse  
✅ **Modüler workflows** tercih ediyorsanız  
✅ **Reusable workflows** kullanmak istiyorsanız  

**Örnek Kullanım:**
- Open source projeler
- GitHub kullanan ekipler
- Marketplace actions'lardan faydalanmak
- Startup'lar (ücretsiz tier)

## 💰 Maliyet Karşılaştırması

### GitLab (Cloud)

| Tier | Fiyat | CI Minutes | Features |
|------|-------|------------|----------|
| Free | $0 | 400/month | Temel CI/CD |
| Premium | $19/user/month | 10,000/month | Advanced CI/CD |
| Ultimate | $99/user/month | 50,000/month | Enterprise |

### GitHub

| Tier | Fiyat | CI Minutes | Features |
|------|-------|------------|----------|
| Free | $0 | 2,000/month | Temel CI/CD |
| Team | $4/user/month | 3,000/month | Advanced features |
| Enterprise | $21/user/month | 50,000/month | Enterprise |

**Not:** 
- Self-hosted runners her iki platformda da ücretsiz
- GitHub Actions open source için unlimited free
- GitLab self-hosted tamamen ücretsiz

## 🚀 Secure Box İçin Öneriler

### Senaryo 1: GitLab Kullanıyorsanız

```bash
# .gitlab-ci.yml kullanın
# ✅ Tek platform avantajı
# ✅ Built-in registry (Harbor yerine GitLab Registry)
# ✅ Built-in environments
```

**Kurulum:**
```bash
# GitLab variables ayarla
GTECH_REPO_URL=gitlab.example.com/registry
KUBECONFIG_DEV=<base64-kubeconfig>
KUBECONFIG_PROD=<base64-kubeconfig>

# Pipeline otomatik çalışacak
git push origin main
```

### Senaryo 2: GitHub Kullanıyorsanız

```bash
# .github/workflows/*.yml kullanın
# ✅ GitHub ecosystem
# ✅ Marketplace actions
# ✅ Daha fazla ücretsiz minutes
```

**Kurulum:**
```bash
# GitHub secrets ayarla
GTECH_REPO_URL=harbor.example.com
GTECH_REPO_TOKEN=<token>
KUBECONFIG=<base64-kubeconfig>

# Pipeline otomatik çalışacak
git push origin main
```

### Hybrid Approach

Her iki platform için de yapılandırma hazır! 🎉

```
secure-box/
├── .gitlab-ci.yml          # GitLab CI/CD
└── .github/
    └── workflows/
        ├── build-and-push.yml    # GitHub Actions - Build
        └── deploy-to-k8s.yml     # GitHub Actions - Deploy
```

**Platform değiştirmek kolay:**
1. Sadece secrets/variables'ı yeni platforma taşıyın
2. Pipeline otomatik çalışmaya başlar
3. Harbor registry her ikisi için ortak

## 📊 Feature Comparison Matrix

| Feature | GitLab CI/CD | GitHub Actions | Winner |
|---------|--------------|----------------|--------|
| **Ease of Setup** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| **Pipeline Syntax** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | GitLab |
| **Docker Support** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | GitLab |
| **Marketplace/Actions** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| **Test Reporting** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | GitLab |
| **Environment Management** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | GitLab |
| **Free Tier** | ⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |
| **Self-Hosted** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | GitLab |
| **Enterprise Features** | ⭐⭐⭐⭐⭐ | ⭐⭐⭐⭐ | GitLab |
| **Community** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | GitHub |

## 🎓 Öğrenme Kaynakları

### GitLab CI/CD

- **Official Docs:** https://docs.gitlab.com/ee/ci/
- **CI/CD Templates:** https://gitlab.com/gitlab-org/gitlab/-/tree/master/lib/gitlab/ci/templates
- **Tutorial:** https://docs.gitlab.com/ee/ci/quick_start/

### GitHub Actions

- **Official Docs:** https://docs.github.com/en/actions
- **Marketplace:** https://github.com/marketplace?type=actions
- **Awesome Actions:** https://github.com/sdras/awesome-actions

## 📞 Sonuç

Her iki platform da production-ready ve güçlü. Seçiminiz:

1. **Mevcut ekosisteminize** bağlı (GitLab vs GitHub)
2. **Özellik ihtiyaçlarınıza** bağlı (built-in vs marketplace)
3. **Bütçenize** bağlı (free tier usage)
4. **Ekip tecrübesine** bağlı (YAML syntax tercihi)

**Secure Box projesi her iki platform için de hazır! 🚀**

Platform değiştirmek isterseniz sadece secrets/variables'ı migrate etmeniz yeterli.

---

**İyi deployment'lar! 🎉**

