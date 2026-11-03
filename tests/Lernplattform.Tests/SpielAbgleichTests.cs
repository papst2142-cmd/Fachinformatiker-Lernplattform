using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 8, W3 Spiel-Abgleich (Weisung P8-5; Plan Phase 8 Abschnitt 3): Vergleichswerte G51
/// (<c>spiel_abgleich.json</c>, Erzeuger <c>werkzeuge/vergleich/erzeuge_spiel_abgleich.py</c>). Drei
/// Python-Datenbanken (PC, Handy, Tablet) haben offline im selben und in verschiedenen Durchgaengen
/// gespielt, das Handy spielt in einem Durchgang, den der PC geloescht hat. Ab da rechnet C# dasselbe wie
/// Python: Abgleich (ein Versuch ohne Netz wie G21) je Geraet, Spielstand je Geraet neu (L2: anderen Platz
/// waehlen), ein Spielschritt je Geraet (Tablet ohne Neuladen, Sammelliste 95), wieder Abgleich, dann ein
/// Spielschritt des PC nach dem Abgleich (L6: Schlange mit vorhergesagten Nummern), wieder Abgleich.
/// R3: jede Zeile aller Geraete steht danach auf jedem Geraet genau einmal (ausser den Ereignissen des
/// geloeschten Durchgangs, die wie in Python ueberall wegfallen).
/// </summary>
public class SpielAbgleichTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_abgleich.json");
    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> Umbenennungen = Core.Daten.Umbenennungen.Laden(Pfade.Inhalte);
    private static readonly string[] Geraete = ["PC", "Handy", "Tablet"];
    private static readonly string[] Runde = ["PC", "Handy", "Tablet", "PC", "Handy"];

    private static readonly (string Name, string Spalten)[] SpielTabellen =
    [
        ("spiel_ereignisse", "uid, timestamp, utc_offset, typ, daten, geraet, lauf"),
        ("spiel_plaetze", "uid, timestamp, platz, lauf, aktion, daten, geraet"),
        ("spiel_bestenliste", "uid, timestamp, utc_offset, durchgang, art, schluessel, wert, daten"),
    ];

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static Uhr Uhr => SpeichernTests.FesteUhr(Soll.GetProperty("jetzt_utc").GetString()!);

    private static Uhrabweichung Abweichung => new(() => 1000.0, "start-test", "prozess-test");

    private static string T(object? wert) => PythonJson.Text(wert);

    private static string T(JsonElement e) => PythonJson.Text(Py.Lesen(e.GetRawText()));

    private static string Sha(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    private static string Lauf(string name) => Soll.GetProperty("laeufe_namen").GetProperty(name).GetString()!;

    /// <summary>Eine Datenbank aus G51 nachbauen (Aufbau, alle Zeilen mit id, sqlite_sequence), wie G44.</summary>
    private static Lernstand Baue(JsonElement quelle, string pfad)
    {
        using (var db = VergleichsHilfe.Oeffnen(pfad))
        {
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
        }
        return new Lernstand(pfad);
    }

    private static List<List<object?>> Zeilen(Lernstand lernstand, string tabelle, string spalten) =>
        [.. lernstand.Abfragen("SELECT " + spalten + " FROM " + tabelle + " ORDER BY uid").Select(z => z.ToList())];

    private static List<SpielEreignis> LaufEreignisse(Lernstand lernstand, string? lauf)
    {
        using var v = lernstand.Verbindung();
        return Spielstaende.LaufEreignisse(v, lauf);
    }

    /// <summary>Die drei Geraete ab dem Stand vor dem Abgleich, mit Spielverwaltung und Schreiber.</summary>
    private sealed class Aufbau : IDisposable
    {
        private readonly TempOrdner ordner = new();
        private string? server;
        private int n;

        public Aufbau() : this(Soll.GetProperty("vorher"), Geraete)
        {
        }

        /// <summary>FW8 (Sammelliste 120): dieselben Geraete ab einem anderen Stand vor dem Abgleich (G51 sonderfaelle).</summary>
        public Aufbau(JsonElement vorher, string[] geraete)
        {
            var start = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
            // ein gemeinsamer Takt fuer alle Geraete: die Spielschritte stehen in derselben Reihenfolge wie in Python
            var uhr = SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n));
            foreach (var g in geraete)
            {
                var lernstand = Baue(vorher.GetProperty(g),
                    Path.Combine(Directory.CreateDirectory(Path.Combine(ordner.Pfad, g)).FullName, Lernstand.DateiName));
                Lernstaende[g] = lernstand;
                var schreiber = new LernstandSchreiber(lernstand, uhr, () => g.ToLowerInvariant() + (++n).ToString("D6", CultureInfo.InvariantCulture));
                // Der PC schreibt ueber die Schlange (W7e, L6), Handy und Tablet sofort
                Verwaltung[g] = new Spielverwaltung(schreiber, g, Inhalt, () => g.ToLowerInvariant() + "00000000000000000000",
                    Vorgaenge.Sperren, schlange: g == "PC" ? Schlange : null);
            }
        }

        public Dictionary<string, Lernstand> Lernstaende { get; } = [];

        public Dictionary<string, Spielverwaltung> Verwaltung { get; } = [];

        public Spielschlange Schlange { get; } = new();

        /// <summary>Ein Versuch des Abgleichs wie AbgleichDienst.Arbeit: erst die Spielschritte schreiben, dann abgleichen.</summary>
        public Abgleich.VersuchErgebnis Abgleichen(string geraet)
        {
            Spielschlange.AlleLeeren();
            using var sperre = Vorgaenge.Sperren();
            var datei = server is null ? null : GitHubAttrappe.Packen(server);
            var r = Abgleich.Versuch(Lernstaende[geraet], datei, null, Uhr, Abweichung, Umbenennungen,
                Path.Combine(ordner.Pfad, geraet, "sicherungen"));
            server = PythonJson.Text(r.Export);
            return r;
        }

        public void Dispose() => ordner.Dispose();
    }

    /// <summary>Eine Runde Abgleich (PC, Handy, Tablet, PC, Handy) wie in Python.</summary>
    private static void RundeWiePython(Aufbau a, JsonElement soll, bool mitExport, string[]? runde = null)
    {
        runde ??= Runde;
        var schritte = soll.EnumerateArray().ToList();
        Assert.Equal(runde.Length, schritte.Count);
        for (var i = 0; i < runde.Length; i++)
        {
            var g = runde[i];
            Assert.Equal(g, schritte[i].GetProperty("geraet").GetString());
            var r = a.Abgleichen(g);
            var wo = "Schritt " + i + " " + g + ": ";
            if (mitExport)
            {
                Assert.Equal(wo + schritte[i].GetProperty("export").GetString(), wo + PythonJson.Text(r.Export));
            }
            Assert.Equal(wo + schritte[i].GetProperty("received").GetInt32(), wo + r.Erhalten);
            Assert.Equal(wo + schritte[i].GetProperty("gone").GetInt32(), wo + r.Weg);
        }
    }

    /// <summary>kurzstand() aus dem Erzeuger: Durchgang, Geld, Ansehen, Tag, Historie, Doppel, Fingerabdruck.</summary>
    private static void KurzWiePython(Aufbau a, string g, JsonElement soll)
    {
        var verwaltung = a.Verwaltung[g];
        var s = verwaltung.Stand;
        var felder = s.Felder();
        var anzahl = verwaltung.Lauf is null ? 0 : LaufEreignisse(a.Lernstaende[g], verwaltung.Lauf).Count;
        var ist = new PyObjekt
        {
            { "lauf", verwaltung.Lauf }, { "geld", felder["money"] }, { "ansehen", PyAusgabe.Wert(felder["reputation"]) }, { "tag", s.Tag },
            { "history", (long)s.Verlauf.Count }, { "uebersprungen", (long)(anzahl - s.Verlauf.Count) },
            { "fingerabdruck", SpielFingerabdruckTests.Hash(s) },
        };
        Assert.Equal(g + " " + T(soll), g + " " + T(ist));
    }

    private static PyObjekt Geloest(int nummer, long tag) => new()
    {
        { "aufgabe", ((PyObjekt)((List<object?>)Inhalt["aufgaben"]!)[nummer]!)["id"] }, { "tag", tag }, { "richtig", true }, { "geld", 60L },
        { "reputation", Ruf() },
    };

    private static PyObjekt Ruf()
    {
        var ruf = new PyObjekt();
        foreach (var k in Ereignisse.Achsen)
        {
            Py.Setzen(ruf, k, 1L);
        }
        return ruf;
    }

    [Fact]
    public void AbgleichZwischenGeraetenWiePython()
    {
        using var a = new Aufbau();
        var vorher = Geraete.ToDictionary(g => g, g => SpielTabellen.ToDictionary(t => t.Name, t => Zeilen(a.Lernstaende[g], t.Name, t.Spalten)));
        foreach (var g in Geraete)
        {
            Assert.Equal(g + " " + T(Soll.GetProperty("oeffnen").GetProperty(g)), g + " " + T(a.Verwaltung[g].Lauf));
        }

        // Runde 1: jeder Export genau wie Python, danach hat jedes Geraet dieselben Spielzeilen
        RundeWiePython(a, Soll.GetProperty("runde1"), mitExport: true);
        var nach1 = Soll.GetProperty("nach1");
        foreach (var g in Geraete)
        {
            foreach (var (name, spalten) in SpielTabellen)
            {
                var zeilen = Zeilen(a.Lernstaende[g], name, spalten);
                var soll = nach1.GetProperty("tabellen").GetProperty(name);
                Assert.Equal(g + " " + name + " " + soll.GetProperty("anzahl").GetInt32(), g + " " + name + " " + zeilen.Count);
                Assert.Equal(g + " " + name + " " + soll.GetProperty("sha256").GetString(), g + " " + name + " " + Sha(T(zeilen)));
            }
        }

        // L2: das Handy spielte im Durchgang, den der PC geloescht hat -> neu waehlen wie _resolve_run
        foreach (var g in new[] { "PC", "Handy" })
        {
            a.Verwaltung[g].Neuladen();
            Assert.Equal(g + " " + nach1.GetProperty("aktiv").GetProperty(g).GetString(), g + " " + a.Verwaltung[g].Lauf);
            Assert.Equal(g + " " + T(nach1.GetProperty("plaetze").GetProperty(g)), g + " " + T(a.Verwaltung[g].Plaetze().Cast<object?>().ToList()));
        }
        Assert.Equal(Lauf("gemein"), a.Verwaltung["Handy"].Lauf);
        NachgerechnetWiePython(a.Lernstaende["PC"], nach1.GetProperty("laeufe"));
        using (var v = a.Lernstaende["PC"].Verbindung())
        {
            var belegung = Spielstaende.Belegung(Spielstaende.Zeilen(v), Spielstaende.HatAlteEreignisse(v));
            Assert.Equal(T(nach1.GetProperty("belegung")), T(new PyObjekt
            {
                { "plaetze", belegung.Plaetze.Select(p => (object?)new List<object?> { p.Platz, p.Lauf }).ToList() },
                { "namen", belegung.Namen.Select(p => (object?)new List<object?> { p.Platz, p.Name }).ToList() },
                { "extra", belegung.Extra.Cast<object?>().ToList() },
                { "geloescht", belegung.Geloescht.Order(StringComparer.Ordinal).Cast<object?>().ToList() },
            }));
        }
        Assert.Equal(T(nach1.GetProperty("bestenliste")), T(SpielDatenbank.Bestenzeilen(a.Lernstaende["PC"])
            .Select(z => (object?)new List<object?> { z.Zeit, z.Lauf, z.Art, z.Schluessel, z.Wert, z.Daten }).ToList()));

        // Ein Spielschritt je Geraet; das Tablet hat noch keinen Durchgang und waehlt ihn erst zum Schreiben (95)
        Assert.Null(a.Verwaltung["Tablet"].Lauf);
        a.Verwaltung["PC"].ZumSchreiben().Log(Ereignisse.Erledigt, Geloest(50, 4));
        a.Verwaltung["Handy"].ZumSchreiben().Log(Ereignisse.Erledigt, Geloest(51, 4));
        a.Verwaltung["Tablet"].ZumSchreiben().Log(Ereignisse.Erledigt, Geloest(52, 4));
        foreach (var g in Geraete)
        {
            KurzWiePython(a, g, Soll.GetProperty("schritt1").GetProperty(g));
        }

        // Runde 2 (vorher schreibt die Schlange), dann L6: der PC spielt weiter, waehrend fremde Zeilen dazukamen
        RundeWiePython(a, Soll.GetProperty("runde2"), mitExport: false);
        Assert.Equal(0, a.Schlange.Anzahl);
        var pc = a.Verwaltung["PC"].Ablauf!;
        pc.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 4L }, { "gehalt", 140L } });
        Assert.Equal(0, pc.NichtGespeichert);
        a.Schlange.Leeren();
        Assert.Equal(a.Verwaltung["PC"].Ablauf!.Speicher.Stempel(), Spielstempel(a.Lernstaende["PC"], Lauf("gemein")));
        Assert.Equal(T(PyAusgabe.Wert(new Spielstand(LaufEreignisse(a.Lernstaende["PC"], Lauf("gemein")), Inhalt).Felder())),
            T(PyAusgabe.Wert(pc.Stand.Felder())));
        KurzWiePython(a, "PC", Soll.GetProperty("schritt2").GetProperty("PC"));
        foreach (var g in new[] { "Handy", "Tablet" })
        {
            a.Verwaltung[g].Neuladen();
            KurzWiePython(a, g, Soll.GetProperty("schritt2").GetProperty(g));
        }

        // Runde 3: alle Geraete gleich wie Python
        RundeWiePython(a, Soll.GetProperty("runde3"), mitExport: false);
        var ende = Soll.GetProperty("ende");
        foreach (var g in Geraete)
        {
            a.Verwaltung[g].Neuladen();
            KurzWiePython(a, g, ende.GetProperty("stand").GetProperty(g));
            foreach (var (name, spalten) in SpielTabellen)
            {
                Assert.Equal(g + " " + name + " " + ende.GetProperty("anzahl").GetProperty(name).GetInt32(),
                    g + " " + name + " " + Zeilen(a.Lernstaende[g], name, spalten).Count);
            }
            var besten = SpielDatenbank.Bestenzeilen(a.Lernstaende[g])
                .Select(z => (object?)new List<object?> { z.Lauf, z.Art, z.Schluessel, z.Wert }).ToList();
            besten.Sort((x, y) => string.CompareOrdinal(T(x), T(y)));
            var soll = ende.GetProperty("bestenliste").EnumerateArray().Select(z => (object?)Py.Lesen(z.GetRawText())).ToList();
            soll.Sort((x, y) => string.CompareOrdinal(T(x), T(y)));
            Assert.Equal(g + " " + T(soll), g + " " + T(besten));
        }

        // R3: jede Zeile jedes Geraets steht auf jedem Geraet genau einmal (Ereignisse des geloeschten Durchgangs fallen weg)
        var drei = Lauf("drei");
        foreach (var (name, spalten) in SpielTabellen)
        {
            var alle = Geraete.SelectMany(g => vorher[g][name]).Where(z => !(name == "spiel_ereignisse" && Equals(z[6], drei)))
                .Select(T).ToHashSet(StringComparer.Ordinal);
            var bezug = Zeilen(a.Lernstaende["PC"], name, spalten).Select(T).ToList();
            foreach (var g in Geraete)
            {
                var zeilen = Zeilen(a.Lernstaende[g], name, spalten).Select(T).ToList();
                Assert.Equal(zeilen.Count, zeilen.Distinct(StringComparer.Ordinal).Count());
                Assert.Equal(bezug, zeilen);
                var fehlt = alle.Where(z => !zeilen.Contains(z)).ToList();
                Assert.True(fehlt.Count == 0, g + " " + name + ": Zeile verloren " + string.Join(" / ", fehlt));
                var uids = zeilen.Select(z => z[..z.IndexOf(',', StringComparison.Ordinal)]).ToList();
                Assert.Equal(uids.Count, uids.Distinct(StringComparer.Ordinal).Count());
            }
        }
        Assert.Empty(LaufEreignisse(a.Lernstaende["Handy"], drei));
    }

    /// <summary>
    /// FW8 (Sammelliste 120): Sonderfaelle wie Python (G51 sonderfaelle, nur angehaengt). Zwei Geraete schrieben in
    /// derselben Sekunde (je ein Zwischenfall und Feierabend 2, E-5), der PC hat alte Ereignisse ohne Durchgang;
    /// nach dem Abgleich loest der PC ohne Neuladen ein Ticket: Neuaufbau mit den fremden Zeilen und dabei das
    /// Abzeichen "krisenfest", das erst beide Geraete zusammen erreichen. Exporte, Ereignisse, Abzeichen,
    /// Bestenliste, Kurzstand je Geraet (die Reihenfolge gleicher Sekunden folgt der id des Geraets, darum
    /// unterscheiden sich PC und Handy wie in Python), der alte Durchgang und die Belegung der Plaetze.
    /// </summary>
    [Fact]
    public void SonderfaelleWiePython()
    {
        var sf = Soll.GetProperty("sonderfaelle");
        string[] zwei = ["PC", "Handy"];
        using var a = new Aufbau(sf.GetProperty("vorher"), zwei);
        var lauf = sf.GetProperty("lauf").GetString()!;
        foreach (var g in zwei)
        {
            Assert.Equal(g + " " + T(sf.GetProperty("oeffnen").GetProperty(g)), g + " " + T(a.Verwaltung[g].Lauf));
        }
        RundeWiePython(a, sf.GetProperty("runde1"), mitExport: true, ["PC", "Handy", "PC"]);

        // der PC loest ohne Neuladen einen Zwischenfall: Neuaufbau mit den fremden Zeilen, dann das Abzeichen
        var vorSchritt = LaufEreignisse(a.Lernstaende["PC"], lauf).Count;
        var daten = Geloest(6, 3);
        Py.Setzen(daten, "zwischenfall", true);
        a.Verwaltung["PC"].ZumSchreiben().Log(Ereignisse.Erledigt, daten);
        a.Schlange.Leeren();
        var schritt = sf.GetProperty("schritt");
        KurzWiePython(a, "PC", schritt.GetProperty("kurz"));
        Assert.Equal(T(schritt.GetProperty("neu")), T(LaufEreignisse(a.Lernstaende["PC"], lauf).Skip(vorSchritt)
            .Select(e => (object?)new List<object?> { e.Art, e.Daten }).ToList()));
        var abzeichen = a.Verwaltung["PC"].Stand.Verlauf.Where(e => e.Art == Ereignisse.Erfolg)
            .Select(e => (object?)new List<object?> { e.Daten["erfolg"], e.Daten["stufe"] }).Distinct(new TextGleich()).ToList();
        abzeichen.Sort((x, y) => string.CompareOrdinal(T(x), T(y)));
        Assert.Equal(T(schritt.GetProperty("abzeichen")), T(abzeichen));
        // Bestenliste ohne die Zeit der Zeile (die neuen Zeilen haben die Zeit von C#), sortiert wie in Python
        var besten = SpielDatenbank.Bestenzeilen(a.Lernstaende["PC"])
            .Select(z => (object?)new List<object?> { z.Lauf, z.Art, z.Schluessel, z.Wert }).ToList();
        besten.Sort((x, y) => string.CompareOrdinal(T(x), T(y)));
        var bestenSoll = schritt.GetProperty("bestenliste").EnumerateArray().Select(z => (object?)Py.Lesen(z.GetRawText())).ToList();
        bestenSoll.Sort((x, y) => string.CompareOrdinal(T(x), T(y)));
        Assert.Equal(T(bestenSoll), T(besten));

        a.Verwaltung["Handy"].Neuladen();
        KurzWiePython(a, "Handy", sf.GetProperty("handy"));
        RundeWiePython(a, sf.GetProperty("runde2"), mitExport: false, zwei);

        foreach (var g in zwei)
        {
            var ende = sf.GetProperty("ende").GetProperty(g);
            a.Verwaltung[g].Neuladen();
            KurzWiePython(a, g, ende.GetProperty("kurz"));
            foreach (var (name, spalten) in SpielTabellen)
            {
                Assert.Equal(g + " " + name + " " + ende.GetProperty("tabellen").GetProperty(name).GetInt32(),
                    g + " " + name + " " + Zeilen(a.Lernstaende[g], name, spalten).Count);
            }
            // der alte Durchgang ohne lauf (wie vor 0.48)
            var alt = LaufEreignisse(a.Lernstaende[g], LernstandSchreiber.AlterLauf);
            var altStand = new Spielstand(alt, Inhalt);
            var altSoll = ende.GetProperty("alt");
            Assert.Equal(g + " " + T(altSoll), g + " " + T(new PyObjekt
            {
                { "anzahl", (long)alt.Count },
                { "ereignisse", Sha(T(alt.Select(e => (object?)new List<object?> { e.Art, e.Daten }).ToList())) },
                { "geld", altStand.Felder()["money"] }, { "tag", altStand.Tag }, { "history", (long)altStand.Verlauf.Count },
                { "fingerabdruck", SpielFingerabdruckTests.Hash(altStand) },
            }));
            using (var v = a.Lernstaende[g].Verbindung())
            {
                var belegung = Spielstaende.Belegung(Spielstaende.Zeilen(v), Spielstaende.HatAlteEreignisse(v));
                Assert.Equal(g + " " + T(ende.GetProperty("belegung")), g + " " + T(new PyObjekt
                {
                    { "plaetze", belegung.Plaetze.Select(p => (object?)new List<object?> { p.Platz, p.Lauf }).ToList() },
                    { "extra", belegung.Extra.Cast<object?>().ToList() },
                    { "geloescht", belegung.Geloescht.Order(StringComparer.Ordinal).Cast<object?>().ToList() },
                }));
            }
            Assert.Equal(g + " " + T(ende.GetProperty("plaetze")), g + " " + T(a.Verwaltung[g].Plaetze().Cast<object?>().ToList()));
        }
    }

    private sealed class TextGleich : IEqualityComparer<object?>
    {
        public new bool Equals(object? x, object? y) => T(x) == T(y);

        public int GetHashCode(object? obj) => StringComparer.Ordinal.GetHashCode(T(obj));
    }

    private static SpielStempel? Spielstempel(Lernstand lernstand, string lauf) =>
        lernstand.Abfragen("SELECT COUNT(*), MAX(id), MAX(timestamp) FROM spiel_ereignisse WHERE lauf = $lauf", ("$lauf", lauf)) is [var z, ..]
            ? new SpielStempel((long)z[0]!, z[1] as long?, z[2])
            : null;

    /// <summary>Je Durchgang dieselben Ereignisse, jedes Feld des Spielstands gleich (wie G44), dazu die Doppel nach E-5.</summary>
    private static void NachgerechnetWiePython(Lernstand lernstand, JsonElement laeufe)
    {
        foreach (var lauf in laeufe.EnumerateObject())
        {
            var wo = lauf.Name;
            var ereignisse = LaufEreignisse(lernstand, lauf.Name);
            Assert.Equal(wo + " " + lauf.Value.GetProperty("anzahl").GetInt32(), wo + " " + ereignisse.Count);
            Assert.Equal(wo + " " + lauf.Value.GetProperty("ereignisse").GetString(),
                wo + " " + Sha(T(ereignisse.Select(e => (object?)new List<object?> { e.Zeit, e.Art, e.Daten }).ToList())));
            var s = new Spielstand(ereignisse, Inhalt);
            Assert.Equal(wo + " " + lauf.Value.GetProperty("uebersprungen").GetInt32(), wo + " " + (ereignisse.Count - s.Verlauf.Count));
            var stand = lauf.Value.GetProperty("stand");
            var felder = s.Felder();
            foreach (var feld in stand.GetProperty("pruefsummen").EnumerateObject())
            {
                Assert.Equal(wo + " " + feld.Name + " " + feld.Value.GetString(), wo + " " + feld.Name + " " + Sha(T(PyAusgabe.Wert(felder[feld.Name]))));
            }
            Assert.Equal(stand.GetProperty("anzahl_history").GetInt32(), s.Verlauf.Count);
        }
        Assert.Equal(2, laeufe.GetProperty(Lauf("gemein")).GetProperty("uebersprungen").GetInt32());
    }

    /// <summary>
    /// Sammelliste 93 (L5): eine Zahl als Text in der Spalte wert speichert SQLite als Zahl (REAL), beide Seiten
    /// lesen 12.5. Nur ein Text, der keine Zahl ist, bleibt Text: Python 0.63.2 gibt ihn roh weiter und die
    /// Bestenliste bricht mit ValueError ab; C# liest ihn als leer und zeigt die Bestenliste weiter.
    /// </summary>
    [Fact]
    public void BestenlisteMitTextWert()
    {
        var soll = Soll.GetProperty("bestenliste_text");
        Assert.Equal("[[12.5,\"float\"],[\"abc\",\"str\"]]", soll.GetProperty("werte").GetRawText());
        Assert.Equal("ValueError", soll.GetProperty("record_board").GetString());
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var nummer = 0;
        foreach (var roh in new[] { "12.5", "abc" })
        {
            Assert.True(lernstand.Schreiben("INSERT INTO spiel_bestenliste (timestamp, utc_offset, durchgang, art, schluessel, wert, daten, uid)"
                + " VALUES ($t, 120, 'ltext', $a, 'diensttage', $w, '{}', $u)",
                ("$t", "2026-10-02 09:00:0" + nummer), ("$a", Betrieb.BestWert), ("$w", roh), ("$u", "text" + nummer++)));
        }
        var zeilen = SpielDatenbank.Bestenzeilen(lernstand);
        Assert.Equal([12.5, null], zeilen.Select(z => z.Wert));
        var tafel = Betrieb.BestenTafel(zeilen, null, Inhalt);
        Assert.NotNull(tafel);
    }

    /// <summary>
    /// Weisung P8-5 Punkt 4: Python 0.63.2 nimmt Ereignisse, die nur C# schreibt (stadt_*, teil_gekauft), in die
    /// Historie auf und rechnet sonst nichts damit. Ein Teil aus dem Elektromarkt kostet in Python also kein Geld;
    /// C# zieht es ab. Alles andere rechnet C# gleich.
    /// </summary>
    [Fact]
    public void UnbekannteArtenInPython()
    {
        var soll = Soll.GetProperty("unbekannte_arten");
        var ereignisse = soll.GetProperty("ereignisse").EnumerateArray()
            .Select(e => new SpielEreignis(e[0].GetString(), e[1].GetString()!, (PyObjekt)Py.Lesen(e[2].GetRawText())!)).ToList();
        var s = new Spielstand(ereignisse, Inhalt);
        Assert.Equal(soll.GetProperty("history").GetInt32(), s.Verlauf.Count);
        Assert.Equal(T(soll.GetProperty("arten")), T(s.Verlauf.Select(e => (object?)e.Art).ToList()));
        Assert.Equal(soll.GetProperty("tag").GetInt64(), s.Tag);
        // Python: nur das Gehalt; C#: Gehalt minus 80 fuer die Teile
        Assert.Equal(140L, soll.GetProperty("geld").GetInt64());
        Assert.Equal(soll.GetProperty("geld").GetInt64() - 80, Py.Int(s.Felder()["money"]));
    }
    /// <summary>
    /// L3 (Nicos Antwort 1a): PC und Handy gingen am selben Spieltag offline durch die Stadt. Nach dem Abgleich
    /// nimmt die Tagesuhr das Geraet, das am weitesten gespielt hat (mit seinem Ort), statt die Minuten zu
    /// addieren. Ein Spiel ohne Geraete (im Arbeitsspeicher) rechnet wie bisher.
    /// </summary>
    [Fact]
    public void TagesuhrNimmtWeitestesGeraet()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var n = 0;
        var start = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
        var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n)),
            () => "uhr" + n.ToString("D6", CultureInfo.InvariantCulture));
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        const string LaufId = "luhr000000000001";
        Assert.True(schreiber.SpielPlatzSpeichern(1, LaufId, Spielstaende.Angelegt, "{}", "PC"));
        void Schreiben(string geraet, string art, PyObjekt daten) =>
            Assert.NotNull(schreiber.SpielEreignisSpeichern(art, PythonJson.Standard(daten), geraet, LaufId));
        Schreiben("PC", Ereignisse.Profil, new PyObjekt { { "name", "Nico" }, { "aussehen", new PyObjekt() } });
        Schreiben("PC", Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", stadt.Heim }, { "nach", "moebelhaus" }, { "minuten", 300L } });
        Schreiben("Handy", Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", stadt.Heim }, { "nach", "elektromarkt" }, { "minuten", 120L } });
        Schreiben("PC", Stadt.Gewartet, new PyObjekt { { "tag", 1L }, { "ort", "moebelhaus" }, { "minuten", 60L } });

        var handy = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "Handy", LaufId, Vorgaenge.Sperren));
        Assert.Equal(new StadtStand(1, "moebelhaus", stadt.Beginn + 360), stadt.Stand(handy.Stand));
        // ohne Geraete (alles eins) wie bisher: Summe, Ort des letzten Gangs
        var eins = new Spielstand(handy.Stand.Verlauf, Inhalt);
        Assert.Equal(new StadtStand(1, "elektromarkt", stadt.Beginn + 480), stadt.Stand(eins));

        // das Handy geht weiter (schrittweise angewendet, eigenes Geraet) und ist jetzt weiter als der PC
        handy.Log(Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", "elektromarkt" }, { "nach", "bank" }, { "minuten", 300L } });
        Assert.Equal(new StadtStand(1, "bank", stadt.Beginn + 420), stadt.Stand(handy.Stand));
        // neu gelesen: dasselbe
        handy.Neuaufbau();
        Assert.Equal(new StadtStand(1, "bank", stadt.Beginn + 420), stadt.Stand(handy.Stand));
        // der PC sieht nach dem Abgleich dasselbe
        var pc = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "PC", LaufId, Vorgaenge.Sperren));
        Assert.Equal(new StadtStand(1, "bank", stadt.Beginn + 420), stadt.Stand(pc.Stand));

        // der PC geht ab dem gezeigten Stand weiter (Bank, 420), nicht ab seinem eigenen (Moebelhaus, 360):
        // die Figur bewegt sich, und die Uhr laeuft ab der gezeigten Zeit
        var gang = stadt.GehenPruefen(pc.Stand, stadt.Heim);
        Assert.True(gang.Ok);
        Assert.Equal("bank", gang.Daten!["von"]);
        pc.Log(gang.Art!, gang.Daten!);
        Assert.Equal(new StadtStand(1, stadt.Heim, stadt.Beginn + 420 + Py.Int(gang.Daten!["minuten"])), stadt.Stand(pc.Stand));
        pc.Neuaufbau();
        Assert.Equal(new StadtStand(1, stadt.Heim, stadt.Beginn + 420 + Py.Int(gang.Daten!["minuten"])), stadt.Stand(pc.Stand));
        // ein drittes Geraet, das an diesem Tag noch nicht gespielt hat, geht ebenso ab dem gezeigten Stand
        var tablet = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "Tablet", LaufId, Vorgaenge.Sperren));
        var vorher = stadt.Stand(tablet.Stand);
        var weiter = stadt.GehenPruefen(tablet.Stand, "bank");
        Assert.True(weiter.Ok);
        tablet.Log(weiter.Art!, weiter.Daten!);
        Assert.Equal(new StadtStand(1, "bank", vorher.Uhr + Py.Int(weiter.Daten!["minuten"])), stadt.Stand(tablet.Stand));
    }

    /// <summary>
    /// FW8 (Sammelliste 118, Gegenprobe): PC und Handy schreiben in derselben Sekunde dieselbe Art. Mit
    /// verschiedenen Daten gehoert jede Zeile ihrem Geraet (Tagesuhr wie bei verschiedenen Sekunden, keine Summe);
    /// mit gleichen Daten bekommt jedes Geraet einen der beiden gleichen Schritte. Vorher galten beide Zeilen als
    /// eigenes Geraet und die Minuten wurden addiert (rot).
    /// </summary>
    [Fact]
    public void TagesuhrGleicheSekundeZweierGeraete()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var jetzt = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
        var n = 0;
        var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => jetzt),
            () => "sek" + (++n).ToString("D6", CultureInfo.InvariantCulture));
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        void Schreiben(string lauf, string geraet, string art, PyObjekt daten) =>
            Assert.NotNull(schreiber.SpielEreignisSpeichern(art, PythonJson.Standard(daten), geraet, lauf));
        PyObjekt Gang(string nach, long minuten) => new() { { "tag", 1L }, { "von", stadt.Heim }, { "nach", nach }, { "minuten", minuten } };

        // verschiedene Daten in derselben Sekunde
        const string Verschieden = "lsek000000000001";
        Assert.True(schreiber.SpielPlatzSpeichern(1, Verschieden, Spielstaende.Angelegt, "{}", "PC"));
        Schreiben(Verschieden, "PC", Ereignisse.Profil, new PyObjekt { { "name", "Nico" }, { "aussehen", new PyObjekt() } });
        jetzt = jetzt.AddSeconds(5);
        Schreiben(Verschieden, "PC", Stadt.Gegangen, Gang("moebelhaus", 300));
        Schreiben(Verschieden, "Handy", Stadt.Gegangen, Gang("elektromarkt", 120));
        foreach (var geraet in new[] { "PC", "Handy" })
        {
            var spiel = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, geraet, Verschieden, Vorgaenge.Sperren));
            Assert.Equal(new StadtStand(1, "moebelhaus", stadt.Beginn + 300), stadt.Stand(spiel.Stand));
        }

        // gleiche Daten in derselben Sekunde: zwei gleiche Schritte, je einer je Geraet
        const string Gleich = "lsek000000000002";
        Assert.True(schreiber.SpielPlatzSpeichern(2, Gleich, Spielstaende.Angelegt, "{}", "PC"));
        jetzt = jetzt.AddSeconds(5);
        Schreiben(Gleich, "PC", Ereignisse.Profil, new PyObjekt { { "name", "Nico" }, { "aussehen", new PyObjekt() } });
        jetzt = jetzt.AddSeconds(5);
        Schreiben(Gleich, "PC", Stadt.Gegangen, Gang("bank", 60));
        Schreiben(Gleich, "Handy", Stadt.Gegangen, Gang("bank", 60));
        foreach (var geraet in new[] { "PC", "Handy", "Tablet" })
        {
            var spiel = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, geraet, Gleich, Vorgaenge.Sperren));
            Assert.Equal(new StadtStand(1, "bank", stadt.Beginn + 60), stadt.Stand(spiel.Stand));
        }
    }

    /// <summary>
    /// FW8 (Sammelliste 119, Gegenprobe): ohne Abgleich auf beiden Geraeten gespielt, beide stehen am selben Ort und
    /// der PC ist weiter. Der Schritt des Handys (offline geschrieben, ohne Merker) rechnet ab dem eigenen Stand des
    /// Handys, nicht ab dem vorderen des PCs (vorher: Teilsumme, rot). Geht ein Geraet nach dem Abgleich vom
    /// gezeigten Stand eines anderen aus, schreibt es den Merker ab_uhr und rechnet auf jedem Geraet ab diesem;
    /// offline (nur eigene Zeilen) schreibt es nie einen Merker.
    /// </summary>
    [Fact]
    public void TagesuhrOhneAbgleichAmSelbenOrt()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var n = 0;
        var start = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
        var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n)),
            () => "ort" + n.ToString("D6", CultureInfo.InvariantCulture));
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        const string LaufId = "lort000000000001";
        Assert.True(schreiber.SpielPlatzSpeichern(1, LaufId, Spielstaende.Angelegt, "{}", "PC"));
        void Schreiben(string geraet, string art, PyObjekt daten) =>
            Assert.NotNull(schreiber.SpielEreignisSpeichern(art, PythonJson.Standard(daten), geraet, LaufId));
        Schreiben("PC", Ereignisse.Profil, new PyObjekt { { "name", "Nico" }, { "aussehen", new PyObjekt() } });

        // offline: das Handy kennt nur seine eigenen Zeilen, sein Schritt hat keinen Merker
        using (var handyOrdner = new TempOrdner())
        {
            var allein = new Lernstand(Path.Combine(handyOrdner.Pfad, Lernstand.DateiName));
            var handySchreiber = new LernstandSchreiber(allein, SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n)));
            Assert.True(handySchreiber.SpielPlatzSpeichern(1, LaufId, Spielstaende.Angelegt, "{}", "Handy"));
            var offline = new Spielablauf(Inhalt, new SpielDatenbank(handySchreiber, "Handy", LaufId, Vorgaenge.Sperren));
            offline.Profil("Nico", new PyObjekt());
            var gang = stadt.GehenPruefen(offline.Stand, "moebelhaus");
            Assert.True(gang.Ok);
            Assert.DoesNotContain(gang.Daten!, p => p.Key == Stadt.AbUhr);
        }

        Schreiben("PC", Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", stadt.Heim }, { "nach", "moebelhaus" }, { "minuten", 300L } });
        Schreiben("Handy", Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", stadt.Heim }, { "nach", "moebelhaus" }, { "minuten", 120L } });
        Schreiben("Handy", Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", "moebelhaus" }, { "nach", "bank" }, { "minuten", 60L } });
        foreach (var geraet in new[] { "PC", "Handy", "Tablet" })
        {
            var spiel = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, geraet, LaufId, Vorgaenge.Sperren));
            // PC: Moebelhaus 300; Handy: Moebelhaus 120, dann Bank 180 (nicht 360)
            Assert.Equal(new StadtStand(1, "moebelhaus", stadt.Beginn + 300), stadt.Stand(spiel.Stand));
        }

        // nach dem Abgleich geht das Handy vom gezeigten Stand des PCs aus: Merker, auf jedem Geraet ab diesem
        var handy = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "Handy", LaufId, Vorgaenge.Sperren));
        var weiter = stadt.GehenPruefen(handy.Stand, "bank");
        Assert.True(weiter.Ok);
        Assert.Equal("moebelhaus", weiter.Daten!["von"]);
        Assert.Equal(stadt.Beginn + 300, weiter.Daten![Stadt.AbUhr]);
        handy.Log(weiter.Art!, weiter.Daten!);
        var minuten = Py.Int(weiter.Daten!["minuten"]);
        Assert.Equal(new StadtStand(1, "bank", stadt.Beginn + 300 + minuten), stadt.Stand(handy.Stand));
        foreach (var geraet in new[] { "PC", "Handy", "Tablet" })
        {
            var spiel = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, geraet, LaufId, Vorgaenge.Sperren));
            Assert.Equal(new StadtStand(1, "bank", stadt.Beginn + 300 + minuten), stadt.Stand(spiel.Stand));
        }
        // der PC geht danach ab dem gezeigten Stand des Handys weiter (wieder mit Merker)
        var pc = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "PC", LaufId, Vorgaenge.Sperren));
        var heim = stadt.GehenPruefen(pc.Stand, stadt.Heim);
        Assert.Equal(stadt.Beginn + 300 + minuten, heim.Daten![Stadt.AbUhr]);
        // der PC geht dann selbst weiter: ab seinem eigenen (gezeigten) Stand, ohne Merker
        pc.Log(heim.Art!, heim.Daten!);
        var nochmal = stadt.GehenPruefen(pc.Stand, "bank");
        Assert.DoesNotContain(nochmal.Daten!, p => p.Key == Stadt.AbUhr);
    }

    /// <summary>
    /// FW8 (Sammelliste 118b), feste, benannte Ausnahme: zwei Geraete mit demselben Namen (seit 24b heisst jedes
    /// Geraet nur "PC" oder "Handy", z. B. zwei PCs) sind fuer die Tagesuhr ein Geraet. Spielen beide offline am selben
    /// Tag, zaehlen ihre Schritte als eine Folge (die Uhr laeuft weiter als auf jedem einzelnen). Ohne eine Kennung je
    /// Installation nicht zu trennen (Weisung P11-16).
    /// </summary>
    [Fact]
    public void TagesuhrGleichnamigeGeraeteSindEinGeraet_Ausnahme()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var n = 0;
        var start = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
        var schreiber = new LernstandSchreiber(lernstand, SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n)),
            () => "pcs" + n.ToString("D6", CultureInfo.InvariantCulture));
        var stadt = new Stadt(Inhalt, SpielStadtTests.Daten.Value);
        const string LaufId = "lpcs000000000001";
        Assert.True(schreiber.SpielPlatzSpeichern(1, LaufId, Spielstaende.Angelegt, "{}", "PC"));
        void Schreiben(string art, PyObjekt daten) =>
            Assert.NotNull(schreiber.SpielEreignisSpeichern(art, PythonJson.Standard(daten), "PC", LaufId));
        Schreiben(Ereignisse.Profil, new PyObjekt { { "name", "Nico" }, { "aussehen", new PyObjekt() } });
        // PC 1 und PC 2 offline: je ein Gang ab zu Hause
        Schreiben(Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", stadt.Heim }, { "nach", "moebelhaus" }, { "minuten", 300L } });
        Schreiben(Stadt.Gegangen, new PyObjekt { { "tag", 1L }, { "von", stadt.Heim }, { "nach", "elektromarkt" }, { "minuten", 120L } });
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "PC", LaufId, Vorgaenge.Sperren));
        Assert.Equal(new StadtStand(1, "elektromarkt", stadt.Beginn + 420), stadt.Stand(spiel.Stand));
    }

    /// <summary>
    /// L4 (Sammelliste 92): laeuft gerade ein Abgleich (Sperre der Vorgaenge belegt), darf das Spiel lesen und
    /// Erfolge pruefen, wenn nichts zu schreiben ist; erst ein Schreiben meldet "belegt" und schreibt nichts.
    /// </summary>
    [Fact]
    public void SperreErstVorDemSchreiben()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var schreiber = new LernstandSchreiber(lernstand);
        const string LaufId = "lsperre000000001";
        Assert.True(schreiber.SpielPlatzSpeichern(1, LaufId, Spielstaende.Angelegt, "{}", "PC"));
        var frei = true;
        var spiel = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, "PC", LaufId, () => frei ? Vorgaenge.Sperren() : null));
        spiel.Profil("Nico", new PyObjekt());
        spiel.ErfolgePruefen();
        var zeilen = lernstand.Abfragen("SELECT COUNT(*) FROM spiel_ereignisse")[0][0];
        var besten = lernstand.Abfragen("SELECT COUNT(*) FROM spiel_bestenliste")[0][0];

        frei = false;   // ein Abgleich laeuft
        Assert.Empty(spiel.ErfolgePruefen());
        Assert.Empty(spiel.WissenPruefen());
        Assert.Throws<SpielBelegtAusnahme>(() => spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 140L } }));
        Assert.Equal(zeilen, lernstand.Abfragen("SELECT COUNT(*) FROM spiel_ereignisse")[0][0]);
        Assert.Equal(besten, lernstand.Abfragen("SELECT COUNT(*) FROM spiel_bestenliste")[0][0]);
        Assert.Equal(0, spiel.Stand.TageFertig);

        frei = true;    // danach geht es weiter, die geschachtelte Aktion haelt die Sperre bis zum Ende
        spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 140L } });
        Assert.Equal(1, spiel.Stand.TageFertig);
        Assert.Equal(0, spiel.NichtGespeichert);
    }
}
