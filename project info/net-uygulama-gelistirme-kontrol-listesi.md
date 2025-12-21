# Proje Kod Standartları Kontrol Listesi

## Genel

1. Service ve Forms'larda hata değişkeni global tanımlanmamalı.

2. Class isimlerinde ki her kelimenin ilk harfi büyük olmalı (FrmHoiz)

3. Method isimlerinde ki her kelimenin ilk harfi büyük olmalı (GetMusteriBilgi)

4. Parametreler küçük harfle başlamalı, sonra gelen tüm kelimelerin ilk harfleri büyük olmalı. (subeKod, opAdi)

5. private değişkenler class'ların ilk başında tanımlanmalı.

6. property içinde kullanılan değişkenler '_' karakteriyle başlamalı.

7. Class isimleri ile dosya isimleri aynı olmalı. (SCommon, SCommon.cs)

8. Method'lar ve class'lar da kod açıklamaları olmalı.(/// Bu method…..)

9. Oluşturulan Rapor dosyalarının path'leri `CommonFunction.GetReportDirectoryPath`, kullanılan şablonların path'leri ise, `CommonFunction.GetTemplateDirectoryPath` kullanılarak alınmalıdır.

## Forms

1. Forms isimlendirmeleri `Modul[.AltModül].Forms.kisa_ad`. (Musteri.Kisi.Forms.kshvz)

2. Forms yardımcı class'lar `F[kisa_ad]` şeklinde verilmiş mi? (FKshvz)

3. Design'ların olduğu class'lar `Frm[kisa_ad]` şeklinde verilmiş mi? (FrmKshvz)

4. Interface'i çağıran tüm kodlarda `if(hata!=null)` kontrolü yapılmış mı?

5. Forms'lar daki DML işlemlerinde DMLManager kullanılmalı.

6. Ekran üzerinde ki kontrollerin ilk harfleri Büyük harfle başlamalı. ('Sorgula', 'Arama Yap')

7. DataGridView'e çift tıklandığında düzeltme yapılmalı.

8. Form'ların size'leri 770x700'den büyük olamaz.

9. Form'ların AutoScroll özelliği true olmalı.

10. Form'larda sql kullanılmamalı

11. Service, Business ve Util.DataAccess dll'leri referans edilmemeli.

12. Assembly ve file versiyonları verilmeli.

13. kul_ekran tablosuna kayıt edilirken versiyon belirtilmeli.

14. Sadece ortak ikonlar kullanılmalı

15. Form'un Text property'si kul_ekran.menudeki_adi ile aynı olmalı

16. kul_ekran.menudeki_adi Büyük karakter ile başlayıp küçük karakterlerle devam eden şeklinde olmalı

17. Ekranlarda uc'ler dışında kontrol kullanılmamalı.

## Service

1. Service'ler de try-catch'ler düzgün biçimde yazılmış mı?

2. sMan using ile kullanılmış mı?

3. string hata variable'ı null atanıp, daha sonra exception'da message'a eşitlenmiş mi?

4. Tüm methodlar string döndürmeli.

5. Servis isimlendirmeleri `Modul.Service` biçiminde yapılmış mı? (Common.Service)

6. Service'de ki class'ın ismi 'S' harfi ile başlamalı. (SCommon)

7. SP çağrılmaları `sMan.ExecuteSP` şeklinde olmamalı, SPBuilder'dan SP dll'i oluşturulmalı.

8. Servislerde class bazlı değişken kesinlikle tanımlanmamalı, tüm tanımlamalar metodlar içinde olmalı

## Interface

1. Interface ismi `Modul.Interface` biçiminde mi?

2. Interface'te ki class ismi 'I' harfi ile başlamalı.