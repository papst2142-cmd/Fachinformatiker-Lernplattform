using System.Security.Cryptography;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Pruefung.Echt;

namespace Lernplattform.Tests;

/// <summary>
/// A 0.84.0, Weisung P12-285: Pruefung im echten Format fuer AE. Zehn neue Saetze (je AP2-Bereich fuenf), nur angehaengt.
/// Prueft Nicos Ziele: wer AE gewaehlt hat, bekommt in beiden Bereichen fuenf Saetze statt des Platzhalters (DPA seit
/// A 0.85.0 und DV seit A 0.86.0 haben eigene Saetze, eigene Tests); AP1 und SI bleiben genau gleich (die Datei vor den AE-Saetzen ist bytegleich). Dazu der
/// heutige Stand der App (Punkt 3): Klassen-, Use-Case-Diagramm und Struktogramm nur als Zeilen "auf Papier",
/// Aktivitaetsdiagramm und ER im Baukasten. Jede Regel mit Gegenprobe.
/// </summary>
public class EchtInhalteAeTests
{
    private static readonly string Datei = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen", EchtSaetze.Datei);

    private static EchtBestand Bestand => EchtSaetze.Laden(File.ReadAllBytes(Datei));

    /// <summary>Die 10 Saetze der Welle in der Reihenfolge der Datei (hinter den 17 Saetzen bis A 0.83.0).</summary>
    public static readonly string[] Ae =
    [
        "ae_b1_01", "ae_b1_02", "ae_b1_03", "ae_b1_04", "ae_b1_05",
        "ae_b2_01", "ae_b2_02", "ae_b2_03", "ae_b2_04", "ae_b2_05",
    ];

    /// <summary>Laenge und SHA-256 der Datei bis vor das schliessende "\n ]\n}" am Stand A 0.83.0 (main 61f2ba8).</summary>
    private const int VorherLaenge = 711381;
    private const string VorherSha = "6134057d467d4e95f1fae824f6da6e5ce97d06214e218504917b471c9e2796e1";

    private static IEnumerable<EchtSatz> AeSaetze => Bestand.Saetze.Where(s => Ae.Contains(s.Kennung));

    [Fact]
    public void AE_ZehnSaetze_JeBereichFuenf_SiUndAp1Gleich()
    {
        // A 0.94.0: wie in der App (die alten AE-Saetze schliesst ausschluss.json aus, die AE-Satzpaare stehen an ihrer Stelle)
        var bestand = EchtTestDatei.App();
        Assert.Empty(bestand.Fehler);
        // ab A 0.85.0 folgen die DPA-, ab A 0.86.0 die DV-Saetze (EchtInhalteDpaTests, EchtInhalteDvTests); die AE-Saetze stehen unveraendert an Platz 17 bis 26
        Assert.Equal(Ae, bestand.Saetze.Skip(17).Take(Ae.Length).Select(s => s.Kennung));
        foreach (var f in Fachrichtung.Alle)
        {
            // AP1 gilt fuer alle (unveraendert 6), AP2: SI 6/5 wie bisher, AE neu 5/5 (DPA und DV: eigene Tests, je 5/5)
            Assert.Equal(6, bestand.Fuer(EchtSaetze.Ap1, f.Id).Count);
            Assert.Equal(f.Id switch { "si" => 6, "ae" or "dpa" or "dv" => 5, _ => 0 }, bestand.Fuer(EchtSaetze.B1, f.Id).Count);
            Assert.Equal(f.Id switch { "si" => 5, "ae" or "dpa" or "dv" => 5, _ => 0 }, bestand.Fuer(EchtSaetze.B2, f.Id).Count);
            Assert.Empty(bestand.FehlerFuer(EchtSaetze.B1, f.Id));
            Assert.Empty(bestand.FehlerFuer(EchtSaetze.B2, f.Id));
        }
        Assert.All(bestand.Fuer(EchtSaetze.B1, "ae"), s => Assert.StartsWith("ae_b1_", s.Kennung, StringComparison.Ordinal));
        Assert.All(bestand.Fuer(EchtSaetze.B2, "ae"), s => Assert.StartsWith("ae_b2_", s.Kennung, StringComparison.Ordinal));
    }

    [Fact]
    public void AE_NurAngehaengt_DateiDavorBytegleich()
    {
        // A 0.94.0: Datei seit Format 3, sonst bis hierher nur angehaengt (EchtTestDatei.AlsFormat2)
        var roh = EchtTestDatei.AlsFormat2(File.ReadAllBytes(Datei));
        Assert.True(roh.Length > VorherLaenge);
        Assert.Equal(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
        // direkt danach beginnt der erste AE-Satz
        var danach = System.Text.Encoding.UTF8.GetString(roh, VorherLaenge, 40);
        Assert.StartsWith(",\n  {", danach, StringComparison.Ordinal);
        // Gegenprobe: ein geaendertes Zeichen vorn faellt auf
        roh[100] ^= 1;
        Assert.NotEqual(VorherSha, Convert.ToHexStringLower(SHA256.HashData(roh.AsSpan(0, VorherLaenge))));
    }

    [Fact]
    public void AE_JederSatzEigeneFirma_RichtigeFachrichtung_NieIhk_NenntDieAufgaben()
    {
        var alle = Bestand.Saetze;
        EchtTestDatei.FirmaJeSatzOderPaar(alle);
        foreach (var s in AeSaetze)
        {
            var aus = string.Join(" ", s.Ausgangssituation);
            Assert.Contains("Anwendungsentwicklung", aus, StringComparison.Ordinal);
            Assert.DoesNotContain("Systemintegration", aus, StringComparison.Ordinal);
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
    /// Punkt 3 (heutiger Stand der App, kein Umbau): Zeichnungen ohne Baukasten sind Zeilen "auf Papier" mit Musterloesung
    /// und Haekchen; im Baukasten nur UML-Aktivitaet und ER; mindestens vier Saetze mit Baukasten (Plan: b1_02, b1_04,
    /// b2_02, b2_03). Gegenprobe: ein Papier-Teil ohne "Papier" im Text waere ein Fund.
    /// </summary>
    [Fact]
    public void AE_ZeichnenNurWieHeute_PapierOderBaukasten()
    {
        foreach (var s in AeSaetze)
        {
            Assert.Empty(PapierFunde(s));
            foreach (var t in s.Aufgaben.SelectMany(a => a.Teile).Where(t => t.Antwort.Form == EchtSaetze.FormZeichnen))
            {
                Assert.Contains(t.Antwort.Diagramm, new[] { EchtZeichnung.Uml, EchtZeichnung.Er });
            }
        }
        Assert.True(AeSaetze.Count(s => s.Aufgaben.SelectMany(a => a.Teile).Any(t => t.Antwort.Form == EchtSaetze.FormZeichnen)) >= 4);
        Assert.True(AeSaetze.Count(s => s.Aufgaben.SelectMany(a => a.Teile).Any(Papier)) >= 3);
        // Gegenprobe
        var satz = AeSaetze.First(s => s.Aufgaben.SelectMany(a => a.Teile).Any(Papier));
        var a = satz.Aufgaben.First(x => x.Teile.Any(Papier));
        var ohne = a with { Teile = [.. a.Teile.Select(t => Papier(t) ? t with { Text = ["Erstellen Sie das Diagramm."] } : t)] };
        Assert.True(Papier(ohne.Teile.First(t => t.Text[0] == "Erstellen Sie das Diagramm.")));
        Assert.NotEmpty(PapierFunde(satz with { Aufgaben = [.. satz.Aufgaben.Select(x => x == a ? ohne : x)] }));
    }

    [Fact]
    public void AE_ProgrammBewertetJeSatzMindestens20Punkte_FuenfFormen()
    {
        foreach (var s in AeSaetze)
        {
            var teile = s.Aufgaben.SelectMany(a => a.Teile).ToList();
            var programm = 100 - teile.Sum(t => t.SelbstPunkte);
            Assert.True(programm >= 20, s.Kennung + ": nur " + programm + " Punkte bewertet das Programm");
            Assert.True(teile.Select(t => t.Antwort.Form).Distinct().Count() >= 5, s.Kennung);
            Assert.Equal(100, s.Aufgaben.Sum(a => a.Punkte));
        }
    }

    /// <summary>Zeichnung ohne Baukasten: Operator erstellen/zeichnen, keine Zeichenform, Text verlangt ein Diagramm oder Struktogramm.</summary>
    private static bool Papier(EchtTeil t) => t.Operator is "erstellen" or "zeichnen" && t.Antwort.Form != EchtSaetze.FormZeichnen
        && System.Text.RegularExpressions.Regex.IsMatch(string.Join(" ", t.Text), "[Dd]iagramm|Struktogramm");

    private static List<string> PapierFunde(EchtSatz s) => [.. s.Aufgaben.SelectMany(a => a.Teile).Where(Papier)
        .Where(t => !string.Join(" ", t.Text).Contains("Papier", StringComparison.Ordinal) || t.Antwort.Form != EchtSaetze.FormZeilen)
        .Select(t => s.Kennung + " " + t.Nr)];

    private static string Alles(EchtSatz s) => string.Join("\n", s.Ausgangssituation.Concat(s.Anlagen.SelectMany(a => a.Zeilen.Append(a.Titel)))
        .Concat(s.Aufgaben.SelectMany(a => a.Einleitung.Append(a.Titel).Concat(a.Teile.SelectMany(t => t.Text.Concat(t.Loesung))))));
}
