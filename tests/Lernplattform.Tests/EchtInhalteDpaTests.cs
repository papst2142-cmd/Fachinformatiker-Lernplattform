using System.Security.Cryptography;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.85.0, Weisung P12-291: Pruefung im echten Format fuer DPA. Zehn neue Saetze (je AP2-Bereich fuenf), nur angehaengt.
/// Prueft Nicos Ziele: wer DPA gewaehlt hat, bekommt in beiden Bereichen (Prozessanalyse, Datenqualitaet) fuenf Saetze
/// statt des Platzhalters (DV hat seit A 0.86.0 eigene Saetze, EchtInhalteDvTests); AP1, SI und AE bleiben genau gleich (die Datei vor den
/// DPA-Saetzen ist bytegleich). Dazu der heutige Stand der App: Prozesse nur als UML-Aktivitaet im Baukasten, Daten im
/// Baukasten nur als ER, keine Zeichnung ohne Baukasten, Diagramme mit Achsen nur als Tabelle (Luecke 2). Jede Regel mit Gegenprobe.
/// </summary>
public class EchtInhalteDpaTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static EchtBestand Bestand => EchtSaetze.Laden(File.ReadAllBytes(Datei));

    /// <summary>Die 10 Saetze der Welle in der Reihenfolge der Datei (hinter den 27 Saetzen bis A 0.84.1).</summary>
    public static readonly string[] Dpa =
    [
        "dpa_b1_01", "dpa_b1_02", "dpa_b1_03", "dpa_b1_04", "dpa_b1_05",
        "dpa_b2_01", "dpa_b2_02", "dpa_b2_03", "dpa_b2_04", "dpa_b2_05",
    ];

    /// <summary>Laenge und SHA-256 der Datei bis vor das schliessende "\n ]\n}" am Stand A 0.84.1 (main 9ced7da).</summary>
    private const int VorherLaenge = 1135182;
    private const string VorherSha = "1b41436059aa89c3dccb1eba0c723b985f897f28c15f1630ac2801ed846f5583";

    private static IEnumerable<EchtSatz> DpaSaetze => Bestand.Saetze.Where(s => Dpa.Contains(s.Kennung));

    [Fact]
    public void DPA_ZehnSaetze_JeBereichFuenf_SiAeUndAp1Gleich()
    {
        // A 0.94.0: wie in der App (die alten AE-Saetze schliesst ausschluss.json aus, die AE-Satzpaare stehen an ihrer Stelle)
        var bestand = EchtTestDatei.App();
        Assert.Empty(bestand.Fehler);
        // ab A 0.86.0 folgen die DV-Saetze (EchtInhalteDvTests); die DPA-Saetze stehen unveraendert an Platz 27 bis 36
        Assert.Equal(Dpa, bestand.Saetze.Skip(27).Take(Dpa.Length).Select(s => s.Kennung));
        foreach (var f in Fachrichtung.Alle)
        {
            // AP1 gilt fuer alle (unveraendert 6), AP2: SI 6/5, AE 5/5 wie bisher, DPA neu 5/5 (DV: eigener Test, 5/5)
            Assert.Equal(6, bestand.Fuer(EchtSaetze.Ap1, f.Id).Count);
            Assert.Equal(f.Id switch { "si" => 6, "ae" or "dpa" or "dv" => 5, _ => 0 }, bestand.Fuer(EchtSaetze.B1, f.Id).Count);
            Assert.Equal(f.Id switch { "si" => 5, "ae" or "dpa" or "dv" => 5, _ => 0 }, bestand.Fuer(EchtSaetze.B2, f.Id).Count);
            Assert.Empty(bestand.FehlerFuer(EchtSaetze.B1, f.Id));
            Assert.Empty(bestand.FehlerFuer(EchtSaetze.B2, f.Id));
        }
        Assert.All(bestand.Fuer(EchtSaetze.B1, "dpa"), s => Assert.StartsWith("dpa_b1_", s.Kennung, StringComparison.Ordinal));
        Assert.All(bestand.Fuer(EchtSaetze.B2, "dpa"), s => Assert.StartsWith("dpa_b2_", s.Kennung, StringComparison.Ordinal));
        // Gegenprobe: ohne die DPA-Saetze hat DPA wieder den Platzhalter (keine Saetze, keine Meldung)
        var ohne = new EchtBestand([.. bestand.Saetze.Where(s => !Dpa.Contains(s.Kennung))], []);
        Assert.Empty(ohne.Fuer(EchtSaetze.B1, "dpa"));
        Assert.Empty(ohne.Fuer(EchtSaetze.B2, "dpa"));
    }

    [Fact]
    public void DPA_NurAngehaengt_DateiDavorBytegleich()
    {
        // A 0.94.0: Datei seit Format 3, sonst bis hierher nur angehaengt (EchtTestDatei.AlsFormat2)
        var roh = EchtTestDatei.AlsFormat2(File.ReadAllBytes(Datei));
        Assert.True(roh.Length > VorherLaenge);
        Assert.Equal(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
        // direkt danach beginnt der erste DPA-Satz
        var danach = System.Text.Encoding.UTF8.GetString(roh, VorherLaenge, 40);
        Assert.StartsWith(",\n  {", danach, StringComparison.Ordinal);
        // Gegenprobe: ein geaendertes Zeichen vorn faellt auf
        roh[100] ^= 1;
        Assert.NotEqual(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
    }

    [Fact]
    public void DPA_JederSatzEigeneFirma_RichtigeFachrichtung_NieIhk_NenntDieAufgaben()
    {
        var alle = Bestand.Saetze;
        EchtTestDatei.FirmaJeSatzOderPaar(alle);
        foreach (var s in DpaSaetze)
        {
            var aus = string.Join(" ", s.Ausgangssituation);
            Assert.Contains("Daten- und Prozessanalyse", aus, StringComparison.Ordinal);
            Assert.DoesNotContain("Systemintegration", aus, StringComparison.Ordinal);
            Assert.DoesNotContain("Anwendungsentwicklung", aus, StringComparison.Ordinal);
            Assert.Contains("Auszubildende", aus, StringComparison.Ordinal);
            Assert.DoesNotContain("IHK", Alles(s), StringComparison.Ordinal);
            Assert.Equal(90, s.Minuten);
            for (var n = 0; n < 4; n++)
            {
                Assert.Contains($"{n + 1}. {s.Aufgaben[n].Titel}", s.Ausgangssituation);
            }
            Assert.Single(s.Anlagen, a => a.Art == "englisch");
        }
    }

    /// <summary>
    /// Heutiger Stand der App (kein Umbau): Prozesse nur als UML-Aktivitaet im Baukasten (Plan: dpa_b1_01, dpa_b1_04), ER nur
    /// in der Datenqualitaet (Plan: dpa_b2_03); keine Zeichnung ohne Baukasten ("auf Papier" wie bei AE waere ein Fund, wenn es
    /// fehlte). Gegenprobe: ein Papier-Teil ohne "Papier" im Text waere ein Fund.
    /// </summary>
    [Fact]
    public void DPA_ZeichnenNurWieHeute_BaukastenUmlUndEr()
    {
        foreach (var s in DpaSaetze)
        {
            Assert.Empty(PapierFunde(s));
            foreach (var t in s.Aufgaben.SelectMany(a => a.Teile).Where(t => t.Antwort.Form == EchtSaetze.FormZeichnen))
            {
                Assert.Equal(s.Bereich == EchtSaetze.B1 ? EchtZeichnung.Uml : EchtZeichnung.Er, t.Antwort.Diagramm);
            }
        }
        bool Zeichnet(EchtSatz s) => s.Aufgaben.SelectMany(a => a.Teile).Any(t => t.Antwort.Form == EchtSaetze.FormZeichnen);
        Assert.True(DpaSaetze.Count(s => s.Bereich == EchtSaetze.B1 && Zeichnet(s)) >= 2);
        Assert.True(DpaSaetze.Count(s => s.Bereich == EchtSaetze.B2 && Zeichnet(s)) >= 1);
        // Gegenprobe: ein Diagramm-Teil (erstellen) ohne Baukasten und ohne "Papier" ist ein Fund
        var satz = DpaSaetze.First();
        var a = satz.Aufgaben[0];
        var t0 = a.Teile[0];
        var papier = t0 with { Operator = "erstellen", Text = ["Erstellen Sie das Diagramm."], Antwort = t0.Antwort with { Form = EchtSaetze.FormZeilen } };
        var mit = satz with { Aufgaben = [.. satz.Aufgaben.Select(x => x == a ? a with { Teile = [papier, .. a.Teile.Skip(1)] } : x)] };
        Assert.NotEmpty(PapierFunde(mit));
    }

    [Fact]
    public void DPA_ProgrammBewertetJeSatzMindestens20Punkte_FuenfFormen()
    {
        foreach (var s in DpaSaetze)
        {
            var teile = s.Aufgaben.SelectMany(a => a.Teile).ToList();
            var programm = 100 - teile.Sum(t => t.SelbstPunkte);
            Assert.True(programm >= 20, s.Kennung + ": nur " + programm + " Punkte bewertet das Programm");
            Assert.True(teile.Select(t => t.Antwort.Form).Distinct().Count() >= 5, s.Kennung);
            Assert.Equal(100, s.Aufgaben.Sum(a => a.Punkte));
        }
    }

    /// <summary>Zeichnung ohne Baukasten: Operator erstellen/zeichnen, keine Zeichenform, Text verlangt ein Diagramm oder Struktogramm.</summary>
    /// <summary>
    /// Selbst gefunden (A 0.85.0): jedes Zahlenfeld ab 1000 (Rechenergebnis, Ersatzergebnis, Lueckenfeld Art zahl) traegt
    /// woertlich den Satz "Geben Sie die Zahl ohne Tausenderpunkt an." (Regel seit A 0.79.0). Gegenprobe: ohne den Satz ein Fund.
    /// </summary>
    [Fact]
    public void DPA_ZahlenfelderAb1000_NennenDenTausenderpunkt()
    {
        Assert.Empty(DpaSaetze.SelectMany(TausenderFunde));
        var satz = DpaSaetze.First(s => s.Aufgaben.SelectMany(a => a.Teile).Any(Ab1000));
        var a = satz.Aufgaben.First(x => x.Teile.Any(Ab1000));
        var ohne = a with { Teile = [.. a.Teile.Select(t => Ab1000(t) ? t with { Text = [.. t.Text.Select(z => z.Replace(Tausender, "", StringComparison.Ordinal))] } : t)] };
        Assert.NotEmpty(TausenderFunde(satz with { Aufgaben = [.. satz.Aufgaben.Select(x => x == a ? ohne : x)] }));
    }

    private const string Tausender = "Geben Sie die Zahl ohne Tausenderpunkt an.";

    private static bool Ab1000(EchtTeil t) => t.Antwort.Form switch
    {
        EchtSaetze.FormRechnen => Math.Abs(t.Antwort.Ergebnis) >= 1000 || Math.Abs(t.Antwort.ErsatzErgebnis ?? 0) >= 1000,
        _ => t.Antwort.Felder.Any(f => f.Art == "zahl" && f.Loesung.Any(l =>
            double.TryParse(l.Replace(",", ".", StringComparison.Ordinal), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var w) && Math.Abs(w) >= 1000)),
    };

    private static IEnumerable<string> TausenderFunde(EchtSatz s) => s.Aufgaben.SelectMany(a => a.Teile)
        .Where(t => Ab1000(t) && !string.Join(" ", t.Text).Contains(Tausender, StringComparison.Ordinal))
        .Select(t => s.Kennung + " " + t.Nr);

    private static bool Papier(EchtTeil t) => t.Operator is "erstellen" or "zeichnen" && t.Antwort.Form != EchtSaetze.FormZeichnen
        && System.Text.RegularExpressions.Regex.IsMatch(string.Join(" ", t.Text), "[Dd]iagramm|Struktogramm");

    private static List<string> PapierFunde(EchtSatz s) => [.. s.Aufgaben.SelectMany(a => a.Teile).Where(Papier)
        .Where(t => !string.Join(" ", t.Text).Contains("Papier", StringComparison.Ordinal) || t.Antwort.Form != EchtSaetze.FormZeilen)
        .Select(t => s.Kennung + " " + t.Nr)];

    private static string Alles(EchtSatz s) => string.Join("\n", s.Ausgangssituation.Concat(s.Anlagen.SelectMany(a => a.Zeilen.Append(a.Titel)))
        .Concat(s.Aufgaben.SelectMany(a => a.Einleitung.Append(a.Titel).Concat(a.Teile.SelectMany(t => t.Text.Concat(t.Loesung))))));
}
