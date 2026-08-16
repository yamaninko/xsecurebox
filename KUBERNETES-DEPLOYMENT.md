# 🚀 Secure Box - Kubernetes Deployment Guide

Bu dokümantasyon Secure Box uygulamasını Kubernetes cluster'ına deploy etmek için hızlı başlangıç rehberidir.

## 📋 İçindekiler

1. [Genel Bakış](#genel-bakış)
2. [Ön Gereksinimler](#ön-gereksinimler)
3. [Hızlı Başlangıç](#hızlı-başlangıç)
4. [CI/CD Kurulumu](#cicd-kurulumu)
5. [Monitoring ve Yönetim](#monitoring-ve-yönetim)

## 🎯 Genel Bakış

### Mimari

```
┌─────────────────────────────────────────────────┐
│                   Internet                       │
└────────────────────┬────────────────────────────┘
                     │
          ┌──────────▼──────────┐
          │  Ingress/LoadBalancer│
          │     (Nginx)          │
          └──────────┬───────────┘
                     │
        ┌────────────┴────────────┐
        │                          │
    ┌───▼────┐              ┌─────▼──┐
    │ Portal │              │  API   │
    │  (x2)  │              │  (x2+) │
    └────────┘              └────┬───┘
                                 │
                    ┌────────────┼────────────┐
                    │            │            │
              ┌─────▼─┐    ┌────▼───┐   ┌───▼───┐
              │Postgres│    │MongoDB │   │ Redis │
              └────────┘    └────────┘   └───────┘
                                  │
                            ┌─────▼─────┐
                            │ RabbitMQ  │
                            └───────────┘
```

### Kaynaklar

**Namespace:** `securebox`

**Services:**
- API Backend (2+ replicas with HPA)
- Portal Frontend (2+ replicas with HPA)
- PostgreSQL (StatefulSet)
- MongoDB (StatefulSet)
- Redis (Deployment)
- RabbitMQ (Deployment)
- Nginx Gateway (2 replicas)

**Storage:**
- PostgreSQL PVC: 10Gi
- MongoDB PVC: 10Gi

**Auto-Scaling:**
- API: 2-10 replicas (CPU 70%, Memory 80%)
- Portal: 2-5 replicas (CPU 70%, Memory 80%)

## 📦 Ön Gereksinimler

### 1. Kubernetes Cluster

- Kubernetes v1.24+
- 4 CPU, 8GB RAM minimum
- StorageClass support (PVC için)
- LoadBalancer support (public access için)

**Desteklenen platformlar:**
- Google Kubernetes Engine (GKE)
- Amazon Elastic Kubernetes Service (EKS)
- Azure Kubernetes Service (AKS)
- On-premise Kubernetes
- Minikube (development)
- Kind (development)

### 2. CLI Tools

```bash
# kubectl
curl -LO "https://dl.k8s.io/release/$(curl -L -s https://dl.k8s.io/release/stable.txt)/bin/linux/amd64/kubectl"
chmod +x kubectl
sudo mv kubectl /usr/local/bin/

# kubectl version
kubectl version --client

# Cluster bağlantısını test et
kubectl cluster-info
kubectl get nodes
```

### 3. Harbor Registry Access

Secure Box Docker image'ları Harbor registry'de saklanır. Erişim için gerekli:

- **REGISTRY_URL**: Harbor registry URL'i
- **REGISTRY_USERNAME**: Harbor kullanıcı adı (robot account)
- **REGISTRY_TOKEN**: Harbor access token

## 🚀 Hızlı Başlangıç

### Yöntem 1: Otomatik Deployment Script (Önerilen)

```bash
# 1. Harbor credentials'ları ayarla
export REGISTRY_URL="harbor.example.com"
export REGISTRY_USERNAME="robot\$securebox"
export REGISTRY_TOKEN="your-token-here"

# 2. Deployment script'ini çalıştır
./scripts/deploy-to-k8s.sh

# 3. Deployment durumunu izle
kubectl get pods -n securebox -w
```

**Script ne yapar?**
- ✅ Ön gereksinimleri kontrol eder
- ✅ Namespace oluşturur
- ✅ Harbor registry secret'ını oluşturur
- ✅ Database'leri deploy eder ve hazır olmasını bekler
- ✅ Application'ı deploy eder
- ✅ Smoke test'leri çalıştırır
- ✅ Erişim bilgilerini gösterir

### Yöntem 2: Manuel Deployment

```bash
# 1. Namespace oluştur
kubectl apply -f kubernetes/base/namespace.yaml

# 2. Harbor registry secret oluştur
kubectl create secret docker-registry harbor-registry-secret \
  --docker-server=$REGISTRY_URL \
  --docker-username=$REGISTRY_USERNAME \
  --docker-password=$REGISTRY_TOKEN \
  --namespace=securebox

# 3. ConfigMap ve Secrets
kubectl apply -f kubernetes/base/configmap.yaml
kubectl apply -f kubernetes/base/secrets.yaml

# 4. Database'leri deploy et
kubectl apply -f kubernetes/base/postgres-deployment.yaml
kubectl apply -f kubernetes/base/mongodb-deployment.yaml
kubectl apply -f kubernetes/base/redis-deployment.yaml
kubectl apply -f kubernetes/base/rabbitmq-deployment.yaml

# Database'lerin hazır olmasını bekle
kubectl wait --for=condition=ready pod -l app=postgres -n securebox --timeout=300s
kubectl wait --for=condition=ready pod -l app=mongodb -n securebox --timeout=300s
kubectl wait --for=condition=ready pod -l app=redis -n securebox --timeout=300s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n securebox --timeout=300s

# 5. Application'ı deploy et
# Not: ${REGISTRY_URL} placeholder'ını gerçek URL ile değiştirin
sed -i "s|\${REGISTRY_URL}|$REGISTRY_URL|g" kubernetes/base/api-deployment.yaml
sed -i "s|\${REGISTRY_URL}|$REGISTRY_URL|g" kubernetes/base/portal-deployment.yaml

kubectl apply -f kubernetes/base/api-deployment.yaml
kubectl apply -f kubernetes/base/portal-deployment.yaml
kubectl apply -f kubernetes/base/nginx-configmap.yaml
kubectl apply -f kubernetes/base/nginx-deployment.yaml

# Deployment'ların hazır olmasını bekle
kubectl rollout status deployment/securebox-api -n securebox
kubectl rollout status deployment/securebox-portal -n securebox
kubectl rollout status deployment/nginx -n securebox
```

### 3. Uygulamaya Erişim

```bash
# LoadBalancer IP'yi al
kubectl get svc nginx-service -n securebox

# Veya port-forward ile local erişim
kubectl port-forward -n securebox svc/nginx-service 8080:80

# Tarayıcıda aç
# http://localhost:8080
```

**Default Credentials:**
- Username: `admin`
- Password: `ADMIN_PASSWORD` environment variable

## 🔄 CI/CD Kurulumu

### GitHub Actions ile Otomatik Deployment

#### 1. Repository Secrets Ayarla

GitHub Repository → Settings → Secrets → Actions → New repository secret

```bash
# Harbor Registry
REGISTRY_URL          # örn: harbor.example.com
REGISTRY_USERNAME     # örn: robot$securebox
REGISTRY_TOKEN        # Harbor access token

# Kubernetes
KUBECONFIG              # Base64 encoded kubeconfig
```

**Kubeconfig Hazırlama:**

```bash
# Kubeconfig'i base64'e çevir
cat ~/.kube/config | base64 -w 0

# macOS için
cat ~/.kube/config | base64

# Output'u KUBECONFIG secret'ı olarak ekle
```

#### 2. Pipeline'lar

**Build Pipeline** (`.github/workflows/build-and-push.yml`)

```yaml
# Otomatik tetiklenme:
# - main veya develop branch'e push
# - src/ klasöründe değişiklik
# - Pull Request

# İşlemler:
# 1. Backend API build ve Harbor'a push
# 2. Frontend Portal build ve Harbor'a push
# 3. Build sonuçlarını bildir
```

**Deploy Pipeline** (`.github/workflows/deploy-to-k8s.yml`)

```yaml
# Otomatik tetiklenme:
# - Build pipeline başarıyla tamamlandığında
# - Manuel tetikleme (workflow_dispatch)

# İşlemler:
# 1. Kubernetes cluster'a bağlan
# 2. Namespace ve secrets oluştur
# 3. Database'leri deploy et
# 4. Application'ı deploy et
# 5. Smoke test'leri çalıştır
# 6. Hata durumunda rollback
```

#### 3. Deployment Workflow

```
Developer Push → GitHub
       ↓
Build Pipeline (GitHub Actions)
   ├─ Build API Docker Image
   ├─ Build Portal Docker Image
   └─ Push to Harbor Registry
       ↓
Deploy Pipeline (GitHub Actions)
   ├─ Connect to K8s Cluster
   ├─ Create/Update Resources
   ├─ Wait for Rollout
   ├─ Run Smoke Tests
   └─ Success/Rollback
       ↓
Production Kubernetes Cluster
```

#### 4. Manuel Deployment Tetikleme

```bash
# GitHub CLI ile
gh workflow run deploy-to-k8s.yml \
  -f environment=prod

# Veya GitHub UI'dan:
# Actions → Deploy to Kubernetes → Run workflow
```

## 📊 Monitoring ve Yönetim

### Pod Durumunu Kontrol

```bash
# Tüm pod'ları listele
kubectl get pods -n securebox

# Pod detaylarını görüntüle
kubectl describe pod <pod-name> -n securebox

# Real-time log izleme
kubectl logs -f -l app=securebox-api -n securebox

# Tüm pod'ların durumunu izle
kubectl get pods -n securebox -w
```

### Service ve Ingress

```bash
# Service'leri listele
kubectl get svc -n securebox

# Ingress'i kontrol et
kubectl get ingress -n securebox

# LoadBalancer external IP
kubectl get svc nginx-service -n securebox -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

### Scaling

```bash
# Manuel scaling
kubectl scale deployment securebox-api --replicas=5 -n securebox

# HPA durumu
kubectl get hpa -n securebox
kubectl describe hpa securebox-api-hpa -n securebox

# HPA metrics
kubectl top pods -n securebox
kubectl top nodes
```

### Health Checks

```bash
# API health
kubectl exec -n securebox deployment/nginx -- \
  curl -s http://securebox-api-service:5000/health

# Database connection test
kubectl exec -it -n securebox deployment/postgres -- \
  psql -U secureboxuser -d secureboxdb -c "SELECT 1;"

kubectl exec -it -n securebox deployment/mongodb -- \
  mongosh -u secureboxuser -p --eval "db.adminCommand('ping')"
```

### Resource Usage

```bash
# Pod resource usage
kubectl top pods -n securebox

# Node resource usage
kubectl top nodes

# Pod details with requests/limits
kubectl describe pods -n securebox | grep -A 5 "Limits\|Requests"
```

## 🔄 Updates ve Rollback

### Rolling Update

```bash
# Yeni image version'ı ile güncelle
kubectl set image deployment/securebox-api \
  api=$REGISTRY_URL/securebox/api:v1.2.0 \
  -n securebox

# Rollout durumunu izle
kubectl rollout status deployment/securebox-api -n securebox

# Rollout pause (sorun varsa)
kubectl rollout pause deployment/securebox-api -n securebox

# Rollout resume
kubectl rollout resume deployment/securebox-api -n securebox
```

### Rollback

```bash
# Son deployment'ı geri al
kubectl rollout undo deployment/securebox-api -n securebox

# Belirli revision'a geri dön
kubectl rollout history deployment/securebox-api -n securebox
kubectl rollout undo deployment/securebox-api --to-revision=2 -n securebox
```

## 🧹 Cleanup

### Script ile Temizlik (Önerilen)

```bash
# Cleanup script'ini çalıştır
./scripts/cleanup-k8s.sh

# Seçenekler:
# 1) Tüm namespace'i sil (hızlı)
# 2) Kaynakları tek tek sil (namespace kalır)
```

### Manuel Temizlik

```bash
# Tüm namespace'i sil (tüm kaynakları siler)
kubectl delete namespace securebox

# Sadece application'ı sil (database'ler kalır)
kubectl delete deployment securebox-api securebox-portal nginx -n securebox
```

## 🐛 Troubleshooting

### Pod Başlatılamıyor

```bash
# Pod events'leri kontrol et
kubectl describe pod <pod-name> -n securebox

# Pod logs
kubectl logs <pod-name> -n securebox

# Önceki container logs (crash durumunda)
kubectl logs <pod-name> -n securebox --previous
```

### Image Pull Error

```bash
# Secret'i kontrol et
kubectl get secret harbor-registry-secret -n securebox -o yaml

# Secret'i yeniden oluştur
kubectl delete secret harbor-registry-secret -n securebox
kubectl create secret docker-registry harbor-registry-secret \
  --docker-server=$REGISTRY_URL \
  --docker-username=$REGISTRY_USERNAME \
  --docker-password=$REGISTRY_TOKEN \
  --namespace=securebox
```

### Database Connection Error

```bash
# Service DNS test
kubectl run -it --rm debug --image=busybox --restart=Never -n securebox -- \
  nslookup postgres-service

# Port connectivity test
kubectl run -it --rm debug --image=busybox --restart=Never -n securebox -- \
  telnet postgres-service 5432
```

### Application Not Accessible

```bash
# Service kontrolü
kubectl get svc nginx-service -n securebox

# Ingress kontrolü
kubectl describe ingress securebox-ingress -n securebox

# Pod health
kubectl get pods -n securebox
kubectl logs -f deployment/nginx -n securebox
```

## 📚 Detaylı Dokümantasyon

- **Kubernetes Manifests:** [kubernetes/README.md](kubernetes/README.md)
- **CI/CD Pipeline:** [.github/workflows/README.md](.github/workflows/README.md)
- **Application Guide:** [README.md](README.md)
- **Deployment Guide:** [DEPLOYMENT.md](DEPLOYMENT.md)

## 🆘 Destek

Sorularınız veya sorunlarınız için:

- **GitHub Issues:** Repository'de issue açın
- **Dokümantasyon:** Yukarıdaki linklere bakın
- **Logs:** Pod loglarını kontrol edin

---

**🎉 Başarılı deployment'lar dileriz!**

