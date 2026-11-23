# Changelog

Bu dosyanın biçimi [Keep a Changelog](https://keepachangelog.com) kurallarına uyar.
Uygulamanın içindeki Sürüm notları ekranı bu dosyadan beslenir. 0.5.0 ve öncesi için not tutulmadı.

Sürüm numarası 0.8.5.3 gibi dört parçalıdır: dönem (ilk GitHub yayını 1.0.0), büyük güncelleme, çalışma günü, o günün derlemesi.
Yeni maddeler [Unreleased] altına yazılır; `tools\yayinla.ps1` onları günün sürümüne taşır ve sonlarına geldikleri derlemeyi ekler.
Program derleme numarasını göstermez. 1.6–1.8 diye çıkan eski sürümler 0.6–0.8 olarak yeniden adlandırıldı.

## [Unreleased]

## [0.9.0] - 2026-11-23

### Added

- ilk açık yayın: kaynak kod ve taşınabilir exe GitHub'da (ön sürüm) [0.9.0.1]
- Ayarlar: sürüm numarasının altında "created by antagonist.g"; exe'nin özelliklerinde de aynı ad [0.9.0.1]

### Changed

- ilk adımlar tanıtımı: uygulamayı ilk kez açan kullanıcıda kendiliğinden başlar; son adımı diğer ekranların davetlerini anlatır, onlar davetle gelmeye devam eder [0.9.0.3]

## [0.8.6] - 2026-11-23

### Changed

- yıl görünümünde açık temada her ay hafif gri bir kartta durur, beyaz gün kareleri zeminden ayrılır [0.8.6.1]
- takvim bölümündeki hafta seçicide her hafta, ay ve yıl seçicideki gibi beyaz bir şerit; hafta numarası solda grinin üstünde, gösterilen hafta turkuaz çerçeveli [0.8.6.3]
- ay ve hafta görünümleri yıl görünümüyle aynı düzende: takvim gri bir kartta, her gün beyaz kutu (açık tema); hafta görünümünde boş günler de kutu olarak görünür [0.8.6.4]
- bitmeyen ama alt işi biten işler "ilerledi" diye sayılır: başlıklarda "2 yapıldı, 1 ilerledi, 6 tamamlanmamış", takvim özetinde "· 1 ilerledi"; Bugün çubuğunda o gün biten alt işlerin payı sarı dilim olarak görünür [0.8.6.7]
- tepsi simgesinde uygulama simgesi biraz küçülüp sol üste alındı, sayı sağ alt köşede daha az yer kaplar [0.8.6.8]
- tepsi panelinde açıklaması olan iş ve alt işlerin yanında küçük bir not simgesi durur (açıklamanın kendisi gösterilmez) [0.8.6.8]
- tepside açık iş varken simgenin tamamı turuncu bir kare olur ve sayı içine sığabilecek en büyük boyda yazar; iki basamaklı sayılar da okunur [0.8.6.9]
- ayarlar sayfaları yenilendi: gri zemin üstünde beyaz kartlar, her ayar bir satır; adı ve açıklaması solda, açma/kapama anahtarı ya da seçici sağda; benzer ayarlar küçük başlıklarla gruplu [0.8.6.9]
- tepside açık iş sayısı artık uygulamanın kendi takvim simgesinin içinde, tikin yerinde yazar; dar yazı tipiyle rakamlar daha büyük [0.8.6.10]
- iş olan güne atlama artık yalnızca Alt+←/→ (Ctrl+Shift+←/→ taşımaya geçti) [0.8.6.12]
- listede Del ile silmeden önce soruluyor; Enter siler, Esc vazgeçer, Ctrl+Z yine geri alır [0.8.6.13]
- kısayollar penceresinde tuş birleşimleri tek tuşta artıyla yazılıyor (Alt+↑, Ctrl+Shift+→) [0.8.6.13]
- kısayollar penceresinde parantezli ek bilgiler (Ctrl+Z geri alır, Shift+Enter da olur) açıklamanın altında küçük soluk satıra indi [0.8.6.14]
- sayfalar arasında geçiş hızlandı: her sayfa satırlarını saklıyor, geri dönünce yalnız değişen satır yenileniyor (gerçek dosyada Bugün ↔ Tümü 409 ms → 92 ms) [0.8.6.15]
- iş satırlarındaki metinler artık görünmeyen kaydırma çubukları kurmuyor [0.8.6.15]
- sağ panelde saat tek satır ("Saat 14:00", altında hatırlatmalar); basınca kendi sayfası açılır, ‹ ya da Esc ile işe dönülür [0.8.6.18]
- sağ panelde Taşı düğmeleri ve Sil / İşi iptal et altta sabit, aralarında çizgi; panelin başlığında "İş" yerine işin günü (Bugün · 23 Kasım Pazartesi) [0.8.6.18]
- sağ panelde açıklaması olan alt işin yanında tepsi panelindeki gibi küçük not simgesi [0.8.6.19]
- gün listesinde biten ve iptal edilen işler en altta, ince bir çizginin altında "✓ 4 iş bitti" satırında toplanır; kapalı gelir, basınca açılır [0.8.6.20]
- alt işler kapalı gelir (sayısı ve ok görünür), ana işin açıklaması ise kapalıyken de görünür; açtığın iş sonraki açılışta da açık kalır [0.8.6.20]
- takvimdeyken İşler düğmesi doğrudan Bugün sayfasını açar [0.8.6.20]
- listede Shift+Enter seçili işi seçime ekler ya da çıkarır (Ctrl+tık gibi) [0.8.6.20]
- gün başlığının altında yalnız tarih kalır; sayılar ilerleme çubuğunun yanında küçük renkli etiketler: "1 ilerledi", "2 tamamlanmamış", "1 iptal" (yer yoksa çubuğun altına iner). Çubuğun olmadığı günlerde "✓ 2 yapıldı" da etiket olur [0.8.6.22]
- sürüm notları penceresi yenilendi: solda sürümler, sağda Ayarlar gibi gri zeminde beyaz kartlar; her madde kalın kısa başlık ve altında soluk ayrıntı, bölümler renkli (Yeni turkuaz, Değişen sarı, Düzeltilen yeşil) [0.8.6.23]
- ayarlarda hatırlatma düzeni: Gün sonu ve Saatli işler ayrı kartlarda; yeni saatli işin iki hatırlatması saat gibi ‹ › ile seçilir (Yok, Tam saatinde … 1 gün) [0.8.6.24]
- gün sonu saati 15 dakikalık adımlarla: 12:00 ile 23:45 arası [0.8.6.24]
- bütün işleri bitmiş gün katlanmaz: bitenler doğrudan listelenir [0.8.6.24]
- başka günün işine basınca doğrudan o iş: gün açılır, iş seçili gelir ve görünene kadar kaydırılır; bitenler listesindeyse liste de açılır [0.8.6.25]
- saat yerine hatırlatıcı: sağ panelde "Hatırlatıcı · 14:00" ve "Hatırlatıcı ekle", ayarlarda Hatırlatıcılar [0.8.6.26]
- biten işe hatırlatıcı eklenmez: biten ya da iptal edilen işte satır görünmez, kurulu olan kaldırılabilsin diye kalır [0.8.6.26]
- adım adım tanıtım parça parça: bir ekrana ilk gelince sağ üstte "✦ Takvimi tanı · 3 adım" daveti çıkar, basınca o ekran birkaç adımda anlatılır; ✕ geçer [0.8.6.28]
- tanıtım parçaları: ilk adımlar, satırlar, sağ panel, hatırlatıcı, Tamamlanmamış, Gelecek ve Tarihsiz, Tümü, takvim, dosyalar, klavye [0.8.6.28]
- Ayarlar › Yardım: Tanıtım davetleri anahtarı ve Tanıtımı sıfırla; Adım adım tanıtım bulunduğun ekranın parçasını başlatır [0.8.6.28]
- kullanım kılavuzu yenilendi: hızlı ekleme, sağ panel, takvim (hafta, ay, yıl), hatırlatıcılar, klavyeyle çalışmak ve Drive ile eşitleme bölümleri eklendi; diğer bölümler güncel [0.8.6.29]
- kılavuzun bütün görüntüleri yeni arayüzle, açık ve koyu temada yeniden çekildi [0.8.6.29]
- kılavuzdaki küçük pencere şeması yeni düzende: solda şerit ve liste, ortada işler, altta ekleme kutusu; okunan bölümün yeri yanar [0.8.6.30]

### Fixed

- yıl görünümünde geniş pencerede gün kareleri basık şerit gibi uzuyordu; artık pencereye göre büyüyüp kareye yakın kalır ve yıl kaydırmadan sığar [0.8.6.2]
- Ayarlar'dan açılan kısayollar penceresi kapanınca Ayarlar'a dönülür [0.8.6.12]
- görsel yazmada (kalemle düzenle, yeni iş kutusu) seçim satırlar arasında geçer: Ctrl+A iki kez bütün satırları seçer, Shift+↑/↓ ya da fareyle sürüklemek de seçer; seçili satırlar kopyalanır, kesilir, silinir ya da üstüne yazılır [0.8.6.21]
- sağ paneli tanı: davete basınca sağ panel kapanıyordu, tanıtım olmayan paneli anlatıyordu; panel artık açık kalır [0.8.6.31]
- tanıtım davetleri: davete basmak dosya listesini de kapatmaz; Tamamlanmamış daveti yalnızca kalan iş varken çıkar, boş ekranda olmayan Bugüne al düğmesini anlatmaz [0.8.6.32]

### Added

- Win+Shift+N kutusunda dosya seçimi: sağ üstteki dosya adına basınca (ya da Ctrl+D) dosyalar gruplarıyla listelenir; seçilen dosya hatırlanır, iş o dosyaya eklenir, açık dosya değişmez [0.8.6.5]
- sağ panelin sol kenarı sürüklenerek genişletilip daraltılabilir (280–760); genişlik hatırlanır, kenara çift tıklamak eski boyuna döndürür [0.8.6.6]
- kısayollar: Tab / Shift+Tab listede işten işe geçer (seçili iş turkuaz çerçeveli), F2 onu yerinde düzenler, Enter sağ panelde açar; Ctrl+Sol/Sağ boş günler dahil önceki/sonraki güne, Ctrl+Shift+Sol/Sağ iş olan güne geçer (takvimde hafta/ay/yıl); Ctrl+Tab İşler ile Takvim arasında geçer [0.8.6.11]
- listede klavyeyle çalışma: ↑/↓ bütün işler, Tab/Shift+Tab yalnız tamamlanmamışlar, →/← alt işlere in/çık, Boşluk tikler, F2 düzenler, Enter sağ panelde açar (seçim işte kalır), Del siler, Alt+↑/↓ sıra, Home/End, Shift+↑/↓ çoklu seçim; seçili iş turkuaz zemin ve sol çizgiyle görünür [0.8.6.12]
- klavyeyle taşıma: M menüsü (Bugüne, Yarına, Tarihsize, Gün seç; gün seçerken oklar küçük takvimde gezer, Enter taşır), Ctrl+Shift+←/→ bir gün öne/sonraya; Ctrl+↑/↓ sayfalar arasında geçer [0.8.6.12]
- kısayollar penceresi yenilendi: konulara göre sekmeler, tuşlar tuş kapağı görünümünde ve üstte bütün sekmelerde arayan arama kutusu [0.8.6.12]
- saatli hatırlatma: işe saat ve en çok iki hatırlatma (0–24 saat önce) verilir, vakti gelince tepsiden haber gelir; dosyada satır sonunda ⏰[14:00 30dk 15dk] [0.8.6.17]
- listede, hafta görünümünde ve tepsi panelinde saatli işin yanında çalar saatli etiket; saati geçince gri [0.8.6.17]
- sağ panelde SAAT ve HATIRLAT: hazır süreler (Tam saatinde … 1 gün) ve Özel…; altında soluk çalacağı saat [0.8.6.17]
- yazarken "toplantı 14:00" saati alır, varsayılan hatırlatmaları koyar (Ayarlar › Davranış) [0.8.6.17]

## [0.8.5] - 2026-11-20

### Changed

- yeni iş kutusu açıkken dışarıda herhangi bir yere (takvim, liste, sol menü) basınca kutu tek satıra küçülür, yazılanlar kalır; üzerine basınca kaldığı yerden açılır. Kutu takvimden gün seçerken takvime basmak yine gün seçer [0.8.5.1]
- yeni iş kutusundaki Vazgeç artık yazılanları siler ve kutuyu kapatır; saklayıp küçültmek için Esc ya da dışarı basmak yeterli [0.8.5.1]
- sürüm numarası yeni düzende: program henüz yayımlanmadığı için 1.8.5 artık 0.8.5; ilk GitHub yayını 1.0.0 olacak. Ayarlar'da üç parça görünür [0.8.5.1]
- ayarlar solda bölüm listesiyle düzenlendi (görünüm, davranış, eşitleme, yardım); pencere kısayken içerik kayar [0.8.5.2]
- hafta görünümünde bir işe tıklayınca o iş sağ panelde açılır, sayfa haftada kalır; işin yanındaki boş yere tıklamak günü açar [0.8.5.3]
- hafta görünümünde sağ panelde açık olan iş renkli zeminle belli olur [0.8.5.4]
- tepsi simgesindeki sayı büyüdü ve keskinleşti, 9+ yerine sayının kendisi yazar (99'a kadar) [0.8.5.6]
- tepsi panelinde yapılmış alt işler görünmez, her işin yalnızca ilk 3 açık alt işi listelenir; "+ 18 alt iş daha" o işi uygulamada açar [0.8.5.7]
- işaretlenen alt iş, açıklamasıyla birlikte diğer alt işlerin altına iner; kalan işler üstte durur [0.8.5.8]
- farklılık ekranında daha yeni olan taraf renkli "Daha yeni" etiketi taşır, onun düğmesi dolu ve "önerilen" yazar [0.8.5.8]
- işareti kaldırılan alt iş bitmemişlerin hemen altına geri çıkar; iptal edilen alt iş de bitenler gibi alta iner [0.8.5.9]

### Fixed

- alt iş yazma kutusu başka bir yere basınca kapanıyor; yazılan kaybolmaz, o işte kutu yeniden açılınca geri gelir. Esc yazıyı bırakıp kapatır [0.8.5.1]

### Added

- tepsi simgesinin köşesinde bugünkü açık iş sayısı (9'dan fazlası 9+); fareyle üstüne gelince "bugün N açık iş" yazar [0.8.5.5]

## [0.8.4] - 2026-11-18

### Added

- görsel yazma: yeni iş kutusunda, kalemle düzenlemede ve Win+Shift+N kutusunda iş yazılırken listedeki gibi görünür; başlığın önünde kutucuk, açıklama soluk, alt işin kendi küçük kutucuğu. Shift+Enter alt satır açar, satırın başına "- " yazınca o satır alt iş olur, satırın başında Backspace geri alır; ＋ Açıklama ve ＋ Alt iş aynı satırları açar. Kalemle açılan işte bitmiş alt işler tikli görünür, kaydedince tikleri ve tarihleri kalır. Win+Shift+N kutusu da aynı görünür: ＋ Açıklama, ＋ Alt iş ve Ekle düğmeleri var, gün yazıdan okunur, Tarih değil yerinde
- Ayarlar › Yazma görünümü: Görsel ya da Metin; Metin eski düz yazıyı geri getirir (- ile alt iş). Seçim değişince yazılanlar kaybolmaz

- bitmiş işin ya da alt işin bitiş tarihi sonradan değiştirilebilir: sağ panelde işte BİTTİ satırında, alt işte DURUM altında "18 Kasım'da bitti" yazar; Tarihi değiştir'e basıp soldaki takvimden günü seçince tarih düzelir, takvim ve haftalık özet işi o güne sayar. İş yerinden oynamaz, alt işlerin tarihleri değişmez, bugünden sonraki gün seçilemez; Esc vazgeçer, Geri al geri getirir

### Changed

- sağ panelde uzun alt iş başlıkları üç satırdan sonra … ile kısalır; başlığa tıklayınca tamamı açılır ve düzeltilebilir
- tikleme, düzenleme, geri alma ve Drive'dan gelen değişiklikte pencere artık donmuyor: liste her seferinde baştan çizilmiyor, yalnızca değişen iş yeniden çiziliyor (Bugün'de bir tık 270 ms'den 5 ms'ye, Tümü'nde 800 ms'den 60 ms'ye indi)
- büyük dosyada da hızlı: iş satırları bir kez okunup hatırlanıyor, üç yıllık bir notta ekranı tazelemek 210 ms'den 46 ms'ye indi

## [0.8.3] - 2026-11-13

### Added

- bitiş tarihi: tiklenen iş ve alt iş bittiği günü yanına yazar (👍[Done 12/11]); tik kaldırılınca tarih silinir. Birkaç günde biten iş tek parça kalır ve 2/4, 4/4 diye ilerler; "Bugüne al" ile taşınsa da her gün kendi kaydını tutar: işin durduğu günde önceki günlerde biten alt işler tek satıra iner (✓ 2 alt iş 12 Kasım'da bitti, basınca açılır), önceki günün listesinde iş o akşamki hâliyle, yalnızca o gün bitenlerle ve devamı 13 Kasım'da ya da ✓ 13 Kasım'da bitti notuyla görünür. Takvimin renkleri ve haftalık özet işi ve alt işleri bittikleri güne göre sayar; eski notlardaki tarihsiz işaretler durdukları günde bitmiş sayılır
- dosya seçici: sol listenin en üstünde açık dosyanın adı ve bitmemiş iş sayısı; basınca bütün dosyalar gruplarıyla açılır, bir dosyaya basınca ona geçilir ve ortadaki ekran aynı kalır. Dosyalar ve gruplar soldaki noktalardan tutulup sürüklenerek sıralanır, mavi çizgi bırakılacak yeri gösterir; başka bir grubun altına bırakılan dosya o gruba taşınır. Kalem ya da F2 adı yerinde değiştirir (F2 farenin üstündeki satırı, fare listede değilse açık dosyayı), üç nokta ve sağ tık gruba taşı, gruptan çıkar, Not Defteri'nde aç ve sil seçeneklerini açar. ＋ Grup boş bir grup açar, grup son dosyası çıkınca da kalır; ✕ ile kaldırılır
- küçük takvimde ayın adına basınca on iki ay açılır; aya basınca o ayın günlerine dönülür, oklar yılı değiştirir, açık işi olan ayda nokta görünür
- Takvim bölümünün solunda Hafta / Ay / Yıl seçimi, on iki ay ve ayın kısa özeti (bitti, açık, iptal); aya basınca büyük takvim o aya gider
- büyük takvimde bir güne basınca o günün listesi İşler'de açılır; başlığın üstündeki Takvime dön aynı haftaya ya da aya geri götürür
- Ayarlar › Sol panel: Yeni ya da Klasik; Klasik seçilince eski tek parça sol menü geri gelir, seçim uygulama kapanıp açılınca da hatırlanır
- yeni iş kutusunda ＋ Açıklama ve ＋ Alt iş düğmeleri: açıklama ve alt işler eklemeden önce kutuda yazılır. Açıklamada Enter açıklamayı bitirir, bir daha Enter işi ekler; alt iş satırında Enter alt işi ekler, Esc alt işleri bitirir
- eski usulle ekleme: kutuda Alt+Enter ya da Shift+Enter yeni satır açar; "- " ile başlayan satırlar alt iş, altındaki girintili satır o alt işin açıklaması, düz satır işin açıklaması olur; gün yalnızca ilk satırdan okunur. Kopyalanan çok satırlı bir liste yapıştırılınca da bütün satırlar kalır

### Changed

- yeni iki katmanlı sol panel: en solda İşler, Takvim ve Ayarlar şeridi, yanında seçilen bölümün içeriği; Notlarda ara ve ＋ Yeni iş her bölümde en üstte. İşler'de küçük takvim her zaman görünür, bir işi sürükleyip güne bırakmak tek hareket. Önceki günlerden açık iş kalmışsa İşler simgesinde turuncu nokta görünür. Pencere daralınca yalnızca şerit kalır, bölüm orta alanın üstüne açılır; bir işi sürükleyip şeride getirmek İşler'i açar
- alttaki iş ekleme kutusu açılır oldu: tıklayınca ya da listede yazmaya başlayınca genişler; gün yazıdan okunur ya da gün düğmesiyle (Bugün, Yarın, Tarihsiz, Takvimden seç…) seçilir; Enter ekler, Esc ya da Vazgeç kutuyu kapatır ama yazılanlar kalır. İş başka güne gittiyse altta Yarına git gibi bir bağlantı çıkar. Alt işleriyle ekle ve Yapıldı olarak ekle düğmeleri ile Ctrl+Shift+Enter kaldırıldı; Ctrl+Enter işi yine yapıldı olarak ekler, Ctrl+N kutuyu açar (takvimdeyken önce Bugün'e geçer)
- sağ panelde Vazgeç yerine İşi iptal et, Alt işi iptal et ve İptali geri al; iptal edilen işte üstte bilgi şeridi görünür, Taşı düğmeleri simgeli oldu, alt iş panelinde DURUM satırı var. Başlıkta, açıklamada ve alt iş başlığında Enter kaydedip kutudan çıkar; açıklamada alt satır Alt+Enter ile
- takvim özetlerinde vazgeçildi yerine iptal edildi yazar
- yalnızca açıklaması olan işlerde, alt işli satırlardaki 0/2 sayısının yerinde küçük bir not simgesi var; okla birlikte açıklamayı açıp kapatır, katlıyken de altında not olduğu görünür
- alt işleri ve açıklamayı açıp kapamak artık takılmıyor: yalnızca o iş yeniden çizilir, bütün ekran değil
- başka bir günün listesine bakarken başlıktaki okların yanında Bugüne dön bağlantısı çıkar
- Takvim bölümünün sol listesi görünüme göre değişir: Hafta'da ayın haftaları yılın kaçıncı haftası olduğuyla ve günleri biten işe göre yeşil, Ay'da 12 ay, Yıl'da bu yıl ortada 3×3 yıllar; üçü aynı yükseklikte, geçerken alttaki özet yerinden oynamaz. Gelecek aylar ve yıllar soluk
- kalemle düzenleme dosyanın ham satırlarını değil, ekleme kutusundaki biçimi açar: ilk satır başlık, altı açıklama, - ile başlayan satır alt iş. Kaydedince alt işler işaretini ve bitiş tarihini korur (adı değişse ya da yeri kaysa da), yeni satır açık alt iş olur, silinen satır silinir; hiçbir şey değişmediyse dosyaya dokunulmaz. Ham metin için Not Defteri'nde aç

## [0.8.2] - 2026-11-12

### Added

- günü yazarak ekleme: yeni iş kutusunda ve Win+Shift+N kutusunda ilk satırın başına ya da sonuna yazılan gün okunur (yarın rapor, toplantı cuma, haftaya salı sunum, öbür gün, 15 kasım teslim, teslim 15.11); iş o güne eklenir, gün kelimeleri başlıktan çıkar. Kutunun altında Yarına eklenecek gibi bir satır görünür, Tarih değil'e basınca kelimeler başlıkta kalır. Alt işleriyle eklenen iş başka bir güne giderse pencere o güne geçer
- vazgeçildi durumu: yapılmayacak bir iş ya da tek bir alt iş silinmeden kapatılır. Panelin altındaki Vazgeç ile; dosyaya 🚫[İptal] yazılır. Üstü çizili ve çarpılı görünür, tamamlanmamışlardan ve ilerleme sayısından çıkar, günün en altına iner, haftalık özete girmez; iptal edilen işin alt işleri de çarpılı görünür. Yeniden aç ile geri gelir
- listede bir alt işe tıklayınca sağdaki panel o alt işi açar: başlığı ve açıklaması orada düzenlenir, üstteki bağlantıyla işin kendisine dönülür
- paneldeki alt işlerin başlığına tıklayıp yerinde düzeltilebilir; yanındaki ok alt işin açıklamasını açar
- sağ panelde alt işlerin sırası değişir: soldaki noktalardan tutup sürükleyince bırakılacak yer çizgiyle görünür, başlıktayken Alt+↑ / Alt+↓ bir sıra taşır; alt işin açıklaması onunla birlikte gider
- listede bir işin alt işleri katlanabilir: sağdaki 1/3 sayısına ya da okuna tıklayınca alt işler ve açıklama gizlenir, tekrar tıklayınca açılır; katlanan işler uygulama kapanıp açılınca da kapalı kalır
- hata günlüğü: beklenmeyen hatalar, kaydedilemeyen değişiklikler ve Drive hataları ayarların yanındaki hatalar.log dosyasına saat ve sürümle yazılır; aynı hata art arda bir kez yazılır, dosya büyümez. Ayarlar › Yardım › Hata günlüğü Not Defteri'nde açar
- gün sonu hatırlatması: seçilen saatte (varsayılan 17:30) bugün açık iş kaldıysa sağ altta Windows bildirimi çıkar, günde bir kez; bildirime tıklayınca bugünün işleri açılır. Ayarlar'dan kapatılır, saati yarım saatlik adımlarla değişir
- Win+Shift+N ile her yerden hızlı ekleme: hangi programda olursan ol, uygulama tepsideyken de küçük bir kutu açılır, yazdığın iş açık dosyada bugünün altına eklenir; Ctrl+Enter yapıldı olarak ekler, - ile başlayan satır alt iş olur, kutunun dışına tıklayınca yazdığın kaybolmaz
- takvimin hafta görünümünde bir işi tutup başka bir günün üstüne sürükleyerek o güne taşıma; hedef gün vurgulanır, Geri al ile döner
- Bugün başlığının altında, mobildeki gibi bugünün işlerinden kaçının bittiğini gösteren çubuk ve yanında 5/9 gibi sayı
- otomatik günlük yedek: her gün notların bütün .txt dosyaları klasör yapısıyla bu bilgisayarda ayrı bir klasöre kopyalanır, son 30 gün saklanır; Drive'a gitmez. Ayarlar › Dosyalar › Yedekleri aç
- takvimin hafta görünümünde Özeti kopyala: o haftada biten işler gün gün, biten alt işleriyle düz metin olarak panoya kopyalanır; yarım işler 2/5 gibi sayıyla gelir, açık işler ve boş günler girmez
- takvimde Hafta, Ay ve Yıl görünümleri: hafta, pencere en az 1360 genişlik ve 700 yükseklikteyse yedi günü yan yana sütunlarda, daha küçükse alt alta gösterir; uzun iş başlıkları sütunda üç, listede iki satırda kısalır, tamamı fareyle üstüne gelince görünür; güne tıklayınca o gün açılır; yıl on iki küçük ayı gösterir, ayın adı o ayı, gün o günü açar; başka haftaya, aya ya da yıla geçince Bu hafta, Bu ay, Bu yıl düğmesi geri getirir

### Changed

- masaüstü uygulaması .NET Framework 4.0 yerine 4.8 için derleniyor; Windows 10 ve 11'de 4.8 hazır geldiği için kurulum gerekmez, uygulama yine tek exe. Google Drive bağlantısı artık Windows'un izin verdiği en güncel TLS sürümünü kullanır
- satırlardaki alt iş ekleme ve silme düğmeleri ile alt iş satırlarındaki düzenle ve sil düğmeleri kaldırıldı, bunlar artık sağdaki panelde; satırda yalnızca kalem kaldı
- panel yalnızca işi listede görünürken açık kalır: panelin ve iş satırlarının dışında herhangi bir yere (sol menü, başlık, alttaki kutu, boş liste alanı) tıklayınca ya da başka ekrana, güne veya dosyaya geçince kapanır; Takvimden seç ile gün seçerken açık kalır
- Ayarlar, Kısayollar, Sürüm notları, dosya adı sorusu ve silme onayı kutunun dışındaki koyu alana tıklayınca kapanır; soru kutularında bu Vazgeç sayılır. Drive çakışma penceresi karar verilene kadar açık kalır
- pencere liste ile paneli yan yana sığdıramayacak kadar daralınca panel listenin üstüne açılır, liste ve alttaki düğmeler sıkışmaz
- Cumartesi ve Pazar takvimlerde kırmızı görünür
- sağ paneldeki Taşı düğmeleri eşit genişlikte iki sıra hâlinde dizildi: Bugüne al ve Yarına üstte, Tarihsize al ve Takvimden seç altta
- pencere kapandığı ya da tepsiye küçüldüğü yerde, aynı boyutta ve tam ekransa tam ekran açılır; o ekran artık bağlı değilse görünen ekrana sığdırılır
- takvimde son seçilen Hafta, Ay ya da Yıl görünümü uygulama kapanıp açılınca da hatırlanır
- fareyle üstüne gelince çıkan ipuçları uygulamanın renklerinde, köşeleri yuvarlak küçük bir kart oldu; uzun yazı ekran boyunca tek satır yerine alt satıra geçer, takvimdeki iş başlıkları işin hemen altında açılır

### Fixed

- alt iş açıkken üstte ana işin adı tek satıra kesiliyordu; artık iki satıra kadar görünür
- hafta görünümünde işler dosyadaki sırayla gösteriliyordu; dosya başka bir yerden değiştirildiyse bitmiş iş yarım işin üstünde kalabiliyordu. Artık her gün yarım, yapılmamış, bitmiş sırasıyla görünür
- ekranda beklenmeyen bir hata olunca uygulama kapanıyordu; artık açık kalır, yarım kalan değişiklik kaydedilmeden ekran dosyadan yeniden yüklenir
- Geri al, Drive eşitlemesi ya da dosyanın dışarıdan değişmesi sağ paneli kapatıyordu; artık panel aynı işi bulup açık kalır
- sağ panel açıkken Bugün başlığının altındaki ilerleme sayısı kesiliyordu
- iki monitörde pencere ana monitöre göre boyutlanıyordu; artık farenin bulunduğu monitörde açılır ve o ekrana sığar
- sol menü uzayınca çıkan kaydırma çubuğu içeriği sola itiyordu; artık içeriğin üstünde durur, sağ paneldeki çubuk da öyle

## [0.8.1] - 2026-11-11

### Added

- bir işe tıklayınca sağda açılan panel: başlık, açıklama ve alt işler burada düzenlenir, alt iş eklenip silinir, iş Bugüne, Yarına, tarihsize ya da takvimden bir güne taşınır, silinir; liste yanında görünmeye devam eder, Esc kapatır. Ctrl ve Shift ile tıklamak eskisi gibi seçer

### Changed

- dosya satırındaki çarpı, İş Günlüğü klasöründeki bir dosyayı sorduktan sonra gerçekten siler; son hali yedeklerde kalır, Drive bağlıysa Drive'dan ve telefondan da kalkar. Klasör dışından eklenmiş dosyalar eskisi gibi yalnızca listeden çıkar

### Fixed

- not eklerken ya da değiştirirken Drive eşitlemesi ya da OneDrive dosyayı aynı anda okuyunca kayıt bir anlığına hata verebiliyordu; artık eşitleme kaydın bitmesini bekliyor, başka bir program dosyayı tutuyorsa kayıt kısa aralıklarla yeniden deneniyor

- bitmiş bir iş, sol menüdeki Tarihsiz başlığına ya da tarihsiz listesine sürüklenerek tarihsiz işlere taşınabiliyordu; artık yapıldığı günde kalır ve nedeni altta yazar

- 768 piksel yüksekliğindeki ya da büyütülmüş ekranlarda pencere üstü kesik açılıyordu; artık ekrana sığacak boyutta ve ortalanmış açılır, alttaki düğmeler dar pencerede alt satıra geçer

## [0.8.0] - 2026-11-11

### Added

- ilk açılış ekranları yenilendi: yeni kullanıcı Hızlı başlangıç ile ilk dosyasını oluşturur ve isterse Google Drive'ı bağlar, eski kullanıcı dosyalarını Drive'dan ya da bilgisayardan getirir; her adımda Geri vardır

## [0.7.1] - 2026-11-10

### Added

- Bir anda çok sayıda dosya silinmiş görünürse silme karşı tarafa geçmez; dosya adları gösterilir, Geri getir ya da Silinsin seçilir

### Changed

- Drive program açıkken 3 saniyede bir ve pencereye her dönüldüğünde kontrol edilir, telefondaki değişiklikler kendiliğinden gelir
- Drive'dan gelen değişiklikte alttaki mesaj artık Dosya Drive'dan güncellendi yazar

## [0.7.0] - 2026-11-10

### Added

- Google Drive eşitleme: Ayarlar'dan Google hesabı bağlanır, notlar Drive'daki İş Günlüğü klasörüyle ve telefon uygulamasıyla ortak kullanılır; klasör ve dosya düzeni aynı kalır
- iki yerde de değişen dosya için Farklılık bulundu ekranı; iki sürümün tarihi ve saati ile yalnızca farklı satırlar görünür, hangisinin kalacağını kullanıcı seçer

## [0.6.0] - 2026-11-04

### Added

- ayarlardan açılan sürüm notları ekranı
- Gelecek ve Tarihsiz ekranları; Gelecek ileri tarihli işleri, Tarihsiz günü belli olmayan işleri gösterir
- bitmemiş her şeyi başlıklara ayıran Tümü ekranı; bütün başlıklar açılır kapanır
- önceki günlerden kalan işi bugüne çekmek için satırda Bugüne al, ekranın üstünde Hepsini bugüne al düğmesi
- soldaki noktaların menüsüne Yarına taşı ve Tarihsiz yap seçenekleri
- Gelecek'e sürüklenen iş yarına, Tarihsiz'e sürüklenen iş tarihsize geçer
- Tamamlanmamış, Gelecek ve Tümü ekranlarında sürükleyerek sıra değiştirme
- sürüklerken imleç listenin üst ya da alt kenarına gelince liste kendiliğinden kayar, ekrana sığmayan işlere de ulaşılır
- Bugün, takvimden açılan günler ve Tamamlanmamış, Gelecek, Tarihsiz, Tümü ekranlarında toplu taşıma: Ctrl+tık, Shift+tık veya Hepsini seç ile seçilen işler birlikte sürüklenir ya da alttaki çubuktan bugüne, yarına veya tarihsize taşınır
- bitmiş ya da bitmemiş işi başka güne taşıma: takvimde bir günün üstüne sürüklenir ya da Başka güne taşı seçilip takvimden gün tıklanır; geçmiş ve ileri günler için çalışır, seçili işler birlikte gider
- sistem tepsisindeki simgeye tıklayınca bugünün yarım ve yapılmamış işlerini gösteren panel; işler ve alt işler buradan tiklenebilir, çift tık uygulamayı açar
- görsellerle ve mini haritalarla anlatılan kullanım kılavuzu; Ayarlar'dan uygulamanın içinde açılır, solda içindekiler ve okunan bölümü gösteren harita durur
- dosya grupları; her grup diskte bir klasördür
- dosyaları uygulamanın içinden yeniden adlandırma
- dosyaların klasörünü Gezgin'de açan düğme
- ayarlardan metin belgesi ekleme; dosyanın kopyası alınır, orijinaline dokunulmaz
- alt iş kutusunda Ctrl+Enter ile tikli alt iş ekleme

### Changed

- Açık işler ekranının adı Tamamlanmamış, Açık iş ekle düğmesinin adı İş ekle oldu
- sol menü yeniden düzenlendi: Tamamlanmamış, Gelecek, Tarihsiz ve Tümü açılır kapanır İşler grubunda, Takvim onların altında
- Bugün ekranı artık sadece bugünün işlerini gösterir
- gün listelerinde yarım kalanlar üstte, bitmemişler ortada, tamamlananlar altta durur; yeni ve durumu değişen iş kendi grubuna gider, elle taşınan iş bırakıldığı yerde kalır
- ekrandaki sıra artık metin dosyasındaki sırayla aynı; eski günler bu sürüm ilk açıldığında bir kez dosyada da sıralanır, önceki halleri yedekler klasöründe saklanır
- notlar Belgeler içindeki İş Günlüğü klasöründe tutulur
- yeni dosya oluştururken konum sorulmaz, sadece ad sorulur
- sol taraf pencere kısayken kayar, böylece dosyalar sıkışmaz
- kısayollar menü sırasına göre değişti: Ctrl+1 Bugün, Ctrl+2 Tamamlanmamış, Ctrl+3 Gelecek, Ctrl+4 Tarihsiz, Ctrl+5 Tümü, Ctrl+6 Takvim
- satırlardaki noktalara ve dosyalara tıklayınca açılan menü uygulamanın görünümüne ve temasına uyduruldu
- adım adım tanıtım (eski adıyla kılavuz) baştan yazıldı; anlattığı ekranı açar, Geri düğmesi ve ok tuşlarıyla gezilir
- adım adım tanıtım görselleşti: anlatılan yer dışında pencere kararır, kartta ilerleme çubuğu ve her adımda küçük bir çizim var
- yedek dosyalarının adına klasör adı eklendi, böylece farklı gruplardaki aynı adlı dosyaların yedekleri karışmaz

### Fixed

- işi düzenlerken Enter alt satıra geçiyordu, artık kaydediyor; alt satır için Alt+Enter kullanılır
- Ctrl+Enter her kutuda aynı anlama geliyor: yazarken tikli ekler, düzenlerken kaydedip tikler
- Tümü ekranında sürükleyerek yer değiştirme çalışmıyordu
- Bugün ve takvimden açılan günlerde farklı durumdaki işler arasına sürüklenen iş eski yerine dönüyordu, artık bırakıldığı yerde kalıyor
- sürüklenen iş kendi satırına bırakılınca listenin sonuna gidiyordu, artık yerinde kalıyor
- Bugün'deki iş sol menüde Bugün'e, tarihsiz iş sol menüde Tarihsiz'e bırakılınca da listenin sonuna gidiyordu, artık yerinde kalıyor
- Insert tuşuna kazara basılınca araya yazılan harfler sağdaki yazının üstüne yazılıyordu; Insert artık yazma kutularında bu modu açmıyor

### Removed

- Dosyalar bölümündeki Var olan dosyayı ekle düğmesi; metin belgesi eklemek artık Ayarlar'da
