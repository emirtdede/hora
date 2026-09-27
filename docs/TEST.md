# TEST - Hora

## Birim + karar testleri (23, hepsi yesil, 0 uyari)
- DomainExtractor: youtube/github/meet URL, bos girdi, Chrome sonek temizleme.
- PrivacyFilter: keepass/banka hassas, normal degil, maskeleme, uzunluk siniri Core icinde.
- UsageAggregator: ayni anahtar birlesir, gecersiz girdi yoksayilir, Drain temizler.
- SettingsStore: varsayilanlar, clamp (genislik 800, opaklik 1.0, tema/view duzeltme).
- DurationFormatter: 0 sn / 45 sn / 5 dk / 1 sa 5 dk.
- StorageTests: UPSERT toplama (60+60=120), gunluk ve 7 gunluk aralik toplami.
- TrackingEngineTests (stub servisler): Kilitli->locked, 120 sn idle->Bosta, aktif->Canli+DB satiri,
  tam ekran+ses 5 dk idle->Film, Teams+ses 10 dk idle->Toplanti.

## Dogrulanmis canli kosular (gelistirme makinesi, 27.09.2026)
- 8 sn kosu: cokus yok, usage.db + WAL + settings.json olustu, hata logu yok.
- 16 sn kosu: 10. sn flush DB'ye yazdi, log temiz.
- Ekran goruntusu 1: Ayar paneli icerikle binisikti, duzeltildi (ContentPanel gizleme).
- Ekran goruntusu 2: Ayarlar temiz, durum Bosta (gercek idle), otomatik baslatma isaretli.
- Ekran goruntusu 3: Liste gercek veri gosterdi (Hora 10 sn + Bosta 10 sn, renkli barlar).
- Hata raporu (kullanici): Tema tiklayinca "Beklenmeyen bir hata" penceresi. Kok neden: dondurulmus
  (frozen) XAML fircasini mutate eden SetBrush. Cozum: kaynagi yeni firca ile degistirme.
  Otomasyonla Tema tiklatilarak dogrulandi: hata yok, acik tema temiz render oluyor.
- Ek hata: "Bugun: 0 dk" sabit kaliyordu (TotalText VM'e yaziliyor, TextBlock'a bagli degildi).
  Cozum: RefreshAsync TextBlock'u dogrudan guncelliyor. Dogrulama: "Bugun: 5 dk" goruntulendi.
- 3 tema dogrulamasi (ekran goruntulu): Siyah varsayilan, Beyaz temiz render, Cam hatasiz
  acrylic + yari seffaf kart.
- Premium revizyon dogrulamasi: satirlarda gercek ikon (Chrome + chatgpt.com domain ayrimi
  canli goruldu), vektor baslik ikonlari, 3'lu segment, Ayarlar'da tema swatch'lari.
  Butonlara AutomationProperties.Name eklendi (erisim + otomasyon).
- Tasarim denetimi duzeltmeleri: ayar metni kesiliyordu, ScrollViewer + gizli kaydirma +
  sikistirilmis yerlesimle varsayilan boyutta tam sigiyor (ekran goruntulu). Siyah swatch
  gorunur koyu lacivert oldu. Ince scrollbar denemesi kaldirildi (uygulanmadi, olu kod birakilmadi).
- Notr cam revizyonu (ekran goruntulu): hale yok (Cam'da kart margin 0), mavi ton yok
  (acrylic tonu ve kart/vurgu notr gri; secili hap gumus + koyu yazi), Mini ve Liste'de
  ikon kutucuklari dogrulandi. Logda yeni hata yok.
- Grafik revizyonu (ekran goruntulu): "Son 7 gun" tekrari kaldirildi, 5 satirlik renk
  lejanti eklendi, %2 alti dilimler Diger'de toplandi (%1 alti cizilmiyor bile),
  varsayilan boy 500 oldu + eski dokunulmamis 420 degerleri tek seferlik gocuyor.
  Test sayisi 27.
- Swatch esitlik denetimi: UIA bounding rect olcumuyle uc butonun piksel esit oldugu
  kanitlandi (95/96/95 x 48). Algilanan fark irradiasyon illuzyonu + cercevesiz onizleme
  kaynakliydi. Cozum: uc onizleme de ayni iz (track) kutucuga alindi, secili halka kutucukta.
  Buyutulmus ekran goruntusuyle uniforme gorunum dogrulandi.
- Korse/yazi/veri/boyut duzeltmeleri (ekran goruntulu): donut merkezine Foreground baglandi
  (varsayilan siyah okunmuyordu); DWM blur kaldirildi, tum temalarda margin 10 sabit
  (kare hale + kirpik kose + boyut algisi cozuldu); Mini artik Diger dahil ilk 3 satiri
  gosteriyor ve gorunum degisiminde liste yenileniyor (tum gorunumler ayni anlik goruntu).
- Swatch/tik duzeltmeleri (ekran goruntulu): Siyah swatch gorunur notr koyu dolguya alindi
  (once cerceve gibi okunuyordu); checkbox tik isareti sabit boyut + Uniform esnetme ile
  optik merkeze oturtuldu.

## Manuel E2E (son kullanici, yayin oncesi her surumde)
1. Temiz kurulum: exe calisir, 60 sn kullanim sonrasi Liste dolar.
2. Surukle/birak konum korunur (kapat-ac).
3. Sag/alt/kose boyutlandirma korunur.
4. Film: YouTube tam ekran 3 dk dokunmadan -> Film, sure tarayiciya isler.
5. Sessiz film: sesi kis, tam ekran 70 sn -> Bosta yazilabilir (bilinen sinir, kabul).
6. Kilit: Win+L 2 dk -> locked satiri artar, uygulamalar artmaz.
7. Toplanti: Teams on planda sesli 2 dk -> Toplanti.
8. Yeniden baslat: --minimized ile tepside baslar.
9. Ikinci exe ornegi kapanir (Mutex).
10. Ag izleme: 5 dk calisma, dis baglanti yok (guvenlik duvarinda dogrulanir).
11. 7 gun cevrimdisi: DB buyur, acilis < 1.5 sn.
12. UAC penceresi: Regedit yonetici acikken kayit exe adiyla surer, cokus yok.
