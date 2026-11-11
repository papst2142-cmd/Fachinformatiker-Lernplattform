using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Weisung P12-179 Schritt 2: was die eigene Engine vom Spiel-Kern braucht, ohne ihn zu kennen. Feste Werte des Spiels
/// stehen hier (die Originale im Spiel-Kern verweisen hierher, gleicher Wert); Regeln, die nur der Spiel-Kern rechnen
/// kann, holt die Engine ueber <see cref="Regeln"/>. Der Spiel-Kern meldet seine Regeln beim Laden selbst an
/// (<c>SpielKernRegeln</c>, Modul-Start von Lernplattform.Core).
/// </summary>
public static class SpielKern
{
    /// <summary>Moebelhaus, Elektromarkt, Kaufhaus (<c>Geschaefte.Laeden</c>).</summary>
    public static readonly IReadOnlyList<string> Laeden = ["moebelhaus", "elektromarkt", KleidungLaden];

    /// <summary>Bank (<c>Schalter.Bank</c>).</summary>
    public const string Bank = "bank";

    /// <summary>Amt (<c>Schalter.Amt</c>).</summary>
    public const string Amt = "amt";

    /// <summary>Orte mit Schalter (<c>Schalter.Orte</c>).</summary>
    public static readonly IReadOnlyList<string> SchalterOrte = [Bank, Amt];

    /// <summary>Art Moebel im Sortiment (<c>Geschaefte.ArtMoebel</c>).</summary>
    public const string ArtMoebel = "moebel";

    /// <summary>Art Kleidung (<c>Kleidung.Art</c>).</summary>
    public const string KleidungArt = "kleidung";

    /// <summary>Laden der Kleidung (<c>Kleidung.Laden</c>).</summary>
    public const string KleidungLaden = "kaufhaus";

    /// <summary>Text an der Kleidung (<c>Kleidung.FuerDieFigur</c>).</summary>
    public const string KleidungFuerDieFigur = "Kleidung für deine Figur.";

    /// <summary>Text an Teilen ohne Firma (<c>Geschaefte.NurMitFirma</c>).</summary>
    public const string NurMitFirma = "Teile für die Firma gibt es erst mit eigener Firma.";

    /// <summary>Typ des Kleiderschranks (<c>Charakter.SchrankTyp</c>).</summary>
    public const string SchrankTyp = "kleiderschrank";

    /// <summary>Text am Kleiderschrank im Moebelhaus (<c>Charakter.ImMoebelhaus</c>).</summary>
    public const string ImMoebelhaus = "Damit kannst du dein Aussehen ändern.";

    /// <summary>DOOR_CLEARANCE (<c>Wohnung.TuerFrei</c>).</summary>
    public const double TuerFrei = 1.0;

    /// <summary>FURNITURE_REACH (<c>Wohnung.MoebelReichweite</c>).</summary>
    public const double MoebelReichweite = 0.9;

    /// <summary>Abstand eines Punkts zum Rechteck (x, y, w, h), 0 innen (<c>Spielwelt.Abstand</c>).</summary>
    public static double Abstand(double rx, double ry, double rw, double rh, double x, double y)
    {
        var dx = Math.Max(Math.Max(rx - x, 0), x - rx - rw);
        var dy = Math.Max(Math.Max(ry - y, 0), y - ry - rh);
        return double.Hypot(dx, dy);
    }

    /// <summary>Einrichtung eines Ladens mit "ware" (Auslage), aus Raeumen und Flur (<c>Geschaefte.Auslage</c>).</summary>
    public static IEnumerable<PyObjekt> Auslage(PyObjekt laden)
    {
        var gebaeude = Objekt(laden["gebaeude"]);
        var deko = Liste(gebaeude["raeume"]).Select(Objekt).SelectMany(r => ListeOderLeer(r, "deko"));
        if (Get(gebaeude, "flur") is PyObjekt flur)
        {
            deko = deko.Concat(ListeOderLeer(flur, "deko"));
        }
        return deko.Select(Objekt).Where(d => Py.Hat(d, "ware"));
    }

    private static ISpielRegeln? _regeln;

    /// <summary>Die Regeln des Spiel-Kerns (vom Spiel-Kern angemeldet; ohne Anmeldung ein klarer Fehler, nie still).</summary>
    public static ISpielRegeln Regeln
    {
        get => Volatile.Read(ref _regeln) ?? throw new InvalidOperationException(
            "Die Regeln des Spiels sind nicht angemeldet (Lernplattform.Core muss geladen sein).");
        set => Volatile.Write(ref _regeln, value ?? throw new ArgumentNullException(nameof(value)));
    }
}

/// <summary>
/// Weisung P12-179 Schritt 2: Regeln des Spiel-Kerns, die die Engine aufruft (jede reicht unveraendert an die Regel des
/// Spiel-Kerns weiter, gleiche Ergebnisse wie vorher). Ein Spielstand kommt nur als <see cref="ISpielSicht"/>.
/// </summary>
public interface ISpielRegeln
{
    /// <summary>Spielstand fuer Pruefung, 3D-Ansicht und Probe (alle Orte zu sehen), <c>StadtbauSpiel.Probestand</c>.</summary>
    ISpielSicht Probestand(PyObjekt inhalt);

    /// <summary>Die Stadt einer Fassung zu Spielinhalten mit deren Karte (<c>StadtbauSpiel.StadtFuer</c>).</summary>
    IStadtSicht Stadt(StadtFassung fassung, PyObjekt inhalt);

    /// <summary>Ladepruefung der Welt des Spiels (<c>SpielPruefung.Welt</c>).</summary>
    List<string> WeltPruefen(PyObjekt inhalt);

    /// <summary>Inhalt der Serverfarm (<c>Betrieb.FarmInhalt</c>).</summary>
    PyObjekt FarmInhalt(PyObjekt inhalt);

    /// <summary>Text im Rechenzentrum (<c>Betrieb.FarmOrtText</c>).</summary>
    string FarmOrtText(ISpielSicht? stand, PyObjekt inhalt);

    /// <summary>Gebaeude der Filiale, ohne Filiale null (<c>Betrieb.FilialGebaeude</c>).</summary>
    PyObjekt? FilialGebaeude(ISpielSicht stand);

    /// <summary>Wohnung zu einer Kennung (<c>Wohnung.Bleibe</c>).</summary>
    PyObjekt? Bleibe(object? kennung, PyObjekt inhalt);

    /// <summary>Einrichtung der eigenen Wohnung (<c>Wohnung.Einrichtung</c>).</summary>
    PyObjekt Einrichtung(ISpielSicht stand);

    /// <summary>Moebelstueck zu einer Kennung (<c>Wohnung.MoebelStueck</c>).</summary>
    PyObjekt? MoebelStueck(object? kennung, PyObjekt inhalt);

    /// <summary>Betrag in ganzen Euro (<c>Wohnung.GanzeEuro</c>).</summary>
    string GanzeEuro(object? wert);

    /// <summary>Tuerzonen eines Grundrisses als Rechtecke (<c>Wohnung.TuerFlaechen</c>).</summary>
    List<(double X, double Y, double W, double H)> TuerFlaechen(PyObjekt gebaeude);

    /// <summary>Plaetze eines Kleiderschranks als Text (<c>Charakter.PlaetzeText(Charakter.Plaetze(moebel))</c>).</summary>
    string SchrankPlaetzeText(PyObjekt moebel);

    /// <summary>Grundriss eines Orts im Spielstand (<c>SpielOrte.Gebaeude</c>).</summary>
    PyObjekt OrtGebaeude(string ort, ISpielSicht stand);

    /// <summary>Personen eines Orts im Spielstand (<c>SpielOrte.Personen</c>).</summary>
    List<PyObjekt> OrtPersonen(string ort, ISpielSicht stand);

    /// <summary>Nachricht an einem Ort (<c>SpielOrte.Nachricht</c>).</summary>
    (string Titel, string Text, List<(string Aktion, string Name)> Knoepfe) OrtNachricht(string ort, double x, double y, PyObjekt? person,
        ISpielSicht stand, PyObjekt? gebaeude);

    /// <summary>Name eines Teile-Typs (<c>Geschaefte.TypName</c>).</summary>
    string TeilName(string typ, PyObjekt inhalt);

    /// <summary>Name der Abteilung einer Stelle (<c>Kleidung.Abteilung</c>).</summary>
    string Abteilung(PyObjekt? daten, string stelle);

    /// <summary>Farben einer Auslage im Kaufhaus (<c>Kleidung.Farben</c>).</summary>
    List<Vector3> KleidungFarben(PyObjekt? daten, string? ware, string? festzeit = null);

    /// <summary>Alle Kleidungsstuecke (Kennung, Stelle, Festzeit) in ihrer Reihenfolge (<c>Kleidung.Alle</c>).</summary>
    List<(string Id, string Slot, string? Festzeit)> KleidungAlle(PyObjekt daten);

    /// <summary>Ein Stueck fuer die Darstellung an der Figur (<c>Kleidung.FigurStueck</c>).</summary>
    Figur.Stueck? FigurStueck(PyObjekt? daten, string id);
}

/// <summary>
/// Weisung P12-179 Schritt 2: das Sortiment der Geschaefte, wie die Engine es fuer Namen und Pruefung braucht (der Spiel-Kern
/// liefert es: <c>Geschaefte</c>).
/// </summary>
public interface ISortiment
{
    /// <summary>Spielinhalt.</summary>
    PyObjekt Inhalt { get; }

    /// <summary>Daten der Geschaefte (geschaefte.json).</summary>
    PyObjekt Daten { get; }

    /// <summary>Ladepruefung der Auslage gegen das Sortiment (<c>Geschaefte.Pruefen</c>).</summary>
    List<string> Pruefen(PyObjekt? innenraeume);

    /// <summary>Name eines Moebels im Sortiment eines Ladens, sonst null.</summary>
    string? MoebelName(string laden, string kennung);
}
