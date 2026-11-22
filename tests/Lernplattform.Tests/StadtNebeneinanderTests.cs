using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Xunit;

namespace Lernplattform.Tests;

/// <summary>
/// Engine-Welle A 1.4.0 (Weisung P12-351, Sammelliste 436): die Stadt wird nebeneinander gebaut (Teile auf allen Kernen,
/// Laufraster neben der Grundlage, Instancing in einem Durchgang). Das Ergebnis muss Byte fuer Byte dem Bau der Reihe nach
/// gleichen (<see cref="StadtSzene.NacheinanderFuerTests"/>): alle Teile, Streifen, Stuecke, Haeuser, Orte, Signatur,
/// Laufraster, Laternen, die Grundlage (Boden, Fernkaesten, Instancing) und die Netze ALLER Kacheln. Dazu feste Zaehlwerte,
/// gemessen auf main 643f429 (A 1.3.2, vor der Welle), damit auch der Bau der Reihe nach nicht wandern kann.
/// Gegenproben: vertauschte Reihenfolge beim Anhaengen (<see cref="StadtSzene.VertauschtFuerTests"/>) und eine veraenderte
/// Ecke in einer Kachel werden erkannt.
/// </summary>
public sealed class StadtNebeneinanderTests
{
    /// <summary>Die Staedte des Belegs: Stadt 1 (Fassung 2) im Sommer und wie die Messung (Winter, Weihnachten), die Testkarte, eine Paket-Stadt.</summary>
    public static TheoryData<string> Staedte => ["stadt1", "stadt1-winter", "testkarte", "paket"];

    private static (IStadtSicht Stadt, ISpielSicht Stand, GrafikBibliothek Grafik, PyObjekt? Feste, string? Fest, Jahreszeit Jahreszeit) Eingabe(string name)
    {
        StadtSpiel spiel = name switch
        {
            "testkarte" => SpielTestkarteTests.Testkarte.Value,
            "paket" => PaketSpiel.Value,
            _ => Stadt1Spiel.Value,
        };
        return name == "stadt1"
            ? (spiel.Stadt, spiel.Ablauf.Stand, spiel.Grafik!, spiel.Feste, null, Jahreszeit.Sommer)
            : (spiel.Stadt, spiel.Ablauf.Stand, spiel.Grafik!, spiel.Feste, Festzeit.Weihnachten, Jahreszeit.Winter);
    }

    private static readonly Lazy<StadtSpiel> Stadt1Spiel = new(() =>
    {
        var stadt = SpielPaketStaedteTests.Stadt1();
        return new StadtSpiel(stadt, SpielQuestTests.AllesErledigt(stadt.Inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
    });

    private static readonly Lazy<StadtSpiel> PaketSpiel = new(() =>
    {
        var stadt1 = SpielPaketStaedteTests.Stadt1();
        var paket = SpielPaketStaedteTests.PaketStadt(stadt1);
        return new StadtSpiel(paket, SpielQuestTests.AllesErledigt(paket.Inhalt), SpielGrafikTests.Bibliothek.Value, SpielInnenraumTests.Innenraeume.Value);
    });

    /// <summary>Bau wie die Messung „Stadt bauen“: Szene, Grundlage, Laternen, dann alle Kacheln.</summary>
    private static Bauabdruck Bauen(string name, bool nacheinander, bool vertauscht = false)
    {
        var e = Eingabe(name);
        StadtSzene.NacheinanderFuerTests = nacheinander;
        StadtSzene.VertauschtFuerTests = vertauscht;
        try
        {
            var szene = StadtSzene.Bauen(e.Stadt, e.Stand, e.Grafik, e.Fest, e.Feste, e.Jahreszeit);
            var lader = new Nachlader(szene, mitGruen: true, hintergrund: false);
            return Bauabdruck.Von(szene, lader.Grundlage);
        }
        finally
        {
            StadtSzene.NacheinanderFuerTests = false;
            StadtSzene.VertauschtFuerTests = false;
        }
    }

    [Theory]
    [MemberData(nameof(Staedte))]
    public void Nebeneinander_BytegleichZumBauDerReiheNach(string name)
    {
        var reihe = Bauen(name, nacheinander: true);
        var neben = Bauen(name, nacheinander: false);
        Assert.Equal(reihe.Abschnitte.Keys, neben.Abschnitte.Keys);
        foreach (var (abschnitt, wert) in reihe.Abschnitte)
        {
            Assert.True(wert == neben.Abschnitte[abschnitt], name + ": Abschnitt " + abschnitt + " weicht ab");
        }
        // feste Zaehlwerte, gemessen auf main 643f429 (vor der Welle)
        Assert.Equal(Fest[name], reihe.Zaehlung);
        Assert.Equal(Fest[name], neben.Zaehlung);
    }

    [Fact]
    public void Gegenprobe_VertauschteReihenfolge_UndVeraenderteEcke_WerdenErkannt()
    {
        var reihe = Bauen("stadt1", nacheinander: true);
        var vertauscht = Bauen("stadt1", nacheinander: false, vertauscht: true);
        var anders = reihe.Abschnitte.Where(a => vertauscht.Abschnitte[a.Key] != a.Value).Select(a => a.Key).ToList();
        Assert.NotEmpty(anders);
        // eine Ecke einer Kachel um den kleinsten Schritt veraendert
        var e = Eingabe("stadt1");
        StadtSzene.NacheinanderFuerTests = true;
        try
        {
            var szene = StadtSzene.Bauen(e.Stadt, e.Stand, e.Grafik, e.Fest, e.Feste, e.Jahreszeit);
            var lader = new Nachlader(szene, mitGruen: true, hintergrund: false);
            var veraendert = Bauabdruck.Von(szene, lader.Grundlage, (k, netz) =>
            {
                if (k.Spalte == 0 && k.Reihe == 0 && netz.Ecken.Count > 0)
                {
                    var feld = MemoryMarshal.CreateSpan(ref MemoryMarshal.GetReference(netz.EckenFeld), netz.EckenFeld.Length);
                    feld[0] = MathF.BitIncrement(feld[0]);
                }
            });
            Assert.Contains(reihe.Abschnitte, a => veraendert.Abschnitte[a.Key] != a.Value);
        }
        finally
        {
            StadtSzene.NacheinanderFuerTests = false;
        }
    }

    /// <summary>Zaehlwerte je Stadt (main 643f429): Teile, Streifen, Stuecke, Haeuser, freie Zellen, Laternen, Saetze, Kacheln, Ecken und Indizes aller Kacheln.</summary>
    private static readonly Dictionary<string, string> Fest = new()
    {
        ["stadt1"] = "teile 855, streifen 902, stuecke 58, haeuser 9, frei 58199, laternen 74, saetze 4, kacheln 16, ecken 3303936, indizes 389832",
        ["stadt1-winter"] = "teile 855, streifen 902, stuecke 58, haeuser 9, frei 58199, laternen 74, saetze 1, kacheln 16, ecken 3613284, indizes 424380",
        ["testkarte"] = "teile 12444, streifen 14312, stuecke 928, haeuser 174, frei 925469, laternen 1184, saetze 8, kacheln 256, ecken 49660830, indizes 5860074",
        ["paket"] = "teile 302, streifen 258, stuecke 32, haeuser 7, frei 10393, laternen 22, saetze 8, kacheln 4, ecken 1846359, indizes 214983",
    };
}

/// <summary>Abdruck eines Baus: je Abschnitt eine SHA-256 der Daten (Gleitkommazahlen bitgenau) und eine Zaehlzeile.</summary>
internal sealed class Bauabdruck
{
    public Dictionary<string, string> Abschnitte { get; } = new(StringComparer.Ordinal);

    public string Zaehlung { get; private set; } = "";

    private sealed class Schreiber : IDisposable
    {
        private readonly IncrementalHash _h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

        public void Text(string? s) => _h.AppendData(Encoding.UTF8.GetBytes((s ?? "<null>") + "\u0001"));

        public void Zahl(float f) => _h.AppendData(BitConverter.GetBytes(f));

        public void Zahl(double d) => _h.AppendData(BitConverter.GetBytes(d));

        public void Zahl(int i) => _h.AppendData(BitConverter.GetBytes(i));

        public void Punkte(IEnumerable<Vector2> pp)
        {
            var n = 0;
            foreach (var p in pp)
            {
                Zahl(p.X);
                Zahl(p.Y);
                n++;
            }
            Zahl(n);
        }

        public void Netz(Netz? n)
        {
            if (n is null)
            {
                Text("kein Netz");
                return;
            }
            _h.AppendData(MemoryMarshal.AsBytes(n.EckenFeld));
            _h.AppendData(MemoryMarshal.AsBytes(n.IndizesFeld));
            Zahl(n.Ecken.Count);
            Zahl(n.Indizes.Count);
        }

        public string Fertig() => Convert.ToHexString(_h.GetHashAndReset());

        public void Dispose() => _h.Dispose();
    }

    private void Abschnitt(string name, Action<Schreiber> was)
    {
        using var s = new Schreiber();
        var kultur = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            was(s);
        }
        finally
        {
            CultureInfo.CurrentCulture = kultur;
        }
        Abschnitte[name] = s.Fertig();
    }

    public static Bauabdruck Von(StadtSzene szene, StadtSzene.Grundlage grundlage, Action<Stadtteile.Kachel, Netz>? aendern = null)
    {
        var a = new Bauabdruck();
        a.Abschnitt("Teile", s => { foreach (var t in szene.Teile) { s.Text(t.ToString()); } });
        a.Abschnitt("Streifen", s =>
        {
            foreach (var t in szene.Streifen)
            {
                s.Punkte(t.Punkte);
                s.Zahl(t.Breite);
                s.Zahl(t.Unten);
                s.Zahl(t.Oben);
                s.Text(t.Farbe.ToString());
            }
        });
        a.Abschnitt("Stuecke", s => { foreach (var t in szene.Stuecke) { s.Text(t.ToString()); } });
        a.Abschnitt("Haeuser", s => { foreach (var t in szene.Haeuser) { s.Text(t.ToString()); } });
        a.Abschnitt("Orte", s => { foreach (var t in szene.Orte) { s.Text(t.ToString()); } });
        a.Abschnitt("Tueren", s => { foreach (var t in szene.Tueren) { s.Text(t.ToString()); } });
        a.Abschnitt("Zierhaeuser", s => { foreach (var t in szene.Zierhaeuser) { s.Text(t.ToString()); } });
        a.Abschnitt("Signatur", s => s.Text(szene.Signatur));
        a.Abschnitt("Wege", s =>
        {
            foreach (var l in szene.GehWege) { s.Punkte(l); }
            foreach (var l in szene.Strassen) { s.Punkte(l); }
            foreach (var l in szene.Gleise) { s.Punkte(l); }
            foreach (var l in szene.Fluesse) { s.Punkte(l); }
            s.Punkte(szene.Bruecken);
            foreach (var t in szene.BrueckenKaesten) { s.Text(t.ToString()); }
            foreach (var t in szene.StrassenEnden) { s.Text(t.ToString()); }
            foreach (var t in szene.Uebergaenge) { s.Text(t.ToString()); }
            foreach (var t in szene.Andreaskreuze) { s.Text(t.ToString()); }
        });
        a.Abschnitt("Baeume", s =>
        {
            foreach (var t in szene.Baeume) { s.Text(t.ToString()); }
            foreach (var (linie, halb) in szene.BaumSperren) { s.Punkte(linie); s.Zahl(halb); }
        });
        a.Abschnitt("Fest", s =>
        {
            s.Text(szene.Fest);
            foreach (var t in szene.FestGesetzt) { s.Text(t.ToString()); }
            foreach (var t in szene.FestPlaetzeAlle) { s.Text(t.ToString()); }
            s.Zahl(szene.FestTeileVon);
            s.Zahl(szene.FestStueckeVon);
        });
        a.Abschnitt("Kleingruen", s =>
        {
            if (szene.GruenKlein is { } g)
            {
                foreach (var t in g.Stuecke) { s.Text(t.ToString()); }
                foreach (var t in g.Teile) { s.Text(t.ToString()); }
                foreach (var t in g.Baeume) { s.Text(t.ToString()); }
                foreach (var t in g.Plaetze) { s.Text(t.ToString()); }
                foreach (var t in g.Hindernisse) { s.Text(t.ToString()); }
                foreach (var t in g.Anzahl.OrderBy(x => x.Key, StringComparer.Ordinal)) { s.Text(t.ToString()); }
            }
        });
        a.Abschnitt("Licht", s =>
        {
            foreach (var i in szene.LichtTeile.Order()) { s.Zahl(i); }
            foreach (var i in szene.Umgefaerbt.Keys.Order()) { s.Zahl(i); s.Text(szene.Umgefaerbt[i].Name); }
        });
        var raster = szene.Laufraster;
        var spalten = (int)Math.Ceiling(szene.Breite / raster.Schritt) + 2;
        var zeilen = (int)Math.Ceiling(szene.Tiefe / raster.Schritt) + 2;
        a.Abschnitt("Laufraster", s =>
        {
            s.Zahl(raster.Schritt);
            s.Zahl(raster.AnzahlFrei);
            for (var x = -1; x < spalten; x++)
            {
                for (var z = -1; z < zeilen; z++)
                {
                    s.Zahl(raster.IstFrei((x, z)) ? 1 : 0);
                }
            }
            for (var x = 0.013; x < szene.Breite; x += 0.37)
            {
                for (var z = 0.017; z < szene.Tiefe; z += 0.37)
                {
                    s.Zahl(raster.FeinGesperrt(x, z) ? 1 : 0);
                }
            }
        });
        a.Abschnitt("Laternen", s => { foreach (var l in szene.Laternen) { s.Text(l.ToString()); } });
        a.Abschnitt("Grundlage", s =>
        {
            s.Netz(grundlage.Boden);
            s.Netz(grundlage.FernKaesten);
            foreach (var (k, b) in grundlage.FernBereiche.OrderBy(x => x.Key.Reihe).ThenBy(x => x.Key.Spalte)) { s.Text(k.Name + b); }
            foreach (var (k, h) in grundlage.Hoehen.OrderBy(x => x.Key.Reihe).ThenBy(x => x.Key.Spalte)) { s.Text(k.Name); s.Zahl(h); }
            foreach (var n in grundlage.AmpelLichter) { s.Netz(n); }
        });
        var plan = grundlage.Instanzen;
        a.Abschnitt("Instancing", s =>
        {
            if (plan is null)
            {
                s.Text("kein Plan");
                return;
            }
            foreach (var i in plan.Stuecke.Order()) { s.Zahl(i); }
            s.Text("|");
            foreach (var i in plan.Baeume.Order()) { s.Zahl(i); }
            s.Text("|");
            foreach (var i in plan.Gruen.Order()) { s.Zahl(i); }
            foreach (var z in plan.Saetze)
            {
                s.Text(z.Ziel + "|" + z.Modell);
                s.Netz(z.Nah);
                s.Netz(z.Fern);
                foreach (var l in z.Lagen) { s.Text(l.ToString()); }
                foreach (var k in z.Kugeln) { s.Text(k.ToString()); }
            }
        });
        var kacheln = Stadtteile.Alle(szene.Breite, szene.Tiefe).ToList();
        long ecken = 0, indizes = 0;
        a.Abschnitt("Kacheln", s =>
        {
            foreach (var k in kacheln)
            {
                var n = szene.KachelVorbereiten(k);
                s.Text(k.Name);
                foreach (var netz in n.Netze)
                {
                    aendern?.Invoke(k, netz);
                    s.Netz(netz);
                    ecken += netz.Ecken.Count;
                    indizes += netz.Indizes.Count;
                }
                foreach (var (haus, _) in n.Haeuser) { s.Text(haus.Id); }
            }
        });
        a.Zaehlung = string.Create(CultureInfo.InvariantCulture,
            $"teile {szene.Teile.Count}, streifen {szene.Streifen.Count}, stuecke {szene.Stuecke.Count}, haeuser {szene.Haeuser.Count}, frei {raster.AnzahlFrei}, laternen {szene.Laternen.Count}, saetze {plan?.Saetze.Count ?? 0}, kacheln {kacheln.Count}, ecken {ecken}, indizes {indizes}");
        return a;
    }
}
