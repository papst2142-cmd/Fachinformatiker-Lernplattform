using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>Was ein Teil der Stadt ist, wenn er nach der Jahreszeit gefaerbt wird (Phase 8 W9).</summary>
public enum Faerbeart
{
    /// <summary>Boden, Strassen, Baeume, Modelle ohne Haus: Bodenfarben, Pflanzen, Schnee auf Pflanzen.</summary>
    Stadt,

    /// <summary>Haeuser: nur Schnee auf dem Dach und Pflanzen unten am Haus; Waende, Fenster, Tueren bleiben.</summary>
    Haus,

    /// <summary>Kleingruen (Bueschel, Steine, Beete): wie Stadt, im Winter Schnee auf allem, was nach oben zeigt.</summary>
    Klein,
}

/// <summary>
/// Phase 8 W9 (Weisung P8-31/P8-32, Nico 1a 2a 3c 5b): Farben der Stadt nach der Jahreszeit, nur Darstellung.
/// Sommer = die Farben wie vorher (es wird nichts umgerechnet). Umgerechnet werden die Farben beim Bau der Netze,
/// es kommen keine Dreiecke dazu. Die Jahreszeit kommt nur ueber <see cref="Jahreszeiten.Heute"/> (Datum nur ueber
/// <see cref="Festzeit.Datum"/>); die Fest-Deko bleibt in ihren Farben (das regelt <see cref="StadtSzene"/>).
/// </summary>
/// <remarks>
/// Schnee traegt die Marke <see cref="SchneeMarke"/> (alle drei Anteile + 2): der Shader zieht sie ab und dunkelt
/// Schnee im dunklen Design nicht ab (Nico 5b), alles andere wie vorher.
/// </remarks>
public static class Jahreszeitfarben
{
    /// <summary>Zu jeder Schneefarbe addiert; der Shader erkennt Schnee an Rot ab 1,5.</summary>
    public const float SchneeMarke = 2f;

    /// <summary>Jeder wievielte Laubbaum im Fruehling bluehend (Nico 3c: jeder 3.).</summary>
    public const int BlueteJeder = 3;

    /// <summary>Jeder wievielte Laubbaum im Herbst noch halb gruen.</summary>
    public const int HalbGruenJeder = 5;

    /// <summary>Ab diesem Betrag der Normale nach oben liegt auf Pflanzen und Kleingruen Schnee.</summary>
    public const float SchneePflanze = 0.35f;

    /// <summary>Ab diesem Betrag der Normale nach oben liegt auf Daechern Schnee (0,45: auch das steile Stellwerk-Dach).</summary>
    public const float SchneeDach = 0.45f;

    /// <summary>Daecher liegen hoeher, Pflanzen am Haus darunter.</summary>
    public const float DachAb = 0.3f;

    public const float PflanzeAmHausBis = 0.9f;

    private static Vector3 F(string hex) => Netz.Farbe(hex);

    /// <summary>Bodenfarben der Stadt (wie <c>StadtSzene.Farben</c>).</summary>
    public static readonly Vector3 Boden = F("#9CC48A");
    public static readonly Vector3 Park = F("#7FB069");
    public static readonly Vector3 Ufer = F("#B9A77A");
    public static readonly Vector3 Wasser = F("#4FA8D8");

    /// <summary>Schnee (ohne Marke).</summary>
    public static readonly Vector3 SchneeFarbe = F("#EEF2F7");

    private static readonly Vector3 Bluete = F("#F4B8CE");
    private static readonly Vector3 Maigruen = F("#B4E26E");
    private static readonly Vector3 Winterlaub = F("#56635B");
    private static readonly Vector3[] Herbstlaub = [F("#D9822B"), F("#E3B53A"), F("#A65A2E"), F("#C9702F")];

    /// <summary>Bodenfarben: (heute, Fruehling, Herbst, Winter); Winter ohne Marke.</summary>
    private static readonly (Vector3 Von, Vector3 Fr, Vector3 He, Vector3 Wi)[] Bodenfarben =
    [
        (Boden, F("#AEDB86"), F("#B9AC72"), SchneeFarbe),
        (Boden * 0.88f, F("#AEDB86") * 0.88f, F("#B9AC72") * 0.88f, SchneeFarbe * 0.9f),
        (Park, F("#92D265"), F("#A88E52"), F("#E2E8F0")),
        (Ufer, Ufer, F("#A89268"), F("#E6EAF0")),
        (Wasser, Wasser, F("#4A93BF"), F("#A9CBE0")),
        (Wasser * 0.85f, Wasser * 0.85f, F("#4A93BF") * 0.85f, F("#A9CBE0") * 0.85f),
    ];

    /// <summary>Schneefarbe mit Marke, <paramref name="hell"/> = Schattierung 0..1.</summary>
    public static Vector3 Schnee(float hell) => (SchneeFarbe * hell) + new Vector3(SchneeMarke);

    /// <summary>Ist die Farbe Schnee (mit Marke)?</summary>
    public static bool IstSchnee(Vector3 c) => c.X >= SchneeMarke - 0.5f;

    /// <summary>Gruenton einer Pflanze: Gruen am staerksten, Farbton 65 bis 178 Grad, gesaettigt, nicht zu dunkel.</summary>
    public static bool IstPflanze(Vector3 c)
    {
        var max = MathF.Max(c.X, MathF.Max(c.Y, c.Z));
        var min = MathF.Min(c.X, MathF.Min(c.Y, c.Z));
        if (max < 0.15f || max - min < 0.2f * max || max != c.Y)
        {
            return false;
        }
        var ton = 60f * (((c.Z - c.X) / (max - min)) + 2f);
        return ton is >= 65f and <= 178f;
    }

    /// <summary>Nummer der Bodenfarbe (0 bis 5) oder -1.</summary>
    public static int BodenNummer(Vector3 c)
    {
        for (var i = 0; i < Bodenfarben.Length; i++)
        {
            if (Vector3.DistanceSquared(c, Bodenfarben[i].Von) < 0.0004f)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Nadelbaum: schmaler, hoher Kegel (Hoehe mehr als 1,5 x Breite); bleibt im Herbst gruen.</summary>
    public static bool Nadel(GrafikModell m) => (m.Max.Y - m.Min.Y) > 1.5f * MathF.Max(m.Max.X - m.Min.X, m.Max.Z - m.Min.Z);

    /// <summary>Samen eines Baums aus seiner Lage (gleich an jedem Tag): waehlt Laubfarbe und Bluete.</summary>
    public static int Samen(Vector3 lage) =>
        (int)((((uint)MathF.Round(lage.X * 13) * 73856093u) ^ ((uint)MathF.Round(lage.Z * 13) * 19349663u)) & 0x7fffffff);

    private static float Hell(Vector3 c) => (0.299f * c.X) + (0.587f * c.Y) + (0.114f * c.Z);

    private static Vector3 Mischen(Vector3 a, Vector3 b, float t) => (a * (1 - t)) + (b * t);

    /// <summary>
    /// Neue Farbe einer Flaeche. <paramref name="oben"/> = Betrag der Normale nach oben (0 = Wand, 1 = flach),
    /// <paramref name="hoehe"/> = mittlere Hoehe, <paramref name="samen"/> und <paramref name="nadel"/> je Baum.
    /// </summary>
    public static Vector3 Faerben(Jahreszeit j, Faerbeart art, Vector3 c, float oben, float hoehe, int samen = 0, bool nadel = false) =>
        Faerben(j, art, c, BodenNummer(c), IstPflanze(c), oben, hoehe, samen, nadel);

    /// <summary>Wie oben mit vorab bestimmter Bodennummer und Pflanzen-Merkmal (je Modell gemerkt).</summary>
    public static Vector3 Faerben(Jahreszeit j, Faerbeart art, Vector3 c, int boden, bool pflanze, float oben, float hoehe, int samen, bool nadel)
    {
        if (j == Jahreszeit.Sommer)
        {
            return c;
        }
        if (art == Faerbeart.Haus)
        {
            if (j == Jahreszeit.Winter && oben > SchneeDach && hoehe > DachAb)
            {
                return Schnee(0.93f + (0.07f * oben));
            }
            // Hecken und Baeume unten am Haus, nie Daecher oder Waende
            if (hoehe >= PflanzeAmHausBis || !pflanze)
            {
                return c;
            }
        }
        else if (boden >= 0)
        {
            var b = Bodenfarben[boden];
            return j switch
            {
                Jahreszeit.Fruehling => b.Fr,
                Jahreszeit.Herbst => b.He,
                _ => b.Wi + new Vector3(SchneeMarke),
            };
        }
        if (!pflanze)
        {
            return art == Faerbeart.Klein && j == Jahreszeit.Winter && oben > SchneePflanze ? Schnee(0.9f + (0.1f * oben)) : c;
        }
        var schatten = Math.Clamp(Hell(c) / 0.5f, 0.65f, 1.25f);
        var s = (uint)samen;
        if (nadel && j != Jahreszeit.Winter)
        {
            // Nadelbaeume (auch Grashalme) bleiben gruen, im Fruehling etwas heller
            return j == Jahreszeit.Fruehling ? Mischen(c, Maigruen * schatten, 0.15f) : c;
        }
        switch (j)
        {
            case Jahreszeit.Fruehling:
                return s % BlueteJeder == 0 && !nadel
                    ? Mischen(c, Bluete * schatten, 0.8f)
                    : Mischen(c, Maigruen * schatten, 0.45f);
            case Jahreszeit.Herbst:
            {
                var laub = Herbstlaub[s % (uint)Herbstlaub.Length] * schatten;
                return Mischen(c, laub, s % HalbGruenJeder == 0 ? 0.5f : 0.85f);
            }
            default:
                return oben > SchneePflanze ? Schnee(0.9f + (0.1f * oben)) : Mischen(c, Winterlaub, 0.55f);
        }
    }

    /// <summary>Bodennummer und Pflanzen-Merkmal je Dreieck eines Modells, einmal je Modell gerechnet.</summary>
    private sealed record Merkmale(int[] Boden, bool[] Pflanze);

    private static readonly ConditionalWeakTable<GrafikModell, Merkmale> Gemerkt = [];

    private static Merkmale MerkmaleVon(GrafikModell m) => Gemerkt.GetValue(m, modell =>
    {
        var boden = new int[modell.Dreiecke];
        var pflanze = new bool[modell.Dreiecke];
        for (var d = 0; d < modell.Dreiecke; d++)
        {
            var c = Farbe(modell, d);
            boden[d] = BodenNummer(c);
            pflanze[d] = IstPflanze(c);
        }
        return new Merkmale(boden, pflanze);
    });

    private static Vector3 Farbe(GrafikModell m, int d) => new Vector3(m.Farben[d * 3], m.Farben[(d * 3) + 1], m.Farben[(d * 3) + 2]) / 255f;

    /// <summary>
    /// Faerbung je Dreieck eines Modells fuer <see cref="Netz.Hinzu(GrafikModell, System.Numerics.Matrix4x4, Func{int, float, float, Vector3}?)"/>;
    /// null im Sommer (dann bitgleich wie vorher).
    /// </summary>
    public static Func<int, float, float, Vector3>? FuerModell(Jahreszeit j, Faerbeart art, GrafikModell m, int samen, bool nadel)
    {
        if (j == Jahreszeit.Sommer)
        {
            return null;
        }
        var mm = MerkmaleVon(m);
        return (d, oben, hoehe) => Faerben(j, art, Farbe(m, d), mm.Boden[d], mm.Pflanze[d], oben, hoehe, samen, nadel);
    }

    /// <summary>Faerbung eigener Teile (<see cref="Netz.Faerbung"/>); null im Sommer.</summary>
    public static Func<Vector3, float, float, Vector3>? FuerTeile(Jahreszeit j, Faerbeart art, int samen = 0) =>
        j == Jahreszeit.Sommer ? null : (c, oben, hoehe) => Faerben(j, art, c, oben, hoehe, samen);
}
