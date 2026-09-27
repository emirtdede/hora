# UI-UX - Hora

## Tema
Siyah varsayilan: kart #0B0E1F, cerceve #262B45, yazi beyaz, ikincil #9AA3C7, iz #232842, vurgu #7C8CF8.
Beyaz: kart beyaz, cerceve #E3E8F7, yazi #11142A, ikincil #5A6285, iz #EDF0FA, vurgu #4F5BD5.
Cam (notr fuume cam): yari seffaf notr kart (#99212121) + isik yansimasi + %20 beyaz cerceve.
DWM blur bilerek yok: katmanli pencerede tum dikdortgene uygulanip kare hale ve kirpik
koseler uretiyordu. Tum temalarda kart margin 10 sabit, boyut algisi degismez.
DynamicResource ile anlik gecis, secim ayarda saklanir. Eski Dark/Light kayitlari Black/White'a gocer.

## Tipografi
Segoe UI Variable, baslik 15 SemiBold, liste 12-13, durum 11, alt bilgi 10. Kontrast 4.5:1 hedef.

## Yerlesim 8pt grid
Kart radius 18, ic bosluk 14, baslik cubugu 44, alt bilgi 28. Min 260x300, maks 800x900.

## Bilesenler
- Gorunum secici: 3 bolmeli segment (Mini/Liste/Grafik), secili hap vurgulu. Tema segmentte
  degil, Ayarlar'daki Siyah/Beyaz/Cam swatch kartlarinda secilir, secili halka ile gosterilir.
- Baslik: logo noktasi + Hora + vektor ikon butonlar (eksi=tepsi, disli=ayar). Durum hapi yok.
- Satirlar: gercek uygulama ikonu (exe'den, onbellekli; yoksa renk noktasi), elipsli baslik,
  sagda sure; listede ince cubuk. Mini'de sira rozeti.
- Ayarlar: ozel slider ve checkbox, bilgi karti.

## Durumlar
- Bos gun: gercek aciklama metni ("Bugun icin kayit yok..."), sahte grafik yok.
- Baslikta durum gostergesi yok (bilerek kaldirildi); ic mantik Film/Toplanti/Bosta kararlarini
  sessizce uygular.
- Ayar paneli: saydamlik kaydirici, otomatik baslatma kutusu, tema swatch, sinirlar notu.

## Etkilesim
Surukleme baslik cubugu DragMove. Boyut sag/alt/kose (sol/ust yok, bilerek; cercevesiz pencerede kararli calisan kapsam). Kapatma tepsiye alir, ilk seferde balon ipucu. Cikis yalnizca tepsi menusunde.
