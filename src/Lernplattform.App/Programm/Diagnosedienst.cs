using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lernplattform.Core.Diagnose;
using Update = Lernplattform.Aktualisierung.Aktualisierung;

namespace Lernplattform.Programm;

/// <summary>
/// Phase 5 W2: Leistungsmessung (BE7, PerfMonitor/MobilePerf), Herzschlag der Haenger-Diagnose und
/// Antworten auf einen zweiten Start (BE8, nur PC) in der Oberflaeche.
/// Ist die Messung aus, haengt nichts am Programm: kein Zeitgeber, keine Bindung, jede Messstelle
/// kehrt sofort zurueck, es entsteht keine Datei. Der Herzschlag laeuft nur, wenn die Haenger-Diagnose
/// eingerichtet ist (PC, Desktop/Program.cs). Nichts davon zeigt je einen Fehler.
/// </summary>
public static class Diagnosedienst
{
    private static readonly bool Pc = !OperatingSystem.IsAndroid();

    /// <summary>RESIZE_END_MS: so lange ohne neue Groesse = Zug mit der Maus zu Ende.</summary>
    public const int GroesseEndeMs = 600;

    private static Leistungsmessung? _messung;
    private static DispatcherTimer? _schreibUhr;
    private static DispatcherTimer? _herzUhr;
    private static DispatcherTimer? _einzelUhr;
    private static TopLevel? _fenster;
    private static Control? _ansicht;
    private static Groessenzug? _zug;
    private static Size? _groesse;
    private static bool _gebunden;

    /// <summary>true, sobald das Programm schliesst (closing() der Haenger-Diagnose und des Mehrfachstarts).</summary>
    public static bool Schliesst { get; private set; }

    /// <summary>Messdatei-Schreiber (fisi_leistung.Recorder); vor <see cref="Starten"/> eine ausgeschaltete Messung.</summary>
    public static Leistungsmessung Messung => _messung ??= Neu(false);

    private static Leistungsmessung Neu(bool aktiv)
    {
        string pfad;
        try
        {
            pfad = Leistungsmessung.StandardPfad();
        }
        catch (Exception)
        {
            pfad = Leistungsmessung.DateiName;
            aktiv = false;
        }
        var messung = new Leistungsmessung(Update.Anzeige, Pc ? "PC" : "Handy", pfad, aktiv, Pc ? Toolkit() : "");
        if (Pc)
        {
            // Spalte "vorgang": letzter Vorgang der Haenger-Diagnose (task_source)
            messung.Vorgangsquelle = () => Haenger.Aktuell().Vorgang;
        }
        return messung;
    }

    /// <summary>Spalte "toolkit" der Startzeile am PC (Python: customtkinter-Version).</summary>
    private static string Toolkit()
    {
        var version = typeof(Application).Assembly.GetName().Version;
        return version is null ? "Avalonia" : "Avalonia " + version.ToString(3);
    }

    /// <summary>Beim Start nach den Einstellungen: Messung nach dem gespeicherten Schalter.</summary>
    public static void Starten(System.Text.Json.Nodes.JsonObject werte)
    {
        _messung = Neu(Leistungsmessung.Eingeschaltet(werte));
    }

    /// <summary>
    /// Mit dem ersten Bild: Startzeile der Messung (mit Elementen), Herzschlag (PC mit Haenger-Diagnose)
    /// und Antworten auf einen zweiten Start (PC mit Sperre).
    /// </summary>
    public static void Verbinden(Control ansicht)
    {
        if (_ansicht is not null)
        {
            return;
        }
        _ansicht = ansicht;
        _fenster = TopLevel.GetTopLevel(ansicht);
        if (Messung.Aktiv)
        {
            Einschalten();
            Messung.Aufzeichnen(Leistungsmessung.EreignisStart, elemente: Elemente());
        }
        if (Haenger.Eingerichtet)
        {
            Haenger.Schliessend(() => Schliesst);
            _herzUhr = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Haenger.TaktMs) };
            _herzUhr.Tick += (_, _) => Herzschlag();
            _herzUhr.Start();
        }
        if (Einzelstart.Uebernehmen() is { } ordner)
        {
            _einzelUhr = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(Einzelstart.AbfrageMs) };
            _einzelUhr.Tick += (_, _) =>
            {
                try
                {
                    if (!Schliesst && Einzelstart.SignaleBeantworten(ordner, false))
                    {
                        NachVorn();
                    }
                }
                catch (Exception)
                {
                }
            };
            _einzelUhr.Start();
        }
    }

    /// <summary>
    /// on_close, zu Beginn: Messung schreiben und zweite Starts ab jetzt mit "endet" beantworten
    /// (auch waehrend des letzten Abgleichs, der den Oberflaechen-Faden belegt).
    /// </summary>
    public static void Beenden()
    {
        if (Schliesst)
        {
            return;
        }
        Schliesst = true;
        _herzUhr?.Stop();
        _einzelUhr?.Stop();
        _schreibUhr?.Stop();
        try
        {
            Einzelstart.Schliessen();
            Messung.Schreiben();
        }
        catch (Exception)
        {
        }
    }

    /// <summary>on_close nach dem letzten Abgleich (dump_on_close): lebt der Prozess 5 s danach noch, eine Zeile in haenger.log.</summary>
    public static void NachDemAbgleich()
    {
        try
        {
            if (Haenger.Eingerichtet)
            {
                Haenger.BeimSchliessen();
            }
        }
        catch (Exception)
        {
        }
    }

    /// <summary>_toggle_perf: Messung ein- oder ausschalten (der Schalter ist schon gespeichert).</summary>
    public static void Schalten(bool an)
    {
        if (an == Messung.Aktiv)
        {
            return;
        }
        if (!an)
        {
            Ausschalten();
        }
        Messung.AktivSetzen(an);
        if (an)
        {
            Einschalten();
        }
    }

    private static void Einschalten()
    {
        _schreibUhr ??= NeueSchreibUhr();
        _schreibUhr.Start();
        if (_ansicht is { } ansicht && !_gebunden)
        {
            ansicht.SizeChanged += GroesseGeaendert;
            ansicht.LayoutUpdated += Neuberechnet;
            _gebunden = true;
            _groesse = ansicht.Bounds.Size;
        }
    }

    private static void Ausschalten()
    {
        if (_zug is not null)
        {
            ZugBeenden();
        }
        _schreibUhr?.Stop();
        if (_ansicht is { } ansicht && _gebunden)
        {
            ansicht.SizeChanged -= GroesseGeaendert;
            ansicht.LayoutUpdated -= Neuberechnet;
            _gebunden = false;
        }
    }

    private static DispatcherTimer NeueSchreibUhr()
    {
        var uhr = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Leistungsmessung.PufferSekunden) };
        uhr.Tick += (_, _) => Messung.Schreiben();
        return uhr;
    }

    /// <summary>count_widgets: Elemente der Oberflaeche (nur bei eingeschalteter Messung gezaehlt).</summary>
    private static int? Elemente()
    {
        try
        {
            return _fenster is { } fenster ? fenster.GetVisualDescendants().Count() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    // -- Messstellen ----------------------------------------------------------

    /// <summary>Seitenwechsel bis die Seite gezeichnet ist (messart 2).</summary>
    public static void Seite(string von, string nach, double ms)
    {
        if (!Messung.Aktiv || von == nach)
        {
            return;
        }
        Messung.Aufzeichnen(Leistungsmessung.EreignisSeite, von, nach, ms, elemente: Elemente(), messart: Leistungsmessung.MessartGezeichnet);
    }

    /// <summary>Darstellungs- oder Schriftwechsel (theme): Dauer des Neuaufbaus, danach sofort schreiben.</summary>
    public static void Darstellung(string von, string nach, Action wechsel, string vorgang = "darstellung")
    {
        var vorher = Haenger.Vorgang(vorgang);
        var uhr = Stopwatch.StartNew();
        try
        {
            wechsel();
        }
        finally
        {
            Haenger.Vorgang(vorher);
        }
        if (!Messung.Aktiv)
        {
            return;
        }
        Messung.Aufzeichnen(Leistungsmessung.EreignisDarstellung, von, nach, uhr.Elapsed.TotalMilliseconds, elemente: Elemente());
        Messung.Schreiben();
    }

    /// <summary>_heartbeat: alle 500 ms; eine Verspaetung ab 2 s geht als "blockiert" in die Messung.</summary>
    private static void Herzschlag()
    {
        try
        {
            var verspaetung = Haenger.Herzschlag();
            if (verspaetung >= Haenger.MessSekunden && Messung.Aktiv)
            {
                Messung.Aufzeichnen(Leistungsmessung.EreignisBlockiert, nach: Haenger.Aktuell().Seite, dauerMs: verspaetung * 1000.0,
                    vorgang: Haenger.Kuerzlich(Haenger.Jetzt() - verspaetung - 1.0));
            }
        }
        catch (Exception)
        {
        }
    }

    // -- Fenstergroesse (je Zug mit der Maus eine Zeile) ------------------------

    private sealed class Groessenzug
    {
        public Size Von;
        public int Schritte;
        public int Ereignisse;
        public readonly List<double> Verzoegerungen = [];
        public long? Offen;
        public DispatcherTimer? Ende;
    }

    private static void Neuberechnet(object? sender, EventArgs e)
    {
        if (_zug is { } zug)
        {
            zug.Ereignisse++;
        }
    }

    private static void GroesseGeaendert(object? sender, SizeChangedEventArgs e)
    {
        var groesse = e.NewSize;
        if (_groesse is not { } alt || alt.Width <= 1)
        {
            _groesse = groesse;   // Fenster erscheint zum ersten Mal: kein Ziehen
            return;
        }
        if (groesse == alt)
        {
            return;
        }
        var zug = _zug ??= new Groessenzug { Von = alt };
        _groesse = groesse;
        zug.Schritte++;
        if (zug.Offen is null)
        {
            zug.Offen = Stopwatch.GetTimestamp();
            _fenster?.RequestAnimationFrame(_ =>
            {
                if (_zug is { Offen: { } seit } offen)
                {
                    offen.Verzoegerungen.Add(Stopwatch.GetElapsedTime(seit).TotalMilliseconds);
                    offen.Offen = null;
                }
            });
        }
        zug.Ende?.Stop();
        zug.Ende = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(GroesseEndeMs) };
        zug.Ende.Tick += (_, _) => ZugBeenden();
        zug.Ende.Start();
    }

    private static void ZugBeenden()
    {
        var zug = _zug;
        _zug = null;
        if (zug is null)
        {
            return;
        }
        zug.Ende?.Stop();
        var verzoegerungen = zug.Verzoegerungen.Count > 0 ? zug.Verzoegerungen : [0.0];
        static string Text(Size groesse) => ((int)groesse.Width).ToString(System.Globalization.CultureInfo.InvariantCulture) + "x"
            + ((int)groesse.Height).ToString(System.Globalization.CultureInfo.InvariantCulture);
        Messung.Aufzeichnen(Leistungsmessung.EreignisGroesse, Text(zug.Von), _groesse is { } jetzt ? Text(jetzt) : "",
            verzoegerungen.Average(), verzoegerungen.Max(), zug.Schritte, zug.Ereignisse, Elemente());
        Messung.Schreiben();
    }

    // -- Mehrfachstart: Fenster nach vorn (bring_to_front) -----------------------

    private static void NachVorn()
    {
        if (_fenster is not Window fenster)
        {
            return;
        }
        if (fenster.WindowState == WindowState.Minimized)
        {
            fenster.WindowState = WindowState.Normal;
        }
        fenster.Show();
        fenster.Activate();
        if (OperatingSystem.IsWindows())
        {
            // kurz "immer oben", damit es auch ohne Fokus ueber den anderen Fenstern liegt
            fenster.Topmost = true;
            Dispatcher.UIThread.Post(() => fenster.Topmost = false, DispatcherPriority.Background);
        }
    }

}
