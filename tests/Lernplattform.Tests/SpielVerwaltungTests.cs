using System.Globalization;
using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, Weisung P7-6 (Sammelliste 94): Vergleichswerte G44 Teil "Verwaltung"
/// (<c>spiel_verwaltung.json</c>, Erzeuger <c>werkzeuge/vergleich/erzeuge_spiel_verwaltung.py</c>):
/// C# spielt dieselben Schritte der Platzverwaltung (neuer Durchgang, Doppel-Tipp, waehlen,
/// umbenennen, loeschen, zuruecksetzen, Loeschen auf dem anderen Geraet) mit derselben Uhr und denselben
/// Kennungen und ergibt nach jedem Schritt dieselben Zeilen in spiel_ereignisse, spiel_plaetze,
/// spiel_bestenliste und sync_meta, denselben aktiven Durchgang und dieselbe Uebersicht wie Python 0.63.2.
/// </summary>
public class SpielVerwaltungTests
{
    private static readonly JsonElement Soll = VergleichsHilfe.Lade("spiel_verwaltung.json");

    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static object? W(JsonElement e) => Py.Lesen(e.GetRawText());

    private static string T(object? wert) => PythonJson.Text(wert);

    // erzeuge_spiel_verwaltung.AUSSEHEN
    private static PyObjekt Aussehen() => new()
    {
        { "haut", "hell" }, { "haare", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "hoodie" }, { "farbe", "blau" },
        { "hose", "jeans" }, { "brille", false }, { "bart", false }, { "extra", "headset" }, { "kreis", "gelb" },
    };

    private static void Gleich(object? soll, object? ist, string wo) =>
        Assert.True(T(soll) == T(ist), wo + "\n  Python: " + T(soll) + "\n  C#:     " + T(ist));

    private static List<object?> Tabelle(Lernstand lernstand, string sql) =>
        [.. lernstand.Abfragen(sql).Select(z => (object?)z.Select(w => w is DBNull ? null : w).ToList())];

    private static PyObjekt Tabellen(Lernstand l) => new()
    {
        { "spiel_ereignisse", Tabelle(l, "SELECT id, timestamp, utc_offset, typ, daten, geraet, uid, lauf FROM spiel_ereignisse ORDER BY id") },
        { "spiel_plaetze", Tabelle(l, "SELECT id, timestamp, platz, lauf, aktion, daten, geraet, uid FROM spiel_plaetze ORDER BY id") },
        { "spiel_bestenliste", Tabelle(l, "SELECT id, timestamp, utc_offset, durchgang, art, schluessel, wert, daten, uid FROM spiel_bestenliste ORDER BY id") },
        { "sync_meta", Tabelle(l, "SELECT key, value FROM sync_meta ORDER BY key") },
    };

    /// <summary>Die Uhr des Erzeugers: jedes Ablesen 1,5 s weiter, Ortszeit ab 2026-10-02 00:00 (MESZ).</summary>
    private static Uhr Laufende()
    {
        var start = new DateTimeOffset(2026, 10, 1, 22, 0, 0, TimeSpan.Zero);
        var n = 0;
        return SpeichernTests.FesteUhr(() => start.AddTicks(15_000_000L * ++n));
    }

    [Fact]
    public void VerwaltungWiePython()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var zaehler = 1L;   // fisi_core zieht beim Import schon eine uuid4 (PROCESS_TOKEN)
        // uuid.UUID(int=(n * 104729) << 64 | n * 7919).hex, fuer uid und Durchgang gemeinsam
        string Kennung()
        {
            zaehler++;
            return (zaehler * 104729).ToString("x16", CultureInfo.InvariantCulture) + (zaehler * 7919).ToString("x16", CultureInfo.InvariantCulture);
        }
        var schreiber = new LernstandSchreiber(lernstand, Laufende(), Kennung);
        Spielverwaltung? spiel = null;
        Spielverwaltung G() => spiel!;
        string Offen() => (string)G().Stand.OffeneTickets()[0]["id"]!;
        string Platz(long n) => G().Belegung().LaufAuf(n)!;
        Spielverwaltung Starten() => spiel = new Spielverwaltung(schreiber, "PC", Inhalt, Kennung, Vorgaenge.Sperren);

        var aktionen = new List<(string Name, Func<object?> Aktion)>
        {
            ("Start leer", () => Starten().Lauf),
            ("Profil ohne Platz (_ensure_run)", () => { G().ZumSchreiben().Profil("Anna", Aussehen(), Ereignisse.Einfach); return null; }),
            ("Ticket verschoben", () => G().ZumSchreiben().Verschieben(Offen())),
            ("Platz 1 belegt", () => G().NeuerLauf(1)),
            ("Platz 4 gibt es nicht", () => G().NeuerLauf(4)),
            ("Platz 0 gibt es nicht", () => G().NeuerLauf(0)),
            ("Platz 3 neu", () => G().NeuerLauf(3)),
            ("Platz 3 doppelt getippt", () => G().NeuerLauf(3)),
            ("Platz 3 Profil", () => { G().ZumSchreiben().Profil("  Ben mit einem sehr langen Namen fuer die Figur  ", Aussehen(), Ereignisse.Normal); return null; }),
            ("Platz 3 Ticket verschoben", () => G().ZumSchreiben().Verschieben(Offen())),
            ("Platz 2 neu", () => G().NeuerLauf(2)),
            ("Platz 3 noch einmal (belegt)", () => G().NeuerLauf(3)),
            ("Platz 1 waehlen", () => { G().LaufWaehlen(Platz(1)); return null; }),
            ("Unbekannten Durchgang waehlen", () => G().LaufWaehlen("lgibtsnicht0000000")),
            ("Platz 1 umbenannt", () => { G().PlatzUmbenennen(1, "   Hauptspiel   "); return null; }),
            ("Platz 3 umbenannt (lang)", () => { G().PlatzUmbenennen(3, "Ein Name, der viel zu lang fuer den Platz ist"); return null; }),
            ("Platz 2 umbenannt (leer)", () => { G().PlatzUmbenennen(2, "   "); return null; }),
            ("Neu gestartet", () => Starten().Lauf),
            ("Platz 3 loeschen (nicht aktiv)", () => G().LaufLoeschen(Platz(3))),
            ("Unbekannten Durchgang loeschen", () => G().LaufLoeschen("lgibtsnicht0000000")),
            ("Platz 1 zuruecksetzen", () => G().Zuruecksetzen()),
            ("Platz 1 neues Profil", () => { G().ZumSchreiben().Profil("Cara", Aussehen(), Ereignisse.Normal); return null; }),
            ("Platz 2 waehlen", () => { G().LaufWaehlen(Platz(2)); return null; }),
            ("Platz 2 loeschen (aktiv, ohne Profil)", () => G().LaufLoeschen(G().Lauf!)),
            ("Zuruecksetzen ohne Durchgang", () => G().Zuruecksetzen()),
            ("Neu gestartet ohne gespeicherten", () => Starten().Lauf),
            ("Platz 1 auf dem anderen Geraet geloescht", () =>
            {
                using (Vorgaenge.Sperren())
                {
                    schreiber.SpielLaufLoeschen(1, G().Lauf!, "Handy");
                }
                G().Neuladen();
                return null;
            }),
            ("Profil danach (_ensure_run)", () => { G().ZumSchreiben().Profil("Dora", Aussehen()); return null; }),
            ("Aktiven Durchgang loeschen (mit Profil)", () => G().LaufLoeschen(G().Lauf!, nameBehalten: true)),
            ("Neu gestartet am Ende", () => Starten().Lauf),
        };

        var schritte = Soll.GetProperty("schritte");
        Assert.Equal(schritte.GetArrayLength(), aktionen.Count);
        for (var i = 0; i < aktionen.Count; i++)
        {
            var soll = schritte[i];
            var wo = soll.GetProperty("name").GetString()!;
            Assert.Equal(wo, aktionen[i].Name);
            PyObjekt ergebnis;
            try
            {
                ergebnis = new PyObjekt { { "wert", aktionen[i].Aktion() } };
            }
            catch (SpielFehler fehler)
            {
                ergebnis = new PyObjekt { { "fehler", fehler.Message } };
            }
            Gleich(W(soll.GetProperty("ergebnis")), ergebnis, wo + ": Ergebnis");
            Gleich(W(soll.GetProperty("tabellen")), Tabellen(lernstand), wo + ": Tabellen");
            Gleich(W(soll.GetProperty("lauf")), G().Lauf, wo + ": run");
            Gleich(W(soll.GetProperty("platz")), G().PlatzVon(), wo + ": slot_of");
            Gleich(W(soll.GetProperty("anzeige")), G().AktivAnzeige(), wo + ": active_label");
            Gleich(W(soll.GetProperty("plaetze")), G().Plaetze(), wo + ": slots");
            Gleich(W(soll.GetProperty("spieler")), G().Stand.Profil is { } p ? p["name"] : null, wo + ": Spieler");
            Gleich(W(soll.GetProperty("ereignisse")), (long)G().Stand.Verlauf.Count, wo + ": Ereignisse");
        }
    }

    [Fact]
    public void BelegtSchreibtNichts()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var schreiber = new LernstandSchreiber(lernstand);
        var frei = true;
        var spiel = new Spielverwaltung(schreiber, "PC", Inhalt, sperre: () => frei ? Vorgaenge.Sperren() : null);
        spiel.ZumSchreiben().Profil("Anna", Aussehen());
        var lauf = spiel.Lauf!;
        string Stand() => T(Tabellen(lernstand));
        var vorher = Stand();
        frei = false;
        Assert.Throws<SpielBelegtAusnahme>(() => spiel.NeuerLauf(2));
        Assert.Throws<SpielBelegtAusnahme>(() => spiel.PlatzUmbenennen(1, "x"));
        Assert.Throws<SpielBelegtAusnahme>(() => spiel.LaufLoeschen(lauf));
        Assert.Throws<SpielBelegtAusnahme>(() => spiel.Zuruecksetzen());
        Assert.Throws<SpielBelegtAusnahme>(() => spiel.LaufWaehlen(lauf));
        Assert.Throws<SpielBelegtAusnahme>(() => spiel.ZumSchreiben().Verschieben(spiel.Stand.OffeneTickets()[0]["id"]));
        Assert.Equal(vorher, Stand());
        Assert.Equal(lauf, spiel.Lauf);
        // Start bei belegter Sperre: derselbe Durchgang, nichts geschrieben
        var neu = new Spielverwaltung(schreiber, "PC", Inhalt, sperre: () => null);
        Assert.Equal(lauf, neu.Lauf);
        Assert.Equal(vorher, Stand());
        // Veralteter Merker ohne Plaetze (Durchgang auf dem anderen Geraet geloescht), Sperre belegt:
        // Start, Neuladen und Zuruecksetzen werfen nicht und schreiben nichts
        using (Vorgaenge.Sperren())
        {
            schreiber.SpielLaufLoeschen(1, lauf, "Handy");
        }
        var geloescht = Stand();
        var ohne = new Spielverwaltung(schreiber, "PC", Inhalt, sperre: () => null);
        Assert.Null(ohne.Lauf);
        ohne.Neuladen();
        Assert.True(ohne.Zuruecksetzen());
        Assert.Equal(geloescht, Stand());
        Assert.Contains(lauf, geloescht);
        // Kontrollfehler: mit freier Sperre wird der Merker geleert
        var mit = new Spielverwaltung(schreiber, "PC", Inhalt, sperre: Vorgaenge.Sperren);
        Assert.Null(mit.Lauf);
        Assert.NotEqual(geloescht, Stand());
        // Kontrollfehler: mit freier Sperre schreibt dieselbe Aktion
        frei = true;
        spiel.PlatzUmbenennen(1, "x");
        Assert.NotEqual(vorher, Stand());
    }
}
