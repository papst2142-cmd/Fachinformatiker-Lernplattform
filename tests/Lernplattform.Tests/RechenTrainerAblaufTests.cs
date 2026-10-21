using System.Text.Json;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Rechner;
using Lernplattform.Core.Texte;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 3, W3 (R7, Weisung P3-12 Punkt 4): Ablauf des Rechen-Trainers je Zeile der
/// Verhaltenstabelle (Meldung P3-11 Anlage). Gezaehlt werden die Zeilen in trainer_aufgaben
/// nach jedem Ereignis, die Anstoesse NachSpeichern (Abgleich) und die Lesungen der Statistik.
/// Dazu R6-Nachtrag "→ wischen" (G27 "wischen") und die Texte von check() (G27 "pruefen").
/// </summary>
public class RechenTrainerAblaufTests
{
    private static readonly JsonElement G27 = VergleichsHilfe.Lade("rechentrainer.json");

    private sealed class Aufbau : IDisposable
    {
        private readonly TempOrdner _ordner = new();
        public readonly string Pfad;
        public readonly Lernstand Lernstand;
        public readonly RechenTrainerAblauf Ablauf;
        public int Anstoesse;
        public int Meldungen;

        public Aufbau(long startwert = 4711, bool kaputt = false)
        {
            Pfad = System.IO.Path.Combine(_ordner.Pfad, Lernstand.DateiName);
            if (kaputt)
            {
                File.WriteAllBytes(Pfad, [.. "keine Datenbank"u8, .. new byte[300]]);
            }
            Lernstand = new Lernstand(Pfad, _ => Meldungen++);
            Ablauf = new RechenTrainerAblauf(new LernstandSchreiber(Lernstand), () => startwert);
            Ablauf.NachSpeichern += () => Anstoesse++;
        }

        /// <summary>Zeilen in trainer_aufgaben (art, stufe, correct).</summary>
        public List<string> Zeilen()
        {
            if (!File.Exists(Pfad) || Lernstand.NurLesenGrund == "kaputt")
            {
                return [];
            }
            using var db = VergleichsHilfe.Oeffnen(Pfad);
            return VergleichsHilfe.Abfrage(db, "SELECT art, stufe, correct FROM trainer_aufgaben ORDER BY id")
                .Select(z => $"{z[0]}:{z[1]}:{z[2]}").ToList();
        }

        public void Dispose() => _ordner.Dispose();
    }

    private static Dictionary<string, string> Loesung(TrainerAufgabe a) => a.Loesung.ToDictionary(p => p.Key, p => p.Value);

    private static readonly Dictionary<string, string> Leer = [];

    /// <summary>Zustand A: Start-Satz, Knopf "Prüfen" gesperrt (PC und Handy), Druecken tut nichts (Zeile 8).</summary>
    [Fact]
    public void OhneRundeGesperrtUndOhneWirkung()
    {
        using var t = new Aufbau();
        var a = t.Ablauf;
        Assert.Equal(TrainerZustand.KeineRunde, a.Zustand);
        Assert.False(a.KnopfAktiv);
        Assert.Equal(RechnerTexte.Pruefen, a.Knopf);
        Assert.Equal("Wähle Aufgabenart und Schwierigkeit und starte eine Runde mit 10 Aufgaben.", a.AufgabeText);
        Assert.Equal("", a.Zaehler);
        Assert.Equal("", a.Kopf);
        Assert.Null(a.Druecken(Leer, false));
        Assert.Null(a.Druecken(Leer, true));
        Assert.Empty(t.Zeilen());
        Assert.Equal(0, t.Anstoesse);
        Assert.Equal(0, a.StatistikLesungen);
    }

    /// <summary>Zeile 3: "Runde starten" baut die Runde wie trainer_round, schreibt nichts, auch mitten in einer Runde.</summary>
    [Fact]
    public void RundeStartenSchreibtNie()
    {
        using var t = new Aufbau(123456789);
        var a = t.Ablauf;
        a.SetzeArt("vlsm");
        a.SetzeStufe(3);
        a.RundeStarten();
        var soll = RechenTrainer.RundeBauen("vlsm", 3, 123456789L);
        Assert.Equal(TrainerZustand.Offen, a.Zustand);
        Assert.True(a.KnopfAktiv);
        Assert.Equal(10, a.Anzahl);
        Assert.Equal(soll[0].Text, a.AufgabeText);
        Assert.Equal("Aufgabe 1 / 10", a.Zaehler);
        Assert.Equal("VLSM · Schwer", a.Kopf);
        a.Druecken(Leer, false);
        a.Druecken(Leer, false);
        Assert.Single(t.Zeilen());
        a.RundeStarten();
        Assert.Equal(0, a.Index);
        Assert.Empty(a.Ergebnisse);
        Assert.Equal(TrainerZustand.Offen, a.Zustand);
        Assert.Single(t.Zeilen());
        Assert.Equal(1, t.Anstoesse);
    }

    /// <summary>Zeile 2: Art oder Stufe waehlen aendert die laufende Runde nicht, erst die naechste.</summary>
    [Fact]
    public void ArtUndStufeErstInDerNaechstenRunde()
    {
        using var t = new Aufbau(7);
        var a = t.Ablauf;
        a.RundeStarten();
        var text = a.AufgabeText;
        a.SetzeArt("teilen");
        a.SetzeStufe(2);
        a.SetzeArt("gibtsnicht");
        a.SetzeStufe(9);
        Assert.Equal(text, a.AufgabeText);
        Assert.Equal("ipv4", a.Aufgabe!.Art);
        Assert.Equal(1, a.Aufgabe.Stufe);
        a.RundeStarten();
        Assert.Equal("teilen", a.Aufgabe!.Art);
        Assert.Equal(2, a.Aufgabe.Stufe);
        Assert.Empty(t.Zeilen());
    }

    /// <summary>
    /// Zeilen 4 bis 7: je "Prüfen" genau eine Zeile (leere Felder = falsch) und genau ein Anstoss;
    /// der zweite Druck ("Nächste Aufgabe", bei der letzten "Auswertung") schreibt nie.
    /// </summary>
    [Fact]
    public void JePruefenGenauEineZeile()
    {
        using var t = new Aufbau(42);
        var a = t.Ablauf;
        a.SetzeArt("zahlen");
        a.SetzeStufe(2);
        a.ErstesZeigen();
        a.RundeStarten();
        var erwartet = new List<string>();
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(TrainerZustand.Offen, a.Zustand);
            Assert.Equal(RechnerTexte.Pruefen, a.Knopf);
            var aufgabe = a.Aufgabe!;
            var richtig = i % 3 != 0;
            var p = a.Druecken(richtig ? Loesung(aufgabe) : Leer, false)!;
            erwartet.Add($"zahlen:2:{(richtig ? 1 : 0)}");
            Assert.Equal(erwartet, t.Zeilen());
            Assert.Equal(i + 1, t.Anstoesse);
            Assert.Equal(TrainerZustand.Geprueft, a.Zustand);
            Assert.Equal(richtig, p.AllesRichtig);
            Assert.Equal(richtig ? "Alles richtig." : "Noch nicht ganz - hier der Rechenweg:", p.Rueckmeldung);
            Assert.Equal(aufgabe.Felder.Select(f => f.Schluessel), p.Marken.Select(m => m.Schluessel));
            Assert.All(p.Marken, m => Assert.Equal(richtig ? "richtig" : "richtig wäre: " + aufgabe.Loesung[m.Schluessel], m.Text));
            Assert.Equal(string.Join("\n", aufgabe.Schritte), p.Rechenweg);
            Assert.Equal(i == 9 ? "Auswertung" : "Nächste Aufgabe", a.Knopf);
            Assert.Equal(i == 9 ? "Auswertung" : "Nächste Aufgabe", p.Knopf);
            // zweiter Druck: weiter, nie eine zweite Zeile
            Assert.Null(a.Druecken(Loesung(aufgabe), false));
            Assert.Equal(erwartet, t.Zeilen());
            Assert.Equal(i + 1, t.Anstoesse);
        }
        Assert.Equal(TrainerZustand.Beendet, a.Zustand);
        Assert.False(a.KnopfAktiv);
        Assert.Equal(RechnerTexte.Pruefen, a.Knopf);
        Assert.Equal("Runde beendet: 6 von 10 Aufgaben richtig.", a.AufgabeText);
        Assert.Equal("Starte eine neue Runde oder wähle eine andere Aufgabenart.", a.Rueckmeldung);
        Assert.Equal("", a.Zaehler);
        Assert.Null(a.Aufgabe);
        Assert.Equal("Binär/Hex/Dezimal · Mittel", a.Kopf);     // bleibt wie in Python stehen
        Assert.Equal("bisher 10 Aufgaben, 6 richtig", a.Statistik);
        // Zustand D: wie A
        Assert.Null(a.Druecken(Leer, false));
        Assert.Equal(10, t.Zeilen().Count);
        Assert.Equal(10, t.Anstoesse);
    }

    /// <summary>Zeilen 1 und 12: Statistik nur beim ersten Zeigen und am Rundenende, nicht nach jedem Pruefen, nicht beim Reiterwechsel.</summary>
    [Fact]
    public void StatistikNurBeimErstenZeigenUndAmRundenende()
    {
        using var t = new Aufbau(1);
        var a = t.Ablauf;
        Assert.Equal(0, a.StatistikLesungen);
        a.ErstesZeigen();
        Assert.Equal(1, a.StatistikLesungen);
        Assert.Equal("", a.Statistik);
        a.ErstesZeigen();
        a.ErstesZeigen();
        a.RundeStarten();
        a.Druecken(Loesung(a.Aufgabe!), false);
        a.ErstesZeigen();
        Assert.Equal(1, a.StatistikLesungen);
        Assert.Equal("", a.Statistik);
        for (var i = 0; i < 19; i++)
        {
            a.Druecken(a.Zustand == TrainerZustand.Offen ? Loesung(a.Aufgabe!) : Leer, false);
        }
        Assert.Equal(TrainerZustand.Beendet, a.Zustand);
        Assert.Equal(2, a.StatistikLesungen);
        Assert.Equal("bisher 10 Aufgaben, 10 richtig", a.Statistik);
        a.RundeStarten();
        a.ErstesZeigen();
        Assert.Equal(2, a.StatistikLesungen);
    }

    /// <summary>
    /// Zeile 9: Reiterwechsel (die Seite ruft nur ErstesZeigen) mitten in B und C schreibt nie,
    /// liest die Statistik nicht neu und laesst Runde, Aufgabe und Pruefung stehen. Blaettern
    /// (Bildlauf der Seite) erreicht den Ablauf gar nicht.
    /// </summary>
    [Fact]
    public void ReiterwechselSchreibtNie()
    {
        using var t = new Aufbau(8);
        var a = t.Ablauf;
        a.ErstesZeigen();
        a.RundeStarten();
        var text = a.AufgabeText;
        for (var i = 0; i < 3; i++)
        {
            a.ErstesZeigen();
        }
        Assert.Equal(TrainerZustand.Offen, a.Zustand);
        Assert.Equal(text, a.AufgabeText);
        Assert.Empty(t.Zeilen());
        var p = a.Druecken(Leer, false);
        for (var i = 0; i < 3; i++)
        {
            a.ErstesZeigen();
        }
        Assert.Equal(TrainerZustand.Geprueft, a.Zustand);
        Assert.Same(p, a.Pruefung);
        Assert.Single(t.Zeilen());
        Assert.Equal(1, t.Anstoesse);
        Assert.Equal(1, a.StatistikLesungen);
    }

    /// <summary>Zeilen 10 und 11: abgelehntes Speichern bleibt still (keine weitere Meldung), die Aufgabe zaehlt in der Runde, Anstoss trotzdem genau einmal.</summary>
    [Fact]
    public void AbgelehntesSpeichernBleibtStill()
    {
        using var t = new Aufbau(5, kaputt: true);
        var a = t.Ablauf;
        Assert.Equal("kaputt", t.Lernstand.NurLesenGrund);
        var meldungen = t.Meldungen;
        var vorher = File.ReadAllBytes(t.Pfad);
        a.ErstesZeigen();
        a.RundeStarten();
        var p = a.Druecken(Loesung(a.Aufgabe!), false)!;
        Assert.True(p.AllesRichtig);
        Assert.Equal([true], a.Ergebnisse);
        Assert.Equal(1, t.Anstoesse);
        Assert.Equal(meldungen, t.Meldungen);
        Assert.Equal(1, t.Lernstand.AbgelehnteSchreibzugriffe);
        for (var i = 0; i < 19; i++)
        {
            a.Druecken(a.Zustand == TrainerZustand.Offen ? Leer : Leer, false);
        }
        Assert.Equal("Runde beendet: 1 von 10 Aufgaben richtig.", a.AufgabeText);
        Assert.Equal(10, t.Anstoesse);
        Assert.Equal(meldungen, t.Meldungen);
        Assert.Equal("", a.Statistik);
        Assert.Equal(vorher, File.ReadAllBytes(t.Pfad));
    }

    /// <summary>
    /// Zeile 10: Datenbankfehler beim Schreiben (Tabelle fehlt): der Lernstand meldet je
    /// Fehlerart genau einmal (Weg zum Hinweis "Datenbankfehler"), die Aufgaben zaehlen trotzdem
    /// in der Runde, der Anstoss kommt je Pruefen genau einmal.
    /// </summary>
    [Fact]
    public void DatenbankfehlerBeimSchreibenMeldetEinmal()
    {
        using var t = new Aufbau(6);
        var a = t.Ablauf;
        a.ErstesZeigen();
        a.RundeStarten();
        a.Druecken(Loesung(a.Aufgabe!), false);
        Assert.Single(t.Zeilen());
        using (var db = VergleichsHilfe.Oeffnen(t.Pfad))
        {
            using var befehl = db.CreateCommand();
            befehl.CommandText = "DROP TABLE trainer_aufgaben";
            befehl.ExecuteNonQuery();
        }
        var meldungen = t.Meldungen;
        a.Druecken(Leer, false);
        a.Druecken(Loesung(a.Aufgabe!), false);
        a.Druecken(Leer, false);
        a.Druecken(Loesung(a.Aufgabe!), false);
        Assert.Equal([true, true, true], a.Ergebnisse);
        Assert.Equal(3, t.Anstoesse);
        Assert.Equal(meldungen + 1, t.Meldungen);
        Assert.Equal(TrainerZustand.Geprueft, a.Zustand);
    }

    /// <summary>
    /// Verdrahtung in der App (Quelltext, die App hat keine eigenen Tests): der Trainer-Reiter
    /// stoesst nach jedem Pruefen den Abgleich an (P3-12 F1 a), sperrt den Knopf nach
    /// KnopfAktiv (PC und Handy gleich) und liest beim Reiterwechsel nur ErstesZeigen.
    /// </summary>
    [Fact]
    public void AppVerdrahtung()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(System.IO.Path.Combine(ordner.FullName, "global.json")))
        {
            ordner = ordner.Parent;
        }
        Assert.NotNull(ordner);
        var views = System.IO.Path.Combine(ordner.FullName, "src", "Lernplattform.App", "Views");
        var teil = File.ReadAllText(System.IO.Path.Combine(views, "RechenTrainerTeil.cs"));
        Assert.Contains("_ablauf.NachSpeichern += AbgleichDienst.NachDemLernen;", teil, StringComparison.Ordinal);
        Assert.Contains("_knopf.IsEnabled = a?.KnopfAktiv ?? false;", teil, StringComparison.Ordinal);
        Assert.Contains("new RechenTrainerAblauf(null, () => 4711)", teil, StringComparison.Ordinal);    // Probe ohne Speichern
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(teil, @"new RechenTrainerAblauf\(Zustand\.Schreiber\)"));
        Assert.DoesNotContain("IsVisible = a?.KnopfAktiv", teil, StringComparison.Ordinal);
        var seite = File.ReadAllText(System.IO.Path.Combine(views, "RechnerSeite.cs"));
        Assert.Contains("_trainer.Gezeigt();", seite, StringComparison.Ordinal);
        Assert.DoesNotContain("Beschriftungen.Platzhalter", seite, StringComparison.Ordinal);
    }

    /// <summary>Ohne Schreiber (null): nichts gespeichert, Statistik leer, Ablauf gleich.</summary>
    [Fact]
    public void OhneSchreiber()
    {
        var a = new RechenTrainerAblauf(null, () => 3);
        var anstoesse = 0;
        a.NachSpeichern += () => anstoesse++;
        a.ErstesZeigen();
        a.RundeStarten();
        Assert.NotNull(a.Druecken(Leer, true));
        Assert.Equal(1, anstoesse);
        Assert.Equal("", a.Statistik);
    }

    /// <summary>
    /// Aufbau mitten in der Runde gewechselt: die Runde bleibt, der Rechenweg folgt beim naechsten
    /// Pruefen dem aktuellen Aufbau (schmal = steps_schmal oder IPv4 Oktett fuer Oktett).
    /// </summary>
    [Fact]
    public void AufbauwechselBehaeltDieRunde()
    {
        foreach (var art in new[] { "ipv4", "teilen", "bedarf" })
        {
            using var t = new Aufbau(99);
            var a = t.Ablauf;
            a.SetzeArt(art);
            a.SetzeStufe(3);
            a.RundeStarten();
            var breit = RechenTrainer.RundeBauen(art, 3, 99L);
            var schmal = RechenTrainer.RundeBauen(art, 3, 99L, schmal: true);
            var p1 = a.Druecken(Leer, false)!;
            Assert.Equal(string.Join("\n", breit[0].Schritte), p1.Rechenweg);
            a.Druecken(Leer, true);
            Assert.Equal(breit[1].Text, a.AufgabeText);
            var p2 = a.Druecken(Leer, true)!;
            Assert.Equal(string.Join("\n", schmal[1].HandySchritte), p2.Rechenweg);
            Assert.NotEqual(p1.Rechenweg.Split('\n').Max(z => z.Length), p2.Rechenweg.Split('\n').Max(z => z.Length));
            a.Druecken(Leer, false);
            var p3 = a.Druecken(Leer, false)!;
            Assert.Equal(string.Join("\n", breit[2].Schritte), p3.Rechenweg);
            Assert.Equal(3, t.Zeilen().Count);
        }
    }

    /// <summary>Breit und schmal gebaute Runden mit gleichem Startwert sind dieselben Aufgaben (nur der Rechenweg unterscheidet sich).</summary>
    [Fact]
    public void BreitUndSchmalGleicheAufgaben()
    {
        var a = new Abweichungen();
        var faelle = 0;
        foreach (var (art, _) in RechenTrainer.Arten)
        {
            foreach (var (stufe, _) in RechenTrainer.Stufen)
            {
                foreach (var startwert in new long[] { 0, 1, 42, 4711, 123456789, (1L << 30) - 1 })
                {
                    var breit = RechenTrainer.RundeBauen(art, stufe, startwert);
                    var schmal = RechenTrainer.RundeBauen(art, stufe, startwert, schmal: true);
                    for (var i = 0; i < breit.Count; i++)
                    {
                        faelle++;
                        var fall = $"{art}/{stufe}/{startwert}/{i}";
                        a.Gleich(breit[i].Text, schmal[i].Text, fall);
                        a.Gleich(string.Join("|", breit[i].Felder), string.Join("|", schmal[i].Felder), fall);
                        a.Gleich(string.Join("|", breit[i].Loesung.OrderBy(p => p.Key, StringComparer.Ordinal)),
                            string.Join("|", schmal[i].Loesung.OrderBy(p => p.Key, StringComparer.Ordinal)), fall);
                    }
                }
            }
        }
        a.Pruefen(1080);
        Assert.Equal(1080, faelle);
    }

    /// <summary>Texte von check() aus Python (G27 "pruefen"): auf PC und Handy dieselben, alle in RechnerTexte.</summary>
    [Fact]
    public void PruefTexteWiePython()
    {
        var pruefen = G27.GetProperty("pruefen");
        foreach (var geraet in new[] { "pc", "handy" })
        {
            var texte = pruefen.GetProperty(geraet).EnumerateArray().Select(e => e.GetString()!).ToList();
            foreach (var text in new[] { RechnerTexte.Richtig, RechnerTexte.RichtigWaere, RechnerTexte.AllesRichtig,
                         RechnerTexte.NochNichtGanz, RechnerTexte.Auswertung, RechnerTexte.NaechsteAufgabe })
            {
                Assert.Contains(text, texte);
            }
        }
    }

    /// <summary>R6-Nachtrag: Wortlaut FIT_HINT und die Regel "eine Zeile nach fit_lines zu lang" wie FitBox._refit (G27 "wischen").</summary>
    [Fact]
    public void WischHinweisWiePython()
    {
        var w = G27.GetProperty("wischen");
        var a = new Abweichungen();
        a.Gleich(w.GetProperty("hinweis").GetString(), RechnerTexte.Wischen, "FIT_HINT");
        var ja = 0;
        foreach (var f in w.GetProperty("faelle").EnumerateArray())
        {
            var text = f[0].GetString()!;
            var zeichen = f[1].GetInt32();
            var einheitlich = f[2].GetBoolean();
            var fall = $"{zeichen}/{einheitlich}/{text[..Math.Min(30, text.Length)]}";
            var gesetzt = Zeilen.Kasten(text, zeichen, einheitlich);
            a.Gleich(f[3].GetString(), gesetzt, fall);
            a.Wahr(f[4].GetBoolean() == Wischhinweis.Noetig(gesetzt, zeichen), fall);
            ja += f[4].GetBoolean() ? 1 : 0;
        }
        a.Pruefen(271);
        Assert.Equal(132, ja);
        Assert.False(Wischhinweis.Noetig("", 0));
        Assert.False(Wischhinweis.Noetig("abc\nde", 3));
        Assert.True(Wischhinweis.Noetig("abc\ndefg", 3));
    }
}
