namespace Lernplattform.Core.Update;

/// <summary>
/// A 0.53.2 (Sammelliste 256, Weisung P12-113): Regeln fuer Stoerungen bei GitHub, gemeinsam fuer Lernplattform und
/// Stadtbau-Werkzeug (bis S 0.25.1 nur im Werkzeug als <c>Updatequelle.Voruebergehend</c>/<c>Wiederholen</c>, dort
/// unveraendert uebernommen). GitHub antwortete am 09.11.2026 auf rund jede dritte Abfrage mit 504 (Gateway Time-out);
/// jede einzelne Abfrage (Liste, Feed, Paket, Seite "latest", APK) wird darum bei einer Stoerung bis zu 4 Mal versucht.
/// </summary>
public static class Stoerung
{
    /// <summary>Wartezeiten zwischen den Versuchen bei Stoerungen von GitHub (insgesamt 4 Versuche).</summary>
    public static readonly TimeSpan[] Pausen = [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    /// <summary>Zeilenanfang jeder Wiederholung in update.log (und im Bericht der Proben).</summary>
    public const string Zeile = "WIEDERHOLUNG ";

    /// <summary>Nur fuer Tests: Pausen fuer <see cref="WiederholenAsync{T}"/> ohne eigenes Warten.</summary>
    public static readonly AsyncLocal<bool> OhnePausenFuerTests = new();

    /// <summary>
    /// Stoerung bei GitHub statt Fehler im Programm: Antwort 5xx (z. B. 504 Gateway Time-out), keine Antwort (Netz,
    /// ohne Statuscode) oder Zeitueberschreitung, auch als innere Ausnahme. 4xx (z. B. 404, 403) und alle anderen
    /// Fehler sind keine Stoerung und werden nie wiederholt.
    /// </summary>
    public static bool Voruebergehend(Exception? x)
    {
        for (; x is not null; x = x.InnerException)
        {
            switch (x)
            {
                case HttpRequestException h when h.StatusCode is null || (int)h.StatusCode >= 500:
                // A 0.61.0 (Sammelliste 283): Abriss beim Lesen der Antwort (HttpIOException ist eine IOException, darum
                // galt er bis A 0.60.0 nicht als Stoerung) und Netzfehler als innere Ausnahme
                case System.Net.Http.HttpIOException:
                case System.Net.Sockets.SocketException:
                case TaskCanceledException:
                case TimeoutException:
                    return true;
                case HttpRequestException:
                    return false;
            }
        }
        return false;
    }

    /// <summary>
    /// A 0.61.0 (Sammelliste 283): Beschreibung einer Ausnahme fuer update.log: Typ, bei Netzfehlern die Art
    /// (<see cref="HttpRequestError"/>) und die innere Ausnahme (Typ und Text), damit ein Abriss nie wie ein Speicherfehler aussieht.
    /// </summary>
    public static string Beschreibung(Exception e)
    {
        var text = e.GetType().Name;
        switch (e)
        {
            case System.Net.Http.HttpIOException io:
                text += " (" + io.HttpRequestError + ")";
                break;
            case HttpRequestException h:
                text += " (" + h.HttpRequestError + (h.StatusCode is { } c ? ", " + (int)c : "") + ")";
                break;
        }
        if (e.InnerException is { } innen)
        {
            text += ", innen " + innen.GetType().Name + ": " + innen.Message;
        }
        return text;
    }

    /// <summary>
    /// Fuehrt einen Netz-Schritt aus und wiederholt ihn bei einer Stoerung von GitHub nach <see cref="Pausen"/>; jede
    /// Wiederholung meldet <paramref name="melden"/>. Nach dem letzten Versuch oder bei einem anderen Fehler geht die
    /// Ausnahme unveraendert weiter.
    /// </summary>
    public static T Wiederholen<T>(string schritt, Func<T> tun, Action<string> melden, Action<TimeSpan>? warten = null)
    {
        warten ??= Thread.Sleep;
        return WiederholenAsync(schritt, () => Task.FromResult(tun()), melden, p => { warten(p); return Task.CompletedTask; })
            .GetAwaiter().GetResult();
    }

    /// <summary>
    /// Wie <see cref="Wiederholen{T}"/>, fuer asynchrone Schritte (jede einzelne Abfrage beim Laden). Ist
    /// <paramref name="abbruch"/> ausgeloest (Abbruch durch den Nutzer oder Stillstand beim Laden), wird nie wiederholt
    /// und die Pause endet sofort.
    /// </summary>
    public static async Task<T> WiederholenAsync<T>(string schritt, Func<Task<T>> tun, Action<string> melden, Func<TimeSpan, Task>? warten = null,
        CancellationToken abbruch = default)
    {
        warten ??= p => OhnePausenFuerTests.Value ? Task.CompletedTask : Task.Delay(p, abbruch);
        for (var versuch = 1; ; versuch++)
        {
            try
            {
                return await tun().ConfigureAwait(false);
            }
            catch (Exception x) when (versuch <= Pausen.Length && !abbruch.IsCancellationRequested && Voruebergehend(x))
            {
                var pause = Pausen[versuch - 1];
                melden(Zeile + schritt + " Versuch " + (versuch + 1) + " nach " + (int)pause.TotalSeconds
                    + " s (" + Beschreibung(x) + ": " + x.Message + ")");
                await warten(pause).ConfigureAwait(false);
            }
        }
    }
}
