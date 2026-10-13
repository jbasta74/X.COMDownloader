# X.COM Downloader

Jednoduchá okenní aplikace pro Windows na stahování fotek a videí z **X.com** (dříve Twitter).
Je to grafická nadstavba nad osvědčeným nástrojem [gallery-dl](https://github.com/mikf/gallery-dl):
o samotné stahování se stará gallery-dl, aplikace přidává frontu, přihlášení a pohodlné ovládání.

## Funkce

- **Fronta stahování s průběhem**: libovolný počet URL nebo účtů, u každé položky stav, počet stažených a přeskočených souborů a živý log.
- **Přihlášení přímo v aplikaci**: jednorázové přihlášení k X ve vestavěném prohlížeči (Microsoft Edge WebView2). Cookies si aplikace před stahováním vezme sama, nic se neexportuje ručně.
- **Moje záložky**: jedním tlačítkem přidá do fronty záložky (bookmarks) přihlášeného účtu.
- **Jen nové soubory**: archiv už stažených médií (`--download-archive`), takže opakované stažení účtu dotáhne jen novinky.
- **Rychlé zadávání**: stačí napsat `@ucet` nebo `ucet` a aplikace z toho udělá `https://x.com/ucet/media`. Víc položek oddělte mezerou, čárkou nebo středníkem.
- **Další zdroje cookies**: vlastní soubor `cookies.txt`, čtení z prohlížeče přes gallery-dl, nebo stahování bez přihlášení.

## Stažení

Hotový `XComDownloader.exe` najdete v sekci [Releases](https://github.com/jbasta74/X.COMDownloader/releases/latest). Stačí ho stáhnout a spustit, instalace není potřeba.

## Požadavky

- Windows 10/11 (x64)
- [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)
- [gallery-dl](https://github.com/mikf/gallery-dl#installation): buď `gallery-dl.exe` v `PATH`, nebo cestu k němu vyberete v nastavení
- Microsoft Edge WebView2 Runtime (ve Windows 10/11 obvykle už je)

## Použití

1. Spusťte `XComDownloader.exe`.
2. V **Nastavení** zkontrolujte cestu ke gallery-dl a cílovou složku.
3. U **Přihlášení (cookies)** nechte *Přihlášení v aplikaci* a klikněte na **Přihlásit k X…**. V otevřeném okně se přihlaste ke svému účtu a po zobrazení „✔ Přihlášeno“ okno zavřete tlačítkem **Hotovo**.
4. Do pole **URL nebo @účet** zadejte, co chcete stáhnout, a dejte **Přidat do fronty** (nebo Enter). Pro záložky použijte tlačítko **Moje záložky**.
5. Klikněte na **▶ Spustit**.

Stažené soubory se ukládají do cílové složky v podsložkách podle gallery-dl (např. `twitter\ucet\`).

> Přihlaste se jménem nebo e-mailem a heslem. „Sign in with Google“ může Google ve vestavěném prohlížeči zablokovat.

## Sestavení ze zdrojů

Projekt je pro Visual Studio 2026 (`X.COMDownloader.slnx`), WPF, .NET 10.

```powershell
git clone https://github.com/jbasta74/X.COMDownloader.git
cd X.COMDownloader
dotnet build
```

### Publikace do jednoho exe

Součástí je publikační profil `SingleExe`:

```powershell
dotnet publish X.COMDownloader\X.COMDownloader.csproj -p:PublishProfile=SingleExe
```

Výsledný `XComDownloader.exe` je ve složce `publish\`. Ve výchozím nastavení potřebuje nainstalovaný .NET 10 Desktop Runtime.
Pro exe, které poběží i bez .NET, nastavte v `Properties\PublishProfiles\SingleExe.pubxml` hodnotu `SelfContained` na `true` (soubor pak bude výrazně větší).

## Kde aplikace ukládá data

Vše je v `%AppData%\XComDownloader\`:

| Soubor / složka   | Obsah                                                        |
|-------------------|--------------------------------------------------------------|
| `settings.json`   | nastavení aplikace                                           |
| `archive.sqlite3` | archiv stažených médií (pro volbu „jen nové soubory“)        |
| `WebView2\`       | profil vestavěného prohlížeče včetně přihlášení k X          |
| `x-cookies.txt`   | dočasný soubor cookies pro gallery-dl, po stažení se maže    |

Odhlásit se můžete tlačítkem **Odhlásit**, které smaže cookies z profilu aplikace. Úplný reset provedete smazáním celé složky.

## Struktura projektu

```
X.COMDownloader/
├── App.xaml                   # aplikace a společné styly
├── MainWindow.xaml(.cs)       # hlavní okno: nastavení, fronta, log
├── LoginWindow.xaml(.cs)      # okno s WebView2 pro přihlášení k X
├── Models/
│   └── DownloadItem.cs        # položka fronty
└── Services/
    ├── AppSettings.cs         # načítání a ukládání nastavení
    ├── GalleryDlRunner.cs     # spouštění gallery-dl a čtení jeho výstupu
    └── XSession.cs            # přihlášení přes WebView2 a export cookies
```

## Upozornění

- Přihlášení (cookies) dává plný přístup k vašemu účtu X. Nesdílejte složku `%AppData%\XComDownloader` ani soubory cookies a nikdy je necommitujte do repozitáře.
- Stahujte jen obsah, ke kterému máte oprávnění, a respektujte podmínky služby X a autorská práva.
- Projekt není nijak spojen se společností X Corp. ani s autory gallery-dl.

## Licence

Projekt je šířen pod licencí MIT, viz [LICENSE.txt](LICENSE.txt).
