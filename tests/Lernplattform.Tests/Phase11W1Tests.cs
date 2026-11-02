using System.Globalization;
using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Fortschritt;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Lernen;
using Lernplattform.Core.Mathe;
using Lernplattform.Core.Zufall;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;
using Microsoft.Data.Sqlite;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 11 W1 (Weisung P11-1): Kleinigkeiten aus der Sammelliste. 170 Fortschritt zeigt AE/DPA/DV-Klausuren mit
/// lesbarem Namen (SI wie Python), 171 Nummern der Szenarien und Testprojekte im Notizblock wie in der Liste,
/// 172 der Abschluss einer AE/DPA/DV-Klausur bucht die Aufgaben auf das Szenario dieser Fachrichtung (nicht auf
/// ein SI-Szenario), 180 Erinnerung ans Tagesziel wie reminder_due/reminder_text. Nur Testdaten (A3).
/// </summary>
public class Phase11W1Tests
{
    private static readonly string Lernen = Path.Combine(Pfade.Wurzel, "inhalte_csharp", "lernen");

    private static Lerninhalte Echt() => Lerninhalte.MitLernen(Lerninhalte.Laden(Pfade.Inhalte),
        name => Path.Combine(Lernen, name + ".json") is var pfad && File.Exists(pfad) ? File.ReadAllBytes(pfad) : null);

    private static readonly string[] Fachrichtungen = ["si", "ae", "dpa", "dv"];

    // -- 170 ------------------------------------------------------------------------------------

    [Fact]
    public void Fortschritt_KlausurDerFachrichtungMitLesbaremNamen_SiUnveraendert()
    {
        // SI (und ap1/wiso) genau wie Python EXAMS
        foreach (var (art, (name, kurz)) in FortschrittTexte.Pruefungen)
        {
            Assert.Equal((name, kurz), Auswertung.PruefungName(art));
        }
        // AE, DPA, DV: Name und Kurzname ihres Pruefungsbereichs
        foreach (var fr in Fachrichtung.Alle.Where(f => f.Id != "si"))
        {
            foreach (var bereich in fr.Bereiche.Where(b => b.Rolle is "b1" or "b2"))
            {
                Assert.Equal((bereich.Name, bereich.Kurz), Auswertung.PruefungName(bereich.Art));
                Assert.DoesNotContain(bereich.Art, bereich.Name, StringComparison.Ordinal);
            }
        }
        Assert.Null(Auswertung.PruefungName("projekt"));
        Assert.Null(Auswertung.PruefungName("gibtsnicht"));
        Assert.Null(Auswertung.PruefungName(null));
        Assert.Null(Auswertung.PruefungName(3L));

        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        _ = new Lernstand(pfad);
        using (var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False"))
        {
            verbindung.Open();
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = "INSERT INTO pruefungen (timestamp, art, punkte, note, dauer, daten) VALUES "
                + "('2026-11-02T10:00:00', 'konzeption', 50.0, 'x', 60, '{}'), "
                + "('2026-11-02T11:00:00', 'gibtsnicht', 50.0, 'x', 60, '{}'), "
                + "('2026-11-02T12:00:00', 'softwareprodukt', 40.0, 'x', 60, '{\"themen\": {\"Planung\": [2, 10]}}')";
            befehl.ExecuteNonQuery();
        }
        var a = Auswertung.Lesen(pfad);
        // neueste zuerst
        Assert.Equal(["AP2 · Planen eines Softwareproduktes", "gibtsnicht", "AP2 · Konzeption und Administration von IT-Systemen"],
            a.Pruefungen.Select(p => p.Name));
        Assert.Equal(["Softwareprodukt", null, "Konzeption"], a.Pruefungen.Select(p => p.Kurz));
        Assert.Equal("Üben (letzte Prüfung: Softwareprodukt, 02.11.2026)", a.Schwaeche!.Titel);
    }

    // -- 171 ------------------------------------------------------------------------------------

    [Fact]
    public void Notizblock_NummerWieInDerListe_JeFachrichtung()
    {
        var alle = Echt();
        foreach (var fr in Fachrichtungen)
        {
            var sicht = alle.Fuer(fr);
            var notiz = new Notizblock(sicht);
            foreach (var (quelle, liste) in new[] { (Quellen.Ap1, sicht.Ap1Szenarien), (Quellen.Ap2, sicht.Ap2Szenarien), (Quellen.Projekt, sicht.Testprojekte) })
            {
                Assert.NotEmpty(liste);
                var ablauf = new Szenarioablauf(quelle, liste, () => throw new InvalidOperationException(), null);
                for (var i = 0; i < liste.Count; i++)
                {
                    object schluessel = (long)(liste[i].Nummer - 1);
                    var eintrag = new NotizEintrag(quelle, schluessel, liste[i], liste[i].Titel ?? "", FragenStatus.Ueben, "", "");
                    // Liste der Seite: SzenarienTexte.Eintrag(Position) mit der Position in der Sicht
                    Assert.Equal(ablauf.Position(liste[i].Nummer - 1) + 1, notiz.Nummer(quelle, schluessel));
                    Assert.Equal(i + 1, notiz.Nummer(quelle, schluessel));
                    Assert.StartsWith((i + 1).ToString(CultureInfo.InvariantCulture) + ". ", NotizblockTexte.EintragTitel(eintrag, notiz), StringComparison.Ordinal);
                    Assert.StartsWith((i + 1).ToString(CultureInfo.InvariantCulture) + ".\u00A0", SzenarienTexte.Eintrag(i, liste[i].Titel ?? "", false), StringComparison.Ordinal);
                    if (fr == "si")
                    {
                        // SI wie Python (5452): Schluessel + 1
                        Assert.Equal((i + 1).ToString(CultureInfo.InvariantCulture) + ". " + eintrag.Titel, NotizblockTexte.EintragTitel(eintrag, notiz));
                        Assert.Equal(i, liste[i].Nummer - 1);
                    }
                }
            }
        }
        // AE beginnt bei den AP2-Szenarien wieder bei 1, obwohl die gespeicherte Stelle hinter SI liegt
        var ae = alle.Fuer("ae");
        var erstes = ae.Ap2Szenarien[0];
        Assert.True(erstes.Nummer > 1);
        Assert.Equal(1, new Notizblock(ae).Nummer(Quellen.Ap2, (long)(erstes.Nummer - 1)));
    }

    // -- 172 ------------------------------------------------------------------------------------

    [Fact]
    public void Abschluss_AufgabenDerFachrichtungTreffenIhrSzenario_SiUnveraendert()
    {
        var alle = Echt();
        var umbenennungen = Umbenennungen.Laden(Pfade.Inhalte);
        foreach (var fr in Fachrichtung.Alle)
        {
            var sicht = alle.Fuer(fr.Id);
            var inhalte = new KlausurInhalte(sicht, umbenennungen);
            foreach (var bereich in fr.Bereiche.Where(b => b.Rolle is "b1" or "b2"))
            {
                var zustand = Klausur.Neu(inhalte, bereich.Art, [], new PythonZufall(5), new DateTimeOffset(2026, 11, 2, 8, 0, 0, TimeSpan.Zero), SpeichernTests.Berlin);
                var ergebnis = Bewertung.Auswerten(inhalte, zustand);
                var zeilen = Abschluss.Antworten(inhalte, zustand, ergebnis).Cast<AufgabeAntwort>().ToList();
                var aufgaben = zustand["aufgaben"]!.AsArray();
                Assert.Equal(aufgaben.Count, zeilen.Count);
                var ablauf = new Szenarioablauf(Quellen.Ap2, sicht.Ap2Szenarien, () => throw new InvalidOperationException(), null);
                for (var n = 0; n < zeilen.Count; n++)
                {
                    var quelle = aufgaben[n]!["quelle"]!.GetValue<string>();
                    var index = aufgaben[n]!["index"]!.GetValue<int>();
                    var szenario = inhalte.Quelle(quelle)[index];
                    Assert.False(zeilen[n].Ap1);
                    // die gebuchte Stelle zeigt die Liste dieser Fachrichtung als genau dieses Szenario
                    var stelle = ablauf.Position(zeilen[n].Position);
                    Assert.True(stelle >= 0, fr.Id + " " + bereich.Art + " Aufgabe " + n);
                    Assert.Same(szenario, sicht.Ap2Szenarien[stelle]);
                    Assert.Equal(szenario.Titel, zeilen[n].Titel);
                    if (fr.Id == "si")
                    {
                        Assert.Equal(index, zeilen[n].Position);    // SI: wie Python log_scenario(task["index"])
                    }
                    else
                    {
                        Assert.Equal(szenario.Nummer - 1, zeilen[n].Position);
                        Assert.True(zeilen[n].Position >= alle.Ap2SzenarienFuer("si").Count);   // nie ein SI-Szenario
                    }
                }
            }
        }
    }

    // -- 180 Erinnerung -------------------------------------------------------------------------

    private static Einstellungen.Tagesziel Ziel(bool zielAn = true, int anzahl = 20, bool erinnerungAn = true, string zeit = "18:00") =>
        new(zielAn, anzahl, true, erinnerungAn, zeit);

    [Theory]
    // erledigt, Uhrzeit, gezeigt am, erwartet
    [InlineData(3, "18:00", "", true)]
    [InlineData(3, "17:59", "", false)]
    [InlineData(3, "23:59", "", true)]
    [InlineData(3, "18:30", "2026-11-02", false)]
    [InlineData(3, "18:30", "2026-11-01", true)]
    [InlineData(19, "18:30", "", true)]
    [InlineData(20, "18:30", "", false)]
    [InlineData(25, "18:30", "", false)]
    [InlineData(0, "00:00", "", false)]
    public void Erinnerung_FaelligWiePython(long erledigt, string uhrzeit, string gezeigt, bool erwartet)
    {
        var teile = uhrzeit.Split(':');
        var jetzt = new DateTime(2026, 11, 2, int.Parse(teile[0], CultureInfo.InvariantCulture), int.Parse(teile[1], CultureInfo.InvariantCulture), 30);
        Assert.Equal(erwartet, Erinnerung.Faellig(Ziel(), erledigt, jetzt, gezeigt));
    }

    [Fact]
    public void Erinnerung_NurMitZielUndErinnerung_TextWiePython()
    {
        var jetzt = new DateTime(2026, 11, 2, 20, 0, 0);
        Assert.False(Erinnerung.Faellig(Ziel(zielAn: false), 0, jetzt, ""));
        Assert.False(Erinnerung.Faellig(Ziel(erinnerungAn: false), 0, jetzt, ""));
        Assert.True(Erinnerung.Faellig(Ziel(zeit: "07:05"), 0, jetzt, ""));
        Assert.False(Erinnerung.Faellig(Ziel(zeit: "20:01"), 0, jetzt, ""));
        Assert.Equal("2026-11-02", Erinnerung.Tag(jetzt));
        // reminder_text (fisi_lernen 431), auch wenn mehr erledigt als das Ziel (max(0, ...))
        Assert.Equal("Dein Tagesziel ist noch offen: 3 von 20 Aufgaben, es fehlen noch 17.", Erinnerung.Text(3, 20));
        Assert.Equal("Dein Tagesziel ist noch offen: 30 von 20 Aufgaben, es fehlen noch 0.", Erinnerung.Text(30, 20));
        // settings.get("erinnerung_gezeigt", ""): nur ein Text zaehlt
        Assert.Equal("", Erinnerung.GezeigtAm(new JsonObject()));
        Assert.Equal("", Erinnerung.GezeigtAm(new JsonObject { [Erinnerung.GezeigtSchluessel] = 20261102 }));
        Assert.Equal("2026-11-02", Erinnerung.GezeigtAm(new JsonObject { [Erinnerung.GezeigtSchluessel] = "2026-11-02" }));
        Assert.Equal(9000, Erinnerung.Dauer);
        Assert.Equal(TimeSpan.FromMinutes(5), Erinnerung.PcAbstand);
        Assert.Equal(TimeSpan.FromSeconds(8), Erinnerung.StartNach);
    }

    [Fact]
    public void Erinnerung_ErledigtWieKachelHeute()
    {
        using var ordner = new TempOrdner();
        var pfad = Path.Combine(ordner.Pfad, Lernstand.DateiName);
        Assert.Null(Core.Dashboard.DashboardWerte.ErledigtAm(pfad, new DateOnly(2026, 11, 2)));
        Assert.False(File.Exists(pfad));
        _ = new Lernstand(pfad);
        using (var verbindung = new SqliteConnection($"Data Source={pfad};Pooling=False"))
        {
            verbindung.Open();
            using var befehl = verbindung.CreateCommand();
            befehl.CommandText = "INSERT INTO quiz_answers (timestamp, category, question, correct) VALUES "
                + "('2026-11-02T09:00:00', 'x', 'a', 1), ('2026-11-02T09:01:00', 'x', 'b', 0), ('2026-11-01T09:00:00', 'x', 'c', 1); "
                + "INSERT INTO card_events (timestamp, category, question, mode, correct) VALUES "
                + "('2026-11-02T10:00:00', 'x', 'k', 'lernen', 1), ('2026-11-02T10:01:00', 'x', 'l', 'lernen', NULL)";
            befehl.ExecuteNonQuery();
        }
        Assert.Equal(3, Core.Dashboard.DashboardWerte.ErledigtAm(pfad, new DateOnly(2026, 11, 2)));
        Assert.Equal(1, Core.Dashboard.DashboardWerte.ErledigtAm(pfad, new DateOnly(2026, 11, 1)));
        Assert.Equal(0, Core.Dashboard.DashboardWerte.ErledigtAm(pfad, new DateOnly(2026, 10, 31)));
    }
}
