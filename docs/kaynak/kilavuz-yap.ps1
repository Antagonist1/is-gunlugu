# Fills the guide template with the pictures and the small window maps, and writes docs\kilavuz.html.
# The pictures are the ones the app carries: run docs\kaynak\kilavuz-goruntu.ps1 first when the window changed.
#   powershell -NoProfile -ExecutionPolicy Bypass -File docs\kaynak\kilavuz-yap.ps1
param()
$ErrorActionPreference = 'Stop'
$docs = Resolve-Path (Join-Path $PSScriptRoot '..')
$pictures = Join-Path $PSScriptRoot 'goruntu'
$inv = [Globalization.CultureInfo]::InvariantCulture
function F($v) { ([double]$v).ToString('0.##', $inv) }

$template = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'kilavuz-sablon.html'), [Text.Encoding]::UTF8)
$version = ((Get-Content (Join-Path $docs '..\tools\surum.txt') -TotalCount 1) -split ' ')[0] -replace '^(\d+\.\d+\.\d+).*', '$1'
$hotkey = 'Win+Shift+N'

$alts = @{
  today = 'Uygulamanın penceresi, Bugün açık'; bugun = 'Bugün ekranı'; satir = 'İş satırları'; menu = 'Noktalara tıklayınca açılan menü'
  panel = 'Sağ panel'; hatirlatici = 'Hatırlatıcı sayfası'; hizliekle = 'Her yerden hızlı ekleme kutusu'; tamamlanmamis = 'Tamamlanmamış ekranı'
  gelecek = 'Gelecek ekranı'; tarihsiz = 'Tarihsiz ekranı'; all = 'Tümü ekranı'; selection = 'İki iş seçili'; hafta = 'Takvim, hafta'
  calendar = 'Takvim, ay'; yil = 'Takvim, yıl'; dosyalar = 'Dosya listesi'; tray = 'Tepsi paneli'; ayarlar = 'Ayarlar penceresi'
}

function DataUri([string]$file) { 'data:image/png;base64,' + [Convert]::ToBase64String([IO.File]::ReadAllBytes($file)) }

# A picture in both themes; the page shows the one that matches the reader's.
function Picture([string]$name) {
  $light = Join-Path $pictures "$name-l.png"; $dark = Join-Path $pictures "$name-d.png"
  if (-not (Test-Path $light) -or -not (Test-Path $dark)) { throw "görüntü yok: $name (önce kilavuz-goruntu.ps1)" }
  "<img class=""l"" alt=""$($alts[$name])"" src=""$(DataUri $light)""><img class=""d"" alt=""$($alts[$name])"" src=""$(DataUri $dark)"">"
}

# The same drawing of the window as in the app's guide (GuideParts.WindowParts): the strip, the list beside it,
# the tasks and the add box; the parts named in $on are lit.
$parts = [ordered]@{
  FilesPanel = 6.19, 1.71, 18.9, 4.14; SearchBox = 6.19, 6.82, 18.9, 4.14
  TodayNav = 5.84, 17.17, 19.59, 4.02; OpenNav = 5.84, 21.44, 19.59, 4.02; FutureNav = 5.84, 25.7, 19.59, 4.02
  UndatedNav = 5.84, 29.96, 19.59, 4.02; AllNav = 5.84, 34.23, 19.59, 4.02; MiniCalendar = 6.19, 47.26, 18.9, 26.31
  TaskGroupButton = 0.7, 6.33, 3.8, 5.85; CalendarNav = 0.7, 12.67, 3.8, 5.85; SettingsButton = 0.7, 92.69, 3.8, 5.85
  ContentScroll = 28, 15.23, 70.5, 76; DraftBox = 29.2, 92.6, 67.7, 5; ShortcutsButton = 88.83, 94.09, 7.56, 2.68
}
function MiniMap([string[]]$on, [switch]$Live) {
  $h = 860 / 1180
  $sb = New-Object Text.StringBuilder
  [void]$sb.Append("<svg class=""mini"" viewBox=""0 0 100 $(F (100*$h))"" aria-hidden=""true""><rect class=""bg"" x=""0"" y=""0"" width=""100"" height=""$(F (100*$h))"" rx=""1.5""/>")
  [void]$sb.Append("<rect class=""sb"" x=""0.3"" y=""0.3"" width=""25.8"" height=""$(F (100*$h - 0.6))"" rx=""1.2""/>")
  foreach ($p in $parts.Keys) {
    $r = $parts[$p]
    $cls = if ($on -contains $p) { ' class="on"' } else { '' }
    $data = if ($Live) { " data-p=""$p""" } else { '' }
    [void]$sb.Append("<rect$cls$data x=""$(F $r[0])"" y=""$(F ($r[1]*$h + 0.25))"" width=""$(F $r[2])"" height=""$(F ([Math]::Max(0.8, $r[3]*$h - 0.5)))"" rx=""0.7""/>")
  }
  [void]$sb.Append('</svg>')
  $sb.ToString()
}

$tick = '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3 8.5 6.5 12 13 4.5" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round"/></svg>'
$dash = '<svg viewBox="0 0 16 16" aria-hidden="true"><path d="M3.5 8h9" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round"/></svg>'
$html = $template.Replace('{{tick}}', $tick).Replace('{{dash}}', $dash).Replace('{{livemap}}', (MiniMap @() -Live)).Replace('{{hotkey}}', $hotkey).Replace('{{version}}', $version)

# One screen: its place on the map, its name and words, and its picture.
$html = [regex]::Replace($html, '\{\{screen:(\w+):([^:}]+):(\w+):(.*?)\}\}', {
  param($m)
  $part = $m.Groups[1].Value
  "<div class=""screen"" data-parts=""$part""><div class=""map"">$(MiniMap @($part))</div><div class=""head""><h3>$($m.Groups[2].Value)</h3><p>$($m.Groups[4].Value)</p></div><div class=""shot"">$(Picture $m.Groups[3].Value)</div></div>"
})
$html = [regex]::Replace($html, '\{\{map:([\w,]+)\}\}', { param($m) MiniMap ($m.Groups[1].Value -split ',') })
$html = [regex]::Replace($html, '\{\{img:(\w+)\}\}', { param($m) Picture $m.Groups[1].Value })

if ($html -match '\{\{') { throw "doldurulmamış yer: $([regex]::Match($html, '\{\{[^}]*\}\}').Value)" }
[IO.File]::WriteAllText((Join-Path $docs 'kilavuz.html'), $html, (New-Object Text.UTF8Encoding($false)))
"kilavuz.html: " + [int]((Get-Item (Join-Path $docs 'kilavuz.html')).Length / 1024) + " KB"
