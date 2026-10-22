using System.Text.Json.Nodes;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Inhalte;
using Lernplattform.Core.Pruefung;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Projekt;

/// <summary>
/// Phase 4, W3 (PB8): Ablauf der Seite "Abschlussprojekt" ohne Oberflaeche wie app_gui
/// FinalProjectView v0.63.2 (Wortliste W3 Abschnitt 3, je Zusage ein Test in ProjektAblaufTests).
/// Eingaben sammeln sich als offene Felder und werden 800 ms nach dem letzten Tastendruck
/// gespeichert (<see cref="Takt"/>, Uhr von aussen), ausserdem beim Verlassen eines Feldes, beim
/// Reiterwechsel, bei Projektwahl, neuem Projekt, Vorlage, Export und von aussen (Seite verlassen,
/// Beenden, Hintergrund, vor Sicherung und Uebernahme). Gespeichert wird nur ein geaenderter Wert
/// (Wert ungleich der geladenen Fassung; ein geleertes Feld mit Vorgabewert also nicht, wie Python,
/// Sammelliste 49). Haken, Fachbereich und Bewertung speichern sofort. Nach jedem Speichern wird
/// ein Abgleich vorgemerkt, am PC wie am Handy (Weisung P4-8, Frage 2 a). Loeschen verwirft offene
/// Eingaben (pending.clear()). Das aktive Projekt steht je Geraet in einstellungen.json.
/// </summary>
public sealed class ProjektAblauf(Func<LernstandSchreiber> schreiber, Func<JsonObject> einstellungen,
    Func<Action<JsonObject>, bool> aendern, IReadOnlyList<ProjektFrage> fragen, Func<bool> abgleichEingerichtet,
    Func<DateTimeOffset>? uhr = null, Func<string>? neueKennung = null)
{
    private readonly Func<DateTimeOffset> _uhr = uhr ?? (() => DateTimeOffset.UtcNow);
    private readonly Dictionary<string, Func<string>> _eingaben = new(StringComparer.Ordinal);
    private readonly List<string> _offen = [];
    private DateTimeOffset? _frist;
    private (long Anzahl, long Hoechste)? _stempel;

    /// <summary>Gezeigtes Projekt (null erst nach dem Loeschen bis zum Neuladen).</summary>
    public string? Projekt { get; private set; }

    /// <summary>Felder des gezeigten Projekts (geladene Fassung plus eigene gespeicherte Aenderungen).</summary>
    public OrderedDictionary<string, string> Felder { get; private set; } = new(StringComparer.Ordinal);

    /// <summary>Projektstunden der Ausbildung (AE 80, sonst 40), gelesen beim Laden und Zeigen.</summary>
    public int MaxStunden { get; private set; } = 40;

    public string Reiter { get; private set; } = Abschlussprojekt.Reiter[0].Schluessel;

    /// <summary>fg_index und fg_hint der Uebung "Fachgespräch".</summary>
    public int FrageNummer { get; private set; }

    public bool HinweisOffen { get; private set; }

    /// <summary>Felder, die getippt, aber noch nicht gespeichert sind.</summary>
    public IReadOnlyList<string> Offen => _offen;

    /// <summary>Wann das verzoegerte Speichern faellig ist (null: nichts geplant).</summary>
    public DateTimeOffset? Frist => _frist;

    /// <summary>Etwas wurde gespeichert: bald abgleichen (sync.schedule).</summary>
    public event Action? NachSpeichern;

    /// <summary>Projekt neu geladen: Reiter neu aufbauen (die Eingaben sind abgemeldet).</summary>
    public event Action? NeuGeladen;

    /// <summary>Berechnete Werte auffrischen (_refresh_derived).</summary>
    public event Action? Geaendert;

    /// <summary>Titel geaendert: Auswahlliste neu (_fill_project_menu).</summary>
    public event Action? MenueGeaendert;

    /// <summary>Fachbereich gewechselt: Reiter "Fachgespräch" neu bauen.</summary>
    public event Action? FragenNeu;

    private string Wert(string feld) => Felder.TryGetValue(feld, out var wert) ? wert : "";

    private (long, long)? Stempel()
    {
        // change_stamp, nur ueber abschlussprojekt (Anzahl und hoechste Nummer), lesend
        var zeile = schreiber().Lernstand.Abfragen("SELECT COUNT(*), MAX(rowid) FROM abschlussprojekt").FirstOrDefault();
        return zeile is null ? null : (Convert.ToInt64(zeile[0] ?? 0L), Convert.ToInt64(zeile[1] ?? 0L));
    }

    /// <summary>Gespeicherte Ausbildung aus der reservierten Zeile (wie Ausbildung.Gespeichert), sonst si.</summary>
    private static string Fachrichtung(OrderedDictionary<string, OrderedDictionary<string, string>> alle) =>
        alle.TryGetValue(Ausbildung.Zeile, out var zeile) && zeile.TryGetValue(Ausbildung.Feld, out var wert)
            && Array.IndexOf(Ausbildung.Werte, wert) >= 0 ? wert : "si";

    private OrderedDictionary<string, OrderedDictionary<string, string>> Projekte() =>
        Abschlussprojekt.Projekte(schreiber().ProjektFelder());

    // -- Projekte ------------------------------------------------------------------------------

    /// <summary>_load_project: Projekt laden (ohne Angabe das aktive dieses Geraets), Reiter neu.</summary>
    public void Laden(string? projekt = null)
    {
        Speichern();
        var alle = schreiber().ProjektFelder();
        var projekte = Abschlussprojekt.Projekte(alle);
        Projekt = projekt ?? Abschlussprojekt.AktivesProjekt(projekte, einstellungen()[Abschlussprojekt.AktivSchluessel] is JsonValue v
            && v.TryGetValue<string>(out var aktiv) ? aktiv : null, neueKennung);
        if (!(einstellungen()[Abschlussprojekt.AktivSchluessel] is JsonValue a && a.TryGetValue<string>(out var gespeichert)
            && gespeichert == Projekt))
        {
            var neu = Projekt;
            aendern(werte => werte[Abschlussprojekt.AktivSchluessel] = neu);
        }
        Felder = projekte.TryGetValue(Projekt, out var felder) ? new(felder, StringComparer.Ordinal) : new(StringComparer.Ordinal);
        MaxStunden = Abschlussprojekt.MaxStunden(Fachrichtung(alle));
        _stempel = Stempel();
        FrageNummer = 0;
        _eingaben.Clear();
        NeuGeladen?.Invoke();
        Geaendert?.Invoke();
    }

    /// <summary>Auswahlliste: (Kennung, Titel), das gezeigte Projekt auch dann, wenn es noch nicht gespeichert ist.</summary>
    public List<(string Kennung, string Titel)> Menue()
    {
        var projekte = Projekte();
        if (Projekt is not null && !projekte.ContainsKey(Projekt))
        {
            projekte.Add(Projekt, Felder);
        }
        return Abschlussprojekt.Menue(projekte)
            .Select(k => (k, Abschlussprojekt.Titel(k == Projekt ? Felder : projekte[k]))).ToList();
    }

    /// <summary>_choose_project.</summary>
    public void Waehlen(string projekt)
    {
        if (projekt != Projekt)
        {
            Laden(projekt);
        }
    }

    /// <summary>_new_project: speichern, neues Projekt mit leerem Titel anlegen und zeigen.</summary>
    public void NeuesProjekt()
    {
        Speichern();
        Laden(Abschlussprojekt.Anlegen(schreiber(), "", neueKennung));
        NachSpeichern?.Invoke();
    }

    /// <summary>Rueckfrage vor dem Loeschen (Titel, Text); der Satz haengt am eingerichteten Abgleich.</summary>
    public (string Titel, string Text) LoeschenFrage()
    {
        bool abgleich;
        try
        {
            abgleich = abgleichEingerichtet();
        }
        catch (Exception)
        {
            abgleich = true;       // im Zweifel der Text, der mehr sagt (fpj 219)
        }
        return (ProjektTexte.RueckfrageTitel, Abschlussprojekt.Rueckfrage(Felder, abgleich));
    }

    /// <summary>_delete_project nach "Ja": offene Eingaben verwerfen, loeschen, aktives Projekt neu.</summary>
    public void Loeschen()
    {
        _offen.Clear();
        _frist = null;
        if (Projekt is not null)
        {
            Abschlussprojekt.Loeschen(schreiber(), Projekt);
        }
        aendern(werte => werte.Remove(Abschlussprojekt.AktivSchluessel));
        Projekt = null;
        Laden();
        NachSpeichern?.Invoke();
    }

    /// <summary>use_template: speichern, Testprojekt als Vorlage (nur leere Felder), neu laden; Text fuer den Kurzhinweis.</summary>
    public string Vorlage(Lerninhalt testprojekt)
    {
        Speichern();
        var anzahl = Projekt is null ? 0 : Abschlussprojekt.VorlageUebernehmen(schreiber(), Projekt, testprojekt);
        Laden(Projekt);
        if (anzahl > 0)
        {
            NachSpeichern?.Invoke();
        }
        return Abschlussprojekt.VorlageHinweis(anzahl);
    }

    // -- Speichern -----------------------------------------------------------------------------

    /// <summary>
    /// Ein Eingabefeld anmelden: roh liefert den Text der Oberflaeche; gespeichert wird bei einem Feld
    /// box.get().strip(), bei einem Textfeld get("1.0", "end-1c").rstrip() (Pythons Leerraum).
    /// </summary>
    public void Eingabe(string feld, Func<string> roh, bool textfeld = false) =>
        _eingaben[feld] = textfeld ? () => Rechner.PythonZeichen.RStrip(roh()) : () => Rechner.PythonZeichen.Strip(roh());

    /// <summary>Wert, mit dem ein Feld startet (fields.get(feld, vorgabe)).</summary>
    public string Startwert(string feld, string vorgabe = "") => Felder.TryGetValue(feld, out var wert) ? wert : vorgabe;

    /// <summary>_changed: Feld ist offen, gespeichert wird 800 ms nach dem letzten Tastendruck.</summary>
    public void Getippt(string feld)
    {
        if (!_offen.Contains(feld))
        {
            _offen.Add(feld);
        }
        _frist = _uhr() + TimeSpan.FromMilliseconds(Abschlussprojekt.SpeicherVerzug);
    }

    /// <summary>Zeitgeber: ist die Frist erreicht, speichern. true: gespeichert wurde versucht.</summary>
    public bool Takt()
    {
        if (_frist is not { } frist || _uhr() < frist)
        {
            return false;
        }
        Speichern();
        return true;
    }

    /// <summary>_flush: geaenderte Felder speichern. true: etwas gespeichert.</summary>
    public bool Speichern()
    {
        _frist = null;
        if (_offen.Count == 0 || Projekt is null)
        {
            _offen.Clear();
            return false;
        }
        var felder = _offen.ToList();
        _offen.Clear();
        var geaendert = false;
        var titel = false;
        foreach (var feld in felder)
        {
            if (!_eingaben.TryGetValue(feld, out var lesen))
            {
                continue;
            }
            var wert = lesen();
            if (wert != Wert(feld))
            {
                schreiber().ProjektFeldSpeichern(Projekt, feld, wert);
                Felder[feld] = wert;
                geaendert = true;
                titel = titel || feld == Abschlussprojekt.TitelFeld;
            }
        }
        if (geaendert)
        {
            _stempel = Stempel();
            if (titel)
            {
                MenueGeaendert?.Invoke();
            }
            Geaendert?.Invoke();
            NachSpeichern?.Invoke();
        }
        return geaendert;
    }

    /// <summary>_set: sofort speichern (Haken, Fachbereich, Bewertung), nur wenn sich der Wert aendert.</summary>
    public bool Setzen(string feld, string wert)
    {
        if (Projekt is null || Wert(feld) == wert)
        {
            return false;
        }
        schreiber().ProjektFeldSpeichern(Projekt, feld, wert);
        Felder[feld] = wert;
        _stempel = Stempel();
        Geaendert?.Invoke();
        NachSpeichern?.Invoke();
        return true;
    }

    /// <summary>Haken einer Checkliste ("check_" + Schluessel, "1"/"0").</summary>
    public void Haken(string schluessel, bool an) => Setzen("check_" + schluessel, an ? "1" : "0");

    /// <summary>_set_category: Fachbereich ("Kein Fachbereich" = leer), Fragen beginnen wieder bei 1.</summary>
    public void FachbereichSetzen(string wert)
    {
        Setzen(Abschlussprojekt.FachbereichFeld, wert == ProjektTexte.KeinFachbereich ? "" : wert);
        FrageNummer = 0;
        FragenNeu?.Invoke();
    }

    /// <summary>Angezeigter Fachbereich der Auswahl.</summary>
    public string Fachbereich => Wert(Abschlussprojekt.FachbereichFeld) is { Length: > 0 } f ? f : ProjektTexte.KeinFachbereich;

    /// <summary>_on_tab: erst speichern, dann wechseln.</summary>
    public void ReiterWechseln(string reiter)
    {
        Speichern();
        Reiter = reiter;
        Geaendert?.Invoke();
    }

    /// <summary>
    /// on_show (beim Zeigen und nach dem Abgleich): ohne offene Felder und bei geaendertem Stempel neu
    /// laden, wenn sich die Felder des Projekts geaendert haben oder es weg ist. true: neu geladen.
    /// </summary>
    public bool Zeigen()
    {
        if (_offen.Count > 0)
        {
            return false;
        }
        var stempel = Stempel();
        if (stempel == _stempel)
        {
            return false;
        }
        var alle = schreiber().ProjektFelder();
        MaxStunden = Abschlussprojekt.MaxStunden(Fachrichtung(alle));
        var projekte = Abschlussprojekt.Projekte(alle);
        if (Projekt is null || !projekte.TryGetValue(Projekt, out var felder) || !Gleich(felder, Felder))
        {
            Laden(Projekt is not null && projekte.ContainsKey(Projekt) ? Projekt : null);
            return true;
        }
        _stempel = stempel;
        Geaendert?.Invoke();
        return false;
    }

    private static bool Gleich(OrderedDictionary<string, string> a, OrderedDictionary<string, string> b) =>
        a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var w) && w == p.Value);

    // -- Export --------------------------------------------------------------------------------

    /// <summary>Vor dem Export speichern; Vorschlag fuer den Dateinamen.</summary>
    public string ExportName(string endung)
    {
        Speichern();
        return Abschlussprojekt.ExportName(Felder, endung);
    }

    /// <summary>Inhalt der Datei: PDF (Datum im Fuss = heute) oder Text (PC mit BOM/CRLF, Handy ohne).</summary>
    public byte[] ExportDaten(string endung, bool handy, DateTime heute) => endung == "pdf"
        ? Abschlussprojekt.ExportPdf(Felder, MaxStunden, heute)
        : Abschlussprojekt.TextDatei(Abschlussprojekt.ExportText(Felder, MaxStunden), handy);

    // -- Angezeigte Werte ----------------------------------------------------------------------

    public string ErledigtText()
    {
        var (erledigt, gesamt) = Abschlussprojekt.Fortschritt(Felder);
        return Beschriftungen.Einsetzen(ProjektTexte.Erledigt, erledigt, gesamt);
    }

    public string AntragText()
    {
        var fehlt = Abschlussprojekt.AntragFehlt(Felder);
        return fehlt.Count > 0 ? Beschriftungen.Einsetzen(ProjektTexte.AntragOffen, string.Join(", ", fehlt)) : ProjektTexte.AntragVoll;
    }

    /// <summary>"Summe: %s von %d Stunden", rot ueber der Grenze.</summary>
    public (string Text, bool Rot) SummeText()
    {
        var summe = Abschlussprojekt.Zeitplan(Felder).Summe;
        return (Beschriftungen.Einsetzen(ProjektTexte.ZeitplanSumme, Abschlussprojekt.StundenText(summe), MaxStunden), summe > MaxStunden);
    }

    public string WarnungenText() => string.Join("\n", Abschlussprojekt.Warnungen(Felder, MaxStunden));

    public string KostenText() => string.Join("\n", Abschlussprojekt.Kostenzeilen(Felder));

    public (string Text, bool Rot) SeitenText() => Abschlussprojekt.SeitenText(Felder);

    // -- Fachgespraech -------------------------------------------------------------------------

    public List<ProjektFrage> AktuelleFragen() => Abschlussprojekt.FragenFuer(Felder, fragen);

    /// <summary>Aktuelle Frage und ihre Bewertung; null ohne Fragen.</summary>
    public (ProjektFrage Frage, bool? Bewertung)? AktuelleFrage()
    {
        var liste = AktuelleFragen();
        if (liste.Count == 0)
        {
            return null;
        }
        var frage = liste[FrageNummer % liste.Count];
        return (frage, Abschlussprojekt.Bewertung(Felder, frage.Id));
    }

    /// <summary>"Frage %d von %d · %d gewusst · %d nicht gewusst · %d offen".</summary>
    public string FragenStand()
    {
        var liste = AktuelleFragen();
        if (liste.Count == 0)
        {
            return "";
        }
        var werte = liste.Select(f => Abschlussprojekt.Bewertung(Felder, f.Id)).ToList();
        return Beschriftungen.Einsetzen(ProjektTexte.FragenStand, FrageNummer % liste.Count + 1, liste.Count,
            werte.Count(w => w == true), werte.Count(w => w == false), werte.Count(w => w is null));
    }

    /// <summary>toggle: Hinweis zeigen oder ausblenden.</summary>
    public void HinweisUmschalten() => HinweisOffen = !HinweisOffen;

    /// <summary>turn: naechste Frage, Hinweis zu.</summary>
    public void Weiter(int schritt = 1)
    {
        var anzahl = AktuelleFragen().Count;
        if (anzahl == 0)
        {
            return;
        }
        FrageNummer = ((FrageNummer + schritt) % anzahl + anzahl) % anzahl;
        HinweisOffen = false;
    }

    /// <summary>rate: Bewertung sofort speichern, dann weiter.</summary>
    public void Bewerten(bool gewusst)
    {
        if (AktuelleFrage() is not { } aktuell)
        {
            return;
        }
        Setzen("fg_" + aktuell.Frage.Id, gewusst ? "1" : "0");
        Weiter();
    }
}
