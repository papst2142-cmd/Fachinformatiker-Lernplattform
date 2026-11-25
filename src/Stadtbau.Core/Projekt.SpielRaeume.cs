using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.60.0 (Weisung P12-337): die Raeume des Spiels (<see cref="Core.SpielRaeume"/>) im Projekt. Sie stehen nicht in
/// <see cref="Innenorte"/> (die Liste der Innenraeume der Stadt bleibt wie bisher), lassen sich aber mit ihrem Schluessel
/// ansehen: <see cref="Grundriss"/> und <see cref="Personen"/> liefern genau den Grundriss und die Personen, die das Spiel beim
/// Betreten baut.
/// Welle S 0.64.0 (Weisung P12-381, Nico 2c: „alles ist aenderbar, auch Innenwaende, Tueren und die Raumgroesse“): in einem
/// Projekt auf Stadt 1 (nicht leer, keine Paket-Stadt) laesst sich jeder Raum des Spiels aendern. Bearbeitet wird der Grundriss
/// <see cref="SpielRaumErsatz.Grundlage"/> (Quelle mit allen Zusaetzen, ohne Zustand eines Spielstands); er liegt wie jeder
/// Innenraum in den bearbeiteten Grundrissen (Schluessel „spiel:…“), jede Aenderung ist ein Schritt im Verlauf. Nur geaenderte
/// Raeume kommen als Ersatzstuecke in die Stadt-Datei (<see cref="SpielRaumStuecke"/>, Teil „stadtdaten/spielraeume“).
/// </summary>
public sealed partial class Projekt
{
    private readonly Dictionary<string, (string Text, Innenraum? Innen)> _spielRaeume = [];

    /// <summary>Alle Raeume des Spiels zu diesen Spieldaten (Wohnungen, Buero, Serverfarm, Zweigstelle, Kunden).</summary>
    public List<SpielRaeume.Raum> SpielRaeumeListe() => SpielRaeume.Alle(Daten.Inhalt, Innenraeume());

    /// <summary>Lassen sich in diesem Projekt Raeume des Spiels aendern? Nur Stadt 1 (keine leere Stadt, keine Paket-Stadt).</summary>
    public bool SpielRaeumeAenderbar => Leer is null && !StadtPaket.Hat(Aussen.Stadtdaten(AussenStand));

    /// <summary>Schluessel des Ersatzes zum Schluessel des Werkzeugs („spiel:zuhause:apartment“ = „zuhause:apartment“).</summary>
    public static string SpielRaumKennung(string schluessel) => schluessel[SpielRaeume.Praefix.Length..];

    /// <summary>Der unveraenderte Raum des Spiels so, wie das Werkzeug ihn bearbeitet (Kopie); null = gibt es nicht.</summary>
    public PyObjekt? SpielRaumOriginal(string schluessel) =>
        SpielRaeume.Ist(schluessel) ? SpielRaumErsatz.Grundlage(SpielRaumKennung(schluessel), Daten.Inhalt, Basis.Innenraeume) : null;

    /// <summary>Wird der Raum des Spiels gerade bearbeitet (Schalter „Ändern“ an oder schon geaendert)?</summary>
    public bool SpielRaumInBearbeitung(string schluessel) => SpielRaeume.Ist(schluessel) && _grundrisse.ContainsKey(schluessel);

    /// <summary>
    /// Schalter „Ändern“: ab jetzt zeigt das Werkzeug den bearbeitbaren Grundriss (<see cref="SpielRaumOriginal"/>, kein Schritt
    /// im Verlauf). Ohne Aenderung bleibt das Projekt bitgleich (es speichert nur geaenderte Raeume).
    /// </summary>
    public bool SpielRaumBearbeiten(string schluessel)
    {
        if (NurLesen || !SpielRaeumeAenderbar || SpielRaumOriginal(schluessel) is not { } g)
        {
            return false;
        }
        _grundrisse.TryAdd(schluessel, g);
        return true;
    }

    /// <summary>Schalter „Ändern“ aus: nur fuer einen unveraenderten Raum (danach zeigt das Werkzeug wieder den Raum wie im Spiel).</summary>
    public bool SpielRaumBeenden(string schluessel)
    {
        if (!SpielRaumInBearbeitung(schluessel) || GeaenderteSpielRaeume.Contains(schluessel))
        {
            return false;
        }
        return _grundrisse.Remove(schluessel);
    }

    /// <summary>Raeume des Spiels, deren Grundriss sich gegenueber dem Spiel geaendert hat (Reihenfolge der Liste).</summary>
    public IReadOnlyList<string> GeaenderteSpielRaeume =>
        !SpielRaeumeAenderbar ? []
        : [.. SpielRaeumeListe().Select(r => r.Schluessel).Where(s => _grundrisse.TryGetValue(s, out var g) && SpielRaumOriginal(s) is { } o
            && PythonJson.Standard(g) != PythonJson.Standard(o))];

    /// <summary>
    /// „Zurück auf den Stand des Spiels“ (Bild 04 P12-244): der Raum bekommt wieder den Grundriss des Spiels, ein Schritt im
    /// Verlauf (Rueckgaengig holt die Aenderung zurueck). Rueckgabe: ob sich etwas geaendert hat.
    /// </summary>
    public bool SpielRaumZuruecksetzen(string schluessel, string was)
    {
        if (!SpielRaumInBearbeitung(schluessel) || SpielRaumOriginal(schluessel) is not { } o)
        {
            return false;
        }
        return EtagenAendern(schluessel, was, (g, _) =>
        {
            var vorher = PythonJson.Standard(g);
            g.Clear();
            g.AddRange(o);
            return vorher != PythonJson.Standard(g);
        });
    }

    /// <summary>Die Ersatzstuecke aller geaenderten Raeume des Spiels (fuer Stand, Export und 3D).</summary>
    public List<SpielRaumErsatz.Stueck> SpielRaumStuecke() =>
        [.. GeaenderteSpielRaeume.Select(s => SpielRaumErsatz.Bauen(SpielRaumKennung(s), _grundrisse[s], Daten.Inhalt, Basis.Innenraeume))];

    /// <summary>Spielinhalte mit allen geaenderten Raeumen des Spiels (ohne Aenderung dasselbe Objekt).</summary>
    public PyObjekt SpielInhaltMitRaeumen() => SpielRaumErsatz.Inhalt(Daten.Inhalt, SpielRaumStuecke());

    /// <summary>Der Raum des Spiels, gebaut wie im Spiel (mit allen geaenderten Raeumen; neu gebaut nach jeder Aenderung).</summary>
    public Innenraum? SpielRaum(string schluessel)
    {
        var stuecke = SpielRaumStuecke();
        var text = stuecke.Count == 0 ? "" : PythonJson.Standard(stuecke.Select(SpielRaumErsatz.AlsObjekt).Cast<object?>().ToList());
        if (!_spielRaeume.TryGetValue(schluessel, out var vorrat) || vorrat.Text != text)
        {
            var innenraeume = SpielRaumErsatz.Innenraeume(Innenraeume(), stuecke);
            var inhalt = SpielRaumErsatz.Inhalt(Daten.Inhalt, stuecke);
            var innen = SpielRaeume.Finden(schluessel, inhalt, innenraeume) is { } raum
                ? SpielRaeume.Bauen(raum, inhalt, innenraeume, Daten.Grafik, Daten.Geschaefte.Daten, Daten.Feste)
                : null;
            vorrat = (text, innen);
            _spielRaeume[schluessel] = vorrat;
        }
        return vorrat.Innen;
    }

    /// <summary>Name eines Raums des Spiels fuer die Anzeige.</summary>
    public string SpielRaumName(string schluessel) =>
        SpielRaeume.Finden(schluessel, Daten.Inhalt, Innenraeume())?.Name ?? schluessel;

    private PyObjekt SpielRaumGrundriss(string schluessel) =>
        _grundrisse.TryGetValue(schluessel, out var g) ? g : SpielRaum(schluessel)?.Gebaeude ?? throw new KeyNotFoundException(schluessel);

    /// <summary>Gilt ein gespeicherter Schluessel „spiel:…“ in diesem Projekt (Oeffnen, Verlauf)?</summary>
    private bool SpielRaumGueltig(string schluessel) =>
        SpielRaeume.Ist(schluessel) && SpielRaeumeAenderbar && SpielRaumOriginal(schluessel) is not null;

    /// <summary>Stand mit dem Teil „stadtdaten/spielraeume“, wenn Raeume des Spiels geaendert sind (sonst unveraendert).</summary>
    private StadtFassung MitSpielRaeumen(StadtFassung f)
    {
        var stuecke = SpielRaumStuecke();
        if (stuecke.Count == 0)
        {
            return f;
        }
        var stadtdaten = (PyObjekt)Py.Kopie(f.Stadtdaten)!;
        Py.Setzen(stadtdaten, SpielRaumErsatz.Abschnitt, stuecke.Select(SpielRaumErsatz.AlsObjekt).Cast<object?>().ToList());
        return new StadtFassung
        {
            StadtId = f.StadtId, Nummer = f.Nummer, Basis = f.Basis, Werkzeug = f.Werkzeug, Erstellt = f.Erstellt, Nachricht = f.Nachricht,
            Karte = f.Karte, Stadtdaten = stadtdaten, Innenraeume = f.Innenraeume, Laternen = f.Laternen, Beete = f.Beete,
        };
    }
}
