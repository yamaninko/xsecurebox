# 🔐 Secure Box - Yüksek Güvenlikli Anahtar Yönetim Sistemi

> **Versiyon:** 1.0.0 MVP  
> **Son Güncelleme:** 2025-11-07  
> **Durum:** ✅ MVP Tamamlandı

## 📝 Genel Bakış

Secure Box, kritik anahtarların (API keys, passwords, secrets, certificates) şifrelenmiş olarak saklanmasını ve yüksek güvenlik standartlarıyla API ve Portal üzerinden yönetimini sağlayan bir sistemdir.

**Varsayılan Giriş Bilgileri:**
- **Kullanıcı Adı:** `admin`
- **Şifre:** `Admin@123`
- **Portal URL:** http://localhost

## Teknoloji Stack

### Backend
- **ASP.NET 9** (C#)
- **Entity Framework Core** (PostgreSQL için)
- **JWT Authentication** (Bearer Token)
- **Sertifika Tabanlı Şifreleme** (X.509)

### Frontend
- **Angular** (son sürüm)
- **Angular Material** (UI Components)
- **RxJS** (Reactive Programming)

### Veritabanları ve Altyapı
- **PostgreSQL**: Ana veri (kullanıcılar, sertifikalar, anahtarlar)
- **Redis**: Session yönetimi ve cache
- **MongoDB**: Log kayıtları
- **RabbitMQ**: Asenkron mesajlaşma
- **ELK Stack / OpenSearch**: Log analizi ve monitoring

### Deployment
- **Docker & Docker-Compose**
- **Nginx**: Load Balancer
- **TLS/SSL**: Tüm iletişimler şifreli

## Özellikler

- ✅ Sertifika tabanlı şifreleme
- ✅ JWT ile authentication/authorization
- ✅ Role-based access control (Admin, Client, Service)
- ✅ Audit logging (tüm işlemler loglanır)
- ✅ Sertifika yaşam döngüsü yönetimi
- ✅ Anahtar yaşam döngüsü yönetimi
- ✅ Portal UI (Angular)
- ✅ High-availability deployment
- ✅ Comprehensive monitoring

## Proje Yapısı

```
secure-box/
├── docs/                          # Tüm dokümantasyon
│   ├── 01-component-diagram.md
│   ├── 02-database-schema.md
│   ├── 03-api-endpoints.md
│   ├── 04-ui-design.md
│   ├── 08-security-checklist.md
│   ├── 09-testing-plan.md
│   └── 10-feature-backlog.md
├── src/
│   ├── backend/                   # ASP.NET 9 API
│   └── frontend/                  # Angular Portal
├── infrastructure/
│   ├── docker-compose.yml
│   ├── nginx/
│   ├── postgres/
│   └── certificates/
└── README.md
```

## Hızlı Başlangıç

### Gereksinimler
- Docker & Docker-Compose
- .NET 9 SDK
- Node.js & npm (Angular için)

### Kurulum

```bash
# Projeyi klonlayın
cd /Users/gtmac29/projects/secure-box

# Docker-Compose ile tüm servisleri başlatın
docker-compose up -d

# Backend API: http://localhost:5000
# Frontend Portal: http://localhost:4200
```

### Veritabanı Başlatma

Backend servisi, PostgreSQL şemasını ve varsayılan admin kullanıcısını uygulama başlangıcında otomatik olarak oluşturur. 
`appsettings.json` içindeki `Database.ApplyMigrationsOnStartup` ve `Database.SeedDefaultsOnStartup` ayarları (veya 
environment değişkenleri `Database__ApplyMigrationsOnStartup`, `Database__SeedDefaultsOnStartup`) ile bu davranışı 
aktif/pasif hale getirebilirsiniz. Kendi veritabanı otomasyonunuza sahipseniz bu ayarları `false` yapmanız yeterlidir.

## Güvenlik

Bu sistem en yüksek güvenlik standartlarıyla tasarlanmıştır:
- TLS 1.3 zorunlu
- Sertifika tabanlı şifreleme
- Tüm işlemler audit log'lanır
- Role-based access control
- Key rotation politikaları
- Sertifika yaşam döngüsü yönetimi

Detaylı güvenlik bilgisi için: [Güvenlik Kontrol Listesi](docs/08-security-checklist.md)

## Lisans

Proprietary - Tüm hakları saklıdır.
