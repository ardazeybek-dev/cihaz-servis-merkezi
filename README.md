# Cihaz Servis Merkezi

[![CI](https://github.com/ardazeybek-dev/cihaz-servis-merkezi/actions/workflows/ci.yml/badge.svg)](https://github.com/ardazeybek-dev/cihaz-servis-merkezi/actions/workflows/ci.yml)

Ölçüm cihazı satan bir firmanın müşteri tarafı için örnek uygulama. **ASP.NET Core 10 (Razor Pages) + MS SQL Server + EF Core**, IIS/Plesk'e kurulacak biçimde hazırlandı. Tüm veriler örnektir, gerçek bir firmaya ait değildir.

*English summary at the bottom.*

## Neler var

| Bölüm | Ne yapıyor |
|---|---|
| **Garanti sorgusu** `/garanti` | Seri numarasıyla model ve garanti durumu. Giriş biçimi normalize edilir (`dmo a240 0001` → `DMO-A240-0001`), kişisel veri dönmez, IP başına hız sınırı (429), bot tuzağı alanı, sorgu kaydında IP yerine HMAC özeti. |
| **Servis talebi** `/servis/talep` | Girişsiz form, en fazla 3 ek (tür uzantıdan değil **dosya imzasından** denetlenir), aydınlatma metni onayı (onaylanan sürüm + zaman kaydedilir), `SR-2026-000001` biçiminde çakışmasız talep numarası, konu/modele göre **atama kuralı** (kural yoksa varsayılan sorumlu), "talebiniz alındı" e-postası. |
| **Durum sorgusu** `/servis/durum` | Talep no + telefonun son 4 hanesi → zaman çizelgesi, sıradaki adım, iş günü takvimine göre tahmini tarih. Aynı talep no için **5 hatalı denemede 15 dk kilit**. |
| **Yönetim** `/yonetim` | Talep kuyruğu ve filtre, geçerli durum geçişleri (Alındı → İnceleniyor → Parça bekleniyor → Serviste → Tamamlandı, her aşamadan İptal), müşteriye görünen not / iç not, ek indirme, sertifika iptali, **yalnız eklenebilen denetim günlüğü** (veritabanı tetikleyicisiyle). |
| **Müşteri paneli** `/musteri/cihazlar` | Kurumun cihazları. Başka kurumun cihazı istenirse **404** (nesne düzeyinde yetki). |
| **Eğitim** `/egitim` | Süreli imzalı HLS akışı (360p/720p), **ileri sarılamayan oynatıcı**, izlenen sürenin **sunucuda** aralık aralık doğrulanması (sahte konum bildirimi reddedilir), %95 izlenmeden quiz açılmaz, sorular ve puan sunucuda (geçme notu 80, 3 hak, hak bitince video sıfırlanır), karekodlu **PDF sertifika** ve herkese açık doğrulama sayfası `/sertifika/{no}`. |

Arka plan işleri Hangfire (SQL Server deposu): e-postalar kuyruktan gider, süresi dolan durum sorgusu kilitleri saatlik temizlenir. Pano `/yonetim/hangfire` yalnız Yönetici rolüne açıktır.

## Teknolojiler

ASP.NET Core 10 Razor Pages · EF Core 10 (SQL Server) · çerez kimlik doğrulaması ve rol politikaları (şifreler Identity `PasswordHasher` ile) · Hangfire · MailKit · QuestPDF · QRCoder · hls.js · Serilog · xUnit · Testcontainers · Playwright · axe-core

## Klasör yapısı

```
src/
  CihazServis.Application     İş kuralları: varlıklar, garanti doğrulama, servis talebi, eğitim, quiz, sertifika
  CihazServis.Infrastructure  EF Core (DbContext, migration, demo verisi), e-posta, dosya deposu, PDF, HMAC
  CihazServis.Web             Razor Pages, REST API (/api/v1), güvenlik başlıkları, hız sınırları, Hangfire
tests/
  CihazServis.Tests           Birim + entegrasyon testleri (gerçek SQL Server konteyneriyle)
  CihazServis.E2E             Playwright uçtan uca testleri, erişilebilirlik (WCAG 2.1 AA) ve 320 px denetimi
scripts/
  medya-uret.sh               Demo eğitim videolarını ffmpeg ile üretir (HLS + altyazı)
  publish.ps1                 IIS/Plesk yayın paketi + idempotent veritabanı betiği
deploy/                       Linux sunucuda Docker ile demo: compose, Caddy, gece sıfırlama
```

## Yerelde çalıştırma

Gerekenler: .NET 10 SDK, Docker, ffmpeg (yalnız eğitim videoları için), Google Chrome (yalnız E2E testleri için).

```bash
docker compose up -d --wait          # SQL Server 2022 (14333) + smtp4dev (arayüz http://localhost:5025)
dotnet tool restore
bash scripts/medya-uret.sh           # media/ klasörüne 3 demo ders videosu
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/CihazServis.Web
```

Geliştirme ortamında migration ve demo verisi açılışta otomatik uygulanır. Uygulama http://localhost:5080 adresinde açılır, gönderilen e-postalar smtp4dev arayüzünde görünür.

**Demo hesapları** (şifre hepsinde `Demo.2026!`)

| Hesap | Rol |
|---|---|
| `musteri@ornek-klinik.test` | Müşteri (Örnek Klinik) |
| `musteri@ornek-spor.test` | Müşteri (başka kurum) |
| `personel@servis.test` | Servis personeli |
| `yonetici@servis.test` | Yönetici |

Demo seri numaraları: `DMO-A240-0001` (garantisi devam ediyor), `DMO-A230-0002` (garantisi bitmiş). Demo talep: `SR-2026-000001`, telefonun son 4 hanesi `1234`.

## Testler

```bash
dotnet test tests/CihazServis.Tests   # birim + entegrasyon testleri, Docker gerekir
dotnet test tests/CihazServis.E2E     # tarayıcı testleri, önce scripts/medya-uret.sh
```

Örnek olarak sınanan durumlar: aynı anda açılan talepler aynı numarayı almaz, geçersiz durum geçişi reddedilir, 5 hatalı denemede kilit, uzantısı değiştirilmiş dosya reddedilir, durum yanıtında kişisel veri yok, başka kurumun cihazı 404, sahte izleme bildirimi ve ileri sarma sunucuda reddedilir, süresi geçmiş medya imzası 403, quiz cevabı istemciye gitmez, iptal edilmiş sertifika doğrulamada geçersiz görünür. E2E testi bir talebi baştan sona yürütür: form → SMTP'ye düşen e-posta → yönetimde durum değişikliği → müşterinin sorgusunda görünmesi.

CI (GitHub Actions) her push'ta derler (uyarılar hata sayılır), açık bulunan paketleri ve bekleyen model değişikliklerini denetler, tüm testleri çalıştırır.

## Ortam değişkenleri

Gizli değerler depoda tutulmaz, sunucuda ortam değişkeni olarak verilir (`:` yerine `__`).

| Ad | Açıklama |
|---|---|
| `ConnectionStrings__Veritabani` | SQL Server bağlantı dizesi |
| `Guvenlik__IpOzetAnahtari` | IP özeti için HMAC anahtarı (en az 16 karakter) |
| `Guvenlik__MedyaImzaAnahtari` | Video adreslerini imzalayan HMAC anahtarı (en az 16 karakter) |
| `Guvenlik__VekilSunucular__0` | Önündeki ters vekil sunucunun IP'si (gerçek istemci IP'si ve HTTPS bilgisi için) |
| `Uygulama__GenelAdres` | Sitenin dışarıdan görünen adresi, ör. `https://servis.ornek.com` (karekod ve e-posta bağlantıları) |
| `Eposta__Sunucu`, `Eposta__Port`, `Eposta__Guvenlik` | SMTP sunucusu. `Guvenlik`: `None`, `Auto`, `StartTls`, `SslOnConnect` |
| `Eposta__Kullanici`, `Eposta__Sifre`, `Eposta__Gonderen`, `Eposta__GonderenAd` | SMTP kimliği ve gönderen |
| `Depolama__Klasor` | Talep eklerinin saklandığı klasör (web kökü dışında olmalı) |
| `Medya__Klasor` | HLS eğitim videolarının klasörü |
| `Hangfire__Etkin` | `true` (varsayılan) / `false` |
| `Veritabani__OtomatikMigration`, `Veritabani__DemoVerisi` | Açılışta migration / demo verisi (canlıda `false`) |
| `HizSiniri__Dogrulama__Limit`, `HizSiniri__DurumSorgusu__Limit`, `HizSiniri__Form__Limit`, `HizSiniri__SertifikaDogrulama__Limit` | IP başına istek sınırları |

## IIS / Plesk kurulumu

1. Sunucuya **ASP.NET Core 10 Hosting Bundle** kurulur.
2. Yerelde paket hazırlanır: `powershell -ExecutionPolicy Bypass -File scripts\publish.ps1` → `publish\CihazServis.zip` (uygulama + `veritabani.sql`).
3. Veritabanı: uygulama kullanıcısının tablo oluşturma yetkisi varsa `Veritabani__OtomatikMigration=true` ile ilk açılışta kurulur, yoksa `veritabani.sql` SQL Server Management Studio'dan bir kez çalıştırılır (betik idempotent, tekrar çalıştırmak zarar vermez).
4. Paket sitenin köküne açılır. Uygulama havuzu: **.NET CLR sürümü "No Managed Code"**, başlatma modu **AlwaysRunning**, sitede **Preload Enabled** (Hangfire işlerinin ziyaretçi beklemeden çalışması için).
5. Ortam değişkenleri IIS Yöneticisi > site > Configuration Editor > `system.webServer/aspNetCore` > `environmentVariables` bölümünden ya da Plesk'te ASP.NET Core ayarlarından girilir. Depodaki `web.config`'e gizli değer yazılmaz.
6. `Depolama__Klasor` ve `Medya__Klasor` web kökü dışında bir klasörü gösterir, uygulama havuzu kimliğine bu klasörlerde yazma/okuma izni verilir.

`web.config` `inprocess` barındırma kullanır, `Server` başlığını kaldırır ve istek boyutunu 34 MB'a çıkarır (IIS varsayılanı 30 MB, form tek istekte 3 × 10 MB ek kabul eder).

## Linux sunucuda demo (Docker)

`deploy/` klasörü tek sunucuda SQL Server Express + uygulama + smtp4dev + Caddy (otomatik HTTPS) kurar. En az 2 GB RAM (4 GB önerilir).

```bash
git clone <depo> /opt/csm && cd /opt/csm/deploy
cp .env.ornek .env          # ALAN_ADI, şifre ve anahtarları doldurun
bash ../scripts/medya-uret.sh deploy/medya   # ffmpeg gerekir, ya da hazır media/ klasörünü deploy/medya olarak kopyalayın
docker compose up -d --build
```

- Caddy, istemci IP'sini `X-Forwarded-For` ile iletir. Uygulama bu başlığa yalnız Caddy'nin sabit iç ağ adresinden (`172.30.0.10`) geldiğinde güvenir, böylece hız sınırı ve IP özeti gerçek ziyaretçiye göre çalışır.
- Gerçek SMTP bağlanmaz, bildirimler `https://posta.<ALAN_ADI>` adresindeki demo posta kutusunda görünür (`.env`'deki kullanıcı ve şifre özetiyle korunur).
- Sunucu `mcr.microsoft.com` ya da Docker Hub'a erişemiyorsa (bağlantı sıfırlanıyor): Docker Hub için `/etc/docker/daemon.json`'a `"registry-mirrors": ["https://mirror.gcr.io"]` eklenir, Microsoft imajları yerelde çekilip taşınır: `docker save <imaj> | gzip | ssh root@sunucu 'gunzip | docker load'`, sonra `docker compose up -d --no-build`.
- `deploy/demo-sifirla.sh` veritabanını, ekleri ve posta kutusunu silip demo verisini yeniden kurar. Cron ile her gece çalıştırılır: `0 1 * * * /opt/csm/deploy/demo-sifirla.sh >> /var/log/csm-sifirla.log 2>&1`

## Güvenlik notları

- Uygulanan CSP: satır içi betik ve stil yok, tüm betikler ayrı dosyada.
- Çerezler `HttpOnly`, `SameSite=Lax`, HTTPS'te `Secure`; tüm formlarda antiforgery.
- Ekler rastgele adla web kökü dışında saklanır, yalnız personel indirebilir, indirme yanıtında `sandbox` CSP'si var.
- Video bölümleri 5 dakikalık imzalı adreslerle verilir, oturum açmadan erişilemez.
- Denetim günlüğü ve talep hareketleri tablolarında güncelleme/silme veritabanı tetikleyicisiyle engellenir.

## Bilinen tuzaklar

- Yerelde HTTP'de çerez/antiforgery 500 veriyor → `SecurePolicy.SameAsRequest` kullanılıyor; canlıda vekil `X-Forwarded-Proto: https` geçirmeli ve `Guvenlik__VekilSunucular` ayarlanmalı, yoksa çerez Secure olmaz ve tüm istemciler tek hız sınırı kovasına düşer.
- Satır içi `style=""` ya da `<script>` CSP'ye takılır → hep sınıf ve `wwwroot/js` altındaki dosyalar.
- `<form action="/x">` antiforgery anahtarı eklemez, gönderim 400 döner → formlarda `asp-page` kullanın.
- Playwright'ın kendi Chromium'u H.264 oynatmaz → E2E testleri kurulu Google Chrome ile çalışır (`E2E_TARAYICI` ile değiştirilebilir).
- SQL Server varsayılan harmanlaması büyük/küçük harf duyarsız → biçim denetleyen CHECK kısıtlarında `COLLATE Latin1_General_BIN2`.

---

## English summary

Sample customer-facing app for a measurement-device vendor, built on **ASP.NET Core 10 Razor Pages, MS SQL Server and EF Core**, ready for IIS/Plesk hosting. All data is fictional.

- **Warranty lookup** by serial number with input normalisation, per-IP rate limiting, honeypot field and no personal data in responses.
- **Service tickets**: anonymous form, attachments validated by file signature, privacy-notice consent with version, collision-free ticket numbers, assignment rules, e-mail notifications via Hangfire + MailKit.
- **Status lookup** with ticket number + last 4 phone digits, timeline and business-day ETA, 15-minute lockout after 5 failed attempts.
- **Back office**: ticket queue, validated status transitions, internal vs. customer-visible notes, append-only audit log enforced by a database trigger, object-level authorisation (other tenants' devices return 404).
- **Training**: signed short-lived HLS URLs, a player that cannot be skipped ahead, server-verified watch intervals, server-side quiz scoring, QR-verifiable PDF certificates.

Run locally with `docker compose up -d --wait`, `bash scripts/medya-uret.sh`, `dotnet run --project src/CihazServis.Web`. Tests: unit/integration tests against a real SQL Server container (Testcontainers) and Playwright end-to-end tests including WCAG 2.1 AA (axe-core) and 320 px overflow checks, all run in GitHub Actions.
