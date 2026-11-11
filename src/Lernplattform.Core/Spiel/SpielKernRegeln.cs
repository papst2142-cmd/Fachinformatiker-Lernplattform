using System.Numerics;
using System.Runtime.CompilerServices;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Weisung P12-179 Schritt 2: die Regeln des Spiel-Kerns fuer die eigene Engine (<see cref="ISpielRegeln"/>). Jede Regel
/// reicht unveraendert an die bisherige Regel weiter; ein Spielstand kommt als <see cref="ISpielSicht"/> und ist immer der
/// <see cref="Spielstand"/> (nur er liefert die Sicht). Angemeldet beim Laden von Lernplattform.Core (<see cref="Anmelden"/>).
/// </summary>
public sealed class SpielKernRegeln : ISpielRegeln
{
    private SpielKernRegeln()
    {
    }

    /// <summary>Meldet die Regeln bei der Engine an (Modul-Start; ein weiterer Aufruf aendert nichts).</summary>
#pragma warning disable CA2255 // Modul-Start ist hier gewollt: die Engine braucht die Regeln, bevor jemand sie ruft
    [ModuleInitializer]
#pragma warning restore CA2255
    public static void Anmelden() => SpielKern.Regeln = Einmal;

    private static readonly SpielKernRegeln Einmal = new();

    private static Spielstand S(ISpielSicht stand) => (Spielstand)stand;

    /// <inheritdoc/>
    public ISpielSicht Probestand(PyObjekt inhalt) => StadtbauSpiel.Probestand(inhalt).Stand;

    /// <inheritdoc/>
    public IStadtSicht Stadt(StadtFassung fassung, PyObjekt inhalt, bool nachsichtig = false) => fassung.StadtFuer(inhalt, nachsichtig);

    /// <inheritdoc/>
    public List<string> WeltPruefen(PyObjekt inhalt) => SpielPruefung.Welt(inhalt);

    /// <inheritdoc/>
    public PyObjekt FarmInhalt(PyObjekt inhalt) => Betrieb.FarmInhalt(inhalt);

    /// <inheritdoc/>
    public string FarmOrtText(ISpielSicht? stand, PyObjekt inhalt) => Betrieb.FarmOrtText((Spielstand?)stand, inhalt);

    /// <inheritdoc/>
    public PyObjekt? FilialGebaeude(ISpielSicht stand) => Betrieb.FilialGebaeude(S(stand));

    /// <inheritdoc/>
    public PyObjekt? Bleibe(object? kennung, PyObjekt inhalt) => Wohnung.Bleibe(kennung, inhalt);

    /// <inheritdoc/>
    public PyObjekt Einrichtung(ISpielSicht stand) => Wohnung.Einrichtung(S(stand));

    /// <inheritdoc/>
    public PyObjekt? MoebelStueck(object? kennung, PyObjekt inhalt) => Wohnung.MoebelStueck(kennung, inhalt);

    /// <inheritdoc/>
    public string GanzeEuro(object? wert) => Wohnung.GanzeEuro(wert);

    /// <inheritdoc/>
    public List<(double X, double Y, double W, double H)> TuerFlaechen(PyObjekt gebaeude) => Wohnung.TuerFlaechen(gebaeude);

    /// <inheritdoc/>
    public string SchrankPlaetzeText(PyObjekt moebel) => Charakter.PlaetzeText(Charakter.Plaetze(moebel));

    /// <inheritdoc/>
    public PyObjekt OrtGebaeude(string ort, ISpielSicht stand) => SpielOrte.Gebaeude(ort, S(stand));

    /// <inheritdoc/>
    public List<PyObjekt> OrtPersonen(string ort, ISpielSicht stand) => SpielOrte.Personen(ort, S(stand));

    /// <inheritdoc/>
    public (string Titel, string Text, List<(string Aktion, string Name)> Knoepfe) OrtNachricht(string ort, double x, double y,
        PyObjekt? person, ISpielSicht stand, PyObjekt? gebaeude) => SpielOrte.Nachricht(ort, x, y, person, S(stand), gebaeude);

    /// <inheritdoc/>
    public string TeilName(string typ, PyObjekt inhalt) => Geschaefte.TypName(typ, inhalt);

    /// <inheritdoc/>
    public string Abteilung(PyObjekt? daten, string stelle) => Kleidung.Abteilung(daten, stelle);

    /// <inheritdoc/>
    public List<Vector3> KleidungFarben(PyObjekt? daten, string? ware, string? festzeit = null) => Kleidung.Farben(daten, ware, festzeit);

    /// <inheritdoc/>
    public List<(string Id, string Slot, string? Festzeit)> KleidungAlle(PyObjekt daten) =>
        [.. Kleidung.Alle(daten).Select(k => (k.Id, k.Slot, k.Festzeit))];

    /// <inheritdoc/>
    public Figur.Stueck? FigurStueck(PyObjekt? daten, string id) => Kleidung.FigurStueck(daten, id);
}
