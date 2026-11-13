using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Tests;

/// <summary>
/// W4b (Weisung P12-228, Nico 13.11.2026): Stadt 1 von Nico ist Fassung 1 (<c>stadt1_fassung1.fistadt</c>, Herkunft
/// <c>fi-fehlerberichte</c> main 2c9be3e <c>stadtbau/2026-11-13_064738_PC_S0.47.0.fistadt</c>, Kopie in
/// <c>tests/Stadtbau.Tests/Testdaten/nico/stadt1_S0.47.0.fistadt</c>). Gegenueber Nicos Datei sind nur der Kopf (Stadt-Kennung
/// stadt1, Fassung 1, Grundlage 0) und die Uhr (Nico 2a: 1 Minute je Schritt) anders. Fassung 0, die Fingerabdruecke und
/// alte Spielstaende bleiben (Gegenprobe Fassung 0 = 2 Minuten, alte Zeilen behalten ihre Minuten).
/// </summary>
public sealed class StadtbauFassung1Tests
{
    private static byte[]? Lesen(string pfad)
    {
        var datei = Path.Combine(Pfade.Wurzel, pfad);
        return File.Exists(datei) ? File.ReadAllBytes(datei) : null;
    }

    private static readonly Lazy<StadtFassung> Fassung1 = new(() => AktiveFassung.LadenNummer(Lesen, 1));

    private static StadtFassung Fassung0 => StadtbauFassungTests.Fassung0.Value;

    private static PyObjekt Roh(string pfad) => (PyObjekt)Py.Lesen(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, pfad)))!;

    [Fact]
    public void Aktiv_IstFassung1_MitKopfStadt1()
    {
        var f = AktiveFassung.Laden(Lesen);
        Assert.Equal(1, f.Nummer);
        Assert.Equal(AktiveFassung.Stadt1, f.StadtId);
        Assert.Empty(StadtbauPruefung.Format(Py.Lesen(File.ReadAllBytes(Path.Combine(Pfade.Wurzel, AktiveFassung.Ordner + "stadt1_fassung1.fistadt")))));
    }

    [Fact]
    public void Fassung1_GleichNicosDatei_BisAufKopfUndUhr()
    {
        var eigen = Roh(AktiveFassung.Ordner + "stadt1_fassung1.fistadt");
        var nico = Roh("tests/Stadtbau.Tests/Testdaten/nico/stadt1_S0.47.0.fistadt");
        Assert.Equal(nico.Select(p => p.Key), eigen.Select(p => p.Key));
        foreach (var (feld, wert) in nico)
        {
            if (feld is "stadt" or "pruefsumme" or "stadtdaten")
            {
                continue;
            }
            Assert.True(Py.Gleich(wert, eigen[feld]), feld);
        }
        var sNico = (PyObjekt)nico["stadtdaten"]!;
        var sEigen = (PyObjekt)eigen["stadtdaten"]!;
        Assert.Equal(sNico.Select(p => p.Key), sEigen.Select(p => p.Key));
        foreach (var (feld, wert) in sNico.Where(p => p.Key != "uhr"))
        {
            Assert.True(Py.Gleich(wert, sEigen[feld]), "stadtdaten." + feld);
        }
        var uNico = (PyObjekt)sNico["uhr"]!;
        var uEigen = (PyObjekt)sEigen["uhr"]!;
        Assert.Equal(2L, Py.Int(uNico["minuten_je_schritt"]));
        Assert.Equal(1L, Py.Int(uEigen["minuten_je_schritt"]));
        foreach (var (feld, wert) in uNico.Where(p => p.Key != "minuten_je_schritt"))
        {
            Assert.True(Py.Gleich(wert, uEigen[feld]), "uhr." + feld);
        }
        var kopf = (PyObjekt)eigen["stadt"]!;
        Assert.Equal("stadt1", kopf["id"]);
        Assert.Equal(1L, Py.Int(kopf["fassung"]));
        Assert.Equal(0L, Py.Int(kopf["basis"]));
    }

    [Fact]
    public void Fassung1_LaedtOhneFund_VerbautLeer()
    {
        Assert.Empty(StadtbauPruefung.Inhalt(Fassung1.Value, SpielInhaltTests.Inhalt));
        Assert.Empty(StadtbauPruefung.Verbaut(Fassung1.Value, null));
    }

    private static Stadt StadtAus(StadtFassung f) => f.StadtFuer(f.MitKarte(SpielInhaltTests.Inhalt));

    /// <summary>1 Minute je Schritt in Fassung 1; Gegenprobe Fassung 0 mit 2 Minuten (unveraendert).</summary>
    [Fact]
    public void Fassung1_EinSchrittEineMinute_GegenprobeFassung0ZweiMinuten()
    {
        Assert.Equal(1, StadtAus(Fassung1.Value).MinutenJeSchritt);
        Assert.Equal(2, StadtAus(Fassung0).MinutenJeSchritt);
    }

    /// <summary>
    /// Alte Spielstaende: ein Weg, geschrieben mit Fassung 0 (2 Minuten je Schritt), behaelt in Fassung 1 seine Minuten
    /// (die Zeile traegt sie); erst ein neuer Weg kostet 1 Minute je Schritt.
    /// </summary>
    [Fact]
    public void AlterWeg_BehaeltSeineMinuten_NeuerWegEineMinuteJeSchritt()
    {
        var alt = StadtAus(Fassung0);
        var ablauf = SpielQuestTests.AllesErledigt(alt.Inhalt);
        var start = alt.Stand(ablauf.Stand);
        Assert.Equal(StadtGrund.Ok, alt.Gehen(ablauf, "amt").Grund);
        var nachAlt = alt.Stand(ablauf.Stand);
        var schritteAlt = Py.Int(((PyObjekt)ablauf.Stand.Verlauf[^1].Daten)["schritte"]);
        Assert.Equal(start.Uhr + 2 * schritteAlt, nachAlt.Uhr);

        var neu = StadtAus(Fassung1.Value);
        ablauf.Neuaufbau();
        Assert.Equal(nachAlt, neu.Stand(ablauf.Stand));
        var ziel = SpielDaten.Liste(SpielDaten.Objekt(neu.Inhalt["orte"])["orte"]).Select(SpielDaten.Objekt).Select(o => (string)o["id"]!)
            .First(id => id != "amt" && neu.Weg("amt", id) is not null);
        Assert.Equal(StadtGrund.Ok, neu.Gehen(ablauf, ziel).Grund);
        var schritteNeu = Py.Int(((PyObjekt)ablauf.Stand.Verlauf[^1].Daten)["schritte"]);
        Assert.Equal(nachAlt.Uhr + schritteNeu, neu.Stand(ablauf.Stand).Uhr);
    }
}
