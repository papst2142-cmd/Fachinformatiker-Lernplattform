using Lernplattform.Core.Update;
using Lernplattform.Desktop;
using Stadtbau.Core;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Stadtbau.Desktop;

/// <summary>
/// Sammelliste 249 (Weisung P12-107): Update-Quelle des Werkzeugs am PC. Bis S 0.25.0 bekam <see cref="LinienQuelle"/>
/// nur "papst2142-cmd/Fachinformatiker-Lernplattform" (ohne https://github.com/); Velopacks GithubSource macht daraus
/// eine Uri und warf <see cref="UriFormatException"/>, die die Suche nicht fing, also kam nie ein Angebot. Die Datei
/// ist auch in Stadtbau.Tests verlinkt (Test mit Gegenprobe).
/// </summary>
internal static class Updatequelle
{
    /// <summary>Velopack-Kanal des Werkzeugs (die Lernplattform nutzt die Vorgabe "win").</summary>
    public const string Kanal = "stadtbau-win";

    /// <summary>Volle Adresse des Repos fuer Velopack (wie Lernplattform.Desktop.Updater.Repo).</summary>
    public static string RepositoryUrl => "https://github.com/" + Werkzeug.Repository;

    /// <summary>Nur fuer die Gegenprobe: die alte Angabe ohne https://github.com/.</summary>
    internal static readonly AsyncLocal<bool> AltFuerTests = new();

    /// <summary>Quelle mit Filter auf die Linie vS.</summary>
    public static IUpdateSource Quelle() =>
        new LinienQuelle(AltFuerTests.Value ? Werkzeug.Repository : RepositoryUrl, ReleaseLinie.Linie.Stadtbau);

    /// <summary>UpdateManager fuer das Werkzeug (Kanal stadtbau-win).</summary>
    public static UpdateManager Manager() => new(Quelle(), new UpdateOptions { ExplicitChannel = Kanal });

    /// <summary>Wartezeiten zwischen den Versuchen bei Stoerungen von GitHub (insgesamt 4 Versuche).</summary>
    public static readonly TimeSpan[] Pausen = [TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60)];

    /// <summary>
    /// Stoerung bei GitHub statt Fehler im Programm (Nico 09.11.2026, viele 504 bei GitHub): Antwort 5xx (z. B. 504
    /// Gateway Time-out), keine Antwort (Netz, ohne Statuscode) oder Zeitueberschreitung, auch als innere Ausnahme.
    /// 4xx (z. B. 404, 403) und alle anderen Fehler sind keine Stoerung und werden nie wiederholt.
    /// </summary>
    public static bool Voruebergehend(Exception? x)
    {
        for (; x is not null; x = x.InnerException)
        {
            switch (x)
            {
                case HttpRequestException h when h.StatusCode is null || (int)h.StatusCode >= 500:
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
    /// Fuehrt einen Netz-Schritt aus und wiederholt ihn bei einer Stoerung von GitHub nach <see cref="Pausen"/>; jede
    /// Wiederholung meldet <paramref name="melden"/>. Nach dem letzten Versuch oder bei einem anderen Fehler geht die
    /// Ausnahme unveraendert weiter.
    /// </summary>
    public static T Wiederholen<T>(string schritt, Func<T> tun, Action<string> melden, Action<TimeSpan>? warten = null)
    {
        warten ??= Thread.Sleep;
        for (var versuch = 1; ; versuch++)
        {
            try
            {
                return tun();
            }
            catch (Exception x) when (versuch <= Pausen.Length && Voruebergehend(x))
            {
                var pause = Pausen[versuch - 1];
                melden("WIEDERHOLUNG " + schritt + " Versuch " + (versuch + 1) + " nach " + (int)pause.TotalSeconds
                    + " s (" + x.GetType().Name + ": " + x.Message + ")");
                warten(pause);
            }
        }
    }

    /// <summary>
    /// Velopack-Protokoll in update.log des Datenordners (Info und hoeher, Ausnahmen mit Art und Text). Velopacks
    /// eigene Datei lag bis S 0.25.0 im Installationsordner und ging mit jeder Neuinstallation verloren.
    /// </summary>
    public sealed class Protokoll(string ordner) : IVelopackLogger
    {
        public void Log(VelopackLogLevel logLevel, string? message, Exception? exception)
        {
            if (logLevel < VelopackLogLevel.Information)
            {
                return;
            }
            var text = "Velopack " + logLevel + ": " + message
                + (exception is null ? "" : " (" + exception.GetType().Name + ": " + exception.Message + ")");
            Updateprotokoll.Schreiben(text, ordner);
        }
    }
}
