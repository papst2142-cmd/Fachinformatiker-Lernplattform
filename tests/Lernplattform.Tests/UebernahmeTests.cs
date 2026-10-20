using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, L3 "Uebernahme" (Variante b, Nico P2-2): Zusammenfuehren wie Pythons
/// "Sicherung zusammenfuehren" (G20) aus Sicherung, Abgleich-Datei und alter Python-Datenbank,
/// mit Vorschau und automatischer Sicherung. Nur synthetische Daten (A3).
/// Pflichttests: zweite Uebernahme ohne neue Zeilen, Vorschau aendert nichts und nennt die
/// Loeschmarken-Zahl, Sicherung scheitert = keine Uebernahme, Absturz mitten in der
/// Uebernahme, nur lesen, gleiche Datei, Python-Datenordner bleibt unveraendert.
/// </summary>
public class UebernahmeTests
{
    private static readonly JsonElement G20 = VergleichsHilfe.Lade("uebernahme.json");
    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> Umbenennungen = Core.Daten.Umbenennungen.Laden(Pfade.Inhalte);

    private static Uhr Uhr => SpeichernTests.FesteUhr(G20.GetProperty("jetzt_utc").GetString()!);

    public static TheoryData<string> Faelle()
    {
        var daten = new TheoryData<string>();
        foreach (var fall in G20.GetProperty("faelle").EnumerateArray())
        {
            daten.Add(fall.GetProperty("name").GetString()!);
        }
        return daten;
    }

    private static JsonElement Fall(string name) =>
        G20.GetProperty("faelle").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);

    /// <summary>Datenbank aus einem Stand von G20 (Zeilen mit id, sync_meta, sqlite_sequence).</summary>
    internal static string BaueDatenbank(JsonElement stand, string pfad)
    {
        _ = new Lernstand(pfad);
        using var db = VergleichsHilfe.Oeffnen(pfad);
        using var tx = db.BeginTransaction();
        VergleichsHilfe.Ausfuehren(db, "DELETE FROM sync_meta");
        VergleichsHilfe.FuelleTabellen(db, stand.GetProperty("tabellen"));
        foreach (var z in stand.GetProperty("sync_meta").EnumerateArray())
        {
            VergleichsHilfe.Ausfuehren(db, "INSERT INTO sync_meta (key, value) VALUES ($p0, $p1)", z[0].GetString(), z[1].GetString());
        }
        VergleichsHilfe.Ausfuehren(db, "DELETE FROM sqlite_sequence");
        foreach (var z in stand.GetProperty("sqlite_sequence").EnumerateArray())
        {
            VergleichsHilfe.Ausfuehren(db, "INSERT INTO sqlite_sequence (name, seq) VALUES ($p0, $p1)", z[0].GetString(), z[1].GetInt64());
        }
        tx.Commit();
        return pfad;
    }

    internal static byte[] Gzip(string text)
    {
        using var ausgabe = new MemoryStream();
        using (var gz = new GZipStream(ausgabe, CompressionLevel.Optimal))
        {
            gz.Write(Encoding.UTF8.GetBytes(text));
        }
        return ausgabe.ToArray();
    }

    /// <summary>Datei der Quelle wie im Erzeuger: a/b/c Sicherung, d Abgleich-Datei, e alte Datenbank.</summary>
    private static Austausch Quelle(string quelle, string ordner)
    {
        switch (quelle)
        {
            case "a" or "b" or "c":
                var pfad = Path.Combine(ordner, quelle + ".fisisicherung");
                File.WriteAllBytes(pfad, Gzip(G20.GetProperty("dateien").GetProperty(quelle).GetString()!));
                return Uebernahme.Lesen(UebernahmeQuelle.Sicherung, pfad);
            case "d":
                var abgleich = Path.Combine(ordner, "lernstand.json.gz");
                File.WriteAllBytes(abgleich, Gzip(G20.GetProperty("dateien").GetProperty("d").GetString()!));
                return Uebernahme.Lesen(UebernahmeQuelle.AbgleichDatei, abgleich);
            default:
                var alt = Path.Combine(Directory.CreateDirectory(Path.Combine(ordner, "python")).FullName, "fisi_lernplattform.db");
                if (!File.Exists(alt))
                {
                    BaueDatenbank(G20.GetProperty("quelle_db"), alt);
                }
                return Uebernahme.Lesen(UebernahmeQuelle.AlteDatenbank, alt);
        }
    }

    [Fact]
    public void ZieleWiePythonAufgebaut()
    {
        using var ordner = new TempOrdner();
        foreach (var ziel in G20.GetProperty("ziele").EnumerateObject())
        {
            var pfad = BaueDatenbank(ziel.Value, Path.Combine(ordner.Pfad, ziel.Name + ".db"));
            _ = new Lernstand(pfad);    // Oeffnen aendert nichts
            Assert.Equal(VergleichsHilfe.Stand(ziel.Value), VergleichsHilfe.Stand(pfad, ziel.Value));
        }
        var quelle = BaueDatenbank(G20.GetProperty("quelle_db"), Path.Combine(ordner.Pfad, "quelle.db"));
        Assert.Equal(VergleichsHilfe.Stand(G20.GetProperty("quelle_db")), VergleichsHilfe.Stand(quelle, G20.GetProperty("quelle_db")));
    }

    /// <summary>Die alte Datenbank wird gelesen wie Pythons export_local (gleicher Inhalt wie austausch_e).</summary>
    [Fact]
    public void AlteDatenbankWieExportLocal()
    {
        using var ordner = new TempOrdner();
        var daten = Quelle("e", ordner.Pfad);
        var erwartet = G20.GetProperty("austausch_e");
        Assert.True(daten.MitSchreiber);
        foreach (var tabelle in erwartet.GetProperty("tables").EnumerateObject())
        {
            Assert.Equal(erwartet.GetProperty("columns").GetProperty(tabelle.Name).EnumerateArray().Select(s => s.GetString()!), daten.Spalten.GetValueOrDefault(tabelle.Name, []));
            Assert.Equal(tabelle.Value.EnumerateArray().Select(VergleichsHilfe.Zeile).ToList(),
                daten.Tabellen.GetValueOrDefault(tabelle.Name, []).Select(z => VergleichsHilfe.Zeile(z)).ToList());
        }
        foreach (var marke in erwartet.GetProperty("markers").EnumerateObject())
        {
            Assert.Equal(VergleichsHilfe.Text(marke.Value), VergleichsHilfe.Text(daten.Marken[marke.Name]));
        }
        Assert.Equal(erwartet.GetProperty("markers").EnumerateObject().Count(), daten.Marken.Count);
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void UebernahmeWiePython(string name)
    {
        using var ordner = new TempOrdner();
        var fall = Fall(name);
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        BaueDatenbank(G20.GetProperty("ziele").GetProperty(fall.GetProperty("ziel").GetString()!), pfad);
        var quelle = Quelle(fall.GetProperty("quelle").GetString()!, ordner.Pfad);
        if (fall.GetProperty("start").ValueKind != JsonValueKind.Null)
        {
            // zweimal: erst der Vorgaenger-Fall, dann dieselbe Datei noch einmal
            Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr);
            Assert.Equal(VergleichsHilfe.Stand(Fall(fall.GetProperty("start").GetString()!).GetProperty("nachher")),
                VergleichsHilfe.Stand(pfad, fall.GetProperty("nachher")));
        }
        var vorschau = Uebernahme.Vorschau(new Lernstand(pfad), quelle, Umbenennungen, Uhr);
        var bericht = Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr);
        var erwartet = VergleichsHilfe.Stand(fall.GetProperty("nachher"));
        var ist = VergleichsHilfe.Stand(pfad, fall.GetProperty("nachher"));
        Assert.True(erwartet.SequenceEqual(ist), VergleichsHilfe.Unterschied(erwartet, ist));
        Assert.Equal(fall.GetProperty("neu").GetInt32(), bericht.Neu);
        Assert.Equal(fall.GetProperty("uebernommen").GetInt32(), bericht.Uebernommen);
        Assert.Equal(fall.GetProperty("nicht_uebernommen").GetInt32(), bericht.NichtUebernommen);
        Assert.Equal(fall.GetProperty("entfernt_durch_marke").GetInt32(), bericht.EntferntDurchLoeschmarke);
        Assert.Equal(fall.GetProperty("ungueltig").GetInt32(), bericht.UngueltigeMarken);
        Assert.Equal(fall.GetProperty("zukunft").GetInt32(), bericht.ZukunftsMarken);
        // Die Vorschau sagt dasselbe voraus, was danach wirklich passiert
        Assert.Equal(bericht with { Vorschau = true, Sicherungsdatei = null, Tabellen = vorschau.Tabellen, MarkenNachher = vorschau.MarkenNachher }, vorschau);
        Assert.Equal(bericht.Tabellen, vorschau.Tabellen);
        Assert.Equal(bericht.MarkenNachher, vorschau.MarkenNachher);
        // Keine gemessene Uhrabweichung: die bringt erst der Abgleich (L10) mit
        using var db = VergleichsHilfe.Oeffnen(pfad);
        Assert.Empty(VergleichsHilfe.Abfrage(db, "SELECT 1 FROM sync_meta WHERE key = 'uhr_abweichung'"));
        if (name.EndsWith("_zweimal", StringComparison.Ordinal))
        {
            Assert.Equal(0, bericht.Neu);
        }
    }

    /// <summary>
    /// Weisung P2-6 A2: Der Ergebnissatz „… übernommen.“ nennt die Einträge, die nach der
    /// Übernahme wirklich neu im Lernstand stehen (Neu, wie Pythons merge_backup), nicht die
    /// Rohzahl Uebernommen (merge_into_local zählt auch Zeilen, die das Aufräumen in derselben
    /// Übernahme wieder entfernt, und die Platz-Zeilen des Spiels).
    /// </summary>
    [Theory]
    [MemberData(nameof(Faelle))]
    public void ErgebnisNenntWirklichNeueEintraege(string name)
    {
        using var ordner = new TempOrdner();
        var fall = Fall(name);
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        BaueDatenbank(G20.GetProperty("ziele").GetProperty(fall.GetProperty("ziel").GetString()!), pfad);
        var quelle = Quelle(fall.GetProperty("quelle").GetString()!, ordner.Pfad);
        var vorherStand = G20.GetProperty("ziele").GetProperty(fall.GetProperty("ziel").GetString()!);
        if (fall.GetProperty("start").ValueKind != JsonValueKind.Null)
        {
            Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr);
            vorherStand = Fall(fall.GetProperty("start").GetString()!).GetProperty("nachher");
        }
        var bericht = Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr);

        // Neue Kennungen im Stand nachher (G20 aus Python), ohne Platz-Zeilen des Spiels
        var vorher = Kennungen(vorherStand);
        var wirklichNeu = Kennungen(fall.GetProperty("nachher")).Count(k => k.Tabelle != "spiel_plaetze" && !vorher.Contains(k));
        Assert.Equal(wirklichNeu, bericht.Neu);
        var ergebnis = UebernahmeTexte.Ergebnis(bericht, null);
        var kern = bericht.Neu > 0 ? UebernahmeTexte.Eintraege(bericht.Neu) + " übernommen." : null;
        if (kern is not null)
        {
            Assert.Contains(kern, ergebnis, StringComparison.Ordinal);
        }
        if (bericht.Uebernommen != bericht.Neu)
        {
            Assert.DoesNotContain(UebernahmeTexte.Eintraege(bericht.Uebernommen) + " übernommen.", ergebnis, StringComparison.Ordinal);
        }
    }

    private static HashSet<(string Tabelle, string Uid)> Kennungen(JsonElement stand)
    {
        var kennungen = new HashSet<(string, string)>();
        foreach (var tabelle in stand.GetProperty("tabellen").EnumerateObject())
        {
            var spalten = tabelle.Value.GetProperty("spalten").EnumerateArray().Select(s => s.GetString()).ToList();
            var stelle = spalten.IndexOf("uid");
            if (stelle < 0)
            {
                continue;
            }
            foreach (var zeile in tabelle.Value.GetProperty("zeilen").EnumerateArray())
            {
                kennungen.Add((tabelle.Name, zeile[stelle].ToString()));
            }
        }
        return kennungen;
    }

    [Fact]
    public void FehlerfaelleWiePython()
    {
        foreach (var fall in G20.GetProperty("fehler").EnumerateArray())
        {
            var name = fall.GetProperty("name").GetString();
            byte[] roh = fall.GetProperty("roh").ValueKind == JsonValueKind.Null
                ? Gzip(fall.GetProperty("text").GetString()!)
                : Convert.FromBase64String(fall.GetProperty("roh").GetString()!);
            if (fall.GetProperty("kuerzen").GetBoolean())
            {
                // Python kuerzt die von gzip.compress erzeugten Bytes; die Laenge des Kopfs ist gleich
                roh = roh[..(roh.Length / 2)];
            }
            string ergebnis;
            try
            {
                Uebernahme.LeseSicherung(roh);
                ergebnis = "ok";
            }
            catch (UebernahmeAusnahme e)
            {
                ergebnis = e.Fehler.ToString();
            }
            Assert.True(fall.GetProperty("erwartet").GetString() == ergebnis, $"{name}: Python {fall.GetProperty("erwartet").GetString()}, C# {ergebnis}");
        }
    }

    [Fact]
    public void ZweiteUebernahmeBringtKeineNeuenZeilen()
    {
        using var ordner = new TempOrdner();
        var pfad = BaueDatenbank(G20.GetProperty("ziele").GetProperty("gefuellt"), Path.Combine(ordner.Pfad, Lernstand.DateiName));
        foreach (var q in new[] { "a", "d", "e" })
        {
            var quelle = Quelle(q, ordner.Pfad);
            Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr);
            var zeilen = Zeilen(pfad);
            var zweite = Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr);
            Assert.Equal(0, zweite.Neu);
            Assert.Equal(zeilen, Zeilen(pfad));
        }
    }

    private static long Zeilen(string pfad)
    {
        using var db = VergleichsHilfe.Oeffnen(pfad);
        return Uebernahme.Tabellen.Sum(t => (long)VergleichsHilfe.Abfrage(db, $"SELECT COUNT(*) FROM {t.Tabelle}")[0][0]!);
    }

    internal static string Sha(string pfad) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(pfad)));

    private static string[] Dateien(string ordner) =>
        Directory.EnumerateFileSystemEntries(ordner, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(ordner, p))
            .Order(StringComparer.Ordinal).ToArray();

    [Fact]
    public void VorschauAendertNichtsUndNenntLoeschmarken()
    {
        using var ordner = new TempOrdner();
        var daten = Directory.CreateDirectory(Path.Combine(ordner.Pfad, "daten")).FullName;
        var pfad = BaueDatenbank(G20.GetProperty("ziele").GetProperty("gefuellt"), Path.Combine(daten, Lernstand.DateiName));
        var quelle = Quelle("a", ordner.Pfad);
        var vorher = Sha(pfad);
        var dateien = Dateien(daten);
        var vorschau = Uebernahme.Vorschau(new Lernstand(pfad), quelle, Umbenennungen, Uhr);
        Assert.True(vorschau.Vorschau);
        Assert.Equal(vorher, Sha(pfad));
        Assert.Equal(dateien, Dateien(daten));
        // Getrennt gezaehlt: neu, schon vorhanden, durch Loeschmarke entfernt (Auflage W1)
        var fall = Fall("a_gefuellt");
        Assert.Equal(fall.GetProperty("entfernt_durch_marke").GetInt32(), vorschau.EntferntDurchLoeschmarke);
        Assert.True(vorschau.EntferntDurchLoeschmarke > 0);
        Assert.Equal(fall.GetProperty("neu").GetInt32(), vorschau.Neu);
        Assert.True(vorschau.SchonVorhanden > 0);
        Assert.Null(vorschau.Sicherungsdatei);
    }

    [Fact]
    public void UebernahmeLegtVorherSicherungAn()
    {
        using var ordner = new TempOrdner();
        var daten = Directory.CreateDirectory(Path.Combine(ordner.Pfad, "daten")).FullName;
        var pfad = BaueDatenbank(G20.GetProperty("ziele").GetProperty("gefuellt"), Path.Combine(daten, Lernstand.DateiName));
        var vorherStand = VergleichsHilfe.Stand(G20.GetProperty("ziele").GetProperty("gefuellt"));
        var bericht = Uebernahme.Uebernehmen(new Lernstand(pfad), Quelle("a", ordner.Pfad), Umbenennungen, Uhr);
        Assert.NotNull(bericht.Sicherungsdatei);
        Assert.Equal(Path.Combine(daten, Uebernahme.SicherungsOrdner, "vor_uebernahme_2026-10-20_120000.db"), bericht.Sicherungsdatei);
        Assert.Equal(vorherStand, VergleichsHilfe.Stand(bericht.Sicherungsdatei!, G20.GetProperty("ziele").GetProperty("gefuellt")));
        // Zweite Uebernahme in derselben Sekunde: eigener Name, nichts ueberschrieben
        var zweite = Uebernahme.Uebernehmen(new Lernstand(pfad), Quelle("a", ordner.Pfad), Umbenennungen, Uhr);
        Assert.Equal(Path.Combine(daten, Uebernahme.SicherungsOrdner, "vor_uebernahme_2026-10-20_120000_2.db"), zweite.Sicherungsdatei);
        Assert.Equal(vorherStand, VergleichsHilfe.Stand(bericht.Sicherungsdatei!, G20.GetProperty("ziele").GetProperty("gefuellt")));
    }

    [Fact]
    public void SicherungScheitertKeineUebernahme()
    {
        using var ordner = new TempOrdner();
        var daten = Directory.CreateDirectory(Path.Combine(ordner.Pfad, "daten")).FullName;
        var pfad = BaueDatenbank(G20.GetProperty("ziele").GetProperty("gefuellt"), Path.Combine(daten, Lernstand.DateiName));
        File.WriteAllText(Path.Combine(daten, Uebernahme.SicherungsOrdner), "eine Datei statt eines Ordners");
        var vorher = Sha(pfad);
        var quelle = Quelle("a", ordner.Pfad);
        var fehler = Assert.Throws<UebernahmeAusnahme>(() => Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr));
        Assert.Equal(UebernahmeFehler.SicherungGescheitert, fehler.Fehler);
        Assert.Equal(vorher, Sha(pfad));
    }

    [Fact]
    public void NurLesenUebernimmtNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
        var vorher = Sha(pfad);
        var quelle = Quelle("a", ordner.Pfad);
        Assert.Equal(UebernahmeFehler.NurLesen,
            Assert.Throws<UebernahmeAusnahme>(() => Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr)).Fehler);
        Assert.Equal(UebernahmeFehler.NurLesen,
            Assert.Throws<UebernahmeAusnahme>(() => Uebernahme.Vorschau(new Lernstand(pfad), quelle, Umbenennungen, Uhr)).Fehler);
        Assert.Equal(vorher, Sha(pfad));
        Assert.False(Directory.Exists(Path.Combine(ordner.Pfad, Uebernahme.SicherungsOrdner)));
    }

    [Fact]
    public void GleicheDateiWirdAbgelehnt()
    {
        using var ordner = new TempOrdner();
        var pfad = BaueDatenbank(G20.GetProperty("ziele").GetProperty("gefuellt"), Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var vorher = Sha(pfad);
        var quelle = Uebernahme.Lesen(UebernahmeQuelle.AlteDatenbank, pfad);
        Assert.Equal(UebernahmeFehler.GleicheDatei,
            Assert.Throws<UebernahmeAusnahme>(() => Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr)).Fehler);
        Assert.Equal(vorher, Sha(pfad));
    }

    /// <summary>Mischbetrieb: der Datenordner der Python-App wird nur gelesen (bytegleich, keine neuen Dateien).</summary>
    [Fact]
    public void PythonDatenordnerBleibtUnveraendert()
    {
        using var ordner = new TempOrdner();
        var ziel = BaueDatenbank(G20.GetProperty("ziele").GetProperty("gefuellt"), Path.Combine(ordner.Pfad, Lernstand.DateiName));
        _ = Quelle("e", ordner.Pfad);     // legt python/fisi_lernplattform.db an
        var python = Path.Combine(ordner.Pfad, "python");
        var alt = Path.Combine(python, "fisi_lernplattform.db");
        var vorher = Sha(alt);
        var dateien = Dateien(python);
        var daten = Uebernahme.Lesen(UebernahmeQuelle.AlteDatenbank, alt);
        Uebernahme.Vorschau(new Lernstand(ziel), daten, Umbenennungen, Uhr);
        Uebernahme.Uebernehmen(new Lernstand(ziel), daten, Umbenennungen, Uhr);
        Assert.Equal(vorher, Sha(alt));
        Assert.Equal(dateien, Dateien(python));
    }

    /// <summary>
    /// Absturz mitten in der Uebernahme (nach der ersten Haelfte der Tabellen): danach ist die
    /// Datenbank in Ordnung und genau wie vorher (alles oder nichts); die Sicherung liegt vor.
    /// </summary>
    [Fact]
    public void AbsturzMittenInDerUebernahme()
    {
        using var ordner = new TempOrdner();
        var ziel = G20.GetProperty("ziele").GetProperty("gefuellt");
        var pfad = BaueDatenbank(ziel, Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var abgleich = Path.Combine(ordner.Pfad, "lernstand.json.gz");
        File.WriteAllBytes(abgleich, Gzip(G20.GetProperty("dateien").GetProperty("d").GetString()!));
        AbsturzProbe.Starten(AbsturzProbe.ArtUebernahme, [pfad, abgleich, "nach:trainer_aufgaben"]);
        using (var db = VergleichsHilfe.Oeffnen(pfad))
        {
            Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
        }
        Assert.Equal(VergleichsHilfe.Stand(ziel), VergleichsHilfe.Stand(pfad, ziel));
        Assert.Single(Directory.GetFiles(Path.Combine(ordner.Pfad, Uebernahme.SicherungsOrdner)));
        // Danach gelingt die Uebernahme normal und ergibt den Stand von Python
        Uebernahme.Uebernehmen(new Lernstand(pfad), Uebernahme.Lesen(UebernahmeQuelle.AbgleichDatei, abgleich), Umbenennungen, Uhr);
        Assert.Equal(VergleichsHilfe.Stand(Fall("d_gefuellt").GetProperty("nachher")), VergleichsHilfe.Stand(pfad, Fall("d_gefuellt").GetProperty("nachher")));
    }

    /// <summary>Umbenennungen aus inhalte/umbenennungen.json wie Python (Paare aus G20 sind enthalten).</summary>
    [Fact]
    public void UmbenennungenWiePython()
    {
        var u = G20.GetProperty("umbenennungen");
        Assert.Contains(("card_events", u.GetProperty("karte")[0].GetString()!, u.GetProperty("karte")[1].GetString()!), Umbenennungen);
        Assert.Contains(("quiz_answers", u.GetProperty("quiz")[0].GetString()!, u.GetProperty("quiz")[1].GetString()!), Umbenennungen);
    }

    /// <summary>Alte Datenbank ohne Spalte uid: Fehler statt still uebersprungener Zeilen (Gegenpruefung W1, Fund 6).</summary>
    [Fact]
    public void AlteDatenbankOhneUidIstNichtLesbar()
    {
        using var ordner = new TempOrdner();
        var alt = Path.Combine(ordner.Pfad, "fisi_lernplattform.db");
        using (var db = VergleichsHilfe.Oeffnen(alt))
        {
            VergleichsHilfe.Ausfuehren(db, "CREATE TABLE card_events (id INTEGER PRIMARY KEY, timestamp TEXT, category TEXT, question TEXT, mode TEXT, correct INTEGER)");
            VergleichsHilfe.Ausfuehren(db, "INSERT INTO card_events (timestamp, category, question, mode, correct) VALUES ('2026-10-01 10:00:00', 'a', 'b', 'lernen', 1)");
        }
        Assert.Equal(UebernahmeFehler.DatenbankNichtLesbar,
            Assert.Throws<UebernahmeAusnahme>(() => Uebernahme.Lesen(UebernahmeQuelle.AlteDatenbank, alt)).Fehler);
    }

    /// <summary>Zeit mit Zeilenumbruch am Ende ist wie bei Pythons strptime/fromisoformat kein Datum (Gegenpruefung W1, Fund 1).</summary>
    [Fact]
    public void ZeitMitZeilenumbruchIstKeinDatum()
    {
        Assert.Null(Loeschmarken.Ortszeit("2026-1-1 00:00:00\n"));
        Assert.NotNull(Loeschmarken.Ortszeit("2026-1-1 00:00:00"));
        Assert.Null(Loeschmarken.Lesen("2026-10-05 12:00:00+02:00\n")?.Utc);
    }

    /// <summary>
    /// Auflage P2-3: Die Vorschau legt keine Datei an, auch nicht im Temp-Ordner, und auch nicht,
    /// wenn sie mittendrin hart abgebrochen wird. Eigener Prozess mit eigenem, leerem Temp-Ordner.
    /// </summary>
    [Fact]
    public void VorschauLegtKeineTempDateiAn()
    {
        using var ordner = new TempOrdner();
        var daten = Directory.CreateDirectory(Path.Combine(ordner.Pfad, "daten")).FullName;
        var temp = Directory.CreateDirectory(Path.Combine(ordner.Pfad, "temp")).FullName;
        var pfad = BaueDatenbank(G20.GetProperty("ziele").GetProperty("gefuellt"), Path.Combine(daten, Lernstand.DateiName));
        var abgleich = Path.Combine(ordner.Pfad, "lernstand.json.gz");
        File.WriteAllBytes(abgleich, Gzip(G20.GetProperty("dateien").GetProperty("d").GetString()!));
        var vorher = Sha(pfad);
        var dateien = Dateien(daten);
        var umgebung = new Dictionary<string, string> { ["TMPDIR"] = temp, ["TMP"] = temp, ["TEMP"] = temp, ["DOTNET_EnableDiagnostics"] = "0" };
        foreach (var halt in new[] { "", "nach:card_events", "nach:abschlussprojekt" })
        {
            AbsturzProbe.Starten(AbsturzProbe.ArtVorschau, [pfad, abgleich, halt], 0, umgebung);
            Assert.Empty(Dateien(temp));
            Assert.Equal(vorher, Sha(pfad));
            Assert.Equal(dateien, Dateien(daten));
        }
    }

    /// <summary>
    /// Beleg Wortlaut L5 "SchreibenGescheitert" (Weisung P2-4): scheitert das Schreiben mitten
    /// in der Transaktion (SQLite-Fehler), ist die Lerndatenbank bytegleich wie vorher; die
    /// Kopie im Ordner "sicherungen" liegt vor. In der Vorschau derselbe Fehler = "Vorschau scheitert".
    /// </summary>
    [Fact]
    public void SchreibenGescheitertLaesstLernstandWieVorher()
    {
        using var ordner = new TempOrdner();
        var daten = Directory.CreateDirectory(Path.Combine(ordner.Pfad, "daten")).FullName;
        var pfad = BaueDatenbank(G20.GetProperty("ziele").GetProperty("gefuellt"), Path.Combine(daten, Lernstand.DateiName));
        var quelle = Quelle("d", ordner.Pfad);
        var vorher = Sha(pfad);
        var faden = Environment.CurrentManagedThreadId;
        Uebernahme.Haltepunkt = halt =>
        {
            if (Environment.CurrentManagedThreadId == faden && halt == "nach:quiz_answers")
            {
                throw new Microsoft.Data.Sqlite.SqliteException("Probe: Platte voll", 13);
            }
        };
        try
        {
            Assert.Equal(UebernahmeFehler.SchreibenGescheitert,
                Assert.Throws<UebernahmeAusnahme>(() => Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr)).Fehler);
            Assert.Equal(vorher, Sha(pfad));
            Assert.Single(Directory.GetFiles(Path.Combine(daten, Uebernahme.SicherungsOrdner)));
            Assert.Equal(UebernahmeFehler.SchreibenGescheitert,
                Assert.Throws<UebernahmeAusnahme>(() => Uebernahme.Vorschau(new Lernstand(pfad), quelle, Umbenennungen, Uhr)).Fehler);
            Assert.Equal(vorher, Sha(pfad));
        }
        finally
        {
            Uebernahme.Haltepunkt = null;
        }
        // Ohne Stoerung gelingt es danach normal
        Assert.True(Uebernahme.Uebernehmen(new Lernstand(pfad), quelle, Umbenennungen, Uhr).Neu > 0);
        Assert.NotEqual(vorher, Sha(pfad));
    }

    /// <summary>
    /// Beleg Wortlaut L5 "ZukunftAbgleich" (V): eine Loeschmarke der Abgleich-Datei mehr als
    /// 300 s nach der Geraeteuhr wird nicht angewendet; das Ergebnis ist genau das ohne diese Marke.
    /// </summary>
    [Fact]
    public void ZukunftsmarkeDerAbgleichDateiGiltNicht()
    {
        var original = G20.GetProperty("dateien").GetProperty("d").GetString()!;
        var marke = "\"reset_at\":\"2026-10-05 12:00:00+02:00\"";
        Assert.Contains(marke, original, StringComparison.Ordinal);
        var stand = new List<List<string>>();
        var berichte = new List<UebernahmeBericht>();
        foreach (var ersatz in new[] { "\"reset_at\":\"2026-10-20 12:05:01+02:00\"", "\"reset_at\":\"2026-10-20 12:05:00+02:00\"", "\"x\":\"\"" })
        {
            using var ordner = new TempOrdner();
            var ziel = G20.GetProperty("ziele").GetProperty("gefuellt");
            var pfad = BaueDatenbank(ziel, Path.Combine(ordner.Pfad, Lernstand.DateiName));
            var datei = Path.Combine(ordner.Pfad, "lernstand.json.gz");
            File.WriteAllBytes(datei, Gzip(original.Replace(marke, ersatz, StringComparison.Ordinal)));
            berichte.Add(Uebernahme.Uebernehmen(new Lernstand(pfad), Uebernahme.Lesen(UebernahmeQuelle.AbgleichDatei, datei), Umbenennungen, Uhr));
            stand.Add(VergleichsHilfe.Stand(pfad, Fall("d_gefuellt").GetProperty("nachher")));
        }
        // 12:05:01 Ortszeit = 10:05:01 UTC (Uhr 10:00 UTC), eine Sekunde ueber der Toleranz: nicht angewendet
        Assert.Equal(1, berichte[0].ZukunftsMarken);
        Assert.Equal(stand[2], stand[0]);
        static object Zahlen(UebernahmeBericht b) => (b.Neu, b.SchonVorhanden, b.EntferntDurchLoeschmarke, b.NichtUebernommen, b.UngueltigInDatei);
        Assert.Equal(Zahlen(berichte[2]), Zahlen(berichte[0]));
        Assert.Equal(berichte[2].MarkenNachher, berichte[0].MarkenNachher);
        Assert.Equal(UebernahmeTexte.ZukunftAbgleich, Assert.Single(UebernahmeTexte.Hinweise(UebernahmeQuelle.AbgleichDatei, 0, berichte[0].ZukunftsMarken)));
        // Genau an der Grenze gilt die Marke (Kontrolle, dass die Marke sonst wirkt)
        Assert.Equal(0, berichte[1].ZukunftsMarken);
        Assert.NotEqual(stand[2], stand[1]);
    }
}
