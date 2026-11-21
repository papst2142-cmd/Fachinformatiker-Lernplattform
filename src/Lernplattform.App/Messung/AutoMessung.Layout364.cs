using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Lernplattform.Core.Inhalte;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Sammelliste 364 (Weisung P12-288): die Layoutpruefung des Lernteils wartet je Ansicht nur noch, bis die Oberflaeche
/// fertig gerechnet ist (<see cref="MainView.WartenArt.Layout"/>), nicht mehr auf zwei gezeichnete Bilder. Beleg, dass sie
/// dabei genau dasselbe prueft: alle Ansichten des Lernteils einmal mit Bild-Warten und einmal mit Layout-Warten; je
/// Ansicht muessen alle sichtbaren Elemente (Art, Lage, Groesse, Text) und die Funde der Layoutpruefung gleich sein.
/// Gegenprobe: ganz ohne Warten muss sich mindestens eine Ansicht unterscheiden (sonst sieht die Probe nichts).
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> LayoutGleichAsync(MainView ansicht, Lerninhalte? inhalte)
    {
        var inv = CultureInfo.InvariantCulture;
        var alt = ansicht.Warten;
        Dictionary<string, string> bild, layout, ohne;
        var zeit = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            // Vorlauf: der erste Durchgang laesst Spuren (offene Karten, Bildlauf, Ansagen); erst danach sind zwei Durchgaenge
            // mit gleichem Warten gleich (lokal gemessen: Bild/Bild nach dem Vorlauf 0 Unterschiede, vorher 50)
            ansicht.Warten = MainView.WartenArt.Bild;
            await LernAbdrueckeAsync(ansicht, inhalte);
            zeit.Restart();
            bild = await LernAbdrueckeAsync(ansicht, inhalte);
            var bildMs = zeit.ElapsedMilliseconds;
            zeit.Restart();
            ansicht.Warten = MainView.WartenArt.Layout;
            layout = await LernAbdrueckeAsync(ansicht, inhalte);
            var layoutMs = zeit.ElapsedMilliseconds;
            // Gegenprobe nur mit den Lernseiten (Phase 4 W1): andere Aufzaehlungen pruefen in sich selbst und meldeten ohne
            // Warten eigene Funde (z. B. die Gegenprobe Nur-Lese-Feld der Updates)
            ansicht.Warten = MainView.WartenArt.Ohne;
            ohne = await LernAbdrueckeAsync(ansicht, inhalte, nurLernseiten: true);
            Messwerte.Notiere(string.Create(inv, $"Sammelliste 364: Ansichten des Lernteils mit Bild-Warten {bildMs} ms, mit Layout-Warten {layoutMs} ms ({bild.Count} Ansichten)"));
        }
        finally
        {
            ansicht.Warten = MainView.WartenArt.Bild;
            await ansicht.BildFertigAsync();
            ansicht.Warten = alt;
        }
        var funde = 0;
        var namen = bild.Keys.Union(layout.Keys).Order(StringComparer.Ordinal).ToList();
        foreach (var name in namen)
        {
            if (!bild.TryGetValue(name, out var b) || !layout.TryGetValue(name, out var l) || b != l)
            {
                funde++;
                Messwerte.Notiere("FEHLER: Sammelliste 364: Ansicht " + name + " mit Layout-Warten anders als mit Bild-Warten" + Unterschied(bild.GetValueOrDefault(name), layout.GetValueOrDefault(name)));
            }
        }
        var gleich = namen.Count - funde;
        var anders = ohne.Keys.Count(n => ohne[n] != bild.GetValueOrDefault(n));
        if (anders == 0)
        {
            funde++;
            Messwerte.Notiere("FEHLER: Sammelliste 364: Gegenprobe ohne Warten zeigt keinen Unterschied (Probe sieht nichts)");
        }
        Messwerte.Notiere(string.Create(inv, $"Sammelliste 364: {namen.Count} Ansichten mit Layout-Warten gleich wie mit Bild-Warten: {gleich}; Gegenprobe ohne Warten (Lernseiten, {ohne.Count} Ansichten): {anders} anders"));
        return funde;
    }

    /// <summary>Weisung P12-290 Punkt 1: Zeit und Zahl der Ansichten je Teil der Layoutpruefung (alle Stufen und Groessen zusammen).</summary>
    private static readonly Dictionary<string, (double Ms, int Ansichten)> Zeiten364 = new(StringComparer.Ordinal);

    private static void ZeitDazu364(string teil, double ms, int ansichten)
    {
        var (alt, n) = Zeiten364.GetValueOrDefault(teil);
        Zeiten364[teil] = (alt + ms, n + ansichten);
    }

    /// <summary>Misst einen Teil der Layoutpruefung: die Zeit vom ersten bis zum letzten Schritt der Aufzaehlung samt Pruefung.</summary>
    private static async IAsyncEnumerable<T> Gemessen364<T>(string teil, IAsyncEnumerable<T> quelle)
    {
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var n = 0;
        try
        {
            await foreach (var x in quelle)
            {
                n++;
                yield return x;
            }
        }
        finally
        {
            ZeitDazu364(teil, uhr.Elapsed.TotalMilliseconds, n);
        }
    }

    private sealed class ZeitTeil364(string teil) : IDisposable
    {
        private readonly System.Diagnostics.Stopwatch _uhr = System.Diagnostics.Stopwatch.StartNew();

        public void Dispose() => ZeitDazu364(teil, _uhr.Elapsed.TotalMilliseconds, 1);
    }

    private static ZeitTeil364 Zeit364(string teil) => new(teil);

    /// <summary>Zeilen „Layoutpruefung Zeit: …“ (laengster Teil zuerst) in die Messdatei; die Summe zum Vergleich mit LayoutAsync.</summary>
    private static void ZeitenNotieren364()
    {
        var inv = CultureInfo.InvariantCulture;
        Messwerte.Notiere(string.Create(inv, $"Layoutpruefung Zeit je Teil (Weisung P12-290), Summe {Zeiten364.Values.Sum(z => z.Ms) / 1000:0.0} s:"));
        foreach (var (teil, (ms, n)) in Zeiten364.OrderByDescending(z => z.Value.Ms))
        {
            Messwerte.Notiere(string.Create(inv, $"  Layoutpruefung Zeit: {teil}: {ms / 1000:0.0} s, {n} Ansichten, {(n > 0 ? ms / n : 0):0} ms je Ansicht"));
        }
    }

    private static string Unterschied(string? a, string? b)
    {
        if (a is null || b is null)
        {
            return a is null ? " (fehlt mit Bild-Warten)" : " (fehlt mit Layout-Warten)";
        }
        var za = a.Split('\n');
        var zb = b.Split('\n');
        for (var i = 0; i < Math.Max(za.Length, zb.Length); i++)
        {
            var x = i < za.Length ? za[i] : "-";
            var y = i < zb.Length ? zb[i] : "-";
            if (x != y)
            {
                return ": Bild [" + x + "] Layout [" + y + "]";
            }
        }
        return "";
    }

    /// <summary>Je Ansicht des Lernteils (gleiche Aufzaehlungen wie <see cref="LayoutAsync"/>) ein Abdruck.</summary>
    private static async Task<Dictionary<string, string>> LernAbdrueckeAsync(MainView ansicht, Lerninhalte? inhalte, bool nurLernseiten = false)
    {
        var abdrucke = new Dictionary<string, string>(StringComparer.Ordinal);
        void Merken(string name)
        {
            var schluessel = name;
            for (var n = 2; abdrucke.ContainsKey(schluessel); n++)
            {
                schluessel = name + " #" + n.ToString(CultureInfo.InvariantCulture);
            }
            abdrucke[schluessel] = Abdruck(ansicht);
        }
        if (nurLernseiten)
        {
            await foreach (var name in W1Ansichten(ansicht))
            {
                Merken(name);
            }
            return abdrucke;
        }
        await foreach (var (name, _) in W2Ansichten(ansicht, inhalte))
        {
            Merken(name);
        }
        foreach (var quelle in (Func<MainView, IAsyncEnumerable<string>>[])[RechnerAnsichten, W1Ansichten, KlausurAnsichten, ProjektAnsichten,
            W4Ansichten, P5Ansichten, P5W2Ansichten, P5W3Ansichten, P6W2Ansichten, P8W2Ansichten, P8W2bAnsichten, P8W8Ansichten])
        {
            await foreach (var name in quelle(ansicht))
            {
                Merken(name);
            }
        }
        return abdrucke;
    }

    /// <summary>Laufende Uhr der Pruefung (aendert sich mit der Zeit, nicht mit dem Warten).</summary>
    // Welle „Ablauf stabil“ (Sammelliste 417): auch „mm:ss“ (Pruefungslauf lauf-wiso, Laeufe 1110 bis 1142 rot im 1. Lauf)
    [System.Text.RegularExpressions.GeneratedRegex(@"^\d+:\d\d(:\d\d)?$")]
    private static partial System.Text.RegularExpressions.Regex Uhrzeit364();

    /// <summary>
    /// Alle sichtbaren Elemente (Art, Lage aus dem Layout zur Ansicht, Groesse, Text) und die Funde der Layoutpruefung. Die Lage
    /// ist die Summe der Layout-Lagen bis zur Ansicht (ohne Render-Transformationen: laufende Animationen aendern sie nicht).
    /// </summary>
    private static string Abdruck(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        foreach (var c in ansicht.GetVisualDescendants().OfType<Control>().Where(c => c.IsEffectivelyVisible))
        {
            var p = default(Point);
            for (Visual? v = c; v is not null && v != ansicht; v = v.GetVisualParent())
            {
                p += v.Bounds.Position;
            }
            var text = c switch
            {
                TextBlock t => t.Text,
                TextBox t => t.Text,
                _ => null,
            };
            if (text is not null && Uhrzeit364().IsMatch(text))
            {
                text = "(Uhr)";
            }
            // Sammelliste 417 (13 Laeufe bis 1150 am Mac): der Problembericht enthaelt die Protokolle (messpunkte.log,
            // haenger.log, spiel_bilder.log), die waehrend des Start-Tests weiter wachsen; er aendert sich mit der Zeit, nicht
            // mit dem Warten. Art, Lage, Groesse und die Funde der Layoutpruefung bleiben verglichen.
            if (text is not null && text.StartsWith(Lernplattform.Core.Diagnose.Problembericht.Programmname + " – Problembericht", StringComparison.Ordinal))
            {
                text = "(Problembericht)";
            }
            sb.Append(c.GetType().Name).Append(' ')
                .Append(string.Create(inv, $"{p.X:0.#},{p.Y:0.#} {c.Bounds.Width:0.#}x{c.Bounds.Height:0.#}"));
            if (text is not null)
            {
                sb.Append(" \"").Append(text.Replace('\n', ' ')).Append('"');
            }
            sb.Append('\n');
        }
        foreach (var fund in Layoutpruefung.Pruefe(ansicht))
        {
            sb.Append("FUND ").Append(fund.Replace('\n', ' ')).Append('\n');
        }
        return sb.ToString();
    }
}
