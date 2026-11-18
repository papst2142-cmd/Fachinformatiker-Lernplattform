using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Spiel;

/// <summary>
/// Patch A 0.75.1 (Sammelliste 340, Pixelfehler am Galaxy Tab A8 mit Mali-G52): Genauigkeit der Grafik im Problembericht.
/// Beim ersten Bild nach dem Einrichten stehen in <c>spiel_bilder.log</c> (Kopf von <see cref="BildZeiten"/>) die Bits des
/// Tiefenpuffers („Tiefe: 16 Bit“) und unter OpenGL ES die Genauigkeit der Gleitkommazahlen im Ecken-Shader, wie der
/// Treiber sie meldet (mediump 10 Bit = 16-Bit-Zahl wie auf Mali, 23 Bit = rechnet in Wahrheit mit 32 Bit wie meist auf
/// Adreno). Nur Lesen, kein Bild aendert sich.
/// </summary>
public sealed partial class StadtFlaeche
{
    private const int GlFramebufferAttachmentObjectType = 0x8CD0;
    private const int GlFramebufferAttachmentDepthSize = 0x8216;
    private const int GlDepthStandard = 0x1801;
    private const int GlVertexShader = 0x8B31;
    private const int GlMediumFloat = 0x8DF1;
    private const int GlHighFloat = 0x8DF2;

    private bool _genauigkeitGemeldet;

    /// <summary>Die Zeilen, die beim ersten Bild in den Kopf von spiel_bilder.log kamen (leer davor; Start-Test).</summary>
    public string GenauigkeitZeilen { get; private set; } = "";

    /// <summary>
    /// Fehler im Kopf eines Ecken-Shaders fuer OpenGL ES (null = in Ordnung): er muss mit <c>#version 300 es</c> und
    /// <c>precision highp float;</c> beginnen und darf nirgends <c>mediump</c> oder <c>lowp</c> nennen.
    /// </summary>
    public static string? EckenKopfFehler(string quelle)
    {
        const string soll = "#version 300 es\nprecision highp float;\n";
        if (!quelle.StartsWith(soll, StringComparison.Ordinal))
        {
            var zeilen = quelle.Split('\n');
            return "Kopf \"" + string.Join(" | ", zeilen.Take(2)) + "\" statt \"" + soll.TrimEnd('\n').Replace("\n", " | ", StringComparison.Ordinal) + "\"";
        }
        if (quelle.Contains("mediump", StringComparison.Ordinal) || quelle.Contains("lowp", StringComparison.Ordinal))
        {
            return "mediump oder lowp im Ecken-Shader";
        }
        return null;
    }

    /// <summary>Beim ersten Bild nach dem Einrichten: Tiefe und Genauigkeit lesen und in den Kopf von spiel_bilder.log schreiben.</summary>
    private unsafe void GenauigkeitMelden(Avalonia.OpenGL.GlInterface gl, int fb)
    {
        if (_genauigkeitGemeldet || _glGetIntegerv == 0)
        {
            return;
        }
        _genauigkeitGemeldet = true;
        var holen = GlFunktion(gl, "glGetFramebufferAttachmentParameteriv");
        var fehlerHolen = GlFunktion(gl, "glGetError");
        var zeilen = new List<string>();
        var tiefe = "unbekannt";
        if (holen != 0)
        {
            var anhang = fb == 0 ? GlDepthStandard : GlDepthAttachment;
            int art = 0, bits = 0;
            var f = (delegate* unmanaged[Stdcall]<int, int, int, int*, void>)holen;
            f(GlFramebuffer, anhang, GlFramebufferAttachmentObjectType, &art);
            if (art == 0)
            {
                tiefe = "0 Bit (kein Tiefenpuffer)";
            }
            else
            {
                f(GlFramebuffer, anhang, GlFramebufferAttachmentDepthSize, &bits);
                tiefe = bits.ToString(System.Globalization.CultureInfo.InvariantCulture) + " Bit";
                TiefeBits = bits;
            }
        }
        zeilen.Add("Tiefe: " + tiefe + (AufloesungDa ? " (bei Aufloesung unter 100 % eigener Bildspeicher mit 24 Bit)" : ""));
        if (_abfrageEs && GlFunktion(gl, "glGetShaderPrecisionFormat") is var genau and not 0)
        {
            int mittel = -1, hoch = -1;
            var bereich = stackalloc int[2];
            var g = (delegate* unmanaged[Stdcall]<int, int, int*, int*, void>)genau;
            g(GlVertexShader, GlMediumFloat, bereich, &mittel);
            g(GlVertexShader, GlHighFloat, bereich, &hoch);
            zeilen.Add(string.Create(System.Globalization.CultureInfo.InvariantCulture,
                $"Genauigkeit Ecken-Shader: mediump {mittel} Bit, highp {hoch} Bit (genutzt: highp)"));
        }
        if (fehlerHolen != 0)
        {
            // Fehler der Abfrage (alter Treiber) nicht ins naechste Bild tragen
            var fehler = (delegate* unmanaged[Stdcall]<int>)fehlerHolen;
            for (var i = 0; i < 8 && fehler() != 0; i++)
            {
            }
        }
        GenauigkeitZeilen = string.Join("\n", zeilen);
        Zeiten.Kopf = Zeiten.Kopf.Length > 0 ? Zeiten.Kopf + "\n" + GenauigkeitZeilen : GenauigkeitZeilen;
    }
}
