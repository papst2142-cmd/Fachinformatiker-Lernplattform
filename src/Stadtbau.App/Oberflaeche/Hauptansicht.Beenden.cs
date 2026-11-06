using Avalonia.Controls;
using Avalonia.Threading;
using Lernplattform.Views;
using Stadtbau.Core;

using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-35 Punkt 1, Nico: "Es gibt keine beenden funktion"): Knopf "Werkzeug beenden" in der
/// Kopfleiste ganz rechts. Am PC schliesst er das Fenster wie das Fensterkreuz (beide laufen ueber
/// <see cref="BeendenPruefenAsync"/>), unter Android endet die App wirklich (<see cref="StadtbauApp.GeraetBeenden"/>).
/// Vorher wird die Arbeit im Hintergrund (Pruefung, Teilbau der 3D-Ansicht) abgewartet; ist eine Aenderung nicht
/// gespeichert (das sofortige Speichern scheiterte), fragt das Werkzeug nach.
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>Laenger wartet "Werkzeug beenden" nicht auf die Arbeit im Hintergrund (sie schreibt nichts in das Projekt).</summary>
    public static readonly TimeSpan BeendenWartetHoechstens = TimeSpan.FromSeconds(20);

    /// <summary>Schliessen der Plattform: am PC das Fenster (wie das Kreuz), unter Android das Ende der App mit Pruefung.</summary>
    public Action? Schliessen { get; set; }

    /// <summary>Nur Start-Test und Fotos: so tun, als sei eine Aenderung nicht gespeichert.</summary>
    internal static bool UngespeichertFuerTests { get; set; }

    private readonly HashSet<Task> _hintergrund = [];
    private bool _beendenLaeuft;

    /// <summary>Knopf "Werkzeug beenden" (Start-Test).</summary>
    public Button BeendenKnopfElement => _beenden;

    /// <summary>Alle Knoepfe der Kopfleiste mit Beschriftung (Start-Test: jeder hat einen Tipp).</summary>
    public IEnumerable<(string Text, Button Knopf)> KopfKnoepfe =>
        _kopf.Select(k => (k.Key, k.Value)).Append((Texte.WerkzeugBeenden, _beenden));

    /// <summary>Text der Zeile unten (Start-Test).</summary>
    public string StatusText => _status.Text ?? "";

    /// <summary>Gibt es eine Aenderung, die noch nicht in der Projektdatei steht?</summary>
    public bool Ungespeichert => UngespeichertFuerTests || _projekt?.Ungespeichert == true;

    /// <summary>Laufende Arbeit im Hintergrund (Pruefung, 3D-Teilbau).</summary>
    public int HintergrundOffen => _hintergrund.Count(t => !t.IsCompleted);

    /// <summary>Aufgabe im Hintergrund merken, damit "Werkzeug beenden" sie abwartet.</summary>
    private Task<T> Hintergrund<T>(Task<T> aufgabe)
    {
        _hintergrund.Add(aufgabe);
        aufgabe.ContinueWith(_ => Dispatcher.UIThread.Post(() => _hintergrund.Remove(aufgabe)), TaskScheduler.Default);
        return aufgabe;
    }

    private void BeendenKnopf()
    {
        if (Schliessen is { } s)
        {
            s();
        }
        else
        {
            _ = BeendenAsync(StadtbauApp.GeraetBeenden);
        }
    }

    /// <summary>Pruefen und dann <paramref name="ende"/> rufen (Android und ohne Fenster).</summary>
    public async Task BeendenAsync(Action? ende)
    {
        if (await BeendenPruefenAsync())
        {
            ende?.Invoke();
        }
    }

    /// <summary>
    /// Darf das Werkzeug jetzt enden? Wartet die Arbeit im Hintergrund ab (mit Hinweis), dann bei einer nicht
    /// gespeicherten Aenderung die Rueckfrage "Speichern und beenden" / "Ohne Speichern beenden" / "Abbrechen".
    /// Scheitert das Speichern, bleibt das Werkzeug offen.
    /// </summary>
    public async Task<bool> BeendenPruefenAsync()
    {
        if (_beendenLaeuft)
        {
            return false;
        }
        _beendenLaeuft = true;
        try
        {
            var offen = _hintergrund.Where(t => !t.IsCompleted).ToArray();
            if (offen.Length > 0)
            {
                using (Dialog.Warten(Texte.BeendenWartet))
                {
                    await Task.WhenAny(Task.WhenAll(offen), Task.Delay(BeendenWartetHoechstens));
                }
            }
            if (!Ungespeichert)
            {
                return true;
            }
            var wahl = await Dialog.Zeigen(Texte.BeendenTitel, Texte.BeendenFrage, Texte.SpeichernUndBeenden, Texte.OhneSpeichernBeenden, Texte.Abbrechen);
            if (wahl == 1)
            {
                return true;
            }
            if (wahl != 0)
            {
                return false;
            }
            try
            {
                _projekt?.Speichern(vonHand: true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                _ordner.Fehler("Speichern beim Beenden: " + e.Message);
                await Dialog.Zeigen(Texte.BeendenTitel, Texte.BeendenSpeichernFehler + e.Message, Texte.Ok);
                return false;
            }
            return !(_projekt?.Ungespeichert == true);
        }
        finally
        {
            _beendenLaeuft = false;
        }
    }

    private readonly List<Control> _gruppen = [];

    /// <summary>Gruppen der Liste links mit Tipp (Start-Test: jede hat einen).</summary>
    public IReadOnlyList<Control> GruppenMitTipp => _gruppen;

    /// <summary>Patch S 0.5.1 (Weisung P12-35 Punkt 2): Gruppe der Liste links mit einem Satz als Tipp.</summary>
    private T Gruppe<T>(T c, string tipp) where T : Control
    {
        _gruppen.Add(c);
        return c.Mit(tipp);
    }
}
