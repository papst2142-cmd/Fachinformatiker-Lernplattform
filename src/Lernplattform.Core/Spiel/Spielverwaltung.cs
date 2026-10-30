using Lernplattform.Core.Daten;
using Microsoft.Data.Sqlite;
using static Lernplattform.Core.Spiel.PyFormat;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Phase 7, Weisung P7-6 (Sammelliste 94; R3): die Platzverwaltung der Klasse <c>Game</c> aus
/// <c>fisi_game.py</c> (Python 0.63.2, Z. 5056-5240 und 6008-6020): aktiven Durchgang bestimmen
/// (_resolve_run), neuen Durchgang auf einem Platz anlegen (new_run, mit Doppel-Tipp), zum Schreiben
/// einen Durchgang sichern (_ensure_run), waehlen (select_run), umbenennen (rename_slot), loeschen
/// (delete_run, vorher kommt der Endstand in die Bestenliste) und zuruecksetzen (reset).
/// Jeder Durchgang ist ein <see cref="Spielablauf"/> ueber <see cref="SpielDatenbank"/>. Geschrieben
/// wird nur ueber <see cref="LernstandSchreiber"/> und nur mit der Sperre der <see cref="Vorgaenge"/>
/// (ohne Warten, belegt = nichts schreiben, <see cref="SpielBelegtAusnahme"/>); eine Aktion haelt die
/// Sperre ueber alle ihre Schritte. Abweichung: Ist die Sperre belegt, wenn der aktive Durchgang nur
/// bestimmt wird (Start, Neuladen), merkt er sich ihn nicht (sync_meta); das holt das naechste
/// Bestimmen nach. Neuladen beim selben Durchgang behaelt den Spielablauf (wie Python reload).
/// Nicht threadsicher (wie Game in Python).
/// </summary>
public sealed class Spielverwaltung
{
    private readonly LernstandSchreiber schreiber;
    private readonly Func<string> laufKennung;
    private readonly Func<IDisposable?> holen;
    private IDisposable? gehalten;
    private Spielstand leer;
    private string? ablaufLauf;

    /// <param name="laufKennung">uuid4().hex fuer neue Durchgaenge (Vorgabe: zufaellig; Tests geben feste vor).</param>
    /// <param name="sperre">Wie die Sperre geholt wird (Vorgabe <see cref="Vorgaenge.Versuchen"/>, ohne Warten).</param>
    /// <param name="schlange">Weisung P7-17: die Spielschritte im Hintergrund schreiben (null: sofort, wie vorher).
    /// Eigene Schreib- und Lesezugriffe der Verwaltung leeren sie vorher.</param>
    public Spielverwaltung(LernstandSchreiber schreiber, string geraet, PyObjekt inhalt, Func<string>? laufKennung = null,
        Func<IDisposable?>? sperre = null, Action<Spielablauf>? einrichten = null, Spielschlange? schlange = null)
    {
        Schlange = schlange;
        this.schreiber = schreiber;
        Geraet = geraet;
        Inhalt = inhalt;
        this.laufKennung = laufKennung ?? LernstandSchreiber.NeueKennung;
        holen = sperre ?? Vorgaenge.Versuchen;
        Einrichten = einrichten;
        leer = new Spielstand([], inhalt);
        LaufBestimmen();
        Neuladen();
    }

    public string Geraet { get; }

    /// <summary>Weisung P7-17: die Schlange der Spielschritte (null: es wird sofort geschrieben).</summary>
    public Spielschlange? Schlange { get; }

    public PyObjekt Inhalt { get; }

    /// <summary>Wird fuer jeden neuen Spielablauf aufgerufen (z. B. Wissen aus dem Lernstand setzen).</summary>
    public Action<Spielablauf>? Einrichten { get; }

    /// <summary>run: der aktive Durchgang (null: keiner gewaehlt).</summary>
    public string? Lauf { get; private set; }

    /// <summary>Der Spielablauf des aktiven Durchgangs (null ohne Durchgang).</summary>
    public Spielablauf? Ablauf { get; private set; }

    /// <summary>state: der Spielstand des aktiven Durchgangs (ohne Durchgang ein leerer).</summary>
    public Spielstand Stand => Ablauf?.Stand ?? leer;

    // -- Lesen ---------------------------------------------------------------

    private T Lesen<T>(Func<SqliteConnection, T> lesen, T vorgabe)
    {
        Schlange?.Leeren();
        try
        {
            using var verbindung = schreiber.Lernstand.Verbindung();
            return lesen(verbindung);
        }
        catch (SqliteException)
        {
            return vorgabe;   // Python: _execute(default=...)
        }
    }

    /// <summary>layout</summary>
    public PlatzBelegung Belegung() =>
        Lesen(v => Spielstaende.Belegung(Spielstaende.Zeilen(v), Spielstaende.HatAlteEreignisse(v)), Spielstaende.Belegung([], false));

    private string? Gespeichert() => Lesen(Spielstaende.GespeicherterLauf, null);

    /// <summary>slot_of: der Platz eines Durchgangs (Vorgabe der aktive), null = auf keinem Platz.</summary>
    public long? PlatzVon(string? lauf = null)
    {
        lauf ??= Lauf;
        foreach (var (platz, eintrag) in Belegung().Plaetze)
        {
            if (eintrag == lauf)
            {
                return platz;
            }
        }
        return null;
    }

    /// <summary>slot_name</summary>
    public string PlatzName(long platz, PlatzBelegung? belegung = null) => Spielstaende.PlatzName(platz, belegung ?? Belegung());

    /// <summary>active_label: kurze Platzanzeige fuer den Kopf der Spielansichten ("" ohne Platz).</summary>
    public string AktivAnzeige()
    {
        if (PlatzVon() is not { } platz)
        {
            return "";
        }
        var name = PlatzName(platz);
        return name == Spielstaende.Standardname(platz) ? name : Prozent("%s · %s", Spielstaende.Standardname(platz), name);
    }

    /// <summary>slot_options_text: Zeile zum Schwierigkeitsgrad in den Optionen, mit dem aktiven Platz (Phase 9 W4).</summary>
    public string OptionenZeile()
    {
        var platz = AktivAnzeige();
        var text = Spielstaende.SchwierigkeitOptionen(Stand);
        return platz.Length > 0 ? Prozent("%s · %s", platz, text) : text;
    }

    /// <summary>slots: die drei Plaetze fuer den Auswahlbildschirm, dazu seltene Extras.</summary>
    public List<PyObjekt> Plaetze() => Lesen(v => Spielstaende.Uebersicht(v, Lauf, Inhalt), []);

    // -- Durchgang bestimmen und laden ---------------------------------------

    /// <summary>
    /// reload: den Spielstand aus dem Protokoll neu rechnen. Hat der Durchgang keine Ereignisse und
    /// gibt es ihn nicht mehr (z. B. auf dem anderen Geraet geloescht), wird der aktive neu bestimmt.
    /// </summary>
    public Spielstand Neuladen()
    {
        var ablauf = Lauf is null ? null : Ablauf is not null && ablaufLauf == Lauf ? Ablauf : Neu(Lauf);
        if (ablauf is not null && ablauf == Ablauf)
        {
            ablauf.Neuaufbau();
        }
        if ((ablauf is null || ablauf.Stand.Verlauf.Count == 0) && (Lauf is null || !Belegung().Bekannt.Contains(Lauf)))
        {
            LaufBestimmen();
            ablauf = Lauf is null ? null : Neu(Lauf);
        }
        Ablauf = ablauf;
        ablaufLauf = Lauf;
        leer = new Spielstand([], Inhalt);
        return Stand;
    }

    private Spielablauf Neu(string lauf)
    {
        var ablauf = new Spielablauf(Inhalt, new SpielDatenbank(schreiber, Geraet, lauf, Sperre, Schlange));
        Einrichten?.Invoke(ablauf);
        return ablauf;
    }

    /// <summary>
    /// Weisung P7-17: eigene Schreibzugriffe der Verwaltung erst, wenn die Spielschritte davor geschrieben
    /// sind (dieselbe Reihenfolge der Zeilen wie beim sofortigen Schreiben).
    /// </summary>
    private LernstandSchreiber Direkt()
    {
        Schlange?.Leeren();
        return schreiber;
    }

    /// <summary>Sperre fuer einen Spielablauf: haelt die Verwaltung sie schon (laufende Aktion), gilt sie mit.</summary>
    private IDisposable? Sperre() => gehalten is not null ? new Mit() : holen();

    /// <summary>_resolve_run: der zuletzt auf diesem Geraet gewaehlte Durchgang, sonst der auf dem kleinsten Platz.</summary>
    private void LaufBestimmen()
    {
        var gespeichert = Gespeichert();
        Lauf = Spielstaende.AktiverLauf(string.IsNullOrEmpty(gespeichert) ? null : gespeichert, Belegung());
        if ((Lauf ?? "") != (gespeichert ?? ""))
        {
            var lauf = Lauf ?? "";
            try
            {
                Halten(() => Direkt().SpielAktivMerken(lauf));
            }
            catch (SpielBelegtAusnahme)
            {
                // nur ein Merker: belegt = nicht merken, das naechste Bestimmen holt es nach
            }
        }
    }

    // -- Schreiben -----------------------------------------------------------

    /// <summary>Eine Aktion mit der Sperre der Vorgaenge (geschachtelt: nur die aeusserste holt sie).</summary>
    private T Halten<T>(Func<T> aktion)
    {
        if (gehalten is not null)
        {
            return aktion();
        }
        // Weisung P7-17: erst alle Spielschritte schreiben (bis dahin haelt das Spiel die Sperre)
        Schlange?.Leeren();
        gehalten = holen() ?? throw new SpielBelegtAusnahme();
        try
        {
            return aktion();
        }
        finally
        {
            // Zeilen, die das Spiel in dieser Aktion angehaengt hat, noch mit dieser Sperre schreiben
            Schlange?.Leeren();
            var frei = gehalten;
            gehalten = null;
            frei.Dispose();
        }
    }

    /// <summary>select_run: einen Durchgang spielen (Platz waehlen).</summary>
    public Spielstand LaufWaehlen(string lauf) => Halten(() =>
    {
        if (!Belegung().Bekannt.Contains(lauf))
        {
            throw new SpielFehler("Diesen Spielstand gibt es nicht mehr.");
        }
        Lauf = lauf;
        Direkt().SpielAktivMerken(lauf);
        Ablauf?.Freigeschaltet.Clear();   // self.unlocked = []
        return Neuladen();
    });

    /// <summary>
    /// new_run: einen neuen, leeren Durchgang auf einem freien Platz anlegen und waehlen. Doppelt
    /// getippt (der Platz ist eben erst angelegt, noch ohne Spielfigur): derselbe Durchgang.
    /// </summary>
    public string NeuerLauf(long platz) => Halten(() =>
    {
        var belegung = Belegung();
        if (Lauf is not null && belegung.LaufAuf(platz) == Lauf && Stand.Profil is null)
        {
            return Lauf;
        }
        if (belegung.LaufAuf(platz) is not null)
        {
            throw new SpielFehler(Prozent("%s ist schon belegt.", PlatzName(platz, belegung)));
        }
        if (platz < 1 || platz > Spielstaende.Anzahl)
        {
            throw new SpielFehler("Diesen Platz gibt es nicht.");
        }
        var lauf = "l" + laufKennung()[..16];
        Direkt().SpielPlatzSpeichern(platz, lauf, Spielstaende.Angelegt, "{}", Geraet);
        LaufWaehlen(lauf);
        return lauf;
    });

    /// <summary>
    /// _ensure_run und danach der Spielablauf zum Schreiben: ohne gewaehlten Platz der aktive,
    /// sonst ein neuer Durchgang auf dem ersten freien Platz. Python ruft das vor jedem Ereignis
    /// (_log); hier ruft es die Oberflaeche vor einer Aktion des Spiels.
    /// </summary>
    public Spielablauf ZumSchreiben()
    {
        if (Lauf is null)
        {
            Halten(() =>
            {
                LaufBestimmen();
                if (Lauf is not null)
                {
                    Neuladen();
                    return true;
                }
                var belegung = Belegung();
                var frei = Enumerable.Range(1, Spielstaende.Anzahl).Select(n => (long)n).Where(n => belegung.LaufAuf(n) is null).ToList();
                if (frei.Count == 0)
                {
                    throw new SpielFehler("Bitte zuerst einen Spielstand wählen.");
                }
                NeuerLauf(frei[0]);
                return true;
            });
        }
        return Ablauf!;
    }

    /// <summary>rename_slot: der Name wird beschnitten und auf 24 Zeichen gekuerzt; leer = Standardname.</summary>
    public bool PlatzUmbenennen(long platz, string? name)
    {
        var text = Rechner.PythonZeichen.Strip(name ?? "");
        text = string.Concat(text.EnumerateRunes().Take(Spielstaende.NameMax).Select(r => r.ToString()));
        var daten = PythonJson.Standard(new PyObjekt { { "name", text } });
        return Halten(() => Direkt().SpielPlatzSpeichern(platz, "", Spielstaende.Umbenannt, daten, Geraet));
    }

    /// <summary>
    /// delete_run: einen Durchgang loeschen (der Platz wird frei). Vorher kommt sein Endstand
    /// vollstaendig in die Bestenliste. Ein eigener Name des Platzes faellt dabei weg (ausser
    /// <paramref name="nameBehalten"/>, beim Zuruecksetzen).
    /// </summary>
    public bool LaufLoeschen(string lauf, bool nameBehalten = false) => Halten(() =>
    {
        var belegung = Belegung();
        var platz = belegung.Plaetze.Where(p => p.Lauf == lauf).Select(p => p.Platz).FirstOrDefault();
        if (!belegung.Bekannt.Contains(lauf))
        {
            throw new SpielFehler("Diesen Spielstand gibt es nicht mehr.");
        }
        var vorher = Lauf;
        if (lauf != vorher)
        {
            LaufWaehlen(lauf);
        }
        Ablauf!.ErfolgePruefen(erzwingen: true);
        var ok = Direkt().SpielLaufLoeschen(platz, lauf, Geraet);
        if (platz != 0 && !nameBehalten && belegung.Namen.Any(n => n.Platz == platz))
        {
            PlatzUmbenennen(platz, "");
        }
        if (lauf != vorher && vorher is not null && Belegung().Bekannt.Contains(vorher))
        {
            LaufWaehlen(vorher);
        }
        else
        {
            Lauf = null;
            Direkt().SpielAktivMerken("");
            Ablauf = null;
            ablaufLauf = null;
            leer = new Spielstand([], Inhalt);
        }
        return ok;
    });

    /// <summary>
    /// reset: den aktiven Spielstand zuruecksetzen. Der Durchgang endet (seine Bestwerte kommen in
    /// die Bestenliste), auf demselben Platz beginnt ein neuer; der Name des Platzes bleibt.
    /// </summary>
    public bool Zuruecksetzen()
    {
        if (Lauf is null)
        {
            Neuladen();
            return true;
        }
        return Halten(() =>
        {
            var platz = PlatzVon() ?? 1;
            var ok = LaufLoeschen(Lauf, nameBehalten: true);
            if (Belegung().LaufAuf(platz) is null)
            {
                NeuerLauf(platz);
            }
            else
            {
                Neuladen();
            }
            return ok;
        });
    }

    private sealed class Mit : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
