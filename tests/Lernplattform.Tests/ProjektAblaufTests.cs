using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Projekt;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W3: Ablauf der Seite "Abschlussprojekt" ohne Oberflaeche (Plan P4-8 Abschnitt 6,
/// Weisung P4-8 Punkt 2): verzoegertes Speichern mit Uhr von aussen, nur geaenderte Werte, Loeschen
/// nach "Ja", Vorlage, Nachladen, Fachgespraech, aktives Projekt je Geraet, Abgleich nach jeder Eingabe.
/// </summary>
public class ProjektAblaufTests
{
    /// <summary>Datenbank, Einstellungen im Speicher, feste Uhr fuer Lernstand und Ablauf, Zaehler fuer NachSpeichern.</summary>
    private sealed class Aufbau : IDisposable
    {
        private readonly TempOrdner _ordner = new();
        private long _mikro;
        private int _kennung;

        public DateTimeOffset Jetzt = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);
        public JsonObject Einstellungen = [];
        public bool Abgleich;
        public Func<bool>? AbgleichFrage;
        public int Abgleiche;
        public int Neu;
        public LernstandSchreiber Schreiber;
        public ProjektAblauf Ablauf;
        public readonly Dictionary<string, string> Eingaben = new(StringComparer.Ordinal);

        public string Pfad => Path.Combine(_ordner.Pfad, Lernstand.DateiName);

        public Aufbau()
        {
            Schreiber = NeuerSchreiber();
            Ablauf = NeuerAblauf();
        }

        public LernstandSchreiber NeuerSchreiber() =>
            // jede Zeile eine Mikrosekunde spaeter, damit die Reihenfolge nie an der zufaelligen uid haengt
            new(new Lernstand(Pfad), new Uhr(() => Jetzt.AddTicks(10 * ++_mikro), TimeZoneInfo.Utc));

        public ProjektAblauf NeuerAblauf()
        {
            var ablauf = new ProjektAblauf(() => Schreiber, () => Einstellungen, aendern =>
            {
                aendern(Einstellungen);
                return true;
            }, AbschlussprojektTests.Fragen, () => (AbgleichFrage ?? (() => Abgleich))(), () => Jetzt,
                () => "neu" + (++_kennung).ToString("00", System.Globalization.CultureInfo.InvariantCulture));
            ablauf.NachSpeichern += () => Abgleiche++;
            ablauf.NeuGeladen += () =>
            {
                Neu++;
                Eingaben.Clear();
            };
            return ablauf;
        }

        /// <summary>Wie die Seite: Feld anmelden (Startwert) und tippen.</summary>
        public void Tippen(string feld, string wert, string vorgabe = "")
        {
            if (!Eingaben.ContainsKey(feld))
            {
                Eingaben[feld] = Ablauf.Startwert(feld, vorgabe);
                Ablauf.Eingabe(feld, () => Eingaben[feld]);
            }
            Eingaben[feld] = wert;
            Ablauf.Getippt(feld);
        }

        public int Zeilen() => Convert.ToInt32(Schreiber.Lernstand.Abfragen("SELECT COUNT(*) FROM abschlussprojekt")[0][0]);

        public OrderedDictionary<string, string> Gespeichert(string projekt) =>
            Abschlussprojekt.Projekte(Schreiber.ProjektFelder()).TryGetValue(projekt, out var felder) ? felder : [];

        public void Dispose() => _ordner.Dispose();
    }

    [Fact]
    public void Speichern_800msNachDemLetztenTastendruck()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        Assert.Equal("neu01", a.Ablauf.Projekt);
        var start = a.Jetzt;
        a.Tippen("ziel", "A");
        Assert.Equal(start.AddMilliseconds(800), a.Ablauf.Frist);
        a.Jetzt = start.AddMilliseconds(500);
        a.Tippen("ziel", "AB");
        a.Jetzt = start.AddMilliseconds(1299);
        Assert.False(a.Ablauf.Takt());
        Assert.Equal(0, a.Zeilen());
        a.Jetzt = start.AddMilliseconds(1300);
        Assert.True(a.Ablauf.Takt());
        Assert.Equal("AB", a.Gespeichert("neu01")["ziel"]);
        Assert.Equal(1, a.Zeilen());
        Assert.Equal(1, a.Abgleiche);
        Assert.Null(a.Ablauf.Frist);
        Assert.False(a.Ablauf.Takt());
    }

    [Fact]
    public void FeldVerlassenSpeichert_UnveraendertNicht()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        a.Tippen("betrieb", "Firma");
        Assert.True(a.Ablauf.Speichern());          // Feld verlassen (FocusOut)
        Assert.Equal(1, a.Abgleiche);
        a.Tippen("betrieb", "Firma X");
        a.Tippen("betrieb", "Firma");               // zurueck auf den gespeicherten Wert
        Assert.False(a.Ablauf.Speichern());
        Assert.Equal(1, a.Zeilen());
        Assert.Equal(1, a.Abgleiche);
        Assert.Empty(a.Ablauf.Offen);
        // Feld: strip, Textfeld: rstrip (Pythons Leerraum, auch U+3000)
        a.Ablauf.Eingabe("ziel", () => "\u3000 Ziel \n", textfeld: true);
        a.Ablauf.Getippt("ziel");
        a.Ablauf.Eingabe("betrieb", () => " Firma\u3000");
        a.Ablauf.Getippt("betrieb");
        Assert.True(a.Ablauf.Speichern());
        Assert.Equal("\u3000 Ziel", a.Gespeichert("neu01")["ziel"]);
        Assert.Equal(2, a.Zeilen());
    }

    [Fact]
    public void GeleertesFeldMitVorgabe_WirdNichtGespeichert()
    {
        // Sammelliste 49: wie Python vergleicht _flush mit fields.get(feld, ""), nicht mit der Vorgabe
        using var a = new Aufbau();
        a.Ablauf.Laden();
        a.Tippen(Abschlussprojekt.SeitenFeld, "", "15");
        Assert.False(a.Ablauf.Speichern());
        Assert.Equal(0, a.Zeilen());
        a.Tippen(Abschlussprojekt.SeitenFeld, "20");
        Assert.True(a.Ablauf.Speichern());
        a.Tippen(Abschlussprojekt.SeitenFeld, "");
        Assert.True(a.Ablauf.Speichern());          // jetzt steht "20" gespeichert, "" ist eine Aenderung
        Assert.Equal("", a.Gespeichert(a.Ablauf.Projekt!)[Abschlussprojekt.SeitenFeld]);
    }

    [Fact]
    public void JedeEingabePlantEinenAbgleich_UnveraendertKeinen()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        a.Ablauf.Haken("antrag_eingereicht", true);
        a.Ablauf.Haken("antrag_eingereicht", true);
        a.Ablauf.FachbereichSetzen(Fachbereiche.Netzwerk);
        a.Ablauf.FachbereichSetzen(Fachbereiche.Netzwerk);
        a.Ablauf.Bewerten(true);
        a.Tippen("ziel", "Z");
        a.Ablauf.ReiterWechseln("antrag");
        a.Tippen("ziel", "Z");
        a.Ablauf.ReiterWechseln("zeitplan");
        Assert.Equal(4, a.Abgleiche);
        Assert.Equal(4, a.Zeilen());
        Assert.Equal("1", a.Gespeichert("neu01")["check_antrag_eingereicht"]);
        a.Ablauf.Haken("antrag_eingereicht", false);
        Assert.Equal("0", a.Gespeichert("neu01")["check_antrag_eingereicht"]);
        Assert.Equal(5, a.Abgleiche);
    }

    [Fact]
    public void Loeschen_NurNachJa_DanachNeuesAktivesProjekt_OffeneEingabenVerworfen()
    {
        using var a = new Aufbau();
        a.Schreiber.ProjektFeldSpeichern("b-zwei", "titel", "Zwei");
        a.Schreiber.ProjektFeldSpeichern("a-eins", "titel", "Eins");
        a.Ablauf.Laden("b-zwei");
        Assert.Equal("b-zwei", (string?)a.Einstellungen[Abschlussprojekt.AktivSchluessel]);
        var (titel, text) = a.Ablauf.LoeschenFrage();   // nur die Frage: nichts geloescht
        Assert.Equal(ProjektTexte.RueckfrageTitel, titel);
        Assert.Equal(Abschlussprojekt.Rueckfrage(a.Gespeichert("b-zwei"), false), text);
        Assert.True(a.Gespeichert("b-zwei").Count > 0);
        a.Tippen("ziel", "nicht speichern");
        a.Ablauf.Loeschen();                         // "Ja"
        Assert.Empty(a.Ablauf.Offen);
        Assert.False(Abschlussprojekt.Projekte(a.Schreiber.ProjektFelder()).ContainsKey("b-zwei"));
        Assert.Equal("a-eins", a.Ablauf.Projekt);
        Assert.Equal("a-eins", (string?)a.Einstellungen[Abschlussprojekt.AktivSchluessel]);
        Assert.DoesNotContain(a.Schreiber.ProjektFelder()["b-zwei"], f => f.Key == "ziel");
        Assert.Equal(1, a.Abgleiche);
        a.Ablauf.Loeschen();
        Assert.Equal("neu01", a.Ablauf.Projekt);     // keins mehr: neue Kennung, noch nicht gespeichert
        Assert.Empty(Abschlussprojekt.Projekte(a.Schreiber.ProjektFelder()));
    }

    [Fact]
    public void LoeschenFrage_SatzHaengtAmAbgleich_FehlerGibtDenAbgleichSatz()
    {
        using var a = new Aufbau();
        a.Schreiber.ProjektFeldSpeichern("p", "titel", "Mein Projekt");
        a.Ablauf.Laden();
        var felder = a.Gespeichert("p");
        Assert.Equal(Abschlussprojekt.Rueckfrage(felder, false), a.Ablauf.LoeschenFrage().Text);
        a.Abgleich = true;
        Assert.Equal(Abschlussprojekt.Rueckfrage(felder, true), a.Ablauf.LoeschenFrage().Text);
        a.AbgleichFrage = () => throw new IOException("kaputt");
        Assert.Equal(Abschlussprojekt.Rueckfrage(felder, true), a.Ablauf.LoeschenFrage().Text);
        Assert.NotEqual(Abschlussprojekt.Rueckfrage(felder, false), Abschlussprojekt.Rueckfrage(felder, true));
    }

    [Fact]
    public void Vorlage_FuelltNurLeereFelder()
    {
        using var a = new Aufbau();
        var vorlage = AbschlussprojektTests.Inhalte.Testprojekte[0];
        a.Ablauf.Laden();
        a.Tippen("titel", "Eigener Titel");
        a.Tippen("betrieb", "   ");
        var hinweis = a.Ablauf.Vorlage(vorlage);     // speichert zuerst die offenen Felder
        var felder = a.Gespeichert("neu01");
        Assert.Equal("Eigener Titel", felder["titel"]);
        Assert.Equal(vorlage.Text("branche"), felder["betrieb"]);
        Assert.Equal(vorlage.Titel, felder["bezeichnung"]);
        Assert.Equal(Abschlussprojekt.VorlageHinweis(6), hinweis);
        Assert.Equal(a.Gespeichert("neu01"), a.Ablauf.Felder);
        Assert.Equal(Abschlussprojekt.VorlageHinweis(0), a.Ablauf.Vorlage(vorlage));
    }

    [Fact]
    public void Nachladen_NurOhneOffeneEingaben()
    {
        using var a = new Aufbau();
        a.Schreiber.ProjektFeldSpeichern("p", "titel", "Alt");
        a.Ablauf.Laden();
        var neu = a.Neu;
        Assert.False(a.Ablauf.Zeigen());             // nichts geaendert
        a.Schreiber.ProjektFeldSpeichern("p", "ziel", "vom Abgleich");
        a.Tippen("betrieb", "offen");
        Assert.False(a.Ablauf.Zeigen());             // offene Eingabe: nicht nachladen
        Assert.Equal(neu, a.Neu);
        a.Ablauf.Speichern();
        // wie Python: _flush setzt den Stempel neu, die Zeile vom Abgleich bleibt bis zur naechsten
        // Aenderung unsichtbar (Sammelliste 50)
        Assert.False(a.Ablauf.Zeigen());
        Assert.False(a.Ablauf.Felder.ContainsKey("ziel"));
        a.Schreiber.ProjektFeldSpeichern("q", "titel", "Anderes");
        Assert.True(a.Ablauf.Zeigen());
        Assert.Equal("vom Abgleich", a.Ablauf.Felder["ziel"]);
        Assert.Equal("offen", a.Ablauf.Felder["betrieb"]);
        Assert.Equal(neu + 1, a.Neu);
        // nur ein anderes Projekt geaendert: Stempel neu, aber nicht neu laden
        a.Schreiber.ProjektFeldSpeichern("q", "ziel", "Q");
        Assert.False(a.Ablauf.Zeigen());
        Assert.Equal(neu + 1, a.Neu);
        // gezeigtes Projekt woanders geloescht: ein anderes aktiv
        Abschlussprojekt.Loeschen(a.Schreiber, "p");
        Assert.True(a.Ablauf.Zeigen());
        Assert.Equal("q", a.Ablauf.Projekt);
    }

    [Fact]
    public void Fachbereich_30Auf36Fragen_ZurueckAufFrage1()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        Assert.Equal(30, a.Ablauf.AktuelleFragen().Count);
        Assert.Equal(ProjektTexte.KeinFachbereich, a.Ablauf.Fachbereich);
        a.Ablauf.Weiter();
        a.Ablauf.Weiter();
        a.Ablauf.HinweisUmschalten();
        Assert.True(a.Ablauf.HinweisOffen);
        Assert.Equal(2, a.Ablauf.FrageNummer);
        var neuGebaut = 0;
        a.Ablauf.FragenNeu += () => neuGebaut++;
        a.Ablauf.FachbereichSetzen(Fachbereiche.Netzwerk);
        Assert.Equal(36, a.Ablauf.AktuelleFragen().Count);
        Assert.Equal(0, a.Ablauf.FrageNummer);
        Assert.Equal(1, neuGebaut);
        Assert.Equal(Fachbereiche.Netzwerk, a.Ablauf.Fachbereich);
        a.Ablauf.FachbereichSetzen(ProjektTexte.KeinFachbereich);
        Assert.Equal("", a.Gespeichert("neu01")[Abschlussprojekt.FachbereichFeld]);
        Assert.Equal(30, a.Ablauf.AktuelleFragen().Count);
        // Bewerten speichert und geht weiter, der Hinweis schliesst
        a.Ablauf.HinweisUmschalten();
        var erste = a.Ablauf.AktuelleFrage()!.Value.Frage;
        a.Ablauf.Bewerten(false);
        Assert.False(a.Ablauf.HinweisOffen);
        Assert.Equal("0", a.Gespeichert("neu01")["fg_" + erste.Id]);
        Assert.Equal(1, a.Ablauf.FrageNummer);
        Assert.Equal(Beschriftungen.Einsetzen(ProjektTexte.FragenStand, 2, 30, 0, 1, 29), a.Ablauf.FragenStand());
        a.Ablauf.Weiter(-2);
        Assert.Equal(29, a.Ablauf.FrageNummer);
    }

    [Fact]
    public void AktivesProjektJeGeraet()
    {
        using var a = new Aufbau();
        a.Schreiber.ProjektFeldSpeichern("a", "titel", "A");
        a.Schreiber.ProjektFeldSpeichern("b", "titel", "B");
        a.Ablauf.Laden();
        Assert.Equal("a", a.Ablauf.Projekt);
        a.Ablauf.Waehlen("b");
        Assert.Equal("b", (string?)a.Einstellungen[Abschlussprojekt.AktivSchluessel]);
        // gleiche Datenbank, anderes Geraet (eigene Einstellungen): dort bleibt "a"
        var anderes = new JsonObject();
        var zweites = new ProjektAblauf(() => a.Schreiber, () => anderes, aendern =>
        {
            aendern(anderes);
            return true;
        }, AbschlussprojektTests.Fragen, () => false);
        zweites.Laden();
        Assert.Equal("a", zweites.Projekt);
        // dieses Geraet startet neu: wieder "b"
        a.Ablauf = a.NeuerAblauf();
        a.Ablauf.Laden();
        Assert.Equal("b", a.Ablauf.Projekt);
    }

    [Fact]
    public void Menue_ZeigtAuchDasUngespeicherteNeueProjekt()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        Assert.Equal([("neu01", ProjektTexte.OhneTitel)], a.Ablauf.Menue());
        a.Schreiber.ProjektFeldSpeichern("x", "titel", "Zebra");
        a.Ablauf.NeuesProjekt();
        Assert.Equal("neu02", a.Ablauf.Projekt);
        Assert.Equal(1, a.Abgleiche);
        var titel = 0;
        a.Ablauf.MenueGeaendert += () => titel++;
        a.Tippen("titel", "Alpha");
        a.Ablauf.Speichern();
        Assert.Equal(1, titel);
        Assert.Equal([("neu02", "Alpha"), ("x", "Zebra")], a.Ablauf.Menue());
    }

    [Fact]
    public void AngezeigteWerte()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        a.Tippen("kapitel_einleitung", new string('x', 5600));
        a.Ablauf.Speichern();
        Assert.Equal(("Etwa 2,0 von 15 Seiten", false), a.Ablauf.SeitenText());
        a.Tippen("kapitel_einleitung", string.Concat(Enumerable.Repeat("😀", 2800)));
        a.Ablauf.Speichern();
        Assert.Equal(("Etwa 1,0 von 15 Seiten", false), a.Ablauf.SeitenText());
        Assert.Equal(Beschriftungen.Einsetzen(ProjektTexte.Erledigt, 0, Abschlussprojekt.ChecklisteUebersicht.Count), a.Ablauf.ErledigtText());
        Assert.StartsWith(ProjektTexte.AntragOffen[..10], a.Ablauf.AntragText(), StringComparison.Ordinal);
        Assert.Equal(("Summe: 40 Std. von 40 Stunden", false), a.Ablauf.SummeText());
    }

    [Fact]
    public void Summe_GrenzeDerAusbildung()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        a.Tippen("phase_planung", "8");     // Vorgaben 40 Stunden, Planung 7 -> 8
        a.Ablauf.Speichern();
        Assert.Equal(("Summe: 41 Std. von 40 Stunden", true), a.Ablauf.SummeText());
        Assert.NotEqual("", a.Ablauf.WarnungenText());
        Assert.True(Ausbildung.Waehlen(a.Schreiber, "ae"));
        Assert.True(a.Ablauf.Zeigen() || a.Ablauf.MaxStunden == 80);
        Assert.Equal(80, a.Ablauf.MaxStunden);
        Assert.Equal(("Summe: 41 Std. von 80 Stunden", false), a.Ablauf.SummeText());
        Assert.Equal(string.Join("\n", Abschlussprojekt.Warnungen(a.Ablauf.Felder, 80)), a.Ablauf.WarnungenText());
        Assert.Contains("höchstens 80 Stunden", System.Text.Encoding.UTF8.GetString(a.Ablauf.ExportDaten("txt", true, new DateTime(2026, 10, 22))),
            StringComparison.Ordinal);
    }

    [Fact]
    public void Schaetzung_FuerDieKachel()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        foreach (var (eingabe, soll) in new (string, double?)[] { ("85.5", 85.5), ("150", 100), ("-5", 0), ("abc", 0), ("", null), ("  ", null) })
        {
            a.Tippen(Abschlussprojekt.SchaetzungFeld, eingabe);
            a.Ablauf.Speichern();
            Assert.Equal(soll, Gesamtstand.Projektschaetzung(a.Schreiber.ProjektFelder(), (string?)a.Einstellungen[Abschlussprojekt.AktivSchluessel]));
        }
    }

    [Fact]
    public void ExportSpeichertZuerst()
    {
        using var a = new Aufbau();
        a.Ablauf.Laden();
        a.Tippen("titel", "Mein Netz");
        var name = a.Ablauf.ExportName("pdf");
        Assert.Equal("Mein Netz", a.Gespeichert("neu01")["titel"]);
        Assert.Equal(Abschlussprojekt.ExportName(a.Gespeichert("neu01"), "pdf"), name);
        Assert.Equal(Abschlussprojekt.ExportPdf(a.Ablauf.Felder, 40, new DateTime(2026, 10, 22)),
            a.Ablauf.ExportDaten("pdf", false, new DateTime(2026, 10, 22)));
        Assert.Equal(Abschlussprojekt.TextDatei(Abschlussprojekt.ExportText(a.Ablauf.Felder, 40), false),
            a.Ablauf.ExportDaten("txt", false, new DateTime(2026, 10, 22)));
    }

    [Fact]
    public void Lesefehler_LeerStattAbsturz()
    {
        using var a = new Aufbau();
        File.WriteAllBytes(a.Pfad, [.. "kein SQLite"u8]);
        a.Schreiber = a.NeuerSchreiber();
        Assert.Equal("kaputt", a.Schreiber.Lernstand.NurLesenGrund);
        a.Ablauf.Laden();
        Assert.Equal("neu01", a.Ablauf.Projekt);
        a.Tippen("ziel", "geht nicht");
        a.Ablauf.Speichern();
        a.Ablauf.Loeschen();
        Assert.False(a.Ablauf.Zeigen());
        Assert.Equal("kein SQLite"u8.ToArray(), File.ReadAllBytes(a.Pfad));
    }

    [Fact]
    public void Schreibschutz_NichtsGespeichert()
    {
        using var a = new Aufbau();
        a.Schreiber.ProjektFeldSpeichern("p", "titel", "T");
        if (OperatingSystem.IsWindows())
        {
            File.SetAttributes(a.Pfad, FileAttributes.ReadOnly);
        }
        else
        {
            File.SetUnixFileMode(a.Pfad, UnixFileMode.UserRead);
        }
        a.Schreiber = a.NeuerSchreiber();
        Assert.Equal("schreibschutz", a.Schreiber.Lernstand.NurLesenGrund);
        a.Ablauf.Laden();
        Assert.Equal("p", a.Ablauf.Projekt);
        a.Tippen("ziel", "Z");
        a.Ablauf.Speichern();
        a.Ablauf.Loeschen();
        Assert.Equal(1, a.Zeilen());
        Assert.Equal("T", a.Gespeichert("p")["titel"]);
    }

    /// <summary>
    /// Absturz mitten im Loeschen (Kindprozess, hart beendet; Diff A, eine Buchung): Datenbank in
    /// Ordnung; jedes Projekt ist ganz geloescht (Marke und jedes Feld leer) oder gar nicht (keine
    /// Marke, kein leeres Feld); jedes gemeldete Projekt ist ganz geloescht.
    /// </summary>
    [Fact]
    public void AbsturzBeimLoeschen_AllesOderNichts()
    {
        using var ordner = new TempOrdner();
        for (var runde = 0; runde < 3; runde++)
        {
            // je Runde eine eigene Datenbank (die Probe beginnt immer bei probe-0)
            var pfad = Path.Combine(Directory.CreateDirectory(Path.Combine(ordner.Pfad, "r" + runde)).FullName, Lernstand.DateiName);
            _ = new Lernstand(pfad);
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtProjektLoeschen, [pfad], 15 + runde * 11);
            using (var db = VergleichsHilfe.Oeffnen(pfad))
            {
                Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
            }
            var alle = new LernstandSchreiber(new Lernstand(pfad)).ProjektFelder();
            Assert.True(gemeldet.Count >= 15 + runde * 11);
            foreach (var projekt in gemeldet)
            {
                Assert.Equal("1", alle[projekt]["_geloescht"]);
            }
            foreach (var (projekt, felder) in alle)
            {
                if (felder.TryGetValue("_geloescht", out var marke))
                {
                    Assert.Equal("1", marke);
                    Assert.All(felder.Where(f => f.Key != "_geloescht"), f => Assert.Equal("", f.Value));
                }
                else
                {
                    Assert.All(felder, f => Assert.NotEqual("", f.Value));
                }
            }
        }
    }

    /// <summary>
    /// Absturz mitten im Speichern mit vielen Projektfeldern (6 Projekte x 8 Felder, 3 Runden):
    /// Datenbank in Ordnung, je Feld genau eine Zeile (die neueste), jede Zeile ganz.
    /// </summary>
    [Fact]
    public void AbsturzBeimSpeichernVielerFelder_JeFeldEineZeile()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        for (var runde = 0; runde < 3; runde++)
        {
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtProjektfelderViele, [pfad], 60 + runde * 41);
            using var db = VergleichsHilfe.Oeffnen(pfad);
            Assert.Equal("ok", (string)VergleichsHilfe.Abfrage(db, "PRAGMA quick_check")[0][0]!);
            var zeilen = VergleichsHilfe.Abfrage(db, "SELECT projekt, feld, wert, uid FROM abschlussprojekt");
            Assert.Equal(zeilen.Count, zeilen.Select(z => ((string)z[0]!, (string)z[1]!)).Distinct().Count());
            Assert.Equal(48, zeilen.Count);
            foreach (var z in zeilen)
            {
                Assert.Equal("wert " + ((string)z[3]!)["probe-".Length..] + new string('.', 3000), z[2]);
            }
            Assert.True(gemeldet.Count >= 60 + runde * 41);
        }
    }
}
