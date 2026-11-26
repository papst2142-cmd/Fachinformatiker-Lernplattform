"""Messreihe 438 (Engine festigen 3, A 1.16.0): Tabelle aus den Messdateien von werkzeuge/messreihe/messen.sh.

Aufruf: python3 -I werkzeuge/messreihe/auswerten.py <ordner> <rid>
        python3 -I werkzeuge/messreihe/auswerten.py selbsttest
Je Fassung (Engine vorab ja/nein, TieredPGO Vorgabe/aus) Median und alle Einzelwerte in ms. Fehlt ein Wert in einer Datei, steht
"fehlt" in der Zeile (nie still weglassen). Prueft dazu, dass jede Datei die Fassung nennt, mit der sie gemessen wurde.
"""
import glob
import os
import re
import statistics
import sys
import tempfile

PUNKTE = [
    ('spiel', 'Spielseite oeffnen (erster Bau)', re.compile(r'Kaltbau: Spielseite oeffnen \(erster Bau der Stadt, einmalig\): (\d+) ms')),
    ('spiel', 'Stadt bauen Testkarte: Bauen', re.compile(r'Spiel: Stadt bauen Testkarte \(geladen[^)]*\): Bauen (\d+) ms')),
    ('spiel', 'Stadt bauen Testkarte: zusammen', re.compile(r'Spiel: Stadt bauen Testkarte \(geladen[^)]*\):.* zusammen (\d+) ms')),
    ('werkzeug', 'Werkzeug Stadt bauen (Stadt 1)', re.compile(r'Werkzeug: Stadt bauen \(Bauen und Vorbereiten, einmalig\): 1x, Mittel (\d+) ms')),
    ('werkzeug', 'Werkzeug Stadt bauen Testkarte: zusammen', re.compile(r'Werkzeug: Stadt bauen Testkarte \(geladen[^)]*\):.* zusammen (\d+) ms')),
]
FASSUNGEN = [('ja', 'vorgabe'), ('ja', 'aus'), ('nein', 'vorgabe'), ('nein', 'aus')]
NAME = re.compile(r'messung_(spiel|werkzeug)_vorab-(ja|nein)_pgo-(vorgabe|aus)_(\d+)\.txt$')


def lesen(ordner):
    werte, fehler = {}, []
    for pfad in sorted(glob.glob(os.path.join(ordner, 'messung_*.txt'))):
        m = NAME.search(os.path.basename(pfad))
        if not m:
            continue
        art, vorab, pgo, nr = m.group(1), m.group(2), m.group(3), int(m.group(4))
        text = open(pfad, encoding='utf-8', errors='replace').read()
        if art == 'spiel':
            erwartet = 'TieredPGO ' + ('0' if pgo == 'aus' else 'Vorgabe')
            if erwartet not in text:
                fehler.append(f'{os.path.basename(pfad)}: Fassung nicht belegt ("{erwartet}" fehlt)')
        for p_art, name, muster in PUNKTE:
            if p_art != art:
                continue
            treffer = muster.search(text)
            werte.setdefault((name, vorab, pgo), []).append((nr, int(treffer.group(1)) if treffer else None))
    return werte, fehler


def tabelle(werte, rid):
    zeilen = [f'### Messreihe 438: {rid}', '',
              '| Messpunkt | Engine vorab | TieredPGO | Median ms | Einzelwerte ms |', '|---|---|---|---|---|']
    for _, name, _ in PUNKTE:
        for vorab, pgo in FASSUNGEN:
            liste = sorted(werte.get((name, vorab, pgo), []))
            if not liste:
                continue
            zahlen = [w for _, w in liste if w is not None]
            median = f'{statistics.median(zahlen):.0f}' if zahlen else 'fehlt'
            einzeln = ', '.join('fehlt' if w is None else str(w) for _, w in liste)
            zeilen.append(f'| {name} | {vorab} | {pgo} | {median} | {einzeln} |')
    return '\n'.join(zeilen)


def selbsttest():
    with tempfile.TemporaryDirectory() as d:
        def schreiben(name, text):
            with open(os.path.join(d, name), 'w', encoding='utf-8') as f:
                f.write(text)
        schreiben('messung_spiel_vorab-ja_pgo-vorgabe_1.txt',
                  'Kaltbau: Laufzeit 10, TieredPGO Vorgabe, x\nKaltbau: Spielseite oeffnen (erster Bau der Stadt, einmalig): 1156 ms\n'
                  'Spiel: Stadt bauen Testkarte (geladen, Winter, Weihnachten): Bauen 1300 ms, Grundlage 437 ms, 16 Kacheln am Start 288 ms, zusammen 2025 ms; x\n')
        schreiben('messung_spiel_vorab-ja_pgo-vorgabe_2.txt', 'Kaltbau: Laufzeit 10, TieredPGO Vorgabe, x\n')
        # Gegenprobe: Datei sagt Vorgabe, Name sagt aus -> Fehler
        schreiben('messung_spiel_vorab-nein_pgo-aus_1.txt', 'Kaltbau: Laufzeit 10, TieredPGO Vorgabe, x\n')
        werte, fehler = lesen(d)
        t = tabelle(werte, 'probe')
        gut = ('| Stadt bauen Testkarte: zusammen | ja | vorgabe | 2025 | 2025, fehlt |' in t
               and '| Spielseite oeffnen (erster Bau) | ja | vorgabe | 1156 | 1156, fehlt |' in t
               and len(fehler) == 1 and 'vorab-nein_pgo-aus' in fehler[0])
        print('Selbsttest auswerten: fehlende Werte genannt, Gegenprobe falsche Fassung rot: ' + ('gruen' if gut else 'ROT'))
        if not gut:
            print(t)
            print(fehler)
        return 0 if gut else 1


def main(args):
    if args[:1] == ['selbsttest']:
        return selbsttest()
    if len(args) != 2:
        print(__doc__)
        return 2
    werte, fehler = lesen(args[0])
    print(tabelle(werte, args[1]))
    for f in fehler:
        print('FEHLER: ' + f)
    return 1 if fehler else 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
