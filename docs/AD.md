# AD - Hora Mimari Kararlari

## Karar 1: WPF, WinUI degil
WinUI 3 seffaf cercevesiz widget ve sistem tepsisi icin kisitli. WPF WindowStyle=None + AllowsTransparency ile surukle/boyutlandir dogal. Hedef net8.0-windows10.0.17763.

## Karar 2: Harici grafik kutuphanesi yok
LiveCharts2 + SkiaSharp net8-windows hedefinde restore sorunu (LiveCharts2#1772) ve ~30 MB native bagimlilik getirir. Donut saf WPF Path geometrisiyle cizildi, bar listesi ItemsControl. Tek dosya publish guvenilir.

## Karar 3: Tepsi icin WinForms NotifyIcon
H.NotifyIcon ek bagimlilik ve surum riski tasir. System.Windows.Forms.NotifyIcon framework icinde, UseWindowsForms zaten acik.

## Karar 4: Serilog yok, dosya logger
Tek dosya log, kilitli yazma, asla firlatmaz. Dis bagimlilik sifir.

## Karar 5: NAudio korundu
Core Audio P/Invoke elle yazmak 300+ satir ve COM hatasina acik. NAudio.Wasapi 2.2.1 kararli, MIT, session state + peak tek pakette. Enum konumu Interfaces altinda (2.x degisikligi) dogrulandi.

## Karar 6: UIAutomation isme gore degil tipe gore
Adres cubugu ismi dile gore degisir. ControlType.Edit + ValuePattern ilk eslesme kullanilir, 300 ms timeout.

## Karar 7: Hora.UI.System yerine Hora.UI.Platform
System ad alani XAML uretilen kodda cozumleme cakismasi yapar. Yeniden adlandirma zorunluydu.

## Katmanlar
Hora.Core (takip, tarayici, gizlilik, birlestirme) / Hora.Storage (SQLite, ayar) / Hora.UI (orkestrasyon, gorunum). Core Storage'i bilmez, UI ikisini orkestre eder. God class yok: 6 takip servisi ayri interface.
