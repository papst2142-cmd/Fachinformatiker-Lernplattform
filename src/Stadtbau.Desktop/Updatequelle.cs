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

    /// <summary>
    /// Quelle mit Filter auf die Linie vS, nur das neueste Release mit Feed, jede Datei bei Stoerung bis zu 4 Mal
    /// (<see cref="StoerungsfesteQuelle"/>, seit A 0.53.2 gemeinsam mit der Lernplattform in Lernplattform.Desktop,
    /// fuer das Werkzeug unveraendert). Die Gegenprobe (<see cref="AltFuerTests"/>) baut die Quelle bis S 0.25.0.
    /// </summary>
    /// <param name="zugang">nur die Update-Probe der CI (GITHUB_TOKEN), sonst null.</param>
    public static IUpdateSource Quelle(Action<string>? melden = null, string? zugang = null) =>
        AltFuerTests.Value
            ? new LinienQuelle(Werkzeug.Repository, ReleaseLinie.Linie.Stadtbau)
            : new StoerungsfesteQuelle(RepositoryUrl, ReleaseLinie.Linie.Stadtbau, Kanal, melden ?? (_ => { }), zugang: zugang);

    /// <summary>UpdateManager fuer das Werkzeug (Kanal stadtbau-win); Wiederholungen meldet <paramref name="melden"/>.</summary>
    public static UpdateManager Manager(Action<string>? melden = null, string? zugang = null) =>
        new(Quelle(melden, zugang), new UpdateOptions { ExplicitChannel = Kanal });

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
