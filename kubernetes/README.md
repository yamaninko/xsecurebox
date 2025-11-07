# Secure Box Kubernetes Deployment

Bu klasör Secure Box uygulamasının Kubernetes deployment dosyalarını içerir.

## 📁 Klasör Yapısı

```
kubernetes/
├── base/                       # Base Kubernetes manifests
│   ├── namespace.yaml         # securebox namespace
│   ├── configmap.yaml         # Uygulama konfigürasyonu
│   ├── secrets.yaml           # Hassas bilgiler (passwords, keys)
│   ├── postgres-deployment.yaml   # PostgreSQL database
│   ├── mongodb-deployment.yaml    # MongoDB database
│   ├── redis-deployment.yaml      # Redis cache
│   ├── rabbitmq-deployment.yaml   # RabbitMQ messaging
│   ├── api-deployment.yaml        # Backend API + HPA
│   ├── portal-deployment.yaml     # Frontend Portal + HPA
│   ├── nginx-configmap.yaml       # Nginx configuration
│   ├── nginx-deployment.yaml      # Nginx reverse proxy + Ingress
│   └── kustomization.yaml         # Kustomize config
├── overlays/                  # Environment-specific overlays
│   ├── dev/                   # Development environment
│   └── prod/                  # Production environment
└── README.md                  # Bu dosya
```

## 🚀 Kurulum

### Ön Gereksinimler

1. **Kubernetes Cluster** (v1.24+)
   - Minikube, Kind, GKE, EKS, AKS veya kendi cluster'ınız

2. **kubectl** (v1.24+)
   ```bash
   kubectl version --client
   ```

3. **Harbor Registry Credentials**
   - `GTECH_REPO_URL`: Harbor registry URL'i
   - `GTECH_REPO_TOKEN`: Harbor access token

### 1. Namespace Oluşturma

```bash
kubectl apply -f kubernetes/base/namespace.yaml
```

### 2. Harbor Registry Secret Oluşturma

```bash
kubectl create secret docker-registry harbor-registry-secret \
  --docker-server=${GTECH_REPO_URL} \
  --docker-username=${GTECH_REPO_USERNAME} \
  --docker-password=${GTECH_REPO_TOKEN} \
  --namespace=securebox
```

### 3. ConfigMap ve Secrets Güncelleme

**UYARI:** Production'da `secrets.yaml` dosyasındaki değerleri mutlaka değiştirin!

```bash
# Secrets dosyasını düzenle
kubectl apply -f kubernetes/base/configmap.yaml
kubectl apply -f kubernetes/base/secrets.yaml
```

### 4. Database ve Cache Deployment

```bash
# PostgreSQL
kubectl apply -f kubernetes/base/postgres-deployment.yaml

# MongoDB
kubectl apply -f kubernetes/base/mongodb-deployment.yaml

# Redis
kubectl apply -f kubernetes/base/redis-deployment.yaml

# RabbitMQ
kubectl apply -f kubernetes/base/rabbitmq-deployment.yaml

# Database'lerin hazır olmasını bekle
kubectl wait --for=condition=ready pod -l app=postgres -n securebox --timeout=300s
kubectl wait --for=condition=ready pod -l app=mongodb -n securebox --timeout=300s
kubectl wait --for=condition=ready pod -l app=redis -n securebox --timeout=300s
kubectl wait --for=condition=ready pod -l app=rabbitmq -n securebox --timeout=300s
```

### 5. Application Deployment

```bash
# Backend API
kubectl apply -f kubernetes/base/api-deployment.yaml

# Frontend Portal
kubectl apply -f kubernetes/base/portal-deployment.yaml

# Nginx Gateway
kubectl apply -f kubernetes/base/nginx-configmap.yaml
kubectl apply -f kubernetes/base/nginx-deployment.yaml

# Deployment'ların hazır olmasını bekle
kubectl rollout status deployment/securebox-api -n securebox
kubectl rollout status deployment/securebox-portal -n securebox
kubectl rollout status deployment/nginx -n securebox
```

### 6. Tüm Kaynakları Tek Seferde Uygulama

```bash
# Sıralı deployment (önerilen)
kubectl apply -f kubernetes/base/ --recursive

# Veya Kustomize ile
kubectl apply -k kubernetes/base/
```

## 📊 Monitoring ve Kontrol

### Pod Durumunu Kontrol Etme

```bash
# Tüm pod'ları görüntüle
kubectl get pods -n securebox

# Pod detaylarını görüntüle
kubectl describe pod <pod-name> -n securebox

# Pod loglarını görüntüle
kubectl logs -f <pod-name> -n securebox

# API logları
kubectl logs -f -l app=securebox-api -n securebox

# Portal logları
kubectl logs -f -l app=securebox-portal -n securebox
```

### Service ve Ingress Kontrolü

```bash
# Service'leri görüntüle
kubectl get services -n securebox

# Ingress'i görüntüle
kubectl get ingress -n securebox

# LoadBalancer IP'yi al
kubectl get svc nginx-service -n securebox
```

### Ölçekleme (Scaling)

```bash
# Manuel scaling
kubectl scale deployment securebox-api --replicas=5 -n securebox

# HPA durumunu kontrol et
kubectl get hpa -n securebox

# HPA detaylarını görüntüle
kubectl describe hpa securebox-api-hpa -n securebox
```

## 🔄 Güncelleme (Rolling Update)

### Docker Image Güncelleme

```bash
# API image'ını güncelle
kubectl set image deployment/securebox-api \
  api=${GTECH_REPO_URL}/securebox/api:v1.2.0 \
  -n securebox

# Portal image'ını güncelle
kubectl set image deployment/securebox-portal \
  portal=${GTECH_REPO_URL}/securebox/portal:v1.2.0 \
  -n securebox

# Rollout durumunu izle
kubectl rollout status deployment/securebox-api -n securebox
```

### Rollback

```bash
# Son deployment'ı geri al
kubectl rollout undo deployment/securebox-api -n securebox

# Belirli bir revision'a geri dön
kubectl rollout undo deployment/securebox-api --to-revision=2 -n securebox

# Rollout geçmişini görüntüle
kubectl rollout history deployment/securebox-api -n securebox
```

## 🔒 Güvenlik

### Secrets Yönetimi

**Production için önemli notlar:**

1. `secrets.yaml` dosyasındaki tüm default password'leri değiştirin
2. JWT Secret Key'i en az 32 karakter uzunluğunda random bir değer yapın
3. Encryption Key'i tam olarak 32 byte (256-bit) uzunluğunda yapın
4. Secrets'i Git'e commit etmeyin (`.gitignore`'a ekleyin)

### Network Policies (Opsiyonel)

```bash
# Network policy örneği
kubectl apply -f kubernetes/base/network-policy.yaml
```

### RBAC (Opsiyonel)

```bash
# Service account ve RBAC kuralları
kubectl apply -f kubernetes/base/rbac.yaml
```

## 🧪 Health Check ve Testing

### Health Endpoints

```bash
# Port forward ile local test
kubectl port-forward -n securebox svc/nginx-service 8080:80

# Health check
curl http://localhost:8080/api/health

# Ready check
curl http://localhost:8080/api/health/ready
```

### Database Bağlantı Testi

```bash
# PostgreSQL'e bağlan
kubectl exec -it -n securebox deployment/postgres -- psql -U secureboxuser -d secureboxdb

# MongoDB'ye bağlan
kubectl exec -it -n securebox deployment/mongodb -- mongosh -u secureboxuser -p

# Redis'e bağlan
kubectl exec -it -n securebox deployment/redis -- redis-cli -a SecureBox2024!
```

## 🧹 Temizlik

### Tüm Kaynakları Silme

```bash
# Namespace'i sil (tüm kaynakları siler)
kubectl delete namespace securebox

# Veya teker teker sil
kubectl delete -f kubernetes/base/ --recursive
```

### Sadece Application'ı Silme (Database'leri Koruma)

```bash
kubectl delete deployment securebox-api -n securebox
kubectl delete deployment securebox-portal -n securebox
kubectl delete deployment nginx -n securebox
```

## 📈 Production Best Practices

1. **Resource Limits**: Her container için resource limits tanımlayın
2. **Liveness & Readiness Probes**: Tüm probes'ları düzgün yapılandırın
3. **PersistentVolume**: Database'ler için uygun storage class kullanın
4. **Backup**: Düzenli database backup stratejisi oluşturun
5. **Monitoring**: Prometheus + Grafana ile monitoring ekleyin
6. **Logging**: ELK veya Loki ile centralized logging kurun
7. **SSL/TLS**: Production'da HTTPS kullanın (cert-manager ile)
8. **Network Policies**: Pod'lar arası trafiği sınırlayın
9. **Pod Security Policies**: Security context'leri tanımlayın
10. **HPA**: Auto-scaling için HPA'yı aktif edin

## 🆘 Troubleshooting

### Pod Restart Ediyor

```bash
# Pod loglarını kontrol et
kubectl logs <pod-name> -n securebox --previous

# Pod event'lerini kontrol et
kubectl describe pod <pod-name> -n securebox
```

### Database Bağlantı Hatası

```bash
# Service DNS'ini test et
kubectl run -it --rm debug --image=busybox --restart=Never -n securebox -- nslookup postgres-service

# Port bağlantısını test et
kubectl run -it --rm debug --image=busybox --restart=Never -n securebox -- telnet postgres-service 5432
```

### Image Pull Hatası

```bash
# Secret'i kontrol et
kubectl get secret harbor-registry-secret -n securebox -o yaml

# Secret'i yeniden oluştur
kubectl delete secret harbor-registry-secret -n securebox
kubectl create secret docker-registry harbor-registry-secret \
  --docker-server=${GTECH_REPO_URL} \
  --docker-username=${GTECH_REPO_USERNAME} \
  --docker-password=${GTECH_REPO_TOKEN} \
  --namespace=securebox
```

## 📞 Destek

Sorularınız için:
- GitHub Issues: [proje-repo]/issues
- Email: support@securebox.local

