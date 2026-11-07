using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>Arten der Stuecke draussen (Plan Abschnitt 3).</summary>
public enum AussenArt
{
    Strasse,
    Haus,
    Gebaeude,
    Baum,
    Park,
    Beet,
    Laterne,
    Paketplatz,

    /// <summary>Phase 13 Welle 3b: Stueck aus dem vollen CC0-Katalog (Modell oder Effekt), in <c>stadtdaten.katalog</c>.</summary>
    Katalog,

    /// <summary>
    /// Phase 13 Welle 4c (Weisung P12-32): nur zum Setzen eines Blueprints (<see cref="Blueprint.Setzen"/>); das gesetzte
    /// Stueck ist danach ein <see cref="Gebaeude"/>, keine Stelle hat diese Art.
    /// </summary>
    Blueprint,

    /// <summary>
    /// Welle K1 (S 0.10.0, Weisung P12-55): nur zum Setzen einer Kurve (Kennung <see cref="Lernplattform.Core.Stadtbau.Strassenkurve.Kennung"/>
    /// als Modell); das gesetzte Stueck ist danach eine <see cref="Strasse"/> mit dem Feld <c>kurve</c>.
    /// </summary>
    Kurve,

    /// <summary>
    /// Welle K3 (S 0.11.0, Weisung P12-59): nur zum Setzen eines Kreisverkehrs (Kennung <see cref="AussenBearbeitung.Kennung(double)"/>
    /// als Modell); der gesetzte Ring ist danach eine <see cref="Strasse"/> mit dem Feld <c>kreisverkehr</c>.
    /// </summary>
    Kreisverkehr,

    /// <summary>S 0.8.0 (Weisung P12-47): Fluss in <c>karte.welt.landschaft</c> (Typ <c>fluss</c>, Punkte wie eine Strasse, Breite fest).</summary>
    Fluss,

    /// <summary>S 0.8.0: Teich in <c>stadtdaten.wasser.teiche</c>; <see cref="AussenStelle.Punkt"/> = Eckpunkt (-1 = ganzer Teich).</summary>
    Teich,

    /// <summary>S 0.8.0: Steg in <c>stadtdaten.wasser.stege</c>.</summary>
    Steg,

    /// <summary>
    /// Welle Z (S 0.14.0, Weisung P12-71): nur zum Ziehen von Punkt zu Punkt (Kennung <see cref="AussenBearbeitung.ZiehKennung"/>
    /// als Modell: Strasse, Fussweg, Fluss); das Gezogene ist danach eine <see cref="Strasse"/>, ein <see cref="Fussweg"/> oder
    /// ein <see cref="Fluss"/>.
    /// </summary>
    Ziehen,

    /// <summary>Welle Z: Fussweg in <c>stadtdaten.fusswege</c> (Linienzug mit runden Ecken, nur zum Gehen).</summary>
    Fussweg,
}

/// <summary>
/// Ein Stueck draussen: Strasse, Park, Gebaeude und Baum ueber die Stelle in <c>karte.welt.landschaft</c> (Baum zusaetzlich
/// der Punkt in seiner Liste), Haus = Ort des Spiels ueber seine Kennung, Beet und Laterne ueber die Stelle in ihrer Liste,
/// der Paketplatz gibt es einmal.
/// </summary>
public sealed record AussenStelle(AussenArt Art, int Nummer, int Punkt = 0, string Ort = "");

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-18, Plan Abschnitte 3 und 5): die Stadt draussen als ein Objekt mit den Abschnitten
/// <c>karte</c>, <c>stadtdaten</c>, <c>laternen</c> und <c>beete</c> der Fassung (Aufbau wie in <see cref="StadtFassung"/>).
/// Bearbeitet wird immer eine Kopie; die Basis-Fassung bleibt unberuehrt. Fuer den Verlauf zerfaellt das Objekt in
/// <see cref="Teile"/> (jeder Schluessel von Karte und Stadtdaten fuer sich), damit ein Schritt nur das Geaenderte haelt.
/// </summary>
public static class Aussen
{
    public static readonly string[] Abschnitte = ["karte", "stadtdaten", "laternen", "beete"];

    /// <summary>
    /// Kopie der Stadt draussen aus einer Fassung. Seit Welle 4c (Weisung P12-32, Meldung P12-23 Frage 1 a) dazu der Teil
    /// <c>bauten</c>, nur im Werkzeug: Innenraeume gesetzter Blueprints je Modell { name, grundriss, personen }. Er kommt
    /// nicht in die Fassung (<see cref="Fassung"/>), das Spiel kennt ihn noch nicht.
    /// </summary>
    public static PyObjekt Aus(StadtFassung f) => new()
    {
        { "karte", Py.Kopie(f.Karte) },
        { "stadtdaten", Py.Kopie(f.Stadtdaten) },
        { "laternen", f.Laternen.Select(l => (object?)new List<object?> { Zahl(l.X), Zahl(l.Z), l.Doppelt }).ToList() },
        { "beete", f.Beete.Select(b => (object?)new List<object?> { Zahl(b.X), Zahl(b.Y) }).ToList() },
        { "bauten", new PyObjekt() },
    };

    /// <summary>Innenraeume gesetzter Blueprints (nur Werkzeug), je Modell { name, grundriss, personen }.</summary>
    public static PyObjekt Bauten(PyObjekt aussen)
    {
        if (Get(aussen, "bauten") is not PyObjekt b)
        {
            b = new PyObjekt();
            Py.Setzen(aussen, "bauten", b);
        }
        return b;
    }

    /// <summary>float als kuerzeste Dezimalzahl (wie <see cref="StadtFassung"/> beim Schreiben).</summary>
    public static double Zahl(float f) => double.Parse(f.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public static PyObjekt Karte(PyObjekt aussen) => Objekt(aussen["karte"]);

    public static PyObjekt Stadtdaten(PyObjekt aussen) => Objekt(aussen["stadtdaten"]);

    public static PyObjekt Welt(PyObjekt aussen) => Objekt(Karte(aussen)["welt"]);

    public static List<object?> Landschaft(PyObjekt aussen)
    {
        var welt = Welt(aussen);
        if (welt["landschaft"] is not List<object?> l)
        {
            l = [];
            Py.Setzen(welt, "landschaft", l);
        }
        return l;
    }

    public static List<object?> Laternen(PyObjekt aussen) => Liste(aussen["laternen"]);

    /// <summary>
    /// Phase 13 Welle 3b (Weisung P12-19): Stuecke aus dem vollen CC0-Katalog draussen, Liste <c>stadtdaten.katalog</c> mit
    /// je {id "paket/name", x, y (Mitte), w, t, dreh (Vierteldrehungen)}. Das Spiel liest den Schluessel noch nicht (Einbau
    /// ab Welle 4); ohne Stuecke fehlt er (leere Liste beim Lesen, angelegt erst beim ersten Setzen).
    /// </summary>
    public static List<object?> KatalogStuecke(PyObjekt aussen) => Get(Stadtdaten(aussen), "katalog") as List<object?> ?? [];

    /// <summary>Liste der Katalog-Stuecke draussen, bei Bedarf angelegt.</summary>
    public static List<object?> KatalogStueckeAnlegen(PyObjekt aussen)
    {
        if (Get(Stadtdaten(aussen), "katalog") is not List<object?> l)
        {
            l = [];
            Py.Setzen(Stadtdaten(aussen), "katalog", l);
        }
        return l;
    }

    public static List<object?> Beete(PyObjekt aussen) => Liste(aussen["beete"]);

    /// <summary>Welle Z (Weisung P12-71): Fusswege in <c>stadtdaten.fusswege</c> (leer, wenn es keine gibt).</summary>
    public static List<object?> Fusswege(PyObjekt aussen) =>
        Get(Stadtdaten(aussen), Lernplattform.Core.Spiel.Probe.Fusswege.Abschnitt) as List<object?> ?? [];

    /// <summary>Welle Z: Liste der Fusswege, bei Bedarf angelegt (erst beim ersten Ziehen).</summary>
    public static List<object?> FusswegeAnlegen(PyObjekt aussen)
    {
        if (Get(Stadtdaten(aussen), Lernplattform.Core.Spiel.Probe.Fusswege.Abschnitt) is not List<object?> l)
        {
            l = [];
            Py.Setzen(Stadtdaten(aussen), Lernplattform.Core.Spiel.Probe.Fusswege.Abschnitt, l);
        }
        return l;
    }

    /// <summary>S 0.8.0 (Weisung P12-47): Teiche in <c>stadtdaten.wasser.teiche</c> (leer, wenn es kein Wasser gibt).</summary>
    public static List<object?> Teiche(PyObjekt aussen) =>
        Get(Stadtdaten(aussen), Lernplattform.Core.Spiel.Probe.Wasser.Abschnitt) is PyObjekt w && Get(w, "teiche") is List<object?> l ? l : [];

    /// <summary>S 0.8.0: Stege in <c>stadtdaten.wasser.stege</c>.</summary>
    public static List<object?> Stege(PyObjekt aussen) =>
        Get(Stadtdaten(aussen), Lernplattform.Core.Spiel.Probe.Wasser.Abschnitt) is PyObjekt w && Get(w, "stege") is List<object?> l ? l : [];

    /// <summary>S 0.8.0: der Teil <c>stadtdaten.wasser</c> mit den Listen teiche und stege, bei Bedarf angelegt (erst beim ersten Setzen).</summary>
    public static PyObjekt WasserAnlegen(PyObjekt aussen)
    {
        if (Get(Stadtdaten(aussen), Lernplattform.Core.Spiel.Probe.Wasser.Abschnitt) is not PyObjekt w)
        {
            w = new PyObjekt();
            Py.Setzen(Stadtdaten(aussen), Lernplattform.Core.Spiel.Probe.Wasser.Abschnitt, w);
        }
        foreach (var k in (string[])["teiche", "stege"])
        {
            if (Get(w, k) is not List<object?>)
            {
                Py.Setzen(w, k, new List<object?>());
            }
        }
        return w;
    }

    /// <summary>S 0.8.0 (Frage 6 = a): Schalter „Eis im Winter“ (<c>stadtdaten.wasser.eis</c>).</summary>
    public static bool Eis(PyObjekt aussen) => Lernplattform.Core.Spiel.Probe.Wasser.EisAn(Stadtdaten(aussen));

    public static (double Breite, double Hoehe) Groesse(PyObjekt aussen) => (Py.F(Welt(aussen)["breite"]), Py.F(Welt(aussen)["hoehe"]));

    /// <summary>Laternen als Werte der Fassung.</summary>
    public static List<(float X, float Z, bool Doppelt)> LaternenWerte(PyObjekt aussen) =>
        [.. Laternen(aussen).OfType<List<object?>>().Select(p => ((float)Py.F(p[0]), (float)Py.F(p[1]), p.Count > 2 && p[2] is true))];

    public static List<Vector2> BeeteWerte(PyObjekt aussen) =>
        [.. Beete(aussen).OfType<List<object?>>().Select(p => new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])))];

    /// <summary>Fassung mit dieser Stadt draussen und den Angaben der Basis (fuer Pruefung, 3D und Export).</summary>
    public static StadtFassung Fassung(PyObjekt aussen, StadtFassung basis, PyObjekt innenraeume, long nummer, long? basisNummer, string werkzeug, string erstellt, string nachricht) => new()
    {
        StadtId = basis.StadtId,
        Nummer = nummer,
        Basis = basisNummer,
        Werkzeug = werkzeug,
        Erstellt = erstellt,
        Nachricht = nachricht,
        Karte = Karte(aussen),
        Stadtdaten = Stadtdaten(aussen),
        Innenraeume = innenraeume,
        Laternen = LaternenWerte(aussen),
        Beete = BeeteWerte(aussen),
    };

    /// <summary>Die Teile fuer den Verlauf: "karte/&lt;Schluessel&gt;", "stadtdaten/&lt;Schluessel&gt;", "laternen", "beete".</summary>
    public static IEnumerable<(string Pfad, object? Wert)> Teile(PyObjekt aussen)
    {
        foreach (var abschnitt in new[] { "karte", "stadtdaten" })
        {
            foreach (var (k, v) in Objekt(aussen[abschnitt]))
            {
                yield return (abschnitt + "/" + k, v);
            }
        }
        yield return ("laternen", aussen["laternen"]);
        yield return ("beete", aussen["beete"]);
        yield return ("bauten", Get(aussen, "bauten") ?? new PyObjekt());
    }

    /// <summary>Teile als Text (json.dumps), fuer den Vergleich vorher/nachher.</summary>
    public static Dictionary<string, string> TeileText(PyObjekt aussen) =>
        Teile(aussen).ToDictionary(t => t.Pfad, t => PythonJson.Standard(t.Wert), StringComparer.Ordinal);

    /// <summary>Teil setzen (fehlt der Wert: Schluessel weg).</summary>
    public static void TeilSetzen(PyObjekt aussen, string pfad, object? wert, bool vorhanden)
    {
        var teile = pfad.Split('/', 2);
        if (teile.Length == 1)
        {
            Py.Setzen(aussen, pfad, wert);
            return;
        }
        var ziel = Objekt(aussen[teile[0]]);
        if (vorhanden)
        {
            Py.Setzen(ziel, teile[1], wert);
        }
        else
        {
            var rest = ziel.Where(p => p.Key != teile[1]).ToList();
            ziel.Clear();
            ziel.AddRange(rest);
        }
    }

    /// <summary>
    /// Leere Stadt (Plan Abschnitt 3 "Leere Stadt", Vorbereitung fuer Beta 1): Karte mit frei waehlbarer Groesse ohne Orte,
    /// Raster und Uhr wie Stadt 1, die Haus-Modelle von Stadt 1 zum Setzen von Haeusern, keine Laternen, keine Beete.
    /// Patch S 0.5.1 (Weisung P12-39): frei = Schalter darstellung.kleingruen = false (nur der Boden, kein Kleingruen an
    /// Strassen und auf freien Flaechen; Parks, die Nico setzt, behalten Baeume und Bueschel).
    /// </summary>
    public static StadtFassung Leer(StadtFassung vorlage, string stadtId, double breite, double hoehe, bool frei = false)
    {
        var sd = (PyObjekt)Py.Kopie(vorlage.Stadtdaten)!;
        var rest = new PyObjekt();
        foreach (var (k, v) in sd)
        {
            if (k.StartsWith('_'))
            {
                continue;
            }
            rest.Add(k, k switch
            {
                "zugaenge" or "oeffnungszeiten" or "zugaenge_neu" => new PyObjekt(),
                "neue_orte" => new List<object?>(),
                "darstellung" => frei
                    ? new PyObjekt { { "modell_tuer", new PyObjekt() }, { "zierwege", new List<object?>() }, { "paket_platz", Get(Objekt(v), "paket_platz") }, { "kleingruen", false } }
                    : new PyObjekt { { "modell_tuer", new PyObjekt() }, { "zierwege", new List<object?>() }, { "paket_platz", Get(Objekt(v), "paket_platz") } },
                _ => v,
            });
        }
        var karte = new PyObjekt
        {
            { "welt", new PyObjekt { { "breite", Bearbeitung.Zahl(breite) }, { "hoehe", Bearbeitung.Zahl(hoehe) }, { "landschaft", new List<object?>() } } },
            { "orte", new List<object?>() },
            { "modelle", Py.Kopie(Get(vorlage.Karte, "modelle") ?? new PyObjekt()) },
        };
        return new StadtFassung
        {
            StadtId = stadtId,
            Nummer = 0,
            Basis = null,
            Werkzeug = Werkzeug.Kennzeile,
            Erstellt = "",
            Karte = karte,
            Stadtdaten = rest,
            Innenraeume = new PyObjekt(),
            Laternen = [],
            Beete = [],
        };
    }

    /// <summary>Groesse einer leeren Stadt: 8 bis 64 je Seite, auf 0,5 gerundet.</summary>
    public static bool GroesseGueltig(double breite, double hoehe) =>
        breite is >= 8 and <= 64 && hoehe is >= 8 and <= 64 && Math.Abs((breite * 2) - Math.Round(breite * 2)) < 1e-9 && Math.Abs((hoehe * 2) - Math.Round(hoehe * 2)) < 1e-9;
}
