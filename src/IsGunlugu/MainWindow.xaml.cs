using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using IsGunlugu.Core;

namespace IsGunlugu;

public partial class MainWindow : Window
{
    private const int DwmUseImmersiveDarkMode = 20;
    private const string DragFormat = "IsGunlugu.Row";

    private readonly MainViewModel _viewModel;
    /// <summary>The steps of the part being shown; the tour is in parts, each for one screen.</summary>
    private List<GuideStep> _guideSteps = new();
    private readonly List<TourPart> _tourParts;
    private TourPart? _tourPart;
    private TourPart? _invited;
    private bool _tourCheckQueued;

    /// <summary>The list was walked with the arrow keys once: the keyboard part may invite.</summary>
    private bool _keysUsed;
    private int _guideIndex = -1;
    private bool _guideOpenedComposer;
    private ICommand? _guideView;
    private Point _dragStart;
    private bool _dragged;
    private EntryViewModel? _dropMarked;
    private MonthCell? _dropCell;
    private int? _edgeScrollAt;
    private bool _dragOpenedOverlay;

    public MainWindow(MainViewModel viewModel)
    {
        _viewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        if (viewModel.DetailPanelWidth is double width) DetailPanel.Width = Math.Max(DetailMinWidth, Math.Min(760, width));
        WireDraftEditor();
        PlaceWindow();
        Activated += (_, _) => viewModel.CheckDrive();
        viewModel.RowWanted += GoToRow;

        var today = viewModel.ShowTodayCommand;
        var open = viewModel.ShowOpenCommand;
        var future = viewModel.ShowFutureCommand;
        var undated = viewModel.ShowUndatedCommand;
        var all = viewModel.ShowAllCommand;
        var calendar = viewModel.ShowCalendarCommand;

        // A part's steps stay on the screen that invited them, so they do not switch the page.
        var stay = new RelayCommand(() => { });

        _tourParts = new List<TourPart>
        {
            new("ilk", "İlk adımları gör", () => _viewModel.IsTodayView, new List<GuideStep>
            {
            new(stay, () => _viewModel.IsVisualWriting ? DraftEditor : DraftBox, "Yeni iş yaz", "İşi buraya yaz, Enter'a bas. Bugünün işi olarak eklenir. Kutunun içindeki yazı, işin hangi güne ekleneceğini söyler. Başa ya da sona yarın, cuma, 15 kasım gibi bir gün yazarsan iş o güne gider."),
            new(stay, ShowComposerChips, "Gün, alt iş, açıklama", "Kutuya tıklayınca ya da listede yazmaya başlayınca açılır. Gün düğmesi işin gününü gösterir: başlığa yarın, cuma, 15 kasım yazarsan o gün kendiliğinden seçilir, düğmeden Bugün, Yarın, Tarihsiz ya da takvimden bir gün de seçersin. ＋ Açıklama tek satırlık açıklama açar, Enter onu tamamlar. ＋ Alt iş'te her alt işi yazıp Enter'a bas, bitince Esc. Sonra Enter ya da Ekle işi ekler, Ctrl+Enter yapıldı olarak ekler. Esc ya da Vazgeç kutuyu kapatır, yazdıkların kaybolmaz."),
            new(stay, () => BottomPanel, "Geri al", "Yaptığın değişiklik kutunun üstünde yazar. Silme ve taşıma dahil, Geri al ya da Ctrl+Z ile dönebilirsin."),
                new(stay, () => Part("SettingsButton"), "Diğer ekranlar", "Bir ekrana ilk geldiğinde sağ üstte böyle küçük bir davet göreceksin; basınca o ekran birkaç adımda anlatılır. Davetleri Ayarlar › Yardım'dan kapatabilir, Tanıtımı sıfırla ile hepsini en baştan alabilirsin."),
            }),
            new("satirlar", "Satırları tanı", () => _viewModel.IsListView && VisibleRows().Count > 0, new List<GuideStep>
            {
            new(stay, () => RowPart("RowCheck"), "İşi bitir", "Kutuyu tikleyince iş yapıldı olur. Alt işlerin bir kısmı bitince kutu sarı çizgi olur, yanında 2/3 gibi sayı görünür. Sarı kutuya tıklarsan kalan alt işler de biter."),
            new(stay, () => RowPart("Actions"), "İşe tıkla", "Bir işe tıklayınca sağda panel açılır: başlık, açıklama ve alt işler orada düzenlenir, alt iş eklenir, iş başka güne taşınır ya da silinir. Bir alt işe tıklarsan panel o alt işin başlığını ve açıklamasını açar. Listede boş bir yere tıklamak ya da Esc paneli kapatır. Kalem satırın içinde düzenler: Enter kaydeder, Ctrl+Enter kaydedip tikler, Esc vazgeçer."),
            new(stay, () => RowPart("Grip"), "Soldaki noktalar", "Tutup sürükleyince işin sırası değişir. Sol menüde Bugün'e bırakırsan bugüne, Gelecek'e bırakırsan yarına geçer, Tarihsiz'e bırakırsan tarihsiz olur. Tıklarsan aynı seçenekler menüde açılır."),
                new(stay, () => RowPart("BodyToggle"), "Alt işleri aç, kapat", "Alt işler kapalı gelir, ana işin açıklaması görünür kalır. Sayıya ya da oka basınca açılır; açtığın iş açık kalır. En alttaki ✓ satırı o günün bitenlerini toplar, basınca açılır."),
            }),
            new("panel", "Sağ paneli tanı", () => _viewModel.IsDetailTaskPage, new List<GuideStep>
            {
                new(stay, () => DetailHeadingText, "İşin günü", "Panelin üstünde işin durduğu gün yazar: Bugün, Yarın ya da tarih. Başlığı ve açıklamayı burada değiştirirsin; Enter kaydeder."),
                new(stay, () => DetailTimeRow, "Hatırlatıcı", "Toplantı, arama gibi saatli işlere hatırlatıcı kur. Basınca kendi sayfası açılır; işin saatini ve ne kadar önce haber verileceğini seçersin."),
                new(stay, () => DetailSubTaskBox, "Alt işler", "Alt iş ekle, tikle, sil. Soldaki noktalarla ya da Alt+↑/↓ ile sırasını değiştirirsin. Bir alt işe basınca panel onun sayfasına geçer, ‹ ile dönersin."),
                new(stay, () => DetailMoveSection, "Taşı ve Sil", "Taşı düğmeleri ve Sil ile İşi iptal et panelin altında sabit durur. İptal edilen iş silinmez, üstü çizili kalır."),
            }),
            new("hatirlatici", "Hatırlatıcıyı tanı", () => _viewModel.IsDetailTimePage, new List<GuideStep>
            {
                new(stay, () => DetailTimeBox, "İşin saati", "İşin saatini yaz ya da ↑/↓ ile 5'er dakika değiştir. Listede işin yanında çalar saatli etiket görünür."),
                new(stay, () => DetailReminderChoices, "Ne kadar önce", "En çok iki hatırlatma seç: tam saatinde, birkaç dakika önce ya da bir gün önce. Özel… ile kendi süreni yazarsın."),
                new(stay, () => DetailReminderList, "Bildirim", "Vakti gelince sağ altta haber gelir; basınca iş açılır. İş biterse kalan hatırlatma çalmaz. Varsayılanlar Ayarlar › Davranış'ta."),
            }),
            new("tamamlanmamis", "Tamamlanmamış'ı tanı", () => _viewModel.IsOpenView && VisibleRows().Count > 0, new List<GuideStep>
            {
            new(stay, () => Part("OpenNav"), "Tamamlanmamış", "Önceki günlerden kalan, bitmemiş işler. Her iş yazıldığı günün başlığı altında durur. Yanındaki sayı kaç iş kaldığını gösterir.", SidebarSection.Tasks),
            new(stay, () => RowPart("BringButton"), "Bugüne çek", "Satırdaki Bugüne al tek işi, sağ üstteki Hepsini bugüne al hepsini taşır. Taşınan iş dosyada da bugünün altına geçer ve eski gününden silinir. Hepsini birden aldıysan tek Geri al hepsini geri koyar."),
            }),
            new("gelecek", "Gelecek ve Tarihsiz'i tanı", () => _viewModel.IsFutureView || _viewModel.IsUndatedView, new List<GuideStep>
            {
            new(stay, () => Part("FutureNav"), "Gelecek", "İleri tarihli işler, en yakın gün önce gelir. Bu ekranda yazdığın iş yarına eklenir. Bir işi buraya sürükler ya da noktalardan Yarına taşı'yı seçersen o da yarına geçer.", SidebarSection.Tasks),
            new(stay, () => Part("UndatedNav"), "Tarihsiz", "Günü belli olmayan işler. Bu ekranda yazdığın iş tarihsiz olur. Bir işi buraya sürükler ya da noktalardan Tarihsiz yap'ı seçersen o da buraya gelir. Tiklediğin iş bitirdiğin günün altına yazılır.", SidebarSection.Tasks),
            }),
            new("tumu", "Tümü'nü tanı", () => _viewModel.IsAllView, new List<GuideStep>
            {
            new(stay, () => Part("AllNav"), "Tümü", "Bitmemiş her şey başlıklara ayrılmış olarak burada: Bugün, Önceki günlerden kalan, Gelecek, Tarihsiz. Her başlık tıklayınca açılır kapanır, kapattığın başlıklar kapalı kalır.", SidebarSection.Tasks),
            new(stay, () => SelectAllButton, "Toplu taşıma", "Bugün, takvimden açılan günler ve İşler grubundaki ekranlarda Ctrl+tık ile tek tek, Shift+tık ile aralık seçersin, Hepsini seç de var. Seçili bir işi sürükleyince hepsi birlikte gider. Alttaki çubuktan bugüne, yarına veya tarihsize de taşırsın. Esc seçimi kaldırır."),
            }),
            new("takvim", "Takvimi tanı", () => _viewModel.IsCalendarView, new List<GuideStep>
            {
            new(stay, () => Part("CalendarNav"), "Takvim", "Hafta, ay ya da yıl. Günlerin içinde işleri görürsün; bir güne tıklayınca o günün listesi İşler'de açılır, başlığın üstündeki Takvime dön seni geri getirir. Soldaki aylardan birine basınca takvim o aya gider.", SidebarSection.Calendar),
                new(stay, () => _viewModel.IsClassicSidebar ? HeaderScaleButtons : PaneScaleButtons, "Hafta, ay, yıl", "Takvimi üç boyda görürsün; seçtiğin hatırlanır. Haftada bir işi başka güne sürükleyerek taşırsın, işe basınca sağ panelde açılır.", SidebarSection.Calendar),
            new(stay, () => Part("MiniCalendar"), "Başka güne taşı", "Bitmiş ya da bitmemiş bir işi noktalarından tutup takvimde bir günün üstüne bırak; geçmiş veya ileri fark etmez. Ya da noktalara tıklayıp Başka güne taşı'yı seç, sonra takvimden güne tıkla. Seçili işler birlikte gider, Esc vazgeçer.", SidebarSection.Tasks),
            }),
            new("dosyalar", "Dosyaları tanı", () => _viewModel.IsFilePickerOpen, new List<GuideStep>
            {
            new(stay, () => Part("FilesPanel"), "Dosyalar", "Her proje için ayrı .txt tutabilirsin; en üstteki düğme açık dosyayı gösterir, basınca hepsi açılır. Bir dosyaya basınca ona geçersin, ortadaki ekran aynı kalır. Sayı o dosyadaki bitmemiş iş sayısıdır. Alttan yeni dosya ya da boş grup açarsın, klasör düğmesi dosyaların klasörünü gösterir.", classicText: "Her proje için ayrı .txt tutabilirsin. Dosyaların Belgeler'deki İş Günlüğü klasöründe durur. Artı adını sorup yeni dosya açar, klasör düğmesi bu klasörü gösterir. Sayı o dosyadaki bitmemiş iş sayısıdır. Satırdaki çarpı sadece listeden kaldırır, dosya silinmez."),
            new(stay, () => Part("FilesPanel"), "Sıra ve gruplar", "Soldaki noktalardan tutup sürükleyince sıra değişir; başka bir grubun altına bırakırsan dosya o gruba geçer. Kalem ya da F2 adı yerinde değiştirir, üç nokta ve sağ tık gruba taşı, Not Defteri'nde aç ve sil seçeneklerini açar. Her grup İş Günlüğü içinde bir klasördür.", classicText: "Dosyanın üstüne gelince çıkan noktalara ya da dosyaya sağ tıkla: yeniden adlandır, yeni grup oluştur, gruba taşı, gruptan çıkar. Her grup İş Günlüğü içinde bir klasördür, dosya da oraya taşınır. Başlığa tıklayınca grup kapanır. Kalem adını değiştirir, çarpı grubu kaldırır, dosyalar üst klasöre döner."),
            new(stay, () => Part("FilesPanel"), "Not Defteri", "Notların kendi .txt dosyanda durur. Not Defteri simgesi dosyayı açar. Dışarıdan değiştirirsen uygulama güncel halini yükler. Tarihsiz işler dosyanın sonunda ----tarihsiz başlığının altındadır."),
            }),
            new("klavye", "Klavyeyi tanı", () => _keysUsed && _viewModel.IsListView, new List<GuideStep>
            {
                new(stay, () => (FrameworkElement?)FocusedRowElement ?? ContentScroll, "Klavyeyle gezin", "↑/↓ işler arasında, Tab yalnız bitmemişler arasında gezer. Boşluk tikler, Enter sağ panelde açar, F2 yerinde düzenler, Del siler. M taşıma menüsünü açar."),
            new(stay, () => Part("SearchBox"), "Ara", "Ctrl+F ile buraya gelirsin. Alt işlerin içinde de arar. Esc aramayı kapatır."),
            new(stay, () => ShortcutsButton, "Kısayollar", "F1 bütün kısayolları gösterir. En çok işine yarayacaklar: Ctrl+N yeni iş, Ctrl+F arama, Ctrl+1'den Ctrl+6'ya kadar sol menüdeki ekranlar."),
            }),
        };

        Loaded += (_, _) =>
        {
            // The add box stays one line until it is clicked or typed into; typing in the list opens it.
            FocusRest.Focus();
            QueueTourCheck();
        };
        viewModel.ToursChanged += QueueTourCheck;
        viewModel.PropertyChanged += (_, _) => QueueTourCheck();
        viewModel.ComposerFocusRequested += FocusComposer;
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.IsClassicSidebar)) UpdateRailOnly();
        };
        AddHandler(TextInputEvent, new TextCompositionEventHandler(Window_TextInput));
        // The targets in the list mark their drops handled; the list still has to close once the drop is made.
        SectionPane.AddHandler(DragDrop.DropEvent, new DragEventHandler((_, _) => CloseDragOverlay()), true);
        viewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.HasFile) && IsLoaded) QueueTourCheck();
        };
        SizeChanged += (_, _) =>
        {
            if (_guideIndex >= 0) PlaceGuide();
            PlaceWeek();
        };
    }

    /// <summary>The close button hid the window instead of closing it.</summary>
    public event Action? HiddenToTray;

    public void UpdateTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero) return;

        var enabled = App.IsDarkTheme ? 1 : 0;
        try
        {
            DwmSetWindowAttribute(handle, DwmUseImmersiveDarkMode, ref enabled, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Windows XP has no dwmapi; the title bar just keeps its colour.
        }
    }

    /// <summary>Starts one part of the tour; without one, the part of the screen on show (or the first steps).</summary>
    public void StartGuide(TourPart? part = null)
    {
        part ??= _tourParts.FirstOrDefault(p => p.Applies()) ?? _tourParts[0];
        _tourPart = part;
        _guideSteps = part.Steps;
        _viewModel.TourInviteText = "";
        _guideIndex = 0;
        _guideView = null;
        GuideLayer.Visibility = Visibility.Visible;
        GuideNextButton.Focus();
        ShowGuideStep();
    }

    public void AdvanceGuide()
    {
        if (_guideIndex < 0) return;

        _guideIndex++;
        if (_guideIndex >= _guideSteps.Count) EndGuide();
        else ShowGuideStep();
    }

    public void RetreatGuide()
    {
        if (_guideIndex <= 0) return;

        _guideIndex--;
        ShowGuideStep();
    }

    public void EndGuide()
    {
        _guideIndex = -1;
        HideGuideComposer();
        GuideLayer.Visibility = Visibility.Collapsed;
        _viewModel.MarkGuideShown();
        // The part stays on its own screen; only lists the tour opened to point at close again.
        if (_tourPart is { } part)
        {
            _viewModel.MarkTourSeen(part.Id);
            if (part.Id != "dosyalar") _viewModel.IsFilePickerOpen = false;
        }
        _tourPart = null;
        _viewModel.IsSectionOverlayOpen = false;
        FocusRest.Focus();
        QueueTourCheck();
    }

    /// <summary>
    /// After anything changes: the first part not seen yet whose screen is on show invites, one at a time, and only
    /// while nothing lies over the window.
    /// </summary>
    private void QueueTourCheck()
    {
        if (_tourCheckQueued) return;
        _tourCheckQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, new Action(() =>
        {
            _tourCheckQueued = false;
            var open = IsLoaded && IsVisible && _viewModel.HasFile && _viewModel.TourInvitesOn && _guideIndex < 0 && NoOverlay
                && !_viewModel.IsComposerOpen && !_viewModel.IsSearching;
            _invited = open ? _tourParts.FirstOrDefault(p => !_viewModel.IsTourSeen(p.Id) && p.Applies()) : null;
            // The very first steps start by themselves on a new user's first look; their last step explains the
            // invites the other parts come with.
            if (_invited is { Id: "ilk" } first && _viewModel.ShouldShowGuide)
            {
                StartGuide(first);
                return;
            }
            _viewModel.TourInviteText = _invited is { } part ? $"{part.Invite} · {part.Steps.Count} adım" : "";
        }));
    }

    private void TourInvite_Click(object sender, RoutedEventArgs e)
    {
        if (_invited is { } part) StartGuide(part);
    }

    private void TourDismiss_Click(object sender, RoutedEventArgs e)
    {
        if (_invited is { } part) _viewModel.MarkTourSeen(part.Id);
        QueueTourCheck();
    }

    /// <summary>
    /// Where the window opens: where it was left last time, pulled onto a screen that is still there; the first time,
    /// in the middle of the screen the pointer is on, and never with its top cut off on a small or zoomed screen.
    /// </summary>
    private void PlaceWindow()
    {
        // The saved corner is in the main monitor's units (see RememberPlace), the size in window units.
        Core.WindowFit fit;
        Monitors.Area area;
        var saved = _viewModel.SavedWindowPlace;
        if (saved is not null)
        {
            var main = Monitors.PrimaryScale;
            area = Monitors.Holding(saved[0] * main, saved[1] * main, saved[2] * main, saved[3] * main);
            fit = Core.WindowFit.Keep(saved[0] * main / area.Scale, saved[1] * main / area.Scale, saved[2], saved[3],
                MinWidth, MinHeight, area.Left, area.Top, area.Width, area.Height);
        }
        else
        {
            area = Monitors.AtPointer();
            fit = Core.WindowFit.Place(Width, Height, MinWidth, MinHeight, area.Left, area.Top, area.Width, area.Height);
        }

        MinWidth = fit.MinWidth;
        MinHeight = fit.MinHeight;
        Width = fit.Width;
        Height = fit.Height;
        WindowStartupLocation = WindowStartupLocation.Manual;
        // Until the window exists it counts in the main monitor's units; OnSourceInitialized puts it on its own monitor.
        var start = Monitors.PrimaryScale;
        Left = fit.Left * area.Scale / start;
        Top = fit.Top * area.Scale / start;
        _placeInPixels = new Point(fit.Left * area.Scale, fit.Top * area.Scale);
        _openMaximized = saved is not null && _viewModel.SavedWindowMaximized;
    }

    private Point? _placeInPixels;
    private bool _openMaximized;

    /// <summary>The size and place to come back to: the normal one even when the window is maximised or minimised.</summary>
    private void RememberPlace()
    {
        var place = WindowState == WindowState.Normal ? new Rect(Left, Top, ActualWidth, ActualHeight) : RestoreBounds;
        if (place.IsEmpty || place.Width <= 0 || place.Height <= 0) return;
        // The corner is kept in the main monitor's units, as older versions did, so either can read the other's place.
        var corner = WindowState == WindowState.Normal
            ? Monitors.PixelPlace(this)
            : new Point(place.Left * Monitors.ScaleOf(this), place.Top * Monitors.ScaleOf(this));
        var main = Monitors.PrimaryScale;
        _viewModel.RememberWindow(corner.X / main, corner.Y / main, place.Width, place.Height, WindowState == WindowState.Maximized);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        UpdateTitleBar();
        // The window comes to life on its monitor, takes that monitor's scale, and only then fills it if it was left so.
        if (_placeInPixels is { } corner) Monitors.MoveTo(this, corner.X, corner.Y);
        _placeInPixels = null;
        if (_openMaximized) WindowState = WindowState.Maximized;
    }

    // The add box and the sub-task box shrink on a press anywhere else; inside them the press is the user's own.
    protected override void OnPreviewMouseDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);
        var inSubTaskBox = false;
        var inComposer = false;
        for (var node = e.OriginalSource as DependencyObject; node is not null; node = node is Visual ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node))
        {
            if (node == Composer) inComposer = true;
            if (node is FrameworkElement { Name: "SubTaskBox" }) inSubTaskBox = true;
        }
        if (!inSubTaskBox) _viewModel.PutAwaySubTaskEntry();
        if (!inComposer) _viewModel.ShrinkComposer();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        if (IsVisible) RememberPlace();
        if (App.IsExiting || !_viewModel.HidesOnClose) return;

        e.Cancel = true;
        Hide();
        HiddenToTray?.Invoke();
    }

    private void ShowGuideStep()
    {
        var step = _guideSteps[_guideIndex];
        HideGuideComposer();
        if (_guideView != step.View)
        {
            _guideView = step.View;
            step.View.Execute(null);
        }

        if (!_viewModel.IsClassicSidebar && step.Section is { } section)
        {
            _viewModel.Section = section;
            if (_viewModel.IsRailOnly) _viewModel.IsSectionOverlayOpen = true;
            UpdateLayout();
        }
        RevealInSidebar(step.Target());
        GuideStepText.Text = $"{_guideIndex + 1} / {_guideSteps.Count}";
        GuideTitle.Text = step.Title;
        GuideText.Text = _viewModel.IsClassicSidebar ? step.ClassicText ?? step.Text : step.Text;
        GuideArtHost.Content = GuideArt.For(step.Title);
        // The card's own width less its padding and border; the bar shows how far along the tour is.
        GuideProgress.Width = (GuideCard.Width - 39) * (_guideIndex + 1) / _guideSteps.Count;
        GuideBackButton.Visibility = _guideIndex == 0 ? Visibility.Collapsed : Visibility.Visible;
        GuideNextButton.Content = _guideIndex == _guideSteps.Count - 1 ? "Bitir" : "İleri";
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            // The sidebar scrolls on a small screen; the part being explained has to be in sight.
            if (_guideIndex >= 0) _guideSteps[_guideIndex].Target().BringIntoView();
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PlaceGuide));
        }));
    }

    /// <summary>The tour points at parts of the sidebar's lists: the section holding the part opens, over the middle on a narrow window.</summary>
    private void RevealInSidebar(FrameworkElement target)
    {
        // A list never shown has no visual tree yet, so the way up goes by the XAML's own nesting.
        _viewModel.IsFilePickerOpen = !_viewModel.IsClassicSidebar && IsWithin(target, FilesPanel);
        if (!IsWithin(target, SectionPane)) return;

        if (IsWithin(target, CalendarList)) _viewModel.Section = SidebarSection.Calendar;
        else if (IsWithin(target, TasksList)) _viewModel.Section = SidebarSection.Tasks;
        if (_viewModel.IsRailOnly) _viewModel.IsSectionOverlayOpen = true;
        UpdateLayout();
    }

    /// <summary>
    /// A part of the sidebar by its name in the new layout; in the classic one the same part has the Classic prefix.
    /// TaskGroupButton is the İşler heading there and the İşler button on the strip here.
    /// </summary>
    private FrameworkElement Part(string name)
    {
        if (_viewModel.IsClassicSidebar) return (FrameworkElement?)FindName("Classic" + name) ?? (FrameworkElement)FindName(name);
        return (FrameworkElement)FindName(name switch
        {
            "TaskGroupButton" => "TasksRail",
            "CalendarNav" => "CalendarRail",
            _ => name,
        });
    }

    /// <summary>The tour opens the add box to point at its buttons, and closes it again when it moves on.</summary>
    private FrameworkElement ShowComposerChips()
    {
        if (!_viewModel.IsComposerOpen)
        {
            _viewModel.ShowComposer(true);
            _guideOpenedComposer = true;
        }
        return ComposerChips;
    }

    private void HideGuideComposer()
    {
        if (!_guideOpenedComposer) return;

        _guideOpenedComposer = false;
        _viewModel.ShowComposer(false);
    }

    /// <summary>The first such part of a row on screen, or the whole list when it has no rows.</summary>
    private FrameworkElement RowPart(string name) => FindShown(ContentScroll, name) ?? ContentScroll;

    private static FrameworkElement? FindShown(DependencyObject parent, string name)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is FrameworkElement { IsVisible: true } element && element.Name == name) return element;

            var found = FindShown(child, name);
            if (found is not null) return found;
        }
        return null;
    }

    private void PlaceGuide()
    {
        if (_guideIndex < 0) return;

        const double gap = 12;
        const double edge = 8;
        const double ringPadding = 4;

        var target = _guideSteps[_guideIndex].Target();
        var topLeft = target.TranslatePoint(new Point(0, 0), GuideLayer);
        var width = target.ActualWidth;
        var height = target.ActualHeight;

        Canvas.SetLeft(GuideRing, topLeft.X - ringPadding);
        Canvas.SetTop(GuideRing, topLeft.Y - ringPadding);
        GuideRing.Width = width + 2 * ringPadding;
        GuideRing.Height = height + 2 * ringPadding;

        var hole = new RectangleGeometry(new Rect(topLeft.X - ringPadding, topLeft.Y - ringPadding, GuideRing.Width, GuideRing.Height), 7, 7);
        GuideDim.Data = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(new Rect(0, 0, GuideLayer.ActualWidth, GuideLayer.ActualHeight)), hole);

        GuideCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var card = GuideCard.DesiredSize;
        var layerWidth = GuideLayer.ActualWidth;
        var layerHeight = GuideLayer.ActualHeight;

        double left, top;
        if (target != ContentScroll && target.IsDescendantOf(ContentScroll))
        {
            // Beside a row the card would hide the row it is talking about.
            left = topLeft.X;
            top = topLeft.Y + height + gap;
        }
        else if (topLeft.X + width + gap + card.Width + edge <= layerWidth)
        {
            left = topLeft.X + width + gap;
            top = topLeft.Y;
        }
        else
        {
            left = topLeft.X;
            top = topLeft.Y - card.Height - gap;
            if (top < edge) top = topLeft.Y + height + gap;
        }

        Canvas.SetLeft(GuideCard, Math.Max(edge, Math.Min(left, layerWidth - card.Width - edge)));
        Canvas.SetTop(GuideCard, Math.Max(edge, Math.Min(top, layerHeight - card.Height - edge)));
    }

    private void GuideNext_Click(object sender, RoutedEventArgs e) => AdvanceGuide();

    private void GuideBack_Click(object sender, RoutedEventArgs e) => RetreatGuide();

    private void GuideSkip_Click(object sender, RoutedEventArgs e) => EndGuide();

    private void ShowGuide_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.IsSettingsOpen = false;
        StartGuide();
    }

    private void StartWithWindowsLabel_Click(object sender, MouseButtonEventArgs e) =>
        _viewModel.StartWithWindows = !_viewModel.StartWithWindows;

    private void CloseToTrayLabel_Click(object sender, MouseButtonEventArgs e) =>
        _viewModel.CloseToTray = !_viewModel.CloseToTray;

    private void DayEndLabel_Click(object sender, MouseButtonEventArgs e) =>
        _viewModel.DayEndOn = !_viewModel.DayEndOn;

    private void ShowShortcuts_Click(object sender, RoutedEventArgs e) => _viewModel.OpenHelpFromSettings();

    /// <summary>Another version's notes start from their top.</summary>
    private void ReleaseTab_Click(object sender, RoutedEventArgs e) => ReleaseScroll.ScrollToTop();

    private Action? _afterDeleteConfirm;

    /// <summary>The delete question takes the keyboard: Enter deletes, Esc leaves it. Closed, the list row gets it back.</summary>
    private void DeleteScrim_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true)
        {
            Dispatcher.BeginInvoke(new Action(() => DeleteYes.Focus()), System.Windows.Threading.DispatcherPriority.Input);
            return;
        }
        var after = _afterDeleteConfirm;
        _afterDeleteConfirm = null;
        after?.Invoke();
    }

    /// <summary>The shortcuts window opens ready to search.</summary>
    private void HelpScrim_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) Dispatcher.BeginInvoke(new Action(() => ShortcutSearch.Focus()), System.Windows.Threading.DispatcherPriority.Input);
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (PickerKey(e))
        {
            e.Handled = true;
            return;
        }

        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;
        if (e.Key == Key.Insert && Keyboard.Modifiers == ModifierKeys.None)
        {
            // Overtype has no visible sign in a text box, so a stray press looks like text eating itself.
            e.Handled = true;
            return;
        }

        var detailEsc = e.Key == Key.Escape && _viewModel.IsDetailOpen && !_viewModel.IsPickingDay && !_viewModel.IsNamePromptOpen
            && !_viewModel.IsSettingsOpen && !_viewModel.IsDeleteConfirmOpen && _guideIndex < 0;
        var inDetailAddBox = e.OriginalSource == DetailSubTaskBox || Keyboard.FocusedElement == DetailSubTaskBox;

        // Esc inside the add box belongs to it: it steps out of a field first, then closes the box; the side panel stays.
        // Elsewhere (search, a row editor) the keyboard's own box handles it.
        var composerEsc = e.Key == Key.Escape && _viewModel.IsComposerOpen && Composer.IsKeyboardFocusWithin && _guideIndex < 0 && !_viewModel.IsPickingDay
            && !_viewModel.IsNamePromptOpen && !_viewModel.IsSettingsOpen && !_viewModel.IsDeleteConfirmOpen && !_viewModel.IsHelpOpen
            && !_viewModel.IsReleaseNotesOpen && !_viewModel.IsUserGuideOpen && !(detailEsc && inDetailAddBox);
        if (composerEsc)
        {
            EscapeComposer();
            e.Handled = true;
        }
        else if (detailEsc && inDetailAddBox)
        {
            // Esc in the add box only ends adding: the box empties and lets go, the panel stays; the next Esc closes it.
            DetailSubTaskBox.Clear();
            FocusRest.Focus();
            e.Handled = true;
        }
        else if (detailEsc)
        {
            CommitDetail();
            // On the time page Esc goes back to the task; on the task's page it closes the panel.
            if (!_viewModel.IsDetailTimePage || !_viewModel.DetailBack()) _viewModel.CloseDetail();
            e.Handled = true;
        }
        else if (_guideIndex >= 0 && e.Key == Key.Escape)
        {
            EndGuide();
        }
        else if (_guideIndex >= 0 && e.Key is Key.Enter or Key.Right)
        {
            AdvanceGuide();
        }
        else if (_guideIndex >= 0 && e.Key == Key.Left)
        {
            RetreatGuide();
        }
        else if (_guideIndex >= 0)
        {
            return;
        }
        else if (_viewModel.IsUserGuideOpen && e.Key == Key.Escape)
        {
            _viewModel.IsUserGuideOpen = false;
        }
        else if (_viewModel.IsUserGuideOpen)
        {
            // The window's shortcuts must not act on the screen hidden behind the guide. Alt+F4 still closes.
            var windowShortcut = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) || e.Key is Key.F1 or Key.F5
                || (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey is Key.Left or Key.Right);
            if (!windowShortcut) return;
        }
        else if (_viewModel.IsPickingDay && e.Key == Key.Escape)
        {
            _viewModel.CancelPickDay();
            RestoreRow();
        }
        else if (_viewModel.HasPickCursor && Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Enter)
        {
            // Picking a day with the keys: the arrows walk the small calendar, Enter moves the work there.
            if (e.Key == Key.Enter)
            {
                _viewModel.TakePickCursor();
                RestoreRow();
            }
            else _viewModel.MovePickCursor(e.Key switch { Key.Left => -1, Key.Right => 1, Key.Up => -7, _ => 7 });
        }
        else if (_viewModel.IsDeleteConfirmOpen && e.Key == Key.Escape)
        {
            _viewModel.CancelDeleteCommand.Execute(null);
        }
        else if (_viewModel.HasSelection && e.Key == Key.Escape && !_viewModel.IsReleaseNotesOpen && !_viewModel.IsHelpOpen && !_viewModel.IsSettingsOpen && !_viewModel.IsNamePromptOpen)
        {
            _viewModel.ClearSelection();
        }
        else if (_viewModel.IsReleaseNotesOpen && e.Key == Key.Escape)
        {
            _viewModel.IsReleaseNotesOpen = false;
        }
        else if (_viewModel.IsHelpOpen && e.Key == Key.Escape)
        {
            // The search first, then the window.
            if (_viewModel.IsShortcutSearching) _viewModel.ShortcutQuery = "";
            else _viewModel.IsHelpOpen = false;
        }
        else if (_viewModel.IsHelpOpen && e.Key == Key.Tab && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            _viewModel.NextShortcutTab(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
        }
        else if (_viewModel.IsSettingsOpen && e.Key == Key.Escape)
        {
            _viewModel.IsSettingsOpen = false;
        }
        else if (_viewModel.IsSectionOverlayOpen && e.Key == Key.Escape && Keyboard.FocusedElement is not TextBox)
        {
            _viewModel.IsSectionOverlayOpen = false;
        }
        else if (ctrl && e.Key is >= Key.D1 and <= Key.D6)
        {
            // Not handled: the window's key bindings still open the screen.
            _viewModel.Section = e.Key == Key.D6 ? SidebarSection.Calendar : SidebarSection.Tasks;
            return;
        }
        else if (ctrl && e.Key == Key.F)
        {
            FocusSearch();
        }
        else if (ctrl && e.Key == Key.N)
        {
            // Same as the sidebar's "＋ Yeni iş": from the calendar it goes to today first, where the box can be seen.
            _viewModel.NewTaskCommand.Execute(null);
        }
        else if (e.Key == Key.F5)
        {
            _viewModel.ReloadCommand.Execute(null);
        }
        else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Tab && NoOverlay)
        {
            // Between the task pages and the calendar.
            (_viewModel.IsCalendarView ? _viewModel.ShowTodayCommand : _viewModel.ShowCalendarCommand).Execute(null);
        }
        else if (e.Key is Key.Left or Key.Right && Keyboard.Modifiers == ModifierKeys.Control && NoOverlay && !IsTyping)
        {
            _viewModel.StepDay(e.Key == Key.Left ? -1 : 1);
        }
        else if (NoOverlay && !IsTyping && _viewModel.IsListView && !Composer.IsKeyboardFocusWithin && !DetailPanel.IsKeyboardFocusWithin
                 && ListKey(e.Key == Key.System ? e.SystemKey : e.Key))
        {
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Left)
        {
            _viewModel.GoBack();
        }
        else if (Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Right)
        {
            _viewModel.GoForward();
        }
        else if (ctrl && e.Key == Key.Z && Keyboard.FocusedElement is not TextBox { IsReadOnly: false })
        {
            _viewModel.UndoCommand.Execute(null);
        }
        else
        {
            return;
        }
        e.Handled = true;
    }

    /// <summary>Nothing lies over the window that the keys should go to instead.</summary>
    private bool NoOverlay => _guideIndex < 0 && !_viewModel.IsSettingsOpen && !_viewModel.IsHelpOpen && !_viewModel.IsReleaseNotesOpen
        && !_viewModel.IsUserGuideOpen && !_viewModel.IsNamePromptOpen && !_viewModel.IsDeleteConfirmOpen && !_viewModel.IsConflictOpen
        && !_viewModel.IsPickingDay;

    /// <summary>A box to type in has the keys; the read-only text in a row (it can be selected) does not.</summary>
    private static bool IsTyping => Keyboard.FocusedElement is TextBox { IsReadOnly: false } or PasswordBox;

    /// <summary>The task rows on screen, top to bottom.</summary>
    private List<Border> VisibleRows() => Find(ContentScroll, "Row", b => b.DataContext is EntryViewModel);

    /// <summary>The sub-task lines shown under a task row.</summary>
    private static List<Border> SubRowsOf(Border row) => Find(row, "SubRow", b => b.DataContext is BodyItemViewModel { IsTask: true });

    private static List<Border> Find(DependencyObject root, string name, Func<Border, bool> wanted)
    {
        var found = new List<Border>();
        void Walk(DependencyObject parent)
        {
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is UIElement { IsVisible: false }) continue;
                if (child is Border border && border.Name == name && wanted(border)) found.Add(border);
                Walk(child);
            }
        }
        Walk(root);
        return found;
    }

    /// <summary>The task row holding the keyboard, itself or through something inside it.</summary>
    private Border? FocusedRowElement => Ancestor("Row");

    /// <summary>The sub-task line holding the keyboard.</summary>
    private Border? FocusedSubRowElement => Ancestor("SubRow");

    private EntryViewModel? FocusedRow => FocusedRowElement?.DataContext as EntryViewModel;

    private Border? Ancestor(string name)
    {
        for (var at = Keyboard.FocusedElement as DependencyObject; at is not null; at = VisualTreeHelper.GetParent(at))
        {
            if (at is Border border && border.Name == name) return border;
            if (at == ContentScroll) return null;
        }
        return null;
    }

    /// <summary>
    /// A task asked for from another day's line: once the page is drawn, the keyboard goes onto its row (lit as the
    /// chosen one) and the list scrolls to it.
    /// </summary>
    private void GoToRow(Entry entry)
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (VisibleRows().FirstOrDefault(r => r.DataContext is EntryViewModel row && row.Entry == entry) is { } found) Reach(found);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private static void Reach(Border element)
    {
        element.Focus();
        element.BringIntoView();
    }

    /// <summary>
    /// Up/Down: every task; Tab/Shift+Tab: only unfinished ones. From nothing the first (or last) one is taken.
    /// False at either end, so Tab can go on to the next part of the window.
    /// </summary>
    private bool StepRow(int step, bool openOnly)
    {
        var rows = VisibleRows();
        var current = FocusedRowElement;
        var at = current is null ? (step > 0 ? -1 : rows.Count) : rows.IndexOf(current);
        for (var next = at + step; next >= 0 && next < rows.Count; next += step)
        {
            if (openOnly && rows[next].DataContext is EntryViewModel { Entry.HasOpenWork: false }) continue;
            Reach(rows[next]);
            return true;
        }
        return false;
    }

    /// <summary>Up/Down on a sub-task: its siblings; past the first back to the task, past the last on to the next task.</summary>
    private void StepSubRow(int step)
    {
        if (FocusedRowElement is not { } row || FocusedSubRowElement is not { } sub) return;
        var subs = SubRowsOf(row);
        var next = subs.IndexOf(sub) + step;
        if (next < 0) Reach(row);
        else if (next >= subs.Count) StepRow(1, openOnly: false);
        else Reach(subs[next]);
    }

    /// <summary>
    /// After a change the list is drawn again and the row may be a new one: the keyboard goes back to the same task,
    /// or, when it moved away (ticked, sent to another day), to whatever now stands at its place.
    /// </summary>
    private void RestoreRow(Entry? entry = null, int index = -1)
    {
        if (entry is null && index < 0)
        {
            if (_keyEntry is null) return;
            entry = _keyEntry;
            index = _keyIndex;
        }
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var rows = VisibleRows();
            var same = entry is null ? null : rows.FirstOrDefault(r => ((EntryViewModel)r.DataContext).Entry == entry);
            var target = same ?? (rows.Count == 0 ? null : rows[Math.Max(0, Math.Min(index, rows.Count - 1))]);
            if (target is not null) Reach(target);
        }), System.Windows.Threading.DispatcherPriority.ContextIdle);
    }

    private Entry? _keyEntry;
    private int _keyIndex;

    /// <summary>The keys of the task list (see docs\superpowers\specs\2026-11-24-klavye-gezinme-design.md). True when used.</summary>
    private bool ListKey(Key key)
    {
        var mods = Keyboard.Modifiers;
        var rowElement = FocusedRowElement;
        var row = rowElement?.DataContext as EntryViewModel;
        var sub = FocusedSubRowElement?.DataContext as BodyItemViewModel;
        var index = rowElement is null ? -1 : VisibleRows().IndexOf(rowElement);
        if (row is not null)
        {
            _keyEntry = row.Entry;
            _keyIndex = index;
        }

        switch (key)
        {
            case Key.Down or Key.Up when mods == ModifierKeys.None:
                _keysUsed = true;
                if (sub is not null) StepSubRow(key == Key.Down ? 1 : -1);
                else StepRow(key == Key.Down ? 1 : -1, openOnly: false);
                return true;
            case Key.Tab when mods is ModifierKeys.None or ModifierKeys.Shift:
                return StepRow(mods == ModifierKeys.Shift ? -1 : 1, openOnly: true);
            case Key.Home or Key.End when mods == ModifierKeys.None:
                var all = VisibleRows();
                if (all.Count == 0) return false;
                Reach(key == Key.Home ? all[0] : all[all.Count - 1]);
                return true;
            case Key.Up or Key.Down when mods == ModifierKeys.Control:
                StepPage(key == Key.Down ? 1 : -1, row is not null);
                return true;
        }

        if (row is null || rowElement is null) return false;

        switch (key)
        {
            case Key.Right when mods == ModifierKeys.None && sub is null:
                if (!row.BodyItems.Any(b => b.IsTask)) return true;
                if (row.IsCollapsed) row.ToggleBodyCommand.Execute(null);
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (SubRowsOf(rowElement).FirstOrDefault() is { } first) Reach(first);
                }), System.Windows.Threading.DispatcherPriority.ContextIdle);
                return true;
            case Key.Left when mods == ModifierKeys.None:
                if (sub is not null) Reach(rowElement);
                else if (row.ShowBody && row.BodyItems.Any(b => b.IsTask)) row.ToggleBodyCommand.Execute(null);
                return true;
            case Key.Space when mods == ModifierKeys.None:
                if (sub is not null) _viewModel.Toggle(sub);
                else if (!row.HasNoCheck) _viewModel.Toggle(row, !row.IsDone);
                RestoreRow(sub is null ? null : row.Entry, index);
                return true;
            case Key.F2 when mods == ModifierKeys.None:
                (sub?.EditCommand ?? row.EditCommand).Execute(null);
                return true;
            case Key.Enter when mods == ModifierKeys.None:
                if (sub?.Task is { } task) _viewModel.OpenDetail(row.Entry, task);
                else _viewModel.OpenDetail(row.Entry);
                RestoreRow(row.Entry, index);
                return true;
            case Key.Enter when mods == ModifierKeys.Shift && sub is null:
                // One task into the selection or out of it, as Ctrl+click does; Shift+↑/↓ takes a run of them.
                if (_viewModel.CanSelectRows) _viewModel.ToggleSelection(row);
                RestoreRow(row.Entry, index);
                return true;
            case Key.Delete when mods == ModifierKeys.None:
                // Asked first; whichever way the window closes, the row keeps the keyboard.
                _afterDeleteConfirm = () => RestoreRow(sub is null ? null : row.Entry, index);
                if (sub is not null) _viewModel.AskToDelete(sub);
                else _viewModel.AskToDelete(row);
                return true;
            case Key.Up or Key.Down when mods == ModifierKeys.Alt && sub is null:
                if (!row.CanReorder) return true;
                if (key == Key.Up) _viewModel.MoveUp(row);
                else _viewModel.MoveDown(row);
                RestoreRow(row.Entry, index);
                return true;
            case Key.Up or Key.Down when mods == ModifierKeys.Shift && sub is null:
                if (!row.IsSelected && _viewModel.CanSelectRows) _viewModel.ToggleSelection(row);
                if (StepRow(key == Key.Down ? 1 : -1, openOnly: false) && FocusedRow is { IsSelected: false } next && _viewModel.CanSelectRows)
                    _viewModel.ToggleSelection(next);
                return true;
            case Key.Left or Key.Right when mods == (ModifierKeys.Control | ModifierKeys.Shift) && sub is null:
                _viewModel.MoveByDays(row, key == Key.Left ? -1 : 1);
                RestoreRow(null, index);
                return true;
            case Key.M when mods == ModifierKeys.None && sub is null:
                OpenMoveMenu(row, rowElement, index);
                return true;
            case Key.Escape when mods == ModifierKeys.None:
                FocusRest.Focus();
                return true;
        }
        return false;
    }

    /// <summary>M on a task: where it goes, each with its letter; with several selected, they all go.</summary>
    private void OpenMoveMenu(EntryViewModel row, Border element, int index)
    {
        var today = _viewModel.TodayDate;
        var menu = new ContextMenu { PlacementTarget = element, Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom, HorizontalOffset = 40 };
        void Add(string header, Action act)
        {
            var item = new MenuItem { Header = header };
            item.Click += (_, _) =>
            {
                act();
                RestoreRow(null, index);
            };
            menu.Items.Add(item);
        }
        Add("_Bugüne al", () => _viewModel.MoveDraggedToDay(row, today));
        Add("_Yarına", () => _viewModel.MoveDraggedToDay(row, today.AddDays(1)));
        Add("_Tarihsize al", () => _viewModel.MoveDraggedToDay(row, Day.Undated));
        var pick = new MenuItem { Header = "_Gün seç…", InputGestureText = "oklar, Enter" };
        pick.Click += (_, _) => _viewModel.BeginPickDayByKeys(row);
        menu.Items.Add(pick);
        menu.IsOpen = true;
        if (menu.Items[0] is MenuItem first) first.Focus();
    }

    /// <summary>Ctrl+Up/Down: the task pages in the sidebar's order; the keyboard goes to the new page's first task if it was on one.</summary>
    private void StepPage(int step, bool onRow)
    {
        var pages = new[] { _viewModel.ShowTodayCommand, _viewModel.ShowOpenCommand, _viewModel.ShowFutureCommand, _viewModel.ShowUndatedCommand, _viewModel.ShowAllCommand };
        var at = _viewModel.IsOpenView ? 1 : _viewModel.IsFutureView ? 2 : _viewModel.IsUndatedView ? 3 : _viewModel.IsAllView ? 4 : 0;
        var next = (at + step + pages.Length) % pages.Length;
        pages[next].Execute(null);
        if (onRow) RestoreRow(null, 0);
    }

    /// <summary>Ctrl+F: the search box in sight. On a narrow window that is the one over the list, the sidebar's otherwise.</summary>
    private void FocusSearch()
    {
        var box = new[] { RailSearchBox, SearchBox, ClassicSearchBox }.FirstOrDefault(b => b.IsVisible) ?? SearchBox;
        box.Focus();
        box.SelectAll();
    }

    /// <summary>
    /// The window's own width decides whether the list fits beside the strip. The grid is not laid out while the
    /// first-run screen covers it, and then has no width to judge by.
    /// </summary>
    private const double RailOnlyBelow = 1000;

    private void UpdateRailOnly()
    {
        // The classic sidebar keeps its width whatever the window does.
        if (MainGrid.ActualWidth > 0) _viewModel.IsRailOnly = !_viewModel.IsClassicSidebar && MainGrid.ActualWidth < RailOnlyBelow;
    }

    private void MainGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        UpdateRailOnly();
    }

    /// <summary>
    /// On a narrow window the list lies over the middle for one choice: a screen, a day, a file, a calendar size.
    /// Paging the month, opening a group or a file's menu keeps it open.
    /// </summary>
    private void SectionPane_Click(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.IsRailOnly || e.OriginalSource is not Button button) return;

        var choice = button.Style == FindResource("NavItem") || button.Style == FindResource("MiniDay") || button.Style == FindResource("ScaleButton")
            || button == BackToTodayButton || button.DataContext is FileRow row && button.Command == row.SelectCommand;
        if (choice) _viewModel.IsSectionOverlayOpen = false;
    }

    private void Grip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStart = e.GetPosition(this);
        _dragged = false;
    }

    private void Grip_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragged) return;

        var moved = e.GetPosition(this) - _dragStart;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var element = (FrameworkElement)sender;
        if (element.DataContext is not EntryViewModel row) return;

        _dragged = true;
        DragDrop.DoDragDrop(element, new DataObject(DragFormat, row), DragDropEffects.Move);
        ClearDropMark();
        ClearDropCell();
        CloseDragOverlay();
    }

    private void Grip_Click(object sender, RoutedEventArgs e)
    {
        if (_dragged)
        {
            _dragged = false;
            return;
        }

        var button = (Button)sender;
        if (button.DataContext is not EntryViewModel row) return;

        var menu = new ContextMenu { PlacementTarget = button };
        if (row.CanReorder)
        {
            menu.Items.Add(MenuItemFor("Yukarı taşı", () => _viewModel.MoveUp(row)));
            menu.Items.Add(MenuItemFor("Aşağı taşı", () => _viewModel.MoveDown(row)));
        }
        if (_viewModel.CanSelectRows)
        {
            if (menu.Items.Count > 0) menu.Items.Add(new Separator());
            menu.Items.Add(MenuItemFor(row.IsSelected ? "Seçimden çıkar" : "Seç", () => _viewModel.ToggleSelection(row)));
        }
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor(_viewModel.MovesAsGroup(row) ? "Seçilenleri başka güne taşı" : "Başka güne taşı", () => _viewModel.BeginPickDay(row)));
        if (row.CanBringToToday) menu.Items.Add(MenuItemFor("Bugüne al", () => _viewModel.BringToToday(row)));
        if (row.CanMoveToFuture) menu.Items.Add(MenuItemFor("Yarına taşı", () => _viewModel.MoveToTomorrow(row)));
        if (row.CanMakeUndated) menu.Items.Add(MenuItemFor("Tarihsiz yap", () => _viewModel.MakeUndated(row)));
        if (menu.Items.Count > 0) menu.IsOpen = true;
    }

    private static MenuItem MenuItemFor(string text, Action action)
    {
        var item = new MenuItem { Header = text };
        item.Click += (_, _) => action();
        return item;
    }

    private void ContentScroll_PreviewDragOver(object sender, DragEventArgs e)
    {
        ClearDropMark();
        var dragged = e.Data.GetData(DragFormat) as EntryViewModel;
        if (dragged is not null) ScrollNearEdge(e);
        var target = dragged is null ? null : RowUnder(e, dragged);

        if (target is not null)
        {
            if (IsOverUpperHalf(e, target.Element)) target.Row.ShowDropAbove = true;
            else target.Row.ShowDropBelow = true;
            _dropMarked = target.Row;
        }

        e.Effects = dragged is not null && !IsOverItself(e, dragged) && (target is not null || _viewModel.ShownDay is not null)
            ? DragDropEffects.Move
            : DragDropEffects.None;
        e.Handled = true;
    }

    /// <summary>
    /// Near the top or bottom of the list a drag scrolls it, faster the closer to the edge, so a row that is
    /// out of sight can still be reached. Time based, since Windows does not report a drag at a steady rate.
    /// </summary>
    private void ScrollNearEdge(DragEventArgs e)
    {
        const double band = 44;
        const double fastest = 650;

        var y = e.GetPosition(ContentScroll).Y;
        var height = ContentScroll.ActualHeight;
        var depth = y < band ? y - band : y > height - band ? y - (height - band) : 0;
        if (depth == 0)
        {
            _edgeScrollAt = null;
            return;
        }

        var now = Environment.TickCount;
        var elapsed = _edgeScrollAt is int last ? Math.Min(now - last, 100) : 16;
        _edgeScrollAt = now;
        var speed = fastest * Math.Max(-1, Math.Min(1, depth / band));
        ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset + speed * elapsed / 1000);
    }

    private void ContentScroll_PreviewDrop(object sender, DragEventArgs e)
    {
        _edgeScrollAt = null;
        ClearDropMark();
        e.Handled = true;
        if (e.Data.GetData(DragFormat) is not EntryViewModel dragged) return;

        // Letting go over its own row means the drag was called off.
        if (IsOverItself(e, dragged)) return;

        var target = RowUnder(e, dragged);
        if (target is not null)
        {
            var row = target.Row;
            var before = IsOverUpperHalf(e, target.Element) ? row.Entry : _viewModel.EntryAfter(row);
            MoveAfterDrag(dragged, row.Date, before);
        }
        else if (_viewModel.ShownDay is DateTime day)
        {
            MoveAfterDrag(dragged, day, null);
        }
    }

    private void ContentScroll_PreviewDragLeave(object sender, DragEventArgs e)
    {
        _edgeScrollAt = null;
        ClearDropMark();
    }

    /// <summary>
    /// A row dragged onto İşler or Takvim on the strip opens that section, so the drag can go on to Bugün, Gelecek,
    /// Tarihsiz or a day of the small calendar. On a narrow window the list opens over the middle until the drag ends.
    /// The strip itself takes no drop.
    /// </summary>
    private void Rail_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = DragDropEffects.None;
        e.Handled = true;
        if (!e.Data.GetDataPresent(DragFormat)) return;

        // Every drop target (the day lists and the small calendar) is under İşler.
        _viewModel.Section = SidebarSection.Tasks;
        if (_viewModel.IsRailOnly && !_viewModel.IsSectionOverlayOpen)
        {
            _viewModel.IsSectionOverlayOpen = true;
            _dragOpenedOverlay = true;
        }
    }

    /// <summary>The list a drag opened over the middle closes when the drag is dropped or called off.</summary>
    private void CloseDragOverlay()
    {
        if (!_dragOpenedOverlay) return;

        _dragOpenedOverlay = false;
        _viewModel.IsSectionOverlayOpen = false;
    }

    private void TodayNav_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void FutureNav_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DragFormat) is EntryViewModel dragged) MoveToDayAfterDrag(dragged, _viewModel.Tomorrow);
    }

    private void UndatedNav_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DragFormat) is EntryViewModel dragged) MoveToDayAfterDrag(dragged, Day.Undated);
    }

    private void FileMenu_Click(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        if (button.DataContext is FileRow row) ShowFileMenu(button, row);
    }

    private void FileRow_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        var element = (FrameworkElement)sender;
        if (element.DataContext is not FileRow row) return;

        ShowFileMenu(element, row);
        e.Handled = true;
    }

    private void ShowFileMenu(FrameworkElement target, FileRow row)
    {
        var menu = new ContextMenu { PlacementTarget = target };
        if (_viewModel.CanRenameFile(row.Path))
        {
            // In the picker the name is edited in place; the classic list asks in a box.
            menu.Items.Add(MenuItemFor("Yeniden adlandır", () =>
            {
                if (IsWithin(target, FilesPanel)) _viewModel.BeginInlineRename(row);
                else _viewModel.BeginRenameFile(row.Path);
            }));
        }

        var groups = new MenuItem { Header = "Gruba taşı" };
        foreach (var name in _viewModel.FileGroupNames.Where(n => n != row.GroupName))
        {
            groups.Items.Add(MenuItemFor(name, () => _viewModel.MoveFileToGroup(row.Path, name)));
        }
        if (groups.Items.Count > 0) groups.Items.Add(new Separator());
        groups.Items.Add(MenuItemFor("Yeni grup…", () => _viewModel.BeginNewGroup(row.Path)));
        menu.Items.Add(groups);
        if (row.IsGrouped) menu.Items.Add(MenuItemFor("Gruptan çıkar", () => _viewModel.MoveFileToGroup(row.Path, null)));
        menu.Items.Add(MenuItemFor("Not Defteri'nde aç", () => _viewModel.OpenFileInEditor(row.Path)));
        menu.Items.Add(new Separator());
        menu.Items.Add(MenuItemFor(row.RemoveTip, () => row.RemoveCommand.Execute(null)));
        menu.IsOpen = true;
    }

    private void NamePrompt_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (!(bool)e.NewValue) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            NamePromptBox.Focus();
            NamePromptBox.SelectAll();
        }));
    }

    private void NamePromptBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) _viewModel.ConfirmNamePromptCommand.Execute(null);
        else if (e.Key == Key.Escape) _viewModel.CancelNamePromptCommand.Execute(null);
        else return;

        e.Handled = true;
    }

    private void TodayNav_Drop(object sender, DragEventArgs e)
    {
        e.Handled = true;
        if (e.Data.GetData(DragFormat) is EntryViewModel dragged) MoveToDayAfterDrag(dragged, _viewModel.TodayDate);
    }

    // The move rebuilds the rows, so it waits until the drag that started on one of them is over.
    private void MoveAfterDrag(EntryViewModel row, DateTime date, Entry? before) =>
        Dispatcher.BeginInvoke(new Action(() => _viewModel.MoveDragged(row, date, before)));

    private void MoveToDayAfterDrag(EntryViewModel row, DateTime date) =>
        Dispatcher.BeginInvoke(new Action(() => _viewModel.MoveDraggedToDay(row, date)));

    private const string SubDragFormat = "IsGunlugu.DetailSubTask";
    private DetailSubTask? _subPressed;
    private Point _subPressedAt;
    private DetailSubTask? _subDropMark;

    private void DetailSubGrip_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _subPressed = ((FrameworkElement)sender).DataContext as DetailSubTask;
        _subPressedAt = e.GetPosition(this);
    }

    private void DetailSubGrip_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_subPressed is null || e.LeftButton != MouseButtonState.Pressed) return;

        var moved = e.GetPosition(this) - _subPressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _subPressed;
        _subPressed = null;
        CommitDetail();
        DragDrop.DoDragDrop((DependencyObject)sender, new DataObject(SubDragFormat, item), DragDropEffects.Move);
        ClearSubDrop();
    }

    /// <summary>Where a dragged sub-task would land over this row: its new place in the order, or -1 for no move.</summary>
    private static int SubDropIndex(DragEventArgs e, FrameworkElement row, out DetailSubTask? target, out bool below)
    {
        target = row.DataContext as DetailSubTask;
        below = e.GetPosition(row).Y > row.ActualHeight / 2;
        if (target is null || e.Data.GetData(SubDragFormat) is not DetailSubTask dragged) return -1;

        var slot = target.Index + (below ? 1 : 0);
        var to = slot > dragged.Index ? slot - 1 : slot;
        return to == dragged.Index ? -1 : to;
    }

    private void DetailSubTask_DragOver(object sender, DragEventArgs e)
    {
        ClearSubDrop();
        var to = SubDropIndex(e, (FrameworkElement)sender, out var target, out var below);
        if (to >= 0 && target is not null)
        {
            if (below) target.ShowDropBelow = true;
            else target.ShowDropAbove = true;
            _subDropMark = target;
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void DetailSubTask_DragLeave(object sender, DragEventArgs e) => ClearSubDrop();

    private void DetailSubTask_Drop(object sender, DragEventArgs e)
    {
        ClearSubDrop();
        e.Handled = true;
        var to = SubDropIndex(e, (FrameworkElement)sender, out _, out _);
        if (to < 0 || e.Data.GetData(SubDragFormat) is not DetailSubTask dragged) return;

        Dispatcher.BeginInvoke(new Action(() => _viewModel.MoveDetailSubTask(dragged.Index, to)));
    }

    private void ClearSubDrop()
    {
        if (_subDropMark is null) return;
        _subDropMark.ShowDropAbove = false;
        _subDropMark.ShowDropBelow = false;
        _subDropMark = null;
    }

    /// <summary>Alt+Up and Alt+Down in a sub-task's title move it one place; the cursor stays in the same title.</summary>
    private void DetailSubTitle_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Alt || e.SystemKey is not (Key.Up or Key.Down)) return;
        e.Handled = true;

        var box = (TextBox)sender;
        if (box.DataContext is not DetailSubTask item) return;

        box.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        var to = item.Index + (e.SystemKey == Key.Up ? -1 : 1);
        _viewModel.MoveDetailSubTask(item.Index, to);
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (DetailSubTaskList.ItemContainerGenerator.ContainerFromIndex(to) is DependencyObject row && FindChild<TextBox>(row) is { } title)
            {
                title.Focus();
                title.CaretIndex = title.Text.Length;
            }
        }), DispatcherPriority.Loaded);
    }

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            if (FindChild<T>(child) is { } deeper) return deeper;
        }
        return null;
    }

    private const string WeekDragFormat = "IsGunlugu.WeekLine";
    private WeekLine? _weekPressed;
    private Point _weekPressedAt;
    private WeekDayCard? _weekDropCard;

    /// <summary>A press on a task of the week page may become a drag; a press beside the tasks is a plain click.</summary>
    private void WeekDay_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _weekPressed = (e.OriginalSource as FrameworkElement)?.DataContext as WeekLine
            ?? ((e.OriginalSource as FrameworkContentElement)?.Parent as FrameworkElement)?.DataContext as WeekLine;
        _weekPressedAt = e.GetPosition(this);
    }

    /// <summary>
    /// The day is a button, which holds the mouse once pressed, so the move is watched on the day rather than the
    /// task. After the drag the button lets go without clicking, so the day does not open.
    /// </summary>
    private void WeekDay_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_weekPressed is null || e.LeftButton != MouseButtonState.Pressed) return;

        var moved = e.GetPosition(this) - _weekPressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var line = _weekPressed;
        _weekPressed = null;
        var day = (UIElement)sender;
        DragDrop.DoDragDrop(day, new DataObject(WeekDragFormat, line), DragDropEffects.Move);
        day.ReleaseMouseCapture();
        ClearWeekDrop();
    }

    /// <summary>
    /// A click on a task opens it in the panel on the right; the day's own click is stopped, so the page stays on the
    /// week. A click beside the tasks still opens the day.
    /// </summary>
    private void WeekDay_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_weekPressed is not { } line) return;
        _weekPressed = null;
        e.Handled = true;
        ((UIElement)sender).ReleaseMouseCapture();
        _viewModel.OpenDetail(line.Entry);
    }

    private void WeekDay_DragOver(object sender, DragEventArgs e)
    {
        var card = ((FrameworkElement)sender).DataContext as WeekDayCard;
        var line = e.Data.GetData(WeekDragFormat) as WeekLine;
        if (card != _weekDropCard) ClearWeekDrop();

        if (card is not null && line is not null && line.Date != card.Date)
        {
            card.IsDropTarget = true;
            _weekDropCard = card;
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void WeekDay_DragLeave(object sender, DragEventArgs e) => ClearWeekDrop();

    private void WeekDay_Drop(object sender, DragEventArgs e)
    {
        ClearWeekDrop();
        e.Handled = true;
        if (((FrameworkElement)sender).DataContext is not WeekDayCard card || e.Data.GetData(WeekDragFormat) is not WeekLine line) return;

        Dispatcher.BeginInvoke(new Action(() => _viewModel.MoveFromWeek(line.Entry, card.Date)));
    }

    private void ClearWeekDrop()
    {
        if (_weekDropCard is null) return;
        _weekDropCard.IsDropTarget = false;
        _weekDropCard = null;
    }

    private void MiniCalendar_DragOver(object sender, DragEventArgs e)
    {
        ClearDropCell();
        var cell = CellAt(e);
        if (cell is not null && e.Data.GetDataPresent(DragFormat))
        {
            cell.IsDropTarget = true;
            _dropCell = cell;
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void MiniCalendar_DragLeave(object sender, DragEventArgs e) => ClearDropCell();

    private void MiniCalendar_Drop(object sender, DragEventArgs e)
    {
        ClearDropCell();
        e.Handled = true;

        var cell = CellAt(e);
        if (cell is null || e.Data.GetData(DragFormat) is not EntryViewModel dragged) return;

        Dispatcher.BeginInvoke(new Action(() => _viewModel.MoveDraggedToDate(dragged, cell.Date)));
    }

    private static MonthCell? CellAt(DragEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { DataContext: MonthCell cell }) return cell;

            current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private void ClearDropCell()
    {
        if (_dropCell is null) return;

        _dropCell.IsDropTarget = false;
        _dropCell = null;
    }

    /// <summary>A plain click on a task opens it in the side panel. Buttons, boxes and the grip keep their own clicks.</summary>
    private void Row_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None || ((FrameworkElement)sender).DataContext is not EntryViewModel row || row.IsEditing) return;
        if (!OpensDetail(e.OriginalSource as DependencyObject, (DependencyObject)sender)) return;

        CommitDetail();
        _viewModel.OpenDetail(row.Entry, SubTaskAt(e.OriginalSource as DependencyObject, (DependencyObject)sender));
    }

    /// <summary>The sub-task under the click, when the click landed on one rather than on the task's own line.</summary>
    private static SubTask? SubTaskAt(DependencyObject? source, DependencyObject row)
    {
        for (var current = source; current != null && current != row; current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current))
        {
            if (current is FrameworkElement { Name: "SubRow", DataContext: BodyItemViewModel { Task: { } task } }) return task;
        }
        return null;
    }

    /// <summary>A click on empty space in the list, away from any task, closes the panel.</summary>
    /// <summary>
    /// A click anywhere outside the side panel closes it: the menu, the header, the box at the bottom, empty list
    /// space. A click on a task moves the panel to that task instead, a scroll bar only scrolls, and while a day is
    /// being picked for the task the small calendar must stay clickable.
    /// </summary>
    private void Window_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        // The tour's invite explains what is open on the screen, so pressing it must not close the panel or a list.
        if (IsWithin(e.OriginalSource as DependencyObject, TourInvite)) return;

        if (_viewModel.IsFilePickerOpen && _guideIndex < 0 && !IsWithin(e.OriginalSource as DependencyObject, FilesPanel, FilePickerButton))
        {
            _viewModel.IsFilePickerOpen = false;
        }

        if (_viewModel.IsSectionOverlayOpen && !_viewModel.IsPickingDay && _guideIndex < 0 && !IsWithin(e.OriginalSource as DependencyObject, SectionPane, Rail))
        {
            _viewModel.IsSectionOverlayOpen = false;
        }

        if (!_viewModel.IsDetailOpen || _viewModel.IsPickingDay || Keyboard.Modifiers != ModifierKeys.None || _guideIndex >= 0
            || _viewModel.IsSettingsOpen || _viewModel.IsHelpOpen || _viewModel.IsReleaseNotesOpen || _viewModel.IsNamePromptOpen
            || _viewModel.IsDeleteConfirmOpen || _viewModel.IsConflictOpen || _viewModel.IsUserGuideOpen)
        {
            return;
        }

        for (var current = e.OriginalSource as DependencyObject; current != null; current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current))
        {
            if (current == DetailPanel || current is FrameworkElement { Name: "Row" } or System.Windows.Controls.Primitives.ScrollBar) return;
        }

        CommitDetail();
        _viewModel.CloseDetail();
    }

    private static bool IsWithin(DependencyObject? source, params DependencyObject[] parts)
    {
        for (var current = source; current != null; current = LogicalTreeHelper.GetParent(current) ?? (current is Visual ? VisualTreeHelper.GetParent(current) : null))
        {
            if (parts.Contains(current)) return true;
        }
        return false;
    }

    private UIElement? _scrimPressed;

    /// <summary>The dark space around a settings, shortcuts or question box: pressed and let go there, it closes the box.</summary>
    private void Scrim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) =>
        _scrimPressed = e.OriginalSource == sender ? (UIElement)sender : null;

    private void Scrim_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var pressed = _scrimPressed;
        _scrimPressed = null;
        if (e.OriginalSource != sender || pressed != sender) return;

        var close = sender == HelpScrim ? _viewModel.ToggleHelpCommand
            : sender == SettingsScrim ? _viewModel.ToggleSettingsCommand
            : sender == ReleaseNotesScrim ? _viewModel.CloseReleaseNotesCommand
            : sender == NamePromptScrim ? _viewModel.CancelNamePromptCommand
            : sender == DeleteScrim ? _viewModel.CancelDeleteCommand
            : null;
        close?.Execute(null);
        e.Handled = true;
    }

    /// <summary>
    /// The week as seven columns needs room: from about a 1360 wide window. A 1366x768 screen gives a maximised window
    /// near 730 high, so the height bar sits below that. Smaller windows get the days one under the other.
    /// </summary>
    private const double WeekColumnsMinWidth = 1360;
    private const double WeekColumnsMinHeight = 700;

    private void PlaceWeek()
    {
        var columns = ActualWidth >= WeekColumnsMinWidth && ActualHeight >= WeekColumnsMinHeight;
        WeekColumnsCard.Visibility = columns ? Visibility.Visible : Visibility.Collapsed;
        WeekRows.Visibility = columns ? Visibility.Collapsed : Visibility.Visible;
    }

    private const double DetailWidth = 340;
    private const double DetailMinWidth = 280;
    private const double ListMinWidth = 510;

    /// <summary>Beside the list while both fit; on a narrow window the panel lies over the list instead of squeezing it.</summary>
    private void ContentArea_SizeChanged(object sender, SizeChangedEventArgs e) => PlaceDetail();

    /// <summary>The widest the panel may be dragged: most of the window, leaving some list to click.</summary>
    private double DetailMaxWidth => Math.Max(DetailMinWidth, Math.Min(760, ContentArea.ActualWidth - 160));

    private void DetailResizer_DragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
    {
        DetailPanel.Width = Math.Max(DetailMinWidth, Math.Min(DetailMaxWidth, DetailPanel.Width - e.HorizontalChange));
        PlaceDetail();
    }

    private void DetailResizer_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e) =>
        _viewModel.DetailPanelWidth = Math.Round(DetailPanel.Width) == DetailWidth ? null : Math.Round(DetailPanel.Width);

    private void DetailResizer_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        DetailPanel.Width = DetailWidth;
        _viewModel.DetailPanelWidth = null;
        PlaceDetail();
        e.Handled = true;
    }

    private void PlaceDetail()
    {
        var over = ContentArea.ActualWidth - DetailPanel.Width < ListMinWidth;
        Grid.SetColumn(DetailPanel, over ? 0 : 1);
        Grid.SetColumnSpan(DetailPanel, over ? 2 : 1);
        DetailPanel.Effect = over ? new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 28, ShadowDepth = 0, Opacity = 0.45 } : null;
    }

    private void DetailSubTitle_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        ((TextBox)sender).GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        FocusRest.Focus();
        e.Handled = true;
    }

    /// <summary>
    /// The title is selectable text, a read-only box that swallows the plain mouse-up, so the row listens on the way
    /// down instead. Buttons, boxes and typing fields keep their own clicks, and dragging over the title to select
    /// part of it does not open the panel.
    /// </summary>
    private static bool OpensDetail(DependencyObject? source, DependencyObject row)
    {
        for (var current = source; current != null && current != row; current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current))
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase) return false;
            if (current is TextBox box && (!box.IsReadOnly || box.SelectionLength > 0)) return false;
        }
        return true;
    }

    /// <summary>Saves what was typed in the panel before it shows another task or closes.</summary>
    private void CommitDetail()
    {
        if (Keyboard.FocusedElement is TextBox typing && typing != DetailTitleBox && typing != DetailDescriptionBox && DetailPanel.IsAncestorOf(typing))
        {
            typing.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        }
        DetailTitleBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        DetailDescriptionBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    /// <summary>Enter saves and leaves the box, so the change is visibly taken.</summary>
    private void DetailTitleBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        DetailTitleBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        FocusRest.Focus();
        e.Handled = true;
    }

    /// <summary>Enter saves the description and leaves it; Alt+Enter or Shift+Enter starts a new line.</summary>
    /// <summary>The task's time box: ↑/↓ five minutes, Enter keeps what was typed.</summary>
    private void DetailTimeBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down)
        {
            _viewModel.StepDetailTime(e.Key == Key.Up ? 1 : -1);
            DetailTimeBox.CaretIndex = DetailTimeBox.Text.Length;
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            DetailTimeBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            FocusRest.Focus();
            e.Handled = true;
        }
    }

    private void CustomReminder_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _viewModel.AddCustomReminderCommand.Execute(null);
        e.Handled = true;
    }

    private void DetailDescriptionBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsAltEnter(e) || (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift))
        {
            InsertLine(DetailDescriptionBox);
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter) return;

        DetailDescriptionBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        FocusRest.Focus();
        e.Handled = true;
    }

    private void DetailSubTaskBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        _viewModel.AddDetailSubTask(DetailSubTaskBox.Text);
        DetailSubTaskBox.Clear();
        e.Handled = true;
    }

    private void Row_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.CanSelectRows || ((FrameworkElement)sender).DataContext is not EntryViewModel row) return;

        if (Keyboard.Modifiers == ModifierKeys.Control) _viewModel.ToggleSelection(row);
        else if (Keyboard.Modifiers == ModifierKeys.Shift) _viewModel.SelectRange(row);
        else return;

        e.Handled = true;
    }

    /// <summary>The row a drop would land next to: only rows of a single day's list, and not the dragged one.</summary>
    private static DropTarget? RowUnder(DragEventArgs e, EntryViewModel dragged)
    {
        var over = RowAt(e);
        return over is not null && over.Row.CanReorder && over.Row != dragged ? over : null;
    }

    /// <summary>Over its own row, or over another row travelling with it in the selection.</summary>
    private static bool IsOverItself(DragEventArgs e, EntryViewModel dragged)
    {
        var over = RowAt(e)?.Row;
        return over == dragged || (over is not null && dragged.IsSelected && over.IsSelected);
    }

    private static DropTarget? RowAt(DragEventArgs e)
    {
        var current = e.OriginalSource as DependencyObject;
        while (current is not null)
        {
            if (current is FrameworkElement { Name: "Row", DataContext: EntryViewModel row } element) return new DropTarget(row, element);

            current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current);
        }
        return null;
    }

    private static bool IsOverUpperHalf(DragEventArgs e, FrameworkElement element) =>
        e.GetPosition(element).Y < element.ActualHeight / 2;

    private void ClearDropMark()
    {
        if (_dropMarked is null) return;

        _dropMarked.ShowDropAbove = false;
        _dropMarked.ShowDropBelow = false;
        _dropMarked = null;
    }

    // ---- The add box ----

    private void DraftBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        // The old way still works: Alt+Enter starts a line under the title ("- x" a sub-task, a plain line the description).
        if (IsAltEnter(e) || (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Shift))
        {
            InsertLine((TextBox)sender);
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter) return;

        if (Keyboard.Modifiers == ModifierKeys.None) _viewModel.SubmitComposer(done: false);
        else if (Keyboard.Modifiers == ModifierKeys.Control) _viewModel.SubmitComposer(done: true);

        // Any other Enter does nothing: the box accepts returns only so that a pasted list keeps its lines.
        e.Handled = true;
    }

    private void ComposerDescriptionBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        // One line only: Enter finishes the description, the next Enter adds the task.
        if (Keyboard.Modifiers == ModifierKeys.Control) _viewModel.SubmitComposer(done: true);
        else _viewModel.ConfirmDescription();
        e.Handled = true;
    }

    private void ComposerSubTaskBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;

        if (Keyboard.Modifiers == ModifierKeys.Control) _viewModel.SubmitComposer(done: true);
        else _viewModel.AddComposerSubTask();
        e.Handled = true;
    }

    /// <summary>The box itself has the keyboard after a description or the sub-tasks are done: Enter adds.</summary>
    private void Composer_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.OriginalSource != Composer || e.Key != Key.Enter) return;

        if (Keyboard.Modifiers == ModifierKeys.None) _viewModel.SubmitComposer(done: false);
        else if (Keyboard.Modifiers == ModifierKeys.Control) _viewModel.SubmitComposer(done: true);
        else return;

        e.Handled = true;
    }

    // Decided by where the keyboard is, not by the remembered field, which can be stale.
    private void EscapeComposer()
    {
        if (ComposerSubTaskBox.IsKeyboardFocused) _viewModel.EndSubTaskEntry();
        else if (ComposerDescriptionBox.IsKeyboardFocused) _viewModel.ConfirmDescription();
        else _viewModel.CloseComposerCommand.Execute(null);
    }

    /// <summary>The keyboard left the box: Enter no longer adds from it, so Ekle is not lit and the hint is the plain one.</summary>
    private void Composer_IsKeyboardFocusWithinChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is false) _viewModel.ActiveComposerField = ComposerField.Title;
    }

    private void Composer_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.OriginalSource == Composer && _viewModel.IsComposerOpen) _viewModel.ActiveComposerField = ComposerField.Frame;
    }

    private void DraftBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_guideIndex >= 0) return;

        if (_viewModel.IsComposerOpen) _viewModel.ActiveComposerField = ComposerField.Title;
        else _viewModel.OpenComposerCommand.Execute(null);
    }

    private void ComposerDescriptionBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.ActiveComposerField = ComposerField.Description;

    private void ComposerSubTaskBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) =>
        _viewModel.ActiveComposerField = ComposerField.SubTask;

    /// <summary>A click anywhere on the closed line opens it, not only on the text.</summary>
    private void Composer_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_viewModel.IsComposerOpen && _guideIndex < 0) _viewModel.OpenComposerCommand.Execute(null);
    }

    private void ComposerDayButton_Click(object sender, RoutedEventArgs e)
    {
        var menu = new ContextMenu { PlacementTarget = (Button)sender, Placement = System.Windows.Controls.Primitives.PlacementMode.Top };
        void Add(string text, string key)
        {
            var item = MenuItemFor(text, () => _viewModel.ChooseComposerDayCommand.Execute(key));
            if (key == _viewModel.ComposerDayKey)
            {
                item.Header = "✓ " + text;
                item.SetResourceReference(ForegroundProperty, "AccentBrush");
            }
            menu.Items.Add(item);
        }
        Add("Bugün", "today");
        Add("Yarın", "tomorrow");
        Add("Tarihsiz", "undated");
        Add("Takvimden seç…", "calendar");
        menu.IsOpen = true;
    }

    /// <summary>Puts the keyboard where the add box asks: a field, the box itself, or out of the box once it closes.</summary>
    private void FocusComposer(ComposerField? field)
    {
        // Wait for the field to be shown; a collapsed element cannot take the keyboard.
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (_guideIndex >= 0) return;

            if (field is null)
            {
                if (Composer.IsKeyboardFocusWithin) FocusRest.Focus();
                return;
            }

            if (_viewModel.IsVisualWriting && field is ComposerField.Title or ComposerField.Description or ComposerField.SubTask)
            {
                DraftEditor.FocusEnd();
                return;
            }

            UIElement target = field switch
            {
                ComposerField.Description => ComposerDescriptionBox,
                ComposerField.SubTask => ComposerSubTaskBox,
                ComposerField.Frame => Composer,
                _ => DraftBox,
            };
            target.Focus();
            if (target is TextBox box) box.CaretIndex = box.Text.Length;
        }));
    }

    /// <summary>Starting to type while the list has the keyboard opens the add box with that letter in it.</summary>
    private void Window_TextInput(object sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length == 0 || !char.IsLetterOrDigit(e.Text[0])) return;
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Alt)) != 0) return;
        if (Keyboard.FocusedElement is System.Windows.Controls.Primitives.TextBoxBase) return;
        if (_guideIndex >= 0 || !_viewModel.IsListView || !_viewModel.HasFile || _viewModel.IsHelpOpen || _viewModel.IsSettingsOpen
            || _viewModel.IsNamePromptOpen || _viewModel.IsDeleteConfirmOpen || _viewModel.IsUserGuideOpen || _viewModel.IsReleaseNotesOpen || _viewModel.IsConflictOpen)
        {
            return;
        }

        _viewModel.OpenComposerCommand.Execute(null);
        if (_viewModel.IsVisualWriting)
        {
            DraftEditor.AppendToTitle(e.Text);
            e.Handled = true;
            return;
        }
        DraftBox.Focus();
        DraftBox.CaretIndex = DraftBox.Text.Length;
        DraftBox.SelectedText = e.Text;
        DraftBox.CaretIndex = DraftBox.Text.Length;
        e.Handled = true;
    }

    // ---- The visual box: the add box's own, and the one a row's pencil opens ----

    private void WireDraftEditor()
    {
        DraftEditor.Submit += done => _viewModel.SubmitComposer(done);
        DraftEditor.CurrentKindChanged += () => _viewModel.VisualKind = DraftEditor.CurrentKind;
    }

    /// <summary>"＋ Açıklama": in the visual box a note line under the title; in the plain one its own field, as before.</summary>
    private void ComposerNoteChip_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsVisualWriting) DraftEditor.FocusNote();
        else _viewModel.OpenDescriptionCommand.Execute(null);
    }

    private void ComposerSubTaskChip_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsVisualWriting) DraftEditor.AddSubTaskLine();
        else _viewModel.OpenSubTaskEntryCommand.Execute(null);
    }

    /// <summary>The pencil's visual box: Enter saves, Ctrl+Enter saves and ticks, Esc gives up, as in the plain one.</summary>
    private void EditEditor_Loaded(object sender, RoutedEventArgs e)
    {
        var editor = (TaskLinesEditor)sender;
        if (editor.Tag is true) return;
        editor.Tag = true;
        editor.Submit += done =>
        {
            if (editor.DataContext is not EntryViewModel row) return;
            if (done) _viewModel.SaveEdit(row, done: true);
            else row.SaveCommand.Execute(null);
        };
        editor.Cancel += () => (editor.DataContext as EntryViewModel)?.CancelCommand.Execute(null);
    }

    private static bool IsAltEnter(KeyEventArgs e) => e.Key == Key.System && e.SystemKey == Key.Return;

    private static void InsertLine(TextBox box)
    {
        box.SelectedText = Environment.NewLine;
        box.CaretIndex = box.SelectionStart + box.SelectionLength;
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;

        _viewModel.SearchText = "";
        FocusRest.Focus();
        e.Handled = true;
    }

    private void EditBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not EntryViewModel row) return;

        if (IsAltEnter(e)) InsertLine((TextBox)sender);
        else if (e.Key == Key.Escape) row.CancelCommand.Execute(null);
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) row.SaveCommand.Execute(null);
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) _viewModel.SaveEdit(row, done: true);
        else return; // Shift+Enter is a new line.

        e.Handled = true;
    }

    private void SubEditBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not BodyItemViewModel row) return;

        if (IsAltEnter(e)) InsertLine((TextBox)sender);
        else if (e.Key == Key.Escape) row.CancelCommand.Execute(null);
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) row.SaveCommand.Execute(null);
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control) _viewModel.SaveEdit(row, done: true);
        else return; // Shift+Enter is a new line.

        e.Handled = true;
    }

    private void SubTaskBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (((FrameworkElement)sender).DataContext is not EntryViewModel row) return;

        if (IsAltEnter(e))
        {
            InsertLine((TextBox)sender);
        }
        else if (e.Key == Key.Escape)
        {
            row.EndSubTaskCommand.Execute(null);
            FocusRest.Focus();
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None)
        {
            row.AddSubTaskCommand.Execute(null);
        }
        else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            _viewModel.AddSubTask(row, done: true);
        }
        else
        {
            return; // Shift+Enter is a new line.
        }

        e.Handled = true;
    }

    // Rows are rebuilt after every change, so a box that was open comes back as a new, already visible control.
    private void SubTaskBox_Loaded(object sender, RoutedEventArgs e)
    {
        var box = (TextBox)sender;
        if (box.DataContext is not EntryViewModel { IsAddingSubTask: true }) return;

        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() => box.Focus()));
    }

    /// <summary>
    /// The day squares on the year page: as wide as a month's column allows, but no taller than lets the three rows
    /// of months fit the page; at least 22, at most 34. A wide window used to flatten them into strips.
    /// </summary>
    private void YearScroll_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // A month card around its squares: 8 margin, 3 border, 16 padding across; 72 for the name, letters and edges down.
        var byWidth = (e.NewSize.Width / 4 - 27) / 7 - 2;
        var byHeight = ((e.NewSize.Height - 12) / 3 - 72) / 6 - 2;
        Resources["YearDayHeight"] = Math.Round(Math.Max(22, Math.Min(34, Math.Min(byWidth, byHeight))));
    }

    // Settings always opens on its first section.
    private void SettingsScrim_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is true) SettingsTabLook.IsChecked = true;
    }

    private void SubTaskPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;

        var box = (TextBox)((Panel)sender).Children[0];
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            box.Focus();
            box.CaretIndex = box.Text.Length; // a put-away box goes on where it was left
        }));
    }

    private void EditPanel_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is not true) return;

        var panel = (Panel)sender;
        var box = (TextBox)panel.Children[0];
        Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
        {
            if (_viewModel.IsVisualWriting && FindChild<TaskLinesEditor>(panel) is { } editor)
            {
                editor.FocusEnd();
                return;
            }
            box.Focus();
            box.CaretIndex = box.Text.Length;
        }));
    }

    // The note texts are TextBoxes; without this the wheel stops working over them.
    private void ContentScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        ContentScroll.ScrollToVerticalOffset(ContentScroll.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);

    private sealed class DropTarget(EntryViewModel row, FrameworkElement element)
    {
        public EntryViewModel Row { get; } = row;
        public FrameworkElement Element { get; } = element;
    }

    /// <summary>One part of the tour: its name, its invitation, when its screen is on show, and its few steps.</summary>
    public sealed class TourPart(string id, string invite, Func<bool> applies, List<GuideStep> steps)
    {
        public string Id { get; } = id;
        public string Invite { get; } = invite;
        public Func<bool> Applies { get; } = applies;
        public List<GuideStep> Steps { get; } = steps;
    }

    public sealed class GuideStep(ICommand view, Func<FrameworkElement> target, string title, string text, SidebarSection? section = null, string? classicText = null)
    {
        /// <summary>The new sidebar's section holding the part; it is selected before the step shows.</summary>
        public SidebarSection? Section { get; } = section;

        /// <summary>What the step says in the classic sidebar, when that differs.</summary>
        public string? ClassicText { get; } = classicText;

        /// <summary>The screen the step talks about.</summary>
        public ICommand View { get; } = view;

        /// <summary>Asked each time, since rows only exist once their screen is on show.</summary>
        public Func<FrameworkElement> Target { get; } = target;
        public string Title { get; } = title;
        public string Text { get; } = text;
    }
}
