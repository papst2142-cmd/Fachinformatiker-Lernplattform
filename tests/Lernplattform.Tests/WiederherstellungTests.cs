using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 5 BE2/BE3 „Alles ersetzen“ wie Pythons replace_all (G37, tests/Vergleich/sicherung_ersetzen.json):
/// Stand danach, Tagesziel, Sicherheitskopie, Aufhebungen (replace_lifts), Texte. Pflicht-
/// Kontrollfehler (Weisung P5-3 Punkt 5): Absturz mitten im Ersetzen, Sicherheitskopie nicht
/// lesbar, Sperre durch einen laufenden Abgleich; dazu nur lesen, Kopie nicht speicherbar,
/// Hilfsdatei mit Zugangsschluessel. Nur synthetische Daten (A3).
/// </summary>
public class WiederherstellungTests
{
    private static readonly JsonElement G37 = VergleichsHilfe.Lade("sicherung_ersetzen.json");
    private static readonly JsonElement G20 = VergleichsHilfe.Lade("uebernahme.json");
    private static readonly IReadOnlyList<(string Tabelle, string Alt, string Neu)> Umbenennungen = Core.Daten.Umbenennungen.Laden(Pfade.Inhalte);

    private static Uhr Uhr => SpeichernTests.FesteUhr(G37.GetProperty("jetzt_utc").GetString()!);

    private static string App => G37.GetProperty("app_version").GetString()!;

    private static string Geraet => G37.GetProperty("geraet").GetString()!;

    public static TheoryData<string> Faelle()
    {
        var daten = new TheoryData<string>();
        foreach (var fall in G37.GetProperty("faelle").EnumerateArray())
        {
            daten.Add(fall.GetProperty("name").GetString()!);
        }
        return daten;
    }

    private static JsonElement Fall(string name) =>
        G37.GetProperty("faelle").EnumerateArray().Single(f => f.GetProperty("name").GetString() == name);

    private static byte[] Datei(string name) => UebernahmeTests.Gzip(G37.GetProperty("dateien").GetProperty(name).GetString()!);

    private static Sicherungsinhalt Sicherung(string name) => Sicherungsinhalt.Lesen(Datei(name));

    /// <summary>Ordner mit lernplattform.db (Ziel aus G37), einstellungen.json wie vorher, Ordner sicherungen.</summary>
    private sealed class Aufbau : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Aufbau(string ziel)
        {
            Daten = _ordner.Pfad;
            Pfad = UebernahmeTests.BaueDatenbank(G37.GetProperty("ziele").GetProperty(ziel), System.IO.Path.Combine(Daten, Lernstand.DateiName));
            File.WriteAllText(System.IO.Path.Combine(Daten, Core.Daten.Einstellungen.DateiName), G37.GetProperty("einstellungen_vorher").GetRawText());
            Einstellungen = new Einstellungen(Daten);
        }

        public string Daten { get; }

        public string Pfad { get; }

        public Einstellungen Einstellungen { get; }

        public string Kopien => System.IO.Path.Combine(Daten, Uebernahme.SicherungsOrdner);

        public string Ersetzen(string datei) =>
            Wiederherstellung.Ersetzen(new Lernstand(Pfad), Sicherung(datei), Einstellungen, App, Geraet, null, Uhr);

        public void Dispose() => _ordner.Dispose();
    }

    private static JsonNode? Entpackt(string pfad)
    {
        using var datei = File.OpenRead(pfad);
        using var gz = new GZipStream(datei, CompressionMode.Decompress);
        return JsonNode.Parse(gz);
    }

    [Theory]
    [MemberData(nameof(Faelle))]
    public void ErsetzenWiePython(string name)
    {
        var fall = Fall(name);
        using var a = new Aufbau(fall.GetProperty("ziel").GetString()!);
        if (fall.GetProperty("start").ValueKind != JsonValueKind.Null)
        {
            a.Ersetzen(Fall(fall.GetProperty("start").GetString()!).GetProperty("datei").GetString()!);
        }
        var kopie = a.Ersetzen(fall.GetProperty("datei").GetString()!);

        var erwartet = VergleichsHilfe.Stand(fall.GetProperty("nachher"));
        var ist = VergleichsHilfe.Stand(a.Pfad, fall.GetProperty("nachher"));
        Assert.True(erwartet.SequenceEqual(ist), VergleichsHilfe.Unterschied(erwartet, ist));
        // Tagesziel aus der Sicherung, alles andere (Zugangsschluessel, Farbe) bleibt
        var einstellungen = JsonNode.Parse(File.ReadAllText(a.Einstellungen.Pfad));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fall.GetProperty("einstellungen").GetRawText()), einstellungen),
            einstellungen!.ToJsonString());
        Assert.False(File.Exists(a.Einstellungen.Hilfsdatei));
        // Sicherheitskopie: Name und Inhalt wie create_backup des Stands davor
        Assert.Equal(a.Kopien, System.IO.Path.GetDirectoryName(kopie));
        Assert.Equal(fall.GetProperty("kopie_name").GetString(), System.IO.Path.GetFileName(kopie));
        var kopieIst = Entpackt(kopie);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(fall.GetProperty("kopie").GetRawText()), kopieIst), kopieIst!.ToJsonString());
        Assert.Equal(fall.GetProperty("meldung").GetString(), SicherungTexte.ErsetztMeldung(Sicherung(fall.GetProperty("datei").GetString()!), System.IO.Path.GetFileName(kopie)));
        // Kontrollfehler "Nummern neu ab 1": nach zweimal Ersetzen zaehlen die Nummern weiter
        if (name.EndsWith("_zweimal", StringComparison.Ordinal))
        {
            using var db = VergleichsHilfe.Oeffnen(a.Pfad);
            var kleinste = (long)VergleichsHilfe.Abfrage(db, "SELECT MIN(id) FROM card_events")[0][0]!;
            Assert.True(kleinste > G37.GetProperty("ziele").GetProperty("gefuellt").GetProperty("tabellen").GetProperty("card_events").GetProperty("zeilen").GetArrayLength());
        }
    }

    /// <summary>Kontrollfehler „Marke nicht übernommen“ und „Aufhebung vergessen“: replace_lifts wie Python.</summary>
    [Fact]
    public void AufhebungenWiePython()
    {
        var anzahl = 0;
        foreach (var fall in G37.GetProperty("aufhebungen").EnumerateArray())
        {
            static Dictionary<string, object?> Werte(JsonElement e) =>
                e.EnumerateObject().ToDictionary(p => p.Name, p => VergleichsHilfe.Wert(p.Value));
            var ist = Loeschmarken.ErsetzenAufhebungen(Werte(fall[0]), Werte(fall[1]), Werte(fall[2]));
            var erwartet = fall[3].EnumerateObject().ToDictionary(p => p.Name, p => p.Value.EnumerateArray().Select(x => x.GetString()!).ToArray());
            Assert.Equal(erwartet.Keys.Order(), ist.Keys.Order());
            foreach (var (k, paar) in erwartet)
            {
                Assert.Equal(paar, ist[k]);
            }
            anzahl++;
        }
        Assert.True(anzahl >= 19);
    }

    [Fact]
    public void TexteWiePython()
    {
        var t = G37.GetProperty("texte");
        string T(string k) => t.GetProperty(k).GetString()!;
        Assert.Equal(SicherungTexte.Hilfe, T("hilfe"));
        Assert.Equal(RuecksicherungTexte.Knopf, T("knopf_erstellen"));
        Assert.Equal(SicherungTexte.KnopfEinspielen, T("knopf_einspielen"));
        Assert.Equal(SicherungTexte.Zusammenfuehren, T("knopf_zusammenfuehren"));
        Assert.Equal(SicherungTexte.Ersetzen, T("knopf_ersetzen"));
        Assert.Equal(SicherungTexte.Abbrechen, T("knopf_abbrechen"));
        Assert.Equal(RuecksicherungTexte.DateiTyp, T("dateityp"));
        Assert.Equal(Ruecksicherung.Endung, T("endung"));
        Assert.Equal(Wiederherstellung.Praefix, T("praefix"));
        Assert.Equal(Core.Daten.Einstellungen.HilfsdateiEndung, T("hilfsdatei_endung"));
        Assert.Equal(SicherungTexte.VorschauHinweis, T("vorschau_hinweis"));
        Assert.Equal(SicherungTexte.ErsetzenTitel, T("ersetzen_titel"));
        Assert.Equal(SicherungTexte.RueckfrageTitel, T("ersetzen_rueckfrage_titel"));
        Assert.Equal(SicherungTexte.Rueckfrage, T("ersetzen_rueckfrage"));
        Assert.Equal(SicherungTexte.Titel, T("fehler_titel"));
        Assert.Equal(RuecksicherungTexte.SpeichernTitel, T("speichern_fehler_titel"));
        Assert.Equal(SicherungTexte.KeineSicherung, T("keine_sicherung"));
        Assert.Equal(UebernahmeTexte.PruefsummeFalsch, T("beschaedigt"));
        Assert.Equal(UebernahmeTexte.InhaltKaputt, T("kaputt"));
        Assert.Equal(UebernahmeTexte.ZuNeu, T("zu_neu"));
        Assert.Equal(SicherungTexte.NurLesen, T("nur_lesen"));
        Assert.Equal(RuecksicherungTexte.Warten, T("warten_erstellen"));
        Assert.Equal(SicherungTexte.WartenErsetzen, T("warten_ersetzen"));
        Assert.Equal(T("vorschau_a"), SicherungTexte.Vorschau(Sicherung("a")));
        Assert.Equal(T("bleibt_geloescht_1"), SicherungTexte.BleibtGeloescht(1));
        Assert.Equal(T("bleibt_geloescht_3"), SicherungTexte.BleibtGeloescht(3));
        // MSG_RESTORE_FAILED ohne „Fehler: %s“ (W6)
        Assert.StartsWith(T("einspielen_gescheitert").Replace(" Fehler: %s", "", StringComparison.Ordinal), SicherungTexte.EinspielenGescheitert);
        // replace_question: genau wie Python bis auf den einen Satz zum Ort der Kopie (W2, D3)
        Assert.Equal(T("ersetzen_frage_a"), SicherungTexte.ErsetzenFragePython(Sicherung("a")));
        Assert.Contains(SicherungTexte.FrageKopiePython, T("ersetzen_frage_a"));
        Assert.Equal(T("ersetzen_frage_a").Replace(SicherungTexte.FrageKopiePython, SicherungTexte.FrageKopie, StringComparison.Ordinal),
            SicherungTexte.ErsetzenFrage(Sicherung("a")));
    }

    /// <summary>
    /// merge_message: Kern, „bleibt gelöscht“ und „entfernt“ zeichengleich wie Python; danach der Satz
    /// zur Kopie (W3) und die Hinweise zu Löschmarken der Datei wie bei „Lernstand übernehmen“ (W4).
    /// </summary>
    [Fact]
    public void ZusammenfuehrenMeldungWiePython()
    {
        var hinweise = new[] { "Die Abgleich-Datei enthält eine ungültige Löschmarke. Sie wurde nicht angewendet.",
            UebernahmeTexte.ZukunftSicherung };
        foreach (var fall in G37.GetProperty("zusammenfuehren").EnumerateArray())
        {
            using var ordner = new TempOrdner();
            var pfad = UebernahmeTests.BaueDatenbank(G37.GetProperty("ziele").GetProperty(fall.GetProperty("ziel").GetString()!),
                System.IO.Path.Combine(ordner.Pfad, Lernstand.DateiName));
            var daten = Sicherung(fall.GetProperty("datei").GetString()!).Daten;
            foreach (var python in fall.GetProperty("meldungen").EnumerateArray().Select(m => m.GetString()!))
            {
                var bericht = Uebernahme.Uebernehmen(new Lernstand(pfad), daten, Umbenennungen, Uhr);
                var kern = hinweise.Aggregate(python, (text, h) => text.Replace(" " + h, "", StringComparison.Ordinal));
                var ohneKopie = SicherungTexte.Zusammengefuehrt(bericht, null);
                var ohneHinweise = UebernahmeTexte.Hinweise(UebernahmeQuelle.Sicherung, bericht.UngueltigInDatei, bericht.ZukunftsMarken)
                    .Aggregate(ohneKopie, (text, h) => text.Replace(" " + h, "", StringComparison.Ordinal));
                Assert.Equal(kern, ohneHinweise);
                var mitKopie = SicherungTexte.Zusammengefuehrt(bericht, "k.db");
                Assert.Contains(" " + Beschriftungen.Einsetzen(UebernahmeTexte.KopieLiegt, "k.db"), mitKopie);
            }
        }
    }

    /// <summary>Pflicht: Absturz zwischen Kopie und Buchung und mitten in der Buchung: alles oder nichts.</summary>
    [Theory]
    [InlineData("vor-buchung")]
    [InlineData("card_events")]
    [InlineData("spiel_ereignisse")]
    [InlineData("vor-commit")]
    public void AbsturzMittenImErsetzen(string punkt)
    {
        using var a = new Aufbau("gefuellt");
        var einstellungenVorher = File.ReadAllText(a.Einstellungen.Pfad);
        var datei = System.IO.Path.Combine(a.Daten, "a.fisisicherung");
        File.WriteAllBytes(datei, Datei("a"));
        AbsturzProbe.Starten(AbsturzProbe.ArtErsetzen, [a.Pfad, datei, a.Daten, punkt]);
        using (var db = VergleichsHilfe.Oeffnen(a.Pfad))
        {
            Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
        }
        var ziel = G37.GetProperty("ziele").GetProperty("gefuellt");
        Assert.Equal(VergleichsHilfe.Stand(ziel), VergleichsHilfe.Stand(a.Pfad, ziel));
        Assert.Equal(einstellungenVorher, File.ReadAllText(a.Einstellungen.Pfad));
        // Die Sicherheitskopie liegt vollstaendig vor und ist lesbar
        var kopien = Directory.GetFiles(a.Kopien);
        Assert.Single(kopien);
        Sicherungsinhalt.Lesen(File.ReadAllBytes(kopien[0]));
        // Kontrollfehler "Tagesziel nach commit": vor dem commit liegt die Hilfsdatei schon da (0600), danach nicht
        Assert.Equal(punkt == "vor-commit", File.Exists(a.Einstellungen.Hilfsdatei));
        if (punkt == "vor-commit" && !OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(a.Einstellungen.Hilfsdatei));
            Assert.Contains("geheim-123456", File.ReadAllText(a.Einstellungen.Hilfsdatei));
        }
        // Kontrollfehler "Hilfsdatei mit Token bleibt liegen": der Start raeumt sie weg (remove_stale_temp)
        a.Einstellungen.HilfsdateiEntfernen();
        Assert.False(File.Exists(a.Einstellungen.Hilfsdatei));
        // Danach gelingt das Ersetzen normal und ergibt den Stand von Python
        a.Ersetzen("a");
        var nachher = Fall("a_gefuellt").GetProperty("nachher");
        Assert.Equal(VergleichsHilfe.Stand(nachher), VergleichsHilfe.Stand(a.Pfad, nachher));
    }

    /// <summary>Pflicht: Sicherheitskopie nach dem Speichern nicht lesbar: nichts ersetzt, keine kaputte Kopie bleibt.</summary>
    [Fact]
    public void KopieNichtLesbarErsetztNichts()
    {
        using var a = new Aufbau("gefuellt");
        var vorher = UebernahmeTests.Sha(a.Pfad);
        var einstellungenVorher = File.ReadAllText(a.Einstellungen.Pfad);
        Wiederherstellung.Haltepunkt = punkt =>
        {
            if (punkt == "kopie")
            {
                var datei = Directory.GetFiles(a.Kopien).Single();
                File.WriteAllBytes(datei, File.ReadAllBytes(datei)[..100]);   // abgeschnitten
            }
        };
        try
        {
            var fehler = Assert.Throws<WiederherstellungAusnahme>(() => a.Ersetzen("a"));
            Assert.Equal(WiederherstellungFehler.KopieGescheitert, fehler.Fehler);
            Assert.StartsWith("Die Sicherheitskopie ließ sich nicht speichern: ", SicherungTexte.ErsetzenFehler(fehler, null));
        }
        finally
        {
            Wiederherstellung.Haltepunkt = null;
        }
        Assert.Equal(vorher, UebernahmeTests.Sha(a.Pfad));
        Assert.Equal(einstellungenVorher, File.ReadAllText(a.Einstellungen.Pfad));
        Assert.Empty(Directory.GetFiles(a.Kopien));
    }

    /// <summary>Kopie laesst sich nicht anlegen (Ordner "sicherungen" ist eine Datei): nichts ersetzt.</summary>
    [Fact]
    public void KopieNichtSpeicherbarErsetztNichts()
    {
        using var a = new Aufbau("gefuellt");
        File.WriteAllText(a.Kopien, "eine Datei statt eines Ordners");
        var vorher = UebernahmeTests.Sha(a.Pfad);
        var fehler = Assert.Throws<WiederherstellungAusnahme>(() => a.Ersetzen("a"));
        Assert.Equal(WiederherstellungFehler.KopieGescheitert, fehler.Fehler);
        Assert.Equal(vorher, UebernahmeTests.Sha(a.Pfad));
        Assert.False(File.Exists(a.Einstellungen.Hilfsdatei));
    }

    /// <summary>Pflicht: Sperre durch einen laufenden Abgleich: Ersetzen wartet und schreibt nichts, bis er fertig ist.</summary>
    [Fact]
    public async Task SperreDurchLaufendenAbgleich()
    {
        using var a = new Aufbau("gefuellt");
        var vorher = UebernahmeTests.Sha(a.Pfad);
        Task<string> ersetzen;
        using (Vorgaenge.Sperren())          // wie Abgleich.Abgleichen
        {
            Assert.True(Vorgaenge.Belegt);   // die Oberflaeche lehnt dann mit AbgleichTexte.Belegt ab
            ersetzen = Task.Run(() => a.Ersetzen("a"));
            await Task.Delay(500, TestContext.Current.CancellationToken);
            Assert.False(ersetzen.IsCompleted);
            Assert.Equal(vorher, UebernahmeTests.Sha(a.Pfad));
            Assert.False(Directory.Exists(a.Kopien));
        }
        await ersetzen;
        var nachher = Fall("a_gefuellt").GetProperty("nachher");
        Assert.Equal(VergleichsHilfe.Stand(nachher), VergleichsHilfe.Stand(a.Pfad, nachher));
    }

    /// <summary>Gesperrt erst nach der Kopie (z. B. ein anderes Programm): Buchung scheitert, alles zurueckgenommen.</summary>
    [Fact]
    public void SperreNachDerKopieNimmtAllesZurueck()
    {
        using var a = new Aufbau("gefuellt");
        var ziel = G37.GetProperty("ziele").GetProperty("gefuellt");
        var einstellungenVorher = File.ReadAllText(a.Einstellungen.Pfad);
        Microsoft.Data.Sqlite.SqliteConnection? fremd = null;
        Wiederherstellung.Haltepunkt = punkt =>
        {
            if (punkt == "vor-buchung")
            {
                fremd = VergleichsHilfe.Oeffnen(a.Pfad);
                VergleichsHilfe.Ausfuehren(fremd, "BEGIN IMMEDIATE");
            }
        };
        try
        {
            var fehler = Assert.Throws<WiederherstellungAusnahme>(() => a.Ersetzen("a"));
            Assert.Equal(WiederherstellungFehler.EinspielenGescheitert, fehler.Fehler);
            Assert.Equal(SicherungTexte.EinspielenGescheitert, SicherungTexte.ErsetzenFehler(fehler, null));
        }
        finally
        {
            Wiederherstellung.Haltepunkt = null;
            fremd?.Dispose();
        }
        Assert.Equal(VergleichsHilfe.Stand(ziel), VergleichsHilfe.Stand(a.Pfad, ziel));
        Assert.Equal(einstellungenVorher, File.ReadAllText(a.Einstellungen.Pfad));
        Assert.False(File.Exists(a.Einstellungen.Hilfsdatei));
        Assert.Single(Directory.GetFiles(a.Kopien));   // die Kopie bleibt (sie wird nie von allein geloescht, D3)
    }

    /// <summary>Wird nichts gespeichert (Lesefehler, Schreibschutz): nichts ersetzt, keine Kopie.</summary>
    [Fact]
    public void NurLesenErsetztNichts()
    {
        using var ordner = new TempOrdner();
        var pfad = System.IO.Path.Combine(ordner.Pfad, Lernstand.DateiName);
        File.WriteAllBytes(pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
        var vorher = UebernahmeTests.Sha(pfad);
        var lernstand = new Lernstand(pfad);
        var fehler = Assert.Throws<WiederherstellungAusnahme>(() =>
            Wiederherstellung.Ersetzen(lernstand, Sicherung("a"), new Einstellungen(ordner.Pfad), App, Geraet, null, Uhr));
        Assert.Equal(WiederherstellungFehler.NurLesen, fehler.Fehler);
        Assert.StartsWith("Die Sicherung lässt sich nicht einspielen.", SicherungTexte.ErsetzenFehler(fehler, lernstand.NurLesenGrund));
        Assert.Equal(vorher, UebernahmeTests.Sha(pfad));
        Assert.False(Directory.Exists(System.IO.Path.Combine(ordner.Pfad, Uebernahme.SicherungsOrdner)));
    }

    /// <summary>Kontrollfehler „Tagesziel nach commit“: ist einstellungen.json nicht lesbar, bleibt sie unberuehrt (wie Speichern).</summary>
    [Fact]
    public void HilfsdateiNurWennEinstellungenLesbar()
    {
        using var ordner = new TempOrdner();
        var einstellungen = new Einstellungen(ordner.Pfad);
        Assert.True(einstellungen.HilfsdateiSchreiben(new JsonObject { ["ziel_anzahl"] = 30, ["sync_token"] = "geheim-123456" }));
        Assert.True(File.Exists(einstellungen.Hilfsdatei));
        Assert.False(File.Exists(einstellungen.Pfad));     // erst das Einsetzen bringt sie an ihren Platz
        Assert.True(einstellungen.HilfsdateiEinsetzen());
        Assert.False(File.Exists(einstellungen.Hilfsdatei));
        Assert.Equal(30, (int)einstellungen.Laden()["ziel_anzahl"]!);
        // Eine alte Hilfsdatei wird vor dem Schreiben entfernt (nie weiterverwendet)
        File.WriteAllText(einstellungen.Hilfsdatei, "alt");
        Assert.True(einstellungen.HilfsdateiSchreiben(new JsonObject { ["ziel_anzahl"] = 40 }));
        Assert.DoesNotContain("alt", File.ReadAllText(einstellungen.Hilfsdatei));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(einstellungen.Hilfsdatei));
        }
        einstellungen.HilfsdateiEntfernen();
        Assert.False(File.Exists(einstellungen.Hilfsdatei));
    }

    /// <summary>Kopien in derselben Minute ueberschreiben sich nie (_2, _3).</summary>
    [Fact]
    public void KopiePfadZaehltWeiter()
    {
        using var ordner = new TempOrdner();
        var jetzt = Uhr.Jetzt();
        var erste = Wiederherstellung.KopiePfad(ordner.Pfad, jetzt);
        Assert.Equal("vor_wiederherstellung_2026-10-20_1200.fisisicherung", System.IO.Path.GetFileName(erste));
        File.WriteAllText(erste, "");
        var zweite = Wiederherstellung.KopiePfad(ordner.Pfad, jetzt);
        Assert.Equal("vor_wiederherstellung_2026-10-20_1200_2.fisisicherung", System.IO.Path.GetFileName(zweite));
        File.WriteAllText(zweite, "");
        Assert.Equal("vor_wiederherstellung_2026-10-20_1200_3.fisisicherung", System.IO.Path.GetFileName(Wiederherstellung.KopiePfad(ordner.Pfad, jetzt)));
    }

    /// <summary>Die Sicherungen von G20 (a, b, c) liest Sicherungsinhalt wie Uebernahme.LeseSicherung; kaputte Dateien gleich abgelehnt.</summary>
    [Fact]
    public void LesenWieUebernahme()
    {
        foreach (var name in new[] { "a", "b", "c", "d", "e" })
        {
            var inhalt = Sicherung(name);
            Assert.Equal(Uebernahme.LeseSicherung(Datei(name)).Zeilen, inhalt.Daten.Zeilen);
            Assert.Equal("20.10.2026, 11:30 Uhr", inhalt.ErstelltText);
        }
        foreach (var fall in G20.GetProperty("fehler").EnumerateArray().Where(f => f.GetProperty("erwartet").GetString() != "ok"))
        {
            var roh = fall.GetProperty("roh").ValueKind == JsonValueKind.String
                ? Convert.FromBase64String(fall.GetProperty("roh").GetString()!)
                : UebernahmeTests.Gzip(fall.GetProperty("text").GetString()!);
            if (fall.GetProperty("kuerzen").GetBoolean())
            {
                roh = roh[..(roh.Length / 2)];
            }
            var fehler = Assert.Throws<UebernahmeAusnahme>(() => Sicherungsinhalt.Lesen(roh));
            Assert.Equal(fall.GetProperty("erwartet").GetString(), fehler.Fehler.ToString());
        }
    }
}
