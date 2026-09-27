# SEC - Hora

## Tehdit modeli
Saldirgan agi yok: uygulama disariya baglanmaz. Risk iceride: hassas pencere basliklarinin diske yazilmasi.

## Onlemler
- Ag kodu yok: HttpClient, Socket, telemetry bagimliligi projede bulunmaz (denetim: `grep HttpClient` bos doner).
- URL saklanmaz, yalnizca domain. Baslik max 200 karakter, hassas eslesmede [gizli].
- Denylist: keepass, keepassxc, 1password, bitwarden, lastpass, dashlane exeleri + banka/cuzdan/sifre domain parcalari.
- DB ve ayarlar %AppData%/Hora altinda, baska kullanici erisemez (kullanici profili ACL).
- Otomatik baslatma HKCU Run, admin yetkisi istemez, UAC tetiklemez.
- Imzasiz exe: SmartScreen uyarabilir, README'de aciklanir. Cozum: ileride kod imzalama sertifikasi.

## Dogrulama
- Wireshark/Guvenlik duvari 5 dk: Hora.exe dis baglanti kurmaz.
- DB acip gozle: bankacilik sirasinda title [gizli].
