using System.Reflection;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;
using Lernplattform.Core.Zufall;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 4, W2: Verhaltenstabellen der Klausursimulation (Wortliste W2 Abschnitt 4, je Satz ein
/// Beleg) ueber Core.Pruefung.KlausurAblauf, den Ablauf hinter der Oberflaeche. Die laufende
/// Pruefung liegt wie in der App in einem JSON-Objekt (einstellungen.json), der Lernstand in
/// einer echten Datei; die Uhr ist fest und wird vorgestellt, ein "Neustart" ist ein neuer Ablauf.
/// </summary>
public class KlausurAblaufTests
{
    private static readonly KlausurInhalte Inhalte = KlausurTests.Klausurinhalte;

    /// <summary>Einstellungen und Lernstand eines Geraets; Uhr zum Vorstellen; Speichern kann ausfallen.</summary>
    private sealed class Geraet : IDisposable
    {
        private readonly TempOrdner _ordner = new();
        public JsonObject Werte { get; set; } = [];
        public DateTimeOffset Jetzt { get; set; } = DateTimeOffset.Parse("2026-10-22T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        public Func<JsonObject, bool> Darf { get; set; } = _ => true;
        public LernstandSchreiber Schreiber { get; set; }
        public Lernstand Lernstand { get; }

        public Geraet()
        {
            Lernstand = new Lernstand(Path.Combine(_ordner.Pfad, Lernstand.DateiName));
            Schreiber = new LernstandSchreiber(Lernstand, SpeichernTests.FesteUhr(() => Jetzt));
        }

        public LaufendePruefung Ablage() => new(() => Werte.DeepClone().AsObject(), aendern =>
        {
            var neu = Werte.DeepClone().AsObject();
            aendern(neu);
            if (!Darf(neu))
            {
                return false;
            }
            Werte = neu;
            return true;
        });

        /// <summary>Ein neuer Ablauf: wie ein Neustart des Programms (nichts im Speicher).</summary>
        public KlausurAblauf Neu(int startwert = 7) => new(Inhalte, Ablage(), () => Schreiber, () => Jetzt, KlausurTests.Berlin,
            () => new PythonZufall(startwert));

        public long Zeilen(string tabelle) => (long)Lernstand.Abfragen($"SELECT COUNT(*) FROM {tabelle}")[0][0]!;

        public JsonObject? Gespeichert => Werte[LaufendePruefung.Schluessel] as JsonObject;

        public void Dispose() => _ordner.Dispose();
    }

    private static void Vorstellen(Geraet g, double sekunden) => g.Jetzt = g.Jetzt.AddSeconds(sekunden);

    // -- Nr 23/24: Prüfung starten ------------------------------------------------------------

    [Fact]
    public void Starten_ZeitLaeuftUeberStartUtcAuchNachDemSchliessen()
    {
        using var g = new Geraet();
        var a = g.Neu();
        Assert.Equal(KlausurAnsicht.Lauf, a.Starten(Klausur.Konzeption).Ansicht);
        Assert.Equal(90 * 60, a.Rest);
        var gespeichert = g.Gespeichert!;
        Assert.Equal(g.Jetzt.ToUnixTimeSeconds(), (long)Json.Zahl(gespeichert["start_utc"])!);
        Assert.False(string.IsNullOrEmpty(Json.Text(gespeichert[LaufendePruefung.KennungSchluessel])));
        // "laeuft sofort": Uhr vorstellen, Programm schliessen (neuer Ablauf), wieder oeffnen
        foreach (var abstand in new[] { 1, 59, 600, 3599 })
        {
            Vorstellen(g, abstand);
            var neu = g.Neu();
            Assert.Equal(KlausurAnsicht.Lauf, neu.Zeigen().Ansicht);
            Assert.Equal(90 * 60 - (long)(g.Jetzt - DateTimeOffset.FromUnixTimeSeconds((long)Json.Zahl(gespeichert["start_utc"])!)).TotalSeconds, neu.Rest);
        }
    }

    [Fact]
    public void Starten_KeinPausenwegImCode()
    {
        // "lässt sich nicht anhalten": kein oeffentlicher Weg, der die Zeit anhaelt oder die Startzeit aendert
        foreach (var typ in new[] { typeof(KlausurAblauf), typeof(LaufendePruefung), typeof(Klausur) })
        {
            foreach (var m in typ.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.DoesNotMatch("(?i)pause|anhalt|fortsetz|unterbrech|startsetz", m.Name);
            }
        }
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Ap1);
        var start = Json.Zahl(g.Gespeichert!["start_utc"]);
        // Antworten, Seitenwechsel, Zeigen und Verbergen aendern die Startzeit nie
        a.Festhalten([(Klausur.Schluessel(0, 0), "Antwort")]);
        Vorstellen(g, 120);
        g.Neu().Zeigen();
        Assert.Equal(start, Json.Zahl(g.Gespeichert!["start_utc"]));
        Assert.Equal(90 * 60 - 120, Klausur.Rest(g.Gespeichert!, Klausur.Sekunden(g.Jetzt), KlausurTests.Berlin));
    }

    [Fact]
    public void Starten_UeberschreibtNieEineGespeichertePruefung()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Wiso);
        var kennung = Json.Text(g.Gespeichert![LaufendePruefung.KennungSchluessel]);
        // zweiter Start (anderes Fenster, nach der Rueckfrage): zeigt die laufende
        var zweiter = g.Neu(99);
        Assert.Equal(KlausurAnsicht.Lauf, zweiter.Starten(Klausur.Konzeption).Ansicht);
        Assert.Equal(Klausur.Wiso, zweiter.Art);
        Assert.Equal(kennung, Json.Text(g.Gespeichert![LaufendePruefung.KennungSchluessel]));
        // auch in Phase "bewertung"
        var b = g.Neu();
        b.Zeigen();
        b.Abbrechen();
        var c = g.Neu();
        c.Starten(Klausur.Netzwerke);
        c.Abgeben(auto: false);
        Assert.Equal(KlausurAnsicht.Bewertung, g.Neu().Starten(Klausur.Ap1).Ansicht);
        Assert.Equal(Klausur.Netzwerke, Json.Text(g.Gespeichert!["art"]));
    }

    [Fact]
    public void Starten_OhneSpeichernStartetNichts()
    {
        using var g = new Geraet { Darf = _ => false };
        var schritt = g.Neu().Starten(Klausur.Ap1);
        Assert.Equal(KlausurAnsicht.Auswahl, schritt.Ansicht);
        Assert.Equal(Optionen.SpeichernFehlgeschlagen, schritt.Hinweis);
        Assert.False(g.Neu().Laeuft);
    }

    // -- Nr 42/43: Prüfung läuft --------------------------------------------------------------

    [Fact]
    public void Laeuft_SperrtNurDieGezielteUebung()
    {
        // Umschalter auf "Prüfung" und laufende (gespeicherte) Pruefung: Hinweis, keine Uebung
        Assert.True(KlausurAblauf.UebungGesperrt(pruefungGewaehlt: true, laeuft: true));
        // ohne laufende Pruefung: Umschalten auf Uebung; in Art "Übung": immer
        Assert.False(KlausurAblauf.UebungGesperrt(true, false));
        Assert.False(KlausurAblauf.UebungGesperrt(false, true));
        Assert.False(KlausurAblauf.UebungGesperrt(false, false));
        using var g = new Geraet();
        g.Neu().Starten(Klausur.Wiso);
        var vorher = g.Werte.ToJsonString();
        Assert.True(g.Neu().Laeuft);
        Assert.Equal(vorher, g.Werte.ToJsonString());      // die Pruefung bleibt unveraendert
    }

    // -- Nr 44/45: Prüfung abbrechen ----------------------------------------------------------

    [Fact]
    public void Abbrechen_JaVerwirftAlles_NeinAendertNichts()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Wiso);
        var frage = Json.Text(a.Zustand!["fragen"]![0])!;
        a.Waehlen(frage, Json.Text(a.Zustand["optionen"]![frage]![0])!);
        // "Nein": nichts wird aufgerufen, alles bleibt
        var vorher = g.Werte.ToJsonString();
        Vorstellen(g, 30);
        Assert.Equal(KlausurAnsicht.Lauf, g.Neu().Zeigen().Ansicht);
        Assert.Equal(vorher, g.Werte.ToJsonString());
        // "Ja": laufende Pruefung weg, kein Ergebnis, keine Antworten
        Assert.Equal(KlausurAnsicht.Auswahl, a.Abbrechen().Ansicht);
        Assert.Null(g.Gespeichert);
        Assert.False(g.Neu().Laeuft);
        foreach (var tabelle in new[] { "pruefungen", "quiz_answers", "ap1_events", "scenario_events" })
        {
            Assert.Equal(0, g.Zeilen(tabelle));
        }
    }

    // -- Nr 46/47: Abgeben --------------------------------------------------------------------

    [Fact]
    public void Abgeben_SperrtDieAntwortenUndHaeltDieDauerFest()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Konzeption);
        a.Festhalten([(Klausur.Schluessel(0, 0), "erste Antwort\n\n")]);
        Assert.Equal("erste Antwort", Klausur.Antwort(g.Gespeichert!, Klausur.Schluessel(0, 0)));
        Vorstellen(g, 1234.9);
        Assert.Equal(KlausurAnsicht.Bewertung, a.Abgeben(auto: false).Ansicht);
        var gespeichert = g.Gespeichert!;
        Assert.Equal(Klausur.Bewertung, Json.Text(gespeichert["phase"]));
        Assert.Equal(1234, (long)Json.Zahl(gespeichert["dauer"])!);
        // "Danach kannst du nichts mehr ändern": Antworten und WiSo-Wahl werden nicht mehr gespeichert
        Assert.False(a.Festhalten([(Klausur.Schluessel(0, 0), "geaendert")]));
        Assert.Equal("erste Antwort", Klausur.Antwort(g.Gespeichert!, Klausur.Schluessel(0, 0)));
        // ein Neustart aendert nichts (auch viel spaeter)
        Vorstellen(g, 86400);
        var neu = g.Neu();
        Assert.Equal(KlausurAnsicht.Bewertung, neu.Zeigen().Ansicht);
        Assert.Equal(1234, (long)Json.Zahl(g.Gespeichert!["dauer"])!);
        Assert.Equal(KlausurAnsicht.Bewertung, neu.Abgeben(auto: true).Ansicht);
        Assert.Equal(1234, (long)Json.Zahl(g.Gespeichert!["dauer"])!);
    }

    // -- Nr 48-50: Zeit abgelaufen ------------------------------------------------------------

    [Fact]
    public void ZeitAbgelaufen_WisoIstSofortGespeichert()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Wiso);
        foreach (var frage in a.Zustand!["fragen"]!.AsArray().Take(10))
        {
            a.Waehlen(Json.Text(frage)!, Json.Text(a.Zustand["optionen"]![Json.Text(frage)!]![1])!);
        }
        Vorstellen(g, 60 * 60 - 1);
        Assert.Equal(1, a.Rest);
        Vorstellen(g, 1);
        Assert.Equal(0, a.Rest);
        // die App ist offen: der Takt gibt automatisch ab
        var schritt = a.Abgeben(auto: true);
        Assert.Equal(KlausurAnsicht.Ergebnis, schritt.Ansicht);
        Assert.Equal((PruefungTexte.ZeitTitel, PruefungTexte.ZeitWiso), (schritt.HinweisTitel, schritt.Hinweis));
        Assert.Equal(1, g.Zeilen("pruefungen"));
        Assert.Equal(30, g.Zeilen("quiz_answers"));
        Assert.Null(g.Gespeichert);
        Assert.Equal(3600, (long)Wert(g, "SELECT dauer FROM pruefungen"));
    }

    [Fact]
    public void ZeitAbgelaufen_AuchNachDemNeustart()
    {
        using var g = new Geraet();
        g.Neu().Starten(Klausur.Wiso);
        Vorstellen(g, 5 * 3600);        // App war zu
        var schritt = g.Neu().Zeigen();
        Assert.Equal(KlausurAnsicht.Ergebnis, schritt.Ansicht);
        Assert.Equal(PruefungTexte.ZeitWiso, schritt.Hinweis);
        Assert.Equal(1, g.Zeilen("pruefungen"));
        Assert.Equal(3600, (long)Wert(g, "SELECT dauer FROM pruefungen"));

        using var h = new Geraet();
        h.Neu().Starten(Klausur.Ap1);
        Vorstellen(h, 90 * 60 + 17);
        var offen = h.Neu().Zeigen();
        // Hinweis vor der Bewertung (Wortliste W2 Abschnitt 8, Nr 42/48), Phase "bewertung" gespeichert
        Assert.Equal((KlausurAnsicht.Bewertung, PruefungTexte.ZeitTitel, PruefungTexte.ZeitOffen), (offen.Ansicht, offen.HinweisTitel, offen.Hinweis));
        Assert.Equal(Klausur.Bewertung, Json.Text(h.Gespeichert!["phase"]));
        Assert.Equal(90 * 60, (long)Json.Zahl(h.Gespeichert!["dauer"])!);
        Assert.Equal(0, h.Zeilen("pruefungen"));
        // naechstes Oeffnen: Bewertung ohne Hinweis
        Assert.Equal(new KlausurSchritt(KlausurAnsicht.Bewertung), h.Neu().Zeigen());
    }

    private static object Wert(Geraet g, string sql) => g.Lernstand.Abfragen(sql)[0][0]!;

    // -- Nr 62/63 und 5/52: Bewertung abschließen ---------------------------------------------

    [Fact]
    public void Bewertung_PunkteVorgeschlagenUndAnpassbar()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Netzwerke);
        a.Abgeben(auto: false);
        var teil = Klausur.Details(Inhalte, a.Zustand!, 0)!.Teile[0];
        Assert.Equal(teil.Kriterien.Count, a.Haken(0, 0).Count);
        Assert.All(a.Haken(0, 0), h => Assert.False((bool)h!));
        var gesetzt = new List<bool>();
        for (var k = 0; k < teil.Kriterien.Count; k++)
        {
            gesetzt.Add(k % 2 == 0);
            if (k % 2 == 0)
            {
                Assert.Equal(Klausur.Vorschlag(teil.Punkte, gesetzt.Concat(Enumerable.Repeat(false, teil.Kriterien.Count - gesetzt.Count)).ToList()),
                    a.Haken(0, 0, k, true));
            }
        }
        Assert.Equal(a.Punkte(0, 0), (long)Json.Zahl(g.Gespeichert!["punkte"]![Klausur.Schluessel(0, 0)])!);
        // "wie ein Prüfer anpassen": 0 bis Maximum
        a.PunkteSetzen(0, 0, teil.Punkte + 50);
        Assert.Equal(teil.Punkte, a.Punkte(0, 0));
        a.PunkteSetzen(0, 0, -3);
        Assert.Equal(0, a.Punkte(0, 0));
        a.PunkteSetzen(0, 0, 1);
        Assert.Equal(1, (long)Json.Zahl(g.Gespeichert!["punkte"]![Klausur.Schluessel(0, 0)])!);
        Assert.Equal("Zwischenstand: 1 von 100 Punkten", Klausur.Zwischenstand(Inhalte, a.Zustand!));
    }

    [Fact]
    public void Abschliessen_JaSpeichertErstDannWegDieLaufende()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Konzeption);
        a.Abgeben(auto: false);
        var reihenfolge = new List<string>();
        g.Darf = neu =>
        {
            reihenfolge.Add(neu.ContainsKey(LaufendePruefung.Schluessel) ? "speichern" : "entfernen:" + g.Zeilen("pruefungen"));
            return true;
        };
        a.PunkteSetzen(0, 0, 3);
        var schritt = a.Abschliessen();
        Assert.Equal(KlausurAnsicht.Ergebnis, schritt.Ansicht);
        Assert.Null(schritt.Hinweis);
        Assert.Equal(["speichern", "entfernen:1"], reihenfolge);   // clear_running erst nach der Buchung
        Assert.Equal(1, g.Zeilen("pruefungen"));
        Assert.Equal(4, g.Zeilen("scenario_events"));
        Assert.Equal(Klausur.Konzeption, a.ErgebnisArt);
        Assert.Null(g.Gespeichert);
    }

    [Fact]
    public void Abschliessen_FalseLaesstDieBewertungStehen()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Ap1);
        a.Abgeben(auto: false);
        var gut = g.Schreiber;
        using var weg = new TempOrdner();
        g.Schreiber = new LernstandSchreiber(new Lernstand(Path.Combine(weg.Pfad, "fehlt", Lernstand.DateiName)));
        var schritt = a.Abschliessen();
        Assert.Equal((KlausurAnsicht.Bewertung, KartenTexte.HinweisTitel, TrainerTexte.NichtGespeichert), (schritt.Ansicht, schritt.HinweisTitel, schritt.Hinweis));
        Assert.NotNull(a.Zustand);
        Assert.Equal(Klausur.Bewertung, Json.Text(g.Gespeichert!["phase"]));
        Assert.Equal(KlausurAnsicht.Bewertung, g.Neu().Zeigen().Ansicht);
        // wieder erreichbar: derselbe Abschluss gelingt, genau eine Zeile
        g.Schreiber = gut;
        Assert.Equal(KlausurAnsicht.Ergebnis, a.Abschliessen().Ansicht);
        Assert.Equal(1, g.Zeilen("pruefungen"));
        Assert.Equal(4, g.Zeilen("ap1_events"));
    }

    [Fact]
    public void Abschliessen_ZweimalNachMisslungenemEntfernenBuchtEinmal()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Wiso);
        g.Darf = neu => neu.ContainsKey(LaufendePruefung.Schluessel);      // clear_running misslingt
        Assert.Equal(KlausurAnsicht.Ergebnis, a.Abgeben(auto: false).Ansicht);
        Assert.Equal(Klausur.Bewertung, Json.Text(g.Gespeichert!["phase"]));
        g.Darf = _ => true;
        var neu = g.Neu();
        Assert.Equal(KlausurAnsicht.Bewertung, neu.Zeigen().Ansicht);
        Assert.Equal(KlausurAnsicht.Ergebnis, neu.Abschliessen().Ansicht);
        Assert.Equal(1, g.Zeilen("pruefungen"));
        Assert.Equal(30, g.Zeilen("quiz_answers"));
        Assert.Null(g.Gespeichert);
    }

    [Fact]
    public void Wiso_HatKeinenBewertungsschritt()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Wiso);
        Assert.Equal(KlausurAnsicht.Ergebnis, a.Abgeben(auto: false).Ansicht);     // direkt ausgewertet
        Assert.Equal(1, g.Zeilen("pruefungen"));
    }

    [Fact]
    public void Probe_SpeichertNichts()
    {
        using var g = new Geraet();
        var a = g.Neu();
        a.Starten(Klausur.Konzeption);
        var vorher = g.Werte.ToJsonString();
        var zustand = a.Zustand!.DeepClone().AsObject();
        a.ProbeSetzen(zustand);
        a.Festhalten([(Klausur.Schluessel(0, 0), "Probe")]);
        a.Abgeben(auto: true);
        a.Haken(0, 0, 0, true);
        a.PunkteSetzen(0, 0, 2);
        Assert.Equal(KlausurAnsicht.Bewertung, a.Abschliessen().Ansicht);
        a.Abbrechen();
        Assert.Equal(vorher, g.Werte.ToJsonString());
        Assert.Equal(0, g.Zeilen("pruefungen"));
        Assert.Equal(KlausurAnsicht.Lauf, a.Zeigen().Ansicht);     // Zeigen beendet die Probe
        Assert.False(a.Probe);
    }

    // -- Texte ---------------------------------------------------------------------------------

    /// <summary>Nur Einfachleerzeichen im Zeilenkern, feste Ausnahmeliste (Wortliste W2 Abschnitt 9).</summary>
    [Fact]
    public void Texte_NurEinfachleerzeichen()
    {
        Assert.Equal([PruefungTexte.Erfuellt, PruefungTexte.Offen, PruefungTexte.NoteDauer, PruefungTexte.Teilaufgabe],
            PruefungTexte.DoppelleerzeichenAusnahmen);
        var texte = typeof(PruefungTexte).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (Name: f.Name, Text: (string)f.GetRawConstantValue()!)).ToList();
        texte.AddRange(PruefungTexte.Fachrichtungen.Select(f => ("Fachrichtung " + f.Wert, f.Text)));
        texte.AddRange(PruefungTexte.AusbildungSuchwoerter.Select(w => ("Suchwort", w)));
        foreach (var f in Fachrichtung.Alle)
        {
            texte.AddRange(f.Bereiche.Select(b => ("Bereich " + f.Id, b.Kurz + " " + b.Name)));
            var kachel = Gesamtstand.Kachel(f, [], 70);
            texte.AddRange(kachel.Zeilen.Select(z => ("Kachel " + f.Id, z)));
            var voll = Bewertung.Gesamt(f, new Dictionary<string, double> { ["ap1"] = 60, ["projekt"] = 70, ["b1"] = 45, ["b2"] = 40, ["wiso"] = 55 });
            texte.AddRange(voll.Regeln.Select(r => ("Regelzeile " + f.Id, Gesamtstand.Regel(r))));
            texte.Add(("Ergaenzung " + f.Id, voll.Ergaenzung));
        }
        // je Muster genau die eine freigegebene Stelle (Wortliste W2 Abschnitt 4), sonst nichts
        var stellen = new Dictionary<string, (string Alt, string Neu)>(StringComparer.Ordinal)
        {
            [PruefungTexte.Erfuellt] = (":  ", ": "),
            [PruefungTexte.Offen] = (":  ", ": "),
            [PruefungTexte.NoteDauer] = ("  ·  ", " · "),
            [PruefungTexte.Teilaufgabe] = ("  (", " ("),
        };
        var mitAusnahme = 0;
        var regelzeilen = 0;
        foreach (var (name, text) in texte)
        {
            var kern = string.Join("\n", text.Split('\n').Select(z => z.TrimStart(' ')));
            if (stellen.TryGetValue(text, out var stelle))
            {
                mitAusnahme++;
                var i = kern.IndexOf(stelle.Alt, StringComparison.Ordinal);
                kern = kern[..i] + stelle.Neu + kern[(i + stelle.Alt.Length)..];
            }
            else if (name.StartsWith("Regelzeile", StringComparison.Ordinal))
            {
                // erfuellt/offen davor (Gesamtstand.Regel): nur dieses Praefix hat zwei Leerzeichen
                regelzeilen++;
                var praefix = text.StartsWith(PruefungTexte.Erfuellt, StringComparison.Ordinal) ? PruefungTexte.Erfuellt : PruefungTexte.Offen;
                Assert.StartsWith(praefix, text, StringComparison.Ordinal);
                kern = text[praefix.Length..];
            }
            Assert.False(kern.Contains("  ", StringComparison.Ordinal), name + ": Doppelleerzeichen in \"" + text + "\"");
            Assert.DoesNotContain(' ', kern);
        }
        Assert.Equal(4, mitAusnahme);
        Assert.True(regelzeilen >= 4 * 4);
        // Kontrollfehler: ohne Ausnahmeliste waeren die vier Muster rot
        Assert.All(PruefungTexte.DoppelleerzeichenAusnahmen, t => Assert.Contains("  ", t, StringComparison.Ordinal));
    }

    /// <summary>
    /// "Zeilen ≤ 28" gilt fuer Text in Festbreitenschrift am Handy (Plan Phase 4, Abschnitt 7). In W2
    /// steht kein Text in Festbreitenschrift: das Gesamtergebnis bricht in der normalen Schrift um
    /// (Layoutpruefung im Start ohne Bedienung). Kommt Festbreite dazu, wird dieser Test rot.
    /// </summary>
    [Fact]
    public void Texte_KeineFestbreiteInDerKlausur()
    {
        var datei = Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Views", "KlausurTeil.cs");
        Assert.DoesNotContain("Festbreite", File.ReadAllText(datei), StringComparison.Ordinal);
    }

    // -- W4, Sammelliste 51: offene Antworten sofort festhalten (flush_inputs) ------------------

    /// <summary>Laufende Pruefung in einer echten einstellungen.json (atomar wie Zustand.Aendere).</summary>
    internal static LaufendePruefung EchteAblage(string ordner)
    {
        var einstellungen = new Einstellungen(ordner);
        return new LaufendePruefung(einstellungen.Laden, aendern =>
        {
            var werte = einstellungen.Laden();
            if (einstellungen.NurAnzeige)
            {
                return false;
            }
            aendern(werte);
            return einstellungen.Speichern(werte);
        });
    }

    /// <summary>
    /// Vor Sicherung und Uebernahme und beim Verlassen der App am Handy ruft die Oberflaeche
    /// Zustand.EingabenSpeichern; die Klausur haelt dann sofort fest (nicht erst nach 1,2 s).
    /// Danach liest ein Neustart die Antwort aus der Datei.
    /// </summary>
    [Fact]
    public void EingabenSpeichernHaeltKlausurSofortFest()
    {
        using var ordner = new TempOrdner();
        var db = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        var a = KlausurAbsturzTests.Ablauf(db, EchteAblage(ordner.Pfad));
        a.Starten(Klausur.Konzeption);
        var feld = "noch nicht gespeichert";
        Action? beiEingabenSpeichern = null;
        beiEingabenSpeichern += () => a.Festhalten([(Klausur.Schluessel(0, 0), feld)]);
        var vorher = File.ReadAllText(Path.Combine(ordner.Pfad, Einstellungen.DateiName));
        Assert.DoesNotContain(feld, vorher, StringComparison.Ordinal);
        beiEingabenSpeichern.Invoke();                  // z. B. "Lernstand übernehmen" oder App verlassen
        var gespeichert = new Einstellungen(ordner.Pfad).Laden()[LaufendePruefung.Schluessel]!["antworten"]!;
        Assert.Equal(feld, (string)gespeichert[Klausur.Schluessel(0, 0)]!);
        var neu = KlausurAbsturzTests.Ablauf(db, EchteAblage(ordner.Pfad));
        Assert.Equal(KlausurAnsicht.Lauf, neu.Zeigen().Ansicht);
        Assert.Equal(feld, (string)neu.Zustand!["antworten"]![Klausur.Schluessel(0, 0)]!);
        // die Oberflaeche meldet sich an das Ereignis an (KlausurTeil, Sammelliste 51)
        var quelle = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Views", "KlausurTeil.cs"));
        Assert.Contains("Zustand.BeiEingabenSpeichern += Festhalten;", quelle, StringComparison.Ordinal);
    }

    /// <summary>
    /// Sammelliste 51 (Weisung P8-3, wie Python flush_inputs/_flush_inputs/_lifecycle): Jede Stelle, an
    /// der Python offene Eingaben speichert, ruft Zustand.EingabenSpeichern: Sicherung erstellen,
    /// Sicherung einspielen, Lernstand uebernehmen, Update (vor der Kopie vor_update_*), Beenden und
    /// der Wechsel der Handy-App in den Hintergrund (Avalonia und Android OnPause).
    /// </summary>
    [Theory]
    [InlineData("Lernplattform.App/Views/RuecksicherungTeil.cs", 1)]
    [InlineData("Lernplattform.App/Views/EinspielenTeil.cs", 1)]
    [InlineData("Lernplattform.App/Views/UebernahmeTeil.cs", 1)]
    [InlineData("Lernplattform.App/Programm/UpdateDienst.cs", 1)]
    [InlineData("Lernplattform.App/App.axaml.cs", 1)]
    [InlineData("Lernplattform.Android/MainActivity.cs", 1)]
    public void EingabenSpeichernVorJedemVorgang(string datei, int anzahl)
    {
        var quelle = File.ReadAllText(Path.Combine([Pfade.Wurzel, "src", .. datei.Split('/')]));
        var treffer = System.Text.RegularExpressions.Regex.Matches(quelle, @"Zustand\.EingabenSpeichern\(\);").Count;
        Assert.Equal(anzahl, treffer);
        var zustand = File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App", "Programm", "Zustand.cs"));
        Assert.Contains("EingabenSpeichern();\n        BeimBeenden?.Invoke();", zustand.Replace("\r\n", "\n"), StringComparison.Ordinal);
        if (datei.EndsWith("UpdateDienst.cs", StringComparison.Ordinal))
        {
            // vor dem Laden und Installieren (dort entsteht die Kopie vor_update_*)
            Assert.True(quelle.IndexOf("Zustand.EingabenSpeichern();", StringComparison.Ordinal)
                < quelle.IndexOf("UpdateAblauf.Installieren(", StringComparison.Ordinal));
        }
    }

    /// <summary>Hart beendet waehrend des Festhaltens: einstellungen.json ist heil und enthaelt eine ganze Antwort.</summary>
    [Fact]
    public void AbsturzBeimFesthalten()
    {
        using var ordner = new TempOrdner();
        var db = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        for (var runde = 0; runde < 3; runde++)
        {
            var gemeldet = AbsturzProbe.Starten(AbsturzProbe.ArtKlausurFesthalten, [db, ordner.Pfad], 10 + runde * 7);
            var einstellungen = new Einstellungen(ordner.Pfad);
            var werte = einstellungen.Laden();
            Assert.Null(einstellungen.DefektHinweis());
            Assert.False(File.Exists(Path.Combine(ordner.Pfad, Einstellungen.DefektName)));
            var antwort = (string)werte[LaufendePruefung.Schluessel]!["antworten"]![Klausur.Schluessel(0, 0)]!;
            Assert.Matches("^Antwort [0-9a-f]{32}\\.{3000}$", antwort);
            Assert.True(gemeldet.Count >= 10 + runde * 7);
            File.Delete(Path.Combine(ordner.Pfad, Einstellungen.DateiName));
        }
    }
}
