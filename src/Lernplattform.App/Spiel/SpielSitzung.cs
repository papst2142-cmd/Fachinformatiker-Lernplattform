using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Suche;

namespace Lernplattform.Spiel;

/// <summary>
/// Phase 7, W6 (Weisung P7-11): das Spiel mit Spielstand in lernplattform.db, wie GameScreen.game
/// in mobile/src/spiel.py. Die Spielstaende verwaltet <see cref="Spielverwaltung"/> (Schreiben nur
/// ueber LernstandSchreiber und die Sperre der Vorgaenge, Weg aus W8); das Wissen fuer Tickets und
/// Abzeichen liest <see cref="SpielWissen"/> aus dem Lernstand. Die Stadt in 3D zeigt den aktiven
/// Durchgang (<see cref="Spiel"/>); solange keine Spielfigur angelegt ist, steht dahinter die
/// Probe-Stadt aus dem Arbeitsspeicher (nichts davon wird gespeichert).
/// </summary>
public sealed class SpielSitzung
{
    public SpielSitzung(Stadt stadt, GrafikBibliothek? grafik, PyObjekt? innenraeume, Lerninhalte? lernen, Suchtext? suche,
        LernstandSchreiber schreiber, string geraet, PyObjekt? geschaefte = null, PyObjekt? feste = null)
    {
        Stadt = stadt;
        Feste = feste;
        Geschaefte = geschaefte is null ? null : new Geschaefte(stadt.Inhalt, geschaefte);
        Grafik = grafik;
        Innenraeume = innenraeume;
        Lernen = lernen;
        Suche = suche;
        if (lernen is not null)
        {
            // Sammelliste 88: die Themen des Lernteils einmal fuer alle Spielstaende setzen
            SpielThemen.Standard ??= new SpielThemen(lernen);
            Wissen = new SpielWissen(schreiber.Lernstand.Pfad, lernen, SpielDaten.Objekt(stadt.Inhalt["balancing"]));
        }
        _schreiber = schreiber;
        // Weisung P7-17: Spielschritte im Hintergrund schreiben; ein Schreibfehler haelt das Spiel an
        Schlange.Fehler += fehler => Avalonia.Threading.Dispatcher.UIThread.Post(Schreibfehler);
        Verwaltung = new Spielverwaltung(schreiber, geraet, stadt.Inhalt, einrichten: Einrichten, schlange: Schlange);
        Spiel = Neu();
    }

    /// <summary>Weisung P7-17: die Schlange der Spielschritte (ein Schreiber, feste Reihenfolge).</summary>
    public Spielschlange Schlange { get; } = new();

    /// <summary>
    /// Weisung P7-17: ein Spielschritt wurde nicht gespeichert (steht in fehler.log). Der Spielstand wird aus der
    /// Datenbank neu aufgebaut, das Spiel haelt an und zeigt den Hinweis.
    /// </summary>
    private void Schreibfehler()
    {
        try
        {
            Verwaltung.Ablauf?.Neuaufbau();
        }
        catch (Exception fehler)
        {
            // ein kaputtes Ereignis im Protokoll: die naechste Aktion wirft wie Python, der Hinweis bleibt
            Core.Diagnose.Fehlerprotokoll.Schreiben("Spiel: Neuaufbau nach Schreibfehler: " + fehler.GetType().Name,
                Path.Combine(Path.GetDirectoryName(_schreiber.Lernstand.Pfad)!, Core.Diagnose.Fehlerprotokoll.DateiName));
        }
        if (!Abgleichen())
        {
            Spiel.Anhalten(Core.Texte.SpielProbeTexte.SchrittNichtGespeichert);
        }
    }

    private readonly LernstandSchreiber _schreiber;

    public Stadt Stadt { get; }

    public GrafikBibliothek? Grafik { get; }

    public PyObjekt? Innenraeume { get; }

    /// <summary>W7: Sortiment und Preise von Moebelhaus und Elektromarkt (geschaefte.json).</summary>
    public Geschaefte? Geschaefte { get; }

    /// <summary>Phase 8 W6: Fest-Deko (festzeiten.json); die Festzeit fragt jede Ansicht beim Aufbau ueber <see cref="Festzeit.Heute"/>.</summary>
    public PyObjekt? Feste { get; }

    public Lerninhalte? Lernen { get; }

    public Suchtext? Suche { get; }

    public SpielWissen? Wissen { get; }

    public Spielverwaltung Verwaltung { get; }

    public PyObjekt Inhalt => Stadt.Inhalt;

    /// <summary>app.slot_chosen: in diesem Programmlauf schon einen Platz gewaehlt.</summary>
    public bool PlatzGewaehlt { get; set; }

    /// <summary>Die Stadt in 3D zum aktiven Durchgang (ohne Spielfigur die Probe).</summary>
    public StadtSpiel Spiel { get; private set; }

    /// <summary>true, solange die Stadt nur die Probe zeigt (keine Spielfigur im aktiven Durchgang).</summary>
    public bool IstProbe => !ReferenceEquals(Spiel.Ablauf, Verwaltung.Ablauf);

    /// <summary>Der Spielstand des aktiven Durchgangs (ohne Durchgang ein leerer).</summary>
    public Spielstand Stand => Verwaltung.Stand;

    /// <summary>Wird gerufen, wenn <see cref="Spiel"/> ersetzt wurde (anderer Durchgang, Figur angelegt).</summary>
    public event Action? SpielGewechselt;

    private void Einrichten(Spielablauf ablauf)
    {
        if (Wissen is { } wissen)
        {
            ablauf.Wissen = wissen.Fachbereiche;
            ablauf.ThemenWissen = wissen.Themen;
            ablauf.LernProjekte = wissen.Projekte;
        }
    }

    private StadtSpiel Neu() => Verwaltung.Ablauf is { } ablauf && ablauf.Stand.Profil is not null
        ? new StadtSpiel(Stadt, ablauf, Grafik, Innenraeume, Geschaefte?.Daten, Feste, Festzeit.Heute)
        : SpielStart.Neu(Stadt, Grafik, Innenraeume, Geschaefte?.Daten, Feste, Festzeit.Heute);

    /// <summary>
    /// Nach jeder Aenderung: gehoert die Stadt noch zum aktiven Durchgang (mit Figur), nur auffrischen,
    /// sonst eine neue Stadt bauen. true = <see cref="Spiel"/> wurde ersetzt.
    /// </summary>
    public bool Abgleichen()
    {
        var soll = Verwaltung.Ablauf is { } ablauf && ablauf.Stand.Profil is not null ? ablauf : null;
        if (soll is not null && ReferenceEquals(Spiel.Ablauf, soll))
        {
            Spiel.Auffrischen();
            return false;
        }
        if (soll is null && IstProbe)
        {
            return false;
        }
        Spiel = Neu();
        SpielGewechselt?.Invoke();
        return true;
    }

    /// <summary>knowledge(): Wissensstand je Fachbereich in Prozent (ohne Lernteil 0).</summary>
    public PyObjekt Fachwissen() => Verwaltung.Ablauf?.Wissen() ?? Wissen?.Fachbereiche() ?? [];

    /// <summary>records(): die Bestenliste aller Durchgaenge.</summary>
    public List<BestenZeile> Bestenliste()
    {
        Schlange.Leeren();
        return SpielDatenbank.Bestenzeilen(_schreiber.Lernstand);
    }

    /// <summary>topic_knowledge()</summary>
    public PyObjekt Themenwissen() => Verwaltung.Ablauf?.ThemenWissen() ?? Wissen?.Themen() ?? [];
}
