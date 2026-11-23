using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using static IsGunlugu.GuideParts;

namespace IsGunlugu;

/// <summary>What the guide says. The pictures are made by docs\kaynak\kilavuz-yap.ps1 from made-up notes.</summary>
public sealed partial class UserGuideView
{
    private void WriteContent()
    {
        Section("Dosyan ve uygulama", "ContentScroll");
        Lead("İş Günlüğü, günlük notlarını tuttuğun düz metin dosyasını okunaklı bir iş listesine çevirir. Dosya senindir: uygulamada yaptığın her şey aynı dosyaya yazılır, Not Defteri'nde de açabilirsin.");
        Add(FileAndList());
        P("Her satır karşısındakine dönüşür. Alt işlerden biri bitip biri kalınca üstteki iş sarı olur; bu durum dosyaya ayrıca yazılmaz, alt işlerden okunur.");
        Add(Table(new[] { "Dosyada", "Anlamı" },
            R("`----04/11/2026`", "Gün başlığı. Altındaki satırlar o güne aittir."),
            R("`🚨iş`", "Bitmemiş iş."),
            R("`👍[Done 12/11]iş`", "Bitmiş iş. Tarih, işin bittiği gündür; işin durduğu günden farklı olabilir."),
            R("`🚫[İptal]iş`", "Vazgeçilen iş. Silinmez, üstü çizili kalır, sayılmaz."),
            R("`iş ⏰[14:00 30dk 15dk]`", "Hatırlatıcılı iş: saati 14:00, 30 ve 15 dakika önce haber verilir."),
            R("`⇥🚨alt iş`", "Sekmeyle başlayan işaretli satır, üstündeki işin alt işidir."),
            R("`⇥⇥açıklama`", "İki sekmeyle başlayan satır, alt işin açıklamasıdır. İşin altındaki düz satır işin açıklamasıdır."),
            R("`----tarihsiz`", "Günü belli olmayan işlerin başlığı. Dosyanın sonunda durur.")));

        Section("İlk beş dakika", "DraftBox");
        Add(Steps(
            "**Dosyanı oluştur.** İlk açılışta ~Yeni dosya oluştur~'a bas, ad ver. Elinde not dosyası varsa ~Metin belgesi ekle~ ile kopyasını al.",
            "**Bugünün işini yaz.** Alttaki kutuya yaz, [[Enter]]'a bas. Bitirdiğin bir işi kaydediyorsan [[Ctrl+Enter]].",
            "**Bitince tikle.** Satırın başındaki kutuya bas. Biten iş en alttaki ✓ satırına iner.",
            "**Yarın geri gel.** Bugün bitmeyen iş ertesi gün Tamamlanmamış'a düşer. Oradan ~Bugüne al~ ile çekersin.",
            "**Kapatma, küçült.** Çarpıya basınca uygulama sistem tepsisine iner. Simgesine tıklayınca bugünün kalan işleri açılır."));
        Add(Note("Bir ekrana ilk geldiğinde sağ üstte küçük bir davet görürsün: ~✦ Takvimi tanı · 3 adım~. Basınca o ekran birkaç adımda anlatılır. Davetleri ~Ayarlar → Yardım~'dan kapatır ya da ~Tanıtımı sıfırla~ ile en baştan alırsın."));

        Section("Pencerenin haritası", "SearchBox", "TodayNav", "TaskGroupButton", "OpenNav", "FutureNav", "UndatedNav", "AllNav", "CalendarNav", "MiniCalendar", "FilesPanel", "ContentScroll", "DraftBox");
        P("Soldaki şeritte İşler ve Takvim, yanındaki listede ekranlar durur. Ortada işler, altta yenisini yazdığın kutu, sağda seçtiğin işin paneli.");
        Add(Shot("today"));
        Add(Table(new[] { "Yer", "Ne var" },
            R("Şerit", "İşler ve Takvim düğmeleri, en altta Ayarlar. İşler'deki turuncu nokta önceki günlerden kalan iş var demektir."),
            R("Dosya düğmesi", "Açık dosyanın adı; basınca bütün dosyalar ve gruplar açılır."),
            R("Arama", "Bütün günlerde ve alt işlerde arar, Türkçe harf farkına takılmaz. [[Ctrl+F]]"),
            R("Ekranlar", "Bugün, Tamamlanmamış, Gelecek, Tarihsiz, Tümü. Yanlarındaki sayı bitmemiş iş sayısıdır."),
            R("Küçük takvim", "Güne tıklayınca o gün açılır; işi üstüne sürükleyince o güne taşınır."),
            R("Başlık", "Ekranın adı, tarih, ilerleme çubuğu ve sayılar: ilerledi, tamamlanmamış, iptal."),
            R("Yeni iş kutusu", "İçindeki yazı işin hangi güne ekleneceğini söyler. Sağında ~Kısayollar (F1)~.")));
        Add(Note("Pencere daralınca liste şeride sığar; şeritteki düğmeye basınca liste ortanın üstünde açılır. Eski düzeni istersen ~Ayarlar → Görünüm → Sol panel → Klasik~."));

        Section("İş eklemek", "DraftBox");
        P("Altta, çerçeveli tek satır: ~Bugüne iş ekle…~. Tıklayınca ya da listede yazmaya başlayınca açılır. İşin adını yaz, [[Enter]]'a bas; iş eklenir, kutu yine tek satıra iner. [[Ctrl+Enter]] işi yapıldı olarak ekler.");
        P("Açılan kutunun altında üç düğme var: gün, ~＋ Açıklama~ ve ~＋ Alt iş~. Gün düğmesi işin gideceği günü gösterir; basınca ~Bugün~, ~Yarın~, ~Tarihsiz~ ve ~Takvimden seç…~ çıkar. [[Esc]] ya da dışarı basmak kutuyu küçültür, yazdıkların kalır; ~Vazgeç~ siler.");
        Sub("Görsel yazma");
        P("Kutu, işi listede görüneceği gibi gösterir: başlığın önünde kutucuk, açıklama soluk, alt işler kendi küçük kutularıyla. [[Shift+Enter]] alt satır açar; satırın başına `- ` yazarsan alt iş olur, satırın başında [[Backspace]] geri alır. Kopyaladığın bir listeyi yapıştırırsan satırlar aynı kurala göre ayrılır.");
        Add(Tree("Müşteri sunumunu hazırla", "  Perşembe gösterilecek", "- Grafikleri güncelle", "- Demo akışını dene"));
        P("Birden çok satırı seçmek için [[Ctrl+A]]'ya iki kez bas: ilki bulunduğun satırı, ikincisi hepsini seçer. Satırlar arasında fareyle sürüklemek ya da satırın kenarında [[Shift+↑]]/[[Shift+↓]] de seçer. Seçili satırları kopyalar, keser, siler ya da üstüne yazarsın.");
        Add(Note("Düz metin kutusunu (Metin) istersen ~Ayarlar → Görünüm → Yazma görünümü~'nden seç. Orada - ile başlayan satır alt iş, girintili satır onun açıklaması olur."));
        Sub("Günü ve saati yazarak");
        P("İlk satırın başına ya da sonuna bir gün yazarsan iş o güne gider, gün kelimeleri başlıktan çıkar: `yarın rapor`, `toplantı cuma`, `haftaya salı sunum`, `15 kasım teslim`, `teslim 15.11`. Bir saat de yazarsan (`yarın 14:00 toplantı`) işe hatırlatıcı kurulur: saati ve varsayılan iki hatırlatma, 30 ve 15 dakika önce.");
        Add(Table(new[] { "Açık ekran", "Yazdığın iş" },
            R("Bugün, Tamamlanmamış, Tümü", "Bugüne"),
            R("Takvimden açılan gün", "O güne, geçmiş ya da ileri fark etmez"),
            R("Gelecek", "Yarına"),
            R("Tarihsiz", "Tarihsizlere")));

        Section("Her yerden hızlı ekleme", "DraftBox");
        P("[[" + GlobalHotKey.Keys + "]] hangi programda olursan ol küçük bir kutu açar. Yaz, [[Enter]]; iş bugüne eklenir, kutu kapanır. Yazdığın gün ve saat burada da okunur; kelime gün değilse altta çıkan ~Tarih değil~'e bas.");
        Add(Shot("hizliekle", 520));
        P("Birden çok not dosyan varsa sağ üstte dosya adı durur: basınca ya da [[Ctrl+D]] ile işin gideceği dosyayı seçersin. Son seçtiğin hatırlanır.");

        Section("Bir satırda neler var", "ContentScroll");
        Add(Shot("satir"));
        Add(Table(new[] { "Parça", "Ne yapar" },
            R(StateCell(State.Open, "Boş kutu"), "Bitmemiş iş. Tıklayınca biter."),
            R(StateCell(State.Partial, "Sarı kutu"), "Yarım iş: alt işlerin bir kısmı bitmiş, yanında 1/3 gibi sayı yazar. Tıklayınca kalan alt işler de biter."),
            R(StateCell(State.Done, "Yeşil tik"), "Bitmiş iş. Tıklayınca yeniden açılır."),
            R("1/3 ve ok", "Alt işler kapalı gelir, ana işin açıklaması görünür kalır. Basınca açılır; açtığın iş sonraki açılışta da açık kalır."),
            R("⏰ 14:00", "Hatırlatıcının saati. Saati geçince ya da iş bitince gri olur."),
            R("İşe tıklamak", "Sağda paneli açar."),
            R(IconCell("IconEdit", "Kalem"), "İşi satırın içinde, ekleme kutusundaki gibi düzenler. [[Enter]] kaydeder, [[Esc]] vazgeçer."),
            R("Bugüne al", "Başka güne ait bitmemiş işi bugüne taşır.")));
        P("Günün bitmiş ve iptal edilmiş işleri en altta, ince bir çizginin altında ~✓ 4 iş bitti~ satırında toplanır; kapalı gelir, basınca açılır. Günün bütün işleri bittiyse doğrudan listelenir.");
        P("Bir işin alt işi başka bir gün yapıldıysa o gün listenin altında ~18 Kasım'daki iş~ gibi bir satır çıkar. Basınca işin durduğu gün açılır ve iş seçili gelir.");
        var gripWords = new StackPanel();
        gripWords.Children.Add(Spaced(Text("Soldaki altı nokta", 18, semiBold: true), 0, 8));
        gripWords.Children.Add(Spaced(Text("Satırın en solundaki noktalar taşımanın tutamağıdır. Tutup sürüklersin; tıklarsan yandaki menü açılır.", 15), 0, 8));
        gripWords.Children.Add(Text("Menüde görünenler satıra göre değişir: örneğin bitmiş işte Bugüne al çıkmaz.", 15));
        var grip = new TwoColumns { Margin = new Thickness(0, 12, 0, 0) };
        grip.Children.Add(gripWords);
        grip.Children.Add(Shot("menu", 300));
        Add(grip);

        Section("Sağ panel", "ContentScroll");
        P("Bir işe tıklayınca sağda açılır. Panel açıkken listede başka işe tıklarsan ona geçer; boş bir yere tıklamak ya da [[Esc]] kapatır. Sol kenarından tutup genişletirsin, çift tık eski boyuna döndürür.");
        Add(Shot("panel", 380));
        Add(Table(new[] { "Panelde", "Ne yapar" },
            R("Üst yazı", "İşin durduğu gün: Bugün, Yarın ya da tarih."),
            R("Başlık ve açıklama", "Yaz, [[Enter]] kaydeder. Açıklamada [[Alt+Enter]] alt satır açar."),
            R("Hatırlatıcı", "~Hatırlatıcı ekle~ ya da ~Hatırlatıcı · 14:00~. Basınca kendi sayfası açılır."),
            R("Alt işler", "Ekle, tikle, sil. Noktalardan sürükleyerek ya da [[Alt+↑]]/[[Alt+↓]] ile sırasını değiştir. Bir alt işe basınca panel o alt işe geçer, ‹ ile dönersin. Açıklaması olan alt işin yanında küçük bir not simgesi durur."),
            R("Bitti", "Bitmiş işte ne zaman bittiği yazar. ~Tarihi değiştir~ ile küçük takvimden başka gün seçersin; iş yerinde kalır."),
            R("Taşı", "Bugüne al, Yarına, Tarihsize al, Takvimden seç…. Panelin altında sabit durur."),
            R("Sil ve İşi iptal et", "Silinen iş ~Geri al~ ile döner. İptal edilen iş silinmez, üstü çizili kalır; ~İptali geri al~ ile geri gelir.")));

        Section("Bir işin yolculuğu", "TodayNav", "OpenNav", "FutureNav", "UndatedNav");
        P("Ekranlar ayrı listeler değildir. Hepsi aynı dosyanın farklı görünümüdür, bir iş hangi ekranda olduğunu tarihinden alır.");
        Add(Journey());
        P("Oklardaki adlar, işi o yöne götüren düğme ya da olay. Tarihsiz bir işi tiklersen, ne zaman yaptığın kayıtta kalsın diye bitirdiğin günün altına yazılır.");

        Section("Ekranlar", "TodayNav", "TaskGroupButton", "OpenNav", "FutureNav", "UndatedNav", "AllNav");
        Screen("Bugün", "Sadece bugünün işleri: önce yarımlar, sonra bitmemişler, en altta ✓ satırında bitenler. Başlığın altındaki çubuk günün ne kadarının bittiğini gösterir; sarı dilim, alt işi bugün biten işlerin payıdır. Yanındaki etiketler: ilerledi, tamamlanmamış, iptal. Takvimden açtığın günler de aynı düzende görünür.",
            new[] { "TodayNav" }, Shot("bugun"));
        Screen("Tamamlanmamış", "Önceki günlerden kalan, bitmemiş işler. Her iş yazıldığı günün başlığı altında durur. Satırdaki ~Bugüne al~ tek işi, sağ üstteki ~Hepsini bugüne al~ hepsini çeker; tek ~Geri al~ hepsini geri koyar.",
            new[] { "OpenNav" }, Shot("tamamlanmamis"));
        Screen("Gelecek", "İleri tarihli işler, en yakın gün en üstte. Bu ekranda yazdığın iş yarına eklenir. Başka bir ileri gün için takvimde o güne tıklayıp orada yaz.",
            new[] { "FutureNav" }, Shot("gelecek"));
        Screen("Tarihsiz", "Bir gün yapılacak, günü belli olmayan işler. Tiklediğin iş bitirdiğin günün altına yazılır ve buradan çıkar.",
            new[] { "UndatedNav" }, Shot("tarihsiz"));
        Screen("Tümü", "Bitmemiş her şey dört başlıkta: Bugün, Önceki günlerden kalan, Gelecek, Tarihsiz. Her başlık tıklayınca açılır kapanır, kapattıkların kapalı kalır.",
            new[] { "AllNav" }, Shot("all"));
        Add(Note("Ekranlar arasında [[Ctrl+↑]]/[[Ctrl+↓]] ile, günler arasında [[Ctrl+←]]/[[Ctrl+→]] ile, iş olan günler arasında [[Alt+←]]/[[Alt+→]] ile geçersin. Başka güne geçtiysen başlıktaki ~Bugüne dön~ geri getirir."));

        Section("Takvim", "CalendarNav", "MiniCalendar");
        P("Şeritteki Takvim düğmesi. Takvimi üç boyda görürsün: hafta, ay ya da yıl; seçtiğin hatırlanır. Soldaki listeden haftaya, aya ya da yıla geçersin. Bir güne basınca o gün İşler'de açılır, başlığın üstündeki ~‹ Takvime dön~ seni geri getirir.");
        Screen("Hafta", "Günler alt alta, işleri yanında. Bir işe basınca sağ panelde açılır, hafta yerinde kalır. Bir işi başka güne sürükleyerek taşırsın. ~Özeti kopyala~ haftada biten işleri gün gün düz metin olarak panoya alır; haftalık rapora yapıştırırsın.",
            new[] { "CalendarNav" }, Shot("hafta"));
        Screen("Ay", "Ayın tamamı, günlerin içinde ilk işler. Yeşil koyulaştıkça o gün daha çok iş bitmiş demektir: 1-2, 3-4, 5 ve üzeri. Turuncu nokta tamamlanmamış iş var demektir.",
            new[] { "CalendarNav" }, Shot("calendar"));
        Screen("Yıl", "On iki ay yan yana, her gün küçük bir kare. Bir yılda nerede yoğunlaştığını bir bakışta görürsün.",
            new[] { "CalendarNav" }, Shot("yil"));
        P("İşler'deki küçük takvim aynı renkleri kullanır. Ayın adına basınca on iki ay açılır, uzak bir aya hızlıca geçersin.");

        Section("Taşımak", "TodayNav", "FutureNav", "UndatedNav", "MiniCalendar", "ContentScroll");
        P("Bir işi soldaki noktalarından tutup sürükle. Nereye bıraktığın ne olacağını belirler. Liste ekrana sığmıyorsa imleci üst ya da alt kenara götür, liste kendiliğinden kayar.");
        Add(Table(new[] { "Bıraktığın yer", "Sonuç" },
            R("Başka bir iş", "Onun üstüne ya da altına yerleşir. Başka günün işiyse o güne geçer."),
            R("Sol listede Bugün", "Bugüne"),
            R("Sol listede Gelecek", "Yarına"),
            R("Sol listede Tarihsiz", "Tarihsizlere"),
            R("Küçük takvimde bir gün", "O güne, geçmiş ya da ileri"),
            R("Haftada başka bir gün", "O güne"),
            R("Listenin altındaki boşluk", "Açık günün sonuna")));
        Sub("Klavyeyle taşımak");
        P("Seçili işte [[M]] taşıma menüsünü açar: ~Bugüne~, ~Yarına~, ~Tarihsize~, ~Gün seç…~. Gün seçerken oklar küçük takvimde gezer, [[Enter]] taşır, [[Esc]] vazgeçer. [[Ctrl+Shift+←]]/[[Ctrl+Shift+→]] işi bir gün öne ya da sonraya alır.");
        Sub("Birden çok işi birlikte taşımak");
        P("[[Ctrl]]+tık tek tek, [[Shift]]+tık aralık seçer, sağ üstteki ~Hepsini seç~ listedeki her şeyi. Klavyede [[Shift+↑]]/[[Shift+↓]] aralık, [[Shift+Enter]] tek iş seçer. [[Esc]] seçimi kaldırır.");
        Add(Shot("selection"));
        P("Seçili işlerden birini sürükleyince hepsi gider, sıraları korunur. Alttaki çubuktan da bugüne, yarına ya da tarihsize taşırsın.");
        Sub("Geçmişe ya da başka bir güne");
        P("Bitmiş ya da bitmemiş fark etmez. İşi küçük takvimde bir günün üstüne sürükle, ya da noktalardan ~Başka güne taşı~'yı seçip takvimde güne tıkla. Tik durumu değişmez; o gün dosyada yoksa başlığı tarih sırasına uygun yere eklenir.");

        Section("Hatırlatıcılar", "ContentScroll");
        P("Toplantı, arama gibi saatli işler unutulmasın diye. Bir işin saati ve en çok iki hatırlatması olur; vakti gelince sağ altta haber gelir.");
        Add(Shot("hatirlatici", 380));
        Add(Steps(
            "**Kur.** Sağ panelde ~Hatırlatıcı ekle~'ye bas. İşe bir sonraki tam saat ve varsayılan iki hatırlatma konur, sayfası açılır.",
            "**Saati ayarla.** Saati yaz ya da [[↑]]/[[↓]] ile 5'er dakika değiştir.",
            "**Ne kadar önce.** Hazır sürelerden en çok iki tane seç: Tam saatinde, 5 dk … 1 gün. ~Özel…~ ile kendi süreni yazarsın. Her hatırlatmanın altında çalacağı saat soluk yazar.",
            "**Haber.** Vakti gelince ~Müşteriyle toplantı · 14:00 — 30 dk sonra~ gibi bir bildirim çıkar; basınca iş açılır."));
        Add(Table(new[] { "Durum", "Ne olur" },
            R("İş bitti ya da iptal edildi", "Kalan hatırlatma çalmaz. Biten işe hatırlatıcı eklenmez."),
            R("Bilgisayar kapalıydı", "Saat geçmediyse açılınca bir kez haber verir; ikisi birden kaçtıysa yalnız sonuncusu."),
            R("Tarihsiz iş", "Saat yazılabilir ama hatırlatma çalmaz."),
            R("Aynı dakikada iki iş", "Tek bildirimde birlikte gelir.")));
        Sub("Gün sonu hatırlatması");
        P("Seçtiğin saatte bugün açık iş kaldıysa günde bir kez haber verir; bildirime basınca bugünün işleri açılır. Saati ve açık kapalı olduğunu ~Ayarlar → Davranış~'ta seçersin. Yeni hatırlatıcıların varsayılan iki süresi de orada.");

        Section("Klavyeyle çalışmak", "ShortcutsButton");
        P("Fareye dokunmadan: listede bir işe gel, sonra tuşlarla yönet. Seçili işin zemini hafif turkuaz, solunda çizgi olur.");
        var keys = new TwoColumns();
        keys.Children.Add(Table(new[] { "Listede", "" },
            R(Text("[[↑]] [[↓]]", 14.5), "Önceki, sonraki iş"),
            R(Text("[[Tab]] [[Shift+Tab]]", 14.5), "Yalnız bitmemiş işler arasında"),
            R(Text("[[→]] [[←]]", 14.5), "Alt işlere in, ana işe çık"),
            R(Key("Boşluk"), "Tikle, tiki kaldır"),
            R(Key("F2"), "Yerinde düzenle"),
            R(Key("Enter"), "Sağ panelde aç"),
            R(Key("Del"), "Sil, önce sorar"),
            R(Text("[[Alt+↑]] [[Alt+↓]]", 14.5), "Sırada yukarı, aşağı"),
            R(Key("M"), "Taşıma menüsü")));
        keys.Children.Add(Table(new[] { "Genel", "" },
            R(Text("[[Ctrl+1]] … [[Ctrl+6]]", 14.5), "Ekranlar ve Takvim"),
            R(Key("Ctrl+N"), "Yeni iş kutusu"),
            R(Key(GlobalHotKey.Keys), "Her yerden hızlı ekleme"),
            R(Key("Ctrl+F"), "Ara"),
            R(Key("Ctrl+Z"), "Geri al"),
            R(Key("Esc"), "Kutuyu, paneli, seçimi ya da aramayı kapat"),
            R(Key("F5"), "Dosyayı yeniden yükle"),
            R(Key("F1"), "Kısayollar penceresi")));
        Add(keys);
        P("[[F1]] bütün kısayolları konularına göre sekmelerde gösterir; üstteki aramaya ~taşı~ ya da ~gün~ yazınca bütün sekmelerden bulur.");

        Section("Dosyalar ve gruplar", "FilesPanel");
        P("Notların Belgeler'deki İş Günlüğü klasöründe durur. Her grup diskte bir klasördür; düzenini uygulamadan yönetirsin.");
        var filesApp = new StackPanel();
        filesApp.Children.Add(Spaced(Text("Uygulamada", 18, semiBold: true), 0, 8));
        filesApp.Children.Add(Shot("dosyalar", 320));
        var filesDisk = new StackPanel();
        filesDisk.Children.Add(Spaced(Text("Belgeler klasöründe", 18, semiBold: true), 0, 8));
        filesDisk.Children.Add(Tree("Belgeler", "└─ İş Günlüğü", "   ├─ iş", "   │  ├─ notlar.txt", "   │  └─ raporlar.txt", "   └─ ev.txt"));
        var files = new TwoColumns();
        files.Children.Add(filesApp);
        files.Children.Add(filesDisk);
        Add(files);
        Add(Table(new[] { "Yapmak istediğin", "Nasıl" },
            R("Dosyaya geçmek", "Üstteki dosya düğmesine bas, listeden seç. Ortadaki ekran aynı kalır; sayı o dosyadaki bitmemiş iş sayısıdır."),
            R("Yeni dosya ya da grup", "Listenin altından. Konum sorulmaz, `.txt` kendiliğinden eklenir."),
            R("Sırayı değiştirmek", "Noktalardan tutup sürükle; başka grubun altına bırakırsan dosya o gruba geçer."),
            R("Adını değiştirmek", "Kalem ya da [[F2]]: ad yerinde değişir."),
            R("Diğerleri", "Üç nokta ya da sağ tık: gruba taşı, gruptan çıkar, Not Defteri'nde aç, sil. Silinen dosyanın son hali yedeklerde kalır."),
            R("Dışarıdan belge eklemek", "Ayarlar → Metin belgesi ekle. Kopyası alınır, orijinaline dokunulmaz.")));
        Add(Note("Listede sadece ad göründüğü için iki dosyaya aynı adı veremezsin, grupları farklı olsa da."));

        Section("Sistem tepsisi", "SettingsButton");
        P("Çarpıya basınca uygulama kapanmaz, saatin yanındaki tepsiye iner. Açık iş varken simgedeki tikin yerinde bugün kalan iş sayısı yazar.");
        var trayWords = new StackPanel();
        trayWords.Children.Add(Table(new[] { "Simgeye", "Sonuç" },
            R("Tek tık", "Bugünün yarım ve yapılmamış işleri"),
            R("Çift tık", "Uygulama açılır"),
            R("Sağ tık", "Menü: Bugünün işleri, Uygulamayı aç, Çıkış")));
        trayWords.Children.Add(Spaced(Text("Panelde işleri ve alt işleri tikleyebilirsin. Her işin ilk üç açık alt işi görünür, gerisi ~+ 4 alt iş daha~ ile uygulamada açılır. Açıklaması olan işin yanında küçük bir not simgesi durur.", 15), 0, 8));
        trayWords.Children.Add(Text("Tamamen kapatmak için simgeye sağ tıklayıp ~Çıkış~'ı seç.", 15));
        var tray = new TwoColumns();
        tray.Children.Add(Shot("tray", 396));
        tray.Children.Add(trayWords);
        Add(tray);

        Section("Drive ile eşitleme", "SettingsButton");
        P("Notların bilgisayarında ve telefonunda aynı kalsın diye kendi Google Drive'ın üzerinden eşitlenir. Uygulama Drive'ında yalnız kendi oluşturduğu dosyaları görür; başka dosyalarına erişemez.");
        Add(Steps(
            "**Bağlan.** ~Ayarlar → Eşitleme → Google Drive'ı bağla~. Tarayıcıda Google hesabını seç.",
            "**Telefonda da.** Telefon uygulamasında aynı hesapla bağlan; aynı dosyalar gelir.",
            "**Gerisi kendiliğinden.** Her değişiklikten birkaç saniye sonra eşitlenir. ~Şimdi eşitle~ hemen yapar."));
        Add(Table(new[] { "Durum", "Uygulamanın yaptığı" },
            R("İki yerde aynı anda değişti", "Üstte ~Farklılık bulundu~ yazar. Açınca iki hali yan yana görürsün; yeni olan vurgulu. Birini seçersin, öbürü yedeklerde kalır."),
            R("Birçok dosya birden silindi", "Silmeyi durdurur ve sorar: ~Silinsin~ ya da ~Geri getir~. Yanlışlıkla klasör boşaltmak notları götürmez."),
            R("İnternet yok", "Notlar bilgisayarda çalışmaya devam eder; bağlantı gelince eşitlenir."),
            R("Bağlantıyı kesmek", "~Ayarlar → Eşitleme~'den. Google tarafında da kaldırmak için myaccount.google.com/permissions.")));

        Section("Ayarlar", "SettingsButton");
        Add(Shot("ayarlar"));
        P("Solda dört bölüm, sağda her ayar bir satır.");
        Add(Table(new[] { "Bölüm", "Neler var" },
            R("Görünüm", "Tema: Sistem, Açık ya da Koyu. Sol panel: Yeni ya da Klasik. Yazma görünümü: Görsel ya da Metin."),
            R("Davranış", "Windows açılınca başlat, çarpıya basınca tepsiye küçült, gün sonu hatırlatması ve saati, hatırlatıcılar ve yeni hatırlatıcının iki süresi."),
            R("Eşitleme", "Drive bağlantısı ve durumu."),
            R("Yardım", "Kullanım kılavuzu, Adım adım tanıtım, Tanıtım davetleri, Tanıtımı sıfırla, Kısayollar, Sürüm notları, Hata günlüğü. Altında sürüm numarası.")));

        Section("Notların güvende mi", "FilesPanel");
        Add(Table(new[] { "Durum", "Uygulamanın yaptığı" },
            R("Yanlış işlem", "~Geri al~ son 50 değişikliğe kadar döner. Listede [[Del]] silmeden önce sorar."),
            R("Günlük yedek", "Her dosyanın gün içindeki ilk değişiklikten önceki hali `%AppData%\\IsGunlugu\\yedekler` klasörüne kopyalanır, son 30 günü tutulur."),
            R("Dosya dışarıdan değişti", "Üstüne yazmaz, güncel halini yükler ve işlemi tekrar denemeni söyler."),
            R("Kayıt yarıda kaldı", "Önce geçici dosyaya yazar, sonra yerine koyar; eski dosya yarım kalmaz."),
            R("Farklı kodlamalı dosya", "UTF-8 ve UTF-16 dışındaki dosyayı bozmamak için açmaz, Not Defteri'nde UTF-8 olarak kaydetmeni söyler."),
            R("Beklenmeyen hata", "~Ayarlar → Yardım → Hata günlüğü~'ne yazılır; bir sorun bildirirken işe yarar. Hiçbir yere gönderilmez.")));
        var version = (DataContext as MainViewModel)?.AppVersion ?? "";
        Add(Spaced(Text($"Bu kılavuzdaki görüntüler örnek notlarla çekildi. Sürüm {version}.", 14, "MutedBrush"), 8, 0));
    }

    private static FrameworkElement IconCell(string icon, string label)
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(Icon(icon, "InkBrush"));
        var text = Text(label, 14.5);
        text.Margin = new Thickness(8, 0, 0, 0);
        row.Children.Add(text);
        return row;
    }

    private static FrameworkElement Tree(params string[] lines)
    {
        var stack = new StackPanel();
        foreach (var line in lines)
        {
            var text = MonoText(line, 13.5);
            text.LineHeight = 25;
            stack.Children.Add(text);
        }
        var tree = new Border { Child = stack, CornerRadius = new CornerRadius(8), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 4, 0, 12) };
        return tree.Paint(Border.BackgroundProperty, "SidebarBrush");
    }

    /// <summary>The opening: the same notes in the file and in the app, line for line.</summary>
    private static FrameworkElement FileAndList()
    {
        var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        var file = new StackPanel();
        file.Children.Add(Spaced(Text("notlar.txt dosyasında", 12.5, "MutedBrush"), 0, 8));
        void FileLine(string text, double indent = 0, bool quiet = false)
        {
            var line = MonoText(text);
            line.Height = 26;
            line.Margin = new Thickness(indent, 0, 0, 0);
            line.TextTrimming = TextTrimming.CharacterEllipsis;
            if (quiet) line.Paint(TextBlock.ForegroundProperty, "MutedBrush");
            file.Children.Add(line);
        }
        FileLine("----04/11/2026", quiet: true);
        FileLine("👍[Done]Sabah toplantısı");
        FileLine("🚨Faturaları kontrol et");
        FileLine("🚨Müşteri sunumunu hazırla");
        FileLine("👍[Done]Grafikleri güncelle", 24);
        FileLine("🚨Demo akışını dene", 24);
        FileLine("Yedek bilgisayarda da aç", 48);
        var left = new Border { Child = file, Padding = new Thickness(16, 14, 12, 14), CornerRadius = new CornerRadius(9, 0, 0, 9) };
        left.Paint(Border.BackgroundProperty, "SidebarBrush");
        grid.Children.Add(left);

        var app = new StackPanel();
        app.Children.Add(Spaced(Text("Uygulamada", 12.5, "MutedBrush"), 0, 8));
        void AppLine(FrameworkElement row)
        {
            row.Height = 26;
            row.VerticalAlignment = VerticalAlignment.Top;
            app.Children.Add(row);
        }
        AppLine(Text("4 Kasım 2026", 13.5, "MutedBrush", true));
        AppLine(MockRow(State.Done, "Sabah toplantısı"));
        AppLine(MockRow(State.Open, "Faturaları kontrol et"));
        AppLine(MockRow(State.Partial, "Müşteri sunumunu hazırla", badge: "1/2"));
        AppLine(MockRow(State.Done, "Grafikleri güncelle", 1));
        AppLine(MockRow(State.Open, "Demo akışını dene", 1));
        var note = Text("Yedek bilgisayarda da aç", 13, "MutedBrush");
        note.Margin = new Thickness(54, 3, 0, 0);
        AppLine(note);
        var right = new Border { Child = app, Padding = new Thickness(16, 14, 12, 14), BorderThickness = new Thickness(0, 1, 1, 1), CornerRadius = new CornerRadius(0, 9, 9, 0) };
        right.Paint(Border.BorderBrushProperty, "InputBorderBrush");
        Grid.SetColumn(right, 1);
        grid.Children.Add(right);
        return grid;
    }

    /// <summary>Where a task can go. The same drawing as the web page, made of the window's colours.</summary>
    private static FrameworkElement Journey()
    {
        var canvas = new Canvas { Width = 860, Height = 400 };

        void Node(double x, double y, string title, string? note, string fill, string edge, string ink, double thickness = 1)
        {
            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var head = new TextBlock { Text = title, FontSize = 15, FontWeight = FontWeights.SemiBold, TextAlignment = TextAlignment.Center };
            words.Children.Add(head.Paint(TextBlock.ForegroundProperty, ink));
            if (note is not null)
            {
                var small = new TextBlock { Text = note, FontSize = 12, TextAlignment = TextAlignment.Center };
                words.Children.Add(small.Paint(TextBlock.ForegroundProperty, "MutedBrush"));
            }
            var box = new Border { Child = words, Width = 170, Height = 64, CornerRadius = new CornerRadius(9), BorderThickness = new Thickness(thickness) };
            box.Paint(Border.BackgroundProperty, fill).Paint(Border.BorderBrushProperty, edge);
            Canvas.SetLeft(box, x);
            Canvas.SetTop(box, y);
            canvas.Children.Add(box);
        }

        void Arrow(string data)
        {
            var geometry = Geometry.Parse(data).GetFlattenedPathGeometry();
            canvas.Children.Add(Stroke(data, "InkBrush", 1.5));
            geometry.GetPointAtFractionLength(1, out var tip, out var tangent);
            var direction = new Vector(tangent.X, tangent.Y);
            direction.Normalize();
            var back = tip - direction * 9;
            var side = new Vector(-direction.Y, direction.X) * 4.5;
            var head = new Polygon { Points = new PointCollection { tip, back + side, back - side } };
            canvas.Children.Add(head.Paint(Shape.FillProperty, "InkBrush"));
        }

        void Label(string text, double x, double y, TextAlignment align = TextAlignment.Left)
        {
            var label = new TextBlock { Text = text, FontSize = 13, Padding = new Thickness(4, 0, 4, 0) };
            label.Paint(TextBlock.ForegroundProperty, "MutedBrush").Paint(TextBlock.BackgroundProperty, "WindowBrush");
            label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            var width = label.DesiredSize.Width;
            Canvas.SetLeft(label, align == TextAlignment.Center ? x - width / 2 : align == TextAlignment.Right ? x - width : x);
            Canvas.SetTop(label, y - 13);
            canvas.Children.Add(label);
        }

        Arrow("M 515,167 L 668,167");
        Arrow("M 405,201 L 405,318");
        Arrow("M 455,318 L 455,201");
        Arrow("M 190,72 C 260,86 300,120 343,150");
        Arrow("M 345,138 C 300,90 250,56 192,50");
        Arrow("M 190,262 C 260,250 300,215 343,186");
        Arrow("M 345,198 C 305,250 255,285 192,286");

        Node(20, 30, "Gelecek", "ileri tarihli", "SidebarBrush", "InputBorderBrush", "InkBrush");
        Node(20, 240, "Tarihsiz", "günü belli değil", "SidebarBrush", "InputBorderBrush", "InkBrush");
        Node(345, 135, "Bugün", null, "AccentBrush", "AccentBrush", "OnAccentBrush");
        Node(670, 135, "Bitti", "yapıldığı günde kalır", "WindowBrush", "DoneBrush", "InkBrush", 1.5);
        Node(345, 320, "Tamamlanmamış", "önceki günlerden kalan", "SidebarBrush", "InputBorderBrush", "InkBrush");

        Label("tikleyince", 592, 157, TextAlignment.Center);
        Label("gün biterse", 397, 262, TextAlignment.Right);
        Label("Bugüne al", 463, 262);
        Label("günü gelince", 280, 128, TextAlignment.Right);
        Label("Yarına taşı", 285, 58);
        Label("Bugüne al", 245, 222, TextAlignment.Right);
        Label("Tarihsiz yap", 205, 312);

        return new Viewbox { Child = canvas, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Margin = new Thickness(0, 4, 0, 12), HorizontalAlignment = HorizontalAlignment.Left };
    }
}
