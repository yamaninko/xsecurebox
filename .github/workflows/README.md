# CI/CD Pipeline Documentation

Bu klasör Secure Box projesinin CI/CD pipeline'larını içerir.

## 🔄 Pipeline'lar

### 1. Build and Push to Harbor (`build-and-push.yml`)

**Tetiklenme:**
- `main` veya `develop` branch'ine push
- `src/` klasöründe değişiklik
- `Dockerfile` değişikliği
- Pull Request oluşturma
- Manuel tetikleme (workflow_dispatch)

**İşlem Adımları:**

1. **Build API**
   - Backend kodunu checkout et
   - Docker Buildx ile multi-platform build hazırlığı
   - Harbor registry'ye login
   - Metadata extraction (tags, labels)
   - Docker image build ve push
   - Build cache kullanımı (GitHub Actions cache)
   - Temizlik (prune)

2. **Build Portal**
   - Frontend kodunu checkout et
   - Docker Buildx kurulumu
   - Harbor registry'ye login
   - Image build ve push
   - Temizlik

3. **Notification**
   - Build sonuçlarını raporla
   - Başarı/hata bildirimi

**Docker Tags:**
- `latest` - main branch için
- `develop` - develop branch için
- `main-<sha>` veya `develop-<sha>` - commit hash'li tag
- `pr-<number>` - Pull Request için

### 2. Deploy to Kubernetes (`deploy-to-k8s.yml`)

**Tetiklenme:**
- Build pipeline başarıyla tamamlandığında (otomatik)
- Manuel tetikleme (environment seçimi ile)

**İşlem Adımları:**

1. **Hazırlık**
   - Kod checkout
   - kubectl kurulumu
   - Kubeconfig yapılandırması
   - Cluster bağlantı testi

2. **Namespace ve Secrets**
   - `securebox` namespace oluşturma
   - Harbor registry secret oluşturma
   - ConfigMap değişkenlerini güncelleme

3. **Database Deployment**
   - PostgreSQL deployment
   - MongoDB deployment
   - Redis deployment
   - RabbitMQ deployment
   - Database'lerin hazır olmasını bekleme

4. **Application Deployment**
   - Backend API deployment
   - Frontend Portal deployment
   - Nginx gateway deployment
   - Rollout durumunu izleme

5. **Verification**
   - Deployment durumlarını kontrol
   - Pod, Service, Ingress listesi
   - Smoke tests (health check)

6. **Rollback (Hata Durumunda)**
   - Otomatik rollback
   - Log toplama
   - Hata bildirimi

## 🔐 Required Secrets

GitHub Repository Settings > Secrets and variables > Actions'da aşağıdaki secret'ları tanımlayın:

### Harbor Registry Secrets

```bash
GTECH_REPO_URL          # Harbor registry URL (örn: harbor.example.com)
GTECH_REPO_USERNAME     # Harbor kullanıcı adı (robot account önerili)
GTECH_REPO_TOKEN        # Harbor access token veya password
```

### Kubernetes Secrets

```bash
KUBECONFIG              # Base64 encoded kubeconfig dosyası
```

**Kubeconfig Hazırlama:**

```bash
# Kubeconfig dosyasını base64'e çevir
cat ~/.kube/config | base64 -w 0

# Veya macOS için
cat ~/.kube/config | base64

# Output'u GitHub Secret olarak KUBECONFIG adıyla ekle
```

## 🚀 Kurulum ve Kullanım

### 1. GitHub Secrets Yapılandırması

```bash
# Repository Settings > Secrets > Actions > New repository secret

1. GTECH_REPO_URL = harbor.example.com
2. GTECH_REPO_USERNAME = robot$securebox
3. GTECH_REPO_TOKEN = eyJhbGc...
4. KUBECONFIG = <base64-encoded-kubeconfig>
```

### 2. Pipeline'ı Tetikleme

**Otomatik Tetikleme:**

```bash
# Main branch'e push
git push origin main

# Develop branch'e push
git push origin develop

# Pull Request oluştur
gh pr create --base main --head feature/new-feature
```

**Manuel Tetikleme:**

1. GitHub Repository'de Actions sekmesine git
2. İlgili workflow'u seç
3. "Run workflow" butonuna tıkla
4. Branch ve environment seç
5. "Run workflow" ile başlat

### 3. Pipeline Durumunu İzleme

```bash
# GitHub CLI ile
gh run list
gh run view <run-id>
gh run watch <run-id>

# Web UI'dan
# https://github.com/<owner>/<repo>/actions
```

## 📊 Build Stratejisi

### Branch Strategy

```
main (production)
  ↓ auto-deploy
  └─ latest tag → Production K8s

develop (staging)
  ↓ auto-deploy
  └─ develop tag → Dev K8s

feature/* (development)
  ↓ build only
  └─ pr-<number> tag → No deploy
```

### Image Tagging Strategy

| Durum | Tag Format | Örnek |
|-------|-----------|-------|
| Main branch | `latest`, `main-<sha>` | `latest`, `main-abc123` |
| Develop branch | `develop`, `develop-<sha>` | `develop`, `develop-def456` |
| Pull Request | `pr-<number>` | `pr-42` |
| Release Tag | `v<version>` | `v1.2.0` |

## 🔒 Güvenlik Best Practices

### 1. Harbor Robot Account Kullanımı

```bash
# Harbor UI'da robot account oluştur
# Project > Robot Accounts > New Robot Account
# Name: securebox-ci
# Expiration: 1 year
# Permissions: Pull & Push
```

### 2. Kubernetes RBAC

```yaml
# CI/CD için minimal RBAC
apiVersion: v1
kind: ServiceAccount
metadata:
  name: github-actions
  namespace: securebox
---
apiVersion: rbac.authorization.k8s.io/v1
kind: Role
metadata:
  name: github-actions-role
  namespace: securebox
rules:
- apiGroups: ["", "apps", "networking.k8s.io"]
  resources: ["*"]
  verbs: ["get", "list", "watch", "create", "update", "patch", "delete"]
---
apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  name: github-actions-binding
  namespace: securebox
subjects:
- kind: ServiceAccount
  name: github-actions
  namespace: securebox
roleRef:
  kind: Role
  name: github-actions-role
  apiGroup: rbac.authorization.k8s.io
```

### 3. Secret Rotation

- Harbor token'ları düzenli yenileyin (3-6 ay)
- Kubeconfig'i güvenli tutun
- Service account token'larını yenileyin

## 🧪 Test ve Validation

### Pre-Deployment Checks

Pipeline otomatik olarak şunları kontrol eder:

1. ✅ Docker build başarılı mı?
2. ✅ Image Harbor'a push edildi mi?
3. ✅ Kubernetes cluster erişilebilir mi?
4. ✅ Namespace mevcut mu?
5. ✅ Secrets doğru mu?

### Post-Deployment Checks

1. ✅ Pod'lar Running durumunda mı?
2. ✅ Service'ler ClusterIP aldı mı?
3. ✅ Health check'ler geçiyor mu?
4. ✅ Smoke tests başarılı mı?

### Manuel Test

```bash
# Pipeline sonrası manuel test
kubectl get pods -n securebox
kubectl get svc -n securebox

# Health check
curl http://<loadbalancer-ip>/api/health

# Portal test
curl http://<loadbalancer-ip>/
```

## 🐛 Troubleshooting

### Build Başarısız

**Problem:** Docker build hatası

**Çözüm:**

```bash
# Local'de test et
cd src/backend/SecureBox.API
docker build -t test-api .

cd ../../frontend
docker build -t test-portal .
```

### Harbor Push Hatası

**Problem:** "unauthorized" veya "authentication required"

**Çözüm:**

```bash
# Credentials'ları kontrol et
docker login ${GTECH_REPO_URL}

# Robot account permissions'ları kontrol et (Harbor UI)
# Project > Robot Accounts > Check permissions
```

### Deployment Hatası

**Problem:** Image pull error

**Çözüm:**

```bash
# Registry secret'i kontrol et
kubectl get secret harbor-registry-secret -n securebox

# Secret'i yeniden oluştur
kubectl delete secret harbor-registry-secret -n securebox
kubectl create secret docker-registry harbor-registry-secret \
  --docker-server=${GTECH_REPO_URL} \
  --docker-username=${GTECH_REPO_USERNAME} \
  --docker-password=${GTECH_REPO_TOKEN} \
  --namespace=securebox
```

**Problem:** Pod CrashLoopBackOff

**Çözüm:**

```bash
# Logs'ları kontrol et
kubectl logs -f -l app=securebox-api -n securebox

# Events'leri kontrol et
kubectl get events -n securebox --sort-by='.lastTimestamp'

# Rollback yap
kubectl rollout undo deployment/securebox-api -n securebox
```

## 📈 Monitoring ve Alerting

### GitHub Actions Monitoring

- Actions sekmesinden workflow çalışmalarını izleyin
- Email notification'ları aktif edin
- Slack/Discord integration ekleyin

### Kubernetes Monitoring

```bash
# Prometheus + Grafana kurulumu (önerilen)
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus prometheus-community/kube-prometheus-stack -n monitoring --create-namespace

# Grafana dashboard'lara erişim
kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80
# http://localhost:3000 (admin/prom-operator)
```

## 🔄 Rollback Stratejisi

### Otomatik Rollback

Pipeline başarısız olursa otomatik rollback tetiklenir:

```yaml
- name: Cleanup on failure
  if: failure()
  run: |
    kubectl rollout undo deployment/securebox-api -n securebox
    kubectl rollout undo deployment/securebox-portal -n securebox
```

### Manuel Rollback

```bash
# Son deployment'ı geri al
kubectl rollout undo deployment/securebox-api -n securebox

# Belirli revision'a dön
kubectl rollout history deployment/securebox-api -n securebox
kubectl rollout undo deployment/securebox-api --to-revision=3 -n securebox
```

## 📚 İleri Seviye

### Multi-Environment Deployment

```yaml
# Kustomize overlay'leri kullan
- name: Deploy to Dev
  run: kubectl apply -k kubernetes/overlays/dev/

- name: Deploy to Prod
  run: kubectl apply -k kubernetes/overlays/prod/
```

### Blue-Green Deployment

```yaml
# İki deployment çalıştır
# Service'i yeni deployment'a yönlendir
kubectl patch service securebox-api-service -p '{"spec":{"selector":{"version":"blue"}}}'
```

### Canary Deployment

```yaml
# Istio veya Argo Rollouts kullan
# Trafiğin %10'unu yeni version'a yönlendir
```

## 📞 Destek

- GitHub Issues: [proje-repo]/issues
- CI/CD Documentation: Bu README
- Kubernetes Documentation: kubernetes/README.md

