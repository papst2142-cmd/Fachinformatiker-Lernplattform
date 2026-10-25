using System.Globalization;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, W8: wo ein Durchgang seine Ereignisse und die Bestenliste ablegt (Python: die Datenbank
/// hinter <c>Game.db</c>). Geschrieben wird nur innerhalb einer <see cref="Sitzung"/>.
/// </summary>
public interface ISpielSpeicher
{
    /// <summary>Schreibrecht fuer eine Aktion; null: gerade laeuft ein anderer Vorgang (nichts schreiben).</summary>
    IDisposable? Sitzung();

    /// <summary>log_game_event: Rueckgabe der Zeitstempel der Zeile, null = nicht gespeichert.</summary>
    string? Schreiben(string art, string daten);

    /// <summary>game_events: chronologisch (Zeitstempel, dann Nummer), Daten wie json.loads.</summary>
    List<SpielEreignis> Ereignisse();

    /// <summary>log_record: eine Zeile der Bestenliste (daten ist JSON-Text).</summary>
    bool BestenSchreiben(string lauf, string art, string schluessel, object? wert, string daten);

    /// <summary>records: alle Zeilen der Bestenliste chronologisch.</summary>
    List<BestenZeile> Bestenliste();

    /// <summary>reset_records</summary>
    bool BestenlisteLoeschen();
}

/// <summary>Das Spiel darf gerade nicht schreiben (Abgleich, Uebernahme, Update oder Sicherung einspielen laeuft).</summary>
public sealed class SpielBelegtAusnahme() : Exception("Spiel: ein anderer Vorgang laeuft, nichts geschrieben");

/// <summary>
/// Phase 7, W3: Speicher eines Durchgangs im Arbeitsspeicher (wie die Tabellen spiel_ereignisse und
/// spiel_bestenliste) fuer Tests und Vergleichswerte. Die Daten stehen als JSON-Text darin wie in
/// Python; die Fassung in der Datenbank ist <see cref="SpielDatenbank"/> (W8).
/// </summary>
public sealed class SpielSpeicher(Func<string>? uhr = null) : ISpielSpeicher
{
    private readonly Func<string> uhr = uhr ?? (() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
    private readonly List<(long Nr, string Zeit, string Art, string Daten)> ereignisse = [];
    private readonly List<(long Nr, BestenZeile Zeile)> bestenliste = [];
    private long nummer;

    public IDisposable? Sitzung() => new Leer();

    public string? Schreiben(string art, string daten)
    {
        var zeit = uhr();
        ereignisse.Add((++nummer, zeit, art, daten));
        return zeit;
    }

    public List<SpielEreignis> Ereignisse() =>
        [.. ereignisse.OrderBy(e => e.Zeit, StringComparer.Ordinal).ThenBy(e => e.Nr)
            .Select(e => new SpielEreignis(e.Zeit, e.Art, Py.Lesen(e.Daten) as PyObjekt ?? []))];

    /// <summary>Die geschriebenen Ereignisse in der Reihenfolge des Schreibens (Art, JSON-Text).</summary>
    public IReadOnlyList<(string Art, string Daten)> Geschrieben => [.. ereignisse.Select(e => (e.Art, e.Daten))];

    public bool BestenSchreiben(string lauf, string art, string schluessel, object? wert, string daten)
    {
        bestenliste.Add((++nummer, new BestenZeile(uhr(), lauf, art, schluessel, wert is null ? null : Py.F(wert),
            Py.Lesen(daten) as PyObjekt ?? [])));
        return true;
    }

    public List<BestenZeile> Bestenliste() => [.. bestenliste.OrderBy(z => z.Zeile.Zeit, StringComparer.Ordinal).ThenBy(z => z.Nr).Select(z => z.Zeile)];

    public bool BestenlisteLoeschen()
    {
        bestenliste.Clear();
        return true;
    }

    private sealed class Leer : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>
/// Phase 7, W3: die Klasse <c>Game</c> aus <c>fisi_game.py</c> (Python 0.63.2, Z. 5042-6006) fuer
/// einen Durchgang: Ereignisse schreiben (_log, mit JSON wie Python), den Spielstand fortschreiben,
/// Erfolge pruefen und die Bestenliste fuehren. Die Aktionen selbst stehen in <see cref="SpielAktionen"/>.
/// </summary>
public sealed class Spielablauf
{
    private bool pruefe;
    private PySet einmal = [];
    private string? letzteZeit;
    private int schritte;   // _incremental
    private IDisposable? sitzung;
    private int tiefe;

    /// <param name="uhr">Nur fuer den Speicher im Arbeitsspeicher (ohne <paramref name="speicher"/>).</param>
    public Spielablauf(PyObjekt inhalt, ISpielSpeicher? speicher = null, Func<string>? uhr = null)
    {
        Inhalt = inhalt;
        Speicher = speicher ?? new SpielSpeicher(uhr);
        Stand = Neuaufbau();
    }

    public PyObjekt Inhalt { get; }

    public ISpielSpeicher Speicher { get; }

    /// <summary>Wie oft ein Ereignis nicht gespeichert wurde (Python: log_game_event gab False).</summary>
    public int NichtGespeichert { get; private set; }

    /// <summary>state</summary>
    public Spielstand Stand { get; private set; }

    /// <summary>unlocked: neu erreichte Abzeichen-Stufen fuer die Anzeige.</summary>
    public List<PyObjekt> Freigeschaltet { get; } = [];

    /// <summary>knowledge(): Wissensstand je Fachbereich aus dem Lernstand.</summary>
    public Func<PyObjekt> Wissen { get; set; } = () => [];

    /// <summary>topic_knowledge(): Wissensstand je Thema aus dem Lernstand.</summary>
    public Func<PyObjekt> ThemenWissen { get; set; } = () => [];

    /// <summary>learned_projects(): im Lernbereich bearbeitete Testprojekte.</summary>
    public Func<PySet> LernProjekte { get; set; } = () => [];

    /// <summary>personal_events (Tests und das Szenario des Fingerabdrucks schalten es ab).</summary>
    public Func<Spielstand, long, PyObjekt, List<object?>> PersonalEreignisse { get; set; } = (stand, tag, inhalt) => Betrieb.PersonalEreignisse(stand, tag, inhalt);

    /// <summary>Wie oft der schrittweise Spielstand am Feierabend vom Neuaufbau abwich (Python: Zeile in fehler.log).</summary>
    public int Abweichungen { get; private set; }

    /// <summary>Die Felder der letzten Pruefung am Feierabend, die abwichen (leer: gleich).</summary>
    public List<string> LetzteUnterschiede { get; private set; } = [];

    /// <summary>firm_levels: Wissensstand je Fachbereich und je Thema zusammen.</summary>
    public PyObjekt FirmenStufen()
    {
        var stufen = new PyObjekt();
        foreach (var (k, v) in Wissen())
        {
            Py.Setzen(stufen, k, v);
        }
        foreach (var (k, v) in ThemenWissen())
        {
            Py.Setzen(stufen, k, v);
        }
        return stufen;
    }

    /// <summary>Alles, was der Feierabend der Firma und die Firmen-Aktionen vom Lernstand brauchen.</summary>
    public FirmenWissen FirmenWissen() => new(FirmenStufen(), ThemenWissen(), LernProjekte()) { Personal = PersonalEreignisse };

    /// <summary>reload: Spielstand aus allen Ereignissen neu berechnen.</summary>
    public Spielstand Neuaufbau()
    {
        var ereignisse = Speicher.Ereignisse();
        Stand = new Spielstand(ereignisse, Inhalt);
        einmal = Stand.EinmalGezaehlt();
        letzteZeit = ereignisse.Count > 0 ? (string?)ereignisse[^1].Zeit : null;
        schritte = 0;
        return Stand;
    }

    /// <summary>
    /// _log: ein Ereignis als JSON schreiben, es (wie json.loads) auf den Spielstand anwenden und danach
    /// die Erfolge pruefen. Am Feierabend gilt wie in Python der vollstaendige Neuaufbau, wenn der
    /// schrittweise Stand abweicht (_verify_state).
    /// </summary>
    public void Log(string art, PyObjekt daten)
    {
        Betreten();
        try
        {
            Schreiben(art, daten);
        }
        finally
        {
            Verlassen();
        }
    }

    /// <summary>
    /// Schreibrecht fuer eine Aktion holen (geschachtelt: nur das aeusserste holt es). Laeuft gerade
    /// Abgleich, Uebernahme, Update oder Sicherung einspielen, schreibt die Aktion gar nichts.
    /// </summary>
    private void Betreten()
    {
        if (tiefe == 0)
        {
            sitzung = Speicher.Sitzung() ?? throw new SpielBelegtAusnahme();
        }
        tiefe += 1;
    }

    private void Verlassen()
    {
        tiefe -= 1;
        if (tiefe == 0)
        {
            sitzung?.Dispose();
            sitzung = null;
        }
    }

    private void Schreiben(string art, PyObjekt daten)
    {
        var text = PythonJson.Text(daten);
        if (Speicher.Schreiben(art, text) is not { } zeit)
        {
            // Nicht gespeichert (Python: log_game_event gibt False, danach voll neu gerechnet)
            NichtGespeichert += 1;
            Neuaufbau();
            if (art != Ereignisse.Erfolg)
            {
                ErfolgePruefen(feierabend: art == Ereignisse.Feierabend);
            }
            return;
        }
        var nutz = Py.Lesen(text) as PyObjekt ?? [];
        if (letzteZeit is not null && string.CompareOrdinal(zeit, letzteZeit) < 0)
        {
            Neuaufbau();
        }
        else
        {
            letzteZeit = zeit;
            if (!Anwenden(zeit, art, nutz))
            {
                // Nicht (ganz) angewendet: neu rechnen (wirft wie Python, falls das Ereignis selbst den Fehler ausloest)
                Neuaufbau();
            }
            else if (art == Ereignisse.Feierabend)
            {
                Pruefen();
            }
        }
        if (art != Ereignisse.Erfolg)
        {
            ErfolgePruefen(feierabend: art == Ereignisse.Feierabend);
        }
    }

    /// <summary>
    /// _apply_written (STATE_COPY = True): das Ereignis auf einer Kopie des Spielstands anwenden. Wer
    /// den alten Stand haelt, sieht ihn unveraendert; schlaegt das Anwenden fehl, bleibt er der
    /// geltende Stand (false: voll neu rechnen).
    /// </summary>
    private bool Anwenden(string zeit, string art, PyObjekt nutz)
    {
        var stand = Stand.Kopie();
        var marken = new PySet(einmal);
        try
        {
            var tagVorher = stand.Tag;
            // Ein uebersprungenes Doppel (E-5, false) kommt nicht in history und zaehlt nicht fuer die Tagestickets
            if (stand.Anwenden(new SpielEreignis(zeit, art, nutz), marken))
            {
                stand.BearbeitetNotieren(art, nutz, tagVorher);
            }
        }
        catch (Exception)
        {
            return false;
        }
        Stand = stand;
        einmal = marken;
        schritte += 1;
        return true;
    }

    /// <summary>
    /// _verify_state: nach schrittweisen Ereignissen am Feierabend mit dem vollstaendigen Neuaufbau
    /// vergleichen (GameState.differences); weicht er ab, gilt der Neuaufbau.
    /// </summary>
    private void Pruefen()
    {
        if (schritte == 0)
        {
            return;
        }
        var stand = Stand;
        var marken = einmal;
        var neu = Neuaufbau();
        LetzteUnterschiede = stand.Unterschiede(neu);
        if (LetzteUnterschiede.Count > 0)
        {
            Abweichungen += 1;
            return;
        }
        Stand = stand;
        einmal = marken;
    }

    /// <summary>Die Ereignisse einer Aktion nacheinander schreiben; Rueckgabe: die Daten des letzten (wie Python).</summary>
    public PyObjekt Ausfuehren(List<(string Art, PyObjekt Daten)> ereignisse)
    {
        Betreten();
        try
        {
            PyObjekt letzte = [];
            foreach (var (art, daten) in ereignisse)
            {
                Schreiben(art, daten);
                letzte = daten;
            }
            return letzte;
        }
        finally
        {
            Verlassen();
        }
    }

    /// <summary>
    /// check_achievements: neu erreichte Abzeichen-Stufen festhalten und die Bestenliste fortschreiben.
    /// Ein aelterer Spielstand ohne Abzeichen bekommt sie still nachgetragen.
    /// </summary>
    public List<PyObjekt> ErfolgePruefen(PyObjekt? wissen = null, bool feierabend = false, bool erzwingen = false)
    {
        Betreten();
        try
        {
            return Erfolge(wissen, feierabend, erzwingen);
        }
        finally
        {
            Verlassen();
        }
    }

    private List<PyObjekt> Erfolge(PyObjekt? wissen, bool feierabend, bool erzwingen)
    {
        var stand = Stand;
        if (pruefe || stand.Profil is null || string.IsNullOrEmpty(stand.ErstesEreignis))
        {
            return [];
        }
        pruefe = true;
        try
        {
            var zeilen = Speicher.Bestenliste();
            var gefunden = Betrieb.NeueErfolge(stand, wissen, Inhalt);
            var still = stand.Erfolge.Count == 0 && stand.TageFertig > 0;
            // Ab 0.47: Eine Story, die schon vor dem Update fertig war, wird still als "Normal" nachgetragen
            var alteStory = stand.Schwierigkeitsgrad is null && Betrieb.StoryFertigTag(stand) < stand.Tag;
            var infos = new List<PyObjekt>();
            if (gefunden.Count > 0)
            {
                var tag = Math.Max(1, feierabend ? stand.TageFertig : stand.Tag);
                foreach (var (regel, stufe) in gefunden)
                {
                    var leise = still || (alteStory && Betrieb.StoryAbzeichen.Contains(Py.Str(regel["id"])));
                    var info = Betrieb.FreischaltInfo(stand, regel, stufe, zeilen, Inhalt);
                    Speicher.Schreiben(Ereignisse.Erfolg, PythonJson.Text(new PyObjekt
                    {
                        { "erfolg", regel["id"] }, { "stufe", stufe }, { "tag", tag }, { "nachgetragen", leise },
                    }));
                    if (!leise)
                    {
                        infos.Add(info);
                    }
                }
                Neuaufbau();
            }
            foreach (var (art, schluessel, wert, daten) in Betrieb.BestenNeu(Stand, zeilen, Inhalt, erzwingen))
            {
                Speicher.BestenSchreiben(Stand.ErstesEreignis!, art, schluessel, wert, PythonJson.Text(daten));
            }
            Freigeschaltet.AddRange(infos);
            return infos;
        }
        finally
        {
            pruefe = false;
        }
    }

    /// <summary>check_knowledge: wie <see cref="ErfolgePruefen"/>, dazu die Wissen-Abzeichen.</summary>
    public List<PyObjekt> WissenPruefen() => ErfolgePruefen(ThemenWissen());

    /// <summary>take_unlocks: je Abzeichen nur die hoechste neue Stufe, Meilenstein-Momente zuerst.</summary>
    public List<PyObjekt> FreischaltungenAbholen()
    {
        var beste = new List<PyObjekt>();
        foreach (var info in Freigeschaltet)
        {
            var stelle = beste.FindIndex(b => Py.Gleich(b["erfolg"], info["erfolg"]));
            if (stelle < 0)
            {
                beste.Add(info);
            }
            else if (Py.Kleiner(beste[stelle]["stufe"], info["stufe"]))
            {
                beste[stelle] = info;
            }
        }
        Freigeschaltet.Clear();
        return [.. beste.OrderBy(i => Py.Wahr(i["moment"]) ? 0 : 1)];
    }

    /// <summary>reset_records: Bestenliste loeschen; der laufende Spielstand traegt sich gleich neu ein.</summary>
    public bool BestenlisteLoeschen()
    {
        Betreten();
        try
        {
            var ok = Speicher.BestenlisteLoeschen();
            ErfolgePruefen();
            return ok;
        }
        finally
        {
            Verlassen();
        }
    }

    // -- Aktionen (die Pruefung steht in SpielAktionen) --------------------------------

    public PyObjekt Profil(string? name, object? aussehen, object? schwierigkeit = null) =>
        Ausfuehren(SpielAktionen.Profil(Stand, name, aussehen, schwierigkeit));

    public PyObjekt Loesen(object? aufgabe, object? antwort, bool hilfe) =>
        Ausfuehren(SpielAktionen.Loesen(Stand, aufgabe, antwort, hilfe, ThemenWissen()));

    public PyObjekt Ersatzteil(object? aufgabe, object? teil) => Ausfuehren(SpielAktionen.Ersatzteil(Stand, aufgabe, teil));

    public PyObjekt Verschieben(object? aufgabe) => Ausfuehren(SpielAktionen.Verschieben(Stand, aufgabe));

    public PyObjekt Feierabend() =>
        Ausfuehren(SpielAktionen.Feierabend(Stand, Py.Wahr(Stand.Firma) ? FirmenWissen() : null));

    public PyObjekt Gruenden(string? name) => Ausfuehren(SpielAktionen.Gruenden(Stand, name));

    public PyObjekt Einstellen(object? bewerbung) => Ausfuehren(SpielAktionen.Einstellen(Stand, bewerbung));

    public PyObjekt Entlassen(object? person) => Ausfuehren(SpielAktionen.Entlassen(Stand, person));

    public PyObjekt Weiterbilden(object? person, string ziel) => Ausfuehren(SpielAktionen.Weiterbilden(Stand, person, ziel));

    public PyObjekt Coachen(object? person) => Ausfuehren(SpielAktionen.Coachen(Stand, person));

    public PyObjekt Entscheiden(object? entscheidung, object? wahl) => Ausfuehren(SpielAktionen.Entscheiden(Stand, entscheidung, wahl));

    public PyObjekt Ausbauen() => Ausfuehren(SpielAktionen.Ausbauen(Stand));

    public PyObjekt RaumBauen(object? raum) => Ausfuehren(SpielAktionen.RaumBauen(Stand, raum));

    public PyObjekt FilialeEroeffnen(string? name) => Ausfuehren(SpielAktionen.FilialeEroeffnen(Stand, name));

    public PyObjekt FilialeAusbauen() => Ausfuehren(SpielAktionen.FilialeAusbauen(Stand));

    public PyObjekt Versetzen(object? person, object? ort) => Ausfuehren(SpielAktionen.Versetzen(Stand, person, ort));

    public PyObjekt Anbieten(object? anfrage, object? zuschlag, PyObjekt? antwort) => Ausfuehren(SpielAktionen.Anbieten(Stand, anfrage, zuschlag, antwort));

    public PyObjekt Delegieren(object? ticket, object? person) => Ausfuehren(SpielAktionen.Delegieren(Stand, ticket, person, FirmenStufen()));

    public PyObjekt KreditAufnehmen(object? summe, object? laufzeit, object? paket = null) =>
        Ausfuehren(SpielAktionen.KreditAufnehmen(Stand, summe, laufzeit, paket));

    public PyObjekt KreditAbloesen(object? kredit) => Ausfuehren(SpielAktionen.KreditAbloesen(Stand, kredit));

    public PyObjekt WerbungBuchen(object? werbung, object? art) => Ausfuehren(SpielAktionen.WerbungBuchen(Stand, werbung, art));

    public PyObjekt WerbungBeenden(object? werbung) => Ausfuehren(SpielAktionen.WerbungBeenden(Stand, werbung));

    public PyObjekt ZertifizierungStarten(object? zert) => Ausfuehren(SpielAktionen.ZertifizierungStarten(Stand, zert));

    public PyObjekt FarmStarten() => Ausfuehren(SpielAktionen.FarmStarten(Stand));

    public PyObjekt FarmLoesen(object? aufgabe, object? antwort, bool hilfe, object? zuschlag = null) =>
        Ausfuehren(SpielAktionen.FarmLoesen(Stand, aufgabe, antwort, hilfe, ThemenWissen(), zuschlag));

    public PyObjekt FarmTeamSetzen(IEnumerable<object?>? team) => Ausfuehren(SpielAktionen.FarmTeamSetzen(Stand, team, ThemenWissen()));

    public PyObjekt FarmMitglied(object? person) => Ausfuehren(SpielAktionen.FarmMitglied(Stand, person, ThemenWissen()));

    public PyObjekt ProjektAnbieten(object? projekt, object? zuschlag, PyObjekt? antwort) =>
        Ausfuehren(SpielAktionen.ProjektAnbieten(Stand, projekt, zuschlag, antwort));

    public PyObjekt ProjektTeamSetzen(object? projekt, IEnumerable<object?>? team) =>
        Ausfuehren(SpielAktionen.ProjektTeamSetzen(Stand, projekt, team, FirmenStufen()));

    public PyObjekt ProjektMitglied(object? projekt, object? person) =>
        Ausfuehren(SpielAktionen.ProjektMitglied(Stand, projekt, person, FirmenStufen()));
}
