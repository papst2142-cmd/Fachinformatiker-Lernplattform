using System.Text.RegularExpressions;
using Avalonia.OpenGL;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Patch A 0.75.1 (Sammelliste 340, Pixelfehler am Galaxy Tab A8 mit Mali-G52): unter OpenGL ES bekommt jeder Ecken-Shader
/// (Stadt, Autos, Instanzen) den Kopf <c>precision highp float;</c> und nie mediump; der Pixel-Shader bleibt mediump, am
/// PC (Desktop-GLSL) bleibt jeder Kopf wie bis A 0.75.0. Gegenprobe: der Kopf bis A 0.75.0 (mediump vor dem Ecken-Shader)
/// wird erkannt. Dazu steht nach dem ersten Bild die Zeile „Tiefe: &lt;n&gt; Bit“ im Kopf von spiel_bilder.log.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> GenauigkeitAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: Genauigkeit " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel Genauigkeit " + punkt + ": " + wert);
                funde++;
            }
        }
        var es = new GlVersion(GlProfileType.OpenGLES, 3, 0);
        var pc = new GlVersion(GlProfileType.OpenGL, 3, 3);
        const string pixelKopfEs = "#version 300 es\nprecision mediump float;\n";
        foreach (var (name, autos, instanzen) in new[] { ("Stadt", false, false), ("Autos", true, false), ("Instanzen", false, true) })
        {
            var (ecke, pixel) = StadtFlaeche.Quellen(true, es, autos, instanzen);
            var fehler = StadtFlaeche.EckenKopfFehler(ecke);
            Pruefe("Ecken-Shader ES " + name, fehler is null, fehler ?? "highp, kein mediump");
            Pruefe("Pixel-Shader ES " + name, pixel.StartsWith(pixelKopfEs, StringComparison.Ordinal), pixel.StartsWith(pixelKopfEs, StringComparison.Ordinal) ? "mediump wie bis A 0.75.0" : "Kopf geaendert");
            var (eckePc, pixelPc) = StadtFlaeche.Quellen(false, pc, autos, instanzen);
            var pcGleich = eckePc.StartsWith("#version 330 core\n", StringComparison.Ordinal) && pixelPc.StartsWith("#version 330 core\n", StringComparison.Ordinal)
                && !eckePc.Contains("precision", StringComparison.Ordinal) && !pixelPc.Contains("precision", StringComparison.Ordinal)
                && eckePc[eckePc.IndexOf('\n')..] == ecke[ecke.IndexOf('\n', ecke.IndexOf('\n') + 1)..]
                && pixelPc[pixelPc.IndexOf('\n')..] == pixel[pixel.IndexOf('\n', pixel.IndexOf('\n') + 1)..];
            Pruefe("PC unveraendert " + name, pcGleich, pcGleich ? "Kopf #version 330 core ohne precision, Rumpf gleich ES" : "Kopf oder Rumpf am PC anders");
            // Gegenprobe: der Kopf bis A 0.75.0 (mediump auch vor dem Ecken-Shader) faellt auf
            var alt = "#version 300 es\nprecision mediump float;\n" + ecke[ecke.IndexOf('\n', ecke.IndexOf('\n') + 1)..].TrimStart('\n');
            var altFehler = StadtFlaeche.EckenKopfFehler(alt);
            Pruefe("Gegenprobe Kopf bis A 0.75.0 " + name, altFehler is not null, altFehler ?? "NICHT erkannt");
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Genauigkeit: Spielseite nicht gebaut");
            return funde + 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Genauigkeit Zeile Tiefe nicht geprueft (ohne 3D kein Bild)");
            return funde;
        }
        for (var i = 0; i < 3; i++)
        {
            await seite.Flaeche.NaechstesBildAsync();
        }
        var zeilen = seite.Flaeche.GenauigkeitZeilen;
        // die Stadt zeichnet mit Tiefenpruefung: 0 Bit oder „unbekannt“ hiesse, die Abfrage selbst ist falsch
        var treffer = Regex.Match(zeilen, @"^Tiefe: (\d+) Bit", RegexOptions.Multiline);
        var tiefeOk = treffer.Success && int.Parse(treffer.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) >= 16
            && seite.Flaeche.Zeiten.Text().Contains(zeilen, StringComparison.Ordinal);
        Pruefe("spiel_bilder.log", tiefeOk, "\"" + zeilen.Replace("\n", " | ", StringComparison.Ordinal) + "\"");
        return funde;
    }
}
