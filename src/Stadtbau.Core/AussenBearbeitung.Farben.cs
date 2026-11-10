using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Sammelliste 264 (Weisung P12-130 Frage 3 = c): Farbe von Fassade, Dach und Rahmen eines Gebaeudes setzen. Daten
/// <c>stadtdaten.farben</c> <c>{Kennung: {fassade, dach, rahmen}}</c> (<see cref="Gebaeudefarben"/>); Kennung = Ort, sonst
/// Modellname des Gebaeudes ohne Ort (gilt dann fuer alle Gebaeude dieses Modells). Leere Eintraege und ein leerer
/// Abschnitt fallen weg, „Wie vorher“ nimmt den ganzen Eintrag weg.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Kennung eines Hauses fuer die Farben (null = kein Gebaeude).</summary>
    public static string? FarbKennung(PyObjekt aussen, AussenStelle? s) => s?.Art switch
    {
        AussenArt.Haus => s.Ort,
        AussenArt.Gebaeude => Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is PyObjekt g && g["modell"] is string m ? m : null,
        _ => null,
    };

    /// <summary>Gesetzte Farbe eines Teils (null = wie vorher).</summary>
    public static string? FarbeVon(PyObjekt aussen, string kennung, string teil) =>
        Get(Aussen.Stadtdaten(aussen), Gebaeudefarben.Schluessel) is PyObjekt alle && Get(alle, kennung) is PyObjekt e && Get(e, teil) is string w ? w : null;

    /// <summary>Hat das Gebaeude eigene Farben?</summary>
    public static bool HatFarben(PyObjekt aussen, string kennung) =>
        Get(Aussen.Stadtdaten(aussen), Gebaeudefarben.Schluessel) is PyObjekt alle && Py.Hat(alle, kennung);

    /// <summary>Farbe eines Teils setzen (<paramref name="wert"/> null = wie vorher). Ungueltiger Wert = nichts, false.</summary>
    public static bool FarbeSetzen(PyObjekt aussen, string kennung, string teil, string? wert)
    {
        if (!Gebaeudefarben.AlleSchluessel.Contains(teil) || (wert is not null && !Gebaeudefarben.WertGueltig(teil, wert)))
        {
            return false;
        }
        var sd = Aussen.Stadtdaten(aussen);
        if (Get(sd, Gebaeudefarben.Schluessel) is not PyObjekt alle)
        {
            if (wert is null)
            {
                return true;
            }
            alle = [];
            Py.Setzen(sd, Gebaeudefarben.Schluessel, alle);
        }
        if (Get(alle, kennung) is not PyObjekt e)
        {
            if (wert is null)
            {
                return true;
            }
            e = [];
            Py.Setzen(alle, kennung, e);
        }
        if (wert is null)
        {
            e.RemoveAll(x => x.Key == teil);
        }
        else
        {
            Py.Setzen(e, teil, teil == Gebaeudefarben.TuerSchluessel ? wert : wert.ToUpperInvariant());
        }
        Aufraeumen(sd, alle);
        return true;
    }

    /// <summary>Alle Farben eines Gebaeudes weg („Wie vorher“).</summary>
    public static void FarbenWeg(PyObjekt aussen, string kennung)
    {
        var sd = Aussen.Stadtdaten(aussen);
        if (Get(sd, Gebaeudefarben.Schluessel) is PyObjekt alle)
        {
            alle.RemoveAll(x => x.Key == kennung);
            Aufraeumen(sd, alle);
        }
    }

    /// <summary>Farben von <paramref name="alt"/> auf <paramref name="neu"/> uebertragen (Gebaeude umbenannt/kopiert); nichts, wenn keine.</summary>
    public static void FarbenUebernehmen(PyObjekt aussen, string alt, string neu)
    {
        if (Get(Aussen.Stadtdaten(aussen), Gebaeudefarben.Schluessel) is PyObjekt alle && Get(alle, alt) is PyObjekt e)
        {
            Py.Setzen(alle, neu, Py.Kopie(e));
        }
    }

    /// <summary>Farben von Gebaeuden, die es nicht mehr gibt (geloescht), weg.</summary>
    public static void FarbenAufraeumen(PyObjekt aussen)
    {
        var sd = Aussen.Stadtdaten(aussen);
        if (Get(sd, Gebaeudefarben.Schluessel) is not PyObjekt alle)
        {
            return;
        }
        var da = new HashSet<string>(StringComparer.Ordinal);
        foreach (var quelle in new[] { Get(Aussen.Karte(aussen), "orte"), Get(sd, "neue_orte") })
        {
            foreach (var o in (quelle as List<object?> ?? []).OfType<PyObjekt>())
            {
                da.Add(Py.Str(o["id"]));
            }
        }
        foreach (var g in Aussen.Landschaft(aussen).OfType<PyObjekt>().Where(t => Py.Gleich(t["typ"], "gebaeude")))
        {
            da.Add(Py.Str(g["modell"]));
        }
        if (alle.Any(x => !da.Contains(x.Key)))
        {
            alle.RemoveAll(x => !da.Contains(x.Key));
            Aufraeumen(sd, alle);
        }
    }

    private static void Aufraeumen(PyObjekt sd, PyObjekt alle)
    {
        alle.RemoveAll(x => x.Value is PyObjekt e && e.Count == 0);
        if (alle.Count == 0)
        {
            sd.RemoveAll(x => x.Key == Gebaeudefarben.Schluessel);
        }
    }

    /// <summary>Farbe als Wert fuer die Regler (Farbton, Saettigung, Helligkeit), aus <c>#RRGGBB</c>.</summary>
    public static (float Ton, float Saettigung, float Hell) Regler(string wert) => Gebaeudefarben.Hsv(Netz.Farbe(wert));

    /// <summary>Wert aus den Reglern.</summary>
    public static string AusReglern(float ton, float saettigung, float hell) => Gebaeudefarben.Wert(Gebaeudefarben.AusHsv(ton, saettigung, hell));
}
