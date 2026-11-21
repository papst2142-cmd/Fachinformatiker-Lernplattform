using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.60.0 (Weisung P12-337, Nico 21.11.2026 Antwort 1a aus P12-207): die Innenraeume, die das Spiel nicht aus der
/// Stadt-Datei des Werkzeugs nimmt, sondern aus seinen Spieldaten (jede Wohnung, das Buero der Bitweiche, das Buero der eigenen
/// Firma auf jeder Stufe, die Zweigstelle auf jeder Stufe, die Serverfarm und jeder Kunden-Ort). Das Werkzeug zeigt sie im
/// Grundriss (2D) und in 3D, gebaut mit genau dem Weg des Spiels (<see cref="Innenraum.Bauen"/> mit den breiten Grundrissen
/// aus <see cref="Spieldaten.Laden"/>). Nur ansehen: aendern und exportieren geht nicht, weil das Format der Stadt-Datei diese
/// Raeume nicht traegt (dafuer waere eine neue Formatnummer noetig, Frage an Nico in Meldung P12-209).
/// </summary>
public static class SpielRaeume
{
    /// <summary>Anfang des Schluessels eines Spiel-Raums (kein Ort der Stadt-Datei faengt so an).</summary>
    public const string Praefix = "spiel:";

    /// <summary>
    /// Ein Raum des Spiels: <paramref name="Ansicht"/> wie im Spiel ("zuhause", "buero", "filiale", "serverfarm", "kunde:…"),
    /// die Wohnung (nur "zuhause"), die Stufe der eigenen Firma (0 = Buero der Bitweiche, nur "buero") bzw. der Zweigstelle.
    /// </summary>
    public sealed record Raum(string Schluessel, string Gruppe, string Name, string Ansicht, object? Wohnung, int Stufe);

    /// <summary>Ist das der Schluessel eines Spiel-Raums?</summary>
    public static bool Ist(string? schluessel) => schluessel is not null && schluessel.StartsWith(Praefix, StringComparison.Ordinal);

    /// <summary>Gruppen in der Reihenfolge der Liste.</summary>
    public const string GruppeWohnung = "Wohnungen";
    public const string GruppeBuero = "Büro";
    public const string GruppeServerfarm = "Serverfarm";
    public const string GruppeZweigstelle = "Zweigstelle";
    public const string GruppeKunden = "Kunden";

    /// <summary>Alle Spiel-Raeume dieser Spielinhalte (Reihenfolge: Wohnungen, Buero, Serverfarm, Zweigstelle, Kunden).</summary>
    public static List<Raum> Alle(PyObjekt inhalt, PyObjekt? innenraeume)
    {
        var liste = new List<Raum>();
        foreach (var w in SpielDaten.ListeOderLeer(OderLeer(inhalt["wohnungen"]), "wohnungen").Select(SpielDaten.Objekt))
        {
            var id = Py.Str(w["id"]);
            liste.Add(new Raum(Praefix + "zuhause:" + id, GruppeWohnung, SpielDaten.Get(w, "name") as string ?? id, "zuhause", w["id"], 0));
        }
        var bitweiche = OderLeer(inhalt["gebaeude"]);
        liste.Add(new Raum(Praefix + "buero:bitweiche", GruppeBuero, SpielDaten.Get(bitweiche, "firma") as string ?? "Bitweiche", "buero", null, 0));
        var firma = OderLeer(OderLeer(inhalt["firma"])["gebaeude"]);
        var stufen = SpielDaten.ListeOderLeer(firma, "stufen");
        for (var i = 0; i < stufen.Count; i++)
        {
            var name = SpielDaten.Get(SpielDaten.Objekt(stufen[i]), "name") as string ?? "";
            liste.Add(new Raum(Praefix + "buero:firma" + (i + 1).ToString(CultureInfo.InvariantCulture), GruppeBuero,
                Texte.SpielRaumFirma(i + 1, name), "buero", null, i + 1));
        }
        if (innenraeume is not null && SpielDaten.Get(innenraeume, Innenraum.Serverfarm) is PyObjekt)
        {
            liste.Add(new Raum(Praefix + Innenraum.Serverfarm, GruppeServerfarm, Texte.SpielRaumServerfarm, Innenraum.Serverfarm, null, 0));
        }
        var filiale = OderLeer(OderLeer(inhalt["firma"])["filiale"]);
        var filialStufen = SpielDaten.ListeOderLeer(filiale, "stufen");
        for (var i = 0; i < filialStufen.Count; i++)
        {
            var name = SpielDaten.Get(SpielDaten.Objekt(filialStufen[i]), "name") as string ?? "";
            liste.Add(new Raum(Praefix + "filiale" + (i + 1).ToString(CultureInfo.InvariantCulture), GruppeZweigstelle,
                Texte.SpielRaumZweigstelle(i + 1, name), "filiale", null, i + 1));
        }
        foreach (var k in SpielDaten.ListeOderLeer(OderLeer(inhalt["kunden"]), "orte").Select(SpielDaten.Objekt))
        {
            var id = Py.Str(k["id"]);
            liste.Add(new Raum(Praefix + "kunde:" + id, GruppeKunden, SpielDaten.Get(k, "name") as string ?? id, "kunde:" + id, null, 0));
        }
        return liste;
    }

    private static PyObjekt OderLeer(object? wert) => wert as PyObjekt ?? [];

    /// <summary>Den Raum zum Schluessel suchen (null = gibt es nicht).</summary>
    public static Raum? Finden(string schluessel, PyObjekt inhalt, PyObjekt? innenraeume) =>
        Ist(schluessel) ? Alle(inhalt, innenraeume).FirstOrDefault(r => r.Schluessel == schluessel) : null;

    /// <summary>
    /// Spielstand, in dem genau dieser Raum so aussieht wie im Spiel: der Probestand des Werkzeugs
    /// (<see cref="StadtbauSpiel.Probestand"/>) mit der Wohnung, ohne eigene Firma (Bitweiche) bzw. mit der Stufe der Firma
    /// oder der Zweigstelle.
    /// </summary>
    public static Spielstand Stand(Raum raum, PyObjekt inhalt)
    {
        var stand = StadtbauSpiel.Probestand(inhalt).Stand;
        if (raum.Ansicht == "zuhause")
        {
            stand.Wohnung = raum.Wohnung;
        }
        else if (raum.Ansicht == "buero")
        {
            stand.Firma = raum.Stufe == 0 ? null : new PyObjekt { { "stufe", (long)raum.Stufe }, { "name", "Probe" }, { "tag", 1L } };
        }
        else if (raum.Ansicht == "filiale")
        {
            stand.Zweigstelle = new PyObjekt { { "stufe", (long)raum.Stufe }, { "tag", 1L } };
        }
        return stand;
    }

    /// <summary>Den Raum bauen wie das Spiel beim Betreten (Tageslicht, Sommer); null = gibt es in diesen Daten nicht.</summary>
    public static Innenraum? Bauen(Raum raum, PyObjekt inhalt, PyObjekt? innenraeume, GrafikBibliothek? grafik, PyObjekt? geschaefte,
        PyObjekt? feste = null) =>
        Innenraum.Bauen(raum.Ansicht, Stand(raum, inhalt), innenraeume, grafik, geschaefte, null, feste, Jahreszeit.Sommer);

    /// <summary>Personen des Raums fuer den Grundriss (Name und Platz, wie das Spiel sie hinstellt).</summary>
    public static List<PyObjekt> Personen(Innenraum innen) =>
        [.. innen.Personen.Select(p => new PyObjekt { { "id", p.Id }, { "name", p.Name }, { "platz", new List<object?> { (double)p.X, (double)p.Z } } })];

    /// <summary>
    /// Kurze Masse fuer die Karte rechts: Groesse, Flur, schmalste Innentuer, Zahl der Raeume und Moebel (aus dem Grundriss,
    /// den auch das Spiel zeichnet).
    /// </summary>
    public static List<string> Masse(PyObjekt gebaeude)
    {
        string M(double v) => v.ToString("0.00", CultureInfo.GetCultureInfo("de-DE")) + " m";
        var zeilen = new List<string> { Texte.SpielRaumGroesse(M(Py.F(gebaeude["breite"]) * Massstab), M(Py.F(gebaeude["hoehe"]) * Massstab)) };
        if (SpielDaten.Get(gebaeude, "flur") is PyObjekt flur && SpielDaten.Get(flur, "h") is { } fh)
        {
            zeilen.Add(Texte.SpielRaumFlur(M(Py.F(fh) * Massstab)));
        }
        var raeume = SpielDaten.ListeOderLeer(gebaeude, "raeume").Select(SpielDaten.Objekt).ToList();
        var tueren = new List<double>();
        foreach (var r in raeume)
        {
            if (SpielDaten.Get(r, "tuer") is PyObjekt t && SpielDaten.Get(t, "von") is { } von && SpielDaten.Get(t, "bis") is { } bis)
            {
                tueren.Add(Py.F(bis) - Py.F(von));
            }
        }
        if (tueren.Count > 0)
        {
            zeilen.Add(Texte.SpielRaumTuer(M(tueren.Min() * Massstab)));
        }
        var moebel = raeume.Sum(r => SpielDaten.ListeOderLeer(r, "deko").Count)
            + (SpielDaten.Get(gebaeude, "flur") is PyObjekt f2 ? SpielDaten.ListeOderLeer(f2, "deko").Count : 0);
        zeilen.Add(Texte.SpielRaumZahlen(raeume.Count, moebel));
        return zeilen;
    }

    /// <summary>Eine Einheit des Grundrisses in Metern (Massstab innen des Spiels, <see cref="Innenraum.Meter"/>: 0,6 m).</summary>
    public static double Massstab => 1.0 / Innenraum.Meter;
}
