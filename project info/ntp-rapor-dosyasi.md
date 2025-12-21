# FIRAT ÜNİVERSİTESİ
## TEKNOLOJİ FAKÜLTESİ
### Yazılım Mühendisliği Bölümü

---

# YMH219 -- NESNE TABANLI PROGRAMLAMA
## Dersi Proje Uygulaması ve Dokümantasyonu

# **E-Sağlık Danışmanı**

**Geliştiren**  
195541099 Ahmet ARSLAN

**Proje Yürütücüleri**  
Dr. Öğr. Üyesi V. Cem BAYDOĞAN  
Arş. Gör. Hüseyin Alperen DAĞDÖGEN

**OCAK -- 2026**

---

## ÖNSÖZ VE TEŞEKKÜR

Hayatım boyunca ve bu çalışma süresince desteklerini esirgemeyen ailem ve arkadaşlarıma teşekkürü bir borç bilirim. Bu projeyi gerçekleştirme aşamasında yararlandığım her kaynağı kaynaklar kısmında bildirdiğimi taahhüt ederim.

**Ahmet ARSLAN**

---

## İÇİNDEKİLER

### 1. GİRİŞ
1. Projenin Tanıtılması
2. Projenin Amacı
3. Projenin Kapsamı
4. Kullanılacak Teknolojiler

### 2. PROJE PLANI
1. Sistemin Kullanıcıları
2. GANT İş Akış Diyagramı
3. İşlevsel İhtiyaçlar (Olmazsa Olmazlar)
4. İşlevsel Olmayan İhtiyaçlar (İlave Özellikler)
5. UML Diyagramları (Class, Activity, Use-Case, Sequence, Interaction - her projede olacak)

### 3. PROJE GERÇEKLEŞTİRİLMESİ
1. Modüllerin ve Tüm Formların Tasarımı (Her form resim olarak koyulup açıklanacaktır)
2. Veritabanı Tasarımı (ER Diyagramı)
3. Çıktılar & Raporlar (Her projede .pdf çıktısı üretilecektir)

### 4. PROJEDE ÖNGÖRÜLEN EKSİKLİKLER
1. Proje planında yapılması planlanmış ancak eksik kalan modüllerin listesi
2. Projeye eklenmesi içeriği zenginleştirecek modüllerin listesi

### 5. PROJE TESLİM
Tüm projeler kaynak kodlarla beraber setup (.exe) dosyası oluşturulup teslim edilecektir. Kurulumla ilgili tüm adımlar bu bölümde resimli anlatılacaktır.

### 6. SONUÇ
- Projenin genel değerlendirmesi (artıları, eksileri, tercih edilme sebebi)
- Projenin geliştirme süresi boyunca size katkısı nedir?

### 7. KAYNAKLAR
Projede referans alınan kaynaklar (web sayfaları, github linkleri, eğitim videoları dahil) bu kısımda verilecektir.

---

## Projelerde Dikkat Edilmesi Gerekli Önemli Hususlar

1. Projeler nesneye dayalı tasarımı prensiplerine uyularak hazırlanmalıdır.

2. Projelerde mutlaka yazılım mühendisliği yöntemleri uygulanmalıdır.

3. Geliştirilen projelerin başarımı ve kullanılabilirliği sınanarak olumlu yönleri gösterilmelidir.

4. Geliştirilecek uygulamanın ilginç ve orijinal bir fikir içermesi tercih edilmektedir. Diğer bir tercih nedeni de kullanılabilir bir ürünün ortaya çıkartılabilmesidir.

5. Geliştirilecek projeler karar verme, öğrenme, hesaplama gibi "akıllı" algoritmalarından birini veya bir kaçını içermesi sonucu olumlu yönde etkileyecektir.

---

## Değerlendirme Kriterleri ve Puanlama

Öğrenciler her bir projede ara ve final raporu olmak üzere iki rapor teslim edeceklerdir. Bu raporlar aşağıdaki kriterlere göre değerlendirilecektir. Sadece proje raporları belirli koşulları sağlayan projelerin final sunumu yapmasına izin verilecektir.

1. Proje Analizi: Data/ Gereksinimlerin Analizi ve Dokümantasyonu (10 puan)
2. Dizayn: Usecase ve Sınıf Diyagramları (10 puan)
3. Projenin Zamanında Teslimi (10 puan)
4. Kullanıcı Arayüzü ve Kullanılabilirlik: Arayüz Tasarımı, Son Kullanıcı Testi (10 puan)
5. Kodlama ve Çıktı (30 puan)
6. Test (10 puan)
7. Dokümantasyon: Proje Dosyası, Bütün modüllerin ekran çıktıları ve Demolar (10 puan)
8. Veritabanı Tasarımı (ER Diyagramları, Şema Tasarımları, Veri Modeli vb...) (10 puan)

### Genel Değerlendirme Ağırlıkları

- Final Rapor: %55
- Final Sunumları: %45

> **Not:** Ara rapor bu projede uygulanmayacaktır.

### Final Sunumundaki Beklentiler

Sadece teknik açıdan yeterliliği ara rapor ve final raporları ile kanıtlanmış projeler değerlendirmeye alınarak demolarının yapılması istenecektir. Sunumlarda iletişim becerisi ve sunuş tekniği önem arz etmektedir.

---

## Örnek Proje Önerisi Dokümanı

### Proje Adı: E-Sağlık Danışmanı

#### Amaç/Vizyon
- Bu projenin temel amacı mobil tabanlı bir Sağlık Bilgi Sistemi uygulaması geliştirmektir.
- Bu sistem soracağı bazı sorularla hastalığın tanımlanmasında kullanıcıya yardımcı olacaktır.
- Alınan tanılara dayanılarak herhangi bir eczaneden alınabilen, reçetesiz olarak satılabilen ilaçlar önerilecek ve doktora gitmesi tavsiye edilecektir.
- Bu sistem verilen geri bildirim ile kendini eğitebilmeye hazır olmalıdır (Yapay Zeka).
- Hastalıkların tanımlanabilmesi için tanıların gerçekleştirilmesi
- Sağlık Yönetim Sistemi tasarlamak
- Hastanın hastalık geçmişini muhafaza etmek ve veritabanını güncellemek için kendi kendine öğrenen bir sistem oluşturmak

#### Sistemin Kullanıcıları
- Hastalar
- Üye olmayanlar
- Yönetici

#### İşlevsel İhtiyaçlar
- Bu yönetim sistemi yönetim ve kazanç odaklı
- Hasta ve hastalık yönetim sisteminin oluşturulması ve uygulanması özellikle vurgulanmaktadır.
- Yapılan işlemlerin planlama kararını hızlandıracak ve kolaylaştıracak, güvenli, gizli ve güvenilir raporlar
- Hastaların güvenliği, gizlilik ve gizlice sorunlara yardımcı olması
- Eski kayıtlara erişmek ve tıbbi kayıtları tutarsizlik yaşanan gecikmeleri kontrol etmek için kullanılır.
- Hasta profilini oluşturmak

#### İşlevsel Olmayan İhtiyaçlar
- Gizli verilerin erişiminde güvenlik
- Kolay arayüz için Kullanıcı dostu UI
- Çalışma zamanlarında gözlemlenebilir güvenlik gibi yürütme nitelikleri
- Yazılım sisteminin mimarisinde gömülü olarak test edilebilir, sürdürülebilir ve ölçeklenebilir gibi uygulama nitelikleri

#### İsteğe Bağlı Özellikler
- Genel (spesifik) ve genel olmayan ilaçların kullanılabilirliği için akıllı seçenekler sunar.
- Ayrıca aynı ilaç kullanarak doktor tarafından tedavi benzer bir hastalığın tanısı okumak için seçenek içerir.

#### Kullanıcı Arabirimi Öncelikleri
- Profesyonel görünüm ve kullanım
- Bütün mobil sistemler de test edilebilir olması
- Admin için stratejik verileri göstermek için grafik aracı kullanımı
- XLS, PDF veya herhangi bir kullanılabilir başka bir formatta raporları export etme

#### Raporlar
- Adı, yeri, hastalık, periyodik temel raporları ara
- En yakın onaylanmış devlet Hastanesi / Klinik ve hasta için önerileri arama

#### Kullanılacak Teknolojiler
- **DevExpress** (UI Framework)
- C# / .NET
- SQL Server / SQLite (Veritabanı)

#### Kullanılacak Araçlar
- Visual Studio 2022
- DevExpress Components
- Git (Versiyon Kontrolü)

---

Her öğrenci proje önerisini yukarıdaki tabloda verilen örnekteki gibi açık ve net ifadeler ile belirtmelidir. Ara ve final raporların teslim tarihi en kısa sürede ilan edilecektir.

---

## Proje Maliyet Kestirim Dokümanı

Projeye başlamadan önce her öğrenci projesi için aşağıdaki sorulara cevap vererek ortalama satır sayısını ve projesinin büyüklüğünü gösteren maliyet kestirim dokümanı oluşturmalıdır.

### Proje adı: (Belirtilecek)

| Ölçüm Parametresi | Sayı | Ağırlık Faktörü | Toplam |
|-------------------|------|-----------------|--------|
| Kullanıcı Girdi Sayısı | | 3 | |
| Kullanıcı Çıktı Sayısı | | 4 | |
| Kullanıcı Sorgu Sayısı | | 3 | |
| Veri Tabanındaki Tablo Sayısı | | 7 | |
| Arayüz Sayısı | | 5 | |
| **Ana İşlev Nokta (AİN Değeri) Sayısı** | | | |

### Teknik Karmaşıklık Değerlendirmesi

| Teknik Karmaşıklık Sorusu | Puan |
|---------------------------|------|
| 1. Uygulama, güvenilir yedekleme ve kurtarma gerektiriyor mu? | |
| 2. Veri iletişimi gerekiyor mu? | |
| 3. Dağıtık işlem işlevleri var mı? | |
| 4. Performans kritik mi? | |
| 5. Sistem mevcut ve ağır yükü olan bir işletim ortamında mı çalışacak? | |
| 6. Sistem, çevrim içi veri girişi gerektiriyor mu? | |
| 7. Çevrim içi veri girişi, bir ara işlem için birden çok ekran gerektiriyor mu? | |
| 8. Ana kütükler çevrim-içi olarak mı günleniyor? | |
| 9. Girdiler, çıktılar, kütükler ya da sorgular karmaşık mı? | |
| 10. İçsel işlemler karmaşık mı? | |
| 11. Tasarlanacak kod, yeniden kullanılabilir mi olacak? | |
| 12. Dönüştürme ve kurulum, tasarımda dikkate alınacak mı? | |
| 13. Sistem birden çok yerde yerleşik farklı kurumlar için mi geliştiriliyor? | |
| 14. Tasarlanan uygulama, kolay kullanılabilir ve kullanıcı tarafından kolayca değiştirilebilir mi olacak? | |
| **Toplam (TKF)** | |

### Puanlama Skalası

- **0:** Hiçbir Etkisi Yok
- **1:** Çok Az etkisi var
- **2:** Etkisi Var
- **3:** Ortalama Etkisi Var
- **4:** Önemli Etkisi Var
- **5:** Mutlaka Olmalı, Kaçınılamaz

### Hesaplama Formülleri

```
İN = AİN × (0.65 + 0.01 × TKF)
Satır Sayısı = İN × 30
```