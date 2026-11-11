namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.22.0, Ansicht „Zusammenhänge“ (Weisung P12-92, Entwurf Meldung P12-60, Nico „Ja, so bauen“): je Ort ein
/// Kasten mit Reiter, Spalten von links nach rechts nach dem Tag, an dem der Ort ins Spiel kommt, Linien „erst nach“ grau
/// mit runden Ecken in den Gassen um die Kaesten herum, ohne Kreuzung. Nur Geometrie, ohne Oberflaeche (testbar); die
/// Hoehe der Texte misst die Oberflaeche (<see cref="Masse"/>).
/// <para>Regeln: Reihenfolge nach Tag, dann mehr Aufgaben zuerst, dann Name. Spalte 0 = Orte von Anfang an; danach beginnt eine neue Spalte, wenn ein Ort mehr als
/// <see cref="SpaltenTage"/> Tage nach dem ersten Ort der Spalte ins Spiel kommt. Zugeklappt eine Linie je Paar von Orten,
/// aufgeklappt bis zur einzelnen Aufgabe. Linien zwischen Orten beginnen rechts am linken Kasten (gleiche Spalte: beide
/// rechts), laufen in der Gasse rechts davon senkrecht und dann waagrecht zwischen den Kaesten hindurch zum Ziel; alle
/// Linien von derselben Stelle teilen sich eine Spur (wie ein Bus). Ein spaeter Kasten rutscht so weit nach unten, dass
/// seine Linien in der Luecke zwischen den Kaesten der Spalten davor laufen. Aufgeklappt stehen die Aufgaben eines Orts
/// als Baum („erst nach“ hat genau einen Vorgaenger): jede Aufgabe direkt unter ihrem Vorgaenger, Geschwister nach Tag;
/// Klammern innerhalb des Orts liegen auf der Seite ohne Linien zu anderen Orten, tiefere Klammern naeher am Kasten.
/// So schneiden sich Klammern nie. Findet die Pruefung trotzdem eine Kreuzung, laufen die Linien zu anderen Orten an den
/// Kasten statt an die Aufgabe.</para>
/// </summary>
public sealed class Zusammenhaenge
{
    /// <summary>Rechteck in Bildpunkten der Flaeche.</summary>
    public readonly record struct Flaeche(double X, double Y, double B, double H)
    {
        public double Rechts => X + B;
        public double Unten => Y + H;
        public double MitteY => Y + (H / 2);

        public Flaeche Verschoben(double dx, double dy) => this with { X = X + dx, Y = Y + dy };
    }

    /// <summary>Schriftart eines gemessenen Texts.</summary>
    public enum Schrift
    {
        Reiter,
        Titel,
        Klein,
        Zeile,
    }

    /// <summary>Hoehe eines Texts bei gegebener Breite (Umbruch) und Breite eines Texts in einer Zeile.</summary>
    public sealed record Masse(Func<string, double, Schrift, double> Hoehe, Func<string, Schrift, double> Breite);

    public sealed record Zeile(Spielgeschichte.Eintrag Aufgabe, Flaeche Ort, int Tiefe);

    public sealed record Kasten(string Ort, string Name, int Spalte, Flaeche Reiter, Flaeche Koerper, Flaeche Kopf, Flaeche Knopf,
        IReadOnlyList<Zeile> Zeilen, bool Offen, int Zahl, long? FreiAb)
    {
        public Flaeche Aussen => new(Koerper.X, Reiter.Y, Koerper.B, Koerper.Unten - Reiter.Y);
    }

    /// <summary>Eine Linie: Gruppe (gleiche Spur), Enden (Kennung eines Orts oder einer Aufgabe), Eckpunkte.</summary>
    public sealed record Linie(string Gruppe, string A, string B, IReadOnlyList<(double X, double Y)> Punkte, bool Klammer);

    public const int SpaltenTage = 14;
    public const double Rand = 24;
    public const double BreiteZu = 280;
    public const double BreiteOffen = 380;
    public const double Innen = 14;
    public const double SymbolBreite = 28;
    public const double TagBreite = 56;
    public const double ZeilenAbstand = 6;
    public const double KastenAbstand = 36;
    public const double Spur = 12;
    public const double GasseMindest = 72;
    public const double Abstand = 12;

    public IReadOnlyList<Kasten> Kaesten { get; }
    public IReadOnlyList<Linie> Linien { get; }
    public double Breite { get; }
    public double Hoehe { get; }

    /// <summary>Laufen die Linien zu anderen Orten an die einzelne Aufgabe (true) oder an den Kasten (false)?</summary>
    public bool AnAufgaben { get; }

    private Zusammenhaenge(List<Kasten> kaesten, List<Linie> linien, bool anAufgaben)
    {
        Kaesten = kaesten;
        Linien = linien;
        AnAufgaben = anAufgaben;
        var rechts = kaesten.Select(k => k.Koerper.Rechts).Concat(linien.SelectMany(l => l.Punkte).Select(p => p.X)).DefaultIfEmpty(0).Max();
        var unten = kaesten.Select(k => k.Koerper.Unten).Concat(linien.SelectMany(l => l.Punkte).Select(p => p.Y)).DefaultIfEmpty(0).Max();
        Breite = rechts + Rand;
        Hoehe = unten + Rand;
    }

    /// <summary>
    /// Nachbesserung Q1 (Weisung P12-187): Kaesten je Spalte in der Ansicht „je Questgeber“ mindestens (die Ansicht nimmt
    /// mehr, wenn die Spalten sonst nicht ins Fenster passen). Fast alle Personen sind ab Tag 1 da und standen vorher in EINER
    /// langen Spalte, die „Alles zeigen“ unlesbar klein machte.
    /// </summary>
    public const int QuestgeberJeSpalte = 4;

    /// <summary>
    /// Nachbesserung Q1 (Weisung P12-187): Breite eines zugeklappten Kastens „je Questgeber“ (Namen der Personen sind kurz;
    /// mit 280 war die Ansicht bei Zoom 1 breiter als das Fenster).
    /// </summary>
    public const double QuestgeberBreiteZu = 220;

    /// <summary>
    /// Spalte je Ort nach der Regel oben (Reihenfolge nach Tag, dann Name). <paramref name="hoechstens"/> (nur „je
    /// Questgeber“): eine Spalte nimmt hoechstens so viele Kaesten, dann beginnt eine neue (gleicher Tag).
    /// </summary>
    public static List<(string Ort, int Spalte)> Spalten(Spielgeschichte g, Func<string, string> name, int? hoechstens = null)
    {
        var orte = g.Zahlen().Keys.OrderBy(o => g.FreiAbTag(o) ?? long.MaxValue).ThenByDescending(g.Zahl).ThenBy(name, StringComparer.Ordinal).ToList();
        var liste = new List<(string, int)>();
        var spalte = -1;
        long? erster = null;
        var inSpalte = 0;
        foreach (var o in orte)
        {
            var tag = g.FreiAbTag(o) ?? long.MaxValue;
            if (spalte < 0 || (erster == 1 && tag > 1) || (erster != 1 && tag > (erster ?? 0) + SpaltenTage)
                || (hoechstens is { } h && inSpalte >= h))
            {
                spalte++;
                erster = tag;
                inSpalte = 0;
            }
            inSpalte++;
            liste.Add((o, spalte));
        }
        return liste;
    }

    private sealed class Bau
    {
        public required string Ort;
        public required string Name;
        public required int Spalte;
        public required bool Offen;
        public required int Zahl;
        public required long? FreiAb;
        public double B;
        public double ReiterB;
        public double ReiterH;
        public double KopfH;
        public double KnopfH;
        public double KoerperH;
        public List<(Spielgeschichte.Eintrag E, double Y, double H, int Tiefe)> Zeilen = [];
        public double Y;
        public double X;
        public bool AnAufgabe;
        public bool KlammernRechts = true;
        public int Tiefen;

        public double KoerperY => Y + ReiterH - 1;
        public double KopfMitte => KoerperY + Innen + (KopfH / 2);
        public double Unten => KoerperY + KoerperH;
    }

    /// <summary>
    /// Ansicht bauen. <paramref name="offen"/> = aufgeklappte Orte. <paramref name="altFuerTests"/> = Gegenprobe: Linien
    /// gerade durch die Spalten wie ein einfacher Plan ohne Gassen (der Start-Test muss dort Kreuzungen finden).
    /// </summary>
    public static Zusammenhaenge Bauen(Spielgeschichte g, Func<string, string> name, IReadOnlySet<string> offen, Masse m, bool altFuerTests = false,
        int? jeSpalte = null, double breiteZu = BreiteZu)
    {
        var mit = Versuch(g, name, offen, m, true, altFuerTests, jeSpalte, breiteZu);
        if (altFuerTests || (mit.Kreuzungen().Count == 0 && mit.DurchKaesten().Count == 0) || !offen.Any())
        {
            return mit;
        }
        var ohne = Versuch(g, name, offen, m, false, false, jeSpalte, breiteZu);
        return ohne.Kreuzungen().Count + ohne.DurchKaesten().Count < mit.Kreuzungen().Count + mit.DurchKaesten().Count ? ohne : mit;
    }

    private static Zusammenhaenge Versuch(Spielgeschichte g, Func<string, string> name, IReadOnlySet<string> offen, Masse m, bool anAufgaben, bool alt,
        int? jeSpalte, double breiteZu)
    {
        var spalten = Spalten(g, name, jeSpalte);
        var orte = spalten.Select(s => s.Ort).ToHashSet(StringComparer.Ordinal);
        var spalteVon = spalten.ToDictionary(s => s.Ort, s => s.Spalte, StringComparer.Ordinal);
        var jeId = g.Alle.GroupBy(e => e.Id).ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var nummer = g.Aufgaben.Select((e, i) => (e.Id, i)).ToDictionary(x => x.Id, x => x.i, StringComparer.Ordinal);
        // Kanten "erst nach": Vorgaenger -> Aufgabe, beide an einem Ort der Ansicht
        var kanten = new List<(Spielgeschichte.Eintrag Vor, Spielgeschichte.Eintrag Nach)>();
        foreach (var e in g.Aufgaben)
        {
            if (e.Nach is { } n && jeId.TryGetValue(n, out var v) && v.Ort is { } vo && e.Ort is { } eo && orte.Contains(vo) && orte.Contains(eo))
            {
                kanten.Add((v, e));
            }
        }

        // ---- Kaesten: Groessen und Zeilen ----------------------------------------------------------------
        var bauten = new List<Bau>();
        foreach (var (ort, spalte) in spalten)
        {
            var b = new Bau
            {
                Ort = ort, Name = name(ort), Spalte = spalte, Offen = offen.Contains(ort), Zahl = g.Zahl(ort), FreiAb = g.FreiAbTag(ort),
            };
            b.B = b.Offen ? BreiteOffen : breiteZu;
            var reiterText = m.Breite(b.Name, Schrift.Reiter);
            b.ReiterB = Math.Min(b.B, reiterText + 32);
            b.ReiterH = Math.Max(30, m.Hoehe(b.Name, b.ReiterB - 32, Schrift.Reiter) + 12);
            var textB = b.B - (2 * Innen) - SymbolBreite - 8;
            b.KopfH = Math.Max(SymbolBreite, m.Hoehe(KopfTitel(b.Zahl), textB, Schrift.Titel) + m.Hoehe(KopfZeile(b.FreiAb), textB, Schrift.Klein));
            b.KnopfH = Math.Max(30, m.Hoehe(Texte.GeschichteAlleZeigen, b.B - (2 * Innen), Schrift.Klein) + 10);
            var y = Innen + b.KopfH + 10;
            if (b.Offen)
            {
                foreach (var (e, tiefe) in Baum(g.Aufgaben.Where(a => a.Ort == ort).ToList(), kanten, nummer))
                {
                    var titelB = b.B - (2 * Innen) - 16 - SymbolBreite - TagBreite;
                    var h = Math.Max(28, m.Hoehe(e.Titel, titelB, Schrift.Zeile) + 10);
                    b.Zeilen.Add((e, y, h, tiefe));
                    y += h + ZeilenAbstand;
                }
                y += 4;
            }
            b.KoerperH = y + b.KnopfH + Innen - 6;
            bauten.Add(b);
        }
        var jeOrt = bauten.ToDictionary(b => b.Ort, StringComparer.Ordinal);
        var zeileVon = bauten.SelectMany(b => b.Zeilen.Select(z => (z.E.Id, b))).ToDictionary(x => x.Id, x => x.b, StringComparer.Ordinal);

        // ---- Linien zwischen Orten: Seiten, Ende an Aufgabe oder Kasten ------------------------------------
        static bool Rechts(int spalteHier, int spalteDort) => spalteDort >= spalteHier;
        var quer = kanten.Where(k => k.Vor.Ort != k.Nach.Ort).ToList();
        foreach (var b in bauten.Where(b => b.Offen))
        {
            var seiten = quer.Where(k => k.Vor.Ort == b.Ort || k.Nach.Ort == b.Ort)
                .Select(k => Rechts(b.Spalte, spalteVon[k.Vor.Ort == b.Ort ? k.Nach.Ort! : k.Vor.Ort!])).Distinct().ToList();
            b.AnAufgabe = anAufgaben && seiten.Count <= 1;
            b.KlammernRechts = seiten.Count != 1 || !seiten[0];
            b.Tiefen = b.Zeilen.Count == 0 ? 0 : b.Zeilen.Where(z => kanten.Any(k => k.Vor.Id == z.E.Id && k.Nach.Ort == b.Ort)).Select(z => z.Tiefe + 1).DefaultIfEmpty(0).Max();
        }
        // Enden: (Kennung, Ort) – Aufgabe nur, wenn ihr Kasten offen ist und an Aufgaben haengt
        (string Id, string Ort) Ende(Spielgeschichte.Eintrag e) => jeOrt[e.Ort!] is { Offen: true, AnAufgabe: true } ? (e.Id, e.Ort!) : (e.Ort!, e.Ort!);
        var paare = new List<((string Id, string Ort) L, (string Id, string Ort) R)>();
        foreach (var (vor, nach) in quer)
        {
            var a = Ende(vor);
            var c = Ende(nach);
            // links = fruehere Spalte (gleiche Spalte: der obere Ort zuerst in der Reihenfolge)
            var links = spalteVon[a.Ort] < spalteVon[c.Ort] || (spalteVon[a.Ort] == spalteVon[c.Ort] && bauten.IndexOf(jeOrt[a.Ort]) < bauten.IndexOf(jeOrt[c.Ort]));
            var paar = links ? (a, c) : (c, a);
            if (!paare.Contains(paar))
            {
                paare.Add(paar);
            }
        }
        var gruppen = paare.GroupBy(p => p.L).Select(x => (Ende: x.Key, Ziele: x.Select(p => p.R).ToList())).ToList();

        // ---- Spalten: Breite, Gassen ----------------------------------------------------------------------
        var anzahl = bauten.Count == 0 ? 0 : bauten.Max(b => b.Spalte) + 1;
        var spaltenB = Enumerable.Range(0, anzahl).Select(s => bauten.Where(b => b.Spalte == s).Max(b => b.B)).ToArray();
        int KlammerSpuren(int s, bool rechts) => bauten.Where(b => b.Spalte == s && b.Offen && b.KlammernRechts == rechts).Select(b => b.Tiefen).DefaultIfEmpty(0).Max();
        var gruppenJeSpalte = Enumerable.Range(0, anzahl).Select(s => gruppen.Count(x => jeOrt[x.Ende.Ort].Spalte == s)).ToArray();
        var spaltenX = new double[anzahl];
        var x0 = Rand + (anzahl > 0 && KlammerSpuren(0, false) > 0 ? Abstand + (KlammerSpuren(0, false) * Spur) : 0);
        for (var s = 0; s < anzahl; s++)
        {
            spaltenX[s] = x0;
            var gasse = Abstand + (KlammerSpuren(s, true) * Spur) + (gruppenJeSpalte[s] > 0 ? Abstand + (gruppenJeSpalte[s] * Spur) : 0)
                + (s + 1 < anzahl ? KlammerSpuren(s + 1, false) * Spur : 0) + 2 * Abstand;
            x0 += spaltenB[s] + Math.Max(GasseMindest, gasse);
        }
        foreach (var b in bauten)
        {
            b.X = spaltenX[b.Spalte];
        }

        // ---- Hoehe: von oben nach unten je Spalte, spaetere Kaesten rutschen in die Luecken ----------------
        static double Lage(Bau b, string id)
        {
            if (id == b.Ort)
            {
                return b.KopfMitte - b.Y;
            }
            var z = b.Zeilen.First(z => z.E.Id == id);
            return b.KoerperY + z.Y + (z.H / 2) - b.Y;
        }
        for (var s = 0; s < anzahl; s++)
        {
            var unten = Rand;
            foreach (var b in bauten.Where(b => b.Spalte == s))
            {
                b.Y = unten;
                // Enden auf der linken Seite dieses Kastens, die aus einer frueheren Spalte kommen
                var links = gruppen.SelectMany(x => x.Ziele.Where(z => z.Ort == b.Ort).Select(z => (Lage(b, z.Id), jeOrt[x.Ende.Ort].Spalte)))
                    .Where(x => x.Item2 < s).ToList();
                for (var runde = 0; runde < 200 && links.Count > 0; runde++)
                {
                    var schieben = 0.0;
                    foreach (var (lage, von) in links)
                    {
                        var y = b.Y + lage;
                        foreach (var k in bauten.Where(k => k.Spalte > von && k.Spalte < s))
                        {
                            if (y > k.Y - Abstand && y < k.Unten + Abstand)
                            {
                                schieben = Math.Max(schieben, k.Unten + Abstand + 1 - y);
                            }
                        }
                    }
                    if (schieben <= 0)
                    {
                        break;
                    }
                    b.Y += schieben;
                }
                unten = b.Unten + KastenAbstand;
            }
        }

        // ---- Kaesten fertig -------------------------------------------------------------------------------
        var kaesten = bauten.Select(b => new Kasten(b.Ort, b.Name, b.Spalte,
            new Flaeche(b.X, b.Y, b.ReiterB, b.ReiterH),
            new Flaeche(b.X, b.KoerperY, b.B, b.KoerperH),
            new Flaeche(b.X + Innen, b.KoerperY + Innen, b.B - (2 * Innen), b.KopfH),
            new Flaeche(b.X + Innen, b.Unten - Innen - b.KnopfH + 6, b.B - (2 * Innen), b.KnopfH - 6),
            [.. b.Zeilen.Select(z => new Zeile(z.E, new Flaeche(b.X + Innen, b.KoerperY + z.Y, b.B - (2 * Innen), z.H), z.Tiefe))],
            b.Offen, b.Zahl, b.FreiAb)).ToList();

        // ---- Linien ---------------------------------------------------------------------------------------
        (double X, double Y) Punkt((string Id, string Ort) e, bool rechts)
        {
            var b = jeOrt[e.Ort];
            return (rechts ? b.X + b.B : b.X, b.Y + Lage(b, e.Id));
        }
        var linien = new List<Linie>();
        // Klammern innerhalb eines Orts (Baum: Vorgaenger hat die Spur seiner Tiefe, tiefere naeher am Kasten)
        foreach (var b in bauten.Where(b => b.Offen))
        {
            foreach (var (vor, nach) in kanten.Where(k => k.Vor.Ort == b.Ort && k.Nach.Ort == b.Ort))
            {
                var tiefe = b.Zeilen.First(z => z.E.Id == vor.Id).Tiefe;
                var stufe = b.Tiefen - tiefe;
                var x = b.KlammernRechts ? spaltenX[b.Spalte] + spaltenB[b.Spalte] + Abstand + ((stufe - 1) * Spur) : b.X - Abstand - ((stufe - 1) * Spur);
                var p = Punkt((vor.Id, b.Ort), b.KlammernRechts);
                var q = Punkt((nach.Id, b.Ort), b.KlammernRechts);
                linien.Add(new Linie("klammer:" + vor.Id, vor.Id, nach.Id, [p, (x, p.Y), (x, q.Y), q], true));
            }
        }
        // Linien zwischen Orten: je Gruppe eine Spur in der Gasse rechts von ihrer Spalte
        var reihenfolge = new Dictionary<int, List<int>>();
        for (var i = 0; i < gruppen.Count; i++)
        {
            var s = jeOrt[gruppen[i].Ende.Ort].Spalte;
            (reihenfolge.TryGetValue(s, out var l) ? l : reihenfolge[s] = []).Add(i);
        }
        List<Linie> Quer()
        {
            var liste = new List<Linie>();
            foreach (var (s, ordnung) in reihenfolge)
            {
                for (var n = 0; n < ordnung.Count; n++)
                {
                    var (ende, ziele) = gruppen[ordnung[n]];
                    var p = Punkt(ende, true);
                    var x = spaltenX[s] + spaltenB[s] + Abstand + (KlammerSpuren(s, true) * Spur) + Abstand + (n * Spur);
                    foreach (var z in ziele)
                    {
                        var gleich = jeOrt[z.Ort].Spalte == s;
                        var q = Punkt(z, gleich);
                        if (alt)
                        {
                            // Gegenprobe: gerade waagrecht bis kurz vor die Zielspalte, dann hoch oder runter
                            var xa = gleich ? x : spaltenX[jeOrt[z.Ort].Spalte] - Abstand;
                            liste.Add(new Linie("alt:" + ende.Id + ":" + z.Id, ende.Id, z.Id, [p, (xa, p.Y), (xa, q.Y), q], false));
                            continue;
                        }
                        liste.Add(new Linie("quer:" + ende.Id, ende.Id, z.Id, [p, (x, p.Y), (x, q.Y), q], false));
                    }
                }
            }
            return liste;
        }
        var ergebnis = new Zusammenhaenge(kaesten, [.. linien, .. Quer()], anAufgaben);
        if (!alt)
        {
            // Spuren je Gasse ordnen: einfache Reihenfolgen, dann Nachbarn tauschen, solange es weniger Kreuzungen gibt
            int Kosten(Zusammenhaenge z) => z.Kreuzungen().Count;
            var beste = Kosten(ergebnis);
            foreach (var (s, ordnung) in reihenfolge.Where(r => r.Value.Count > 1).ToList())
            {
                double PortY(int i) => Punkt(gruppen[i].Ende, true).Y;
                double Spanne(int i) => gruppen[i].Ziele.Select(z => Punkt(z, false).Y).Append(PortY(i)).Max() - gruppen[i].Ziele.Select(z => Punkt(z, false).Y).Append(PortY(i)).Min();
                foreach (var kandidat in new[]
                {
                    ordnung.OrderBy(PortY).ToList(), ordnung.OrderByDescending(PortY).ToList(),
                    ordnung.OrderBy(Spanne).ToList(), ordnung.OrderByDescending(Spanne).ToList(),
                })
                {
                    var vorher = reihenfolge[s];
                    reihenfolge[s] = kandidat;
                    var z = new Zusammenhaenge(kaesten, [.. linien, .. Quer()], anAufgaben);
                    var k = Kosten(z);
                    if (k < beste)
                    {
                        beste = k;
                        ergebnis = z;
                    }
                    else
                    {
                        reihenfolge[s] = vorher;
                    }
                }
                for (var runde = 0; runde < 20 && beste > 0; runde++)
                {
                    var besser = false;
                    for (var i = 0; i + 1 < reihenfolge[s].Count; i++)
                    {
                        var neu = reihenfolge[s].ToList();
                        (neu[i], neu[i + 1]) = (neu[i + 1], neu[i]);
                        var vorher = reihenfolge[s];
                        reihenfolge[s] = neu;
                        var z = new Zusammenhaenge(kaesten, [.. linien, .. Quer()], anAufgaben);
                        var k = Kosten(z);
                        if (k < beste)
                        {
                            beste = k;
                            ergebnis = z;
                            besser = true;
                        }
                        else
                        {
                            reihenfolge[s] = vorher;
                        }
                    }
                    if (!besser)
                    {
                        break;
                    }
                }
            }
        }
        return ergebnis;
    }

    /// <summary>Kopfzeile eines Kastens.</summary>
    public static string KopfTitel(int zahl) => zahl == 1 ? "1 Aufgabe" : zahl + " Aufgaben";

    public static string KopfZeile(long? freiAb) => freiAb is { } t ? "frei ab Tag " + t : "frei im Lauf des Spiels";

    /// <summary>Aufgaben eines Orts als Baum: Wurzeln nach Tag, jede Aufgabe direkt unter ihrem Vorgaenger.</summary>
    private static List<(Spielgeschichte.Eintrag E, int Tiefe)> Baum(List<Spielgeschichte.Eintrag> aufgaben,
        List<(Spielgeschichte.Eintrag Vor, Spielgeschichte.Eintrag Nach)> kanten, Dictionary<string, int> nummer)
    {
        var hier = aufgaben.Select(a => a.Id).ToHashSet(StringComparer.Ordinal);
        var kinder = kanten.Where(k => hier.Contains(k.Vor.Id) && hier.Contains(k.Nach.Id)).GroupBy(k => k.Vor.Id)
            .ToDictionary(x => x.Key, x => x.Select(k => k.Nach).OrderBy(e => e.AbTag).ThenBy(e => nummer[e.Id]).ToList(), StringComparer.Ordinal);
        var mitVorgaenger = kanten.Where(k => hier.Contains(k.Vor.Id) && hier.Contains(k.Nach.Id)).Select(k => k.Nach.Id).ToHashSet(StringComparer.Ordinal);
        var liste = new List<(Spielgeschichte.Eintrag, int)>();
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        void Hinzu(Spielgeschichte.Eintrag e, int tiefe)
        {
            if (!gesehen.Add(e.Id))
            {
                return;
            }
            liste.Add((e, tiefe));
            foreach (var k in kinder.TryGetValue(e.Id, out var l) ? l : [])
            {
                Hinzu(k, tiefe + 1);
            }
        }
        foreach (var w in aufgaben.Where(a => !mitVorgaenger.Contains(a.Id)).OrderBy(e => e.AbTag).ThenBy(e => nummer[e.Id]))
        {
            Hinzu(w, 0);
        }
        // ein Kreis (gibt es in den Daten nicht) faellt nicht weg: Rest nach Tag
        foreach (var e in aufgaben.Where(a => !gesehen.Contains(a.Id)).OrderBy(e => e.AbTag))
        {
            Hinzu(e, 0);
        }
        return liste;
    }

    // ---- Pruefungen (Start-Test und Tests) -----------------------------------------------------------------

    private const double Eps = 0.5;

    private static IEnumerable<((double X, double Y) A, (double X, double Y) B)> Strecken(Linie l)
    {
        for (var i = 0; i + 1 < l.Punkte.Count; i++)
        {
            if (Math.Abs(l.Punkte[i].X - l.Punkte[i + 1].X) > 1e-9 || Math.Abs(l.Punkte[i].Y - l.Punkte[i + 1].Y) > 1e-9)
            {
                yield return (l.Punkte[i], l.Punkte[i + 1]);
            }
        }
    }

    private static bool Waag(((double X, double Y) A, (double X, double Y) B) s) => Math.Abs(s.A.Y - s.B.Y) < 1e-9;

    /// <summary>
    /// Kreuzungen zweier Linien: senkrechte Strecken, die sich im Inneren beider schneiden, oder Strecken, die
    /// aufeinander laufen (erlaubt nur in derselben Gruppe oder ab einem gemeinsamen Ende).
    /// </summary>
    public List<string> Kreuzungen()
    {
        var funde = new List<string>();
        var strecken = Linien.Select(l => Strecken(l).ToList()).ToList();
        for (var i = 0; i < Linien.Count; i++)
        {
            for (var j = i + 1; j < Linien.Count; j++)
            {
                var li = Linien[i];
                var lj = Linien[j];
                var gleicheGruppe = li.Gruppe == lj.Gruppe;
                var enden = new[] { li.Punkte[0], li.Punkte[^1] }.Where(p => lj.Punkte[0] == p || lj.Punkte[^1] == p).ToList();
                foreach (var s in strecken[i])
                {
                    foreach (var t in strecken[j])
                    {
                        if (Waag(s) != Waag(t))
                        {
                            var (h, v) = Waag(s) ? (s, t) : (t, s);
                            var x = v.A.X;
                            var y = h.A.Y;
                            var innenH = x > Math.Min(h.A.X, h.B.X) + Eps && x < Math.Max(h.A.X, h.B.X) - Eps;
                            var innenV = y > Math.Min(v.A.Y, v.B.Y) + Eps && y < Math.Max(v.A.Y, v.B.Y) - Eps;
                            if (innenH && innenV)
                            {
                                funde.Add(li.A + "–" + li.B + " kreuzt " + lj.A + "–" + lj.B);
                            }
                        }
                        else if (!gleicheGruppe)
                        {
                            var waag = Waag(s);
                            var gleicheLinie = waag ? Math.Abs(s.A.Y - t.A.Y) < Eps : Math.Abs(s.A.X - t.A.X) < Eps;
                            if (!gleicheLinie)
                            {
                                continue;
                            }
                            double Lo((double X, double Y) p) => waag ? p.X : p.Y;
                            var von = Math.Max(Math.Min(Lo(s.A), Lo(s.B)), Math.Min(Lo(t.A), Lo(t.B)));
                            var bis = Math.Min(Math.Max(Lo(s.A), Lo(s.B)), Math.Max(Lo(t.A), Lo(t.B)));
                            if (bis - von > Eps && !enden.Any(e => (waag ? Math.Abs(e.Y - s.A.Y) < Eps : Math.Abs(e.X - s.A.X) < Eps)
                                && (Math.Abs(Lo(e) - von) < Eps || Math.Abs(Lo(e) - bis) < Eps)))
                            {
                                funde.Add(li.A + "–" + li.B + " laeuft auf " + lj.A + "–" + lj.B);
                            }
                        }
                    }
                }
            }
        }
        return funde;
    }

    /// <summary>Linien, die durch einen Kasten (Reiter oder Koerper) laufen.</summary>
    public List<string> DurchKaesten()
    {
        var funde = new List<string>();
        foreach (var l in Linien)
        {
            foreach (var (a, b) in Strecken(l))
            {
                foreach (var k in Kaesten)
                {
                    foreach (var f in new[] { k.Reiter, k.Koerper })
                    {
                        var (x1, x2) = (Math.Min(a.X, b.X), Math.Max(a.X, b.X));
                        var (y1, y2) = (Math.Min(a.Y, b.Y), Math.Max(a.Y, b.Y));
                        if (x2 > f.X + Eps && x1 < f.Rechts - Eps && y2 > f.Y + Eps && y1 < f.Unten - Eps)
                        {
                            funde.Add(l.A + "–" + l.B + " durch " + k.Name);
                        }
                    }
                }
            }
        }
        return funde;
    }
}
