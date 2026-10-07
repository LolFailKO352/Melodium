# 📋 Melodium – Seznam změn (Changelog)

Všechny významné změny v aplikaci **Melodium** jsou zaznamenávány v tomto souboru. Formát vychází z konvencí [Keep a Changelog](https://keepachangelog.com/cs/1.0.0/) a dodržuje sémantické verzování (Semantic Versioning).

---

## [1.7.0] – 2026-10-05

### 🎧 Paměť relace a navázání poslechu (Session Persistence & Cross-Device Resume)
- **Automatická obnova relace (Session Restore)**:
  - Aplikace si při zavření (nebo minimalizaci do lišty / ukončení) automaticky zapamatuje naposledy přehrávanou skladbu, pozici v sekundách a celou aktivní frontu včetně indexu.
  - Při novém spuštění aplikace se přehrávač ve spodním panelu i fronta okamžitě vizuálně obnoví ve stavu, kde jste skončili (bez nechtěného hlasitého spuštění zvuku do sluchátek).
  - Po stisknutí tlačítka *Přehrát* aplikace automaticky naváže přehrávání přesně na uložené pozici.
- **Navázání poslechu napříč zařízeními (Cross-Device Resume z YouTube Music účtu)**:
  - Stejně jako v oficiální mobilní aplikaci na Androidu a ve webovém rozhraní YouTube Music, aplikace po přihlášení zkontroluje historii poslechu z vašeho Google/YouTube účtu.
  - Pokud jste naposledy poslouchali hudbu na telefonu nebo jiném počítači, aplikace na domovské obrazovce zobrazí elegantní Fluent banner s miniaturou skladby, názvem a interpretem a nabídne možnost pokračovat v poslechu jedním kliknutím tlačítka *Pokračovat*.
  - Banner lze kdykoliv jednoduše zavřít tlačítkem se symbolem křížku.

### 🎨 Nový Microsoft Store Fluent UI Design domovské obrazovky
- **Hero Spotlight rotátor (Carousel)**:
  - Velká interaktivní karta s dynamickým pozadím, odznáčky kategorií (*Hlavní výběr*, *Bez reklam*), názvem a tlačítkem *Přehrát nyní*.
  - Indikátory stránek (pill indikátory s plynulou animací šířky a barev) a šipky `<` / `>` pro přepínání doporučených alb a skladeb.
- **Dvě paralelní sekce vedle sebe**:
  - Levý panel: *Populární skladby* (rychlý přehled skladeb s pořadovými čísly a přímým spuštěním).
  - Pravý panel: *Doporučené výběry* (karty playlistů a rádií v kompaktní mřížce s gradientními obaly).
- **Záhlaví a vyhledávání ve stylu Microsoft Store**:
  - Vycentrované vyhledávací pole v záhlaví okna s klávesovou zkratkou (Ctrl+E).
  - Profilový kruhový avatar v pravém horním rohu s přímým přístupem do správy účtu a knihovny.
  - Vylepšené záhlaví sekcí s klikacími šipkami `>` a náladovými filtry (chips).

### ⚡ Optimalizace jádra a stability
- Bezpečné ukládání relace přehrávače přímo do nastavení aplikace v `LocalApplicationData`.
- Nové API pro získávání historie účtu přímo z Innertube endpointu `FEmusic_history`.

---

## [1.6.0] – 2026-09-20

### ✨ Nové funkce
- **Karaoke texty písní**:
  - Synchronizované texty písní v reálném čase s plynulým posunem podle aktuálního času přehrávání.
  - Integrace více zdrojů (LRCLIB, Musixmatch, Youtube synchro texty).
- **Systémová lišta (System Tray & Minimalizace)**:
  - Možnost běhu na pozadí s ikonou v oznamovací oblasti Windows (systray).
  - Kontextové menu lišty s ovládáním přehrávání (*Přehrát / Pozastavit*, *Zobrazit / Skrýt*, *Ukončit*).
  - Možnost v nastavení zapnout/vypnout zavírání aplikace do lišty.
- **Discord Rich Presence**:
  - Zobrazování aktuálně poslouchané skladby, interpreta, alba a času v profilu Discordu.
- **Automatická kontrola a stahování aktualizací**:
  - Vestavěný modul pro zjišťování nových verzí přímo z GitHub Releases.
  - Možnost stažení a instalace aktualizace přímo z nastavení aplikace.
- **Pokročilá lokalizace**:
  - Přepínání jazyků se zachováním preferencí a dynamickým načítáním překladových slovníků.

### 🛠️ Audio a výkon
- Hybridní přehrávač s okamžitou diskovou mezipamětí skladeb pro 0ms odezvu při opakovaném přehrání.
- Inteligentní přednačítání (prefetching) následující skladby ve frontě na pozadí.
- Nekonečné rádio (Autoplay) při dojití na konec fronty skladeb.
