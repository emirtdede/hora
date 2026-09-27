# Hora

Windows 11 icin suruklenebilir, boyutlandirilabilir kullanim takibi widget'i. Hangi uygulamada ne kadar zaman gecirdigini gosterir. Veriler yalnizca bu bilgisayarda saklanir, aga gonderilmez.

## Kurulum (son kullanici)

`setup.bat` cift tikla. Yonetici yetkisi gerekmez. Kurulum sunlari yapar:

- `Hora.exe` dosyasini `%LOCALAPPDATA%/Hora` altina kopyalar
- Baslat menusu kisayolu olusturur
- Program Ekle/Kaldir kaydini yazar
- Uygulamayi baslatir

Kaldirma: Baslat menusu Hora kisayolu veya `uninstall.ps1`. Kaldirma verilerin de silinip silinmeyecegini sorar.

## Gunluk kullanim

1. Widget ekranda acilir, simge sistem tepsisine yerlesir.
2. Baslik cubugundan surukle, sag ve alt kenar ile kosedeki tutamactan boyutlandir.
3. Mini / Liste / Grafik dugmeleriyle gorunumu degistir.
4. Ilk acilista Ayarlar paneli acilir: otomatik baslatma aciktir, istemiyorsan kutuyu kaldir.

## Temalar

Segmentteki tema dugmesi Siyah / Beyaz / Cam sirasiyla doner:

- Siyah: derin OLED siyahi zemin
- Beyaz: acik zemin, koyu yazi
- Cam: notr fuume cam gorunumu (yari seffaf kart + isik yansimasi, saf WPF; DWM blur
  koselerde kare artefakt yaptigi icin bilerek kullanilmiyor).

## Ozellikler

- 1 saniyede bir on plan uygulamasi takibi, SQLite yerel kayit
- 60 sn hareketsizlikte Bosta sayimi
- Film/Oyun modu: tam ekran + ses aktifse sayac durmaz, hep acik
- Toplanti modu: Teams/Zoom + ses aktifse Bosta sayilmaz, hep acik
- Kilit/uyku: Win+L ve uykuda sayac durur
- Tarayici detayi: domain bazinda ayrim (youtube.com 1 sa gibi)
- Gizlilik: sifre yoneticisi ve banka iceriklerinde baslik [gizli] saklanir
- Otomatik baslatma varsayilan acik, Ayarlar kutusundan kapatilabilir
- Saydamlik kaydiricisi, konum ve boyut hafizasi

## Bilinen sinirlar

- Sessiz filmde ses cikisi yoksa Bosta yazilabilir.
- Windows hangi uygulamanin mikrofonu kullandigini gostermez, toplanti algilama sezgiseldir.
- Kamera durumu algilanmaz.
- Yonetici olarak calisan pencerelerin basligi okunamaz, exe adiyla kaydedilir.

## Gelistirme

- `dotnet build Hora.slnx`
- `dotnet test Hora.slnx`
- Publish: `dotnet publish src/Hora.UI/Hora.UI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish/Hora`

## Dokumanlar

`docs/` altinda PRD, SRS, AD, UI-UX, TEST, PERF, SEC.
