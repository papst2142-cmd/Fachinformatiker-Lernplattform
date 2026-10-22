using System.Security.Cryptography;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pruefung;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W2: Fachrichtung als reservierte Zeile zz-einstellungen/fachrichtung in abschlussprojekt
/// (Weisung P4-6 Frage 1, Verhaltenstabelle und Testplan P4-5 Teil A Abschnitte 3 und 5). Die Zeile
/// reist mit Abgleich, Sicherung und Uebernahme wie jedes Projektfeld (neueste Fassung gewinnt,
/// gleiche Mikrosekunde: groessere uid), zaehlt aber nie als Projekt (Frage 2 a). Nur synthetische
/// Daten (A3).
/// </summary>
public class AusbildungTests
{
    private const string Repo = "pruefer/lernstand-test";
    private const string Token = "ghp_TestSchluessel0123456789abcdefghijklmn";
    private static readonly DateTimeOffset Zeit = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> KeineUmbenennungen = [];

    /// <summary>Ein Geraet mit eigenem Ordner, fester Uhr und fester Kennung fuer neue Zeilen.</summary>
    private sealed class Geraet : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Geraet(string name, string kennung = "")
        {
            Name = name;
            Uhr = SpeichernTests.FesteUhr(() => Jetzt);
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            Einstellungen = new Einstellungen(_ordner.Pfad);
            Einstellungen.Setze(Abgleich.RepoSchluessel, Repo);
            Einstellungen.Setze(Abgleich.TokenSchluessel, Token);
            var zaehler = 0;
            Schreiber = new LernstandSchreiber(Lernstand, Uhr,
                kennung.Length > 0 ? () => kennung + "-" + zaehler++ : null, new Uhrabweichung(() => 1000.0, "start", "prozess"));
        }

        public string Name { get; }

        public DateTimeOffset Jetzt { get; set; } = Zeit;

        public Uhr Uhr { get; }

        public Lernstand Lernstand { get; }

        public Einstellungen Einstellungen { get; }

        public LernstandSchreiber Schreiber { get; }

        public string Ordner => _ordner.Pfad;

        public void Abgleichen(GitHubAttrappe attrappe)
        {
            using var netz = new GitHubZugang(attrappe.Basis, TimeSpan.FromSeconds(10));
            Abgleich.Ausfuehren(Lernstand, Einstellungen, Name, netz, KeineUmbenennungen, Uhr);
        }

        public List<object?[]> Zeilen() =>
            Lernstand.Abfragen("SELECT wert, uid, timestamp FROM abschlussprojekt WHERE projekt = 'zz-einstellungen'");

        public void Dispose() => _ordner.Dispose();
    }

    private static GitHubAttrappe Attrappe()
    {
        var a = new GitHubAttrappe();
        a.Datum = () => Zeit;
        return a;
    }

    private static UebernahmeBericht Einspielen(Geraet g, byte[] sicherung)
    {
        var datei = Path.Combine(g.Ordner, "einspielen" + Ruecksicherung.Endung);
        File.WriteAllBytes(datei, sicherung);
        return Uebernahme.Uebernehmen(g.Lernstand, Uebernahme.Lesen(UebernahmeQuelle.Sicherung, datei), KeineUmbenennungen, g.Uhr);
    }

    private static byte[] Sicherung(Geraet g) => Ruecksicherung.Erstellen(g.Lernstand, [], "A 0.9.0", g.Name, new DateTime(2026, 10, 22, 12, 0, 0));

    // -- Verhaltenstabelle P4-5 Abschnitt 3 ------------------------------------------------

    [Fact]
    public void NieGewaehlt_KeineZeile_GiltSi()
    {
        using var g = new Geraet("PC");
        Assert.False(Ausbildung.Gewaehlt(g.Schreiber));
        Assert.Null(Ausbildung.Gespeichert(g.Schreiber));
        Assert.Equal("si", Ausbildung.Aktuell(g.Schreiber));
        Assert.Empty(g.Zeilen());
        // auch Pruefen, Start und Abschluss einer Klausur schreiben keine Zeile
        var ablage = new LaufendePruefung(() => [], _ => true);
        var ablauf = new KlausurAblauf(KlausurTests.Klausurinhalte, ablage, () => g.Schreiber, () => g.Jetzt, KlausurTests.Berlin);
        ablauf.Starten(Klausur.Wiso);
        Assert.Empty(g.Zeilen());
    }

    [Fact]
    public void Waehlen_GenauEineZeile_NeuesteGilt()
    {
        using var g = new Geraet("PC", "b");
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "ae"));
        Assert.Equal("ae", Ausbildung.Aktuell(g.Schreiber));
        // zweite Wahl in derselben Sekunde (1 ms spaeter)
        g.Jetzt = g.Jetzt.AddMilliseconds(1);
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "dv"));
        var zeilen = g.Zeilen();
        Assert.Single(zeilen);                                  // Kontrollfehler "Aufraeumen vergessen": zwei Zeilen
        Assert.Equal("dv", zeilen[0][0]);
        Assert.Equal(26, ((string)zeilen[0][2]!).Length);      // Mikrosekunden (Kontrollfehler "grober Stempel")
        Assert.True(Ausbildung.Gewaehlt(g.Schreiber));
    }

    /// <summary>
    /// Kontrollfehler "grober Stempel in Sekunden": die erste Wahl hat die groessere uid; mit einem
    /// Stempel in Sekunden waeren beide gleich alt und die erste gewaenne.
    /// </summary>
    [Fact]
    public void ZweiWahlenInDerselbenSekunde_DieSpaetereGilt()
    {
        using var g = new Geraet("PC");
        var kennungen = new Queue<string>(["z-erste", "a-zweite"]);
        var schreiber = new LernstandSchreiber(g.Lernstand, g.Uhr, kennungen.Dequeue);
        Assert.True(Ausbildung.Waehlen(schreiber, "ae"));
        g.Jetzt = g.Jetzt.AddTicks(10);     // 1 Mikrosekunde
        Assert.True(Ausbildung.Waehlen(schreiber, "dpa"));
        Assert.Equal("dpa", Ausbildung.Aktuell(schreiber));
        Assert.Equal("a-zweite", Assert.Single(g.Zeilen())[1]);
    }

    [Fact]
    public void UnbekannterWert_GiltSi_WirdNichtUeberschrieben()
    {
        using var g = new Geraet("PC");
        Assert.True(g.Schreiber.ProjektFeldSpeichern(Ausbildung.Zeile, Ausbildung.Feld, "neu-in-version-9"));
        Assert.Null(Ausbildung.Gespeichert(g.Schreiber));
        Assert.Equal("si", Ausbildung.Aktuell(g.Schreiber));
        Assert.True(Ausbildung.Gewaehlt(g.Schreiber));          // W4 fragt nicht
        Assert.False(Ausbildung.Waehlen(g.Schreiber, "xx"));     // unbekannt waehlen geht nicht
        Assert.Equal("neu-in-version-9", Assert.Single(g.Zeilen())[0]);
        // eine Klausur lesen, starten und abschliessen laesst ihn stehen
        var werte = new JsonObject();
        var ablage = new LaufendePruefung(() => werte.DeepClone().AsObject(), aendern => { aendern(werte); return true; });
        var ablauf = new KlausurAblauf(KlausurTests.Klausurinhalte, ablage, () => g.Schreiber, () => g.Jetzt, KlausurTests.Berlin);
        ablauf.Starten(Klausur.Wiso);
        Assert.Equal(KlausurAnsicht.Ergebnis, ablauf.Abgeben(auto: false).Ansicht);
        Assert.Equal("neu-in-version-9", Assert.Single(g.Zeilen())[0]);
        // nur eigenes Waehlen ueberschreibt
        g.Jetzt = g.Jetzt.AddSeconds(1);
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "ae"));
        Assert.Equal("ae", Assert.Single(g.Zeilen())[0]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ZweiGeraete_JuengereWahlGewinnt_BeideReihenfolgen(bool handyZuerst)
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC");
        using var handy = new Geraet("Handy");
        Assert.True(Ausbildung.Waehlen(pc.Schreiber, "ae"));
        handy.Jetzt = Zeit.AddMinutes(5);
        Assert.True(Ausbildung.Waehlen(handy.Schreiber, "dv"));
        var reihe = handyZuerst ? new[] { handy, pc, handy } : [pc, handy, pc];
        foreach (var g in reihe)
        {
            g.Abgleichen(attrappe);
        }
        foreach (var g in new[] { pc, handy })
        {
            Assert.Equal("dv", Ausbildung.Aktuell(g.Schreiber));
            Assert.Single(g.Zeilen());
        }
    }

    [Fact]
    public void ZweiGeraete_GleicheMikrosekunde_GroessereUidGewinnt()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC", "uid-b");
        using var handy = new Geraet("Handy", "uid-a");
        Assert.True(Ausbildung.Waehlen(pc.Schreiber, "ae"));
        Assert.True(Ausbildung.Waehlen(handy.Schreiber, "dv"));
        Assert.Equal(pc.Zeilen()[0][2], handy.Zeilen()[0][2]);     // dieselbe Mikrosekunde
        handy.Abgleichen(attrappe);
        pc.Abgleichen(attrappe);
        handy.Abgleichen(attrappe);
        foreach (var g in new[] { pc, handy })
        {
            Assert.Equal("ae", Ausbildung.Aktuell(g.Schreiber));    // "uid-b-0" > "uid-a-0"
            Assert.Equal("uid-b-0", Assert.Single(g.Zeilen())[1]);
        }
    }

    [Fact]
    public void Sicherung_EnthaeltDieZeile_AlteSicherungUeberschreibtNeuereWahlNie()
    {
        using var pc = new Geraet("PC");
        Assert.True(Ausbildung.Waehlen(pc.Schreiber, "ae"));
        var alt = Sicherung(pc);
        Assert.Contains("zz-einstellungen", RuecksicherungTests.Entpacken(alt), StringComparison.Ordinal);
        pc.Jetzt = Zeit.AddHours(1);
        Assert.True(Ausbildung.Waehlen(pc.Schreiber, "dpa"));
        var neu = Sicherung(pc);
        Einspielen(pc, alt);
        Assert.Equal("dpa", Ausbildung.Aktuell(pc.Schreiber));
        Assert.Single(pc.Zeilen());

        // neueres Geraet mit aelterer Wahl: die Sicherung mit der neueren Wahl gewinnt
        using var zweites = new Geraet("Zweites");
        Assert.True(Ausbildung.Waehlen(zweites.Schreiber, "dv"));
        Einspielen(zweites, neu);
        Assert.Equal("dpa", Ausbildung.Aktuell(zweites.Schreiber));
        Assert.Single(zweites.Zeilen());
        // leeres Geraet: die Zeile kommt an
        using var leer = new Geraet("Leer");
        Einspielen(leer, alt);
        Assert.Equal("ae", Ausbildung.Aktuell(leer.Schreiber));
    }

    [Fact]
    public void PythonSicherungUndPythonUebernahme_LassenDenWertStehen()
    {
        using var g = new Geraet("PC");
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "dv"));
        var python = Path.Combine(g.Ordner, "python" + Ruecksicherung.Endung);
        File.Copy(Pfade.Vergleich("sicherung_test.json.gz"), python);
        var bericht = Uebernahme.Uebernehmen(g.Lernstand, Uebernahme.Lesen(UebernahmeQuelle.Sicherung, python), KeineUmbenennungen, g.Uhr);
        Assert.True(bericht.Neu > 0);
        Assert.Equal("dv", Ausbildung.Aktuell(g.Schreiber));

        var alt = UebernahmeTests.BaueDatenbank(VergleichsHilfe.Lade("uebernahme.json").GetProperty("quelle_db"),
            Path.Combine(Directory.CreateDirectory(Path.Combine(g.Ordner, "alt")).FullName, "fisi_lernplattform.db"));
        Uebernahme.Uebernehmen(g.Lernstand, Uebernahme.Lesen(UebernahmeQuelle.AlteDatenbank, alt), KeineUmbenennungen, g.Uhr);
        Assert.Equal("dv", Ausbildung.Aktuell(g.Schreiber));
        Assert.Single(g.Zeilen());
    }

    /// <summary>"Alle Lerndaten löschen" (reset_at, z. B. von der alten App per Abgleich) laesst die Zeile stehen wie jedes Projektfeld.</summary>
    [Fact]
    public void AlleLerndatenLoeschen_LaesstDieZeileStehen()
    {
        using var attrappe = Attrappe();
        using var pc = new Geraet("PC");
        using var handy = new Geraet("Handy");
        Assert.True(Ausbildung.Waehlen(pc.Schreiber, "ae"));
        pc.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
        pc.Abgleichen(attrappe);
        handy.Abgleichen(attrappe);
        Assert.True(handy.Lernstand.Schreiben("INSERT OR REPLACE INTO sync_meta (key, value) VALUES ('reset_at', '2026-10-22 13:00:00+02:00')"));
        Assert.True(handy.Lernstand.Schreiben("DELETE FROM card_events"));
        handy.Abgleichen(attrappe);
        pc.Abgleichen(attrappe);
        Assert.Empty(pc.Lernstand.Abfragen("SELECT 1 FROM card_events"));
        Assert.Equal("ae", Ausbildung.Aktuell(pc.Schreiber));
        Assert.Equal("ae", Ausbildung.Aktuell(handy.Schreiber));
    }

    [Fact]
    public void Lesefehler_WaehlenGibtFalse_GiltSi()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var schreiber = new LernstandSchreiber(new Lernstand(pfad));
        Assert.False(Ausbildung.Waehlen(schreiber, "ae"));
        Assert.Equal("si", Ausbildung.Aktuell(schreiber));
        Assert.False(Ausbildung.Gewaehlt(schreiber));
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
    }

    /// <summary>Schreibgeschuetzte Datei: false, alter Wert bleibt, Datei bytegleich (gelingt nur ohne Root, CI laeuft ohne).</summary>
    [Fact]
    public void Schreibschutz_WaehlenGibtFalse_AlterWertBleibt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        Assert.True(Ausbildung.Waehlen(new LernstandSchreiber(new Lernstand(pfad)), "ae"));
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(pfad, FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(pfad, UnixFileMode.UserRead);
        }
        var vorher = SHA256.HashData(File.ReadAllBytes(pfad));
        var lernstand = new Lernstand(pfad);
        Assert.Equal("schreibschutz", lernstand.NurLesenGrund);
        var schreiber = new LernstandSchreiber(lernstand);
        Assert.False(Ausbildung.Waehlen(schreiber, "dv"));
        Assert.Equal("ae", Ausbildung.Aktuell(schreiber));
        Assert.Equal(vorher, SHA256.HashData(File.ReadAllBytes(pfad)));
    }

    // -- Zaehlung (Frage 2 a): die Zeile ist kein Projekt -----------------------------------

    /// <summary>
    /// C#-Sicherung mit und ohne die Zeile: gespeicherte Zusammenfassung, Vorschau beim Einspielen
    /// (Sicherungszeilen und Bericht der Uebernahme) und SicherungsPruefung. Nur die Rohzahl der
    /// Tabelle abschlussprojekt und "Neu auf diesem Gerät" zaehlen die Zeile (wie jede Projektfeld-Zeile).
    /// </summary>
    [Fact]
    public void Zaehlung_ZeileIstKeinProjekt()
    {
        using var ohne = new Geraet("PC");
        ohne.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
        Assert.True(ohne.Schreiber.ProjektFeldSpeichern("a1b2c3d4e5f6", "titel", "Mein Projekt"));
        using var mit = new Geraet("PC");
        mit.Schreiber.KarteSpeichern("Netzwerk", "Frage A", "karte", true);
        Assert.True(mit.Schreiber.ProjektFeldSpeichern("a1b2c3d4e5f6", "titel", "Mein Projekt"));
        Assert.True(Ausbildung.Waehlen(mit.Schreiber, "ae"));
        var sicherungOhne = Sicherung(ohne);
        var sicherungMit = Sicherung(mit);

        // gespeicherte Zusammenfassung: unveraendert (1 Projekt)
        var zusammen = Ruecksicherung.Zusammenfassung(Ruecksicherung.Lesen(mit.Lernstand));
        Assert.Equal(Ruecksicherung.Zusammenfassung(Ruecksicherung.Lesen(ohne.Lernstand)).Select(e => (e.Key, e.Value)),
            zusammen.Select(e => (e.Key, e.Value)));
        Assert.Equal(1L, zusammen["projekte"]);

        // Vorschau beim Einspielen: dieselben Zeilen bis auf Erstellt/Geraet
        Assert.Equal(UebernahmeAblauf.SicherungsZeilen(sicherungOhne), UebernahmeAblauf.SicherungsZeilen(sicherungMit));
        Assert.Contains("Abschlussprojekte: 1", UebernahmeAblauf.SicherungsZeilen(sicherungMit));

        // SicherungsPruefung: Tabelle abschlussprojekt +1 (Rohzahl), Zusammenfassung projekte gleich
        var pruefungOhne = SicherungsPruefung.Pruefe(sicherungOhne);
        var pruefungMit = SicherungsPruefung.Pruefe(sicherungMit);
        Assert.Contains("Tabelle abschlussprojekt: 1", pruefungOhne);
        Assert.Contains("Tabelle abschlussprojekt: 2", pruefungMit);
        Assert.Contains("Zusammenfassung projekte: 1", pruefungOhne);
        Assert.Contains("Zusammenfassung projekte: 1", pruefungMit);
        Assert.Equal(pruefungOhne.Where(z => !z.StartsWith("Tabelle abschlussprojekt", StringComparison.Ordinal) && !z.StartsWith("SHA-256", StringComparison.Ordinal)),
            pruefungMit.Where(z => !z.StartsWith("Tabelle abschlussprojekt", StringComparison.Ordinal) && !z.StartsWith("SHA-256", StringComparison.Ordinal)));

        // Vorschau der Uebernahme auf ein leeres Geraet: "Neu" zaehlt die Zeile als Eintrag mit
        using var leer = new Geraet("Leer");
        long Neu(byte[] s)
        {
            var datei = Path.Combine(leer.Ordner, Guid.NewGuid().ToString("N") + Ruecksicherung.Endung);
            File.WriteAllBytes(datei, s);
            return Uebernahme.Vorschau(leer.Lernstand, Uebernahme.Lesen(UebernahmeQuelle.Sicherung, datei), KeineUmbenennungen, leer.Uhr).Neu;
        }
        Assert.Equal(Neu(sicherungOhne) + 1, Neu(sicherungMit));
    }

    // -- Kontrollfehler: die Zeile ist nie ein Projekt der Kachel -----------------------------

    [Fact]
    public void Kachel_ZeileIstWederProjektNochAktivesProjekt()
    {
        using var g = new Geraet("PC");
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "ae"));
        Assert.Null(Gesamtstand.Projektschaetzung(g.Schreiber.ProjektFelder(), null));
        Assert.Null(Gesamtstand.Projektschaetzung(g.Schreiber.ProjektFelder(), Ausbildung.Zeile));
        // auch mit einem Feld "schaetzung" in der reservierten Zeile
        Assert.True(g.Schreiber.ProjektFeldSpeichern(Ausbildung.Zeile, Gesamtstand.SchaetzungFeld, "88"));
        Assert.Null(Gesamtstand.Projektschaetzung(g.Schreiber.ProjektFelder(), Ausbildung.Zeile));
        Assert.True(g.Schreiber.ProjektFeldSpeichern("a1b2c3d4e5f6", Gesamtstand.SchaetzungFeld, "71"));
        Assert.Equal(71.0, Gesamtstand.Projektschaetzung(g.Schreiber.ProjektFelder(), Ausbildung.Zeile));
        Assert.Equal(71.0, Gesamtstand.Projektschaetzung(g.Schreiber.ProjektFelder(), null));
    }
}
