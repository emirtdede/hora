# PERF - Hora

## Hedefler ve teknikler
- Ornekleme 1 sn DispatcherTimer degil arka plan Task: UI bloklanmaz.
- UIA okuma yalnizca tarayici exelerde ve 300 ms tavanla, haftada degil saniyede en fazla 1 kez.
- DB yazma 10 sn toplu flush, tek writer SemaphoreSlim, WAL.
- Donut en fazla 7 dilim, liste en fazla 8 satir render.
- NAudio MMDevice her tickte dispose edilir, tutamac sizintisi yok.

## Olcum (kabul kriteri)
- Gorev Yoneticisi 5 dk gozlem: CPU <%1, RAM < 80 MB.
- Soguk acilis kronometre: pencere < 1.5 sn.
- 10 bin satir insert testi < 1 sn (StorageTests genisletilebilir).
- DB boyutu: gunluk ~100 satir, yillik < 10 MB projeksiyon.
