using Lernplattform.Core.Diagnose;

namespace Lernplattform.Core.Daten;

/// <summary>
/// Phase 2, L10: wann abgeglichen wird, wie SyncController in Python v0.63.2 (app_gui.py PC,
/// mobile/src/main.py Handy). Automatisch nur, wenn eingerichtet und „Automatisch abgleichen“
/// an ist (auto_enabled): beim Start (PC nach 1,5 s, Handy sofort), nach dem Lernen (PC 60 s,
/// Handy 30 s; weitere Antworten in dieser Zeit verschieben den Abgleich nicht), beim Beenden
/// (PC, nur wenn seither gelernt wurde, höchstens 12 s warten) bzw. beim Verlassen der App
/// (Handy, nur wenn seither gelernt wurde). „Speichern und abgleichen“ gleicht immer ab.
/// Läuft schon ein Abgleich, folgt genau einer danach (_again). Der Abgleich selbst läuft im
/// Hintergrund; die Datenbank ist dabei nur kurz gesperrt, nie während des Netzes (P2-12 A1).
/// </summary>
public sealed class AbgleichSteuerung : IDisposable
{
    /// <summary>Wartezeiten je Gerät.</summary>
    public sealed record Zeiten(TimeSpan Start, TimeSpan NachDemLernen, TimeSpan Beenden);

    /// <summary>PC: root.after(1500, auto_start), DELAY_MS 60000, run_before_exit(timeout=12).</summary>
    public static readonly Zeiten Pc = new(TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(12));

    /// <summary>Handy: auto_start beim Start, DELAY 30, on_leave ohne Wartezeit.</summary>
    public static readonly Zeiten Handy = new(TimeSpan.Zero, TimeSpan.FromSeconds(30), TimeSpan.Zero);

    private readonly Zeiten _zeiten;
    private readonly Func<bool> _autoAn;
    private readonly Func<AbgleichErgebnis> _arbeit;
    private readonly Action<AbgleichErgebnis?, AbgleichAusnahme?> _fertig;
    private readonly Func<TimeSpan, Action, IDisposable> _planen;
    private readonly Action<Action> _hintergrund;
    private readonly Action<Exception> _protokoll;
    private readonly object _sperre = new();
    private readonly ManualResetEventSlim _ruhig = new(true);
    private IDisposable? _geplant;
    private bool _laeuft;
    private bool _nochmal;
    private bool _beendet;

    /// <param name="autoAn">auto_enabled: eingerichtet und sync_auto.</param>
    /// <param name="arbeit">Ein Abgleich (Abgleich.Ausfuehren); wirft AbgleichAusnahme.</param>
    /// <param name="fertig">Nach jedem Abgleich (Ergebnis oder Fehler), im Hintergrund-Faden.</param>
    /// <param name="planen">Zeitgeber: Aktion nach der Wartezeit; Dispose bricht ab. Null = System-Zeitgeber.</param>
    /// <param name="hintergrund">Startet den Abgleich im Hintergrund. Null = Thread-Pool.</param>
    /// <param name="protokoll">Unerwartete Fehler (gefiltert). Null = Fehlerprotokoll im Datenordner.</param>
    public AbgleichSteuerung(Zeiten zeiten, Func<bool> autoAn, Func<AbgleichErgebnis> arbeit,
        Action<AbgleichErgebnis?, AbgleichAusnahme?> fertig, Func<TimeSpan, Action, IDisposable>? planen = null,
        Action<Action>? hintergrund = null, Action<Exception>? protokoll = null)
    {
        _protokoll = protokoll ?? (e => Fehlerprotokoll.Schreiben(e));
        _zeiten = zeiten;
        _autoAn = autoAn;
        _arbeit = arbeit;
        _fertig = fertig;
        _planen = planen ?? Zeitgeber;
        _hintergrund = hintergrund ?? (a => Task.Run(a));
    }

    /// <summary>dirty: seit dem letzten Abgleich wurde gelernt.</summary>
    public bool Gelernt { get; private set; }

    /// <summary>Läuft gerade ein Abgleich?</summary>
    public bool Laeuft
    {
        get
        {
            lock (_sperre)
            {
                return _laeuft;
            }
        }
    }

    private static IDisposable Zeitgeber(TimeSpan nach, Action was) =>
        new Timer(_ => was(), null, nach, Timeout.InfiniteTimeSpan);

    /// <summary>auto_start: beim Programmstart.</summary>
    public void Starten()
    {
        if (!AutoAn())
        {
            return;
        }
        if (_zeiten.Start <= TimeSpan.Zero)
        {
            Abgleichen();
            return;
        }
        lock (_sperre)
        {
            _geplant?.Dispose();
            _geplant = _planen(_zeiten.Start, Geplant);
        }
    }

    /// <summary>schedule: nach einer gespeicherten Antwort. Ein schon geplanter Abgleich wird nicht verschoben.</summary>
    public void NachDemLernen()
    {
        lock (_sperre)
        {
            Gelernt = true;
            if (_geplant is not null || _beendet || !AutoAn())
            {
                return;
            }
            _geplant = _planen(_zeiten.NachDemLernen, Geplant);
        }
    }

    private void Geplant()
    {
        lock (_sperre)
        {
            _geplant?.Dispose();
            _geplant = null;
        }
        Abgleichen();
    }

    /// <summary>on_leave (Handy): App geht in den Hintergrund, offene Einträge hochladen.</summary>
    public void Verlassen()
    {
        if (Gelernt && AutoAn())
        {
            Abgleichen();
        }
    }

    /// <summary>run: jetzt abgleichen (Knopf oder Auslöser). Läuft schon einer, folgt genau einer danach.</summary>
    public void Abgleichen()
    {
        lock (_sperre)
        {
            if (_beendet)
            {
                return;
            }
            if (_laeuft)
            {
                _nochmal = true;
                return;
            }
            _laeuft = true;
            Gelernt = false;
            _ruhig.Reset();
        }
        try
        {
            _hintergrund(Arbeiten);
        }
        catch (Exception fehler) when (fehler is not OutOfMemoryException)
        {
            // Faden startet nicht: nichts laeuft, sonst bliebe der naechste Abgleich gesperrt (G-4)
            lock (_sperre)
            {
                _laeuft = false;
                _ruhig.Set();
            }
            _protokoll(fehler);
        }
    }

    private void Arbeiten()
    {
        AbgleichErgebnis? ergebnis = null;
        AbgleichAusnahme? fehler = null;
        try
        {
            ergebnis = _arbeit();
        }
        catch (AbgleichAusnahme e)
        {
            fehler = e;
        }
        catch (Exception e)
        {
            // Nie die App wegen des Abgleichs abstuerzen lassen; Einzelheiten nur gefiltert ins Fehlerprotokoll
            _protokoll(e);
            fehler = new AbgleichAusnahme(AbgleichFehler.Unerwartet);
        }
        bool nochmal;
        lock (_sperre)
        {
            _laeuft = false;
            nochmal = _nochmal && !_beendet;
            _nochmal = false;
            _ruhig.Set();
        }
        try
        {
            _fertig(ergebnis, fehler);
        }
        catch (Exception e)
        {
            _protokoll(e);
        }
        if (nochmal)
        {
            Abgleichen();
        }
    }

    /// <summary>
    /// run_before_exit (PC): wurde seit dem letzten Abgleich gelernt, erst auf einen laufenden
    /// Abgleich warten, dann einen letzten; nach der Wartezeit (12 s) wird nicht länger gewartet.
    /// Danach plant die Steuerung nichts mehr. Fehler bleiben still (_quietly).
    /// </summary>
    public void VorDemBeenden()
    {
        bool gelernt;
        lock (_sperre)
        {
            _beendet = true;
            _geplant?.Dispose();
            _geplant = null;
            gelernt = Gelernt;
        }
        if (!(gelernt && AutoAn()))
        {
            return;
        }
        var ende = DateTime.UtcNow + _zeiten.Beenden;
        if (!_ruhig.Wait(_zeiten.Beenden))
        {
            return;
        }
        var faden = new Thread(() =>
        {
            try
            {
                _arbeit();
            }
            catch (Exception e)
            {
                if (e is not AbgleichAusnahme)
                {
                    _protokoll(e);
                }
            }
        })
        { IsBackground = true, Name = "Abgleich beim Beenden" };
        faden.Start();
        var rest = ende - DateTime.UtcNow;
        faden.Join(rest > TimeSpan.Zero ? rest : TimeSpan.Zero);
    }

    private bool AutoAn()
    {
        try
        {
            return _autoAn();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        lock (_sperre)
        {
            _beendet = true;
            _geplant?.Dispose();
            _geplant = null;
        }
        _ruhig.Dispose();
    }
}
