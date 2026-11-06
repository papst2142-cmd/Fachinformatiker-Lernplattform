using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Patch S 0.5.1 (Weisung P12-37 Punkt 2): Knopf "Vollbild" und F11 am PC, gemerkt nach einem Neustart; am Tablet geht
/// der Wunsch an Avalonias InsetsManager (nachgestellt), damit die Leisten nach Neustart und Rueckkehr aus bleiben.
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Nachgestellter InsetsManager wie unter Android: Avalonia merkt sich den Wunsch und setzt ihn bei jedem
    /// Sichtbarwerden der Ansicht (Start, Rueckkehr aus dem Hintergrund); ohne Wunsch zeigt es die Leisten.</summary>
    private sealed class Rand
    {
        public bool? Gemerkt { get; set; }

        public bool LeistenNachSichtbarwerden => Gemerkt ?? true;
    }

    private static async Task VollbildAsync(Hauptansicht a, Window? f)
    {
        var ordner = StadtbauApp.Ordner;
        // Wunsch an das Fenster mitschreiben: unter Xvfb ohne Fensterverwaltung meldet X11 den Zustand nicht zurueck
        // (WindowState bleibt Normal), deshalb zaehlt, was das Werkzeug dem Fenster sagt
        var echt = WerkzeugVollbild.Fenster;
        bool? fenster = null;
        WerkzeugVollbild.Fenster = an =>
        {
            fenster = an;
            echt?.Invoke(an);
        };
        if (WerkzeugVollbild.An || WerkzeugEinstellungen.Vollbild(ordner))
        {
            Fund("Vollbild: beim ersten Start schon an");
        }
        a.VollbildUmschalten();
        await BildAsync(a);
        if (!WerkzeugVollbild.An || !WerkzeugEinstellungen.Vollbild(ordner))
        {
            Fund("Vollbild: Knopf schaltet nicht ein oder merkt es nicht");
        }
        if (f is not null && fenster != true)
        {
            Fund("Vollbild: das Fenster bekommt kein Vollbild");
        }
        Messwerte.Notiere("Vollbild: Fensterzustand laut System " + (f?.WindowState.ToString() ?? "(Tablet)"));
        foreach (var (name, b, h) in Groessen)
        {
            await GroesseAsync(a, f, b, h);
            Layout(a, "Vollbild " + name);
        }

        // Neustart: das neue Programm kennt nur die Datei
        WerkzeugVollbild.VergessenFuerTests();
        if (WerkzeugVollbild.An || (f is not null && fenster != false))
        {
            Fund("Vollbild: Probe Neustart setzt das Fenster nicht zurueck (Probe taugt nicht)");
        }
        StadtbauApp.VollbildBeimStart();
        if (!WerkzeugVollbild.An || (f is not null && fenster != true))
        {
            Fund("Vollbild: nach dem Neustart nicht wieder an");
        }

        // Tablet (nachgestellt): Neustart und Rueckkehr aus dem Hintergrund behalten "Leisten aus"
        var rand = new Rand();
        WerkzeugVollbild.WunschFuerTests = w => rand.Gemerkt = w;
        StadtbauApp.VollbildBeimStart();
        if (rand.LeistenNachSichtbarwerden)
        {
            Fund("Vollbild Tablet: Avalonia zeigt die Systemleisten nach Neustart oder Rueckkehr wieder");
        }
        // Gegenprobe: ohne Wunsch an Avalonia (wie die Lernplattform vor A 0.41.1) waeren sie wieder da
        var ohne = new Rand();
        WerkzeugVollbild.WunschFuerTests = w => ohne.Gemerkt = w;
        WerkzeugVollbild.LeistenUeberAvalonia = false;
        StadtbauApp.VollbildBeimStart();
        WerkzeugVollbild.LeistenUeberAvalonia = true;
        Messwerte.Notiere("Vollbild Tablet Gegenprobe (ohne InsetsManager): Leisten nach Rueckkehr " + (ohne.LeistenNachSichtbarwerden ? "sichtbar" : "aus"));
        if (!ohne.LeistenNachSichtbarwerden)
        {
            Fund("Vollbild Tablet Gegenprobe: ohne Wunsch an Avalonia trotzdem aus (Probe taugt nicht)");
        }
        WerkzeugVollbild.WunschFuerTests = null;

        // F11 schaltet wieder aus und merkt es
        if (TopLevel.GetTopLevel(a) is { } top)
        {
            top.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F11, Source = top });
        }
        await BildAsync(a);
        if (WerkzeugVollbild.An || WerkzeugEinstellungen.Vollbild(ordner) || (f is not null && fenster != false))
        {
            Fund("Vollbild: F11 schaltet nicht aus");
            WerkzeugVollbild.Setzen(false);
        }
        WerkzeugVollbild.Fenster = echt;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
    }
}
