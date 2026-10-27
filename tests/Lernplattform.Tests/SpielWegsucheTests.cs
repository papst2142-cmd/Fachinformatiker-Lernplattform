using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P7-16: die schnellere Wegsuche (Feld statt Menge, Puffer wiederverwendet) findet auf allen
/// Orten genau dieselben Wege wie vorher. Die alte Fassung steht hier unveraendert als Bezug
/// (Stand main 6c0930d): Stadt (freies Gehen, Laufraster) und Innenraeume (walk_path, G48).
/// </summary>
public sealed class SpielWegsucheTests
{
    private static readonly (int Dx, int Dy)[] Nachbarn =
        [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    private static readonly (int, int)[] Richtungen = Nachbarn.Select(n => (n.Dx, n.Dy)).ToArray();

    /// <summary>Alte Wegsuche der Stadt (HashSet, Dictionary), unveraendert.</summary>
    private sealed class Alt(HashSet<(int, int)> frei, double schritt)
    {
        private readonly HashSet<(int, int)> _frei = frei;

        public double Schritt { get; } = schritt;

        public (int Spalte, int Zeile) Zelle(double x, double y) => ((int)(x / Schritt), (int)(y / Schritt));

        public (double X, double Y) Mitte((int Spalte, int Zeile) zelle) => ((zelle.Spalte + 0.5) * Schritt, (zelle.Zeile + 0.5) * Schritt);

        public bool KannStehen(double x, double y) => _frei.Contains(Zelle(x, y));

        public (int Spalte, int Zeile)? NaechsteFreie(double x, double y)
        {
            var start = Zelle(x, y);
            if (_frei.Contains(start))
            {
                return start;
            }
            (int, int)? beste = null;
            long besteWeite = long.MaxValue;
            foreach (var zelle in _frei)
            {
                long dx = zelle.Item1 - start.Spalte, dy = zelle.Item2 - start.Zeile;
                var weite = (dx * dx) + (dy * dy);
                if (weite < besteWeite || (weite == besteWeite && beste is { } b && zelle.CompareTo(b) < 0))
                {
                    beste = zelle;
                    besteWeite = weite;
                }
            }
            return beste;
        }

        public bool StreckeFrei((double X, double Y) a, (double X, double Y) b)
        {
            var schritte = (int)(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)) / (Schritt / 3.0)) + 1;
            for (var i = 0; i <= schritte; i++)
            {
                var t = i / (double)schritte;
                if (!_frei.Contains(Zelle(a.X + ((b.X - a.X) * t), a.Y + ((b.Y - a.Y) * t))))
                {
                    return false;
                }
            }
            return true;
        }

        public IReadOnlyList<(double X, double Y)> Weg((double X, double Y) start, (double X, double Y) ziel)
        {
            var beginn = NaechsteFreie(start.X, start.Y);
            var zielZelle = NaechsteFreie(ziel.X, ziel.Y);
            if (beginn is not { } von || zielZelle is not { } nach || von == nach)
            {
                return [];
            }
            var vorher = new Dictionary<(int, int), (int, int)?> { [von] = null };
            var reihe = new List<(int, int)> { von };
            (int, int)? gefunden = null;
            for (var i = 0; i < reihe.Count; i++)   // Breitensuche (die Liste waechst mit)
            {
                var zelle = reihe[i];
                if (zelle == nach)
                {
                    gefunden = zelle;
                    break;
                }
                foreach (var (dx, dy) in Nachbarn)
                {
                    var naechste = (zelle.Item1 + dx, zelle.Item2 + dy);
                    if (!_frei.Contains(naechste) || vorher.ContainsKey(naechste))
                    {
                        continue;
                    }
                    // diagonal nur, wenn beide Nachbarn frei sind (keine Ecken schneiden)
                    if (dx != 0 && dy != 0 && (!_frei.Contains((zelle.Item1 + dx, zelle.Item2)) || !_frei.Contains((zelle.Item1, zelle.Item2 + dy))))
                    {
                        continue;
                    }
                    vorher[naechste] = zelle;
                    reihe.Add(naechste);
                }
            }
            if (gefunden is null)
            {
                return [];
            }
            var zellen = new List<(int, int)>();
            for (var z = gefunden; z is { } jetzt; z = vorher[jetzt])
            {
                zellen.Add(jetzt);
            }
            zellen.Reverse();
            var punkte = zellen.Select(Mitte).ToList();
            if (KannStehen(ziel.X, ziel.Y))
            {
                punkte[^1] = ziel;
            }
            // Glaetten: Zwischenpunkte weglassen, solange die gerade Strecke frei ist
            var glatt = new List<(double X, double Y)> { punkte[0] };
            var index = 0;
            while (index < punkte.Count - 1)
            {
                var weiter = punkte.Count - 1;
                while (weiter > index + 1 && !StreckeFrei(punkte[index], punkte[weiter]))
                {
                    weiter--;
                }
                glatt.Add(punkte[weiter]);
                index = weiter;
            }
            return glatt.Skip(1).ToList();
        }
    }

    private static List<(double, double)> WegAlt((double, double) start, (double, double) ziel, double reichweite, LaufOrt ort)
    {
        var frei = ort.Frei;
        var anfang = Wege.NaechsteFrei(start.Item1, start.Item2, frei);
        var ziele = new HashSet<(long, long)>();
        if (reichweite > 0)
        {
            var grenze = reichweite * reichweite;
            foreach (var zelle in frei)
            {
                var m = Wege.Mitte(zelle);
                if (PythonPow.Pow(m.Item1 - ziel.Item1, 2) + PythonPow.Pow(m.Item2 - ziel.Item2, 2) <= grenze)
                {
                    ziele.Add(zelle);
                }
            }
        }
        else if (Wege.NaechsteFrei(ziel.Item1, ziel.Item2, frei) is { } zielZelle)
        {
            ziele.Add(zielZelle);
        }
        if (anfang is not { } beginn || ziele.Count == 0 || ziele.Contains(beginn))
        {
            return [];
        }
        var vorher = new Dictionary<(long, long), (long, long)?> { [beginn] = null };
        var schlange = new List<(long, long)> { beginn };
        (long, long)? gefunden = null;
        for (var k = 0; k < schlange.Count; k++)
        {
            var zelle = schlange[k];
            if (ziele.Contains(zelle))
            {
                gefunden = zelle;
                break;
            }
            foreach (var (dx, dy) in Richtungen)
            {
                var naechste = (zelle.Item1 + dx, zelle.Item2 + dy);
                if (frei.Contains(naechste) && !vorher.ContainsKey(naechste))
                {
                    // diagonal nur, wenn beide Nachbarn frei sind (keine Ecken schneiden)
                    if (dx != 0 && dy != 0 && (!frei.Contains((zelle.Item1 + dx, zelle.Item2)) || !frei.Contains((zelle.Item1, zelle.Item2 + dy))))
                    {
                        continue;
                    }
                    vorher[naechste] = zelle;
                    schlange.Add(naechste);
                }
            }
        }
        if (gefunden is null)
        {
            return [];
        }
        var zellen = new List<(long, long)>();
        for (var z = gefunden; z is { } aktuell; z = vorher[aktuell])
        {
            zellen.Add(aktuell);
        }
        zellen.Reverse();
        var punkte = zellen.Select(Wege.Mitte).ToList();
        if (reichweite <= 0 && Wege.KannStehen(ziel.Item1, ziel.Item2, ort))
        {
            punkte[^1] = ziel;
        }
        // Glaetten: Zwischenpunkte weglassen, solange die gerade Strecke frei ist
        var glatt = new List<(double, double)> { punkte[0] };
        var index = 0;
        while (index < punkte.Count - 1)
        {
            var weiter = punkte.Count - 1;
            while (weiter > index + 1 && !Wege.StreckeFrei(punkte[index], punkte[weiter], frei))
            {
                weiter--;
            }
            glatt.Add(punkte[weiter]);
            index = weiter;
        }
        return glatt.Skip(1).ToList();
    }

    /// <summary>Altes _base_grid (Stand main dfadfd8): jede Zelle gegen jedes Rechteck, unveraendert.</summary>
    private static PyMenge BasisRasterAlt(PyObjekt gebaeude)
    {
        var spalten = (long)(Py.F(gebaeude["breite"]) / Wege.Schritt);
        var zeilen = (long)(Py.F(gebaeude["hoehe"]) / Wege.Schritt);
        var rechtecke = Wege.Hindernisse(gebaeude);
        var grenze = Wege.Koerper * Wege.Koerper;
        var frei = new PyMenge();
        for (long spalte = 0; spalte < spalten; spalte++)
        {
            var cx = (spalte + 0.5) * Wege.Schritt;
            for (long zeile = 0; zeile < zeilen; zeile++)
            {
                var cy = (zeile + 0.5) * Wege.Schritt;
                var gesperrt = false;
                foreach (var (x1, y1, x2, y2) in rechtecke)
                {
                    var dx = Math.Max(Math.Max(x1 - cx, 0), cx - x2);
                    var dy = Math.Max(Math.Max(y1 - cy, 0), cy - y2);
                    if (dx * dx + dy * dy < grenze)
                    {
                        gesperrt = true;
                        break;
                    }
                }
                if (!gesperrt)
                {
                    frei.Add((spalte, zeile));
                }
            }
        }
        return frei;
    }

    /// <summary>Alter Tupel-Hash (Stand main dfadfd8) fuer hash((a, b)).</summary>
    private static long HashAlt((long, long) zelle)
    {
        const ulong Prime1 = 11400714785074694791UL, Prime2 = 14029467366897019727UL, Prime5 = 2870177450012600261UL;
        var acc = Prime5;
        foreach (var teil in new[] { zelle.Item1, zelle.Item2 })
        {
            acc += unchecked((ulong)(teil == -1 ? -2 : teil) * Prime2);
            acc = (acc << 31) | (acc >> 33);
            acc = unchecked(acc * Prime1);
        }
        acc += 2UL ^ (Prime5 ^ 3527539UL);
        return acc == ulong.MaxValue ? 1546275796 : unchecked((long)acc);
    }

    /// <summary>
    /// Weisung P8-6 (109): das schnellere Raster der Innenraeume (Rechteck nur gegen nahe Zellen) hat dieselben
    /// Zellen in derselben Reihenfolge wie vorher, auch mit Moebeln an jeder Stelle des Grundrisses; der Hash
    /// ohne neues Feld je Aufruf ist derselbe.
    /// </summary>
    [Fact]
    public void InnenraeumeGleichesRaster()
    {
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt);
        ablauf.Stand.TageFertig = 200;
        var ansichten = new List<string> { "buero", "zuhause", "serverfarm" };
        ansichten.AddRange(SpielDaten.Kundenorte(SpielInhaltTests.Inhalt).Select(k => "kunde:" + Py.Str(k["id"])));
        ansichten.AddRange(["geschaeft:moebelhaus", "geschaeft:elektromarkt", "schalter:bank", "schalter:amt"]);
        var zufall = new Random(18);
        var verglichen = 0;
        foreach (var ansicht in ansichten)
        {
            var innen = Innenraum.Bauen(ansicht, ablauf.Stand, SpielInnenraumTests.Innenraeume.Value, null)!;
            Assert.Equal([.. BasisRasterAlt(innen.Gebaeude)], [.. Wege.BasisRaster(innen.Gebaeude)]);
            verglichen++;
            // dazu Grundrisse mit einem Moebelstueck an zufaelligen Stellen, auch am und ueber dem Rand
            for (var i = 0; i < 25; i++)
            {
                var gebaeude = (PyObjekt)Py.Kopie(innen.Gebaeude)!;
                var flur = (PyObjekt)gebaeude["flur"]!;
                var deko = Py.Hat(flur, "deko") ? (List<object?>)flur["deko"]! : [];
                deko.Add(new PyObjekt
                {
                    { "typ", "regal" }, { "x", zufall.NextDouble() * (innen.Breite + 2) - 1 }, { "y", zufall.NextDouble() * (innen.Tiefe + 2) - 1 },
                    { "w", 0.05 + zufall.NextDouble() * 2 }, { "h", 0.05 + zufall.NextDouble() * 2 },
                });
                Py.Setzen(flur, "deko", deko);
                Assert.Equal([.. BasisRasterAlt(gebaeude)], [.. Wege.BasisRaster(gebaeude)]);
                verglichen++;
            }
        }
        Assert.Equal(ansichten.Count * 26, verglichen);
        for (var i = 0; i < 2000; i++)
        {
            var zelle = ((long)zufall.Next(-300, 300), (long)zufall.Next(-300, 300));
            Assert.Equal(HashAlt(zelle), PyMenge.Hash(zelle));
        }
        Assert.Equal(HashAlt((-1, -1)), PyMenge.Hash((-1, -1)));
    }

    [Fact]
    public void StadtGleicheWege()
    {
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt);
        ablauf.Stand.TageFertig = 200;
        var stadt = new Stadt(SpielInhaltTests.Inhalt, SpielStadtTests.Daten.Value);
        var szene = StadtSzene.Bauen(stadt, ablauf.Stand, null);
        var neu = szene.Laufraster;
        var frei = new HashSet<(int, int)>();
        for (var s = -2; s < (szene.Breite / neu.Schritt) + 2; s++)
        {
            for (var z = -2; z < (szene.Tiefe / neu.Schritt) + 2; z++)
            {
                if (neu.IstFrei((s, z)))
                {
                    frei.Add((s, z));
                }
            }
        }
        Assert.Equal(neu.AnzahlFrei, frei.Count);
        var alt = new Alt(frei, neu.Schritt);
        var punkte = szene.Orte.Select(o => ((double)o.StehX, (double)o.StehZ)).ToList();
        var zufall = new Random(16);
        for (var i = 0; i < 150; i++)
        {
            // auch Punkte in Haeusern, im Wasser und ausserhalb (naechste freie Zelle)
            punkte.Add(((zufall.NextDouble() * (szene.Breite + 4)) - 2, (zufall.NextDouble() * (szene.Tiefe + 4)) - 2));
        }
        var verglichen = 0;
        foreach (var a in punkte)
        {
            Assert.Equal(alt.NaechsteFreie(a.Item1, a.Item2), neu.NaechsteFreie(a.Item1, a.Item2));
            foreach (var b in punkte.Where((_, k) => (k + verglichen) % 7 == 0))
            {
                Assert.Equal(alt.Weg(a, b), neu.Weg(a, b));
                verglichen++;
            }
        }
        Assert.True(verglichen > 2000, verglichen.ToString(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void InnenraeumeGleicheWege()
    {
        var ablauf = new Spielablauf(SpielInhaltTests.Inhalt);
        ablauf.Stand.TageFertig = 200;
        var ansichten = new List<string> { "buero", "zuhause", "serverfarm" };
        ansichten.AddRange(SpielDaten.Kundenorte(SpielInhaltTests.Inhalt).Select(k => "kunde:" + Py.Str(k["id"])));
        ansichten.AddRange(["geschaeft:moebelhaus", "geschaeft:elektromarkt", "schalter:bank", "schalter:amt"]);
        var zufall = new Random(17);
        var verglichen = 0;
        foreach (var ansicht in ansichten)
        {
            var innen = Innenraum.Bauen(ansicht, ablauf.Stand, SpielInnenraumTests.Innenraeume.Value, null)!;
            for (var i = 0; i < 120; i++)
            {
                var a = (zufall.NextDouble() * innen.Breite, zufall.NextDouble() * innen.Tiefe);
                var b = (zufall.NextDouble() * innen.Breite, zufall.NextDouble() * innen.Tiefe);
                var reichweite = i % 3 == 0 ? Wege.Reichweite : 0;
                Assert.Equal(WegAlt(a, b, reichweite, innen.Lauf), Wege.Weg(a, b, reichweite, innen.Lauf));
                verglichen++;
            }
        }
        Assert.Equal(ansichten.Count * 120, verglichen);
    }
}
