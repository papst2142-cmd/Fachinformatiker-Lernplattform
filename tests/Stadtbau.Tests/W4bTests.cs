using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;

namespace Stadtbau.Tests;

/// <summary>
/// W4b (Weisung P12-228): Nicos Stadt 1 ist im Spiel Fassung 1; die Grundlage des Werkzeugs bleibt Fassung 0 (alte Projekte
/// bleiben lesbar, P12-20 Abschnitt 5). Nico Frage 2 = a: jede neue Stadt geht 1 Minute je Schritt (leere Stadt, Stadt-Datei
/// als neues Projekt), die Laufzeiten rechnen mit dem Wert der Stadt. Gegenprobe
/// <see cref="Aussen.UhrAltFuerTests"/>: Uhr der Vorlage (2 Minuten) wie bis S 0.47.0.
/// </summary>
public class W4bTests
{
    private static Spieldaten D => Daten.Spiel;

    private const string NicoDatei = "tests/Stadtbau.Tests/Testdaten/nico/stadt1_S0.47.0.fistadt";

    private static long Minuten(Projekt p) =>
        Py.Int(((PyObjekt)Aussen.Stadtdaten(p.AussenStand)["uhr"]!)["minuten_je_schritt"]);

    [Fact]
    public void Grundlage_BleibtFassung0_ObwohlDasSpielFassung1Zeigt()
    {
        Assert.Equal(0, D.Fassung.Nummer);
        Assert.Equal(Spieldaten.BasisFassung, D.Fassung.Nummer);
        Assert.Equal(1, AktiveFassung.Laden(Daten.Lesen).Nummer);
        Assert.Equal(2, Py.Int(((PyObjekt)D.Fassung.Stadtdaten["uhr"]!)["minuten_je_schritt"]));
    }

    [Fact]
    public void NeueStaedte_EineMinuteJeSchritt_GegenprobeAlt()
    {
        Assert.Equal(1, Minuten(Projekt.Neu("Leer", D, Daten.NeueAblage(), leer: (40, 32))));
        // ein neues Projekt „Stadt 1“ baut auf Fassung 0 (alte Stadt, Grundlage des Werkzeugs) und bleibt bei ihrer Uhr
        Assert.Equal(2, Minuten(Projekt.Neu("Stadt", D, Daten.NeueAblage())));
        Assert.Equal(1, Minuten(Projekt.AusStadtDatei(Daten.Lesen(NicoDatei)!, D, Daten.NeueAblage(), "Nico")));
        Aussen.UhrAltFuerTests = true;
        try
        {
            Assert.Equal(2, Minuten(Projekt.Neu("Leer", D, Daten.NeueAblage(), leer: (40, 32))));
            Assert.Equal(2, Minuten(Projekt.AusStadtDatei(Daten.Lesen(NicoDatei)!, D, Daten.NeueAblage(), "Nico")));
        }
        finally
        {
            Aussen.UhrAltFuerTests = false;
        }
    }

    [Fact]
    public void EineMinute_BleibtNachSpeichernUndOeffnen_UndStehtInDerStadtDatei()
    {
        var ablage = Daten.NeueAblage();
        var p = Projekt.Neu("Leer", D, ablage, leer: (40, 32));
        var wieder = Projekt.Oeffnen(p.Datei, D, ablage);
        Assert.Equal(1, Minuten(wieder));
        Assert.Equal(1, Py.Int(((PyObjekt)wieder.Stand().Stadtdaten["uhr"]!)["minuten_je_schritt"]));
    }

    /// <summary>
    /// Nicos Stadt 1: jede Laufzeit ist mit 1 Minute je Schritt genau halb so lang wie mit 2 (gleiche Wege). Die Zahlen
    /// (Wege ab 60 Minuten, laengster Weg) stehen in der Meldung P12-130.
    /// </summary>
    [Fact]
    public void NicosStadt1_LaufzeitenHalb()
    {
        var neu = Laufzeiten.Rechnen(Projekt.AusStadtDatei(Daten.Lesen(NicoDatei)!, D, Daten.NeueAblage(), "Nico").Stand(), D.Inhalt, D.Grafik);
        List<Laufzeiten.Weg> alt;
        Aussen.UhrAltFuerTests = true;
        try
        {
            alt = Laufzeiten.Rechnen(Projekt.AusStadtDatei(Daten.Lesen(NicoDatei)!, D, Daten.NeueAblage(), "Nico").Stand(), D.Inhalt, D.Grafik);
        }
        finally
        {
            Aussen.UhrAltFuerTests = false;
        }
        Assert.Equal(alt.Count, neu.Count);
        var altJe = alt.ToDictionary(w => (w.Von, w.Nach));
        foreach (var w in neu)
        {
            Assert.Equal(altJe[(w.Von, w.Nach)].Minuten, w.Minuten * 2);
        }
        string Angabe(List<Laufzeiten.Weg> wege) =>
            $"{wege.Count} Wege, ab 60 Min. zu Fuss {wege.Count(w => w.Minuten >= Laufzeiten.GelbAb)}, mit Bus kuerzer ab 60 {wege.Count(w => w.Kuerzer >= Laufzeiten.GelbAb)}, " +
            $"laengster {wege.MaxBy(w => w.Minuten)?.VonName} -> {wege.MaxBy(w => w.Minuten)?.NachName} {wege.Max(w => w.Minuten)} Min.";
        TestContext.Current.SendDiagnosticMessage("2 Min.: " + Angabe(alt) + "\n1 Min.: " + Angabe(neu));
    }
}
