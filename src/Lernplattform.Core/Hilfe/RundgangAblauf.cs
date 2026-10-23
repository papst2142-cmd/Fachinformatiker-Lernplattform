using System.Text.Json.Nodes;

namespace Lernplattform.Core.Hilfe;

/// <summary>Bedienelemente des Rundgangs (fuer die Tab-Reihenfolge).</summary>
public enum RundgangElement
{
    Name,
    AbschnittB,
    AbschnittD,
    AbschnittE,
    TerminAp1,
    TerminAp2,
    Ueberspringen,
    Zurueck,
    Weiter,
    Fertig,
}

/// <summary>
/// Phase 4, W4: Ablauf des Rundgangs wie app_gui.TourOverlay 0.63.2 (9055-9310), ohne Oberflaeche.
/// Seiten 1-5, der Entwurf der Einrichtung bleibt beim Blaettern erhalten (_keep_draft). Tab laeuft
/// ringfoermig wie die Knoepfe stehen (B-J-N2): Seiten 1-4 Weiter, Ueberspringen, Zurueck (nicht auf
/// Seite 1); Seite 5 Name, Schalter B/D/E, Termine, dann Ueberspringen, Zurueck, Speichern.
/// Eingabe im Textfeld = Speichern, Esc = Ueberspringen (beides setzt den Merker).
/// </summary>
public sealed class RundgangAblauf
{
    public RundgangAblauf(EinrichtenWerte start)
    {
        Name = start.Name;
        Abschnitte = new Dictionary<string, bool> { ["B"] = start.B, ["D"] = start.D, ["E"] = start.E };
        TerminAp1 = start.TerminAp1;
        TerminAp2 = start.TerminAp2;
    }

    public static int Anzahl => HilfeTexte.Seiten.Count;

    /// <summary>Seite 0 bis 4.</summary>
    public int Index { get; private set; }

    public bool Letzte => Index == Anzahl - 1;

    public RundgangSeite Seite => HilfeTexte.Seiten[Index];

    // Entwurf der Seite "Jetzt einrichten"
    public string Name { get; set; }

    public Dictionary<string, bool> Abschnitte { get; }

    public string TerminAp1 { get; set; }

    public string TerminAp2 { get; set; }

    /// <summary>Fehlertext nach einem misslungenen Speichern (Seite bleibt offen) oder null.</summary>
    public string? Fehler { get; private set; }

    /// <summary>Erstes falsches Datumsfeld (wird rot) oder null.</summary>
    public RundgangElement? FehlerFeld { get; private set; }

    /// <summary>Kopfzeile "RUNDGANG · SCHRITT 1 VON 5" (gross wie Python .upper()).</summary>
    public string Kopf => (RundgangTexte.Titel + " · " + RundgangTexte.SchrittText(Index + 1, Anzahl)).ToUpperInvariant();

    /// <summary>Tab-Reihenfolge der aktuellen Seite; das erste Element bekommt beim Zeigen den Fokus.</summary>
    public IReadOnlyList<RundgangElement> Reihenfolge => Letzte
        ? [RundgangElement.Name, RundgangElement.AbschnittB, RundgangElement.AbschnittD, RundgangElement.AbschnittE,
           RundgangElement.TerminAp1, RundgangElement.TerminAp2, RundgangElement.Ueberspringen, RundgangElement.Zurueck, RundgangElement.Fertig]
        : Index > 0
            ? [RundgangElement.Weiter, RundgangElement.Ueberspringen, RundgangElement.Zurueck]
            : [RundgangElement.Weiter, RundgangElement.Ueberspringen];

    /// <summary>Tab (vorwaerts) bzw. Umschalt+Tab (rueckwaerts), ringfoermig.</summary>
    public RundgangElement Naechstes(RundgangElement aktuell, bool rueckwaerts)
    {
        var liste = Reihenfolge;
        var i = Math.Max(0, IndexVon(liste, aktuell));
        return liste[(i + (rueckwaerts ? liste.Count - 1 : 1)) % liste.Count];
    }

    private static int IndexVon(IReadOnlyList<RundgangElement> liste, RundgangElement element)
    {
        for (var i = 0; i < liste.Count; i++)
        {
            if (liste[i] == element)
            {
                return i;
            }
        }
        return -1;
    }

    public bool Weiter()
    {
        if (Letzte)
        {
            return false;
        }
        Index++;
        FehlerWeg();
        return true;
    }

    public bool Zurueck()
    {
        if (Index == 0)
        {
            return false;
        }
        Index--;
        FehlerWeg();
        return true;
    }

    /// <summary>Seite direkt zeigen (Start ohne Bedienung, Bilder).</summary>
    public void Springen(int index)
    {
        Index = Math.Clamp(index, 0, Anzahl - 1);
        FehlerWeg();
    }

    /// <summary>Ein Fehler gilt nur fuer die Seite 5, auf der er entstand (Gegenpruefung W4 F1).</summary>
    private void FehlerWeg()
    {
        Fehler = null;
        FehlerFeld = null;
    }

    /// <summary>
    /// "Speichern und loslegen" (finish): auf Seiten 1-4 wie Weiter (null). Auf Seite 5 die Aenderung fuer einen
    /// Schreibvorgang mit Merker; bei einem ungueltigen Datum null, Fehler und FehlerFeld gesetzt.
    /// </summary>
    public Action<JsonObject>? Fertig(DateOnly heute)
    {
        if (!Letzte)
        {
            Weiter();
            return null;
        }
        var (fehler, aenderung) = Einrichten.Speichern(Name, Abschnitte, TerminAp1, TerminAp2, heute);
        Fehler = fehler;
        FehlerFeld = fehler is null ? null
            : Einrichten.PruefeDatum(TerminAp1).Fehler is not null ? RundgangElement.TerminAp1 : RundgangElement.TerminAp2;
        return aenderung;
    }

    /// <summary>Speichern misslang (Datei gesperrt): N9 zeigen, der Rundgang bleibt offen.</summary>
    public void SpeichernFehlgeschlagen()
    {
        Fehler = Texte.Optionen.SpeichernFehlgeschlagen;
        FehlerFeld = null;
    }

    /// <summary>"Ueberspringen", Esc, Zurueck-Geste: nur der Merker.</summary>
    public static Action<JsonObject> Ueberspringen(DateOnly heute) => werte => Einrichten.RundgangMerken(werte, heute);
}
