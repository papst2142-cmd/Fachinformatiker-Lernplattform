using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 2, Welle 4: Vergleichswerte G21 (werkzeuge/vergleich/erzeuge_abgleich.py, Python v0.63.2).
/// Kommazahlen wie repr, Sicherung fuer die alte App zeichengleich wie create_backup, und je Fall
/// ein Versuch des Abgleichs ohne Netz (Mischbetrieb, Uhr-Nachtrag, Messung, Zukunftsmarken,
/// Zusammenfuehren, export_local, Vergleich mit der Datei) mit gleichem Stand und gleichen Zahlen.
/// </summary>
public class AbgleichVergleichTests
{
    private static readonly JsonElement G21 = VergleichsHilfe.Lade("abgleich.json");
    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> Umbenennungen = Core.Daten.Umbenennungen.Laden(Pfade.Inhalte);

    private static Uhr Uhr => SpeichernTests.FesteUhr(G21.GetProperty("jetzt_utc").GetString()!);

    private static Uhrabweichung Abweichung => new(() => G21.GetProperty("lauf").GetDouble(), G21.GetProperty("start").GetString(),
        G21.GetProperty("prozess").GetString()!);

    public static TheoryData<string> Faelle()
    {
        var daten = new TheoryData<string>();
        foreach (var fall in G21.GetProperty("faelle").EnumerateArray())
        {
            daten.Add(fall.GetProperty("name").GetString()!);
        }
        return daten;
    }

    [Fact]
    public void ZahlenWiePython()
    {
        var anzahl = 0;
        foreach (var z in G21.GetProperty("zahlen").EnumerateArray())
        {
            var wert = BitConverter.Int64BitsToDouble(z[0].GetInt64());
            Assert.Equal(z[1].GetString(), PythonJson.Zahl(wert));
            anzahl++;
        }
        Assert.True(anzahl >= 30);
    }

    [Fact]
    public void SicherungWiePython()
    {
        using var ordner = new TempOrdner();
        var s = G21.GetProperty("sicherung");
        var pfad = UebernahmeTests.BaueDatenbank(s.GetProperty("stand"), Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var lernstand = new Lernstand(pfad);
        var daten = Ruecksicherung.Lesen(lernstand);
        Assert.Equal(s.GetProperty("export").GetString(), PythonJson.Text(daten));
        var einstellungen = JsonNode.Parse(s.GetProperty("einstellungen_eingabe").GetRawText())!.AsObject();
        var huelle = Ruecksicherung.Huelle(daten, Ruecksicherung.Tagesziel(einstellungen), s.GetProperty("app_version").GetString()!,
            DateTime.ParseExact(s.GetProperty("erstellt").GetString()!, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture),
            s.GetProperty("geraet").GetString()!);
        Assert.Equal(s.GetProperty("text").GetString(), PythonJson.Text(huelle));
        Assert.Equal(s.GetProperty("pruefsumme").GetString(), (string?)huelle["pruefsumme"]);
        // Die gepackte Datei liest die eigene Pruefung wie read_backup
        Ruecksicherung.Pruefen(Ruecksicherung.Packen(huelle));
    }

    /// <summary>Abgleich-Texte wie Python: SyncResult.message, _http_error, sync_mixed_text (P2-12).</summary>
    [Fact]
    public void TexteWiePython()
    {
        var t = G21.GetProperty("texte");
        foreach (var z in t.GetProperty("ergebnisse").EnumerateArray())
        {
            var e = new AbgleichErgebnis(z[0].GetInt32(), z[1].GetInt32(), z[2].GetInt32(), z[3].GetBoolean(), z[4].GetBoolean(), []);
            Assert.Equal(z[5].GetString(), AbgleichTexte.Ergebnis(e));
        }
        foreach (var z in t.GetProperty("http").EnumerateArray())
        {
            var code = z[0].GetInt32();
            var fehler = code switch
            {
                401 => AbgleichFehler.TokenUngueltig,
                403 => AbgleichFehler.KeinSchreibrecht,
                404 => AbgleichFehler.NichtGefunden,
                _ => AbgleichFehler.Abgelehnt,
            };
            Assert.Equal(z[1].GetString(), AbgleichTexte.Fehler(new AbgleichAusnahme(fehler, code), "nico/Fi-Lernstand"));
        }
        var anzahl = 0;
        foreach (var z in t.GetProperty("mischbetrieb").EnumerateArray())
        {
            object? wert = z[0].ValueKind == JsonValueKind.String ? z[0].GetString() : null;
            Assert.Equal(z[3].GetString(), AbgleichTexte.Mischbetrieb(z[1].GetBoolean(), z[2].GetString()!, wert, Uhr));
            anzahl++;
        }
        Assert.True(anzahl >= 10);
    }

    /// <summary>Feste Texte nach dem Wortlaut P2-12 und die Zuordnung jedes Fehlers.</summary>
    [Fact]
    public void FesteTexteUndFehler()
    {
        Assert.Equal("Automatisch abgleichen (beim Start, nach dem Lernen und beim Verlassen der App)", AbgleichTexte.Automatisch);
        Assert.Equal("Unerwarteter Fehler beim Abgleich.", AbgleichTexte.Fehler(new AbgleichAusnahme(AbgleichFehler.Unerwartet), "a/b"));
        Assert.Equal(AbgleichTexte.SicherungGescheitert, AbgleichTexte.Fehler(new AbgleichAusnahme(AbgleichFehler.SicherungGescheitert), "a/b"));
        Assert.Equal("Das Repository bitte als „Benutzer/Name“ angeben, z. B. deinname/Fi-Lernstand.",
            AbgleichTexte.Fehler(new AbgleichAusnahme(AbgleichFehler.RepositoryFormat), "a"));
        Assert.Equal("Der Abgleich ist ausgesetzt. Dein Lernfortschritt wird nicht gespeichert: Die Datenbank ist beschädigt.",
            AbgleichTexte.Fehler(new AbgleichAusnahme(AbgleichFehler.Ausgesetzt, grund: "kaputt"), "a/b"));
        Assert.Equal("Der Abgleich ist ausgesetzt.", AbgleichTexte.Fehler(new AbgleichAusnahme(AbgleichFehler.Ausgesetzt), "a/b"));
        foreach (var fehler in Enum.GetValues<AbgleichFehler>())
        {
            var text = AbgleichTexte.Fehler(new AbgleichAusnahme(fehler, 500), "a/b");
            Assert.False(string.IsNullOrWhiteSpace(text));
            Assert.DoesNotContain("%", text);
            Assert.Equal(fehler == AbgleichFehler.Unerwartet, text == AbgleichTexte.Unerwartet);
        }
        Assert.Equal("Noch nicht eingerichtet.", AbgleichTexte.Status(false, "2026-10-20 12:00:00"));
        Assert.Equal("Noch nicht abgeglichen.", AbgleichTexte.Status(true, ""));
        Assert.Equal("Zuletzt abgeglichen: 27.09.2026 21:40", AbgleichTexte.Status(true, "2026-09-27 21:40:05"));
        foreach (var text in new[] { AbgleichTexte.Repository, AbgleichTexte.Token, AbgleichTexte.Hinweis, AbgleichTexte.RepositoryBeispiel })
        {
            Assert.DoesNotContain("fisi-lernstand", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>P2-12 A4: Stempel wie Python _stamp (Uhr geht vor = Abstand plus Abweichung/60, gerundet halb gerade).</summary>
    [Fact]
    public void StempelWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G21.GetProperty("stempel").EnumerateArray())
        {
            using var ordner = new TempOrdner();
            var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
            if (fall[0].ValueKind == JsonValueKind.String)
            {
                Assert.True(lernstand.Schreiben("INSERT OR REPLACE INTO sync_meta (key, value) VALUES ($k, $w)",
                    ("$k", Uhrabweichung.Schluessel), ("$w", fall[0].GetString())));
            }
            var (zeit, abstand) = new LernstandSchreiber(lernstand, Uhr, null, Abweichung).Stempel();
            Assert.Equal(fall[1].GetString(), zeit);
            Assert.Equal(fall[2].GetInt32(), abstand);
            anzahl++;
        }
        Assert.True(anzahl >= 15);
    }

    /// <summary>P2-12 A4: fehlt die Tabelle sync_meta, rechnet Stempel ohne Abweichung und das Schreiben gelingt wie bisher.</summary>
    [Fact]
    public void StempelOhneSyncMeta()
    {
        using var ordner = new TempOrdner();
        var lernstand = new Lernstand(Path.Combine(ordner.Pfad, Lernstand.DateiName));
        Assert.True(lernstand.Schreiben("DROP TABLE sync_meta"));
        var schreiber = new LernstandSchreiber(lernstand, Uhr, null, Abweichung);
        Assert.Equal(("2026-10-20 12:00:00", 120), schreiber.Stempel());
        Assert.True(schreiber.KarteSpeichern("Netzwerk", "Frage", "karte", true));
        Assert.Null(lernstand.NurLesenGrund);
        Assert.Single(lernstand.Abfragen("SELECT 1 FROM card_events"));
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void VersuchWiePython(string name)
    {
        using var ordner = new TempOrdner();
        var fall = G21.GetProperty("faelle").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);
        var pfad = UebernahmeTests.BaueDatenbank(fall.GetProperty("vorher"), Path.Combine(ordner.Pfad, Lernstand.DateiName));
        var datei = fall.GetProperty("datei").ValueKind == JsonValueKind.Null ? null : GitHubAttrappe.Packen(fall.GetProperty("datei").GetString()!);
        double? dev = fall.GetProperty("abweichung").ValueKind == JsonValueKind.Null ? null : fall.GetProperty("abweichung").GetDouble();

        var r = Abgleich.Versuch(new Lernstand(pfad), datei, dev, Uhr, Abweichung, Umbenennungen, Path.Combine(ordner.Pfad, "sicherungen"));

        var erwartet = VergleichsHilfe.Stand(fall.GetProperty("nachher"));
        var ist = VergleichsHilfe.Stand(pfad, fall.GetProperty("nachher"));
        Assert.True(erwartet.SequenceEqual(ist), VergleichsHilfe.Unterschied(erwartet, ist));
        Assert.Equal(fall.GetProperty("export").GetString(), PythonJson.Text(r.Export));
        Assert.Equal(fall.GetProperty("received").GetInt32(), r.Erhalten);
        Assert.Equal(fall.GetProperty("gone").GetInt32(), r.Weg);
        Assert.Equal(fall.GetProperty("invalid").GetInt32(), r.Ungueltig);
        Assert.Equal(fall.GetProperty("clamped").GetBoolean(), r.Gekappt);
        Assert.Equal(fall.GetProperty("mixed").GetBoolean(), r.Misch);
        Assert.Equal(fall.GetProperty("gleich").GetBoolean(), r.Gleich);
        Assert.Equal(fall.GetProperty("sent").GetInt32(), r.Gesendet);
    }
}
