# Kasma testi: masaüstü programını gerçek penceresiyle açar, sık yapılan işlemleri ölçer,
# sınırı aşan varsa KALDI der ve 1 ile çıkar. Her sürümden önce çalıştır:
#   powershell -STA -NoProfile -ExecutionPolicy Bypass -File tests\Hiz\kasma-testi.ps1
#   ... -Not "C:\yol\not.txt"   (kendi dosyanla; verilmezse 1 yıllık örnek dosya üretilir)
# Drive'a ve gerçek ayarlara dokunmaz: her şey geçici klasörde olur.
param([string]$Not, [int]$Sinir = 100, [int]$Tekrar = 5)
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$tmp = Join-Path ([IO.Path]::GetTempPath()) ('kasma-' + [Guid]::NewGuid().ToString('N').Substring(0, 8))
New-Item -ItemType Directory $tmp | Out-Null
$bin = Join-Path $tmp 'bin'
dotnet build (Join-Path $root 'src\IsGunlugu\IsGunlugu.csproj') -c Release -o $bin -v q -nologo | Out-Null
if ($LASTEXITCODE) { throw 'Derleme başarısız.' }

$file = Join-Path $tmp 'not.txt'
if ($Not) { Copy-Item -LiteralPath $Not $file }
else {
  # Bir yıllık örnek: iş günü başına 3-7 iş, işlerde 0-4 alt iş, son günlerde açık işler.
  $rnd = New-Object Random 1; $sb = New-Object Text.StringBuilder; $today = [DateTime]::Today
  for ($back = 365; $back -ge 0; $back--) {
    $d = $today.AddDays(-$back); if ($d.DayOfWeek -in 'Saturday', 'Sunday') { continue }
    [void]$sb.Append("----" + $d.ToString('dd/MM/yyyy') + "`r`n")
    for ($i = 0; $i -lt $rnd.Next(3, 8); $i++) {
      $done = $back -gt 0 -and $rnd.NextDouble() -lt 0.9
      $mark = if ($done) { [char]::ConvertFromUtf32(0x1F44D) + '[Done]' } else { [char]::ConvertFromUtf32(0x1F6A8) }
      [void]$sb.Append("${mark}iş $i, " + $d.ToString('d MMMM') + " için biraz uzun bir başlık`r`n")
      for ($j = 0; $j -lt $rnd.Next(0, 5); $j++) { [void]$sb.Append("`t${mark}alt iş $j`r`n") }
    }
    [void]$sb.Append("`r`n")
  }
  [IO.File]::WriteAllText($file, $sb.ToString(), (New-Object Text.UTF8Encoding $false))
}

Add-Type -AssemblyName PresentationFramework, WindowsBase, PresentationCore
$asm = [Reflection.Assembly]::LoadFrom((Join-Path $bin 'IsGunlugu.exe'))
$bf = [Reflection.BindingFlags]'NonPublic,Public,Static,Instance'
$st = $asm.GetType('IsGunlugu.Settings')
$st.GetField('Directory', $bf).SetValue($null, $tmp)
$st.GetField('FilePath', $bf).SetValue($null, (Join-Path $tmp 'settings.json'))
$asm.GetType('IsGunlugu.DpapiTokenStore').GetField('FilePath', $bf).SetValue($null, (Join-Path $tmp 'google.bin'))
$vmT = $asm.GetType('IsGunlugu.MainViewModel')
$vmT.GetField('BackupDirectory', $bf).SetValue($null, (Join-Path $tmp 'yedekler'))
$vmT.GetField('DefaultNotesFolder', $bf).SetValue($null, (Join-Path $tmp 'notes'))

$app = New-Object System.Windows.Application
$app.ShutdownMode = 'OnExplicitShutdown'
foreach ($u in 'Themes/Dark.xaml', 'Themes/Styles.xaml') {
  $app.Resources.MergedDictionaries.Add([System.Windows.Application]::LoadComponent([Uri]::new("/IsGunlugu;component/$u", [UriKind]::Relative)))
}
$disp = [System.Windows.Threading.Dispatcher]::CurrentDispatcher
function Settle { $disp.Invoke([Action]{}, [System.Windows.Threading.DispatcherPriority]::ContextIdle) }

# Komut satırından verilen dosya gibi açılır: eşitleme kapalı.
$paths = New-Object 'System.Collections.Generic.List[string]'; $paths.Add($file)
$vm = $vmT.GetConstructors()[0].Invoke(@([System.Collections.Generic.IList[string]]$paths, $null))
$win = [Activator]::CreateInstance($asm.GetType('IsGunlugu.MainWindow'), @($vm))
$win.Width = 1400; $win.Height = 900
$win.Show(); Settle

function Cmd($n) { $vm.GetType().GetProperty($n).GetValue($vm).Execute($null) }
function OpenRow { foreach ($g in $vm.Groups) { foreach ($r in $g.Entries) { if ($r.IsOpen -and -not $r.Entry.IsPartlyDone) { return $r } } } }
$rebuild = $vmT.GetMethod('Rebuild', $bf)
$script:kaldi = 0

# Süre = işlem + WPF'in yerleşimi ve çizimi bitene kadar geçen süre; en kötü tekrar sayılır.
function Olc([string]$ad, [scriptblock]$op, [int]$sinir) {
  $t = @(for ($i = 0; $i -lt $Tekrar; $i++) {
    $s = [Diagnostics.Stopwatch]::StartNew(); & $op; Settle; $s.Elapsed.TotalMilliseconds
  }) | Sort-Object
  # Ortanca karar verir; ilk çağrıdaki ısınma en kötüde görünür.
  $median = $t[[int][Math]::Floor($t.Count / 2)]
  $ok = $median -le $sinir
  if (-not $ok) { $script:kaldi++ }
  "{0,-32} ortanca {1,5:N0} ms  en kötü {2,5:N0} ms  (sınır {3})  {4}" -f $ad, $median, $t[-1], $sinir, $(if ($ok) { 'tamam' } else { 'KALDI' })
}

# Tıklar: her biri sınırın altında kalmalı.
Olc 'Bugün: ekranı tazele' { $rebuild.Invoke($vm, $null) } $Sinir
Olc 'Bugün: iş tikle' { $r = OpenRow; if ($r) { $r.CheckState = $true } } $Sinir
Olc 'Bugün: geri al' { Cmd 'UndoCommand' } $Sinir
$first = $null; foreach ($g in $vm.Groups) { if ($g.Entries.Count) { $first = $g.Entries[0]; break } }
if ($first) { Olc 'Düzenle aç ve kapat' { $first.EditCommand.Execute($null); Settle; $first.CancelCommand.Execute($null) } $Sinir }
Olc 'Takvim: ay ileri' { Cmd 'NextMonthCommand' } $Sinir
Cmd 'ShowTodayCommand'; Settle

# Ekran geçişleri: her sayfa satırlarını saklar, dönüş yalnız değişeni yamar; yine de iki sayfa çizildiği için iki kat sınır.
Olc 'Geçiş: Bugün -> Takvim -> Bugün' { Cmd 'ShowCalendarCommand'; Settle; Cmd 'ShowTodayCommand' } (2 * $Sinir)
Olc 'Geçiş: Bugün -> Tümü -> Bugün' { Cmd 'ShowAllCommand'; Settle; Cmd 'ShowTodayCommand' } (2 * $Sinir)

$win.Close()
Remove-Item $tmp -Recurse -Force -ErrorAction SilentlyContinue
if ($script:kaldi) { "$($script:kaldi) ölçüm sınırı aştı."; exit 1 }
'Hepsi sınırın altında.'
