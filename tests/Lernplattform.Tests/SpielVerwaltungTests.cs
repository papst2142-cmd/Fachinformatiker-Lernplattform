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
/// Teil "alt" (Weisung P7-7, Sammelliste 97): zweite Datenbank mit dem alten Durchgang von vor 0.48,
/// mehr Durchgaengen als Plaetzen (Extra) und Namen mit Umlauten, Emoji und Python-Leerzeichen.
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
        var uhr = Laufende();
        var schreiber = new LernstandSchreiber(lernstand, uhr, Kennung);
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

        Vergleichen(Soll.GetProperty("schritte"), aktionen);

        // Teil "alt": neue Datenbank, Uhr und Kennungen laufen weiter
        lernstand = new Lernstand(Path.Combine(ordner.Pfad, "alt_" + Lernstand.DateiName));
        schreiber = new LernstandSchreiber(lernstand, uhr, Kennung);
        using (Vorgaenge.Sperren())
        {
            // die Ereignisse aus Schritt 2 (Schwierigkeit, Profil) ohne Durchgang (vor 0.48)
            foreach (var zeile in Soll.GetProperty("schritte")[1].GetProperty("tabellen").GetProperty("spiel_ereignisse").EnumerateArray())
            {
                Assert.NotNull(schreiber.SpielEreignisSpeichern(zeile[3].GetString()!, zeile[4].GetString()!, "PC", null));
            }
        }
        object? Handy(long platz, string lauf, string aktion, string daten = "{}")
        {
            using (Vorgaenge.Sperren())
            {
                Assert.True(schreiber.SpielPlatzSpeichern(platz, lauf, aktion, daten, "Handy"));
            }
            G().Neuladen();
            return null;
        }
        var alt = new List<(string Name, Func<object?> Aktion)>
        {
            ("Start mit altem Durchgang", () => Starten().Lauf),
            ("Alter Durchgang: Ticket verschoben", () => G().ZumSchreiben().Verschieben(Offen())),
            ("Handy legt Platz 2 an", () => Handy(2, "lhandy0000000002", Spielstaende.Angelegt)),
            ("Handy legt Platz 3 an", () => Handy(3, "lhandy0000000003", Spielstaende.Angelegt)),
            ("Handy legt Platz 1 noch einmal an (Extra)", () => Handy(1, "lhandy0000000004", Spielstaende.Angelegt)),
            ("Handy legt Platz 5 an (Extra)", () => Handy(5, "lhandy0000000005", Spielstaende.Angelegt)),
            ("Handy benennt Platz 4 um (zaehlt nicht)", () => Handy(4, "", Spielstaende.Umbenannt, "{\"name\": \"Vier\"}")),
            ("Handy benennt Platz 3 um (Emoji, zu lang)", () =>
                Handy(3, "", Spielstaende.Umbenannt, "{\"name\": \"\u3000" + string.Concat(Enumerable.Repeat("\U0001F3AE", 30)) + "\"}")),
            ("Neu gestartet mit Extras", () => Starten().Lauf),
            ("Extra waehlen", () => { G().LaufWaehlen(G().Belegung().Extra[0]); return null; }),
            ("Extra Profil", () => { G().ZumSchreiben().Profil("J\u00f6rg \U0001F3AE", Aussehen(), Ereignisse.Einfach); return null; }),
            ("Platz 2 umbenannt (Umlaute, Emoji, strip)", () =>
            {
                G().PlatzUmbenennen(2, "\u001f\u2003 Pr\u00fcfung \u00e4\u00f6\u00fc \u00c4\u00d6\u00dc \u00df 12\U0001F3AE\U0001F3AE Ende \u3000\u001c");
                return null;
            }),
            ("Platz 3 loeschen (Extra rueckt nach)", () => G().LaufLoeschen(Platz(3))),
            ("Alten Durchgang waehlen", () => { G().LaufWaehlen(Spielstaende.AlterLauf); return null; }),
            ("Alten Durchgang loeschen", () => G().LaufLoeschen(Spielstaende.AlterLauf)),
            ("Neu gestartet ohne alten Durchgang", () => Starten().Lauf),
        };
        Vergleichen(Soll.GetProperty("alt"), alt);
        // Kontrolle: der Teil "alt" hat wirklich den alten Durchgang, ein Extra und einen gekuerzten Emoji-Namen
        var altSoll = Soll.GetProperty("alt");
        Assert.Equal("alt", altSoll[0].GetProperty("lauf").GetString());
        Assert.Contains(altSoll[5].GetProperty("plaetze").EnumerateArray(), p => p.GetProperty("extra").GetBoolean());
        Assert.Equal(24, altSoll[7].GetProperty("plaetze")[2].GetProperty("name").GetString()!.EnumerateRunes().Count());

        void Vergleichen(JsonElement schritte, List<(string Name, Func<object?> Aktion)> aktionen)
        {
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
