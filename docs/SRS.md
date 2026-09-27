# SRS - Hora

## 1. Fonksiyonel gereksinimler
- SRS-FR1: Ornekleme periyodu 1000 ms, tolerans +-100 ms.
- SRS-FR2: Idle esigi 60 sn sabit (GetLastInputInfo, TickCount overflow guvenli).
- SRS-FR3: Film karari: o monitorde fullscreen (+-4 px) VE render session Active veya 5 sn pencerede peak > 0.02.
- SRS-FR4: Toplanti karari: bilinen exe/domain VE (render Active VEYA capture Active), 4 sa tavan sonra Bosta.
- SRS-FR5: Kilit: SessionSwitch Lock/Unlock + LogonUI fallback; uykuda tick uretilmez cunku surec askida.
- SRS-FR6: Domain: UIAutomation Edit+ValuePattern 300 ms timeout, fallback baslik/sezgisel domain, sonuc bos olabilir.
- SRS-FR7: Gizlilik: denylist disinda baslik max 200 karakter; hassasta [gizli].
- SRS-FR8: DB UPSERT (Date,Exe,Domain), WAL, tek writer, busy retry 3x.
- SRS-FR9: Ayarlar JSON, aralik disi degerler sanitize edilir (genislik 260-800, opaklik 0.4-1.0).
- SRS-FR10: Tek instance Mutex "HoraSingleInstance", ikinci acilis sessizce kapanir.

## 2. Performans gereksinimleri
- CPU: bosta olcumde <%1 (1 sn timer + 10 sn flush).
- RAM: < 80 MB calisma (WPF + SQLite + NAudio).
- Baslangic: < 1.5 sn pencere gorunur.
- DB: yillik < 10 MB (gunluk ~100 satir x ~60 byte).
- UIA okuma ana donguyu bloklamaz (Task.Run + 300 ms).

## 3. Guvenilirlik
- Tum harici okumalar try/catch, logger asla firlatmaz.
- Flush kapanista garanti, ara flush 10 sn.
