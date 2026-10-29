namespace Lernplattform.Core.Texte;

/// <summary>
/// Phase 9 W2 (Weisung P9-5 Punkt 6): neue Texte des Geruests Fachrichtungen, PC und Handy gleich.
/// Wortliste in Meldung P9-5; Chat entscheidet ueber den Wortlaut.
/// </summary>
public static class FachrichtungTexte
{
    /// <summary>AP2-Szenarien und Testprojekte, wenn die gewaehlte Fachrichtung (noch) keine eigenen hat.</summary>
    public const string KeineAufgaben = "Für deine Fachrichtung gibt es hier noch keine Aufgaben. Sie kommen mit den nächsten Lerninhalten.";

    /// <summary>Klausursimulation, Bereich b1/b2 ohne Aufgaben (ersetzt bei AE, DPA und DV den allgemeinen Platzhalter).</summary>
    public const string KlausurOhneAufgaben = "Für diesen Prüfungsbereich gibt es noch keine Aufgaben. Sie kommen mit den nächsten Lerninhalten.";

    /// <summary>Optionen &gt; Lerninhalte, erste Zeile des Zaehlteils (Sammelliste 54).</summary>
    public const string GezaehltFuer = "Gezählt für: %s";

    // Zaehlteil wie app_gui._build_lerninhalte v0.63.2 (7509-7524), Zahlen der gewaehlten Fachrichtung
    public const string KartenGesamt = "Karteikarten gesamt: %s";            // (Py) app_gui 7512
    public const string FragenGesamt = "Prüfungsfragen gesamt: %s";          // (Py) app_gui 7513
    public const string Ap1Gesamt = "AP1-Szenarien gesamt: %s";              // (Py) app_gui 7514
    public const string Ap2Gesamt = "AP2-Szenarien gesamt: %s";              // (Py) app_gui 7515
    public const string ProjekteGesamt = "Testprojekte gesamt: %s";          // (Py) app_gui 7517
    public const string JeFachbereich = "%s: %s Inhalte";                    // (Py) app_gui 7521

    /// <summary>Zaehlteil als Zeilen (Python: "\n".join(lines)), erste Zeile neu mit der Fachrichtung.</summary>
    public static string Zaehlteil(Inhalte.Lerninhalte inhalte, string fachrichtung)
    {
        static string D(int zahl) => Dashboard.DashboardTexte.DeInt(zahl);
        var zeilen = new List<string>
        {
            Beschriftungen.Einsetzen(GezaehltFuer, PruefungTexte.Fachrichtungen.First(f => f.Wert == fachrichtung).Text),
            "",
            Beschriftungen.Einsetzen(KartenGesamt, D(inhalte.Karteikarten.Count)),
            Beschriftungen.Einsetzen(FragenGesamt, D(inhalte.Quizfragen.Count)),
            Beschriftungen.Einsetzen(Ap1Gesamt, D(inhalte.Ap1Szenarien.Count)),
            Beschriftungen.Einsetzen(Ap2Gesamt, D(inhalte.Ap2Szenarien.Count)),
            Beschriftungen.Einsetzen(ProjekteGesamt, D(inhalte.Testprojekte.Count)),
            "",
        };
        var summen = inhalte.InhalteJeFachbereich();
        foreach (var fachbereich in Inhalte.Fachbereiche.Alle)
        {
            zeilen.Add(Beschriftungen.Einsetzen(JeFachbereich, Inhalte.Fachbereiche.Kurzname[fachbereich], D(summen.GetValueOrDefault(fachbereich))));
        }
        return string.Join("\n", zeilen);
    }
}
