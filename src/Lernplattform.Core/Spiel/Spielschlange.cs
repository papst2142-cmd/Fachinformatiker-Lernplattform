using Lernplattform.Core.Diagnose;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Weisung P7-17 (Nico 26.10.2026, Weg b): die Spielschritte im Hintergrund speichern. Ein einziger
/// Schreiber arbeitet die Zeilen in fester Reihenfolge ab (zuerst angehaengt, zuerst geschrieben), jede in
/// ihrer eigenen Buchung wie vorher (synchronous bleibt FULL, kein WAL). Die Aktion gilt im Spiel sofort;
/// Zeitstempel und uid jeder Zeile stehen schon fest (<see cref="Daten.LernstandSchreiber.SpielMarke"/>),
/// darum sind Reihenfolge und Inhalt der Zeilen genau wie beim Schreiben im Oberflaechen-Faden.
/// Die Sperre der Vorgaenge haelt <see cref="SpielDatenbank"/>, bis ihre Zeilen geschrieben sind; vor
/// jedem anderen Vorgang wird geleert (<see cref="Leeren"/>, <see cref="AlleLeeren"/>). Ein Schreibfehler
/// wird nie still verworfen: die uebrigen Zeilen desselben Durchgangs bleiben ungeschrieben (sie bauen auf
/// der fehlenden auf), alles steht in fehler.log, und <see cref="Fehler"/> meldet es der Oberflaeche, die
/// den Spielstand aus der Datenbank neu aufbaut.
/// </summary>
public sealed class Spielschlange
{
    private static readonly List<WeakReference<Spielschlange>> Alle = [];

    private readonly Queue<Eintrag> offen = new();
    private Eintrag? laeuft;
    private bool arbeitet;

    public Spielschlange()
    {
        lock (Alle)
        {
            Alle.RemoveAll(w => !w.TryGetTarget(out _));
            Alle.Add(new WeakReference<Spielschlange>(this));
        }
    }

    /// <summary>Gemeinsame Sperre fuer die Schlange und den Zustand der Datenbanken, die in sie schreiben.</summary>
    internal object Sperre { get; } = new();

    /// <summary>Ein Schreibfehler (im Faden des Schreibers gemeldet).</summary>
    public event Action<SpielSchreibfehler>? Fehler;

    /// <summary>Nur fuer Tests: wird vor jeder Zeile im Faden des Schreibers gerufen (z. B. warten, Fehler ausloesen).</summary>
    internal Func<string, bool>? VorZeile { get; set; }

    internal sealed record Eintrag(SpielDatenbank Besitzer, string Art, Func<bool> Schreiben);

    /// <summary>Noch nicht geschriebene Zeilen (auch die gerade laufende).</summary>
    public int Anzahl
    {
        get
        {
            lock (Sperre)
            {
                return offen.Count + (laeuft is null ? 0 : 1);
            }
        }
    }

    /// <summary>Offene Zeilen eines Besitzers; nur unter <see cref="Sperre"/>.</summary>
    internal int Offen(SpielDatenbank besitzer)
    {
        var n = laeuft?.Besitzer == besitzer ? 1 : 0;
        foreach (var e in offen)
        {
            n += e.Besitzer == besitzer ? 1 : 0;
        }
        return n;
    }

    /// <summary>Eine Zeile anhaengen; nur unter <see cref="Sperre"/>.</summary>
    internal void Anhaengen(Eintrag eintrag)
    {
        offen.Enqueue(eintrag);
        if (!arbeitet)
        {
            arbeitet = true;
            ThreadPool.UnsafeQueueUserWorkItem(_ => Arbeiten(), null);
        }
    }

    /// <summary>Wartet, bis alle angehaengten Zeilen geschrieben (oder nach einem Fehler verworfen) sind.</summary>
    public void Leeren()
    {
        lock (Sperre)
        {
            while (offen.Count > 0 || laeuft is not null)
            {
                Monitor.Wait(Sperre);
            }
        }
    }

    /// <summary>
    /// Leert alle Schlangen des Programms: vor Sicherung, Ruecksicherung, Abgleich, Uebernahme, Update,
    /// Loeschen, beim Verlassen des Spiels, im Hintergrund (Android) und am Programmende.
    /// </summary>
    public static void AlleLeeren()
    {
        List<Spielschlange> lebend;
        lock (Alle)
        {
            lebend = [.. Alle.Select(w => w.TryGetTarget(out var s) ? s : null).OfType<Spielschlange>()];
        }
        foreach (var schlange in lebend)
        {
            schlange.Leeren();
        }
    }

    private void Arbeiten()
    {
        while (true)
        {
            Eintrag eintrag;
            lock (Sperre)
            {
                if (offen.Count == 0)
                {
                    arbeitet = false;
                    return;
                }
                eintrag = offen.Dequeue();
                laeuft = eintrag;
            }
            bool ok;
            string? ausnahme = null;
            try
            {
                ok = (VorZeile?.Invoke(eintrag.Art) ?? true) && eintrag.Schreiben();
            }
            catch (Exception fehler)
            {
                // Schreiben darf den Faden nie beenden; die Art des Fehlers kommt ins Protokoll
                ok = false;
                ausnahme = fehler.GetType().Name;
            }
            if (!ok)
            {
                SpielSchreibfehler gemeldet;
                lock (Sperre)
                {
                    // die uebrigen Zeilen dieses Durchgangs bauen auf der fehlenden auf: nicht schreiben
                    var rest = offen.ToList();
                    offen.Clear();
                    var verworfen = 0;
                    foreach (var e in rest)
                    {
                        if (e.Besitzer == eintrag.Besitzer)
                        {
                            verworfen++;
                        }
                        else
                        {
                            offen.Enqueue(e);
                        }
                    }
                    gemeldet = new SpielSchreibfehler(eintrag.Besitzer.Lauf, eintrag.Art, verworfen, ausnahme);
                    eintrag.Besitzer.Gestoert();
                }
                // gemeldet, bevor die Zeile als erledigt gilt: wer leert, sieht den Fehler schon
                try
                {
                    Fehlerprotokoll.Schreiben(gemeldet.Text(), Path.Combine(Path.GetDirectoryName(eintrag.Besitzer.Lernstand.Pfad)!, Fehlerprotokoll.DateiName));
                    Fehler?.Invoke(gemeldet);
                }
                catch (Exception)
                {
                    // Melden darf den Schreiber nie beenden (die Zeile ist schon als nicht geschrieben markiert)
                }
            }
            lock (Sperre)
            {
                laeuft = null;
                if (Offen(eintrag.Besitzer) == 0)
                {
                    eintrag.Besitzer.Geschrieben();
                }
                Monitor.PulseAll(Sperre);
            }
        }
    }
}

/// <summary>Weisung P7-17: eine Zeile des Spiels wurde nicht gespeichert (dazu die verworfenen danach).</summary>
public sealed record SpielSchreibfehler(string? Lauf, string Art, int Verworfen, string? Ausnahme)
{
    /// <summary>Eintrag fuer fehler.log (ohne Inhalt der Zeile).</summary>
    public string Text() =>
        $"Spiel: Spielschritt nicht gespeichert (Art {Art}{(Ausnahme is null ? "" : ", " + Ausnahme)}); {Verworfen} weitere Zeile(n) "
        + "desselben Durchgangs nicht geschrieben. Der Spielstand wird aus der Datenbank neu aufgebaut.";
}
