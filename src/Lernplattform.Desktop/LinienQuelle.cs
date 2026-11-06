using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Lernplattform.Core.Update;
using Velopack.Sources;

namespace Lernplattform.Desktop;

/// <summary>
/// Phase 13 Welle 2 (Weisung P12-17, bindend aus P12-16 Abschnitt 3): Velopack-Quelle GitHub, die nur Releases der
/// eigenen Linie sieht (Lernplattform vA/vB/v1, Stadtbau-Werkzeug vS, Regel in <see cref="ReleaseLinie"/>). Abfrage wie
/// <see cref="GithubSource"/> (neueste zuerst), aber 30 statt 10 Releases je Seite, damit Releases der anderen Linie
/// die eigenen nicht aus der ersten Seite draengen. Die Datei wird in Lernplattform.Desktop und Stadtbau.Desktop
/// gleich gebaut (verlinkt).
/// </summary>
public sealed class LinienQuelle(string repo, ReleaseLinie.Linie linie) : GithubSource(repo, null, false)
{
    public ReleaseLinie.Linie Linie { get; } = linie;

    protected override async Task<GithubRelease[]> GetReleases(bool includePrereleases)
    {
        var adresse = new Uri(GetApiBaseUrl(RepoUri), $"repos{RepoUri.AbsolutePath}/releases?per_page=30&page=1");
        var antwort = await Downloader.DownloadString(adresse.ToString(), GetRequestHeaders("application/vnd.github.v3+json"), 30).ConfigureAwait(false);
        return Waehlen(antwort, Linie, includePrereleases);
    }

    /// <summary>Releases der Linie aus der Antwort, neueste zuerst, ohne Vorab-Releases (ausser erlaubt).</summary>
    public static GithubRelease[] Waehlen(string antwort, ReleaseLinie.Linie linie, bool vorab) =>
        ReleaseLinie.Filtern(antwort, linie)
            .Select(r => JsonSerializer.Deserialize<GithubRelease>(r.ToJsonString()))
            .Where(r => r is not null && (vorab || !r.Prerelease))
            .Select(r => r!)
            .OrderByDescending(r => r.PublishedAt)
            .ToArray();
}
