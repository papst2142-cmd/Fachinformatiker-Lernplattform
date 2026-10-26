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

        public Aufbau()
        {
            var start = DateTimeOffset.Parse("2026-10-03T06:00:00+00:00", CultureInfo.InvariantCulture);
            // ein gemeinsamer Takt fuer alle Geraete: die Spielschritte stehen in derselben Reihenfolge wie in Python
            var uhr = SpeichernTests.FesteUhr(() => start.AddSeconds(5 * ++n));
            foreach (var g in Geraete)
            {
                var lernstand = Baue(Soll.GetProperty("vorher").GetProperty(g),
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
    private static void RundeWiePython(Aufbau a, JsonElement soll, bool mitExport)
    {
        var schritte = soll.EnumerateArray().ToList();
        Assert.Equal(Runde.Length, schritte.Count);
        for (var i = 0; i < Runde.Length; i++)
        {
            var g = Runde[i];
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
