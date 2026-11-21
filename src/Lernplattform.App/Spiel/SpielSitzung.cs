using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
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
        // Phase 9 W4 (Sammelliste 178): ein Umzug ohne Angabe nimmt den Schalter "Miete" aus den Optionen dieses Geraets
        ablauf.MieteSchalter = () => Wohnung.MieteGewaehlt(Programm.Zustand.Einstellungen.Laden());
        if (Wissen is { } wissen)
        {
            ablauf.Wissen = wissen.Fachbereiche;
            ablauf.ThemenWissen = wissen.Themen;
            ablauf.LernProjekte = wissen.Projekte;
        }
    }

    /// <summary>
    /// Welle A 1.2.0 (Weisung P12-334 Punkt 2): beim ersten Laden eines Spielstands mit A 1.2.0 die Moebel der Wohnungen auf die
    /// breiten Grundrisse umrechnen (Kopie vor_update_* vorher, angehaengte Zeilen; spaeter nie wieder). Ein Fehler haelt das
    /// Spiel nicht an: er steht im Fehlerprotokoll, der naechste Start versucht es wieder.
    /// </summary>
    private void GrundrisseUmrechnen()
    {
        var ordner = Path.GetDirectoryName(_schreiber.Lernstand.Pfad)!;
        try
        {
            var ergebnis = Verwaltung.GrundrisseUmrechnen(SpielStart.Breit());
            if (ergebnis.Protokoll.Length > 0)
            {
                Core.Update.Updateprotokoll.Schreiben(ergebnis.Protokoll, ordner);
            }
        }
        catch (Exception fehler) when (fehler is SpielBelegtAusnahme or SpielFehler or IOException or UnauthorizedAccessException or InvalidDataException
            or Microsoft.Data.Sqlite.SqliteException)
        {
            Core.Diagnose.Fehlerprotokoll.Schreiben("Spiel: Umrechnung breite Flure: " + fehler.GetType().Name,
                Path.Combine(ordner, Core.Diagnose.Fehlerprotokoll.DateiName));
        }
    }

    private StadtSpiel Neu()
    {
        if (Verwaltung.Ablauf is not { } ablauf || ablauf.Stand.Profil is null)
        {
            return SpielStart.Neu(Stadt, Grafik, Innenraeume, Geschaefte?.Daten, Feste, Festzeit.Heute, Jahreszeiten.Heute);
        }
        // Welle A 0.88.0: die Stadt, in der die Figur ist (Stadt 1 oder eine Paket-Stadt); fehlt das Paket, Stadt 1 mit dem Hinweis
        ablauf.Sperre = SperreGrund;
        GrundrisseUmrechnen();
        var kennung = StadtDerFigur(ablauf.Stand);
        _fehlt = null;
        StadtSpiel spiel;
        if (kennung is not null && PaketStadt(kennung) is { } paket)
        {
            spiel = new StadtSpiel(paket.Stadt, ablauf, Grafik, paket.Innenraeume, Geschaefte?.Daten, Feste, Festzeit.Heute, Jahreszeiten.Heute);
        }
        else
        {
            spiel = new StadtSpiel(Stadt, ablauf, Grafik, Innenraeume, Geschaefte?.Daten, Feste, Festzeit.Heute, Jahreszeiten.Heute);
            if (kennung is not null)
            {
                _fehlt = (kennung, Core.Spiel.Stadt.StadtName(ablauf.Stand, kennung));
                spiel.PaketFehlt(_fehlt.Value.Kennung, _fehlt.Value.Name);
            }
        }
        spiel.Fahrplan = Fahrplan;
        return spiel;
    }

    // -- Welle A 0.88.0: Paket-Staedte ---------------------------------------------------------------------

    /// <summary>Die geladenen Staedte aus Paketen (Kopf, Pfad der Stadt-Datei); null = keine (Tests).</summary>
    public Func<IReadOnlyList<(Core.Pakete.PaketKopf Kopf, string Datei)>>? PaketStaedte { get; set; }

    /// <summary>Fahrplan am Bahnhof (von der Oberflaeche aus der Liste der Pakete); null = keiner.</summary>
    public Func<IReadOnlyList<FahrplanStadt>>? Fahrplan { get; set; }

    private readonly Dictionary<string, (long Stand, Stadt Stadt, PyObjekt Innenraeume)> _paketStaedte = new(StringComparer.Ordinal);

    private (string Kennung, string Name)? _fehlt;

    /// <summary>Warum eine Paket-Stadt nicht gebaut werden konnte (je Kennung), fuer den Fahrplan.</summary>
    public IReadOnlyDictionary<string, string> PaketFehler => _paketFehler;

    private readonly Dictionary<string, string> _paketFehler = new(StringComparer.Ordinal);

    /// <summary>Kennung der Paket-Stadt, in der die Figur ist (null = Stadt 1).</summary>
    public string? StadtDerFigur(Spielstand stand) => Core.Spiel.Stadt.StadtVon(Stadt.OrtGlobal(stand));

    /// <summary>Die Figur ist in einer Paket-Stadt, deren Paket hier fehlt (Kennung, Name), sonst null.</summary>
    public (string Kennung, string Name)? Fehlt => _fehlt;

    /// <summary>Kein Spielschritt, solange das Paket der Stadt fehlt, in der die Figur ist (Plan Abschnitt 5).</summary>
    private string? SperreGrund() => _fehlt is { } f ? Core.Texte.ZugTexte.Format(Core.Texte.ZugTexte.KeinSchritt, f.Name) : null;

    /// <summary>
    /// Die Stadt aus dem geladenen Paket <paramref name="kennung"/> (einmal je Stand gebaut, Vorsatz <c>kennung:</c>); null, wenn
    /// das Paket fehlt oder die Stadt nicht laedt (Grund in <see cref="PaketFehler"/>).
    /// </summary>
    public (Stadt Stadt, PyObjekt Innenraeume)? PaketStadt(string kennung)
    {
        if (PaketStaedte?.Invoke().FirstOrDefault(p => p.Kopf.Kennung == kennung) is not { Kopf: not null } eintrag)
        {
            _paketStaedte.Remove(kennung);
            return null;
        }
        if (_paketStaedte.TryGetValue(kennung, out var fertig) && fertig.Stand == eintrag.Kopf.Stand)
        {
            return (fertig.Stadt, fertig.Innenraeume);
        }
        try
        {
            var (stadt, innen) = SpielStart.AusPaketDatei(File.ReadAllBytes(eintrag.Datei));
            stadt.AlsPaket(kennung, eintrag.Kopf.Name, Stadt);
            _paketStaedte[kennung] = (eintrag.Kopf.Stand, stadt, innen);
            _paketFehler.Remove(kennung);
            return (stadt, innen);
        }
        catch (Exception fehler) when (fehler is IOException or UnauthorizedAccessException or InvalidDataException or StadtbauFehler or SpielFehler or KeyNotFoundException or InvalidCastException)
        {
            _paketFehler[kennung] = fehler.Message;
            Core.Diagnose.Fehlerprotokoll.Schreiben("Spiel: Paket-Stadt " + kennung + " laedt nicht: " + fehler.GetType().Name,
                Path.Combine(Path.GetDirectoryName(_schreiber.Lernstand.Pfad)!, Core.Diagnose.Fehlerprotokoll.DateiName));
            return null;
        }
    }

    /// <summary>
    /// Mit dem Zug in die Stadt <paramref name="kennung"/> (<see cref="StadtSpiel.ZugStadt1"/> = Stadt 1) fahren; danach die andere
    /// Stadt zeigen. Geht es nicht, steht der Grund in der Zeile.
    /// </summary>
    public StadtGrund ZugFahren(string kennung)
    {
        var name = kennung == StadtSpiel.ZugStadt1 ? Core.Texte.ZugTexte.Stadt1
            : PaketStaedte?.Invoke().FirstOrDefault(p => p.Kopf.Kennung == kennung).Kopf?.Name ?? kennung;
        if (kennung != StadtSpiel.ZugStadt1 && PaketStadt(kennung) is null)
        {
            return StadtGrund.Unbekannt;
        }
        var grund = Spiel.ZugFahren(kennung, name);
        if (grund == StadtGrund.Ok)
        {
            Abgleichen();
            Spiel.FahrplanNeu(Core.Texte.ZugTexte.Format(Core.Texte.ZugTexte.Angekommen, name));
        }
        return grund;
    }

    /// <summary>Plan Abschnitt 5 (Nico 5a): Entfernen gesperrt, solange die Figur in dieser Stadt ist (Text mit dem Namen), sonst null.</summary>
    public string? EntfernenGrund(string kennung) =>
        Verwaltung.Ablauf is { } ablauf && ablauf.Stand.Profil is not null && StadtDerFigur(ablauf.Stand) == kennung
            ? Core.Texte.ZugTexte.Format(Core.Texte.ZugTexte.EntfernenGesperrt, Core.Spiel.Stadt.StadtName(ablauf.Stand, kennung))
            : null;

    /// <summary>Die Stadt, in der die Figur jetzt sein soll (Stadt 1, Paket-Stadt, oder Stadt 1 mit Hinweis, wenn das Paket fehlt).</summary>
    private bool StadtPasst(Spielablauf ablauf)
    {
        var kennung = StadtDerFigur(ablauf.Stand);
        if (kennung is null)
        {
            return _fehlt is null && ReferenceEquals(Spiel.Stadt, Stadt);
        }
        if (_fehlt is { } f)
        {
            return f.Kennung == kennung && PaketStadt(kennung) is null;
        }
        return Spiel.Stadt.Kennung == kennung && PaketStadt(kennung) is { } p && ReferenceEquals(p.Stadt, Spiel.Stadt);
    }

    /// <summary>
    /// Nach jeder Aenderung: gehoert die Stadt noch zum aktiven Durchgang (mit Figur), nur auffrischen,
    /// sonst eine neue Stadt bauen. true = <see cref="Spiel"/> wurde ersetzt.
    /// </summary>
    public bool Abgleichen()
    {
        var soll = Verwaltung.Ablauf is { } ablauf && ablauf.Stand.Profil is not null ? ablauf : null;
        if (soll is not null && ReferenceEquals(Spiel.Ablauf, soll) && StadtPasst(soll))
        {
            Spiel.Auffrischen();
            if (_fehlt is { } f)
            {
                Spiel.PaketFehlt(f.Kennung, f.Name);
            }
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
