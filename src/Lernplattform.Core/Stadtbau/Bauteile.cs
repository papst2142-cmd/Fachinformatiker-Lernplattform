using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>
/// S 0.9.0 (Weisung P12-48, Plan P12-26b Abschnitte 2 und 3): ein selbst gebautes Gebaeude aus Teilen von Kenney Modular
/// Buildings (CC0, Paket "modular" des Katalogs). Ein Teil steht in einem Feld des Bauraster (1 x 1, Stockwerk 0,625 hoch)
/// mit einer Drehung in Vierteln im Uhrzeigersinn (von oben gesehen, wie <see cref="Katalogstueck.Lage"/>). Dieser Teil ist
/// gemeinsam fuer Werkzeug und Spiel: die Vorschau im Werkzeug und (ab dem Einbau W4b) das Spiel setzen die Teile mit
/// <see cref="Zusammensetzen"/> zu EINEM Modell zusammen; es zeichnet sich danach wie jedes Haus-Modell (eigenes Hausnetz,
/// kein Zeichenaufruf je Teil). Die feste <see cref="Tabelle"/> sagt je Teil, was es ist und wohin seine Fassade bei
/// Drehung 0 zeigt (Test je Teil gegen die echten Dreiecke).
/// Daten in der Stadt: <c>stadtdaten.bauteile</c> = { modell: { teile: [{teil, x, y, stockwerk, dreh}], eingang: [{x, y,
/// seite}] } }; das Modell selbst steht als ein Block in <c>karte.modelle</c> bzw. <c>stadtdaten.neue_modelle</c>
/// (<see cref="Block"/>), damit Grundflaeche, Tuer und Pruefungen der Stadt wie bei jedem Haus gelten.
/// </summary>
public static class Bauteile
{
    /// <summary>Paket des Katalogs, aus dem die Teile stammen.</summary>
    public const string Paket = "modular";

    /// <summary>Vorsilbe der Teile (wie die Typen des Katalogs, "cc0:modular/&lt;name&gt;").</summary>
    public const string Praefix = Katalogstueck.Praefix + Paket + "/";

    /// <summary>Vorsilbe des Modellnamens eines selbst gebauten Gebaeudes ("bau:&lt;name&gt;", Plan Abschnitt 2).</summary>
    public const string ModellPraefix = "bau:";

    /// <summary>Hoehe eines Stockwerks (Hoehe der Wandteile).</summary>
    public const float Stockwerk = 0.625f;

    /// <summary>Ebenen 0 (Erdgeschoss) bis 5; Waende hoechstens bis Ebene 4 (EG + 4 wie P12-34), darueber nur Daecher.</summary>
    public const int Ebenen = 6;

    public const int HoechsteWandEbene = 4;

    /// <summary>Grenze je Gebaeude (Frage 3 = a): 8 000 Dreiecke, 0 zusaetzliche Zeichenaufrufe.</summary>
    public const int GrenzeDreiecke = 8000;

    /// <summary>Innenraum-Einheiten je Feld beim Vorschlag des Grundrisses (Erdgeschoss).</summary>
    public const double InnenJeFeld = 4.0;

    /// <summary>Was ein Teil ist.</summary>
    public enum Art
    {
        /// <summary>Wand mit einer Fassade (Fenster, Balkon ...).</summary>
        Wand,

        /// <summary>Ecke: zwei Fassaden (bei Drehung 0 Sued und Ost).</summary>
        Ecke,

        /// <summary>Wand mit Tuer (Eingang moeglich).</summary>
        Tuer,

        /// <summary>Block ohne Fassade (innen oder als glatte Wand).</summary>
        Block,

        /// <summary>Dach (liegt auf einer Wand der Ebene darunter).</summary>
        Dach,

        /// <summary>Kleines Teil an einer Fassade (Fenster, Tuer, Klimageraet), belegt kein Feld.</summary>
        Detail,

        /// <summary>Kleines Teil auf einem Dach (Klimageraet), belegt kein Feld.</summary>
        DachDetail,
    }

    /// <summary>Seiten eines Felds (Karte: Nord = kleines y, Ost = grosses x).</summary>
    [Flags]
    public enum Seiten
    {
        Keine = 0,
        Nord = 1,
        Ost = 2,
        Sued = 4,
        West = 8,
    }

    /// <summary>Eintrag der Tabelle: Art, Fassade bei Drehung 0, hat Fenster, hat Tuer, deutscher Name.</summary>
    public sealed record Ausrichtung(Art Art, Seiten Fassade, bool Fenster, bool Tuer, string Anzeige)
    {
        public bool BelegtFeld => Art is not (Art.Detail or Art.DachDetail);

        public bool IstDach => Art == Art.Dach;

        /// <summary>Wand im weiteren Sinn (traegt Daecher und Teile darueber).</summary>
        public bool IstWand => Art is Art.Wand or Art.Ecke or Art.Tuer or Art.Block;
    }

    /// <summary>
    /// Alle Teile des Pakets, die der Editor anbietet. Die Fassade zeigt bei Drehung 0 nach Sueden (+z des Modells, Vorderseite
    /// wie bei allen Kenney-Modellen); Ecken zeigen nach Sueden und Osten. Musterhaeuser und Treppen stehen in
    /// <see cref="NichtImEditor"/> (ganze Haeuser bzw. ragen ueber das Feld hinaus).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, Ausrichtung> Tabelle = new Dictionary<string, Ausrichtung>(StringComparer.Ordinal)
    {
        ["building-block"] = new(Art.Block, Seiten.Keine, false, false, "Wand ohne Fenster"),
        ["building-corner-bottom"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, false, false, "Ecke unten"),
        ["building-corner-top"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, false, false, "Ecke oben"),
        ["building-corner-window-middle"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, true, false, "Ecke mit Fenster Mitte"),
        ["building-corner-window-narrow"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, true, false, "Ecke mit schmalem Fenster"),
        ["building-corner-window-sill"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, true, false, "Ecke mit Fenster und Sims"),
        ["building-corner-window-top-round"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, true, false, "Ecke mit Rundbogenfenster"),
        ["building-corner-window-top-square"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, true, false, "Ecke mit hohem Fenster"),
        ["building-corner-window"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, true, false, "Ecke mit Fenster"),
        ["building-corner"] = new(Art.Ecke, Seiten.Sued | Seiten.Ost, false, false, "Ecke"),
        ["building-door-window-narrow"] = new(Art.Tuer, Seiten.Sued, true, true, "Tür mit schmalem Fenster"),
        ["building-door-window"] = new(Art.Tuer, Seiten.Sued, true, true, "Tür mit Fenster"),
        ["building-door"] = new(Art.Tuer, Seiten.Sued, false, true, "Tür"),
        ["building-edges-door"] = new(Art.Tuer, Seiten.Sued, false, true, "Tür mit Rahmen"),
        ["building-window-awnings"] = new(Art.Wand, Seiten.Sued, true, false, "Fenster mit Markisen"),
        ["building-window-balcony"] = new(Art.Wand, Seiten.Sued, true, false, "Fenster mit Balkon"),
        ["building-window-door-window-round"] = new(Art.Tuer, Seiten.Sued, true, true, "Tür zwischen Rundfenstern"),
        ["building-window-door-window"] = new(Art.Tuer, Seiten.Sued, true, true, "Tür zwischen Fenstern"),
        ["building-window-large-left"] = new(Art.Wand, Seiten.Sued, true, false, "Großes Fenster links"),
        ["building-window-large-middle"] = new(Art.Wand, Seiten.Sued, true, false, "Großes Fenster Mitte"),
        ["building-window-large-right"] = new(Art.Wand, Seiten.Sued, true, false, "Großes Fenster rechts"),
        ["building-window-large"] = new(Art.Wand, Seiten.Sued, true, false, "Großes Fenster"),
        ["building-window-left-sill"] = new(Art.Wand, Seiten.Sued, true, false, "Fenster links mit Sims"),
        ["building-window-left"] = new(Art.Wand, Seiten.Sued, true, false, "Fenster links"),
        ["building-window-right-sill"] = new(Art.Wand, Seiten.Sued, true, false, "Fenster rechts mit Sims"),
        ["building-window-right"] = new(Art.Wand, Seiten.Sued, true, false, "Fenster rechts"),
        ["building-window-sill"] = new(Art.Wand, Seiten.Sued, true, false, "Fenster mit Sims"),
        ["building-window-wide-sill"] = new(Art.Wand, Seiten.Sued, true, false, "Breites Fenster mit Sims"),
        ["building-window-wide"] = new(Art.Wand, Seiten.Sued, true, false, "Breites Fenster"),
        ["building-window"] = new(Art.Wand, Seiten.Sued, true, false, "Fenster"),
        ["building-windows-high-middle"] = new(Art.Wand, Seiten.Sued, true, false, "Hohe Fenster Mitte"),
        ["building-windows-high-top-round"] = new(Art.Wand, Seiten.Sued, true, false, "Hohe Rundbogenfenster"),
        ["building-windows-high-top-square"] = new(Art.Wand, Seiten.Sued, true, false, "Hohe Fenster eckig"),
        ["building-windows-round"] = new(Art.Wand, Seiten.Sued, true, false, "Zwei Rundfenster"),
        ["building-windows-sills-round"] = new(Art.Wand, Seiten.Sued, true, false, "Zwei Rundfenster mit Sims"),
        ["building-windows-sills"] = new(Art.Wand, Seiten.Sued, true, false, "Zwei Fenster mit Sims"),
        ["building-windows"] = new(Art.Wand, Seiten.Sued, true, false, "Zwei Fenster"),
        ["detail-ac-a"] = new(Art.DachDetail, Seiten.Keine, false, false, "Klimagerät auf dem Dach"),
        ["detail-ac-b"] = new(Art.Detail, Seiten.Sued, false, false, "Klimagerät an der Wand"),
        ["door-brown-glass"] = new(Art.Detail, Seiten.Sued, false, true, "Haustür braun mit Glas"),
        ["door-brown-window"] = new(Art.Detail, Seiten.Sued, false, true, "Haustür braun mit Fenster"),
        ["door-brown"] = new(Art.Detail, Seiten.Sued, false, true, "Haustür braun"),
        ["door-white-glass"] = new(Art.Detail, Seiten.Sued, false, true, "Haustür weiß mit Glas"),
        ["door-white-window"] = new(Art.Detail, Seiten.Sued, false, true, "Haustür weiß mit Fenster"),
        ["door-white"] = new(Art.Detail, Seiten.Sued, false, true, "Haustür weiß"),
        ["roof-flat-awning-a"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach mit Vordach A"),
        ["roof-flat-awning-b"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach mit Vordach B"),
        ["roof-flat-awning-c"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach mit Vordach C"),
        ["roof-flat-border-center-corner"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Rand Mitte Ecke"),
        ["roof-flat-border-center"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Rand Mitte"),
        ["roof-flat-border-corner-slanted-passage"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Ecke schräg mit Durchgang"),
        ["roof-flat-border-corner-slanted"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Ecke schräg"),
        ["roof-flat-border-corner"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Rand Ecke"),
        ["roof-flat-border-end"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Rand Ende"),
        ["roof-flat-border-side"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Rand Seite"),
        ["roof-flat-border-straight"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Rand gerade"),
        ["roof-flat-center"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Mitte"),
        ["roof-flat-corner-high"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Ecke hoch"),
        ["roof-flat-corner"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Ecke"),
        ["roof-flat-detail-a"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach mit Aufbau A"),
        ["roof-flat-detail-b"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach mit Aufbau B"),
        ["roof-flat-detail-c"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach mit Aufbau C"),
        ["roof-flat-detail-d"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach mit Aufbau D"),
        ["roof-flat-top"] = new(Art.Dach, Seiten.Keine, false, false, "Flachdach Abdeckung"),
        ["roof-gable-corner"] = new(Art.Dach, Seiten.Keine, false, false, "Giebeldach Ecke"),
        ["roof-gable-end"] = new(Art.Dach, Seiten.Keine, false, false, "Giebeldach Ende"),
        ["roof-gable"] = new(Art.Dach, Seiten.Keine, false, false, "Giebeldach"),
        ["roof-slanted-corner-a"] = new(Art.Dach, Seiten.Keine, false, false, "Schrägdach Ecke A"),
        ["roof-slanted-corner-b"] = new(Art.Dach, Seiten.Keine, false, false, "Schrägdach Ecke B"),
        ["roof-slanted-corner-c"] = new(Art.Dach, Seiten.Keine, false, false, "Schrägdach Ecke C"),
        ["roof-slanted-corner-inner"] = new(Art.Dach, Seiten.Keine, false, false, "Schrägdach Innenecke"),
        ["roof-slanted-detail"] = new(Art.Dach, Seiten.Keine, false, false, "Schrägdach mit Verzierung"),
        ["roof-slanted-flat"] = new(Art.Dach, Seiten.Keine, false, false, "Schrägdach flaches Stück"),
        ["roof-slanted-window"] = new(Art.Dach, Seiten.Sued, true, false, "Schrägdach mit Dachfenster"),
        ["roof-slanted"] = new(Art.Dach, Seiten.Keine, false, false, "Schrägdach"),
        ["window-brown-detailed"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun verziert"),
        ["window-brown-large"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun groß"),
        ["window-brown-narrow"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun schmal"),
        ["window-brown-round-low"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun rund niedrig"),
        ["window-brown-round-middle"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun rund mittel"),
        ["window-brown-round-narrow"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun rund schmal"),
        ["window-brown-round-top"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun Rundbogen"),
        ["window-brown-round-wide"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun rund breit"),
        ["window-brown-tall"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun hoch"),
        ["window-brown-wide"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun breit"),
        ["window-brown"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen braun"),
        ["window-white-detailed"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß verziert"),
        ["window-white-large"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß groß"),
        ["window-white-narrow"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß schmal"),
        ["window-white-round-low"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß rund niedrig"),
        ["window-white-round-middle"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß rund mittel"),
        ["window-white-round-narrow"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß rund schmal"),
        ["window-white-round-top"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß Rundbogen"),
        ["window-white-round-wide"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß rund breit"),
        ["window-white-tall"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß hoch"),
        ["window-white-wide"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß breit"),
        ["window-white"] = new(Art.Detail, Seiten.Sued, true, false, "Fensterrahmen weiß"),
    };

    /// <summary>Teile des Pakets, die der Editor nicht anbietet, mit Grund (Test: jedes Teil steht genau in einer Liste).</summary>
    public static readonly IReadOnlyDictionary<string, string> NichtImEditor = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["building-sample-house-a"] = "ganzes Musterhaus",
        ["building-sample-house-b"] = "ganzes Musterhaus",
        ["building-sample-house-c"] = "ganzes Musterhaus",
        ["building-sample-tower-a"] = "ganzer Musterturm",
        ["building-sample-tower-b"] = "ganzer Musterturm",
        ["building-sample-tower-c"] = "ganzer Musterturm",
        ["building-sample-tower-d"] = "ganzer Musterturm",
        ["building-steps-narrow-windows-round"] = "Treppe ragt über das Feld hinaus",
        ["building-steps-narrow-windows"] = "Treppe ragt über das Feld hinaus",
        ["building-steps-narrow"] = "Treppe ragt über das Feld hinaus",
        ["building-steps-wide"] = "Treppe ragt über das Feld hinaus",
    };

    // ---- Seiten -----------------------------------------------------------------------------------

    /// <summary>Die vier Seiten im Uhrzeigersinn (von oben gesehen, Nord oben).</summary>
    public static readonly Seiten[] Alle = [Seiten.Nord, Seiten.Ost, Seiten.Sued, Seiten.West];

    /// <summary>Seiten nach <paramref name="dreh"/> Vierteldrehungen im Uhrzeigersinn (Sued wird West, West wird Nord ...).</summary>
    public static Seiten Drehen(Seiten s, int dreh)
    {
        dreh = ((dreh % 4) + 4) % 4;
        var w = (int)s & 15;
        return (Seiten)(((w << dreh) | (w >> (4 - dreh))) & 15);
    }

    /// <summary>Schritt im Raster zur Nachbarseite (eine einzelne Seite).</summary>
    public static (int Dx, int Dy) Richtung(Seiten s) => s switch
    {
        Seiten.Nord => (0, -1),
        Seiten.Ost => (1, 0),
        Seiten.Sued => (0, 1),
        Seiten.West => (-1, 0),
        _ => throw new ArgumentException("eine einzelne Seite erwartet", nameof(s)),
    };

    public static string SeiteText(Seiten s) => s switch
    {
        Seiten.Nord => "nord",
        Seiten.Ost => "ost",
        Seiten.Sued => "sued",
        Seiten.West => "west",
        _ => throw new ArgumentException("eine einzelne Seite erwartet", nameof(s)),
    };

    public static Seiten? SeiteAus(object? text) => text switch
    {
        "nord" => Seiten.Nord,
        "ost" => Seiten.Ost,
        "sued" => Seiten.Sued,
        "west" => Seiten.West,
        _ => null,
    };

    // ---- Daten ------------------------------------------------------------------------------------

    /// <summary>Ein gesetztes Teil: Kennung "cc0:modular/&lt;name&gt;", Feld, Ebene, Drehung (0 bis 3).</summary>
    public sealed record Teil(string Id, int X, int Y, int Stockwerk, int Dreh)
    {
        public string Name => Id.StartsWith(Praefix, StringComparison.Ordinal) ? Id[Praefix.Length..] : Id;

        public Ausrichtung? Art => Tabelle.GetValueOrDefault(Name);

        /// <summary>Seiten der Fassade nach der Drehung.</summary>
        public Seiten Fassade => Art is { } a ? Drehen(a.Fassade, Dreh) : Seiten.Keine;
    }

    /// <summary>Eingang: Feld im Erdgeschoss und die Seite, an der die Tuer nach draussen zeigt.</summary>
    public sealed record Eingang(int X, int Y, Seiten Seite);

    /// <summary>
    /// Teile aus den Daten lesen (<c>[{teil, x, y, stockwerk, dreh}]</c>, ganze Zahlen). Falscher Aufbau:
    /// <see cref="InvalidDataException"/>. Unbekannte Teile werden gelesen (die Pruefung meldet sie).
    /// </summary>
    public static List<Teil> TeileLesen(object? daten)
    {
        if (daten is not List<object?> liste)
        {
            throw new InvalidDataException("bauteile: Liste erwartet");
        }
        var teile = new List<Teil>(liste.Count);
        foreach (var e in liste)
        {
            if (e is not PyObjekt o || o.Count != 5 || o["teil"] is not string id || !Ganz(o, "x") || !Ganz(o, "y") || !Ganz(o, "stockwerk") || !Ganz(o, "dreh")
                || Py.Int(o["dreh"]) is < 0 or > 3 || Py.Int(o["stockwerk"]) is < 0 or >= Ebenen || Math.Abs(Py.Int(o["x"])) > 64 || Math.Abs(Py.Int(o["y"])) > 64)
            {
                throw new InvalidDataException("bauteile: falscher Eintrag");
            }
            teile.Add(new Teil(id, (int)Py.Int(o["x"]), (int)Py.Int(o["y"]), (int)Py.Int(o["stockwerk"]), (int)Py.Int(o["dreh"])));
        }
        return teile;
    }

    private static bool Ganz(PyObjekt o, string k) => Py.Hat(o, k) && Py.IstNurInt(o[k]);

    /// <summary>Eingaenge lesen (<c>[{x, y, seite}]</c>); falscher Aufbau: <see cref="InvalidDataException"/>.</summary>
    public static List<Eingang> EingaengeLesen(object? daten)
    {
        if (daten is not List<object?> liste)
        {
            throw new InvalidDataException("eingang: Liste erwartet");
        }
        var e = new List<Eingang>();
        foreach (var x in liste)
        {
            if (x is not PyObjekt o || o.Count != 3 || !Ganz(o, "x") || !Ganz(o, "y") || SeiteAus(Get(o, "seite")) is not { } s)
            {
                throw new InvalidDataException("eingang: falscher Eintrag");
            }
            e.Add(new Eingang((int)Py.Int(o["x"]), (int)Py.Int(o["y"]), s));
        }
        return e;
    }

    public static List<object?> TeileDaten(IEnumerable<Teil> teile) =>
    [
        .. teile.Select(t => (object?)new PyObjekt { { "teil", t.Id }, { "x", (long)t.X }, { "y", (long)t.Y }, { "stockwerk", (long)t.Stockwerk }, { "dreh", (long)t.Dreh } }),
    ];

    public static List<object?> EingaengeDaten(IEnumerable<Eingang> e) =>
        [.. e.Select(x => (object?)new PyObjekt { { "x", (long)x.X }, { "y", (long)x.Y }, { "seite", SeiteText(x.Seite) } })];

    // ---- Zusammensetzen ---------------------------------------------------------------------------

    /// <summary>
    /// Lage eines Teils im Gebaeude (Ecke des Rasters = 0, 0; x nach Osten, z = Karte y nach Sueden, y nach oben). Wand und Dach:
    /// Mitte des Felds; Fassaden-Teil: an der Aussenseite der Fassade, Fenster mittig in der Hoehe, Tuer am Boden;
    /// Dach-Teil: auf dem Flachdach des Felds.
    /// </summary>
    public static Matrix4x4 Lage(Teil t, GrafikModell m)
    {
        var a = t.Art ?? throw new InvalidDataException("Teil unbekannt: " + t.Id);
        var drehung = Matrix4x4.CreateRotationY(-MathF.PI / 2 * t.Dreh);
        var feld = Matrix4x4.CreateTranslation(t.X + 0.5f, t.Stockwerk * Stockwerk, t.Y + 0.5f);
        switch (a.Art)
        {
            case Art.Detail:
            {
                var hoehe = m.Max.Y - m.Min.Y;
                var unten = a.Tuer ? 0f : MathF.Max(0f, (Stockwerk - hoehe) / 2);
                var vorne = Matrix4x4.CreateTranslation(-(m.Min.X + m.Max.X) / 2, unten - m.Min.Y, 0.5f - m.Min.Z);
                return vorne * drehung * feld;
            }
            case Art.DachDetail:
            {
                var auf = Matrix4x4.CreateTranslation(-(m.Min.X + m.Max.X) / 2, DachDetailHoehe - m.Min.Y, -(m.Min.Z + m.Max.Z) / 2);
                return auf * drehung * feld;
            }
            default:
                return drehung * feld;
        }
    }

    /// <summary>Hoehe ueber dem Boden der Ebene, auf der ein Dach-Teil steht (Oberseite des Flachdachs „Flachdach Mitte“).</summary>
    public const float DachDetailHoehe = 0.1062f;

    /// <summary>
    /// Die Teile zu EINEM Modell zusammensetzen (Reihenfolge der Teile = Reihenfolge der Dreiecke, Farben und Eckfarben wie das Teil).
    /// <paramref name="modell"/> liefert zu einer Kennung "cc0:modular/&lt;name&gt;" das Modell; fehlt eines oder ist ein
    /// Teil unbekannt: <see cref="InvalidDataException"/> (nie still weglassen).
    /// </summary>
    public static GrafikModell Zusammensetzen(string name, IReadOnlyList<Teil> teile, Func<string, GrafikModell?> modell)
    {
        var ecken = new List<float>();
        var farben = new List<byte>();
        var eckFarben = new List<byte>();
        foreach (var t in teile)
        {
            if (t.Art is null)
            {
                throw new InvalidDataException("Teil unbekannt: " + t.Id);
            }
            var m = modell(t.Id) ?? throw new InvalidDataException("Teil fehlt: " + t.Id);
            var lage = Lage(t, m);
            for (var i = 0; i + 2 < m.Ecken.Length; i += 3)
            {
                var p = Vector3.Transform(new Vector3(m.Ecken[i], m.Ecken[i + 1], m.Ecken[i + 2]), lage);
                ecken.Add(p.X);
                ecken.Add(p.Y);
                ecken.Add(p.Z);
            }
            farben.AddRange(m.Farben);
            if (m.EckFarben is { } eck)
            {
                eckFarben.AddRange(eck);
            }
            else
            {
                for (var d = 0; d < m.Dreiecke; d++)
                {
                    for (var k = 0; k < 3; k++)
                    {
                        eckFarben.AddRange(m.Farben.AsSpan(d * 3, 3));
                    }
                }
            }
        }
        return new GrafikModell(name, [.. ecken], [.. farben], [.. eckFarben]);
    }

    /// <summary>Dreiecke aller Teile (unbekannte oder fehlende zaehlen 0).</summary>
    public static int Dreiecke(IEnumerable<Teil> teile, Func<string, GrafikModell?> modell) =>
        teile.Sum(t => t.Art is null ? 0 : modell(t.Id)?.Dreiecke ?? 0);

    /// <summary>
    /// Das Modell der Stadt (<c>karte.modelle</c>): ein Block genau ueber dem Kasten des zusammengesetzten Gebaeudes. Das
    /// Spiel streckt das Modell auf den Block (<see cref="StadtSzene.GrafikBlock"/>); bei genau dem Kasten bleibt es
    /// unveraendert (Streckung 1, Test). Ohne Fenster: ohne Grafik waere es ein glatter Kasten.
    /// </summary>
    public static List<object?> Block(GrafikModell gebaeude)
    {
        var (min, max) = (gebaeude.Min, gebaeude.Max);
        var g = max - min;
        return
        [
            new PyObjekt
            {
                { "art", "block" }, { "x", Zahl(min.X) }, { "y", Zahl(min.Z) }, { "w", Zahl(g.X) }, { "t", Zahl(g.Z) }, { "h", Zahl(max.Y) }, { "fenster", false },
            },
        ];
    }

    private static double Zahl(float f) => double.Parse(f.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Die selbst gebauten Gebaeude einer Stadt (<c>stadtdaten.bauteile</c>), leer ohne.</summary>
    public static PyObjekt Gebaeude(PyObjekt stadtdaten) => Get(stadtdaten, "bauteile") as PyObjekt ?? new PyObjekt();

    /// <summary>
    /// <paramref name="basis"/> mit den zusammengesetzten Modellen aller selbst gebauten Gebaeude der Stadt und der
    /// Zuordnung "modell:0" (der Block aus <see cref="Block"/>). So zeichnet die Stadt sie wie jedes Haus-Modell. Falscher
    /// Aufbau oder fehlendes Teil: <see cref="InvalidDataException"/>.
    /// </summary>
    public static GrafikBibliothek? MitGebaeuden(GrafikBibliothek? basis, PyObjekt stadtdaten, Func<string, GrafikModell?> modell)
    {
        var alle = Gebaeude(stadtdaten);
        if (basis is null || alle.Count == 0)
        {
            return basis;
        }
        var neu = new List<GrafikModell>();
        var bloecke = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, eintrag) in alle)
        {
            if (eintrag is not PyObjekt e)
            {
                throw new InvalidDataException("bauteile: falscher Eintrag");
            }
            neu.Add(Zusammensetzen(name, TeileLesen(Get(e, "teile")), modell));
            bloecke[name + ":0"] = name;
        }
        return basis.Mit(neu, bloecke);
    }
}
