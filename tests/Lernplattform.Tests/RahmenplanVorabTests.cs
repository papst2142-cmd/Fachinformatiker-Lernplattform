using System.Text.Json.Nodes;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P12-369 (Nico 24.11.2026): die fertige Rahmenplan-Zuordnung (eingebettete Tabelle) gibt dieselben Punkte wie
/// die Rechnung mit den Stichwort-Mustern, fuer alle Inhalte (bisherige und alle Fachrichtungen); Gegenproben: ein
/// geaenderter Text oder andere Regeln nutzen die Tabelle nicht und rechnen wie bisher.
/// </summary>
public class RahmenplanVorabTests
{
    private static Lerninhalte Alle()
    {
        var lernen = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");
        byte[]? Lesen(string name) => Path.Combine(lernen, name + ".json") is var pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null;
        return Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte), Lesen, null);
    }

    private static RahmenplanGewicht Gerechnet(Lerninhalte inhalte)
    {
        RahmenplanGewicht.OhneVorabFuerTests.Value = true;
        try
        {
            return new RahmenplanGewicht(inhalte);
        }
        finally
        {
            RahmenplanGewicht.OhneVorabFuerTests.Value = false;
        }
    }

    [Fact]
    public void Tabelle_GleichDerRechnung()
    {
        var inhalte = Alle();
        var gerechnet = Gerechnet(inhalte);
        var soll = RahmenplanVorab.Schreiben(inhalte.RahmenplanZuordnung, gerechnet.Einzeln);
        var datei = Path.Combine(Pfade.Wurzel, "src", "Lernplattform.Core", "Lernen", RahmenplanVorab.Datei);
        if (Environment.GetEnvironmentVariable("LERNPLATTFORM_VORAB_SCHREIBEN") == "1")
        {
            File.WriteAllText(datei, soll);
        }
        Assert.True(File.Exists(datei), "Vorab-Tabelle fehlt: " + datei);
        Assert.True(File.ReadAllText(datei) == soll,
            "Vorab-Tabelle passt nicht zu den Inhalten: neu schreiben mit LERNPLATTFORM_VORAB_SCHREIBEN=1 und diesem Test");
        Assert.NotNull(RahmenplanVorab.Fuer(inhalte.RahmenplanZuordnung));
    }

    [Fact]
    public void MitTabelle_GleichePunkteWieGerechnet_JedeSicht()
    {
        var alle = Alle();
        foreach (var sicht in new[] { alle, alle.Fuer("si"), alle.Fuer("ae"), alle.Fuer("dpa"), alle.Fuer("dv") })
        {
            var gerechnet = Gerechnet(sicht);
            var mitTabelle = new RahmenplanGewicht(sicht);
            Assert.Equal(0, gerechnet.AusVorab);
            Assert.True(mitTabelle.AusVorab > 1000, "Tabelle kaum genutzt: " + mitTabelle.AusVorab);
            Assert.Equal(gerechnet.AllePunkte.Count, mitTabelle.AllePunkte.Count);
            foreach (var (schluessel, punkte) in gerechnet.AllePunkte)
            {
                Assert.Equal(punkte, mitTabelle.AllePunkte[schluessel]);
            }
        }
    }

    [Fact]
    public void Gegenprobe_GeaenderterText_UndAndereRegeln_RechnenWieBisher()
    {
        // Gegenprobe 1: derselbe Inhalt mit anderem Text hat einen anderen Schluessel und steht nicht in der Tabelle
        var inhalte = Alle();
        var tabelle = RahmenplanVorab.Fuer(inhalte.RahmenplanZuordnung)!;
        var karte = inhalte.Karteikarten.First(k => k.Daten["rahmenplan"] is null);
        var felder = new[] { "q", "a", "a_full" }.Select(karte.Text).OfType<string>().ToList();
        Assert.Contains(RahmenplanVorab.Schluessel(Quellen.Karte, karte.Thema, string.Join(" ", felder)), (IDictionary<string, string[]>)tabelle);
        Assert.DoesNotContain(RahmenplanVorab.Schluessel(Quellen.Karte, karte.Thema, string.Join(" ", felder) + " subnetz"),
            (IDictionary<string, string[]>)tabelle);
        // Gegenprobe 2: andere Regeln (ein Muster mehr) = keine Tabelle
        var andere = JsonNode.Parse(inhalte.RahmenplanZuordnung.ToJsonString())!;
        andere["standard_punkt"]!["Probe"] = "A1";
        Assert.Null(RahmenplanVorab.Fuer(andere));
    }
}
