namespace Lernplattform.Tests;

/// <summary>
/// Sammelliste 453: lange Pruefungen in Tests nebeneinander rechnen, auf eigenen Faeden statt im Thread-Pool. So bleiben die
/// Pool-Faeden frei fuer andere Tests, die gleichzeitig laufen (z. B. Abgleich ueber die GitHub-Attrappe mit 10 s Wartezeit:
/// ein voller Pool liess dort eine Antwort zu spaet ankommen). Jeder Index wird genau einmal gerechnet; der erste Fehler wird
/// nach dem Ende aller Faeden weitergeworfen.
/// </summary>
internal static class Nebeneinander
{
    /// <summary>
    /// So viele Faeden hoechstens: die Haelfte der Kerne, mindestens zwei. Die andere Haelfte bleibt fuer die Tests, die xUnit
    /// gleichzeitig laufen laesst (sonst wuerden sie entsprechend langsamer).
    /// </summary>
    internal static int Faeden => Math.Max(2, Environment.ProcessorCount / 2);

    internal static void Fuer(int anzahl, Action<int> rumpf) => Fuer(anzahl, () => 0, (i, _) => rumpf(i));

    /// <summary>Wie oben, mit einem eigenen Zustand je Faden (z. B. wiederverwendete Puffer).</summary>
    internal static void Fuer<T>(int anzahl, Func<T> anfang, Action<int, T> rumpf)
    {
        var naechster = -1;
        Exception? fehler = null;
        void Arbeiten()
        {
            try
            {
                var zustand = anfang();
                int i;
                while (Volatile.Read(ref fehler) is null && (i = Interlocked.Increment(ref naechster)) < anzahl)
                {
                    rumpf(i, zustand);
                }
            }
            catch (Exception e)
            {
                Interlocked.CompareExchange(ref fehler, e, null);
            }
        }
        var faeden = Enumerable.Range(0, Math.Min(Faeden, Math.Max(1, anzahl))).Select(_ => new Thread(Arbeiten) { IsBackground = true }).ToList();
        faeden.ForEach(f => f.Start());
        faeden.ForEach(f => f.Join());
        if (fehler is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(fehler).Throw();
        }
    }
}
