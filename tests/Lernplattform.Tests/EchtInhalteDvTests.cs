using System.Security.Cryptography;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.86.0, Weisung P12-292: Pruefung im echten Format fuer DV. Zehn neue Saetze (je AP2-Bereich fuenf), nur angehaengt.
/// Prueft Nicos Ziele: wer DV gewaehlt hat, bekommt in beiden Bereichen (Diagnose und Stoerungsbeseitigung, Betrieb und
/// Erweiterung) fuenf Saetze statt des Platzhalters; danach zeigt keine Fachrichtung mehr den Platzhalter; AP1, SI, AE und DPA
/// bleiben genau gleich (die Datei vor den DV-Saetzen ist bytegleich). Dazu der heutige Stand der App: jeder Satz hat einen
/// Netzplan aus den vorhandenen Anlagen, Ablaeufe nur als UML-Aktivitaet im Baukasten (Plan: dv_b1_03, dv_b2_02, dv_b2_05),
/// kein ER, keine Zeichnung ohne Baukasten. Jede Regel mit Gegenprobe.
/// </summary>
public class EchtInhalteDvTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static EchtBestand Bestand => EchtSaetze.Laden(File.ReadAllBytes(Datei));

    /// <summary>Die 10 Saetze der Welle in der Reihenfolge der Datei (hinter den 37 Saetzen bis A 0.85.0).</summary>
    public static readonly string[] Dv =
    [
        "dv_b1_01", "dv_b1_02", "dv_b1_03", "dv_b1_04", "dv_b1_05",
        "dv_b2_01", "dv_b2_02", "dv_b2_03", "dv_b2_04", "dv_b2_05",
    ];

    /// <summary>Saetze mit Baukasten (UML-Aktivitaet) laut Plan der Welle.</summary>
    private static readonly string[] MitBaukasten = ["dv_b1_03", "dv_b2_02", "dv_b2_05"];

    /// <summary>Laenge und SHA-256 der Datei bis vor das schliessende "\n ]\n}" am Stand A 0.85.0 (main 2d9f3e7).</summary>
    private const int VorherLaenge = 1536354;
    private const string VorherSha = "44b26f3b0bd772842e7acfbb6f4620b62190a7c9afec1f5057ab541a5b28d36c";

    private static IEnumerable<EchtSatz> DvSaetze => Bestand.Saetze.Where(s => Dv.Contains(s.Kennung));

    [Fact]
    public void DV_ZehnSaetze_JeBereichFuenf_KeinPlatzhalterMehr_AndereGleich()
    {
        // A 0.94.0: wie in der App (die alten AE-Saetze schliesst ausschluss.json aus, die AE-Satzpaare stehen an ihrer Stelle)
        var bestand = EchtTestDatei.App();
        Assert.Empty(bestand.Fehler);
        // A 0.94.0: dahinter folgen die AE-Satzpaare (EchtInhalteAePaareTests)
        Assert.Equal(57, bestand.Saetze.Count);
        Assert.Equal(Dv, bestand.Saetze.Skip(37).Take(Dv.Length).Select(s => s.Kennung));
        foreach (var f in Fachrichtung.Alle)
        {
            // AP1 gilt fuer alle (unveraendert 6), AP2: SI 6/5, AE und DPA 5/5 wie bisher, DV neu 5/5
            Assert.Equal(6, bestand.Fuer(EchtSaetze.Ap1, f.Id).Count);
            Assert.Equal(f.Id == "si" ? 6 : 5, bestand.Fuer(EchtSaetze.B1, f.Id).Count);
            Assert.Equal(5, bestand.Fuer(EchtSaetze.B2, f.Id).Count);
            Assert.Empty(bestand.FehlerFuer(EchtSaetze.B1, f.Id));
            Assert.Empty(bestand.FehlerFuer(EchtSaetze.B2, f.Id));
        }
        Assert.All(bestand.Fuer(EchtSaetze.B1, "dv"), s => Assert.StartsWith("dv_b1_", s.Kennung, StringComparison.Ordinal));
        Assert.All(bestand.Fuer(EchtSaetze.B2, "dv"), s => Assert.StartsWith("dv_b2_", s.Kennung, StringComparison.Ordinal));
        // Gegenprobe: ohne die DV-Saetze hat DV wieder den Platzhalter (keine Saetze, keine Meldung), die anderen nicht
        var ohne = new EchtBestand([.. bestand.Saetze.Where(s => !Dv.Contains(s.Kennung))], []);
        Assert.Empty(ohne.Fuer(EchtSaetze.B1, "dv"));
        Assert.Empty(ohne.Fuer(EchtSaetze.B2, "dv"));
        Assert.Equal(5, ohne.Fuer(EchtSaetze.B2, "dpa").Count);
    }

    [Fact]
    public void DV_NurAngehaengt_DateiDavorBytegleich()
    {
        // A 0.94.0: Datei seit Format 3, sonst bis hierher nur angehaengt (EchtTestDatei.AlsFormat2)
        var roh = EchtTestDatei.AlsFormat2(File.ReadAllBytes(Datei));
        Assert.True(roh.Length > VorherLaenge);
        Assert.Equal(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
        // direkt danach beginnt der erste DV-Satz
        var danach = System.Text.Encoding.UTF8.GetString(roh, VorherLaenge, 40);
        Assert.StartsWith(",\n  {", danach, StringComparison.Ordinal);
        Assert.Contains("\"kennung\": \"dv_b1_01\"", System.Text.Encoding.UTF8.GetString(roh, VorherLaenge, 400), StringComparison.Ordinal);
        // Gegenprobe: ein geaendertes Zeichen vorn faellt auf
        roh[100] ^= 1;
        Assert.NotEqual(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
    }

    [Fact]
    public void DV_JederSatzEigeneFirma_RichtigeFachrichtung_NieIhk_NenntDieAufgaben()
    {
        var alle = Bestand.Saetze;
        EchtTestDatei.FirmaJeSatzOderPaar(alle);
        foreach (var s in DvSaetze)
        {
            var aus = string.Join(" ", s.Ausgangssituation);
            Assert.Contains("Digitale Vernetzung", aus, StringComparison.Ordinal);
            Assert.DoesNotContain("Systemintegration", aus, StringComparison.Ordinal);
            Assert.DoesNotContain("Anwendungsentwicklung", aus, StringComparison.Ordinal);
            Assert.DoesNotContain("Daten- und Prozessanalyse", aus, StringComparison.Ordinal);
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
    /// Netzplan und Monitor nur aus den vorhandenen Anlagen (keine neue Anlagenart): jeder DV-Satz hat einen Netzplan, je
    /// Bereich hat mindestens ein Satz einen Monitor. Gegenprobe: ein Satz ohne Netzplan waere ein Fund.
    /// </summary>
    [Fact]
    public void DV_JederSatzMitNetzplan_JeBereichEinMonitor()
    {
        string[] bekannt = ["netzplan", "monitor", "text", "code", "tabelle", "englisch"];
        foreach (var s in DvSaetze)
        {
            Assert.Contains(s.Anlagen, a => a.Art == "netzplan");
            Assert.All(s.Anlagen, a => Assert.Contains(a.Art, bekannt));
        }
        Assert.Contains(DvSaetze, s => s.Bereich == EchtSaetze.B1 && s.Anlagen.Any(a => a.Art == "monitor"));
        Assert.Contains(DvSaetze, s => s.Bereich == EchtSaetze.B2 && s.Anlagen.Any(a => a.Art == "monitor"));
        // Gegenprobe
        var erster = DvSaetze.First();
        var ohne = erster with { Anlagen = [.. erster.Anlagen.Where(a => a.Art != "netzplan")] };
        Assert.DoesNotContain(ohne.Anlagen, a => a.Art == "netzplan");
    }

    /// <summary>
    /// Heutiger Stand der App (kein Umbau): Ablaeufe nur als UML-Aktivitaet im Baukasten, genau in den Saetzen des Plans; kein
    /// ER; keine Zeichnung ohne Baukasten. Gegenprobe: ein Diagramm-Teil ohne Baukasten und ohne "Papier" ist ein Fund.
    /// </summary>
    [Fact]
    public void DV_ZeichnenNurWieHeute_BaukastenUml()
    {
        foreach (var s in DvSaetze)
        {
            Assert.Empty(PapierFunde(s));
            var zeichnen = s.Aufgaben.SelectMany(a => a.Teile).Where(t => t.Antwort.Form == EchtSaetze.FormZeichnen).ToList();
            Assert.All(zeichnen, t => Assert.Equal(EchtZeichnung.Uml, t.Antwort.Diagramm));
            Assert.Equal(MitBaukasten.Contains(s.Kennung), zeichnen.Count > 0);
        }
        var satz = DvSaetze.First();
        var a = satz.Aufgaben[0];
        var t0 = a.Teile[0];
        var papier = t0 with { Operator = "erstellen", Text = ["Erstellen Sie das Diagramm."], Antwort = t0.Antwort with { Form = EchtSaetze.FormZeilen } };
        var mit = satz with { Aufgaben = [.. satz.Aufgaben.Select(x => x == a ? a with { Teile = [papier, .. a.Teile.Skip(1)] } : x)] };
        Assert.NotEmpty(PapierFunde(mit));
    }

    [Fact]
    public void DV_ProgrammBewertetJeSatzMindestens20Punkte_FuenfFormen()
    {
        foreach (var s in DvSaetze)
        {
            var teile = s.Aufgaben.SelectMany(a => a.Teile).ToList();
            var programm = 100 - teile.Sum(t => t.SelbstPunkte);
            Assert.True(programm >= 20, s.Kennung + ": nur " + programm + " Punkte bewertet das Programm");
            Assert.True(teile.Select(t => t.Antwort.Form).Distinct().Count() >= 5, s.Kennung);
            Assert.Equal(100, s.Aufgaben.Sum(a => a.Punkte));
        }
    }

    /// <summary>
    /// Jedes Zahlenfeld ab 1000 (Rechenergebnis, Ersatzergebnis, Lueckenfeld Art zahl) traegt woertlich den Satz "Geben Sie die
    /// Zahl ohne Tausenderpunkt an." (Regel seit A 0.79.0). Gegenprobe: ohne den Satz ein Fund.
    /// </summary>
    [Fact]
    public void DV_ZahlenfelderAb1000_NennenDenTausenderpunkt()
    {
        Assert.Empty(DvSaetze.SelectMany(TausenderFunde));
        var satz = DvSaetze.First(s => s.Aufgaben.SelectMany(a => a.Teile).Any(Ab1000));
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
