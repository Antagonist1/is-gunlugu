# Yayın: sürüm numarasını artırır, sürüm notlarını işler, exe'yi publish klasörüne koyar.
#   powershell -NoProfile -ExecutionPolicy Bypass -File tools\yayinla.ps1         aynı gün: derleme +1, yeni çalışma günü: gün +1
#   ... -Yayin   GitHub'a gidecek sürüm (0.9.0.x -> 0.9.1.0), yalnızca kullanıcı "release" deyince
#   ... -Buyuk   büyük güncelleme (0.9.x -> 0.10.0.0), yalnızca kullanıcı isteyince
#   ... -Dene    hiçbir şeyi değiştirmeden çıkacak numarayı yazar
# Numara dört parça, Windows'taki dosya sürümüyle aynı: dönem.büyük.yayın.GGDD. Son parçanın son iki hanesi
# gün içi derleme, öncesi yayından sonraki çalışma günü: 0.9.1.302 = v0.9.1'den sonra 3. gün, 2. derleme.
param([switch]$Yayin, [switch]$Buyuk, [switch]$Dene)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$utf8 = New-Object Text.UTF8Encoding $false

# 1. Numara: tools\surum.txt son derlemeyi tutar ("0.9.1.302 2026-11-26").
$stateFile = Join-Path $PSScriptRoot 'surum.txt'
$state = [IO.File]::ReadAllText($stateFile).Trim() -split '\s+'
$v = @($state[0].Split('.') | ForEach-Object { [int]$_ })
$day = [math]::Floor($v[3] / 100); $build = $v[3] % 100
$today = (Get-Date).ToString('yyyy-MM-dd')
if ($Buyuk) { $v[1]++; $v[2] = 0; $day = 0; $build = 0 }
elseif ($Yayin) { $v[2]++; $day = 0; $build = 0 }
elseif ($state[1] -ne $today) { $day++; $build = 1 }   # ara verilen günler sayılmaz, yalnızca iş yapılan gün
else { $build++ }
if ($build -gt 99) { throw 'Bir günde 99 derlemeden fazlası numaraya sığmaz.' }
$v[3] = $day * 100 + $build
$release = "$($v[0]).$($v[1]).$($v[2])"
$full = "$release.$($v[3])"
# Sürüm notlarında her çalışma gününün kendi başlığı var; yayın gününün başlığı yayının kendisi.
$short = if ($day -eq 0) { $release } else { "$release.$day" }
"Yeni sürüm: $full (programda $release, sürüm notlarında $short)"
if ($Dene) { return }

# 2. Derle ve test et; biri başarısızsa hiçbir şey değişmez.
dotnet build (Join-Path $root 'src\IsGunlugu\IsGunlugu.csproj') -c Release -nologo -v q "-p:Version=$full" | Out-Host
if ($LASTEXITCODE) { throw 'Derleme başarısız, hiçbir şey değişmedi.' }
dotnet test (Join-Path $root 'tests\IsGunlugu.Tests\IsGunlugu.Tests.csproj') -nologo -v q | Select-String 'Başarılı!|Başarısız!|Passed!|Failed!' | Out-Host
if ($LASTEXITCODE) { throw 'Testler başarısız, hiçbir şey değişmedi.' }

# 3. Sürüm notları: [Unreleased] maddeleri günün sürümüne taşınır, numarasız maddelere tam numara eklenir.
$clPath = Join-Path $root 'CHANGELOG.md'
$text = [IO.File]::ReadAllText($clPath, $utf8)
$nl = if ($text.Contains("`r`n")) { "`r`n" } else { "`n" }
$lines = New-Object 'System.Collections.Generic.List[string]'
$lines.AddRange([string[]]($text -split "`r?`n"))
function NextHeader([int]$from) { for ($i = $from; $i -lt $lines.Count; $i++) { if ($lines[$i].StartsWith('## ')) { return $i } }; return $lines.Count }

$u = $lines.IndexOf('## [Unreleased]')
if ($u -lt 0) { throw 'CHANGELOG.md içinde ## [Unreleased] bulunamadı.' }
$n = NextHeader ($u + 1)
$sections = [ordered]@{}; $current = $null
for ($i = $u + 1; $i -lt $n; $i++) {
  $line = $lines[$i]
  if ($line.StartsWith('### ')) { $current = $line.Substring(4).Trim(); if (-not $sections.Contains($current)) { $sections[$current] = New-Object 'System.Collections.Generic.List[string]' } }
  elseif ($line.StartsWith('- ') -and $current) { $sections[$current].Add($line) }
}
$lines.RemoveRange($u + 1, $n - $u - 1)
$lines.Insert($u + 1, '')
$n = $u + 2

if ($n -lt $lines.Count -and $lines[$n] -match ('^## \[' + [regex]::Escape($short) + '\]')) {
  foreach ($name in $sections.Keys) {
    $end = NextHeader ($n + 1)
    $k = -1
    for ($i = $n + 1; $i -lt $end; $i++) { if ($lines[$i] -eq "### $name") { $k = $i; break } }
    if ($k -ge 0) {
      $stop = $k + 1; while ($stop -lt $end -and -not $lines[$stop].StartsWith('### ')) { $stop++ }
      $at = $stop; while ($at -gt $k + 1 -and $lines[$at - 1].Trim() -eq '') { $at-- }
      $lines.InsertRange($at, [string[]]$sections[$name])
    }
    else {
      $at = $end; while ($at -gt $n + 1 -and $lines[$at - 1].Trim() -eq '') { $at-- }
      $lines.InsertRange($at, [string[]](@('', "### $name", '') + $sections[$name]))
    }
  }
}
else {
  $block = @("## [$short] - $today", '')
  foreach ($name in $sections.Keys) { $block += "### $name"; $block += ''; $block += $sections[$name]; $block += '' }
  $lines.InsertRange($n, [string[]]$block)
}

$end = NextHeader ($n + 1)
for ($i = $n + 1; $i -lt $end; $i++) {
  if ($lines[$i].StartsWith('- ') -and $lines[$i] -notmatch '\[\d+\.\d+\.\d+\.\d+\]\s*$') { $lines[$i] = $lines[$i].TrimEnd() + " [$full]" }
}

# 4. Exe publish klasörüne: açık program kendi exe'sini kilitler ama yeniden adlandırmaya izin verir.
$publish = Join-Path $root 'publish'
New-Item -ItemType Directory -Force $publish | Out-Null
$target = Join-Path $publish 'IsGunlugu.exe'
if (Test-Path $target) {
  foreach ($old in Get-ChildItem $publish -Filter 'IsGunlugu.onceki*.exe') { try { Remove-Item $old.FullName -ErrorAction Stop } catch { } }
  $k = 1; do { $name = if ($k -eq 1) { 'IsGunlugu.onceki.exe' } else { "IsGunlugu.onceki$k.exe" }; $k++ } while (Test-Path (Join-Path $publish $name))
  Rename-Item $target $name
}
Copy-Item (Join-Path $root 'src\IsGunlugu\bin\Release\net48\IsGunlugu.exe') $target

[IO.File]::WriteAllText($clPath, ($lines -join $nl), $utf8)
[IO.File]::WriteAllText($stateFile, "$full $today`r`n", $utf8)
"Yayınlandı: $target ($full). Program açıksa kapatıp açınca yeni sürüm gelir."
