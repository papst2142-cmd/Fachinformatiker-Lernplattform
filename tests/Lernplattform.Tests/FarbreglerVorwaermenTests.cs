using System.Text.RegularExpressions;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P2-15: Das Vorwaermen der Farbregler (EigeneFarbenTeil.Vorwaermen, FarbRegler.Vorwaermen)
/// zeichnet nur neu. A1: Es setzt keinen Wert, speichert nichts und setzt keine Vorschau
/// (Quelltext-Pruefung hier, dazu der Laufzeit-Vergleich vorher/nachher im Start ohne Bedienung).
/// A2: Es gibt genau eine Aufrufstelle, im Aufbau der Ansicht; die Messung ruft es nicht auf.
/// P2-16 A1: Die Aufrufstelle liegt im selben Durchgang im Aufbau (Bauen), nicht nach dem ersten
/// Laden und nicht mit niedriger Vorrang (kein Post, kein Loaded).
/// </summary>
public partial class FarbreglerVorwaermenTests
{
    // Was im Rumpf eines Vorwaermens nie vorkommen darf: Werte setzen, speichern, Vorschau zeichnen
    private static readonly string[] Verboten =
    [
        ".Value =", "_werte =", "_welt =", "_setzen", "_gespeichert", "Speichern", "Ablegen", "Zeichnen()", "Einstellungen",
        "Zustand.", "_ergebnis", "_vorschau", "_vKnopf", "_zustand.Text =", "_warnung.Text =", "Mit(",
    ];

    [GeneratedRegex(@"(?<![\w.])Vorwaermen\b|\.Vorwaermen\b")]
    private static partial Regex Aufruf();

    private static string Rumpf(string quelltext, string kopf)
    {
        var start = quelltext.IndexOf(kopf, StringComparison.Ordinal);
        Assert.True(start >= 0, kopf);
        var auf = quelltext.IndexOf('{', start);
        var tiefe = 0;
        for (var i = auf; i < quelltext.Length; i++)
        {
            tiefe += quelltext[i] switch { '{' => 1, '}' => -1, _ => 0 };
            if (tiefe == 0)
            {
                return quelltext[(auf + 1)..i];
            }
        }
        throw new InvalidOperationException(kopf);
    }

    internal static List<string> Funde(IReadOnlyList<(string Datei, string Text)> dateien)
    {
        var funde = new List<string>();
        var teil = dateien.Single(d => d.Datei == "Views/EigeneFarbenTeil.cs").Text;
        var regler = dateien.Single(d => d.Datei == "Views/FarbRegler.cs").Text;
        foreach (var (name, rumpf) in (ValueTuple<string, string>[])[("EigeneFarbenTeil", Rumpf(teil, "private void Vorwaermen()")),
                     ("FarbRegler", Rumpf(regler, "public void Vorwaermen()"))])
        {
            funde.AddRange(Verboten.Where(v => rumpf.Contains(v, StringComparison.Ordinal)).Select(v => $"{name}.Vorwaermen enthaelt \"{v}\""));
        }
        // Aufrufstellen ausserhalb der beiden Definitionen: nur Vorwaermen() im Aufbau (Bauen) und
        // spur.Vorwaermen() im Rumpf von EigeneFarbenTeil.Vorwaermen
        var erlaubt = new[] { "Vorwaermen();", "spur.Vorwaermen();" };
        foreach (var (datei, text) in dateien)
        {
            var nr = 0;
            foreach (var zeile in text.Split('\n'))
            {
                nr++;
                var z = zeile.Trim();
                if (z.StartsWith("//", StringComparison.Ordinal) || z.StartsWith("///", StringComparison.Ordinal) || z.Contains("void Vorwaermen()", StringComparison.Ordinal)
                    || !Aufruf().IsMatch(z))
                {
                    continue;
                }
                if (datei != "Views/EigeneFarbenTeil.cs" || !erlaubt.Contains(z))
                {
                    funde.Add($"{datei}:{nr}: {z}");
                }
            }
        }
        foreach (var z in erlaubt)
        {
            if (teil.Split('\n').Count(x => x.Trim() == z) != 1)
            {
                funde.Add($"nicht genau einmal: {z}");
            }
        }
        // P2-16 A1: der Aufruf steht genau einmal im Aufbau (Bauen), im selben Durchgang
        if (Rumpf(teil, "public Control Bauen()").Split('\n').Count(x => x.Trim() == "Vorwaermen();") != 1)
        {
            funde.Add("Vorwaermen nicht genau einmal im Aufbau");
        }
        foreach (var spaeter in (string[])["Dispatcher", "DispatcherPriority", "Loaded +=", "ErstesLaden"])
        {
            if (teil.Contains(spaeter, StringComparison.Ordinal))
            {
                funde.Add($"Vorwaermen spaeter eingeplant: {spaeter}");
            }
        }
        return funde;
    }

    private static List<(string Datei, string Text)> AppDateien()
    {
        var ordner = new DirectoryInfo(AppContext.BaseDirectory);
        while (ordner is not null && !File.Exists(Path.Combine(ordner.FullName, "global.json")))
        {
            ordner = ordner.Parent;
        }
        Assert.NotNull(ordner);
        var wurzel = Path.Combine(ordner.FullName, "src", "Lernplattform.App");
        return [.. Directory.EnumerateFiles(wurzel, "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                        && !p.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .Select(p => (Path.GetRelativePath(wurzel, p).Replace('\\', '/'), File.ReadAllText(p).Replace("\r\n", "\n")))];
    }

    private static List<(string Datei, string Text)> Ersetze(List<(string Datei, string Text)> dateien, string datei, string alt, string neu)
    {
        var ergebnis = dateien.Select(d => d.Datei == datei ? (Datei: d.Datei, Text: d.Text.Replace(alt, neu, StringComparison.Ordinal)) : d).ToList();
        Assert.NotEqual(dateien.Single(d => d.Datei == datei).Text, ergebnis.Single(d => d.Datei == datei).Text);
        return ergebnis;
    }

    [Fact]
    public void VorwaermenAendertNichtsUndHatEineAufrufstelle()
    {
        var funde = Funde(AppDateien());
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    [Fact]
    public void Kontrollfehler_VorwaermenSetztEinenWert()
    {
        var dateien = AppDateien();
        var falsch = Ersetze(dateien, "Views/FarbRegler.cs", "            _gezeichnet = null;\n", "            _gezeichnet = null;\n            Regler.Value = 0;\n");
        Assert.Contains(Funde(falsch), f => f.Contains(".Value =", StringComparison.Ordinal));
        falsch = Ersetze(dateien, "Views/EigeneFarbenTeil.cs", "        var vorher = Abbild();\n", "        var vorher = Abbild();\n        _werte = Mit(_werte, EigeneFarben.TeilAkzent1, \"h\", 0);\n");
        Assert.Contains(Funde(falsch), f => f.Contains("_werte =", StringComparison.Ordinal));
    }

    [Fact]
    public void Kontrollfehler_VorwaermenNachDemErstenLaden()
    {
        // Weisung P2-16 A1: der Stand von P2-15 (nach dem ersten Laden, niedrige Vorrang) ist falsch
        var dateien = AppDateien();
        var falsch = Ersetze(dateien, "Views/EigeneFarbenTeil.cs", "        Vorwaermen();\n        return panel;",
            "        panel.Loaded += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Vorwaermen, Avalonia.Threading.DispatcherPriority.Background);\n        return panel;");
        var funde = Funde(falsch);
        Assert.Contains("Vorwaermen nicht genau einmal im Aufbau", funde);
        Assert.Contains(funde, f => f.StartsWith("Vorwaermen spaeter eingeplant", StringComparison.Ordinal));
    }

    [Fact]
    public void Kontrollfehler_MessungRuftVorwaermenAuf()
    {
        var dateien = AppDateien();
        var falsch = Ersetze(dateien, "Messung/AutoMessung.cs", "        var alt = regler.Value;\n", "        foreach (var s in spuren) s.Vorwaermen();\n        var alt = regler.Value;\n");
        Assert.Contains(Funde(falsch), f => f.StartsWith("Messung/AutoMessung.cs:", StringComparison.Ordinal));
    }
}
