#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Erzeugt die Vergleichswerte G25 (Phase 3, Welle 1, Paket R2 "Subnetting"), gerechnet
vom echten Python-Code der Python-Linie (v0.63.2, eingefroren) mit Python 3.12 (CI):

  adressen  - Fallliste je Einstieg (Weisung P3-2 A3) fuer jeden Eingabetext:
              * Netzdaten: ip_network(text.strip(), strict=False) (Version, str(),
                Netzadresse, Praefix), subnet_report(text), ipv6_details(text) und
                die Netzdaten der Rechner-Seite in beiden Modi (wie app_gui
                SubnetCalc.calc: subnet_report, andere Version -> subnet_parse,
                im Modus IPv6 dazu Ausgeschrieben und Adresstyp),
              * subnet_parse(text, None/4/6) (Aufteilen und Hostbedarf),
              * Trainer-Pruefer: ip_address(text) (Version, Wert, str(), exploded),
                ip_network(text) mit strict=True, dazu ip_interface(text).
              Faelle: feste Liste (ohne Praefix, Maske, Hostmaske, "/024", Zone,
              ::ffff:1.2.3.4, ::1.2.3.4, /128, fuehrende Nullen, fremde Ziffern ...),
              Netztexte aus den Python-Tests und feste Zufallsadressen (Startwert 25).
  typen     - ipv6_type(Wert, Praefix) fuer feste und zufaellige Netze.
  anzahl    - subnet_count(text).
  aufteilen - split_network(net, count, version) mit Kennwerten, split_report fuer
              Seiten 1, 2, letzte, Sprung und ausserhalb (PC und schmal),
              split_explain (PC und schmal), oder die Meldung.
  zeilen    - read_host_lines(text).
  bedarf    - subnet_by_hosts(net, needs, version) mit Kennwerten, hosts_report und
              hosts_explain (PC und schmal), oder die Meldung.
  konstanten - IPV6_TYPES (Praefix als Hex-Text).

Es werden keine Lerndaten benutzt; alle Daten- und Einstellungsordner zeigen
in einen leeren Temp-Ordner.

Aufruf:
  python3 -I werkzeuge/vergleich/erzeuge_subnetting.py <pfad_fisi_lernplattform> tests/Vergleich/subnetting.json
"""
import ast
import atexit
import ipaddress
import json
import os
import random
import re
import shutil
import sys
import tempfile

TESTDATEIEN = [
    "test_rechner_0632.py", "test_rechner_bedarf.py", "test_rechner_pc_0632.py",
    "test_rechner_seite_breite.py", "test_subnetz_aufteilen.py", "test_subnetz_handy.py",
    "test_subnetz_pc.py", "test_subnetz_pc_breite.py", "test_rechenweg.py",
    "test_trainer_breite_0632.py", "test_trainer_nibble.py", "test_trainer_richtig_waere_pc.py",
    "test_ipv6_details.py", "test_anzeige_rfc.py", "test_anzeige_handy.py",
    "test_handy_anzeige_0632.py",
]

ADRESSEN_FEST = [
    # IPv4 mit Praefix, Maske, Hostmaske, ohne Praefix
    "192.168.1.50/24", "192.168.10.0/24", "10.0.0.0/8", "0.0.0.0/0", "255.255.255.255/32",
    "10.0.0.0/255.255.255.0", "10.0.0.5/255.255.255.0", "10.0.0.0/0.0.0.255", "10.0.0.0/0.255.255.255",
    "10.0.0.0/0.0.0.0", "10.0.0.0/255.255.255.255", "10.0.0.0/255.0.255.0", "10.0.0.0/255.255.255.1",
    "10.0.0.0/024", "10.0.0.0/0024", "10.0.0.0/00", "10.0.0.0/33", "10.0.0.0/-1", "10.0.0.0/+24",
    "10.0.0.0/ 24", "10.0.0.0/24 ", " 10.0.0.0/24 ", "\t10.0.0.0/24\n", "10.0.0.0/2٤", "10.0.0.0/²",
    "192.168.1.1", "192.168.1.1/31", "192.168.1.0/31", "192.168.1.1/32", "10.1.2.3/30",
    "010.0.0.1", "10.0.0.01/24", "1.2.3", "1.2.3.4.5", "1", "", " ", "1.2.3.4/", "/24", "1.2.3.4//24",
    "1.2.3.256", "1.2.3.-1", "1.2.3.4 /24", "1.2.3. 4", "١.2.3.4", "1.2.3.4/24/1",
    "1.2.3.0004", "0.0.0.0", "127.0.0.1/8", "172.16.5.4/12", "1.2.3.4/255.255.255.256",
    "1.2.3.4/255.255.0255.0", "1.2.3.4/255.255.255", "1.2.3.4%1/24", "1.2.3.4/24%1",
    # IPv6
    "2001:db8::1/64", "2001:db8::/32", "2001:db8:abcd::/48", "2001:db8:abcd::1/48", "2001:DB8:ABCD::1/48",
    "::/0", "::/128", "::1", "::1/128", "::", ":::", ":", "::/129", "2001:db8::/064", "2001:db8::/ffff::",
    "fe80::1%1", "fe80::1%eth0/64", "fe80::%eth0/64", "fe80::%eth0/10", "fe80::1%/64", "fe80::1%a%b",
    "fe80::1%", "%eth0", "fe80::1%eth 0/64", "fe80::1%ä/64", "::ffff:1.2.3.4%eth0",
    "::ffff:1.2.3.4", "::ffff:1.2.3.4/128", "::ffff:1.2.3.4/96", "::ffff:0:0/96", "::ffff:102:304",
    "0:0:0:0:0:ffff:102:304/128", "::ffff:1.2.3.4/127", "::fffe:1.2.3.4", "::1.2.3.4", "::1.2.3.4/128",
    "1:2:3:4:5:6:1.2.3.4", "1:2:3:4:5:6:7:1.2.3.4", "::1.2.3.4.5", "::1.2.3", "::01.2.3.4",
    "1:2:3:4:5:6:7:8", "1:2:3:4:5:6:7:8:9", "1::2::3", "1:2:3:4:5:6:7::", "::1:2:3:4:5:6:7",
    "1:2:3:4:5:6::7:8", "1:2:3:4::5:6:7:8", "12345::", "g::", "::g", "1:2:3:4:5:6:7:", ":1:2:3:4:5:6:7",
    "2001:db8:0:0:1:0:0:1", "2001:0:0:1:0:0:0:1", "2001:db8::0:1", "0:0:1:0:0:0:1:0", "1:0:0:2:0:0:3:4",
    "2001:0db8:0000:0000:0000:0000:0000:0001/64", "2001:0db8:0000:0000:0000:0000:0000:00001/64",
    "fe80::/10", "fe80::/9", "fc00::/7", "fd00::/8", "fc00::/8", "fec0::/10", "ff02::1", "ff00::/8",
    "3fff::/20", "3fff:1000::/20", "2000::/3", "4000::/2", "::/64", "::2/127", "2001:db8:abcd:12::/64",
    "2001:db8::/31", "2001:db8::/33", "1::/16", "１::", "2001:db8::1 /64", "2001:db8::1/ 64",
    "a" * 46, "1:2:3:4:5:6:7:8" + ":0" * 3, "0" * 40 + "::1", "0" * 41 + "::1", "1.2.3.4/0.0.0.0",
]


def literale(repo):
    texte = []
    for name in TESTDATEIEN:
        with open(os.path.join(repo, name), encoding="utf-8") as handle:
            baum = ast.parse(handle.read())
        for knoten in ast.walk(baum):
            if isinstance(knoten, ast.Constant) and isinstance(knoten.value, str):
                texte.append(knoten.value)
    return texte


def zufall_adressen():
    zufall = random.Random(25)
    ergebnis = []
    for _ in range(80):
        wert = zufall.getrandbits(32)
        ergebnis.append("%s/%d" % (ipaddress.IPv4Address(wert), zufall.randint(0, 32)))
    for _ in range(160):
        gruppen = [zufall.choice([0, 0, 0, zufall.getrandbits(16), zufall.getrandbits(4)]) for _ in range(8)]
        wert = 0
        for gruppe in gruppen:
            wert = (wert << 16) | gruppe
        ergebnis.append("%s/%d" % (ipaddress.IPv6Address(wert), zufall.randint(0, 128)))
        ergebnis.append(":".join("%x" % g for g in gruppen))
    return ergebnis


def gross(wert):
    return None if wert is None else str(wert)


def netz_info(netz):
    if netz is None:
        return None
    return [netz.version, str(netz), str(int(netz.network_address)), netz.prefixlen]


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    repo, ziel = sys.argv[1], sys.argv[2]
    tmp = tempfile.mkdtemp(prefix="vergleich_")
    atexit.register(shutil.rmtree, tmp, True)   # Sammelliste 91: Temp-Ordner nach dem Lauf weg
    for name in ("HOME", "APPDATA", "LOCALAPPDATA", "XDG_DATA_HOME", "XDG_CONFIG_HOME"):
        os.environ[name] = tmp
    os.environ["FISI_DB_PATH"] = os.path.join(tmp, "unbenutzt.db")
    sys.path.insert(0, repo)
    import fisi_core as fc

    def versuch(aufruf):
        try:
            return {"wert": aufruf()}
        except fc.InputError as fehler:
            return {"fehler": str(fehler)}

    def netzdaten(net, mode):
        # wie app_gui SubnetCalc.calc (key == "netzdaten"); mobile CalcScreen.calc_subnet gleich
        version = 4 if mode == "ipv4" else 6
        text = fc.subnet_report(net)
        found = ipaddress.ip_network(net.strip(), strict=False)
        if found.version != version:
            fc.subnet_parse(str(found), version)
        if mode == "ipv6":
            text += "".join("\n%-22s: %s" % pair for pair in fc.ipv6_details(net))
        return text

    def roh(aufruf):
        try:
            return aufruf()
        except ValueError:
            return None

    def adresse_info(adresse):
        if adresse is None:
            return None
        try:
            lang = adresse.exploded
        except ValueError:
            lang = None
        return [adresse.version, str(int(adresse)), str(adresse), lang]

    netztexte = [t for t in literale(repo)
                 if len(t) <= 60 and re.fullmatch(r"[0-9A-Fa-f:./%a-z ]*[:.][0-9A-Fa-f:./%a-z ]*", t)]
    faelle = []
    for text in ADRESSEN_FEST + netztexte + zufall_adressen():
        if text not in faelle:
            faelle.append(text)
    adressen = []
    for text in faelle:
        schnitt = roh(lambda: ipaddress.ip_interface(text))
        adressen.append({
            "text": text,
            "netz": netz_info(roh(lambda: ipaddress.ip_network(text.strip(), strict=False))),
            "bericht": versuch(lambda: fc.subnet_report(text)),
            "details": versuch(lambda: [list(p) for p in fc.ipv6_details(text)]),
            "netzdaten": [versuch(lambda: netzdaten(text, "ipv4")), versuch(lambda: netzdaten(text, "ipv6"))],
            "parse": [versuch(lambda v=v: {k: gross(w) if isinstance(w, int) else w
                                          for k, w in fc.subnet_parse(text, v).items()}) for v in (None, 4, 6)],
            "adresse": adresse_info(roh(lambda: ipaddress.ip_address(text))),
            "streng": netz_info(roh(lambda: ipaddress.ip_network(text))),
            "schnittstelle": None if schnitt is None else [schnitt.version, str(schnitt), str(int(schnitt.ip)),
                                                           netz_info(schnitt.network)],
        })

    zufall = random.Random(6)
    typen = []
    for basis, laenge, _text in fc.IPV6_TYPES:
        for praefix in sorted({0, 1, 2, 3, 6, 7, 8, 9, 10, 11, 19, 20, 21, 31, 32, 33, 64, 127, 128, laenge - 1, laenge, laenge + 1}):
            if 0 <= praefix <= 128:
                wert = basis & (((1 << 128) - 1) ^ ((1 << (128 - praefix)) - 1))
                typen.append([str(wert), praefix, fc.ipv6_type(wert, praefix)])
    for _ in range(300):
        praefix = zufall.randint(0, 128)
        wert = zufall.getrandbits(128) & (((1 << 128) - 1) ^ ((1 << (128 - praefix)) - 1))
        typen.append([str(wert), praefix, fc.ipv6_type(wert, praefix)])

    anzahl_faelle = ["1", "6", " 6 ", "0", "-1", "1,5", "4,0", "abc", "", "1.000", "1 000", "01.000",
                     "9007199254740993", "9.007.199.254.740.993", "900.719.925.474.099",
                     "18446744073709551617", "18446744073709551616", "9007199254740993,5",
                     "18.446.744.073.709.551.616", "99999999999999999999", "1e3", "2.5", "1.0", "1,000"]
    anzahl = [[t, versuch(lambda t=t: str(fc.subnet_count(t)))] for t in anzahl_faelle]

    def aufteilung_info(r):
        felder = {k: gross(v) if isinstance(v, int) and not isinstance(v, bool) else v for k, v in r.items()}
        return felder

    aufteil_faelle = []
    for net in ("192.168.10.0/24", "192.168.10.77/24", "10.0.0.0/8", "0.0.0.0/0", "192.168.10.0/30",
                "192.168.10.0/31", "192.168.10.1/32", "172.16.0.0/12", "10.20.30.40/255.255.0.0"):
        for count in ("1", "2", "3", "4", "5", "6", "8", "9", "16", "17", "20", "64", "65", "128", "255",
                      "256", "257", "1.000", "16777216", "16777217", "4294967296", "4294967297", "0", "abc"):
            aufteil_faelle.append((net, count, None))
    for net in ("2001:db8:abcd::/48", "2001:DB8:ABCD::1/48", "2001:db8::/32", "::/0", "2001:db8::/64",
                "2001:db8::/120", "2001:db8::/126", "2001:db8::/127", "2001:db8::1/128", "fe80::/10",
                "2001:db8:abcd:12::/64", "2001:db8:abcd::/46", "2001:db8:ab00::/40"):
        for count in ("1", "2", "3", "6", "12", "16", "17", "100", "256", "4096", "65536", "65537",
                      "4294967296", "18446744073709551616", "18446744073709551617", "1.000"):
            aufteil_faelle.append((net, count, None))
    for net, count, version in (("2001:db8::/32", "2", 4), ("10.0.0.0/8", "2", 6), ("10.0.0.0/8", "2", 4),
                                ("2001:db8::/32", "2", 6), ("fe80::1%1/64", "2", None),
                                ("10.0.0.0", "2", None), ("", "2", None), ("10.0.0.0/8", "", None)):
        aufteil_faelle.append((net, count, version))
    aufteilen = []
    for net, count, version in aufteil_faelle:
        eintrag = {"net": net, "count": count, "version": version}
        try:
            r = fc.split_network(net, count, version)
        except fc.InputError as fehler:
            eintrag["fehler"] = str(fehler)
            aufteilen.append(eintrag)
            continue
        eintrag["kennwerte"] = aufteilung_info(r)
        seiten = r["seiten"]
        eintrag["seiten"] = {}
        for seite in sorted({1, 2, 3, seiten, seiten - 1, seiten // 2 + 1, seiten + 5, 0, -3}):
            eintrag["seiten"][str(seite)] = [fc.split_report(r, seite), fc.split_report(r, seite, schmal=True)]
        eintrag["rechenweg"] = [fc.split_explain(r), fc.split_explain(r, schmal=True)]
        aufteilen.append(eintrag)

    zeilen_faelle = [
        "A 50\nB 20\nC 10\nD 2", "", " \n \n", "A 50, B 20", "A 50; B 20", "A/B 20", "A:50\nVertrieb Nord 20 Hosts\n7",
        "A 1.000", "A 1 000", "A: 1 000 Hosts", "A 50 B 20", "VLAN 10 20", "VLAN 10: 20", "A: 1: 2",
        "A - 50", "A – 50", "A — 50", "A −50", "A\t50", "50", "50 Hosts", "Hosts", "A Hosts",
        "A 0", "A -5", "A 4294967294", "A 4294967295", "A 1,5", "A 50.0", "A 50,0", "A x",
        "Netz 1 5\n7", "7\nNetz 1 5", "a 5\nA 6", "Straße 5\nSTRASSE 6", "Netz 2 5\n7\n8",
        "\n".join("N%d %d" % (i, i + 1) for i in range(12)), "\n".join("N%d %d" % (i, i + 1) for i in range(13)),
        "SehrLangerName12 5", "SehrLangerName123 5", "\U0001f600" * 16 + " 5", "\U0001f600" * 17 + " 5",
        "A 5\r\nB 6\rC 7\x1cD 8", "A 5 B 6", "² 5", "A ²", "A ٥", "A HOSTS 5", "A 5 HOSTS",
        "A 5 Host", "A: 5 host", "A:", ":5", "  A   B   5  ", "A 1.2.3", "A 1..2 5", "A -1 5", "A 5.5.5 6",
        "Klein 2\nGross: 100\n30 Hosts\nGleich 100", "A 2\nB 1\nC 3",
    ]
    zeilen = [[t, versuch(lambda t=t: [[n, str(b)] for n, b in fc.read_host_lines(t)])] for t in zeilen_faelle]

    def bedarf_info(r):
        felder = {}
        for k, v in r.items():
            if k in ("bedarfe",):
                felder[k] = [[n, str(b)] for n, b in v]
            elif k == "subnetze":
                felder[k] = [{a: gross(b) if isinstance(b, int) else b for a, b in s.items()} for s in v]
            elif k == "bloecke":
                felder[k] = [[str(a), b] for a, b in v]
            elif isinstance(v, int) and not isinstance(v, bool):
                felder[k] = str(v)
            else:
                felder[k] = v
        return felder

    bedarf_faelle = [("192.168.10.0/24", t) for t in zeilen_faelle]
    bedarf_faelle += [("192.168.10.0/24", "A 200\nB 60"), ("192.168.10.0/24", "A 127\nB 62\nC 30\nD 14\nE 6\nF 2"),
                      ("192.168.10.0/30", "A 3"), ("10.0.0.0/8", "4294967294\n4294967294"),
                      ("10.0.0.0/23", "Klein 2\nGross: 100\n30 Hosts\nGleich 100"), ("10.20.30.40/16", "A 5"),
                      ("0.0.0.0/0", "\n".join(str(2 ** (31 - i)) for i in range(12))), ("192.168.10.0/24", "A 254"),
                      ("192.168.10.0/24", "A 255"), ("192.168.10.0/24", "A 126\nB 126"), ("192.168.10.0/24", "A 126\nB 127"),
                      ("10.0.0.0/30", "A 1"), ("10.0.0.0/32", "A 1"), ("2001:db8::/32", "A 5"), ("10.0.0.0", "A 5"),
                      ("10.0.0.0/24", "A 1\nB 2\nC 3"), ("172.16.0.0/16", "\n".join("V%d %d" % (i, 1000 - 70 * i) for i in range(12))),
                      ("192.168.10.0/26", "Verwaltung 20\nVertrieb Nord 10\nGeschaeftsfuehrung 5")]
    bedarf = []
    for net, needs in bedarf_faelle:
        eintrag = {"net": net, "needs": needs}
        try:
            r = fc.subnet_by_hosts(net, needs, version=4)
        except fc.InputError as fehler:
            eintrag["fehler"] = str(fehler)
            bedarf.append(eintrag)
            continue
        eintrag["kennwerte"] = bedarf_info(r)
        eintrag["bericht"] = [fc.hosts_report(r), fc.hosts_report(r, schmal=True)]
        eintrag["rechenweg"] = [fc.hosts_explain(r), fc.hosts_explain(r, schmal=True)]
        bedarf.append(eintrag)

    daten = {
        "quelle": "fisi_core subnet_report, ipv6_details, ipv6_type, subnet_parse, subnet_count, split_network, "
                  "split_report, split_explain, read_host_lines, subnet_by_hosts, hosts_report, hosts_explain; "
                  "ipaddress ip_address/ip_network/ip_interface; app_gui SubnetCalc.calc (Python v0.63.2)",
        "python": sys.version.split()[0],
        "konstanten": {"IPV6_TYPES": [["%x" % b, n, t] for b, n, t in fc.IPV6_TYPES]},
        "adressen": adressen,
        "typen": typen,
        "anzahl": anzahl,
        "aufteilen": aufteilen,
        "zeilen": zeilen,
        "bedarf": bedarf,
    }
    with open(ziel, "w", encoding="utf-8", newline="\n") as handle:
        json.dump(daten, handle, ensure_ascii=False, indent=None, separators=(",", ":"))
        handle.write("\n")
    print("G25: %d Adressen, %d Typen, %d Aufteilungen, %d Bedarfsfaelle -> %s"
          % (len(adressen), len(typen), len(aufteilen), len(bedarf), ziel))
    return 0


if __name__ == "__main__":
    sys.exit(main())
