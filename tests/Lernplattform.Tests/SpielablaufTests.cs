using System.Globalization;
using System.Reflection;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 7, W8: Sammelliste 86 und 87. Der schrittweise Weg von <c>Game._log</c> rechnet wie Python
/// (STATE_COPY = True) auf einer Kopie des Spielstands, und die Pruefung am Feierabend vergleicht wie
/// <c>GameState.differences</c> (Typ, Tupel/Liste, Zwischenspeicher je Thema).
/// </summary>
public class SpielablaufTests
{
    private static PyObjekt Inhalt => SpielInhaltTests.Inhalt;

    private static Spielablauf Neu()
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, uhr: () =>
        {
            zaehler += 7;
            return new DateTime(2026, 10, 1).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        });
        spiel.Log(Ereignisse.Profil, new PyObjekt { { "name", "Probe" } });
        return spiel;
    }

    private static PyObjekt Erledigt(string aufgabe, long geld = 60L) => new()
    {
        { "aufgabe", aufgabe }, { "tag", 1L }, { "richtig", true }, { "geld", geld },
    };

    /// <summary>86: ein vor der Aktion gemerkter Stand aendert sich nicht mit (Python: neues Objekt).</summary>
    [Fact]
    public void GemerkterStandBleibtUnveraendert()
    {
        var spiel = Neu();
        var vorher = spiel.Stand;
        var geld = vorher.Geld;
        var verlauf = vorher.Verlauf.Count;
        var geloest = vorher.Geloest.Count;

        spiel.Log(Ereignisse.Erledigt, Erledigt("probe-1"));

        Assert.NotSame(vorher, spiel.Stand);
        Assert.Equal(geld, vorher.Geld);
        Assert.Equal(verlauf, vorher.Verlauf.Count);
        Assert.Equal(geloest, vorher.Geloest.Count);
        Assert.Equal(geld + 60L, spiel.Stand.Geld);
        Assert.Equal(verlauf + 1, spiel.Stand.Verlauf.Count);
    }

    /// <summary>86: wirft ein Ereignis beim Anwenden (und beim Neuaufbau), bleibt der alte Stand ganz erhalten.</summary>
    [Fact]
    public void KaputtesEreignisLaesstKeinenHalbenStand()
    {
        var spiel = Neu();
        spiel.Log(Ereignisse.Erledigt, Erledigt("probe-1"));
        var vorher = spiel.Stand;
        var verlauf = vorher.Verlauf.Count;
        var geld = vorher.Geld;

        // "geld" ist kein int: _apply_firm setzt die Firma und scheitert dann an int("viel")
        Assert.Throws<FormatException>(() => spiel.Log(Ereignisse.Gegruendet, new PyObjekt { { "name", "Probe IT" }, { "geld", "viel" } }));

        Assert.Same(vorher, spiel.Stand);
        Assert.Null(vorher.Firma);
        Assert.Equal(verlauf, vorher.Verlauf.Count);
        Assert.Equal(geld, vorher.Geld);
    }

    /// <summary>
    /// Gegenpruefung W8 Fund 2: steht das kaputte Ereignis im Protokoll, wirft jede weitere Aktion wie
    /// Python (Stempel leer, voll neu gerechnet), statt still auf dem alten Stand weiterzurechnen.
    /// </summary>
    [Fact]
    public void NachKaputtemEreignisWirftJedeAktion()
    {
        var spiel = Neu();
        Assert.Throws<FormatException>(() => spiel.Log(Ereignisse.Gegruendet, new PyObjekt { { "name", "Probe IT" }, { "geld", "viel" } }));

        Assert.Throws<FormatException>(() => spiel.Log(Ereignisse.Erledigt, Erledigt("probe-2")));
        Assert.Throws<FormatException>(() => spiel.Log(Ereignisse.Erledigt, Erledigt("probe-3")));
    }

    /// <summary>86: die Kopie teilt mit dem Original nur die Inhalte, gemeinsame Objekte bleiben gemeinsam.</summary>
    [Fact]
    public void KopieTeiltNurDieInhalte()
    {
        var stand = new Spielstand(SpielFingerabdruckTests.Gespielt.Value.Speicher.Ereignisse(), Inhalt);
        stand.PersonalThemen(stand.Personal.Keys.First());
        var kopie = stand.Kopie();

        Assert.Same(stand.Inhalt, kopie.Inhalt);
        Assert.Empty(stand.Unterschiede(kopie));
        Assert.Equal(PythonJson.Text(PyAusgabe.Wert(stand.Felder())), PythonJson.Text(PyAusgabe.Wert(kopie.Felder())));
        var original = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Sammeln(stand, original);
        var geteilt = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var neu = new HashSet<object>(ReferenceEqualityComparer.Instance);
        Sammeln(kopie, neu);
        foreach (var o in neu)
        {
            if (original.Contains(o))
            {
                geteilt.Add(o);
            }
        }
        Assert.Empty(geteilt);
    }

    /// <summary>Alle veraenderbaren Objekte eines Stands (ohne die Inhalte), rekursiv.</summary>
    private static void Sammeln(Spielstand stand, HashSet<object> gesehen)
    {
        foreach (var feld in typeof(Spielstand).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (feld.Name != "<Inhalt>k__BackingField")
            {
                Sammeln(feld.GetValue(stand), gesehen);
            }
        }
    }

    private static void Sammeln(object? wert, HashSet<object> gesehen)
    {
        if (wert is null or string or ValueType || !gesehen.Add(wert))
        {
            return;
        }
        switch (wert)
        {
            case PyObjekt o:
                foreach (var (_, v) in o)
                {
                    Sammeln(v, gesehen);
                }
                break;
            case PyDict d:
                foreach (var (k, v) in d)
                {
                    Sammeln(k, gesehen);
                    Sammeln(v, gesehen);
                }
                break;
            case PyTupel t:
                foreach (var v in t.Werte)
                {
                    Sammeln(v, gesehen);
                }
                break;
            case SpielEreignis e:
                Sammeln(e.Daten, gesehen);
                break;
            case Dictionary<object, PyObjekt> b:
                foreach (var (k, v) in b)
                {
                    Sammeln(k, gesehen);
                    Sammeln(v, gesehen);
                }
                break;
            case System.Collections.IEnumerable l:
                foreach (var v in l)
                {
                    Sammeln(v, gesehen);
                }
                break;
        }
    }

    /// <summary>87: am Feierabend zaehlt ein Tupel statt einer Liste als Abweichung (JSON-Text waere gleich).</summary>
    [Fact]
    public void FeierabendErkenntTupelStattListe()
    {
        var spiel = Neu();
        var daten = Erledigt("probe-1");
        daten.Add("probe", new List<object?> { 1L, 2L });
        spiel.Log(Ereignisse.Erledigt, daten);
        var letzte = spiel.Stand.Verlauf[^1].Daten;
        var stelle = letzte.FindIndex(p => p.Key == "probe");
        letzte[stelle] = new KeyValuePair<string, object?>("probe", new PyTupel(1L, 2L));

        spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 140L }, { "rang", "Junior" } });

        Assert.Equal(1, spiel.Abweichungen);
        Assert.Equal(["history"], spiel.LetzteUnterschiede);
        // Es gilt der Neuaufbau: dort ist es wieder die Liste aus dem JSON-Text
        Assert.IsType<List<object?>>(spiel.Stand.Verlauf[^2].Daten["probe"]);
    }

    /// <summary>87: ohne Eingriff weicht der schrittweise Stand am Feierabend nicht ab.</summary>
    [Fact]
    public void FeierabendOhneEingriffGleich()
    {
        var spiel = Neu();
        spiel.Log(Ereignisse.Erledigt, Erledigt("probe-1"));
        var stand = spiel.Stand;
        spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 140L }, { "rang", "Junior" } });

        Assert.Equal(0, spiel.Abweichungen);
        Assert.Empty(spiel.LetzteUnterschiede);
        Assert.NotSame(stand, spiel.Stand);
    }

    /// <summary>87: ein falscher Wert im schrittweisen Stand wird erkannt, danach gilt der Neuaufbau.</summary>
    [Fact]
    public void FeierabendErkenntFalschenWert()
    {
        var spiel = Neu();
        spiel.Log(Ereignisse.Erledigt, Erledigt("probe-1"));
        spiel.Stand.Geld += 1;

        spiel.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", 1L }, { "gehalt", 140L }, { "rang", "Junior" } });

        Assert.Equal(1, spiel.Abweichungen);
        Assert.Equal(["balances", "money"], spiel.LetzteUnterschiede);
        Assert.Equal(new Spielstand(spiel.Speicher.Ereignisse(), Inhalt).Geld, spiel.Stand.Geld);
    }

    /// <summary>87: _same wie Python (Typ zaehlt, 1 ist nicht 1.0, Tupel keine Liste, Reihenfolge der Schluessel zaehlt).</summary>
    [Fact]
    public void GleichWiePython()
    {
        Assert.True(Spielstand.Gleich(1L, 1));
        Assert.False(Spielstand.Gleich(1L, 1.0));
        Assert.False(Spielstand.Gleich(true, 1L));
        Assert.False(Spielstand.Gleich(new PyTupel(1L), new List<object?> { 1L }));
        Assert.True(Spielstand.Gleich(new PyTupel(1L, "a"), new PyTupel(1L, "a")));
        Assert.False(Spielstand.Gleich(new List<object?> { 1L }, new List<object?> { 1.0 }));
        Assert.True(Spielstand.Gleich(new PyObjekt { { "a", 1L }, { "b", 2L } }, new PyObjekt { { "a", 1L }, { "b", 2L } }));
        Assert.False(Spielstand.Gleich(new PyObjekt { { "a", 1L }, { "b", 2L } }, new PyObjekt { { "b", 2L }, { "a", 1L } }));
        Assert.False(Spielstand.Gleich(new PyObjekt { { "a", 1L } }, new PyObjekt { { "a", 1.0 } }));
        Assert.True(Spielstand.Gleich(new PySet([1L, 2L]), new PySet([2L, 1L])));
        Assert.True(Spielstand.Gleich(null, null));
        Assert.False(Spielstand.Gleich(null, 0L));
        Assert.False(Spielstand.Gleich(double.NaN, double.NaN));
        var a = new PyDict { [1L] = "x" };
        var b = new PyDict { [1.0] = "x" };
        // Schluessel vergleicht Python mit == (1 == 1.0)
        Assert.True(Spielstand.Gleich(a, b));
    }

    /// <summary>87: gemeinsame Eintraege des Zwischenspeichers je Thema (_topic_base) muessen gleich sein.</summary>
    [Fact]
    public void ZwischenspeicherJeThemaZaehlt()
    {
        var spiel = SpielFingerabdruckTests.Gespielt.Value;
        var a = new Spielstand(spiel.Speicher.Ereignisse(), Inhalt);
        var b = new Spielstand(spiel.Speicher.Ereignisse(), Inhalt);
        var person = a.Personal.Keys.First();
        a.PersonalThemen(person);
        Assert.Empty(a.Unterschiede(b));
        b.PersonalThemen(person);
        Assert.Empty(a.Unterschiede(b));

        var basis = (Dictionary<object, PyObjekt>)typeof(Spielstand)
            .GetField("themenBasis", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(b)!;
        var werte = basis[person!];
        werte[0] = new KeyValuePair<string, object?>(werte[0].Key, Py.Plus(werte[0].Value, 1L));
        Assert.Equal(["_topic_base"], a.Unterschiede(b));
        // Nur in einem Stand vorhandene Eintraege zaehlen nicht
        basis.Remove(person!);
        Assert.Empty(a.Unterschiede(b));
    }
}
