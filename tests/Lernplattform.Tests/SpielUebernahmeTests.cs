using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W8 "Spielstand aus Python" (Weisung P7-5, 2b; Plan Phase 7 Abschnitt 5): Vergleichswerte
/// G44 Teil Uebernahme (<c>spiel_uebernahme.json</c>, Erzeuger
/// <c>werkzeuge/vergleich/erzeuge_spiel_uebernahme.py</c>). Eine echte Python-Datenbank mit vier
/// Durchgaengen (alter Durchgang, Firma aus dem Szenario des Fingerabdrucks, geloeschter Durchgang,
/// Einfach) kommt ueber den bestehenden Uebernahme-Weg (Sicherung, Abgleich-Datei, alte Datenbank) in
/// eine leere und in eine schon bespielte C#-Datenbank. Danach rechnet C# jeden Durchgang gleich nach
/// (jedes Feld, Hash f2b59ce8), die Plaetze und die Bestenliste stimmen, und keine Zeile geht verloren
/// (R3: alle Spielzeilen der Quelle und alle eigenen, eine zweite Uebernahme bringt nichts Neues).
/// </summary>
public class SpielUebernahmeTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_uebernahme.json");
    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> Umbenennungen = Core.Daten.Umbenennungen.Laden(Pfade.Inhalte);

    private static readonly (string Name, string Spalten)[] SpielTabellen =
    [
        ("spiel_ereignisse", "uid, timestamp, utc_offset, typ, daten, geraet, lauf"),
        ("spiel_plaetze", "uid, timestamp, platz, lauf, aktion, daten, geraet"),
        ("spiel_bestenliste", "uid, timestamp, utc_offset, durchgang, art, schluessel, wert, daten"),
    ];

    private const string EigenerLauf = "leigen0000000001";

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static Uhr Uhr => SpeichernTests.FesteUhr(Soll.GetProperty("jetzt_utc").GetString()!);

    private static string T(object? wert) => PythonJson.Text(wert);

    private static string T(JsonElement e) => PythonJson.Text(Py.Lesen(e.GetRawText()));

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static TheoryData<string, bool> Faelle()
    {
        var daten = new TheoryData<string, bool>();
        foreach (var quelle in new[] { "sicherung", "abgleich", "alte_db" })
        {
            daten.Add(quelle, false);
            daten.Add(quelle, true);
        }
        return daten;
    }

    /// <summary>Die Python-Datenbank nachbauen: Aufbau aus sqlite_master, alle Zeilen mit id, sqlite_sequence.</summary>
    private static string BaueQuelle(string ordner)
    {
        var pfad = Path.Combine(Directory.CreateDirectory(Path.Combine(ordner, "python")).FullName, "fisi_lernplattform.db");
        if (File.Exists(pfad))
        {
            return pfad;
        }
        var quelle = Soll.GetProperty("quelle_db");
        using var db = VergleichsHilfe.Oeffnen(pfad);
        using var tx = db.BeginTransaction();
        foreach (var teil in quelle.GetProperty("aufbau").EnumerateArray())
        {
            VergleichsHilfe.Ausfuehren(db, teil[2].GetString()!);
        }
        VergleichsHilfe.FuelleTabellen(db, quelle.GetProperty("tabellen"));
        VergleichsHilfe.Ausfuehren(db, "DELETE FROM sqlite_sequence");
        foreach (var z in quelle.GetProperty("sqlite_sequence").EnumerateArray())
        {
            VergleichsHilfe.Ausfuehren(db, "INSERT INTO sqlite_sequence (name, seq) VALUES ($p0, $p1)", z[0].GetString(), z[1].GetInt64());
        }
        tx.Commit();
        return pfad;
    }

    private static byte[] Gzip(string text) => UebernahmeTests.Gzip(text);

    /// <summary>Die Quelle wie im Programm lesen: Sicherung, Abgleich-Datei (Teil daten der Sicherung) oder alte Datenbank.</summary>
    private static Austausch Quelle(string art, string ordner)
    {
        var sicherung = Soll.GetProperty("sicherung").GetString()!;
        switch (art)
        {
            case "sicherung":
                var pfad = Path.Combine(ordner, "spiel.fisisicherung");
                File.WriteAllBytes(pfad, Gzip(sicherung));
                return Uebernahme.Lesen(UebernahmeQuelle.Sicherung, pfad);
            case "abgleich":
                using (var doc = JsonDocument.Parse(sicherung, new JsonDocumentOptions { MaxDepth = 256 }))
                {
                    var abgleich = Path.Combine(ordner, "lernstand.json.gz");
                    File.WriteAllBytes(abgleich, Gzip(doc.RootElement.GetProperty("daten").GetRawText()));
                    return Uebernahme.Lesen(UebernahmeQuelle.AbgleichDatei, abgleich);
                }
            default:
                return Uebernahme.Lesen(UebernahmeQuelle.AlteDatenbank, BaueQuelle(ordner));
        }
    }

    private static List<List<object?>> SpielZeilen(Lernstand lernstand, string tabelle, string spalten) =>
        [.. lernstand.Abfragen("SELECT " + spalten + " FROM " + tabelle + " ORDER BY uid").Select(z => z.ToList())];

    /// <summary>Ein eigener C#-Durchgang vor der Uebernahme (geschrieben ueber den neuen Weg aus W8).</summary>
    private static void EigenesSpiel(Lernstand ziel)
    {
        var n = 0;
        var start = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
        var schreiber = new LernstandSchreiber(ziel, SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n)),
            () => "eigen" + n.ToString("D6", CultureInfo.InvariantCulture));
        Assert.True(schreiber.SpielPlatzSpeichern(1, EigenerLauf, Spielstaende.Angelegt, "{}", "Handy"));
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "Handy", EigenerLauf, Vorgaenge.Sperren));
        spiel.Profil("Eigen", new PyObjekt());
        spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "eigen-1" }, { "tag", 1L }, { "richtig", true }, { "geld", 60L } });
        spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 140L } });
        Assert.Equal(0, spiel.NichtGespeichert);
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void SpielstandAusPythonGleichUndVollstaendig(string quelle, bool eigen)
    {
        using var ordner = new TempOrdner();
        var ziel = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        if (eigen)
        {
            EigenesSpiel(ziel);
        }
        var eigene = SpielTabellen.ToDictionary(t => t.Name, t => SpielZeilen(ziel, t.Name, t.Spalten));
        var eigenerStand = T(PyAusgabe.Wert(new Spielstand(LaufEreignisse(ziel, EigenerLauf), Inhalt).Felder()));
        Assert.Equal(eigen, eigene["spiel_ereignisse"].Count > 0);

        var daten = Quelle(quelle, ordner.Pfad);
        Uebernahme.Uebernehmen(ziel, daten, Umbenennungen, Uhr, Path.Combine(ordner.Pfad, "sicherungen"));
        Vollstaendig(ziel, eigene, quelle);
        Nachgerechnet(ziel);
        if (!eigen)
        {
            WieGelesen(ziel);
        }
        Assert.Equal(eigenerStand, T(PyAusgabe.Wert(new Spielstand(LaufEreignisse(ziel, EigenerLauf), Inhalt).Felder())));

        // Dieselbe Datei noch einmal: keine neue Zeile, nichts verloren
        var vorher = SpielTabellen.Select(t => T(SpielZeilen(ziel, t.Name, t.Spalten))).ToList();
        Uebernahme.Uebernehmen(ziel, Quelle(quelle, ordner.Pfad), Umbenennungen, Uhr, Path.Combine(ordner.Pfad, "sicherungen"));
        Assert.Equal(vorher, SpielTabellen.Select(t => T(SpielZeilen(ziel, t.Name, t.Spalten))).ToList());
    }

    /// <summary>R3: genau die Spielzeilen, die Python beim eigenen Zusammenfuehren behaelt, und alle eigenen.</summary>
    private static void Vollstaendig(Lernstand ziel, Dictionary<string, List<List<object?>>> eigene, string quelle)
    {
        var python = Soll.GetProperty("ziele").GetProperty(quelle == "sicherung" ? "sicherung" : "abgleich");
        foreach (var (name, spalten) in SpielTabellen)
        {
            var alle = SpielZeilen(ziel, name, spalten);
            var eigeneTexte = eigene[name].Select(T).ToHashSet(StringComparer.Ordinal);
            Assert.True(eigene[name].All(z => alle.Any(a => T(a) == T(z))), name + ": eigene Zeile fehlt nach der Uebernahme");
            var fremd = alle.Where(z => !eigeneTexte.Contains(T(z))).ToList();
            var soll = python.GetProperty(name);
            Assert.Equal(name + ": " + soll.GetProperty("anzahl").GetInt32(), name + ": " + fremd.Count);
            Assert.Equal(name + ": " + soll.GetProperty("sha256").GetString(), name + ": " + Sha(T(fremd)));
        }
    }

    private static List<SpielEreignis> LaufEreignisse(Lernstand lernstand, string lauf)
    {
        using var v = lernstand.Verbindung();
        return Spielstaende.LaufEreignisse(v, lauf);
    }

    /// <summary>Jeder Durchgang: dieselben Ereignisse, jedes Feld des Spielstands gleich, beim Firmen-Durchgang der Hash.</summary>
    private static void Nachgerechnet(Lernstand ziel)
    {
        foreach (var lauf in Soll.GetProperty("laeufe").EnumerateObject())
        {
            var wo = lauf.Name;
            var ereignisse = LaufEreignisse(ziel, lauf.Name);
            Assert.Equal(wo + " " + lauf.Value.GetProperty("anzahl").GetInt32(), wo + " " + ereignisse.Count);
            Assert.Equal(wo + " " + lauf.Value.GetProperty("ereignisse").GetString(),
                wo + " " + Sha(T(ereignisse.Select(e => (object?)new List<object?> { e.Zeit, e.Art, e.Daten }).ToList())));
            var s = new Spielstand(ereignisse, Inhalt);
            var stand = lauf.Value.GetProperty("stand");
            var felder = s.Felder();
            Assert.Equal(stand.GetProperty("pruefsummen").EnumerateObject().Select(p => p.Name), felder.Select(p => p.Key));
            foreach (var feld in stand.GetProperty("pruefsummen").EnumerateObject())
            {
                Assert.Equal(wo + " " + feld.Name + " " + feld.Value.GetString(), wo + " " + feld.Name + " " + Sha(T(PyAusgabe.Wert(felder[feld.Name]))));
            }
            foreach (var feld in stand.GetProperty("klartext").EnumerateObject())
            {
                Assert.Equal(wo + " " + feld.Name + " " + T(feld.Value), wo + " " + feld.Name + " " + T(PyAusgabe.Wert(felder[feld.Name])));
            }
            Assert.Equal(stand.GetProperty("anzahl_history").GetInt32(), s.Verlauf.Count);
            var abgeleitet = new PyObjekt
            {
                { "day", s.Tag }, { "rank", s.Rang }, { "level", s.Stufe }, { "salary", s.Gehalt }, { "mean_reputation", s.MittlereReputation },
            };
            Assert.Equal(wo + " " + T(stand.GetProperty("abgeleitet")), wo + " " + T(abgeleitet));
            if (lauf.Value.TryGetProperty("fingerabdruck", out var hash))
            {
                Assert.Equal(SpielFingerabdruckTests.SollHash, hash.GetString());
                Assert.Equal(SpielFingerabdruckTests.SollHash, SpielFingerabdruckTests.Hash(s));
            }
        }
        Assert.Equal(4, Soll.GetProperty("laeufe").EnumerateObject().Count());
    }

    /// <summary>Plaetze, Anzahl je Durchgang, Uebersicht und Bestenliste wie Python aus seiner Datenbank.</summary>
    private static void WieGelesen(Lernstand ziel)
    {
        var soll = Soll.GetProperty("gelesen");
        using var v = ziel.Verbindung();
        var zeilen = Spielstaende.Zeilen(v);
        var belegung = Spielstaende.Belegung(zeilen, Spielstaende.HatAlteEreignisse(v));
        Assert.Equal(T(soll.GetProperty("belegung")), T(new PyObjekt
        {
            { "plaetze", belegung.Plaetze.Select(p => (object?)new List<object?> { p.Platz, p.Lauf }).ToList() },
            { "namen", belegung.Namen.Select(p => (object?)new List<object?> { p.Platz, p.Name }).ToList() },
            { "extra", belegung.Extra.Cast<object?>().ToList() },
            { "geloescht", belegung.Geloescht.Order(StringComparer.Ordinal).Cast<object?>().ToList() },
        }));
        Assert.Equal(T(soll.GetProperty("anzahl")), T(Spielstaende.AnzahlJeLauf(v).OrderBy(p => p.Key, StringComparer.Ordinal)
            .Select(p => (object?)new List<object?> { p.Key, p.Value }).ToList()));
        // Den aktiven Durchgang merkt sich jedes Geraet selbst (sync_meta kommt nicht mit): wie in Python gewaehlt
        var plaetze = Spielstaende.Uebersicht(v, soll.GetProperty("aktiv").GetString(), Inhalt);
        Assert.Equal(T(soll.GetProperty("plaetze")), T(plaetze.Cast<object?>().ToList()));
        Assert.Equal(T(soll.GetProperty("zeilen_text")), T(plaetze.Select(p => (object?)Spielstaende.Zeile(p)).ToList()));
        Assert.Equal(T(Soll.GetProperty("bestenliste")), T(SpielDatenbank.Bestenzeilen(ziel)
            .Select(z => (object?)new List<object?> { z.Zeit, z.Lauf, z.Art, z.Schluessel, z.Wert, z.Daten }).ToList()));
    }

    /// <summary>Der Nachbau ist die Python-Datenbank: C# liest dort dasselbe wie Python (auch den gewaehlten Durchgang).</summary>
    [Fact]
    public void NachbauWiePython()
    {
        using var ordner = new TempOrdner();
        var pfad = BaueQuelle(ordner.Pfad);
        var quelle = new Lernstand(pfad);
        Assert.Null(quelle.NurLesenGrund);
        Nachgerechnet(quelle);
        WieGelesen(quelle);
        using var v = quelle.Verbindung();
        var belegung = Spielstaende.Belegung(Spielstaende.Zeilen(v), Spielstaende.HatAlteEreignisse(v));
        Assert.Equal(Soll.GetProperty("gelesen").GetProperty("aktiv").GetString(), Spielstaende.AktiverLauf(Spielstaende.GespeicherterLauf(v), belegung));
        var namen = Soll.GetProperty("laeufe_namen");
        Assert.Contains(namen.GetProperty("weg").GetString()!, belegung.Geloescht);
        Assert.Equal(0, Soll.GetProperty("laeufe").GetProperty(namen.GetProperty("weg").GetString()!).GetProperty("anzahl").GetInt32());
    }
}
