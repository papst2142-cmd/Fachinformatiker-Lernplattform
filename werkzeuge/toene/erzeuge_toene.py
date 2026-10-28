#!/usr/bin/env python3
"""Phase 8 W8 (Weisungen P8-17, P8-17c, P8-17d): Toene des Spiels aus den CC0-Paketen zuschneiden.

Aufruf (Python 3.12, numpy, ffmpeg mit libvorbis):
    python3 -I werkzeuge/toene/erzeuge_toene.py <audio_cc0> [--hoerproben <ordner>]

<audio_cc0> ist der Ordner `anlagen/audio_cc0/` von Fi-Austausch (Originale von Nico, CC0 1.0).
Ergebnis: `src/Lernplattform.App/Assets/Toene/*.ogg` und `toene.json` (Datei -> Anlass, Herkunft, Lizenz).
Die Dateien werden nur angehaengt: eine schon vorhandene Datei mit anderem Inhalt bricht ab
(gleicher Grundsatz wie modelle.bin, Test SpielTonTests.ToeneNurAngehaengt).

- Fluss: ruhigster Ausschnitt von 75 s, Ende in den Anfang uebergeblendet (nahtlose Schleife),
  mono 22 050 Hz, OGG 48 kbit/s.
- Stadt-Grundgeraeusch je Jahreszeit (Weisungen P8-23, P8-24, Nico): 60-s-Schleifen wie der Fluss.
  Fruehling und Sommer: Voegel, Ausschnitt mit den wenigsten Rufen, Tiefpass 4 kHz (klingt entfernt),
  6 dB leiser als die erste W8-Fassung. Herbst: leiser Wind ohne Boeen, die Voegel ganz leise darueber.
  Winter: nur Wind, noch leiser und stark gedaempft (Tiefpass 1,5 kHz, wie unter Schnee), keine Voegel.
- Autos: je Autoart eine Schleife aus loop_0..5, Tonhoehe abgesenkt (Muellwagen am tiefsten).
- Vogelruf: mono, Spitze auf -6 dB.
- Kenney: die genutzten Dateien unveraendert kopiert.
"""

from __future__ import annotations

import hashlib
import json
import subprocess
import sys
import tempfile
import zipfile
from pathlib import Path

import numpy as np

REPO = Path(__file__).resolve().parents[2]
ZIEL = REPO / "src" / "Lernplattform.App" / "Assets" / "Toene"

CC0 = "CC0-1.0"

# Ambiente: Ausschnitt, Ueberblendung, Abtastrate
AMBIENTE_LAENGE = 75.0
AMBIENTE_BLENDE = 3.0
AMBIENTE_RATE = 22050

# Autos (W7, Verkehr.Strecken): Modell -> (Quelle loop_<n>, Faktor der Tonhoehe)
AUTOS = [
    ("taxi", 0, 0.62),
    ("van", 1, 0.56),
    ("sedan", 2, 0.66),
    ("police", 3, 0.6),
    ("ambulance", 4, 0.58),
    ("garbage-truck", 5, 0.42),
]

THIMRAS = ("Park ambiences, Thimras", "https://opengameart.org/content/park-ambiences")
DOMASX2 = ("racing car engine sound loops, domasx2", "https://opengameart.org/node/5633")
SYNCOPIKA = ("Bird chirping sounds, syncopika", "https://opengameart.org/content/bird-chirping-sounds")
KENNEY = {
    "kenney_impact-sounds": ("Impact Sounds, Kenney", "https://kenney.nl/assets/impact-sounds"),
    "kenney_rpg-audio": ("RPG Audio, Kenney", "https://kenney.nl/assets/rpg-audio"),
    "kenney_music-jingles": ("Music Jingles, Kenney", "https://kenney.nl/assets/music-jingles"),
}

# Kenney: (Paket, Pfad im ZIP, Anlass)
KENNEY_DATEIEN = (
    [("kenney_impact-sounds", f"Audio/footstep_{boden}_{n:03d}.ogg", f"schritt_{boden}")
     for boden in ("concrete", "grass", "wood", "carpet") for n in range(5)]
    + [("kenney_rpg-audio", f"Audio/doorOpen_{n}.ogg", "tuer_auf") for n in (1, 2)]
    + [("kenney_rpg-audio", f"Audio/doorClose_{n}.ogg", "tuer_zu") for n in (1, 2, 3, 4)]
    + [("kenney_rpg-audio", f"Audio/{name}.ogg", "kasse") for name in ("handleCoins", "handleCoins2")]
    # E2 P8-17d Punkt 4: eine Reihe; Nico waehlte an den Hoerproben STEEL (Weisung P8-21)
    + [("kenney_music-jingles", "Audio/Steel jingles/jingles_STEEL07.ogg", "melodie_feierabend"),
       ("kenney_music-jingles", "Audio/Steel jingles/jingles_STEEL01.ogg", "melodie_erfolg"),
       ("kenney_music-jingles", "Audio/Steel jingles/jingles_STEEL09.ogg", "melodie_ticket")]
)

# zweite Reihe nur als Hoerprobe (nicht im Programm)
STEEL = [("Audio/Steel jingles/jingles_STEEL07.ogg", "melodie_feierabend"),
         ("Audio/Steel jingles/jingles_STEEL01.ogg", "melodie_erfolg"),
         ("Audio/Steel jingles/jingles_STEEL09.ogg", "melodie_ticket")]


def lesen(pfad: Path | str, rate: int, daten: bytes | None = None) -> np.ndarray:
    """Datei (oder Bytes) als mono float32 mit <rate> Hz."""
    eingabe = "pipe:0" if daten is not None else str(pfad)
    ergebnis = subprocess.run(["ffmpeg", "-v", "error", "-i", eingabe, "-ac", "1", "-ar", str(rate), "-f", "f32le", "pipe:1"],
                              input=daten, capture_output=True, check=True)
    return np.frombuffer(ergebnis.stdout, dtype="<f4").astype(np.float64)


def schreiben(pfad: Path, signal: np.ndarray, rate: int, bitrate: str) -> None:
    roh = np.clip(signal, -1.0, 1.0).astype("<f4").tobytes()
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ar", str(rate), "-ac", "1", "-i", "pipe:0",
                    "-map_metadata", "-1", "-fflags", "+bitexact", "-flags:a", "+bitexact",
                    "-c:a", "libvorbis", "-b:a", bitrate, str(pfad)], input=roh, check=True)


def huellkurve(signal: np.ndarray, rate: int, fenster: float = 0.05) -> np.ndarray:
    n = int(rate * fenster)
    stuecke = len(signal) // n
    teile = signal[: stuecke * n].reshape(stuecke, n)
    return 20 * np.log10(np.sqrt((teile ** 2).mean(axis=1)) + 1e-9)


def vogelband(signal: np.ndarray, rate: int) -> np.ndarray:
    """Energie 2,5 bis 8 kHz (Vogelrufe) je 50 ms in dB."""
    n = int(rate * 0.05)
    stuecke = len(signal) // n
    teile = signal[: stuecke * n].reshape(stuecke, n) * np.hanning(n)
    spektrum = np.abs(np.fft.rfft(teile, axis=1)) ** 2
    frequenz = np.fft.rfftfreq(n, 1 / rate)
    band = spektrum[:, (frequenz >= 2500) & (frequenz <= 8000)].sum(axis=1)
    return 10 * np.log10(band + 1e-12)


def ruhigster(kurve: np.ndarray, fenster: int) -> int:
    """Beginn (in Kurvenschritten) des Fensters mit dem kleinsten Abstand Spitze (p99) zu Mitte (p50)."""
    best, wert = 0, None
    for start in range(0, len(kurve) - fenster, 20):
        teil = kurve[start:start + fenster]
        abstand = float(np.percentile(teil, 99) - np.percentile(teil, 50))
        if wert is None or abstand < wert:
            best, wert = start, abstand
    return best


def schleife(signal: np.ndarray, start: int, rate: int) -> np.ndarray:
    laenge = int(AMBIENTE_LAENGE * rate)
    blende = int(AMBIENTE_BLENDE * rate)
    teil = signal[start:start + laenge + blende]
    aus = teil[:laenge].copy()
    t = np.linspace(0, np.pi / 2, blende)
    # gleiche Leistung: Ende (Fortsetzung hinter dem Ausschnitt) blendet in den Anfang
    aus[:blende] = teil[laenge:laenge + blende] * np.cos(t) + teil[:blende] * np.sin(t)
    return aus


def auf_rms(signal: np.ndarray, db: float) -> np.ndarray:
    rms = np.sqrt((signal ** 2).mean())
    return signal * (10 ** (db / 20) / max(rms, 1e-9))


def auf_spitze(signal: np.ndarray, db: float) -> np.ndarray:
    return signal * (10 ** (db / 20) / max(np.abs(signal).max(), 1e-9))


def ambiente(quelle: Path, art: str) -> np.ndarray:
    signal = lesen(quelle, AMBIENTE_RATE)
    kurve = vogelband(signal, AMBIENTE_RATE) if art == "fluss" else huellkurve(signal, AMBIENTE_RATE)
    schritt = 0.05
    fenster = int((AMBIENTE_LAENGE + AMBIENTE_BLENDE) / schritt)
    start = ruhigster(kurve[: len(kurve) - 1], fenster)
    return auf_rms(schleife(signal, int(start * schritt * AMBIENTE_RATE), AMBIENTE_RATE), -24.0 if art == "voegel" else -23.0)


def tiefpass(signal: np.ndarray, rate: int, grenze: float) -> np.ndarray:
    # weich (2. Ordnung), ueber die ganze Schleife: bleibt nahtlos
    spektrum = np.fft.rfft(signal)
    spektrum *= 1 / np.sqrt(1 + (np.fft.rfftfreq(len(signal), 1 / rate) / grenze) ** 4)
    return np.fft.irfft(spektrum, len(signal))


def rufe_je_fenster(band: np.ndarray, fenster: int) -> list[tuple[int, int]]:
    oben = band > np.percentile(band, 50) + 10
    summe = np.cumsum(np.r_[False, oben[1:] & ~oben[:-1]])
    return [(int(summe[i + fenster] - summe[i]), i) for i in range(0, len(summe) - fenster, 20)]


def jahreszeiten(cc0: Path) -> dict[str, np.ndarray]:
    """Stadt-Grund je Jahreszeit (60 s, 22 050 Hz). Pegel bezogen auf die erste W8-Fassung (-24 dB RMS)."""
    global AMBIENTE_LAENGE
    alt, AMBIENTE_LAENGE = AMBIENTE_LAENGE, 60.0
    try:
        ordner = cc0 / "opengameart_park_ambiences_thimras"
        fenster = int((AMBIENTE_LAENGE + AMBIENTE_BLENDE) / 0.05)
        voegel = lesen(ordner / "park_ambience_birds.ogg", AMBIENTE_RATE)
        start = min(rufe_je_fenster(vogelband(voegel, AMBIENTE_RATE), fenster))[1]
        ruhig = tiefpass(schleife(voegel, int(start * 0.05 * AMBIENTE_RATE), AMBIENTE_RATE), AMBIENTE_RATE, 4000.0)
        wind = lesen(ordner / "park_ambience_wind.ogg", AMBIENTE_RATE)
        start = ruhigster(huellkurve(wind, AMBIENTE_RATE)[:-1], fenster)
        wind = schleife(wind, int(start * 0.05 * AMBIENTE_RATE), AMBIENTE_RATE)
        return {
            "voegel": auf_rms(ruhig, -30.0),
            "herbst": auf_rms(auf_rms(wind, -28.0) + auf_rms(ruhig, -38.0), -27.5),
            "winter": auf_rms(tiefpass(wind, AMBIENTE_RATE, 1500.0), -31.0),
        }
    finally:
        AMBIENTE_LAENGE = alt


def auto(quelle: Path, faktor: float) -> np.ndarray:
    # Tonhoehe absenken: langsamer abspielen (laenger und tiefer), wieder auf 44 100 Hz
    signal = lesen(quelle, 44100)
    alt = np.arange(len(signal))
    neu = np.arange(0, len(signal) - 1, faktor)
    return auf_rms(np.interp(neu, alt, signal), -20.0)


def sha(pfad: Path) -> str:
    return hashlib.sha256(pfad.read_bytes()).hexdigest()


def anhaengen(eintraege: list[dict], datei: str, inhalt_neu: Path, anlass: str, werk: tuple[str, str], quelle: str) -> None:
    ziel = ZIEL / datei
    if ziel.exists() and ziel.read_bytes() != inhalt_neu.read_bytes():
        raise SystemExit(f"FEHLER: {datei} gibt es schon mit anderem Inhalt (Toene nur anhaengen)")
    ziel.write_bytes(inhalt_neu.read_bytes())
    eintraege.append({"datei": datei, "anlass": anlass, "werk": werk[0], "fundstelle": werk[1], "quelle": quelle,
                      "lizenz": CC0, "sha256": sha(ziel)})


def main() -> int:
    argumente = sys.argv[1:]
    if not argumente:
        print(__doc__)
        return 2
    cc0 = Path(argumente[0])
    hoerproben = Path(argumente[argumente.index("--hoerproben") + 1]) if "--hoerproben" in argumente else None
    ZIEL.mkdir(parents=True, exist_ok=True)
    arbeit = Path(tempfile.mkdtemp(prefix="toene_"))
    eintraege: list[dict] = []

    grund = jahreszeiten(cc0)
    for art, datei, anlass, name in (("voegel", "stadt_voegel_ruhig.ogg", "stadt_grund", "park_ambience_birds.ogg"),
                                     ("herbst", "stadt_herbst.ogg", "stadt_grund_herbst", "park_ambience_wind.ogg + park_ambience_birds.ogg"),
                                     ("winter", "stadt_winter.ogg", "stadt_grund_winter", "park_ambience_wind.ogg")):
        tmp = arbeit / datei
        # gedaempft (Tiefpass), darum reichen 32 kbit/s
        schreiben(tmp, grund[art], AMBIENTE_RATE, "32k")
        anhaengen(eintraege, datei, tmp, anlass, THIMRAS, name)
    tmp = arbeit / "stadt_fluss.ogg"
    schreiben(tmp, ambiente(cc0 / "opengameart_park_ambiences_thimras" / "park_ambience_river.ogg", "fluss"), AMBIENTE_RATE, "48k")
    anhaengen(eintraege, "stadt_fluss.ogg", tmp, "fluss", THIMRAS, "park_ambience_river.ogg")

    tmp = arbeit / "vogelruf.ogg"
    schreiben(tmp, auf_spitze(lesen(cc0 / "opengameart_bird_chirping_syncopika" / "birdchirping071414.wav", 44100), -6.0), 44100, "64k")
    anhaengen(eintraege, "vogelruf.ogg", tmp, "vogelruf", SYNCOPIKA, "birdchirping071414.wav")

    for modell, nr, faktor in AUTOS:
        tmp = arbeit / f"auto_{modell}.ogg"
        schreiben(tmp, auto(cc0 / "opengameart_car_engine_loops_domasx2" / f"loop_{nr}.wav", faktor), 44100, "64k")
        anhaengen(eintraege, f"auto_{modell}.ogg", tmp, "auto:car-" + modell, DOMASX2, f"loop_{nr}.wav (Tonhoehe x {faktor})")

    for paket, pfad, anlass in KENNEY_DATEIEN:
        with zipfile.ZipFile(cc0 / f"{paket}.zip") as zip_:
            daten = zip_.read(pfad)
        tmp = arbeit / Path(pfad).name
        tmp.write_bytes(daten)
        anhaengen(eintraege, Path(pfad).name, tmp, anlass, KENNEY[paket], f"{paket}.zip/{pfad}")

    liste = ZIEL / "toene.json"
    alt = json.loads(liste.read_text(encoding="utf-8")) if liste.exists() else {"dateien": []}
    bekannt = {e["datei"] for e in alt["dateien"]}
    for eintrag in eintraege:
        if eintrag["datei"] not in bekannt:
            alt["dateien"].append(eintrag)
    alt["_hinweis"] = ("Phase 8 W8: Toene des Spiels (werkzeuge/toene/erzeuge_toene.py), alle CC0 1.0. "
                       "Nur angehaengt; anlass ordnet die Datei dem Tonplan zu (Core/Spiel/Ton/Tonplan.cs).")
    liste.write_text(json.dumps({"_hinweis": alt["_hinweis"], "dateien": alt["dateien"]}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    groesse = sum((ZIEL / e["datei"]).stat().st_size for e in alt["dateien"])
    print(f"{len(alt['dateien'])} Toene, zusammen {groesse / 1024:.0f} KB")

    if hoerproben is not None:
        hoerproben_bauen(cc0, hoerproben)
    return 0


def mischen(teile: list[tuple[float, np.ndarray, float]], laenge: float, rate: int = 44100) -> np.ndarray:
    aus = np.zeros(int(laenge * rate))
    for beginn, signal, laut in teile:
        a = int(beginn * rate)
        stueck = signal[: max(0, len(aus) - a)] * laut
        aus[a:a + len(stueck)] += stueck
    return aus


def ton(datei: str, rate: int = 44100) -> np.ndarray:
    return lesen(ZIEL / datei, rate)


def hoerproben_bauen(cc0: Path, ordner: Path) -> None:
    """Je Anlass eine Datei (Weisung P8-17d Punkt 7) und 30 s Stadt mit Voegeln, Fluss und Auto nah."""
    ordner.mkdir(parents=True, exist_ok=True)
    def raus(name: str, signal: np.ndarray) -> None:
        schreiben(ordner / name, auf_spitze(signal, -1.0) if np.abs(signal).max() > 0.89 else signal, 44100, "96k")
    for boden in ("concrete", "grass", "wood", "carpet"):
        teile = [(i * 0.36, ton(f"footstep_{boden}_{i % 5:03d}.ogg"), 0.8) for i in range(10)]
        raus(f"01_schritte_{boden}.ogg", mischen(teile, 4.0))
    raus("02_tuer_auf_und_zu.ogg", mischen([(0.0, ton("doorOpen_1.ogg"), 0.8), (1.5, ton("doorClose_2.ogg"), 0.8),
                                            (3.0, ton("doorOpen_2.ogg"), 0.8), (4.5, ton("doorClose_4.ogg"), 0.8)], 6.0))
    raus("03_kasse.ogg", mischen([(0.0, ton("handleCoins.ogg"), 0.8), (1.5, ton("handleCoins2.ogg"), 0.8)], 3.0))
    with zipfile.ZipFile(cc0 / "kenney_music-jingles.zip") as zip_:
        for pfad, anlass in STEEL:
            raus(f"04_melodie_{anlass.split('_')[1]}_STEEL.ogg", mischen([(0.2, lesen("", 44100, zip_.read(pfad)), 0.9)], 2.2))
    raus("05_vogelruf.ogg", mischen([(0.3, ton("vogelruf.ogg"), 0.5)], 4.0))
    for modell, _, _ in AUTOS:
        signal = ton(f"auto_{modell}.ogg")
        raus(f"06_auto_{modell}.ogg", np.tile(signal, int(3 * 44100 / len(signal)) + 1)[: 3 * 44100] * 0.7)
    # 30 s Stadt je Jahreszeit: Grund, Fluss mittel, Einzelrufe wie im Spiel, ein Taxi faehrt nah vorbei
    laenge = 30.0
    fluss = ton("stadt_fluss.ogg")[: int(laenge * 44100)]
    taxi = ton("auto_taxi.ogg")
    taxi = np.tile(taxi, int(laenge * 44100 / len(taxi)) + 1)[: int(laenge * 44100)]
    t = np.arange(int(laenge * 44100)) / 44100
    # Abstand des Autos zur Bildmitte: von 24 auf 1 (bei 15 s) und wieder auf 24, Laut = 1 / (1 + d / 6)
    abstand = np.abs(t - 15) * 1.55 + 1
    naehe = np.where(abstand >= 25, 0, 1 / (1 + abstand / 6))
    # Einzelrufe (Tonplan.Rufe): Sommer alle 8 bis 25 s (0,3), Fruehling und Herbst 30 bis 90 s (0,2), Winter keine
    for name, datei, rufe, laut in (("fruehling", "stadt_voegel_ruhig.ogg", (19.0,), 0.2), ("sommer", "stadt_voegel_ruhig.ogg", (4.5, 17.0, 26.5), 0.3),
                                    ("herbst", "stadt_herbst.ogg", (19.0,), 0.2), ("winter", "stadt_winter.ogg", (), 0.0)):
        grund = ton(datei)[: int(laenge * 44100)]
        stadt = grund * 0.8 + fluss * 0.8 * 0.45 + taxi * naehe * 0.8 * 0.6
        raus(f"08_stadt_30s_{name}.ogg", mischen([(0, stadt, 1.0)] + [(s, ton("vogelruf.ogg"), laut) for s in rufe], laenge))
    print(f"Hoerproben in {ordner}")


if __name__ == "__main__":
    sys.exit(main())
