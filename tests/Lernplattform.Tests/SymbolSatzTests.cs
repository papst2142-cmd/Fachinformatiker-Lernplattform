using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Design;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// A 1.17.0 (Weisung P12-430, Abnahmesaetze B2 und B3): Symbole Satz 3 mit Start = Sonnenaufgang. Prueft: jedes Ziel der Leisten
/// hat genau sein Symbol, jedes Symbol ist zweifarbig (Form und Akzent), die Umrisse sind genau die aus dem Entwurf (fester
/// Anker), und in allen 72 Farbwelten (hell und dunkel) heben sich Form, gewaehlte Form und Akzent von der Leiste und der
/// gewaehlten Pille ab (Grenze fuer Bedienelemente 3:1), die Woerter der Leisten sind lesbar (4,6:1). Gegenproben machen jede
/// Pruefung rot.
/// </summary>
public class SymbolSatzTests
{
    /// <summary>Motive je Symbol wie in Meldung P12-271 (Satz 3) und P12-272 (Start Variante 3).</summary>
    private static readonly string[] Namen = ["START", "PRUEFUNG", "LERNEN", "SPIEL", "FORTSCHRITT", "HILFE", "OPTIONEN", "MEHR", "BEENDEN"];

    [Fact]
    public void JedesZielHatSeinSymbol()
    {
        Assert.Equal(Namen.Order(StringComparer.Ordinal), SymbolSatz.Satz3.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(Namen.Order(StringComparer.Ordinal), Pruefungsteile.Symbole.Values.Order(StringComparer.Ordinal));
        Assert.Equal(Pruefungsteile.Symbole.Count, Pruefungsteile.Symbole.Values.Distinct().Count());
        // Hilfe oben in der Kopfleiste mit demselben Wegweiser wie links am PC
        Assert.Equal(Pruefungsteile.Symbole["help"], Navigation.KopfKnoepfeHandy.Single(k => k.Id == "help").Symbol);
    }

    [Fact]
    public void JedesSymbolIstZweifarbig()
    {
        foreach (var (name, teile) in SymbolSatz.Satz3)
        {
            Assert.Contains(teile, t => t.Art == SymbolTeilArt.Form);
            Assert.Contains(teile, t => t.Art == SymbolTeilArt.Akzent);
            Assert.All(teile, t => Assert.Matches("^[MmLlHhVvAaZzCc0-9., -]+$", t.Pfad));
            Assert.All(teile.Where(t => t.Art == SymbolTeilArt.Linie), t => Assert.True(t.Breite > 0, name));
        }
    }

    /// <summary>Anker: SHA-256 ueber alle Teile (Name, Art, Pfad, Breite). Aendert sich nur mit einer neuen Weisung.</summary>
    private const string Anker = "387d4ee4c7ed2b6c2de8e97e12ec4c5e1f5d9f71894028415156f6ca53e98d6d";

    internal static string Fingerabdruck(IReadOnlyDictionary<string, SymbolTeil[]> satz)
    {
        var text = new StringBuilder();
        foreach (var name in satz.Keys.Order(StringComparer.Ordinal))
        {
            foreach (var t in satz[name])
            {
                text.Append(name).Append('|').Append(t.Art).Append('|').Append(t.Pfad).Append('|')
                    .Append(t.Breite.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
            }
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    [Fact]
    public void UmrisseGenauWieDerEntwurf()
    {
        Assert.Equal(Anker, Fingerabdruck(SymbolSatz.Satz3));
        // Start ist der Sonnenaufgang (Variante 3: Sonne als Akzent, Strahlen und Horizont als Form), nicht die Rakete aus P12-271
        var start = SymbolSatz.Satz3["START"];
        Assert.Equal(SymbolTeilArt.Akzent, start[0].Art);
        Assert.Equal(7, start.Count(t => t.Art == SymbolTeilArt.Form));
        // Gegenprobe: ein veraenderter Punkt aendert den Fingerabdruck
        var anders = SymbolSatz.Satz3.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        anders["START"] = [.. start.Select((t, i) => i == 0 ? t with { Pfad = t.Pfad.Replace("16", "15", StringComparison.Ordinal) } : t)];
        Assert.NotEqual(Anker, Fingerabdruck(anders));
    }

    /// <summary>Alle 72 Farbwelten (6 Grundfarben x 6 Hintergruende x hell/dunkel).</summary>
    public static IEnumerable<Palette> Farbwelten() =>
        from d in new[] { Farbwelt.Dunkel, Farbwelt.Hell }
        from g in Farbwelt.Grundfarben
        from h in Farbwelt.Hintergruende
        select Farbwelt.Berechne(d, g.Id, h.Id);

    /// <summary>Funde: (Farbe, Flaeche, Grenze) unter der Grenze in einer Farbwelt.</summary>
    internal static List<string> Kontrastfunde(Palette p)
    {
        var funde = new List<string>();
        void Pruefe(string farbe, string flaeche, double grenze)
        {
            var k = Farbwelt.Kontrast(p.Farben[farbe], p.Farben[flaeche]);
            if (k < grenze)
            {
                funde.Add($"{(p.Hell ? "hell" : "dunkel")} {farbe} auf {flaeche}: {k:F2} < {grenze}");
            }
        }
        // Symbole: Form nicht gewaehlt (muted auf sidebar), gewaehlt (accent auf card_hi), Akzent (accent2) auf beiden
        foreach (var flaeche in new[] { "sidebar", "card_hi", "bg" })
        {
            Pruefe("muted", flaeche, Farbwelt.BedienKontrast);
            Pruefe("accent", flaeche, Farbwelt.BedienKontrast);
            Pruefe("accent2", flaeche, Farbwelt.BedienKontrast);
            Pruefe("text_dim", flaeche, Farbwelt.BedienKontrast);
        }
        // Woerter: unten muted bzw. gewaehlt text, links Unterpunkte und Gruppenkopf text_dim, Hauptpunkte text
        Pruefe("muted", "sidebar", Farbwelt.MindestKontrast);
        Pruefe("text", "card_hi", Farbwelt.MindestKontrast);
        Pruefe("text", "sidebar", Farbwelt.MindestKontrast);
        Pruefe("text_dim", "sidebar", Farbwelt.MindestKontrast);
        // Segment-Leiste: nicht gewaehlte Haelfte text_dim auf card_alt
        Pruefe("text_dim", "card_alt", Farbwelt.MindestKontrast);
        // P12-434: Symbol gewaehlt (accent, Akzent accent2) auf der Pille der unteren Leiste
        var pille = Leistenpille.Farbe(p);
        foreach (var farbe in new[] { "accent", "accent2" })
        {
            var k = Farbwelt.Kontrast(p.Farben[farbe], pille);
            if (k < Farbwelt.BedienKontrast)
            {
                funde.Add($"{(p.Hell ? "hell" : "dunkel")} {farbe} auf Pille {pille}: {k:F2} < {Farbwelt.BedienKontrast}");
            }
        }
        return funde;
    }

    [Fact]
    public void KontrastInAllen72Farbwelten()
    {
        var welten = Farbwelten().ToList();
        Assert.Equal(72, welten.Count);
        var funde = welten.SelectMany(Kontrastfunde).ToList();
        Assert.Empty(funde);
    }

    /// <summary>
    /// Weisung P12-434 (Frage 4 = a): die Pille hinter dem gewaehlten Knopf unten hebt sich in jeder hellen Farbwelt mindestens so
    /// deutlich von der Leiste ab wie im Entwurf P12-271 (#E3D9F5 auf #EBE4F7); im Dunklen bleibt card_hi.
    /// </summary>
    [Fact]
    public void PilleUntenSoDeutlichWieImEntwurf()
    {
        Assert.InRange(Leistenpille.EntwurfKontrast, 1.09, 1.10);
        var welten = Farbwelten().ToList();
        Assert.Equal(36, welten.Count(p => p.Hell));
        foreach (var p in welten)
        {
            var pille = Leistenpille.Farbe(p);
            if (p.Hell)
            {
                Assert.True(Farbwelt.Kontrast(p.Farben["sidebar"], pille) >= Leistenpille.EntwurfKontrast,
                    $"hell {p.Farben["sidebar"]}: Pille {pille} zu blass");
                // aus der Farbwelt abgeleitet: liegt zwischen Leiste und Rand, nicht fest
                Assert.True(Farbwelt.Kontrast(p.Farben["sidebar"], pille) <= Farbwelt.Kontrast(p.Farben["sidebar"], p.Farben["border"]) + 1e-9);
            }
            else
            {
                Assert.Equal(p.Farben["card_hi"], pille);
            }
        }
        // Gegenprobe: die bisherige Farbe card_hi war im Hellen zu blass (violett 1,03 statt 1,09)
        var violett = Farbwelt.Berechne(Farbwelt.Hell, Farbwelt.StandardGrundfarbe, "violett");
        Assert.True(Farbwelt.Kontrast(violett.Farben["sidebar"], violett.Farben["card_hi"]) < Leistenpille.EntwurfKontrast);
    }

    /// <summary>
    /// Weisung P12-435 (Nico 27.11.2026 14:21 MEZ): das Symbol „Mehr“ bleibt genau wie gebaut, mit dem hellen Punkt in der Mitte
    /// des Kreuzes. Der Punkt entsteht, weil die Oberflaeche den Pfad mit der Fuellregel von Avalonia (evenodd, ohne „F1“) liest
    /// und sich die zwei Balken in der Mitte ueberlagern. Darum: Kreuz aus genau zwei sich kreuzenden Balken, wortgleich, und
    /// <c>SatzSymbol</c> liest die Pfade ohne Vorsatz „F1“.
    /// </summary>
    private static string Wurzel()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "Directory.Build.props")))
        {
            ordner = ordner.Parent;
        }
        return ordner!.FullName;
    }

    [Fact]
    public void MehrBehaeltDenHellenPunktInDerMitte()
    {
        var kreuz = SymbolSatz.Satz3["MEHR"].Single(t => t.Art == SymbolTeilArt.Akzent).Pfad;
        Assert.Equal("M10.8 6.8h2.4v10.4h-2.4zM6.8 10.8h10.4v2.4H6.8z", kreuz);
        var quelle = File.ReadAllText(Path.Combine(Wurzel(), "src", "Lernplattform.App", "Views", "SatzSymbol.cs"));
        Assert.Contains("Geometry.Parse(teil.Pfad)", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("F1", quelle, StringComparison.Ordinal);
    }

    [Fact]
    public void GegenprobeKontrastFindetZuHellenAkzent()
    {
        // ungedaempftes Gelb (accent2 Orange/Gelb dunkel) auf der hellen Leiste: muss ein Fund sein
        var hell = Farbwelt.Berechne(Farbwelt.Hell, "orange_gelb", "violett");
        var farben = hell.Farben.ToDictionary(e => e.Key, e => e.Value, StringComparer.Ordinal);
        farben["accent2"] = "#FDE047";
        Assert.Contains(Kontrastfunde(hell with { Farben = farben }), f => f.Contains("accent2 auf sidebar", StringComparison.Ordinal));
    }
}
