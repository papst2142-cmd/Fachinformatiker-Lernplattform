using System.Globalization;
using System.Text.RegularExpressions;

namespace Lernplattform.Core.Update;

/// <summary>
/// Phase 5 BE9: Programmversion der C#-Linie fuer den Versionsvergleich (statt parse_version/is_newer).
/// Reihe A &lt; B &lt; 1.0: zuerst die Stufe (A = 1, B = 2, ab 1.0 = 3), dann Haupt, Neben, Fix.
/// Gelesen werden Paketversion ("1.0.0-a.0.11.0", ab 1.0 "1.0.0"), Anzeige ("A 0.11.0") und
/// Release-Tag ("vA0.11.0", ab 1.0 "v1.0.0"); fehlende Stellen gelten wie in Python als 0.
/// </summary>
public sealed partial record UpdateVersion(int Stufe, int Haupt, int Neben, int Fix) : IComparable<UpdateVersion>
{
    public const int StufeA = 1;
    public const int StufeB = 2;
    public const int StufeFertig = 3;

    [GeneratedRegex(@"^1\.0\.0-([ab])\.(\d+(?:\.\d+){0,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex PaketMuster();

    [GeneratedRegex(@"^v?([AB])\s?(\d+(?:\.\d+){0,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex MitStufe();

    [GeneratedRegex(@"^v?(\d+(?:\.\d+){0,2})$", RegexOptions.CultureInvariant)]
    private static partial Regex OhneStufe();

    /// <summary>Liest eine Version; ungueltige Angaben (auch Ziffern ausserhalb 0-9) ergeben null.</summary>
    public static UpdateVersion? Lesen(string? text)
    {
        var t = (text ?? "").Trim();
        if (PaketMuster().Match(t) is { Success: true } p)
        {
            return Bauen(p.Groups[1].Value == "a" ? StufeA : StufeB, p.Groups[2].Value);
        }
        if (MitStufe().Match(t) is { Success: true } m)
        {
            return Bauen(m.Groups[1].Value == "A" ? StufeA : StufeB, m.Groups[2].Value);
        }
        return OhneStufe().Match(t) is { Success: true } o ? Bauen(StufeFertig, o.Groups[1].Value) : null;
    }

    private static UpdateVersion? Bauen(int stufe, string zahlen)
    {
        var teile = new int[3];
        var gelesen = zahlen.Split('.');
        for (var i = 0; i < gelesen.Length; i++)
        {
            if (!int.TryParse(gelesen[i], NumberStyles.None, CultureInfo.InvariantCulture, out teile[i]))
            {
                return null;
            }
        }
        return new UpdateVersion(stufe, teile[0], teile[1], teile[2]);
    }

    [GeneratedRegex(@"^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\z", RegexOptions.CultureInvariant)]
    private static partial Regex DreiZahlen();

    /// <summary>
    /// Phase 6 W1 (Sammelliste 73): Tag eines Releases aus Stufe und Version wie in Directory.Build.props.
    /// Stufe "A"/"B" ergibt "vA0.12.0"/"vB0.1.0", ohne Stufe nur ab 1.0 ("v1.0.0"). Alles andere (auch "0.12.0"
    /// ohne Stufe, das <see cref="Lesen"/> als 1.0-Reihe liest) ergibt null, und der Release-Ablauf bricht ab.
    /// Version nur aus drei Zahlen mit Ziffern 0-9 ohne fuehrende Null. <see cref="Lesen"/> selbst bleibt unveraendert (G41).
    /// </summary>
    public static string? ReleaseTag(string? stufe, string? version)
    {
        // jede der drei Zahlen muss in int passen (gleich release.py), sonst kein Tag
        if (DreiZahlen().Match(version ?? "") is not { Success: true } m || !int.TryParse(m.Groups[1].Value,
                NumberStyles.None, CultureInfo.InvariantCulture, out var haupt)
            || !int.TryParse(m.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _)
            || !int.TryParse(m.Groups[3].Value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
        {
            return null;
        }
        return stufe switch
        {
            "A" or "B" => "v" + stufe + version,
            "" or null when haupt >= 1 => "v" + version,
            _ => null,
        };
    }

    /// <summary>is_newer: true, wenn kandidat hoeher als aktuell ist; ist eine der beiden ungueltig, false.</summary>
    public static bool IstNeuer(string? kandidat, string? aktuell) =>
        Lesen(kandidat) is { } neu && Lesen(aktuell) is { } alt && neu.CompareTo(alt) > 0;

    public int CompareTo(UpdateVersion? andere)
    {
        if (andere is null)
        {
            return 1;
        }
        var vergleich = Stufe.CompareTo(andere.Stufe);
        if (vergleich == 0)
        {
            vergleich = Haupt.CompareTo(andere.Haupt);
        }
        if (vergleich == 0)
        {
            vergleich = Neben.CompareTo(andere.Neben);
        }
        return vergleich != 0 ? vergleich : Fix.CompareTo(andere.Fix);
    }

    private string Zahlen => string.Create(CultureInfo.InvariantCulture, $"{Haupt}.{Neben}.{Fix}");

    /// <summary>Anzeige wie im Programm: "A 0.11.0", ab 1.0 ohne Stufe ("1.0.0").</summary>
    public string Anzeige => Stufe switch
    {
        StufeA => "A " + Zahlen,
        StufeB => "B " + Zahlen,
        _ => Zahlen,
    };

    /// <summary>Paketversion fuer den Updater (Directory.Build.props): "1.0.0-a.0.11.0", ab 1.0 "1.0.0".</summary>
    public string Paket => Stufe switch
    {
        StufeA => "1.0.0-a." + Zahlen,
        StufeB => "1.0.0-b." + Zahlen,
        _ => Zahlen,
    };
}
