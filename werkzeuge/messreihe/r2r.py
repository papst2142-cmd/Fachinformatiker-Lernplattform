"""Messreihe 438 (Engine festigen 3, A 1.16.0, Weisung P12-399 Antwort 1a): ist eine .NET-DLL vorab uebersetzt (ReadyToRun)?

Aufruf:
  python3 -I werkzeuge/messreihe/r2r.py pruefen <dll> ja|nein   -> Rueckgabe 0, wenn die DLL so ist wie erwartet, sonst 1
  python3 -I werkzeuge/messreihe/r2r.py zeigen <dll>            -> "ReadyToRun: ja|nein"
  python3 -I werkzeuge/messreihe/r2r.py selbsttest <il.dll> <r2r.dll> (Gegenprobe: beide Erwartungen vertauscht sind rot)

Eine vorab uebersetzte DLL hat im CLI-Kopf einen Verweis auf den ManagedNativeHeader (Eintrag 11 des CLI-Kopfs, RVA ungleich 0);
eine reine IL-DLL hat dort 0. Gelesen wird nur der PE-Kopf, nichts wird geaendert.
"""
import struct
import sys


def ist_r2r(pfad):
    with open(pfad, 'rb') as f:
        daten = f.read()
    if daten[:2] != b'MZ':
        raise ValueError(f'{pfad}: keine PE-Datei')
    pe = struct.unpack_from('<I', daten, 0x3C)[0]
    if daten[pe:pe + 4] != b'PE\0\0':
        raise ValueError(f'{pfad}: kein PE-Kopf')
    anzahl = struct.unpack_from('<H', daten, pe + 6)[0]
    opt_groesse = struct.unpack_from('<H', daten, pe + 20)[0]
    opt = pe + 24
    magic = struct.unpack_from('<H', daten, opt)[0]
    verz = opt + (96 if magic == 0x10B else 112)
    cli_rva = struct.unpack_from('<I', daten, verz + 14 * 8)[0]
    if cli_rva == 0:
        raise ValueError(f'{pfad}: keine .NET-DLL')
    abschnitte = opt + opt_groesse
    for i in range(anzahl):
        a = abschnitte + i * 40
        vgroesse, vadr, rgroesse, radr = struct.unpack_from('<IIII', daten, a + 8)
        if vadr <= cli_rva < vadr + max(vgroesse, rgroesse):
            cli = radr + cli_rva - vadr
            native_rva = struct.unpack_from('<I', daten, cli + 64)[0]
            return native_rva != 0
    raise ValueError(f'{pfad}: CLI-Kopf nicht gefunden')


def main(args):
    if len(args) >= 2 and args[0] == 'zeigen':
        print('ReadyToRun: ' + ('ja' if ist_r2r(args[1]) else 'nein'))
        return 0
    if len(args) == 3 and args[0] == 'pruefen':
        ist = 'ja' if ist_r2r(args[1]) else 'nein'
        print(f'{args[1]}: ReadyToRun {ist} (erwartet {args[2]})')
        return 0 if ist == args[2] else 1
    if len(args) == 3 and args[0] == 'selbsttest':
        il, r2r = args[1], args[2]
        gut = not ist_r2r(il) and ist_r2r(r2r)
        print('Selbsttest r2r: IL-DLL nein, R2R-DLL ja, Gegenprobe (vertauscht) rot: ' + ('gruen' if gut else 'ROT'))
        return 0 if gut else 1
    print(__doc__)
    return 2


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
