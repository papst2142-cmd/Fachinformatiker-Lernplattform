using System.IO.Compression;
using System.Text;
using System.Text.Json.Nodes;
using Lernplattform.Core.Dashboard;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Hilfe;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W4 (Plan W4 Abschnitt 6, Wortliste W4 G, Weisung P4-11): Bestandsschutz des Rundgangs,
/// Einrichten in einem Schreibvorgang, Tastatur, Ausbildungsfrage, Warnhinweis, zwei Geraete,
/// Name nur auf diesem Geraet. Nur synthetische Daten (A3).
/// </summary>
public class RundgangTests
{
    private const string Repo = "pruefer/lernstand-test";
    private const string Token = "ghp_TestSchluessel0123456789abcdefghijklmn";
    private static readonly DateTimeOffset Zeit = new(2026, 10, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Heute = new(2026, 10, 22);
    private static readonly Lazy<RahmenplanGewicht> Plan = new(() => new RahmenplanGewicht(Lerninhalte.Laden(Pfade.Inhalte)));

    /// <summary>Ein Geraet mit eigenem Ordner (Lernstand, einstellungen.json), fester Uhr und Abgleich-Zugang.</summary>
    private sealed class Geraet : IDisposable
    {
        private readonly TempOrdner _ordner = new();

        public Geraet(string name)
        {
            Name = name;
            Uhr = SpeichernTests.FesteUhr(() => Jetzt);
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            Einstellungen = new Einstellungen(_ordner.Pfad);
            Schreiber = new LernstandSchreiber(Lernstand, Uhr);
        }

        public string Name { get; }

        public DateTimeOffset Jetzt { get; set; } = Zeit;

        public Uhr Uhr { get; }

        public Lernstand Lernstand { get; }

        public Einstellungen Einstellungen { get; }

        public LernstandSchreiber Schreiber { get; }

        public string Ordner => _ordner.Pfad;

        public JsonObject Werte => Einstellungen.Laden();

        /// <summary>Wie Zustand.Aendere: laden, aendern, einmal speichern.</summary>
        public bool Aendere(Action<JsonObject> aenderung)
        {
            var werte = Einstellungen.Laden();
            if (Einstellungen.NurAnzeige)
            {
                return false;
            }
            aenderung(werte);
            return Einstellungen.Speichern(werte);
        }

        public void Abgleichen(GitHubAttrappe attrappe)
        {
            Aendere(w =>
            {
                w[Abgleich.RepoSchluessel] = Repo;
                w[Abgleich.TokenSchluessel] = Token;
            });
            using var netz = new GitHubZugang(attrappe.Basis, TimeSpan.FromSeconds(10));
            Abgleich.Ausfuehren(Lernstand, Einstellungen, Name, netz, [], Uhr);
        }

        public StartSchritte Start(bool nurAnzeige = false, bool autoModus = false) =>
            AusbildungFrage.Start(nurAnzeige, autoModus, Lernstand.NurLesenGrund is null, Werte, Einrichten.HatDaten(Lernstand.Pfad),
                Ausbildung.Gewaehlt(Schreiber), Ausbildung.Gespeichert(Schreiber));

        public Dictionary<string, long> Zeilenzahlen()
        {
            using var db = VergleichsHilfe.Oeffnen(Lernstand.Pfad);
            return VergleichsHilfe.Abfrage(db, "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'")
                .Select(z => (string)z[0]!)
                .ToDictionary(t => t, t => (long)VergleichsHilfe.Abfrage(db, "SELECT COUNT(*) FROM \"" + t + "\"")[0][0]!);
        }

        public void Dispose() => _ordner.Dispose();
    }

    private static GitHubAttrappe Attrappe()
    {
        var a = new GitHubAttrappe();
        a.Datum = () => Zeit;
        return a;
    }

    // -- Bestandsschutz -----------------------------------------------------------------------

    /// <summary>Eine Zeile in irgendeiner Tabelle (jede Tabelle einzeln) = kein Rundgang; die Pruefung aendert nichts.</summary>
    [Fact]
    public void BestandsschutzJedeTabelleEinzeln()
    {
        using var leer = new Geraet("PC");
        var tabellen = leer.Zeilenzahlen().Keys.ToList();
        Assert.True(tabellen.Count >= 12, string.Join(",", tabellen));
        Assert.False(Einrichten.HatDaten(leer.Lernstand.Pfad));
        Assert.True(leer.Start().Rundgang);
        foreach (var tabelle in tabellen)
        {
            using var g = new Geraet("PC");
            using (var db = VergleichsHilfe.Oeffnen(g.Lernstand.Pfad))
            {
                var spalten = VergleichsHilfe.Abfrage(db, "SELECT name, \"notnull\", dflt_value FROM pragma_table_info('" + tabelle + "')")
                    .Where(s => (long)s[1]! == 1 && s[2] is null).Select(s => (string)s[0]!).ToList();
                using var befehl = db.CreateCommand();
                befehl.CommandText = spalten.Count == 0
                    ? "INSERT INTO \"" + tabelle + "\" DEFAULT VALUES"
                    : "INSERT INTO \"" + tabelle + "\" (" + string.Join(",", spalten.Select(s => "\"" + s + "\"")) + ") VALUES ("
                        + string.Join(",", spalten.Select(_ => "'1'")) + ")";
                befehl.ExecuteNonQuery();
            }
            var vorher = g.Zeilenzahlen();
            Assert.True(Einrichten.HatDaten(g.Lernstand.Pfad), tabelle);
            Assert.False(g.Start().Rundgang, tabelle);
            Assert.Equal(vorher, g.Zeilenzahlen());
            Assert.False(File.Exists(g.Einstellungen.Pfad), tabelle);
        }
    }

    [Fact]
    public void LesefehlerGiltAlsDaten()
    {
        using var ordner = new TempOrdner();
        var kaputt = HilfeTests.Datenbank(ordner.Pfad, "kaputt");
        var vorher = File.ReadAllBytes(kaputt);
        Assert.True(Einrichten.HatDaten(kaputt));
        Assert.False(Einrichten.RundgangFaellig([], Einrichten.HatDaten(kaputt)));
        Assert.Equal(vorher, File.ReadAllBytes(kaputt));
    }

    /// <summary>Plan 2.3 K1: RundgangFaellig vor der Frage bestimmt; nach der Frage kommt der Rundgang, obwohl jetzt eine Zeile da ist.</summary>
    [Fact]
    public void RundgangNachAusbildungsfrage()
    {
        using var g = new Geraet("PC");
        var schritte = g.Start();
        Assert.True(schritte.Frage);
        Assert.True(schritte.Rundgang);
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "ae"));
        Assert.True(g.Aendere(w => AusbildungFrage.Beantwortet(w, "ae", Heute)));
        Assert.True(Einrichten.HatDaten(g.Lernstand.Pfad));       // jetzt bestimmt waere der Rundgang weg
        Assert.False(g.Start().Rundgang);
        Assert.True(schritte.Rundgang);                           // der Ablauf nutzt den vorher bestimmten Wert
    }

    /// <summary>Der ganze Startablauf: Frage legt eine Zeile an, der Rundgang kommt trotzdem (vorher bestimmt), danach.</summary>
    [Fact]
    public async Task StartablaufRundgangNachDerFrage()
    {
        using var g = new Geraet("PC");
        var folge = new List<string>();
        var schritte = await Ablauf(g, folge, nurAnzeige: false, autoModus: false);
        Assert.Equal(["lesen", "frage", "rundgang"], folge);
        Assert.Equal(new StartSchritte(true, true, false), schritte);
        // zweiter Start: nichts mehr von selbst (Zeile da, Merker gesetzt)
        folge.Clear();
        Assert.Equal(StartSchritte.Keine, await Ablauf(g, folge, nurAnzeige: false, autoModus: false));
        Assert.Equal(["lesen"], folge);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task StartablaufNurAnzeigeOderOhneBedienung(bool nurAnzeige, bool autoModus)
    {
        using var g = new Geraet("PC");
        var folge = new List<string>();
        Assert.Equal(StartSchritte.Keine, await Ablauf(g, folge, nurAnzeige, autoModus));
        Assert.Empty(folge);
        Assert.False(File.Exists(g.Einstellungen.Pfad) && g.Werte.ContainsKey(AusbildungFrage.GefragtSchluessel));
    }

    private static Task<StartSchritte> Ablauf(Geraet g, List<string> folge, bool nurAnzeige, bool autoModus) =>
        AusbildungFrage.Ablauf(nurAnzeige, autoModus, () => g.Werte,
            () =>
            {
                folge.Add("lesen");
                return Task.FromResult(new StartLage(Einrichten.HatDaten(g.Lernstand.Pfad), g.Lernstand.NurLesenGrund is null,
                    Ausbildung.Gewaehlt(g.Schreiber), Ausbildung.Gespeichert(g.Schreiber)));
            },
            () =>
            {
                folge.Add("frage");
                Assert.True(Ausbildung.Waehlen(g.Schreiber, "ae"));
                Assert.True(g.Aendere(w => AusbildungFrage.Beantwortet(w, "ae", Heute)));
                return Task.CompletedTask;
            },
            () =>
            {
                folge.Add("anderswo");
                return Task.CompletedTask;
            },
            () =>
            {
                folge.Add("rundgang");
                Assert.True(g.Aendere(w => Einrichten.RundgangMerken(w, Heute)));
                return Task.CompletedTask;
            });

    [Fact]
    public void NurAnzeigeUndStartOhneBedienungZeigenNichts()
    {
        using var g = new Geraet("PC");
        Assert.Equal(StartSchritte.Keine, g.Start(nurAnzeige: true));
        Assert.Equal(StartSchritte.Keine, g.Start(autoModus: true));
        Assert.NotEqual(StartSchritte.Keine, g.Start());
        Assert.False(AusbildungFrage.Faellig(false, [], lesbar: false));
    }

    /// <summary>Gesperrte Datei: nichts wird geschrieben, N9 erscheint, der Rundgang bleibt offen (nur ohne Root pruefbar).</summary>
    [Fact]
    public void EinrichtenMitGesperrterDatei()
    {
        if (OperatingSystem.IsWindows())
        {
            return;     // Windows: derselbe Weg wie EinstellungenTests (IOException beim Lesen)
        }
        using var g = new Geraet("PC");
        Assert.True(g.Aendere(w => w["a"] = 1));
        var vorher = File.ReadAllBytes(g.Einstellungen.Pfad);
        File.SetUnixFileMode(g.Einstellungen.Pfad, UnixFileMode.None);
        try
        {
            var ablauf = new RundgangAblauf(Einrichten.Startwerte(g.Werte));
            ablauf.Springen(4);
            ablauf.Name = "Kim";
            var aenderung = ablauf.Fertig(Heute);
            Assert.NotNull(aenderung);
            Assert.False(g.Aendere(aenderung));
            ablauf.SpeichernFehlgeschlagen();
            Assert.Equal(Optionen.SpeichernFehlgeschlagen, ablauf.Fehler);
            Assert.True(ablauf.Letzte);
            Assert.False(g.Aendere(RundgangAblauf.Ueberspringen(Heute)));
        }
        finally
        {
            File.SetUnixFileMode(g.Einstellungen.Pfad, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        Assert.Equal(vorher, File.ReadAllBytes(g.Einstellungen.Pfad));
    }

    [Theory]
    [InlineData("31.02.2027", "", true)]
    [InlineData("", "1.1.99", false)]
    public void UngueltigesDatumSpeichertGarNichts(string ap1, string ap2, bool erstesFeld)
    {
        using var g = new Geraet("PC");
        var ablauf = new RundgangAblauf(Einrichten.Startwerte(g.Werte));
        ablauf.Springen(4);
        ablauf.Name = "Kim";
        ablauf.Abschnitte["B"] = true;
        ablauf.TerminAp1 = ap1;
        ablauf.TerminAp2 = ap2;
        Assert.Null(ablauf.Fertig(Heute));
        Assert.Equal(RahmenplanTexte.DatumUngueltig, ablauf.Fehler);
        Assert.Equal(erstesFeld ? RundgangElement.TerminAp1 : RundgangElement.TerminAp2, ablauf.FehlerFeld);
        Assert.True(ablauf.Letzte);
        Assert.False(File.Exists(g.Einstellungen.Pfad));
    }

    /// <summary>Einrichten schreibt Name, Rahmenplan und Merker in einem Vorgang; leere Angaben gehen; unbekannte Schluessel bleiben; keine Zeile in der Datenbank.</summary>
    [Fact]
    public void EinrichtenSchreibtNurEinstellungen()
    {
        using var g = new Geraet("PC");
        Assert.True(g.Aendere(w => w["fremd"] = new JsonArray(1, 2)));
        var vorher = g.Zeilenzahlen();
        var ablauf = new RundgangAblauf(Einrichten.Startwerte(g.Werte));
        Assert.Equal(("", "29.09.2027", "26.04.2028"), (ablauf.Name, ablauf.TerminAp1, ablauf.TerminAp2));
        ablauf.Springen(4);
        ablauf.TerminAp1 = "";
        ablauf.TerminAp2 = " ";
        var aenderung = ablauf.Fertig(Heute)!;
        var geschrieben = 0;
        var e = g.Einstellungen;
        var werte = e.Laden();
        aenderung(werte);
        Assert.True(e.Speichern(werte));
        geschrieben++;
        var datei = JsonNode.Parse(File.ReadAllText(e.Pfad))!.AsObject();
        Assert.Equal("", (string)datei["nutzer_name"]!);
        Assert.Equal("", (string)datei["rp_termin_ap1"]!);
        Assert.Equal("", (string)datei["rp_termin_ap2"]!);
        Assert.Equal("2026-10-22", (string)datei["rundgang_gesehen"]!);
        Assert.Equal("[1,2]", datei["fremd"]!.ToJsonString());
        Assert.Equal(1, geschrieben);
        Assert.Equal(vorher, g.Zeilenzahlen());
        Assert.False(Einrichten.RundgangFaellig(g.Werte, false));
    }

    /// <summary>SETUP_DATES_HINT "leer lassen geht auch" (wie Python 167-172): leere Termine sind auch in den Optionen gueltig.</summary>
    [Fact]
    public void LeereTermineWieInDenOptionen()
    {
        Assert.Equal(("", (string?)null), Einrichten.PruefeDatum(""));
        Assert.Equal(("", (string?)null), Einrichten.PruefeDatum("  "));
        var werte = new JsonObject();
        Assert.True(Einrichten.RahmenplanSetzen(werte, Einrichten.TerminAp1Schluessel, ""));
        Assert.Equal("", RahmenplanWerte.Aus(werte, Plan.Value).TerminAp1);
        Assert.Equal("", Einrichten.Startwerte(werte).TerminAp1);
    }

    /// <summary>Der gespeicherte Termin kommt in RahmenplanWerte.Aus an (exam_phase rechnet damit, G16).</summary>
    [Fact]
    public void TermineAusEinrichtenWirken()
    {
        var werte = new JsonObject();
        var (fehler, aenderung) = Einrichten.Speichern("Kim", new Dictionary<string, bool> { ["D"] = true }, "1.3.2027", "2027-4-2", Heute);
        Assert.Null(fehler);
        aenderung!(werte);
        var plan = RahmenplanWerte.Aus(werte, Plan.Value);
        Assert.Equal(("2027-03-01", "2027-04-02", true, false), (plan.TerminAp1, plan.TerminAp2, plan.AbschnittD, plan.AbschnittB));
        Assert.True(plan.Gewichtung);
    }

    /// <summary>Absturzprobe: hart beendet mitten im Einrichten ist einstellungen.json alt oder neu, nie halb.</summary>
    [Fact]
    public void AbsturzBeimEinrichten()
    {
        using var ordner = new TempOrdner();
        for (var runde = 0; runde < 3; runde++)
        {
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtEinrichten, [ordner.Pfad], 10 + runde * 7);
            var einstellungen = new Einstellungen(ordner.Pfad);
            var werte = einstellungen.Laden();
            Assert.Null(einstellungen.DefektHinweis());
            var name = (string)werte[Einrichten.NameSchluessel]!;
            Assert.Matches("^N[0-9a-f]{32}$", name);
            // dieselbe Runde: Name, Termin und Merker stammen aus genau einem Schreibvorgang
            Assert.Equal((string)werte["probe"]!, name);
            Assert.Equal("2026-10-22", (string)werte[Einrichten.GesehenSchluessel]!);
            Assert.True(gemeldet.Count >= 10 + runde * 7);
        }
    }

    // -- Tastatur (Kern) -----------------------------------------------------------------------

    [Fact]
    public void RundgangTastatur()
    {
        var ablauf = new RundgangAblauf(Einrichten.Startwerte([]));
        Assert.Equal([RundgangElement.Weiter, RundgangElement.Ueberspringen], ablauf.Reihenfolge);
        Assert.Equal(RundgangElement.Weiter, ablauf.Naechstes(RundgangElement.Ueberspringen, rueckwaerts: false));
        Assert.False(ablauf.Zurueck());
        Assert.Null(ablauf.Fertig(Heute));          // Eingabe auf "Weiter" = naechste Seite
        Assert.Equal(1, ablauf.Index);
        Assert.Equal([RundgangElement.Weiter, RundgangElement.Ueberspringen, RundgangElement.Zurueck], ablauf.Reihenfolge);
        Assert.Equal(RundgangElement.Weiter, ablauf.Naechstes(RundgangElement.Zurueck, rueckwaerts: false));     // ringfoermig
        Assert.Equal(RundgangElement.Zurueck, ablauf.Naechstes(RundgangElement.Weiter, rueckwaerts: true));      // Umschalt+Tab
        ablauf.Springen(9);
        Assert.Equal(4, ablauf.Index);
        Assert.Equal(
            [RundgangElement.Name, RundgangElement.AbschnittB, RundgangElement.AbschnittD, RundgangElement.AbschnittE,
             RundgangElement.TerminAp1, RundgangElement.TerminAp2, RundgangElement.Ueberspringen, RundgangElement.Zurueck, RundgangElement.Fertig],
            ablauf.Reihenfolge);
        Assert.Equal(RundgangElement.Name, ablauf.Naechstes(RundgangElement.Fertig, rueckwaerts: false));
        Assert.Equal(RundgangElement.Fertig, ablauf.Naechstes(RundgangElement.Name, rueckwaerts: true));
        Assert.Equal("RUNDGANG · SCHRITT 5 VON 5", ablauf.Kopf);
        Assert.False(ablauf.Weiter());
        // Esc = Ueberspringen: nur der Merker
        var werte = new JsonObject { ["x"] = 1 };
        RundgangAblauf.Ueberspringen(Heute)(werte);
        Assert.Equal("{\"x\":1,\"rundgang_gesehen\":\"2026-10-22\"}", werte.ToJsonString());
    }

    /// <summary>_keep_draft: der Entwurf bleibt beim Blaettern erhalten.</summary>
    [Fact]
    public void EntwurfBleibtBeimBlaettern()
    {
        var ablauf = new RundgangAblauf(new EinrichtenWerte("Alex", true, false, false, "29.09.2027", ""));
        ablauf.Springen(4);
        ablauf.Name = "Kim";
        ablauf.Abschnitte["E"] = true;
        Assert.True(ablauf.Zurueck());
        Assert.True(ablauf.Weiter());
        Assert.Equal("Kim", ablauf.Name);
        Assert.True(ablauf.Abschnitte["B"] && ablauf.Abschnitte["E"] && !ablauf.Abschnitte["D"]);
    }

    // -- Ausbildungsfrage und Warnhinweis --------------------------------------------------------

    [Fact]
    public void AusbildungsfrageUeberspringenSpeichertNichts()
    {
        using var g = new Geraet("PC");
        Assert.True(g.Start().Frage);
        var vorher = g.Zeilenzahlen();
        // "Überspringen" (1) und Esc/Zurueck-Geste (-1), auch mit angeklickter Wahl: nur der Merker
        foreach (var knopf in new[] { 1, -1 })
        {
            var (gewaehlt, fehler, merker) = AusbildungFrage.FrageAntwort(g.Schreiber, knopf, "ae", Heute);
            Assert.Null(gewaehlt);
            Assert.False(fehler);
            Assert.True(g.Aendere(merker));
        }
        var ohneWahl = AusbildungFrage.FrageAntwort(g.Schreiber, 0, null, Heute);     // "Speichern" ist ohne Wahl gesperrt
        Assert.Null(ohneWahl.Gewaehlt);
        Assert.False(ohneWahl.Fehler);
        Assert.Equal(vorher, g.Zeilenzahlen());
        Assert.False(Ausbildung.Gewaehlt(g.Schreiber));
        Assert.Equal("si", Ausbildung.Aktuell(g.Schreiber));
        Assert.Equal("2026-10-22", (string)g.Werte[AusbildungFrage.GefragtSchluessel]!);
        Assert.False(g.Werte.ContainsKey(AusbildungFrage.ZuletztSchluessel));
        Assert.False(g.Start().Frage);                 // kommt auf diesem Geraet nicht wieder
        Assert.False(g.Start().VonAnderswo);
    }

    /// <summary>Keine Vorwahl (Gegenpruefung M2): die Oberflaeche setzt keine Wahl, "Speichern" erst nach einer Wahl.</summary>
    [Fact]
    public void AusbildungsfrageOhneVorwahl()
    {
        var quelle = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Views", "AusbildungTeil.cs"));
        Assert.Contains("IsChecked = false,", quelle, StringComparison.Ordinal);
        Assert.Contains("speichern.IsEnabled = false;", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("IsChecked = true", quelle, StringComparison.Ordinal);
    }

    [Fact]
    public void WarnhinweisNurWennNoetig()
    {
        var werte = new JsonObject();
        Assert.False(AusbildungFrage.WarnungNoetig("si", werte));
        Assert.False(AusbildungFrage.WarnungNoetig(null, werte));
        foreach (var (fachrichtung, schluessel) in new[] { ("ae", "rp_abschnitt_b"), ("dpa", "rp_abschnitt_d"), ("dv", "rp_abschnitt_e") })
        {
            Assert.True(AusbildungFrage.WarnungNoetig(fachrichtung, werte));
            Assert.False(AusbildungFrage.WarnungNoetig(fachrichtung, new JsonObject { [schluessel] = true }));
            Assert.True(AusbildungFrage.WarnungNoetig(fachrichtung, new JsonObject { [schluessel] = "" }));    // bool("") = aus
        }
        // AE -> SI -> AE: der Hinweis kommt wieder, solange B aus ist
        Assert.True(AusbildungFrage.WarnungNoetig("ae", werte));
        Assert.False(AusbildungFrage.WarnungNoetig("si", werte));
        Assert.True(AusbildungFrage.WarnungNoetig("ae", werte));
    }

    [Fact]
    public void WarnhinweisNeinAendertNichts()
    {
        using var g = new Geraet("PC");
        Assert.True(g.Aendere(w => w["rp_abschnitt_d"] = true));
        var vorher = File.ReadAllBytes(g.Einstellungen.Pfad);
        // "Nein" und Esc: keine Aenderung, kein Schreibvorgang
        Assert.True(AusbildungFrage.WarnungNoetig("ae", g.Werte));
        Assert.Null(AusbildungFrage.WarnungAntwort(1, "ae"));
        Assert.Null(AusbildungFrage.WarnungAntwort(-1, "ae"));
        Assert.Equal(vorher, File.ReadAllBytes(g.Einstellungen.Pfad));
        // "Ja": genau der Abschnitt B
        var ja = AusbildungFrage.WarnungAntwort(0, "ae");
        Assert.NotNull(ja);
        Assert.True(g.Aendere(ja));
        Assert.Equal("{\"rp_abschnitt_d\":true,\"rp_abschnitt_b\":true}", Ohne(g.Werte, "auto_check").ToJsonString());
    }

    private static JsonObject Ohne(JsonObject werte, string schluessel)
    {
        werte.Remove(schluessel);
        return werte;
    }

    [Fact]
    public void WarnhinweisJaSchaltetNurEin()
    {
        foreach (var (fachrichtung, schluessel) in new[] { ("ae", "rp_abschnitt_b"), ("dpa", "rp_abschnitt_d"), ("dv", "rp_abschnitt_e") })
        {
            var werte = new JsonObject { ["rp_abschnitt_b"] = false, ["rp_abschnitt_d"] = true, ["rp_gewichtung"] = false, ["x"] = 1 };
            var erwartet = werte.DeepClone().AsObject();
            erwartet[schluessel] = true;
            AusbildungFrage.Einschalten(werte, fachrichtung);
            Assert.True(JsonNode.DeepEquals(erwartet, werte), fachrichtung + ": " + werte.ToJsonString());
        }
        var si = new JsonObject { ["rp_abschnitt_b"] = false };
        AusbildungFrage.Einschalten(si, "si");
        Assert.Equal("{\"rp_abschnitt_b\":false}", si.ToJsonString());
    }

    [Fact]
    public void WarnhinweisTextMitFachrichtungUndAbschnitt()
    {
        var text = AusbildungFrage.WarnungText("dpa");
        Assert.StartsWith("Du hast „Daten- und Prozessanalyse (DPA)“ gewählt.", text, StringComparison.Ordinal);
        Assert.Contains("Bei „Ja“ wird „Abschnitt D anzeigen (Daten- und Prozessanalyse)“ eingeschaltet.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("%s", text, StringComparison.Ordinal);
        Assert.Equal(new[] { "ae", "dpa", "dv" }.Max(f => AusbildungFrage.WarnungText(f).Length), text.Length);   // DPA ist der laengste (Bilder)
    }

    /// <summary>Nach Frage und Hinweis kein zweiter Hinweis: ausbildung_zuletzt wird mit der Antwort gesetzt (Auflage P4-11).</summary>
    [Fact]
    public void NachFrageUndHinweisKeinZweiterHinweis()
    {
        using var g = new Geraet("PC");
        Assert.True(g.Start().Frage);
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "dv"));
        Assert.True(g.Aendere(w => AusbildungFrage.Beantwortet(w, "dv", Heute)));
        Assert.True(AusbildungFrage.WarnungNoetig("dv", g.Werte));
        Assert.True(g.Aendere(w => AusbildungFrage.Einschalten(w, "dv")));
        var schritte = g.Start();
        Assert.False(schritte.Frage);
        Assert.False(schritte.VonAnderswo);
    }

    // -- Zwei Geraete (Frage 2 a) -----------------------------------------------------------------

    /// <summary>A waehlt AE und gleicht ab, dann gleicht B ab: B fragt nicht und zeigt den Hinweis genau einmal.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WarnhinweisNachAbgleich(bool bHatteFrage)
    {
        using var attrappe = Attrappe();
        using var a = new Geraet("PC");
        using var b = new Geraet("Handy");
        if (bHatteFrage)
        {
            Assert.True(b.Aendere(w => AusbildungFrage.Beantwortet(w, null, Heute)));     // B hat uebersprungen
        }
        Assert.True(Ausbildung.Waehlen(a.Schreiber, "ae"));
        Assert.True(a.Aendere(w => AusbildungFrage.Beantwortet(w, "ae", Heute)));
        a.Abgleichen(attrappe);
        b.Abgleichen(attrappe);
        var schritte = b.Start();
        Assert.False(schritte.Frage);
        Assert.True(schritte.VonAnderswo);
        var gespeichert = Ausbildung.Gespeichert(b.Schreiber);
        Assert.Equal("ae", gespeichert);
        Assert.True(AusbildungFrage.WarnungNoetig(gespeichert, b.Werte));
        Assert.True(b.Aendere(w => AusbildungFrage.Merken(w, gespeichert!)));
        Assert.False(b.Start().VonAnderswo);             // genau einmal
        Assert.False(a.Start().VonAnderswo);             // A hat den Merker mit der Antwort gesetzt
    }

    /// <summary>Fachrichtung von anderswo, Merker fehlt bei gewaehlter Fachrichtung (Auflage P4-11): Hinweis einmal, wenn noetig.</summary>
    [Fact]
    public void MerkerFehltBeiGewaehlterFachrichtung()
    {
        using var g = new Geraet("PC");
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "dpa"));     // z. B. aus einer Uebernahme
        Assert.True(g.Start().VonAnderswo);
        Assert.True(AusbildungFrage.VonAnderswo("dpa", []));
        Assert.True(AusbildungFrage.VonAnderswo("dpa", new JsonObject { [AusbildungFrage.ZuletztSchluessel] = "unbekannt" }));
        Assert.False(AusbildungFrage.VonAnderswo(null, []));
        Assert.True(g.Aendere(w => AusbildungFrage.Merken(w, "dpa")));
        Assert.False(g.Start().VonAnderswo);
    }

    /// <summary>Abgleich beim Beenden (ohne Vergleich danach): der Merker weicht beim naechsten Start ab.</summary>
    [Fact]
    public void AbgleichBeimBeendenKommtBeimNaechstenStart()
    {
        using var attrappe = Attrappe();
        using var a = new Geraet("PC");
        using var b = new Geraet("Handy");
        Assert.True(Ausbildung.Waehlen(b.Schreiber, "si"));
        Assert.True(b.Aendere(w => AusbildungFrage.Beantwortet(w, "si", Heute)));
        b.Abgleichen(attrappe);
        a.Jetzt = Zeit.AddMinutes(3);
        Assert.True(Ausbildung.Waehlen(a.Schreiber, "dv"));
        Assert.True(a.Aendere(w => AusbildungFrage.Beantwortet(w, "dv", Heute)));
        a.Abgleichen(attrappe);
        b.Abgleichen(attrappe);                           // "beim Beenden": niemand vergleicht jetzt
        Assert.Equal("dv", Ausbildung.Gespeichert(b.Schreiber));
        Assert.True(b.Start().VonAnderswo);               // naechster Start
        Assert.True(AusbildungFrage.WarnungNoetig("dv", b.Werte));
    }

    /// <summary>Ein zweites, noch nicht abgeglichenes Geraet fragt ebenfalls; die neuere Wahl gewinnt (beide Reihenfolgen, gleiche und andere Wahl).</summary>
    [Theory]
    [InlineData("ae", "ae", false)]
    [InlineData("ae", "dv", false)]
    [InlineData("ae", "dv", true)]
    public void AusbildungZweitesGeraetVorDemAbgleich(string wahlA, string wahlB, bool bZuerst)
    {
        using var attrappe = Attrappe();
        using var a = new Geraet("PC");
        using var b = new Geraet("Handy");
        Assert.True(a.Start().Frage);
        Assert.True(b.Start().Frage);
        var (erst, dann, erstWahl, dannWahl) = bZuerst ? (b, a, wahlB, wahlA) : (a, b, wahlA, wahlB);
        Assert.True(Ausbildung.Waehlen(erst.Schreiber, erstWahl));
        dann.Jetzt = Zeit.AddMinutes(1);
        Assert.True(Ausbildung.Waehlen(dann.Schreiber, dannWahl));
        foreach (var g in new[] { a, b, a })
        {
            g.Abgleichen(attrappe);
        }
        Assert.Equal(dannWahl, Ausbildung.Aktuell(a.Schreiber));
        Assert.Equal(dannWahl, Ausbildung.Aktuell(b.Schreiber));
    }

    /// <summary>N4 "gilt auf allen Geräten, die du abgleichst": das zweite Geraet fragt nach dem Abgleich nicht mehr.</summary>
    [Fact]
    public void AusbildungsfrageAufZweitemGeraet()
    {
        using var attrappe = Attrappe();
        using var a = new Geraet("PC");
        using var b = new Geraet("Handy");
        Assert.True(Ausbildung.Waehlen(a.Schreiber, "dpa"));
        a.Abgleichen(attrappe);
        b.Abgleichen(attrappe);
        Assert.False(b.Start().Frage);
        Assert.Equal("dpa", Ausbildung.Aktuell(b.Schreiber));
    }

    // -- Name -----------------------------------------------------------------------------------

    private static string Entpackt(byte[] gz)
    {
        using var ein = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress);
        using var aus = new MemoryStream();
        ein.CopyTo(aus);
        return Encoding.UTF8.GetString(aus.ToArray());
    }

    /// <summary>NAME_OPTION_HINT: der Name steht nur in einstellungen.json, nicht in einer Tabelle, nicht in der Sicherung, nicht im Abgleich.</summary>
    [Fact]
    public void NameNurAufDiesemGeraet()
    {
        const string Kennung = "NamensprobeQ7x";
        using var attrappe = Attrappe();
        using var g = new Geraet("PC");
        Assert.True(Ausbildung.Waehlen(g.Schreiber, "ae"));         // eine Zeile, damit der Abgleich etwas hochlaedt
        var (_, aenderung) = Einrichten.Speichern("  " + Kennung + "  ", new Dictionary<string, bool>(), "", "", Heute);
        Assert.True(g.Aendere(aenderung!));
        Assert.True(g.Aendere(w => Einrichten.NameSetzen(w, Kennung + " Zwei")));
        Assert.Equal(Kennung + " Zwei", Einrichten.NameLesen(g.Werte));
        g.Abgleichen(attrappe);
        using (var db = VergleichsHilfe.Oeffnen(g.Lernstand.Pfad))
        {
            foreach (var tabelle in g.Zeilenzahlen().Keys)
            {
                foreach (var zeile in VergleichsHilfe.Abfrage(db, "SELECT * FROM \"" + tabelle + "\""))
                {
                    Assert.DoesNotContain(zeile, w => w is string s && s.Contains(Kennung, StringComparison.Ordinal));
                }
            }
        }
        Assert.DoesNotContain(Kennung, Entpackt(Ruecksicherung.Erstellen(g.Lernstand, g.Werte, "A 0.9.0", "PC", new DateTime(2026, 10, 22))), StringComparison.Ordinal);
        Assert.NotNull(attrappe.Roh);
        Assert.DoesNotContain(Kennung, Entpackt(attrappe.Roh!), StringComparison.Ordinal);
        Assert.DoesNotContain(attrappe.PutKoerper, k => k.ToJsonString().Contains(Kennung, StringComparison.Ordinal));
    }

    /// <summary>NAME_OPTION_HINT "oben im Dashboard ... Hallo und dein Name": nach Einrichten und nach den Optionen.</summary>
    [Fact]
    public void BegruessungNachNamen()
    {
        var werte = new JsonObject();
        Einrichten.Speichern(" Alex   Muster ", new Dictionary<string, bool>(), "", "").Aenderung!(werte);
        Assert.Equal("Hallo Alex Muster", DashboardTexte.Gruss(Einrichten.NameLesen(werte)));
        Assert.True(Einrichten.NameSetzen(werte, "Kim"));
        Assert.False(Einrichten.NameSetzen(werte, " Kim "));          // gleich: kein Schreibvorgang (save_name)
        Assert.Equal("Hallo Kim", DashboardTexte.Gruss(Einrichten.NameLesen(werte)));
        Assert.True(Einrichten.NameSetzen(werte, ""));
        Assert.Equal("", DashboardTexte.Gruss(Einrichten.NameLesen(werte)));
        var quelle = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Views", "DashboardSeite.cs"));
        Assert.Contains("Core.Hilfe.Einrichten.NameLesen(Zustand.Werte)", quelle, StringComparison.Ordinal);
    }

    /// <summary>WEIGHT_OPTION_HINT "Wiederholungen bleiben unverändert": die Gewichtung ordnet nur neue Inhalte (Schalter aus = Vorgabe-Reihenfolge).</summary>
    [Fact]
    public void GewichtungAendertWiederholungNicht()
    {
        var quelle = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.Core", "Lernen", "Wiederholung.cs"));
        Assert.DoesNotContain("RahmenplanGewicht", quelle, StringComparison.Ordinal);
        Assert.DoesNotContain("RahmenplanWerte", quelle, StringComparison.Ordinal);
        var aus = new JsonObject();
        Assert.True(Einrichten.RahmenplanSetzen(aus, Einrichten.GewichtungSchluessel, false));
        Assert.False(Einrichten.Gewichtung(aus));
        Assert.True(Einrichten.Gewichtung([]));
    }
}
