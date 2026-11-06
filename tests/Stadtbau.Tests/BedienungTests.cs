using Lernplattform.Core.Daten;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-35, Funde von Nico am Werkzeug S 0.5.0): "Werkzeug beenden" fragt nur nach, wenn eine
/// Aenderung nicht gespeichert ist (das sofortige Speichern scheiterte), und die Beschriftungen sagen, was passiert.
/// </summary>
public class BedienungTests
{
    private static Spieldaten D => Daten.Spiel;

    [Fact]
    public void Ungespeichert_NurWennSpeichernScheitert()
    {
        var p = Projekt.Neu("Beenden", D, Daten.NeueAblage(), "moebelhaus");
        Assert.False(p.Ungespeichert);
        // Gegenprobe: eine normale Aenderung wird sofort gespeichert, keine Rueckfrage noetig
        p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich"));
        Assert.False(p.Ungespeichert);

        // Projektdatei gesperrt (an ihrer Stelle liegt ein Ordner): das Speichern scheitert, die Aenderung ist offen
        File.Delete(p.Datei);
        Directory.CreateDirectory(p.Datei);
        // Nachbesserung S 0.5.1 (P12-40 Punkt 5): der Schritt steht im Verlauf, "Geaendert" kommt an (Knoepfe der
        // Oberflaeche), der Fehler steht in SpeicherFehler und fehler.log; vorher lief er bis zur Oberflaeche
        var gemeldet = 0;
        p.Geaendert += () => gemeldet++;
        var zurueck = p.SchritteZurueck;
        p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "fliesen"));
        Assert.Equal(1, gemeldet);
        Assert.Equal(zurueck + 1, p.SchritteZurueck);
        Assert.NotNull(p.SpeicherFehler);
        Assert.True(p.Ungespeichert);
        Assert.Contains("Automatisch speichern", File.ReadAllText(p.Werkzeugordner.FehlerLog), StringComparison.Ordinal);
        // Gegenprobe: wie vorher (Linux/macOS IOException, Windows UnauthorizedAccessException), "Geaendert" bleibt aus
        Projekt.SpeichernAltFuerTests = true;
        try
        {
            var fehler = Record.Exception(() => p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "teppich")));
            Assert.True(fehler is IOException or UnauthorizedAccessException, fehler?.GetType().Name ?? "kein Fehler");
            Assert.Equal(1, gemeldet);
        }
        finally
        {
            Projekt.SpeichernAltFuerTests = false;
        }
        p.Aendern("moebelhaus", "Boden", g => Bearbeitung.Boden(g, "flur", "fliesen"));
        Assert.True(p.Ungespeichert);

        // wieder frei: "Speichern und beenden" schreibt sie, danach ist nichts mehr offen
        Directory.Delete(p.Datei);
        p.Speichern(vonHand: true);
        Assert.False(p.Ungespeichert);
        Assert.Contains("fliesen", File.ReadAllText(p.Datei), StringComparison.Ordinal);
    }

    [Fact]
    public void Ungespeichert_NieBeiNurLesen()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Nur lesen", D, ablage, "moebelhaus");
        var text = File.ReadAllText(p.Datei).Replace("\"formatversion\": " + Projekt.FormatVersion, "\"formatversion\": " + (Projekt.FormatVersion + 1), StringComparison.Ordinal);
        File.WriteAllText(p.Datei, text);
        var q = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.True(q.NurLesen);
        Assert.False(q.Ungespeichert);
    }

    /// <summary>Beschriftungen der Kopfleiste (Reihenfolge wie im Werkzeug).</summary>
    private static readonly string[] Kopf =
    [
        Texte.Draufsicht, Texte.Innenraeume, Texte.Ansicht3D, Texte.Neu, Texte.Oeffnen, Texte.Speichern, Texte.Rueckgaengig,
        Texte.Wiederholen, Texte.DateiSpeichern, Texte.AnCcSenden, Texte.Vollbild, Texte.Dunkel, Texte.WerkzeugBeenden,
    ];

    /// <summary>Woerter, die allein nicht sagen, womit ein Knopf arbeitet (Nico: "Neu" kann auch ein Gebaeude meinen).</summary>
    private static readonly string[] Ungenau = ["Neu", "Öffnen", "Speichern", "Datei speichern", "An CC senden", "Neues Gebäude", "Anlegen", "Hell", "Dunkel"];

    private static List<string> UngenaueKnoepfe(IEnumerable<string> beschriftungen) =>
        [.. beschriftungen.Where(b => Ungenau.Contains(b, StringComparer.Ordinal))];

    [Fact]
    public void Kopfleiste_KeinUngenauerKnopf()
    {
        Assert.Empty(UngenaueKnoepfe(Kopf.Append(Texte.Anlegen).Append(Texte.NeuesGebaeude).Append(Texte.Hell)));
        // Gegenprobe: die Beschriftungen von S 0.5.0 sind rot
        Assert.Equal(6, UngenaueKnoepfe(["Neu", "Öffnen", "Speichern", "Datei speichern", "An CC senden", "Dunkel", "Draufsicht"]).Count);
    }

    [Fact]
    public void Kopfleiste_JederKnopfHatEinenSatzAlsTipp()
    {
        Assert.Equal(Kopf.Order(StringComparer.Ordinal), Texte.KopfTipps.Keys.Order(StringComparer.Ordinal));
        foreach (var (knopf, tipp) in Texte.KopfTipps)
        {
            Assert.True(EinSatz(tipp), knopf + ": " + tipp);
        }
        foreach (var tipp in new[] { Texte.TippAussenKatalog, Texte.TippVollerKatalog, Texte.TippHausOhneOrt, Texte.TippMeineBlueprints, Texte.TippOrt, Texte.TippKatalog, Texte.TippGruppe("Regale") })
        {
            Assert.True(EinSatz(tipp), tipp);
        }
        // Gegenprobe der Satz-Regel
        Assert.False(EinSatz("Neu"));
        Assert.False(EinSatz("Erster Satz. Zweiter Satz."));
    }

    /// <summary>Ein ganzer Satz: beginnt gross, endet mit Punkt, kein weiterer Satz dahinter.</summary>
    private static bool EinSatz(string text) =>
        text.Length > 10 && char.IsUpper(text[0]) && text.EndsWith('.') && !text[..^1].Contains(". ", StringComparison.Ordinal);

    [Fact]
    public void Start_ErklaertWasEinProjektIst()
    {
        Assert.Contains("Ein Projekt ist", Texte.WasIstEinProjektText, StringComparison.Ordinal);
        Assert.Contains("Stadt von oben umbauen", Texte.WasIstEinProjektText, StringComparison.Ordinal);
    }
}
