using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Projekt;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W3 (Plan P4-8 Abschnitt 6, "Zurueckspielen"): Projekte und geloeschte Projekte in
/// Sicherung (.fisisicherung), alter Datenbank und Abgleich-Export, wie test_n16_0632 E10 bis E12.
/// Die Dateien entstehen hier mit C# im Python-Format (gleiches Schema; dass Python 0.63.2 die
/// C#-Sicherung liest, belegt die CI). C# kennt kein "Alles ersetzen" (Uebernahme nur zusammenfuehren,
/// Variante b): E11 bringt das Projekt deshalb NICHT zurueck. Nur synthetische Daten (A3).
/// </summary>
public class ProjektSicherungTests
{
    private const string Geheim = "PRIVATER TEXT N16";

    private sealed class Db : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Db()
        {
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            Schreiber = new LernstandSchreiber(Lernstand);
        }

        public Lernstand Lernstand { get; }

        public LernstandSchreiber Schreiber { get; }

        public string Ordner => _ordner.Pfad;

        public void Dispose() => _ordner.Dispose();

        /// <summary>Projekt mit drei Feldern wie N16Kern.projekt.</summary>
        public string Projekt()
        {
            var projekt = Abschlussprojekt.Anlegen(Schreiber, Geheim + " Titel");
            Schreiber.ProjektFeldSpeichern(projekt, "ausgangslage", Geheim + " Ausgangslage");
            Schreiber.ProjektFeldSpeichern(projekt, "ziel", Geheim + " Ziel");
            Schreiber.ProjektFeldSpeichern(projekt, Abschlussprojekt.SeitenFeld, "15");
            return projekt;
        }

        public string Loeschzeit(string projekt) =>
            (string)Lernstand.Abfragen("SELECT MIN(timestamp) FROM abschlussprojekt WHERE projekt = $p AND feld = '_geloescht'",
                ("$p", projekt))[0][0]!;

        public void Zeile(string uid, string zeit, string projekt, string feld, string wert) =>
            Assert.True(Lernstand.Schreiben("INSERT INTO abschlussprojekt (timestamp, projekt, feld, wert, uid) VALUES ($t, $p, $f, $w, $u)",
                ("$t", zeit), ("$p", projekt), ("$f", feld), ("$w", wert), ("$u", uid)));

        public List<(string Feld, string Wert)> Zeilen(string projekt) =>
            [.. Lernstand.Abfragen("SELECT feld, wert FROM abschlussprojekt WHERE projekt = $p ORDER BY feld, uid", ("$p", projekt))
                .Select(z => ((string)z[0]!, (string)z[1]!))];

        public bool OhneText(string projekt) => Zeilen(projekt).All(z => z.Wert == "" || (z.Feld == "_geloescht" && z.Wert == "1"));

        public void Uebernehmen(Austausch daten) =>
            Uebernahme.Uebernehmen(Lernstand, daten, Umbenennungen.Laden(Pfade.Inhalte), null, Path.Combine(Ordner, "sicherungen"));
    }

    private static string Spaeter(string stempel, double stunden) =>
        DateTime.ParseExact(stempel, "yyyy-MM-dd HH:mm:ss.ffffff", System.Globalization.CultureInfo.InvariantCulture)
            .AddHours(stunden).ToString("yyyy-MM-dd HH:mm:ss.ffffff", System.Globalization.CultureInfo.InvariantCulture);

    private static byte[] Sicherung(Lernstand lernstand) =>
        Ruecksicherung.Erstellen(lernstand, new JsonObject(), "A 0.9.0", "PC Test", new DateTime(2026, 10, 22, 12, 0, 0));

    private static string Entpacken(byte[] bytes)
    {
        using var gz = new GZipStream(new MemoryStream(bytes), CompressionMode.Decompress);
        using var aus = new MemoryStream();
        gz.CopyTo(aus);
        return Encoding.UTF8.GetString(aus.ToArray());
    }

    /// <summary>E10: Sicherung bzw. alte Datenbank mit spaeterem Text eines hier geloeschten Projekts zusammenfuehren.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void E10_GeloeschtesProjektBleibtOhneText(bool sicherung)
    {
        using var ziel = new Db();
        var projekt = ziel.Projekt();
        Assert.True(Abschlussprojekt.Loeschen(ziel.Schreiber, projekt));
        using var quelle = new Db();
        quelle.Zeile("fremd-e10", Spaeter(ziel.Loeschzeit(projekt), 2), projekt, "ziel", Geheim + " aus Sicherung");
        ziel.Uebernehmen(sicherung ? Uebernahme.LeseSicherung(Sicherung(quelle.Lernstand))
            : Uebernahme.LeseAlteDatenbank(quelle.Lernstand.Pfad));
        Assert.True(ziel.OhneText(projekt), string.Join("; ", ziel.Zeilen(projekt)));
        Assert.False(Abschlussprojekt.Projekte(ziel.Schreiber.ProjektFelder()).ContainsKey(projekt));
    }

    /// <summary>E10 umgekehrt: die Quelle hat geloescht, das Ziel haelt das Projekt noch.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void E10_LoeschungAusDerQuelleGilt(bool sicherung)
    {
        using var quelle = new Db();
        var projekt = quelle.Projekt();
        using var ziel = new Db();
        ziel.Uebernehmen(Uebernahme.LeseAlteDatenbank(quelle.Lernstand.Pfad));
        Assert.True(Abschlussprojekt.Projekte(ziel.Schreiber.ProjektFelder()).ContainsKey(projekt));
        Assert.True(Abschlussprojekt.Loeschen(quelle.Schreiber, projekt));
        ziel.Uebernehmen(sicherung ? Uebernahme.LeseSicherung(Sicherung(quelle.Lernstand))
            : Uebernahme.LeseAlteDatenbank(quelle.Lernstand.Pfad));
        Assert.True(ziel.OhneText(projekt), string.Join("; ", ziel.Zeilen(projekt)));
        Assert.Empty(Abschlussprojekt.Projekte(ziel.Schreiber.ProjektFelder()));
    }

    /// <summary>E11 (C#): eine Sicherung von VOR dem Loeschen bringt das Projekt beim Zusammenfuehren nicht zurueck.</summary>
    [Fact]
    public void E11_AeltereSicherungBringtDasProjektNichtZurueck()
    {
        using var ziel = new Db();
        var projekt = ziel.Projekt();
        var alt = Uebernahme.LeseSicherung(Sicherung(ziel.Lernstand));
        Assert.True(Abschlussprojekt.Loeschen(ziel.Schreiber, projekt));
        ziel.Uebernehmen(alt);
        Assert.False(Abschlussprojekt.Projekte(ziel.Schreiber.ProjektFelder()).ContainsKey(projekt));
        Assert.True(ziel.OhneText(projekt), string.Join("; ", ziel.Zeilen(projekt)));
    }

    /// <summary>E12: Abgleich-Export und Sicherung enthalten keinen Text des geloeschten Projekts.</summary>
    [Fact]
    public void E12_ExportUndSicherungOhneText()
    {
        using var ziel = new Db();
        var projekt = ziel.Projekt();
        Assert.True(Abschlussprojekt.Loeschen(ziel.Schreiber, projekt));
        using var quelle = new Db();
        quelle.Zeile("fremd-e12", Spaeter(ziel.Loeschzeit(projekt), 2), projekt, "ausgangslage", Geheim + " spaeter");
        ziel.Uebernehmen(Uebernahme.LeseAlteDatenbank(quelle.Lernstand.Pfad));
        using (var db = ziel.Lernstand.Verbindung())
        {
            Assert.DoesNotContain(Geheim, PythonJson.Text(Ruecksicherung.Austausch(db, null)), StringComparison.Ordinal);
        }
        Assert.DoesNotContain(Geheim, Entpacken(Sicherung(ziel.Lernstand)), StringComparison.Ordinal);
        Assert.Contains("\"_geloescht\"", Entpacken(Sicherung(ziel.Lernstand)), StringComparison.Ordinal);
    }

    /// <summary>C#-Sicherung mit neuerer Fassung zusammenfuehren: danach zeigt die Seite die neue Fassung.</summary>
    [Fact]
    public void CSicherung_SeiteZeigtDieNeueFassung()
    {
        using var ziel = new Db();
        Assert.True(ziel.Schreiber.ProjektFeldSpeichern("p1", "titel", "Alt"));
        var einstellungen = new JsonObject();
        var ablauf = new ProjektAblauf(() => ziel.Schreiber, () => einstellungen, aendern =>
        {
            aendern(einstellungen);
            return true;
        }, AbschlussprojektTests.Fragen, () => false);
        ablauf.Laden();
        Assert.Equal("Alt", ablauf.Felder["titel"]);
        using var quelle = new Db();
        quelle.Zeile("neuer", Spaeter((string)ziel.Lernstand.Abfragen("SELECT MAX(timestamp) FROM abschlussprojekt")[0][0]!, 1),
            "p1", "titel", "Neu aus der Sicherung");
        ziel.Uebernehmen(Uebernahme.LeseSicherung(Sicherung(quelle.Lernstand)));
        Assert.True(ablauf.Zeigen());
        Assert.Equal("Neu aus der Sicherung", ablauf.Felder["titel"]);
        Assert.Equal([("p1", "Neu aus der Sicherung")], ablauf.Menue());
    }
}
