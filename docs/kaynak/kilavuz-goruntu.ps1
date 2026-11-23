# Kılavuz görüntüleri: uygulamayı örnek notlarla gerçek penceresiyle açar, her parçayı kendi sınırından kırpıp
# açık (-l) ve koyu (-d) temada docs\kaynak\goruntu içine yazar. Uygulama bu klasörü exe'nin içine gömer.
#   powershell -STA -NoProfile -ExecutionPolicy Bypass -File docs\kaynak\kilavuz-goruntu.ps1
# Gerçek ayarlara, notlara ve Drive'a dokunmaz: her şey geçici klasörde olur.
param([string]$Out)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
if (-not $Out) { $Out = Join-Path $PSScriptRoot 'goruntu' }
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('kilavuz-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory $tmp | Out-Null
New-Item -ItemType Directory $Out -Force | Out-Null
$bin = Join-Path $tmp 'bin'
# The version the last release got, so Settings shows the right number in the pictures.
$version = ((Get-Content (Join-Path $root 'tools\surum.txt') -TotalCount 1) -split ' ')[0]
dotnet build (Join-Path $root 'src\IsGunlugu\IsGunlugu.csproj') -c Release -o $bin -v q -nologo "-p:Version=$version" | Out-Null
if ($LASTEXITCODE) { throw 'Derleme başarısız.' }

# Örnek notlar: bugünün ve çevresindeki günlerin işleri, hatırlatıcı, iptal ve başka gün biten alt iş ile.
$notes = Join-Path $tmp 'notlar'
New-Item -ItemType Directory $notes | Out-Null
function D([int]$days) { [DateTime]::Today.AddDays($days).ToString('dd/MM/yyyy', [Globalization.CultureInfo]::InvariantCulture) }
function DM([int]$days) { [DateTime]::Today.AddDays($days).ToString('dd/MM', [Globalization.CultureInfo]::InvariantCulture) }
$ok = [char]::ConvertFromUtf32(0x1F44D); $open = [char]::ConvertFromUtf32(0x1F6A8); $drop = [char]::ConvertFromUtf32(0x1F6AB); $alarm = [char]0x23F0
function Done([int]$day, [string]$text) { "$ok[Done $(DM $day)]$text" }
$lines = @(
  "----$(D -9)", (Done -9 'Saha ziyareti'), (Done -9 'Teklifi gönder'), (Done -9 'Fatura girişi'), (Done -9 'Arıza kaydını kapat'), (Done -9 'Haftalık rapor'), ''
  "----$(D -5)", "${open}Tedarikçiyle görüş", "`t${open}Fiyat listesini iste", "`t`tGeçen yılki listeyle karşılaştır", (Done -5 'Ofis ağını kontrol et'), ''
  "----$(D -2)", (Done -2 'Müşteri ziyareti raporu'), (Done -2 'Yazıcı toneri'), ''
  "----$(D -1)", "${open}Depo sayımını tamamla", "`t$(Done 0 'Raflar A-C')", "`t${open}Raflar D-F", (Done -1 'Toplantı notlarını paylaş'), ''
  "----$(D 0)", "${open}Müşteri sunumunu hazırla", 'Perşembe gösterilecek', "`t$(Done 0 'Grafikleri güncelle')", "`t${open}Demo akışını dene", "`t`tYedek bilgisayarda da açılıyor mu bak", "`t${open}Slaytları paylaş",
  "${open}Müşteriyle toplantı $alarm[15:30 30dk 15dk]", "${open}Faturaları kontrol et", "${open}Yeni sürüm notunu yaz",
  (Done 0 'Sabah toplantısı'), (Done 0 'Yazıcı arızasını çöz'), "$drop[İptal]Eski projeyi kapat", ''
  "----$(D 1)", "${open}Haftalık raporu gönder", "${open}Ahmet'i ara $alarm[10:00 0dk]", ''
  "----$(D 3)", "${open}Bakım planını çıkar", "`t${open}Yedek parça listesi", ''
  '----tarihsiz', "${open}Ofis için yeni sandalye bak", "${open}Eski kayıtları arşivle", ''
)
$work = $notes
$file = Join-Path $work 'notlar.txt'
$utf8 = New-Object Text.UTF8Encoding $false
[IO.File]::WriteAllText($file, ($lines -join "`r`n"), $utf8)
[IO.File]::WriteAllText((Join-Path $work 'raporlar.txt'), "----$(D 0)`r`n${open}Aylık özet`r`n${open}Satış tablosu`r`n", $utf8)
[IO.File]::WriteAllText((Join-Path $notes 'ev.txt'), "----$(D 0)`r`n${open}Market alışverişi`r`n", $utf8)

Add-Type -AssemblyName PresentationFramework, WindowsBase, PresentationCore
$asm = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'IsGunlugu.exe'))
$bf = [Reflection.BindingFlags]'NonPublic,Public,Static,Instance'
$st = $asm.GetType('IsGunlugu.Settings')
$st.GetField('Directory', $bf).SetValue($null, $tmp)
$st.GetField('FilePath', $bf).SetValue($null, (Join-Path $tmp 'settings.json'))
$asm.GetType('IsGunlugu.DpapiTokenStore').GetField('FilePath', $bf).SetValue($null, (Join-Path $tmp 'google.bin'))
$vmT = $asm.GetType('IsGunlugu.MainViewModel')
$vmT.GetField('BackupDirectory', $bf).SetValue($null, (Join-Path $tmp 'yedekler'))
$vmT.GetField('DefaultNotesFolder', $bf).SetValue($null, $notes)

$app = New-Object System.Windows.Application
$app.ShutdownMode = 'OnExplicitShutdown'
$disp = [System.Windows.Threading.Dispatcher]::CurrentDispatcher
function Settle { for ($i = 0; $i -lt 3; $i++) { $disp.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::ContextIdle) } }
function Theme($name) {
  $app.Resources.MergedDictionaries.Clear()
  foreach ($u in "Themes/$name.xaml", 'Themes/Styles.xaml') {
    $app.Resources.MergedDictionaries.Add([System.Windows.Application]::LoadComponent([Uri]::new("/IsGunlugu;component/$u", [UriKind]::Relative)))
  }
}
Theme 'Light'

$paths = New-Object 'System.Collections.Generic.List[string]'
$paths.Add($file); $paths.Add((Join-Path $work 'raporlar.txt')); $paths.Add((Join-Path $notes 'ev.txt'))
$vm = $vmT.GetConstructor(@([System.Collections.Generic.IList[string]], [string])).Invoke(@([System.Collections.Generic.IList[string]]$paths, [string]$notes))
$vm.TourInvitesOn = $false
$win = [Activator]::CreateInstance($asm.GetType('IsGunlugu.MainWindow'), @($vm))
$win.Width = 1180; $win.Height = 860; $win.Left = 20; $win.Top = 20
$win.Show(); Settle
# Two files in a group, as the guide's picture of the file list shows.
$vm.MoveFileToGroup((Join-Path $notes 'raporlar.txt'), 'İş'); Settle
$vm.MoveFileToGroup($file, 'İş'); Settle
function Cmd($n) { $vm.GetType().GetProperty($n).GetValue($vm).Execute($null); Settle }

$scale = 1.5
function Save([System.Windows.Media.Imaging.BitmapSource]$image, [string]$name) {
  $png = New-Object System.Windows.Media.Imaging.PngBitmapEncoder
  $png.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($image))
  $fs = [IO.File]::Create((Join-Path $Out "$name.png")); $png.Save($fs); $fs.Close()
}
function Picture([System.Windows.FrameworkElement]$element) {
  $element.UpdateLayout()
  $bmp = New-Object System.Windows.Media.Imaging.RenderTargetBitmap ([int]($element.ActualWidth * $scale)), ([int]($element.ActualHeight * $scale)), (96 * $scale), (96 * $scale), ([System.Windows.Media.PixelFormats]::Pbgra32)
  $bmp.Render($element)
  return $bmp
}
# The whole window's content, or the part of it [x, y, w, h] in window units.
function Shot([string]$name, $rect = $null) {
  Settle
  $content = [System.Windows.FrameworkElement]$win.Content
  $image = Picture $content
  if ($rect) {
    $r = New-Object System.Windows.Int32Rect ([int]($rect[0] * $scale)), ([int]($rect[1] * $scale)), ([int]($rect[2] * $scale)), ([int]($rect[3] * $scale))
    $image = New-Object System.Windows.Media.Imaging.CroppedBitmap $image, $r
  }
  Save $image "$name-$script:suffix"
}
function Bounds([string]$part) {
  $element = $win.FindName($part)
  $at = $element.TransformToAncestor($win.Content).Transform((New-Object System.Windows.Point 0, 0))
  return @($at.X, $at.Y, $element.ActualWidth, $element.ActualHeight)
}
# The middle of the window from the top down to $height: header and list.
function Middle([double]$height) {
  $list = Bounds 'ContentScroll'
  return @($list[0], 0, $list[2], [Math]::Min($height, $list[1] + $list[3]))
}
function Row([string]$text) { foreach ($g in $vm.Groups) { foreach ($r in $g.Entries) { if ($r.Text -eq $text) { return $r } } } }

foreach ($theme in 'Light', 'Dark') {
  Theme $theme
  $script:suffix = if ($theme -eq 'Light') { 'l' } else { 'd' }
  Settle

  Cmd 'ShowTodayCommand'
  $vm.CloseDetail(); Settle
  Shot 'today'
  Shot 'bugun' (Middle 560)
  Shot 'satir' (Middle 330)

  # The panel in a lower window, so its picture is not mostly empty space.
  $win.Height = 660; Settle
  $vm.OpenDetail((Row 'Müşteri sunumunu hazırla').Entry, $null); Settle
  Shot 'panel' (Bounds 'DetailPanel')
  $vm.OpenDetail((Row 'Müşteriyle toplantı').Entry, $null); Settle
  $vm.OpenDetailTimeCommand.Execute($null); Settle
  Shot 'hatirlatici' (Bounds 'DetailPanel')
  $vm.CloseDetail(); $win.Height = 860; Settle

  Cmd 'ShowOpenCommand'; Shot 'tamamlanmamis' (Middle 420)
  Cmd 'ShowFutureCommand'; Shot 'gelecek' (Middle 380)
  Cmd 'ShowUndatedCommand'; Shot 'tarihsiz' (Middle 300)
  Cmd 'ShowAllCommand'
  foreach ($g in @($vm.Groups)) { if ($g.IsCollapsed) { $g.OpenCommand.Execute($null); Settle } }
  Shot 'all'

  Cmd 'ShowTodayCommand'
  $vm.ToggleSelection((Row 'Faturaları kontrol et')); $vm.ToggleSelection((Row 'Yeni sürüm notunu yaz')); Settle
  Shot 'selection'
  $vm.ClearSelection(); Settle

  Cmd 'ShowCalendarCommand'
  Cmd 'ShowWeekScaleCommand'; Shot 'hafta'
  Cmd 'ShowMonthScaleCommand'; Shot 'calendar'
  Cmd 'ShowYearScaleCommand'; Shot 'yil'
  Cmd 'ShowMonthScaleCommand'
  Cmd 'ShowTodayCommand'

  $vm.IsFilePickerOpen = $true; Settle
  $pane = Bounds 'SectionPane'
  Shot 'dosyalar' @($pane[0], $pane[1], $pane[2], 300)
  $vm.IsFilePickerOpen = $false; Settle

  $vm.IsSettingsOpen = $true; Settle
  $win.FindName('SettingsTabBehave').IsChecked = $true; Settle
  $scrim = $win.FindName('SettingsScrim'); $card = [System.Windows.Media.VisualTreeHelper]::GetChild($scrim, 0)
  $at = $card.TransformToAncestor($win.Content).Transform((New-Object System.Windows.Point 0, 0))
  Shot 'ayarlar' @($at.X, $at.Y, $card.ActualWidth, $card.ActualHeight)
  $win.FindName('SettingsTabLook').IsChecked = $true
  $vm.IsSettingsOpen = $false; Settle

  # The tray panel and the quick add box are windows of their own.
  $panel = [Activator]::CreateInstance($asm.GetType('IsGunlugu.TrayPanel'), @($vm, [Action]{}))
  $panel.ShowActivated = $false; $panel.Left = -4000; $panel.Top = 0
  $panel.Show(); Settle
  Save (Picture $panel.Content) "tray-$suffix"
  $panel.Close()

  $quick = [Activator]::CreateInstance($asm.GetType('IsGunlugu.QuickAdd'), @($vm))
  $quick.ShowActivated = $false; $quick.Left = -4000; $quick.Top = 0
  $quick.Show(); Settle
  Save (Picture $quick.Content) "hizliekle-$suffix"
  $quick.Close()

  # The menu behind a row's dots, as the app builds it for an open task of another day.
  $menu = New-Object System.Windows.Controls.ContextMenu
  foreach ($label in 'Yukarı taşı', 'Aşağı taşı', '-', 'Seç', '-', 'Başka güne taşı', 'Bugüne al', 'Yarına taşı', 'Tarihsiz yap') {
    if ($label -eq '-') { [void]$menu.Items.Add((New-Object System.Windows.Controls.Separator)); continue }
    [void]$menu.Items.Add((New-Object System.Windows.Controls.MenuItem -Property @{ Header = $label }))
  }
  $menu.Measure((New-Object System.Windows.Size ([double]::PositiveInfinity), ([double]::PositiveInfinity)))
  $menu.Arrange((New-Object System.Windows.Rect $menu.DesiredSize))
  $menu.UpdateLayout()
  Save (Picture $menu) "menu-$suffix"
}

$win.Close()
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
"Görüntüler: $Out (" + (Get-ChildItem $Out -Filter *.png).Count + " dosya)"
