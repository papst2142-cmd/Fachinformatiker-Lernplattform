using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W8 (Weisung P7-5, 2c; R3): das Spiel schreibt in <c>lernplattform.db</c> wie Python 0.63.2
/// (log_game_event, log_slot, delete_run, log_record, reset_records), nur angehaengte Zeilen, mit
/// Geraet, Durchgang, utc_offset und uid, und nie neben Abgleich, Uebernahme, Update oder Sicherung
/// einspielen (Sperre aus <see cref="Vorgaenge"/>, ohne zu warten).
/// </summary>
public class SpielDatenbankTests
{
    private const string Lauf = "lprobe0000000001";

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    /// <summary>Uhr, die bei jedem Ablesen 7 s weiterlaeuft (wie die Erzeuger).</summary>
    private static Uhr Laufende(string startUtc)
    {
        var start = DateTimeOffset.Parse(startUtc, CultureInfo.InvariantCulture);
        var n = 0;
        return SpeichernTests.FesteUhr(() => start.AddSeconds(7 * ++n));
    }

    private static LernstandSchreiber Schreiber(Lernstand lernstand, string startUtc = "2026-10-02T08:00:00+00:00")
    {
        var n = 0;
        return new LernstandSchreiber(lernstand, Laufende(startUtc), () => "u" + (++n).ToString("D6", CultureInfo.InvariantCulture));
    }

    /// <summary>Tests warten auf die Sperre: andere Testklassen halten sie parallel (Abgleich, Uebernahme).</summary>
    private static SpielDatenbank Datenbank(LernstandSchreiber schreiber, string? lauf = Lauf, string geraet = "PC") =>
        new(schreiber, geraet, lauf, Vorgaenge.Sperren);

    private static List<object?[]> Zeilen(Lernstand lernstand, string tabelle, string spalten) =>
        lernstand.Abfragen("SELECT " + spalten + " FROM " + tabelle + " ORDER BY rowid");

    /// <summary>Das Szenario des Fingerabdrucks in der Datenbank: gleiche Ereignisse, gleiche Bestenliste, Hash f2b59ce8.</summary>
    [Fact]
    public void FingerabdruckInDerDatenbank()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var spiel = SpielFingerabdruckTests.Spielen(Datenbank(Schreiber(lernstand)));
        var speicher = SpielFingerabdruckTests.Gespielt.Value;

        Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(spiel.Stand));
        Assert.Equal(0, spiel.Abweichungen);
        Assert.Equal(0, spiel.NichtGespeichert);
        Assert.Equal(PythonJson.Text(speicher.Stand.Verlauf.Select(e => (object?)new List<object?> { e.Art, e.Daten }).ToList()),
            PythonJson.Text(spiel.Stand.Verlauf.Select(e => (object?)new List<object?> { e.Art, e.Daten }).ToList()));
        Assert.Equal(PythonJson.Text(speicher.Speicher.Bestenliste().Select(z => (object?)new List<object?> { z.Lauf.Length > 0, z.Art, z.Schluessel, z.Wert, z.Daten }).ToList()),
            PythonJson.Text(spiel.Speicher.Bestenliste().Select(z => (object?)new List<object?> { z.Lauf.Length > 0, z.Art, z.Schluessel, z.Wert, z.Daten }).ToList()));

        // Jede Zeile wie Python: Geraet, Durchgang, utc_offset, eigene uid; die Daten sind der JSON-Text
        var zeilen = Zeilen(lernstand, "spiel_ereignisse", "timestamp, utc_offset, typ, daten, geraet, uid, lauf");
        var ereignisse = ((SpielSpeicher)speicher.Speicher).Geschrieben;
        Assert.Equal(ereignisse.Count, zeilen.Count);
        for (var i = 0; i < zeilen.Count; i++)
        {
            Assert.Equal((ereignisse[i].Art, ereignisse[i].Daten), ((string)zeilen[i][2]!, (string)zeilen[i][3]!));
            Assert.Equal(120L, zeilen[i][1]);
            Assert.Equal("PC", zeilen[i][4]);
            Assert.Equal(Lauf, zeilen[i][6]);
        }
        Assert.Equal(zeilen.Count, zeilen.Select(z => z[5]).Distinct().Count());
        Assert.All(Zeilen(lernstand, "spiel_bestenliste", "durchgang, utc_offset, uid"), z =>
        {
            Assert.Equal(spiel.Stand.ErstesEreignis, z[0]);
            Assert.Equal(120L, z[1]);
        });
    }

    /// <summary>Laeuft ein anderer Vorgang, schreibt eine Aktion gar nichts (auch nicht teilweise) und der Stand bleibt.</summary>
    [Fact]
    public void BelegtSchreibtNichts()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(Schreiber(lernstand), "PC", Lauf));
        var vorher = spiel.Stand;
        using (Vorgaenge.Sperren())
        {
            Assert.Throws<SpielBelegtAusnahme>(() => spiel.Profil("Probe", new PyObjekt()));
            Assert.Throws<SpielBelegtAusnahme>(() => spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L } }));
            Assert.Throws<SpielBelegtAusnahme>(() => spiel.BestenlisteLoeschen());
        }
        Assert.Same(vorher, spiel.Stand);
        Assert.Empty(Zeilen(lernstand, "spiel_ereignisse", "uid"));
        Assert.Empty(Zeilen(lernstand, "sync_meta", "key"));
    }

    /// <summary>Eine Aktion mit mehreren Ereignissen haelt die Sperre ueber alle (geschachtelt nur einmal geholt).</summary>
    [Fact]
    public void AktionHaeltDieSperreEinmal()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var geholt = 0;
        var speicher = new SpielDatenbank(Schreiber(lernstand), "PC", Lauf, () =>
        {
            geholt++;
            var sperre = Vorgaenge.Sperren();
            Assert.Null(Vorgaenge.Versuchen());   // gehalten: kein zweiter Vorgang
            return sperre;
        });
        var spiel = new Spielablauf(Inhalt, speicher);
        spiel.Ausfuehren([
            (Ereignisse.Profil, new PyObjekt { { "name", "Probe" } }),
            (Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-1" }, { "tag", 1L }, { "richtig", true }, { "geld", 60L } }),
        ]);
        Assert.Equal(1, geholt);
        // dazu ein Abzeichen aus check_achievements nach dem Profil, in derselben Sitzung
        Assert.Equal([Ereignisse.Profil, Ereignisse.Erfolg, Ereignisse.Erledigt], Zeilen(lernstand, "spiel_ereignisse", "typ").Select(z => (string)z[0]!));
        // ohne Sitzung schreibt der Speicher nie
        Assert.Throws<InvalidOperationException>(() => speicher.Schreiben(Ereignisse.Feierabend, "{}"));
    }

    /// <summary>Wird nichts gespeichert (Lesefehler der Datenbank), aendert sich der Stand nicht (Python: log_game_event False).</summary>
    [Fact]
    public void NurLesenSpeichertNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
        var lernstand = new Lernstand(pfad);
        Assert.Equal("kaputt", lernstand.NurLesenGrund);
        var spiel = new Spielablauf(Inhalt, Datenbank(Schreiber(lernstand)));
        spiel.Profil("Probe", new PyObjekt());
        Assert.Null(spiel.Stand.Profil);
        Assert.True(spiel.NichtGespeichert >= 1);
        Assert.True(lernstand.AbgelehnteSchreibzugriffe >= 1);
    }

    /// <summary>delete_run: die Zeile "geloescht" bleibt, Ereignisse geloeschter Durchgaenge gehen (alter Durchgang = ohne lauf).</summary>
    [Fact]
    public void LaufLoeschenWiePython()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var schreiber = Schreiber(lernstand);
        foreach (var lauf in new[] { "", "alt", "l1", "l2" })
        {
            Assert.NotNull(schreiber.SpielEreignisSpeichern(Ereignisse.Profil, "{\"name\": \"" + lauf + "\"}", "PC", lauf));
        }
        Assert.True(schreiber.SpielPlatzSpeichern(2, "l1", Spielstaende.Angelegt, "", "PC"));
        Assert.True(schreiber.SpielPlatzSpeichern(3, "l2", Spielstaende.Angelegt, "{}", "Handy"));
        Assert.Equal([null, null, "l1", "l2"], Zeilen(lernstand, "spiel_ereignisse", "lauf").Select(z => z[0]));

        Assert.True(schreiber.SpielLaufLoeschen(2, "l1", "PC"));
        Assert.Equal([null, null, "l2"], Zeilen(lernstand, "spiel_ereignisse", "lauf").Select(z => z[0]));
        Assert.True(schreiber.SpielLaufLoeschen(1, LernstandSchreiber.AlterLauf, "PC"));
        Assert.Equal(["l2"], Zeilen(lernstand, "spiel_ereignisse", "lauf").Select(z => z[0]));

        var plaetze = Zeilen(lernstand, "spiel_plaetze", "platz, lauf, aktion, daten, geraet");
        Assert.Equal(["2|l1|angelegt|{}|PC", "3|l2|angelegt|{}|Handy", "2|l1|geloescht|{}|PC", "1|alt|geloescht|{}|PC"],
            plaetze.Select(z => string.Join("|", z.Select(w => Convert.ToString(w, CultureInfo.InvariantCulture)))));
        // _now_fine: Ortszeit mit Mikrosekunden
        Assert.All(Zeilen(lernstand, "spiel_plaetze", "timestamp"), z => Assert.Matches(@"^\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d{6}$", (string)z[0]!));
    }

    /// <summary>log_record: liegt der Eintrag nicht nach der Marke "Bestenliste loeschen", bekommt er Marke + 1 s.</summary>
    [Theory]
    [InlineData("2026-10-20 12:00:00+02:00", "2026-10-20T09:00:00+00:00", "2026-10-20 12:00:01", 120L)]   // Text davor
    [InlineData("2026-10-20 12:00:00+01:00", "2026-10-20T10:30:00+00:00", "2026-10-20 12:00:01", 60L)]    // Text danach, in UTC davor
    [InlineData("2026-10-20 12:00:00+02:00", "2026-10-20T09:59:53+00:00", "2026-10-20 12:00:01", 120L)]   // gleiche Sekunde
    [InlineData("2026-10-20 12:00:00", "2026-10-20T09:00:00+00:00", "2026-10-20 12:00:01", 120L)]         // ohne Zone: Abstand bleibt
    [InlineData("2026-10-20 12:00:00", "2026-10-20T11:00:00+00:00", "2026-10-20 13:00:07", 120L)]         // danach: unveraendert
    [InlineData("kein Datum", "2026-10-20T09:00:00+00:00", "2026-10-20 11:00:07", 120L)]                  // ungueltige Marke zaehlt nicht
    public void BestenlisteNachDerMarke(string marke, string jetztUtc, string zeit, long abstand)
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        Assert.True(lernstand.Schreiben("INSERT INTO sync_meta (key, value) VALUES ('bestenliste_reset_at', $m)", ("$m", marke)));
        var schreiber = Schreiber(lernstand, jetztUtc);
        Assert.True(schreiber.BestenSpeichern("2026-10-01 10:00:00", "erfolg", "erster", 2L, ""));
        var z = Assert.Single(Zeilen(lernstand, "spiel_bestenliste", "timestamp, utc_offset, durchgang, art, schluessel, wert, daten"));
        Assert.Equal([zeit, abstand, "2026-10-01 10:00:00", "erfolg", "erster", 2.0, "{}"], z);
    }

    /// <summary>reset_records: alle Zeilen weg, Marke bestenliste_reset_at gesetzt; das Spiel traegt sich gleich neu ein.</summary>
    [Fact]
    public void BestenlisteLoeschen()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var schreiber = Schreiber(lernstand);
        Assert.True(schreiber.BestenSpeichern("x", "erfolg", "erster", 1L, "{}"));
        var spiel = new Spielablauf(Inhalt, Datenbank(schreiber));
        Assert.True(spiel.BestenlisteLoeschen());
        Assert.Empty(Zeilen(lernstand, "spiel_bestenliste", "uid"));
        var meta = Assert.Single(Zeilen(lernstand, "sync_meta", "key, value"));
        Assert.Equal("bestenliste_reset_at", meta[0]);
        Assert.Matches(@"^2026-10-02 10:00:\d\d\+02:00$", (string)meta[1]!);
    }
}
