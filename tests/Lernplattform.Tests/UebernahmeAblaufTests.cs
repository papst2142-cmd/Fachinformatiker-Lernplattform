using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L5 (Weisung P2-4): Ablauf des Dialogs "Lernstand übernehmen" ohne Oberflaeche:
/// Quelle am Inhalt erkennen, Datei ohne Pfad lesen (Android), Texte der Vorschau und des
/// Ergebnisses nach dem Wortlaut P2-4_Wortlaut_L5.md.
/// </summary>
public class UebernahmeAblaufTests
{
    private static readonly JsonElement G20 = VergleichsHilfe.Lade("uebernahme.json");

    private static byte[] Datei(string name) => UebernahmeTests.Gzip(G20.GetProperty("dateien").GetProperty(name).GetString()!);

    private static byte[] AlteDatenbank(TempOrdner ordner)
    {
        var pfad = UebernahmeTests.BaueDatenbank(G20.GetProperty("quelle_db"), Path.Combine(ordner.Pfad, "alt.db"));
        return File.ReadAllBytes(pfad);
    }

    [Fact]
    public void QuelleAmInhaltErkannt()
    {
        using var ordner = new TempOrdner();
        Assert.Equal(UebernahmeQuelle.Sicherung, UebernahmeAblauf.Erkennen(Datei("a")));
        Assert.Equal(UebernahmeQuelle.AbgleichDatei, UebernahmeAblauf.Erkennen(Datei("d")));
        Assert.Equal(UebernahmeQuelle.AlteDatenbank, UebernahmeAblauf.Erkennen(AlteDatenbank(ordner)));
        Assert.Null(UebernahmeAblauf.Erkennen([]));
        Assert.Null(UebernahmeAblauf.Erkennen("Hallo"u8.ToArray()));
        Assert.Null(UebernahmeAblauf.Erkennen([0x1F, 0x8B, 0, 1, 2]));             // kein gzip
        Assert.Null(UebernahmeAblauf.Erkennen(UebernahmeTests.Gzip("[1, 2]")));    // kein Objekt
        Assert.Null(UebernahmeAblauf.Erkennen(UebernahmeTests.Gzip("{kaputt")));
        Assert.Equal(UebernahmeFehler.KeineSicherung,
            Assert.Throws<UebernahmeAusnahme>(() => UebernahmeAblauf.Lesen("Hallo"u8.ToArray(), null, ordner.Pfad)).Fehler);
    }

    /// <summary>Alte Datenbank ohne Pfad (Android): kurze Arbeitskopie, danach kein Rest im Ordner.</summary>
    [Fact]
    public void AlteDatenbankOhnePfadLaesstNichtsLiegen()
    {
        using var ordner = new TempOrdner();
        var inhalt = AlteDatenbank(ordner);
        var arbeit = Directory.CreateDirectory(Path.Combine(ordner.Pfad, "daten")).FullName;
        var daten = UebernahmeAblauf.Lesen(inhalt, null, arbeit);
        var vergleich = Uebernahme.LeseAlteDatenbank(Path.Combine(ordner.Pfad, "alt.db"));
        Assert.Null(daten.QuellPfad);
        Assert.Equal(vergleich.Tabellen.Keys.Order(), daten.Tabellen.Keys.Order());
        Assert.Equal(vergleich.Tabellen.Sum(t => t.Value.Count), daten.Tabellen.Sum(t => t.Value.Count));
        Assert.Empty(Directory.GetFileSystemEntries(arbeit));
        // Kaputte Datenbank: Fehlercode, ebenfalls kein Rest
        byte[] kaputt = [.. inhalt.Take(100), .. new byte[4000]];
        Assert.Equal(UebernahmeFehler.DatenbankNichtLesbar,
            Assert.Throws<UebernahmeAusnahme>(() => UebernahmeAblauf.Lesen(kaputt, null, arbeit)).Fehler);
        Assert.Empty(Directory.GetFileSystemEntries(arbeit));
    }

    [Fact]
    public void MitPfadWirdDieEigeneDateiErkannt()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, "a.fisisicherung");
        File.WriteAllBytes(pfad, Datei("a"));
        Assert.Equal(pfad, UebernahmeAblauf.Lesen(File.ReadAllBytes(pfad), pfad, ordner.Pfad).QuellPfad);
        Assert.Null(UebernahmeAblauf.Lesen(File.ReadAllBytes(pfad), null, ordner.Pfad).QuellPfad);
    }

    [Fact]
    public void AufraeumenEntferntReste()
    {
        using var ordner = new TempOrdner();
        File.WriteAllText(Path.Combine(ordner.Pfad, UebernahmeAblauf.ArbeitsKopie), "x");
        File.WriteAllText(Path.Combine(ordner.Pfad, UebernahmeAblauf.ArbeitsKopie + "-journal"), "x");
        File.WriteAllText(Path.Combine(ordner.Pfad, "lernplattform.db"), "bleibt");
        UebernahmeAblauf.Aufraeumen(ordner.Pfad);
        Assert.Equal([Path.Combine(ordner.Pfad, "lernplattform.db")], Directory.GetFileSystemEntries(ordner.Pfad));
    }

    private static UebernahmeBericht Bericht(UebernahmeQuelle quelle, int neu = 0, int vorhanden = 0, int entfernt = 0,
        int bleibt = 0, int ungueltig = 0, int zukunft = 0) =>
        new(quelle, false, neu, neu, vorhanden, entfernt, 0, bleibt, 0, 0, ungueltig, zukunft, 0,
            new Dictionary<string, string>(), [], null, ungueltig);

    [Fact]
    public void ZahlzeilenImmerVierInFesterReihenfolge()
    {
        Assert.Equal(
        [
            "Neu auf diesem Gerät: 0 Einträge", "Schon vorhanden: 1 Eintrag",
            "Wegen einer Löschung entfernt: 2 Einträge", "Bleiben gelöscht: 1.234 Einträge",
        ], UebernahmeTexte.Zahlzeilen(Bericht(UebernahmeQuelle.Sicherung, 0, 1, 2, 1234)));
    }

    [Fact]
    public void HinweiseJeQuelle()
    {
        Assert.Equal([UebernahmeTexte.UngueltigSicherung, UebernahmeTexte.ZukunftSicherung],
            UebernahmeTexte.Hinweise(UebernahmeQuelle.Sicherung, 1, 2));
        Assert.Equal([UebernahmeTexte.UngueltigAbgleich, UebernahmeTexte.ZukunftAbgleich],
            UebernahmeTexte.Hinweise(UebernahmeQuelle.AbgleichDatei, 3, 1));
        Assert.Empty(UebernahmeTexte.Hinweise(UebernahmeQuelle.AlteDatenbank, 1, 1));
        Assert.Empty(UebernahmeTexte.Hinweise(UebernahmeQuelle.Sicherung, 0, 0));
    }

    [Fact]
    public void ErgebnisSaetzeNachWortlaut()
    {
        Assert.Equal("Sicherung eingespielt: 3 Einträge übernommen. 1 Eintrag bleibt gelöscht, weil er auf diesem Gerät gelöscht wurde."
            + " 2 Einträge wegen einer Löschung entfernt. Dein bisheriger Lernstand liegt als Kopie im Ordner „sicherungen“: vor_uebernahme_2026-10-20_120000.db."
            + " Die Sicherung enthält eine ungültige Löschmarke. Sie wurde nicht angewendet.",
            UebernahmeTexte.Ergebnis(Bericht(UebernahmeQuelle.Sicherung, 3, 0, 2, 1, 1), "vor_uebernahme_2026-10-20_120000.db"));
        Assert.Equal("Abgleich-Datei eingelesen: Nichts übernommen. 4 Einträge bleiben gelöscht, weil sie auf diesem Gerät gelöscht wurden."
            + " Die Abgleich-Datei enthält eine Löschmarke in der Zukunft. Sie wurde nicht angewendet.",
            UebernahmeTexte.Ergebnis(Bericht(UebernahmeQuelle.AbgleichDatei, 0, 5, 0, 4, 0, 1), null));
        Assert.Equal("Alte Datenbank eingelesen: Es fehlte nichts, alles war schon da.",
            UebernahmeTexte.Ergebnis(Bericht(UebernahmeQuelle.AlteDatenbank, 0, 7, 0, 0, 1, 1), null));
    }

    /// <summary>Weisung P2-6 A3: unsichtbares Trennzeichen nur am Handy, am PC bleibt der Name kopierbar.</summary>
    [Fact]
    public void DateinameDerKopieNurAmHandyMitTrennzeichen()
    {
        const string name = "vor_uebernahme_2026-10-20_120000_2.db";
        Assert.Equal(name, UebernahmeTexte.KopieName(name, handy: false));
        Assert.DoesNotContain('​', UebernahmeTexte.KopieName(name, handy: false));
        Assert.Equal("vor_​uebernahme_​2026-10-20_​120000_​2.db", UebernahmeTexte.KopieName(name, handy: true));
        // Ohne die unsichtbaren Zeichen ist es am Handy derselbe Name
        Assert.Equal(name, UebernahmeTexte.KopieName(name, handy: true).Replace("​", "", StringComparison.Ordinal));
        Assert.EndsWith("„sicherungen“: " + name + ".",
            UebernahmeTexte.Ergebnis(Bericht(UebernahmeQuelle.Sicherung, 3, 0, 0, 0, 0), UebernahmeTexte.KopieName(name, handy: false)), StringComparison.Ordinal);
    }

    [Fact]
    public void FehlertexteJeCode()
    {
        Assert.Equal("Die Sicherung stammt aus Version 0.70.0. Bitte zuerst die App aktualisieren.",
            UebernahmeTexte.Fehler(UebernahmeFehler.ZuNeu, "0.70.0", null, false, UebernahmeQuelle.Sicherung));
        Assert.Equal(UebernahmeTexte.AbgleichDateiKaputt,
            UebernahmeTexte.Fehler(UebernahmeFehler.ZuNeu, null, null, false, UebernahmeQuelle.AbgleichDatei));
        Assert.Equal(UebernahmeTexte.VorschauGescheitert, UebernahmeTexte.Fehler(UebernahmeFehler.SchreibenGescheitert, null, null, true));
        Assert.Equal(UebernahmeTexte.SchreibenGescheitert, UebernahmeTexte.Fehler(UebernahmeFehler.SchreibenGescheitert, null, null, false));
        Assert.Equal(UebernahmeTexte.SicherungGescheitert, UebernahmeTexte.Fehler(UebernahmeFehler.SicherungGescheitert, null, null, false));
        Assert.Equal(UebernahmeTexte.GleicheDatei, UebernahmeTexte.Fehler(UebernahmeFehler.GleicheDatei, null, null, false));
        foreach (var (grund, zeile) in Lernstand.NurLesenZeilen)
        {
            Assert.Equal("Der Lernstand lässt sich nicht übernehmen. " + zeile.Trim(),
                UebernahmeTexte.Fehler(UebernahmeFehler.NurLesen, null, grund, false));
        }
        // Jeder Fehlercode hat einen eigenen, nicht leeren Text
        foreach (var code in Enum.GetValues<UebernahmeFehler>())
        {
            Assert.False(string.IsNullOrWhiteSpace(UebernahmeTexte.Fehler(code, "1", null, false)), code.ToString());
        }
    }
}
