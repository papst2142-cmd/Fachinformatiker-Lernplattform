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

    /// <summary>
    /// Beschriftungen der Kopfleiste mit ihren Menues (Welle Q: Reiter, Pfeile, Menue Projekt, Menue Einstellungen,
    /// „Werkzeug beenden“; S 0.22.0 Reiter „Spielgeschichte“).
    /// </summary>
    private static readonly string[] Kopf =
    [
        Texte.Draufsicht, Texte.Innenraeume, Texte.Ansicht3D, Texte.Spielgeschichte, Texte.GebaeudeEntwerfen, Texte.Rueckgaengig, Texte.Wiederholen,
        Texte.MenueProjekt, Texte.Neu, Texte.Oeffnen, Texte.Speichern, Texte.DateiSpeichern, Texte.AnCcSenden,
        Texte.MenueEinstellungen, Texte.Dunkel, Texte.Vollbild, Texte.UpdateSuchen, Texte.Tastenkuerzel, Texte.WerkzeugBeenden,
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
        foreach (var tipp in new[] { Texte.TippGruppeStrassen, Texte.TippGruppeNatur, Texte.TippGruppeGebaeude, Texte.StueckSuchenTipp, Texte.Hilfe3DTipp, Texte.TippVollerKatalog, Texte.TippHausOhneOrt, Texte.TippMeineBlueprints, Texte.TippOrt, Texte.TippKatalog, Texte.TippGruppe("Regale") })
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

    /// <summary>
    /// Welle Q (Weisung P12-76 Punkt 9): das Fenster „Tastenkürzel“ nennt jede Taste, die das Werkzeug abfragt (jede
    /// Stelle <c>Key.&lt;Name&gt;</c> in der Oberflaeche). Gegenprobe: eine Taste, die fehlt, faellt auf.
    /// </summary>
    [Fact]
    public void Tastenkuerzel_NennenJedeTasteDerOberflaeche()
    {
        var genannt = Texte.TastenListe.SelectMany(t => t.Tasten).ToHashSet(StringComparer.Ordinal);
        var abgefragt = AbgefragteTasten();
        Assert.True(abgefragt.Count >= 15, "nur " + abgefragt.Count + " Tasten gefunden");
        Assert.Empty(abgefragt.Where(t => !genannt.Contains(t)).Order(StringComparer.Ordinal));
        // jede genannte Taste fragt das Werkzeug auch ab (keine Zeile ohne Wirkung)
        Assert.Empty(genannt.Where(t => !abgefragt.Contains(t)).Order(StringComparer.Ordinal));
        // Gegenprobe: ohne die Zeile „M“ fehlt eine Taste
        var ohneM = Texte.TastenListe.Where(t => t.Taste != "M").SelectMany(t => t.Tasten).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("M", abgefragt.Where(t => !ohneM.Contains(t)));
    }

    /// <summary>Tasten aus der Oberflaeche des Werkzeugs; die Kamera-Tasten der 3D-Ansicht kommen aus der Tastenbelegung des Spiels.</summary>
    private static HashSet<string> AbgefragteTasten()
    {
        var ordner = Path.Combine(Daten.Wurzel, "src", "Stadtbau.App", "Oberflaeche");
        var tasten = new HashSet<string>(StringComparer.Ordinal);
        foreach (var datei in Directory.GetFiles(ordner, "*.cs"))
        {
            foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(datei), @"\bKey\.([A-Z][A-Za-z0-9]*)\b"))
            {
                tasten.Add(m.Groups[1].Value);
            }
        }
        // 3D-Ansicht: Drei.Taste nutzt die Tastenbelegung des Spiels (Q dreht, + und - zoomen, Pfeile schieben); E ist im
        // Werkzeug „Einrasten“ und Eingabe (hineingehen) gibt es im Werkzeug nicht
        foreach (var t in new[] { "Q", "OemPlus", "Add", "OemMinus", "Subtract" })
        {
            tasten.Add(t);
        }
        return tasten;
    }

    /// <summary>Welle Q (Punkt 6): die offene Gruppe links bleibt gemerkt; unbekannte oder kaputte Werte geben die Vorgabe.</summary>
    [Fact]
    public void OffeneGruppe_WirdGemerkt()
    {
        var o = Daten.NeueAblage();
        string[] bekannt = ["strassen", "natur", "wasser", "gebaeude"];
        Assert.Equal("strassen", WerkzeugEinstellungen.OffeneGruppe(o, bekannt, "strassen"));
        WerkzeugEinstellungen.OffeneGruppeSetzen(o, "wasser");
        WerkzeugEinstellungen.VollbildSetzen(o, true);
        Assert.Equal("wasser", WerkzeugEinstellungen.OffeneGruppe(o, bekannt, "strassen"));
        Assert.True(WerkzeugEinstellungen.Vollbild(o));
        WerkzeugEinstellungen.OffeneGruppeSetzen(o, "");
        Assert.Equal("", WerkzeugEinstellungen.OffeneGruppe(o, bekannt, "strassen"));
        // Gegenprobe: eine Gruppe, die es nicht (mehr) gibt, und kein Text
        WerkzeugEinstellungen.OffeneGruppeSetzen(o, "bahn");
        Assert.Equal("strassen", WerkzeugEinstellungen.OffeneGruppe(o, bekannt, "strassen"));
        File.WriteAllText(WerkzeugEinstellungen.Datei(o), "{\"gruppe_links\": 3}");
        Assert.Equal("strassen", WerkzeugEinstellungen.OffeneGruppe(o, bekannt, "strassen"));
    }
}
