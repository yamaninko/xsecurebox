# Secure Box - Deployment Rehberi

## 🚀 Hızlı Başlangıç

### Gereksinimler
- Docker 20.10+
- Docker Compose 2.0+
- 8GB RAM (önerilen)
- 20GB disk alanı

### 1. Depoyu Klonlayın
```bash
git clone https://github.com/your-org/secure-box.git
cd secure-box
```

### 2. Tüm Servisleri Başlatın
```bash
docker-compose up -d
```

### 3. Servislerin Durumunu Kontrol Edin
```bash
docker-compose ps
```

### 4. Portal'a Erişin
- **Portal:** http://localhost
- **API:** http://localhost/api
- **Swagger:** http://localhost/swagger (sadece development)

**Varsayılan Admin Hesabı:**
- Kullanıcı: `admin`
- Şifre: `ADMIN_PASSWORD` environment variable

---

## 📦 Servisler

### Çalışan Konteynerler
| Servis | Port | Açıklama |
|--------|------|----------|
| `nginx` | 80, 443 | Reverse Proxy & Load Balancer |
| `securebox-portal-1` | 4200 | Angular Frontend (Instance 1) |
| `securebox-portal-2` | 4200 | Angular Frontend (Instance 2) |
| `securebox-api-1` | 5000 | ASP.NET API (Instance 1) |
| `securebox-api-2` | 5000 | ASP.NET API (Instance 2) |
| `postgres` | 5432 | Ana veritabanı |
| `redis` | 6379 | Cache & Session |
| `mongodb` | 27017 | Audit logs |
| `rabbitmq` | 5672, 15672 | Message broker |
| `elasticsearch` | 9200 | Log indexing |
| `kibana` | 5601 | Log visualization |

---

## 🔄 Database Migration

### İlk Kurulum
Database migration ve seed otomatik olarak API başlatıldığında çalışır.

### Manuel Migration
```bash
cd src/backend
dotnet ef database update --project SecureBox.Infrastructure --startup-project SecureBox.API
```

### Yeni Migration Oluşturma
```bash
dotnet ef migrations add <MigrationName> --project SecureBox.Infrastructure --startup-project SecureBox.API
```

### Database Reset (Dikkat: Tüm veri silinir!)
```bash
docker-compose down -v
docker-compose up -d postgres
# Veritabanı otomatik olarak yeniden oluşturulur
docker-compose up -d
```

---

## 🛠️ Geliştirme Ortamı

### Backend Geliştirme
```bash
cd src/backend/SecureBox.API
dotnet watch run
```

### Frontend Geliştirme
```bash
cd src/frontend
npm install
npm start
```

API: http://localhost:5000  
Portal: http://localhost:4200

---

## 🔐 Güvenlik Ayarları

### Üretim Ortamına Geçmeden Önce

#### 1. JWT Secret Key Değiştirin
`appsettings.json`:
```json
{
  "JwtSettings": {
    "SecretKey": "YourProductionSecretKeyMinimum32CharactersLong!"
  }
}
```

#### 2. Database Şifrelerini Değiştirin
`docker-compose.yml` ve `appsettings.json`

#### 3. HTTPS Aktifleştirin
```yaml
# docker-compose.yml
nginx:
  ports:
    - "443:443"
  volumes:
    - ./certs:/etc/nginx/certs
```

#### 4. Admin Şifresini Değiştirin
İlk giriş sonrası:
1. Portal → Profile → Change Password

---

## 📊 Monitoring & Logs

### Kibana (Log Visualization)
http://localhost:5601

### RabbitMQ Management
http://localhost:15672  
- Kullanıcı: `guest`
- Şifre: `guest`

### Container Logları
```bash
# Tüm servisler
docker-compose logs -f

# Sadece API
docker-compose logs -f securebox-api-1

# Sadece Portal
docker-compose logs -f securebox-portal-1
```

---

## 🔥 Sorun Giderme

### API Container'ları "Unhealthy" Durumunda

**Sebep:** Database migration veya bağlantı problemi

**Çözüm:**
```bash
docker-compose logs securebox-api-1
docker-compose restart securebox-api-1 securebox-api-2
```

### "Duplicate Key" Hataları

**Sebep:** Race condition (birden fazla instance aynı anda seed yapıyor)

**Çözüm:** Otomatik handle ediliyor. Loglar görmezden gelinebilir.

### Portal Açılmıyor

**Çözüm:**
```bash
docker-compose logs securebox-portal-1
docker-compose restart nginx
```

### Database Bağlantı Hatası

**Çözüm:**
```bash
docker-compose down
docker volume rm secure-box_postgres-data
docker-compose up -d
```

---

## 🚢 Production Deployment

### Docker Swarm (Önerilen)
```bash
docker stack deploy -c docker-compose.yml securebox
```

### Kubernetes
Kubernetes manifest'leri `k8s/` klasöründe.

```bash
kubectl apply -f k8s/
```

### Cloud Deployment

#### Azure
- Azure Container Apps
- Azure Database for PostgreSQL
- Azure Redis Cache
- Azure Key Vault (secrets için)

#### AWS
- ECS/EKS
- RDS PostgreSQL
- ElastiCache Redis
- Secrets Manager

---

## 📈 Scaling

### Horizontal Scaling (Daha Fazla Instance)
```yaml
# docker-compose.yml
securebox-api:
  deploy:
    replicas: 4  # 2 → 4'e çıkar
```

### Load Balancer Ayarları
`infrastructure/nginx/nginx.conf`:
```nginx
upstream api_backend {
    least_conn;  # En az bağlantılı server'a yönlendir
    server securebox-api-1:5000;
    server securebox-api-2:5000;
    server securebox-api-3:5000;
    server securebox-api-4:5000;
}
```

---

## 🔄 Backup & Restore

### Database Backup
```bash
docker exec postgres pg_dump -U securebox_user secureboxdb > backup_$(date +%Y%m%d).sql
```

### Database Restore
```bash
docker exec -i postgres psql -U securebox_user secureboxdb < backup_20251107.sql
```

### Otomatik Backup (Cron)
```bash
# /etc/cron.daily/secure-box-backup
#!/bin/bash
docker exec postgres pg_dump -U securebox_user secureboxdb | gzip > /backups/securebox_$(date +\%Y\%m\%d).sql.gz
find /backups -name "securebox_*.sql.gz" -mtime +30 -delete
```

---

## 📞 Destek

- **Dokümantasyon:** `docs/` klasörü
- **Bug Raporu:** GitHub Issues
- **Email:** support@securebox.local

---

**Son Güncelleme:** 2025-11-07

