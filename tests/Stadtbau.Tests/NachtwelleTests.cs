using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Welle S 0.47.0 (Weisung P12-226): Fund 306 (leeres <c>laternen_auto</c> nach Rueckgaengig), Fund 320 (Funde nennen Namen
/// des Probestands), W2 „Laterne versetzen“, W6 „Frühere Fassungen“. Je mit Gegenprobe.
/// </summary>
public class NachtwelleTests
{
    private static Spieldaten D => Daten.Spiel;

    private static Projekt Leer(string name, Werkzeugordner? ablage = null) => Projekt.Neu(name, D, ablage ?? Daten.NeueAblage(), null, (30, 20));

    private static AussenStelle Setzen(Projekt p, AussenArt art, double x, double y, string? modell = null) =>
        p.AussenAendern("Setzen", a => AussenBearbeitung.Setzen(a, art, x, y, modell: modell))!;

    // ---- 306 ------------------------------------------------------------------------------------------

    [Fact]
    public void Fund306_RueckgaengigOhneLeerenTeil()
    {
        static (string Vorher, string Nachher) Lauf()
        {
            var p = Projekt.Neu("306", D, Daten.NeueAblage());
            var vorher = BelastungsprobenTests.Vergleich(p.AussenStand);
            var strasse = new AussenStelle(AussenArt.Strasse, Aussen.Landschaft(p.AussenStand).FindIndex(t => t is PyObjekt o && Py.Gleich(o["typ"], "strasse")));
            p.AussenAendern("Schieben", a => AussenBearbeitung.Verschieben(a, strasse, 0, 0.5));
            Assert.True(p.Rueckgaengig());
            return (vorher, BelastungsprobenTests.Vergleich(p.AussenStand));
        }
        var (v, n) = Lauf();
        Assert.Equal(v, n);
        Assert.DoesNotContain("\"" + LaternenAutomatik.Teil + "\"", n, StringComparison.Ordinal);

        // Gegenprobe: wie bis S 0.46.1 bleibt ein leerer Teil stehen
        Aussen.LeerAltFuerTests = true;
        try
        {
            var (va, na) = Lauf();
            Assert.NotEqual(va, na);
            Assert.Contains("\"" + LaternenAutomatik.Teil + "\": {}", na, StringComparison.Ordinal);
        }
        finally
        {
            Aussen.LeerAltFuerTests = false;
        }
    }

    // ---- 320 ------------------------------------------------------------------------------------------

    [Fact]
    public void Fund320_ErsatzNenntNamenDerKarte_Gegenprobe()
    {
        var stand = D.Fassung;
        var ersatz = ProbeNamen.Ersatz(stand, D.Inhalt);
        Assert.Equal("Gewerbehof Am Stellwerk", ersatz["Probe"]);
        Assert.True(ersatz.Values.All(n => n.Length > 0));
        Assert.DoesNotContain(ersatz.Values, n => n == n.ToLowerInvariant() && !n.Contains(' ', StringComparison.Ordinal));
        var f = new StadtbauFund("„Probe“ ist zu Fuß nicht erreichbar (von „Zuhause“ aus).", "", true, 1, 2);
        Assert.Equal("„Gewerbehof Am Stellwerk“ ist zu Fuß nicht erreichbar (von „Zuhause“ aus).", ProbeNamen.Umbenennen(ersatz, f).Text);
        // nur ganze Namen in Anfuehrungszeichen
        var g = new StadtbauFund("Probelauf an „Zuhause“", "", true, null, null);
        Assert.Same(g, ProbeNamen.Umbenennen(ersatz, g));
        Assert.Equal("Filiale", ProbeNamen.Lesbar("filiale"));
        Assert.Equal("Bahnhof neu", ProbeNamen.Lesbar("bahnhof_neu"));

        ProbeNamen.AltFuerTests = true;
        try
        {
            Assert.Empty(ProbeNamen.Ersatz(stand, D.Inhalt));
        }
        finally
        {
            ProbeNamen.AltFuerTests = false;
        }
    }

    [Fact]
    public void Fund320_ErreichbarkeitInNicosTeststadt_NenntKeinenProbenamen()
    {
        var p = TorEingangTests.Nico();
        foreach (var (modell, ort) in SpielortTests.Zuordnung)
        {
            Assert.Null(SpielortTests.Festlegen(p, modell, ort));
        }
        var probe = ProbeNamen.Ersatz(Aussen.Fassung(p.AussenStand, p.Basis, p.Innenraeume(), 1, 0, Werkzeug.Kennzeile, "", ""), D.Inhalt);
        var texte = p.PruefenAussen().Select(f => f.Text).ToList();
        foreach (var alt in probe.Keys)
        {
            Assert.DoesNotContain(texte, t => t.Contains("„" + alt + "“", StringComparison.Ordinal));
        }
        ProbeNamen.AltFuerTests = true;
        try
        {
            var alteTexte = p.PruefenAussen().Select(f => f.Text).ToList();
            Assert.Contains(alteTexte, t => probe.Keys.Any(alt => t.Contains("„" + alt + "“", StringComparison.Ordinal)));
        }
        finally
        {
            ProbeNamen.AltFuerTests = false;
        }
    }

    [Fact]
    public void Fund320_TuerImLaubengang_Gewerbehof_NichtGlatt_GegenprobeTiefeAlt()
    {
        // Welle S 0.47.0 (selbst gefunden): mit Fund 320 nennt der rote Fund den Gewerbehof beim Namen; vorher hiess er „Probe“
        // und der Test der Tuer-Welle filterte ihn weg (falsches Gruen). Die Stelle hinter dem Laubengang ist jetzt nicht glatt.
        static List<string> Lauf()
        {
            var p = Projekt.Neu("Laubengang", D, Daten.NeueAblage());
            p.AussenAendern("Laterne 20", Daten.Laterne20Frei);
            var ort = new AussenStelle(AussenArt.Haus, -1, 0, "gewerbehof");
            var platz = TuerFassade.Rechnen(p, p.AussenStand, ort)!.Finden(new Vector2(21.4f, 15.1f), null, einrasten: true);
            p.AussenAendern("Tuer", a => TuerSetzen.SetzenAn(a, ort, platz));
            return [.. p.PruefenAussen().Where(f => f.Rot && f.Text.Contains("liegt nicht auf der Wand", StringComparison.Ordinal)).Select(f => f.Text)];
        }
        Assert.Empty(Lauf());
        TuerFassade.TiefeAltFuerTests = true;
        try
        {
            Assert.Contains("Die Tür von „Gewerbehof Am Stellwerk“ liegt nicht auf der Wand des Hauses.", Lauf());
        }
        finally
        {
            TuerFassade.TiefeAltFuerTests = false;
        }
    }

    // ---- W2 -------------------------------------------------------------------------------------------

    private static Projekt MitStrasseUndLaterne(out int nummer, double lx, double ly)
    {
        var p = Leer("W2");
        Setzen(p, AussenArt.Strasse, 15, 10);
        nummer = p.AussenAendern("Laterne", a =>
        {
            Aussen.Laternen(a).Add(new List<object?> { lx, ly, false });
            return Aussen.Laternen(a).Count - 1;
        });
        return p;
    }

    private static (LaterneVersetzen.Vorschlag? V, string? Grund) Suchen(Projekt p, int nummer) =>
        LaterneVersetzen.Suchen((PyObjekt)Py.Kopie(p.AussenStand)!, p.Basis, p.Innenraeume(), D, p.SzeneOhneLaternen, nummer, TestContext.Current.CancellationToken);

    [Fact]
    public void LaterneVersetzen_RoterFund_Vorschlag_EinSchritt_DanachOhneFund()
    {
        var p = MitStrasseUndLaterne(out var nr, 15, 10.1);
        var fund = p.PruefenAussen().FirstOrDefault(f => LaterneVersetzen.Nummer(p.AussenStand, f) == nr);
        Assert.True(fund is not null, string.Join(" | ", p.PruefenAussen().Select(f => f.Text)));
        var (v, grund) = Suchen(p, nr);
        Assert.Null(grund);
        Assert.NotNull(v);
        Assert.True(Vector2.Distance(v!.Von, v.Nach) < 3);
        var schritte = p.SchritteZurueck;
        Assert.True(p.AussenAendern(Texte.LaterneVersetzenSchritt, a => LaterneVersetzen.Anwenden(a, v)));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
        Assert.DoesNotContain(p.PruefenAussen(), f => LaterneVersetzen.Nummer(p.AussenStand, f) is not null);
        // veraltet: noch einmal anwenden tut nichts
        Assert.False(p.AussenAendern(Texte.LaterneVersetzenSchritt, a => LaterneVersetzen.Anwenden(a, v)));
        Assert.Equal(schritte + 1, p.SchritteZurueck);
    }

    [Fact]
    public void LaterneVersetzen_OhneFreieStelle_Grund()
    {
        // Gegenprobe: ohne jede freie Stelle (kein Platz fuer die Suche) kommt der Grund, kein Vorschlag
        var p = MitStrasseUndLaterne(out var nr, 15, 10.1);
        var (v, grund) = LaterneVersetzen.Suchen((PyObjekt)Py.Kopie(p.AussenStand)!, p.Basis, p.Innenraeume(), D, _ => null, nr, TestContext.Current.CancellationToken);
        Assert.Null(v);
        Assert.Equal(Texte.LaterneKeinPlatz, grund);
        Assert.Equal(Texte.LaterneVersetzenWeg, Suchen(p, 99).Grund);
    }

    // ---- W6 -------------------------------------------------------------------------------------------

    [Fact]
    public void FruehereFassung_AlsNeuesProjekt_OriginalUndSicherungBleiben()
    {
        var ablage = Daten.NeueAblage();
        var p = Leer("Fassungen", ablage);
        Setzen(p, AussenArt.Baum, 4, 4);
        p.Speichern(vonHand: true);
        var sicherungen = new Sicherungen(ablage, p.Name);
        var datei = sicherungen.Anlegen(File.ReadAllBytes(p.Datei), new DateTime(2026, 11, 12, 22, 5, 0));
        Setzen(p, AussenArt.Baum, 8, 4);
        p.Speichern(vonHand: true);
        var original = File.ReadAllBytes(p.Datei);
        var sicherung = File.ReadAllBytes(datei);

        var liste = Projekt.Fassungen(ablage, p.Name);
        Assert.Contains(liste, f => f.Datei == datei);
        var name = Projekt.NameAusSicherung(p.Name, new DateTime(2026, 11, 12, 22, 5, 0), ablage);
        Assert.Equal("Fassungen vom 12-11 22-05", name);
        var neu = Projekt.AusSicherung(datei, D, ablage, name);
        Assert.Equal(name, neu.Name);
        Assert.Equal(1, Aussen.Landschaft(neu.AussenStand).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "baum")).Sum(t => Liste(t["punkte"]).Count));
        Assert.Equal(original, File.ReadAllBytes(p.Datei));
        Assert.Equal(sicherung, File.ReadAllBytes(datei));
        // nie ueberschreiben: gleicher Name nochmal = Fehler, neuer Vorschlag bekommt eine Nummer
        Assert.Throws<IOException>(() => Projekt.AusSicherung(datei, D, ablage, name));
        Assert.Equal(name + " 2", Projekt.NameAusSicherung(p.Name, new DateTime(2026, 11, 12, 22, 5, 0), ablage));
        Assert.Throws<IOException>(() => Projekt.AusSicherung(datei, D, ablage, p.Name));
        Assert.Equal(original, File.ReadAllBytes(p.Datei));
    }

    [Fact]
    public void FruehereFassung_KeineSicherung_LeereListe()
    {
        var ablage = Daten.NeueAblage();
        Assert.Empty(Projekt.Fassungen(ablage, "Gibt es nicht"));
    }
}
