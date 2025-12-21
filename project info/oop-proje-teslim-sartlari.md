# Projelerde Dikkat Edilmesi Gerekli Önemli Hususlar

1. Projeler nesneye dayalı tasarımı prensiplerine uyularak hazırlanmalıdır.

2. Projelerde mutlaka yazılım mühendisliği yöntemleri uygulanmalıdır.

3. Geliştirilen projelerin başarımı ve kullanılabilirliği sınanarak olumlu yönleri gösterilmelidir.

4. Geliştirilecek uygulamanın ilginç ve orijinal bir fikir içermesi tercih edilmektedir. Diğer bir tercih nedeni de kullanılabilir bir ürünün ortaya çıkartılabilmesidir.

5. Geliştirilecek projeler karar verme, öğrenme, hesaplama gibi "akıllı" algoritmalarından birini veya bir kaçını içermesi sonucu olumlu yönde etkileyecektir.

## Değerlendirme Kriterleri ve Puanlama

Öğrenciler her bir projede ara ve final raporu olmak üzere iki rapor teslim edeceklerdir. Bu raporlar aşağıdaki kriterlere göre değerlendirilecektir. Sadece proje raporları belirli koşulları sağlayan projelerin final sunumu yapmasına izin verilecektir.

1. Proje Analizi: Data/ Gereksinimlerin Analizi ve Dokümantasyonu (10 puan)
2. Dizayn: Usecase ve Sınıf Diyagramları (10 puan)
3. Projenin Zamanında Teslimi (10 puan)
4. Kullanıcı Arayüzü ve Kullanılabilirlik: Arayüz Tasarımı, Son Kullanıcı Testi (10 puan)
5. Kodlama ve Çıktı (30 puan)
6. Test (10 puan)
7. Dokümantasyon: Proje Dosyası, Javadoc, Bütün modüllerin ekran çıktıları ve Demolar (10 puan)
8. Veritabanı Tasarımı (ER Diyagramları, Şema Tasarımları, Veri Modeli vb...) (10 puan)

## Genel Değerlendirme Ağırlıkları

- Final Rapor: %55
- Final Sunumları: %45

> **Not:** Ara rapor bu projede uygulanmayacaktır.

## Final Sunumundaki Beklentiler

Sadece teknik açıdan yeterliliği ara rapor ve final raporları ile kanıtlanmış projeler değerlendirmeye alınarak demolarının yapılması istenecektir. Sunumlarda iletişim becerisi ve sunuş tekniği önem arz etmektedir.

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

Her öğrenci proje önerisini yukarıdaki tabloda verilen örnekteki gibi açık ve net ifadeler ile belirtmelidir. Proje önerileri **30.02.2024-15.03.2024** tarihleri arasındaki laboratuvar derslerinde toplanacaktır. Ara ve final raporların teslim tarihi en kısa sürede ilan edilecektir.

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

**Puanlama Skalası:**
- **0:** Hiçbir Etkisi Yok
- **1:** Çok Az etkisi var
- **2:** Etkisi Var
- **3:** Ortalama Etkisi Var
- **4:** Önemli Etkisi Var
- **5:** Mutlaka Olmalı, Kaçınılamaz

**Formüller:**
- İN = AİN x (0.65 x 0.01 x TKF)
- Satır Sayısı = İN × 30

---

## Proje Raporu Yapısı

Proje raporu hazırlanırken aşağıdaki yazılım yaşam döngüsü adımları rehber olarak izlenmelidir.

### 1. GİRİŞ
1. Projenin Amacı
2. Projenin Kapsamı
3. Tanımlamalar ve Kısaltmalar

### 2. PROJE PLANI
1. Giriş
2. Projenin Plan Kapsamı
3. Proje Zaman-İş Planı
4. Proje Ekip Yapısı
5. Önerilen Sistemin Teknik Tanımları
6. Kullanılan Özel Geliştirme Araçları ve Ortamları
7. Proje Standartları, Yöntem ve Metodolojiler
8. Kalite Sağlama Planı
9. Konfigürasyon Yönetim Planı
10. Kaynak Yönetim Planı
11. Eğitim Planı
12. Test Planı
13. Bakım Planı
14. Projede Kullanılan Yazılım/Donanım Araçlar

### 3. SİSTEM ÇÖZÜMLEME

#### 3.1 Mevcut Sistem İncelemesi
1. Örgüt Yapısı
2. İşlevsel Model
3. Veri Modeli
4. Varolan Yazılım/Donanım Kaynakları
5. Varolan Sistemin Değerlendirilmesi

#### 3.2 Gereksenen Sistemin Mantıksal Modeli
1. Giriş
2. İşlevsel Model
3. Genel Bakış
4. Bilgi Sistemleri/Nesneler
5. Veri Modeli
6. Veri Sözlüğü
7. İşlevlerin Sıradüzeni
8. Başarım Gerekleri

#### 3.3 Arayüz (Modül) Gerekleri
1. Yazılım Arayüzü
2. Kullanıcı Arayüzü
3. İletişim Arayüzü
4. Yönetim Arayüzü

#### 3.4 Belgeleme Gerekleri
1. Geliştirme Sürecinin Belgelenmesi
2. Eğitim Belgeleri
3. Kullanıcı El Kitapları

### 4. SİSTEM TASARIMI

#### 4.1 Genel Tasarım Bilgileri
1. Genel Sistem Tanımı
2. Varsayımlar ve Kısıtlamalar
3. Sistem Mimarisi
4. Dış Arabirimler
   - Kullanıcı Arabirimleri
   - Veri Arabirimleri
   - Diğer Sistemlerle Arabirimler
5. Veri Modeli
6. Testler
7. Performans

#### 4.2 Veri Tasarımı
1. Tablo tanımları
2. Tablo-İlişki Şemaları
3. Veri Tanımları
4. Değer Kümesi Tanımları

#### 4.3 Süreç Tasarımı
1. Genel Tasarım
2. Modüller
   - XXX Modülü
     - İşlev
     - Kullanıcı Arabirimi
     - Modül Tanımı
     - Modül iç Tasarımı
   - YYY Modülü
3. Kullanıcı Profilleri
4. Entegrasyon ve Test Gereksinimleri

#### 4.4 Ortak Alt Sistemlerin Tasarımı
1. Ortak Alt Sistemler
2. Modüller arası Ortak Veriler
3. Ortak Veriler İçin Veri Giriş ve Raporlama Modülleri
4. Güvenlik Altsistemi
5. Veri Dağıtım Altsistemi
6. Yedekleme ve Arşivleme İşlemleri

### 5. SİSTEM GERÇEKLEŞTİRİMİ

#### 5.1 Giriş

#### 5.2 Yazılım Geliştirme Ortamları
1. Programlama Dilleri
2. Veri Tabanı Yönetim Sistemleri
   - VTYS Kullanımının Ek Yararları
   - Veri Modelleri
   - Şemalar
   - VTYS Mimarisi
   - Veritabanı Dilleri ve Arabirimleri
   - Veri Tabanı Sistem Ortamı
   - VTYS'nin Sınıflandırılması
   - Hazır Program Kütüphane Dosyaları
   - CASE Araç ve Ortamları

#### 5.3 Kodlama Stili
1. Açıklama Satırları
2. Kod Biçimlemesi
3. Anlamlı İsimlendirme
4. Yapısal Programlama Yapıları

#### 5.4 Program Karmaşıklığı
1. Programın Çizge Biçimine Dönüştürülmesi
2. McCabe Karmaşıklık Ölçütü Hesaplama

#### 5.5 Olağan Dışı Durum Çözümleme
1. Olağandışı Durum Tanımları
2. Farklı Olağandışı Durum Çözümleme Yaklaşımları

#### 5.6 Kod Gözden Geçirme
1. Gözden Geçirme Sürecinin Düzenlenmesi
2. Gözden Geçirme Sırasında Kullanılacak Sorular
   - Öbek Arayüzü
   - Giriş Açıklamaları
   - Veri Kullanımı
   - Öbeğin Düzenlenişi
   - Sunuş

### 6. DOĞRULAMA VE GEÇERLEME

1. Giriş
2. Sınama Kavramları
3. Doğrulama ve Geçerleme Yaşam Döngüsü
4. Sınama Yöntemleri
   - Beyaz Kutu Sınaması
   - Temel Yollar Sınaması
5. Sınama ve Bütünleştirme Stratejileri
   - Yukarıdan Aşağı Sınama ve Bütünleştirme
   - Aşağıdan Yukarıya Sınama ve Bütünleştirme
6. Sınama Planlaması
7. Sınama Belirtimleri
8. Yaşam Döngüsü Boyunca Sınama Etkinlikleri

### 7. BAKIM

1. Giriş
2. Kurulum
3. Yerinde Destek Organizasyonu
4. Yazılım Bakımı
   - Tanım
   - Bakım Süreç Modeli

### 8. SONUÇ

### 9. KAYNAKLAR