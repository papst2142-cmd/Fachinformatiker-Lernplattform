using System.Security.Cryptography;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.23.0 (Weisung P12-96, Sammelliste 242): ein Projekt loeschen. Vorher kommt die Datei als Kopie in die
/// Sicherungen des Projekts; nur die Projektdatei verschwindet, alles andere (Sicherungen, Blueprints, Exporte, andere
/// Projekte) bleibt bytegleich. Scheitert die Kopie, bleibt die Datei.
/// </summary>
public class ProjektLoeschenTests
{
    private static readonly DateTime Zeit = new(2026, 11, 8, 20, 30, 15);

    private static Dictionary<string, string> Pruefsummen(Werkzeugordner ablage) =>
        Directory.GetFiles(ablage.Wurzel, "*", SearchOption.AllDirectories)
            .ToDictionary(f => Path.GetRelativePath(ablage.Wurzel, f), f => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(f))), StringComparer.Ordinal);

    /// <summary>Ablage mit zwei Projekten, einer zweiten Sicherung, einem Blueprint und einer Export-Datei.</summary>
    private static (Werkzeugordner Ablage, Projekt P) Gefuellt()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Stadt 1 neu", Daten.Spiel, ablage);
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        p.Speichern(vonHand: true);
        Projekt.Neu("Anderes Projekt", Daten.Spiel, ablage);
        Directory.CreateDirectory(ablage.Blueprints);
        File.WriteAllText(Path.Combine(ablage.Blueprints, "Haus.fisblueprint"), "{}");
        Directory.CreateDirectory(ablage.Exporte);
        File.WriteAllText(Path.Combine(ablage.Exporte, "stadt1_x.fistadt"), "{}");
        return (ablage, p);
    }

    [Fact]
    public void Loeschen_KopieInDenSicherungen_NurDieProjektdateiFehlt_AllesAndereBleibt()
    {
        var (ablage, p) = Gefuellt();
        var datei = p.Datei;
        var inhalt = File.ReadAllBytes(datei);
        var vorher = Pruefsummen(ablage);
        var sicherungenVorher = new Sicherungen(ablage, p.Name).Liste();
        Assert.Equal(2, sicherungenVorher.Count);

        var kopie = Projekt.Loeschen(ablage, datei, Zeit);

        Assert.False(File.Exists(datei));
        Assert.Equal(Path.Combine(ablage.Sicherungen, "Stadt 1 neu", "geloescht_2026-11-08_203015.fisbau"), kopie);
        Assert.Equal(inhalt, File.ReadAllBytes(kopie));
        var nachher = Pruefsummen(ablage);
        var rel = Path.GetRelativePath(ablage.Wurzel, datei);
        // genau eine Datei weg (das Projekt), genau eine dazu (die Kopie), alles andere bytegleich
        Assert.Equal(vorher.Where(e => e.Key != rel), nachher.Where(e => e.Key != Path.GetRelativePath(ablage.Wurzel, kopie)));
        Assert.Equal(sicherungenVorher, new Sicherungen(ablage, p.Name).Liste());
        Assert.Equal(["Anderes Projekt"], Projekt.Liste(ablage).Select(Projekt.AnzeigeName));
    }

    [Fact]
    public void KopieScheitert_DateiBleibt_Gegenprobe_OhneSperreGehtEs()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Gesperrt", Daten.Spiel, ablage);
        var datei = p.Datei;
        // Sicherungsordner des Projekts durch eine Datei gleichen Namens versperren: die Kopie kann nicht entstehen
        var ordner = new Sicherungen(ablage, p.Name).Ordner;
        Directory.Delete(ordner, recursive: true);
        File.WriteAllText(ordner, "versperrt");
        Assert.ThrowsAny<IOException>(() => Projekt.Loeschen(ablage, datei, Zeit));
        Assert.True(File.Exists(datei));
        // Gegenprobe: Sperre weg, dann geht es
        File.Delete(ordner);
        Projekt.Loeschen(ablage, datei, Zeit);
        Assert.False(File.Exists(datei));
    }

    [Fact]
    public void KaputteDatei_LaesstSichLoeschen_KopieUnterDemDateinamen()
    {
        var ablage = Daten.NeueAblage();
        var datei = Path.Combine(ablage.Projekte, "kaputt.fisbau");
        File.WriteAllText(datei, "kein JSON {");
        Assert.Throws<StadtbauFehler>(() => Projekt.Oeffnen(datei, Daten.Spiel, ablage));
        var kopie = Projekt.Loeschen(ablage, datei, Zeit);
        Assert.False(File.Exists(datei));
        Assert.Equal(Path.Combine(ablage.Sicherungen, "kaputt", Projekt.LoeschKopieName(Zeit)), kopie);
        Assert.Equal("kein JSON {", File.ReadAllText(kopie));
    }

    [Fact]
    public void GleicherName_DanachNeuAnlegbar_ZweitesLoeschenMitNummer()
    {
        var ablage = Daten.NeueAblage();
        var datei = Projekt.Neu("Probe", Daten.Spiel, ablage).Datei;
        var k1 = Projekt.Loeschen(ablage, datei, Zeit);
        var neu = Projekt.Neu("Probe", Daten.Spiel, ablage);
        Assert.Equal(datei, neu.Datei);
        Assert.True(File.Exists(neu.Datei));
        var k2 = Projekt.Loeschen(ablage, neu.Datei, Zeit);
        Assert.NotEqual(k1, k2);
        Assert.EndsWith("geloescht_2026-11-08_203015_2.fisbau", k2, StringComparison.Ordinal);
        Assert.True(File.Exists(k1));
    }

    [Fact]
    public void NurImOrdnerProjekte_SonstNichts()
    {
        var ablage = Daten.NeueAblage();
        Directory.CreateDirectory(ablage.Exporte);
        var fremd = Path.Combine(ablage.Exporte, "x.fisbau");
        File.WriteAllText(fremd, "{}");
        Assert.ThrowsAny<IOException>(() => Projekt.Loeschen(ablage, fremd, Zeit));
        Assert.True(File.Exists(fremd));
        var blueprint = Path.Combine(ablage.Projekte, "x.fisblueprint");
        File.WriteAllText(blueprint, "{}");
        Assert.ThrowsAny<IOException>(() => Projekt.Loeschen(ablage, blueprint, Zeit));
        Assert.True(File.Exists(blueprint));
    }

    [Fact]
    public void SpeichernOhneSicherung_ErsetztKeinenPlatz()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Ohne", Daten.Spiel, ablage);
        var vorher = new Sicherungen(ablage, p.Name).Liste();
        p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
        p.Speichern(vonHand: true, ohneSicherung: true);
        Assert.Equal(vorher, new Sicherungen(ablage, p.Name).Liste());
        Assert.False(p.Ungespeichert);
        // Gegenprobe: von Hand mit Sicherung legt eine an
        p.Speichern(vonHand: true);
        Assert.Equal(vorher.Count + 1, new Sicherungen(ablage, p.Name).Liste().Count);
    }
}
