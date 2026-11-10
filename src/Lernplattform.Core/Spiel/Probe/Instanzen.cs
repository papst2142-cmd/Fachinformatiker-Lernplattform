using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle D2 (Weisung P12-157, Nico 1a): ein Modell, das oft gleich vorkommt, geht einmal an die Grafikkarte und wird mit
/// einer Liste von Lagen in EINEM Zeichenaufruf gezeichnet. <see cref="Nah"/> und <see cref="Fern"/> liegen im Raum des
/// Modells (<see cref="Netz.HinzuLokal"/>), jede Lage setzt das Stueck an seinen Platz. Im Bild sind nur die Stuecke,
/// deren Kugel im Sichtkegel liegt; ab <see cref="StadtSzene.FernAb"/> die Fernform.
/// </summary>
public sealed class InstanzSatz
{
    public InstanzSatz(string ziel, string modell, Netz nah, Netz? fern, Matrix4x4[] lagen, (Vector3 Mitte, float Halb)[] kugeln)
    {
        Ziel = ziel;
        Modell = modell;
        Nah = nah;
        Fern = fern;
        Lagen = lagen;
        Kugeln = kugeln;
    }

    /// <summary><see cref="Instanzen.Stadt"/> (wie das Netz der Stadt) oder <see cref="Instanzen.Gruen"/> (nur nah wie das Kleingruen).</summary>
    public string Ziel { get; }

    public string Modell { get; }

    public Netz Nah { get; }

    /// <summary>Fernform je Stueck; null = das Ziel wird fern nicht gezeichnet (Kleingruen).</summary>
    public Netz? Fern { get; }

    public Matrix4x4[] Lagen { get; }

    /// <summary>Kugel je Stueck in der Welt (umschliesst nah und fern).</summary>
    public (Vector3 Mitte, float Halb)[] Kugeln { get; }

    public int DreieckeNah => Nah.Indizes.Count / 3;

    public int DreieckeFern => (Fern ?? Nah).Indizes.Count / 3;

    /// <summary>
    /// Die Stuecke im Bild: ihre Lagen kommen nach <paramref name="ziel"/> (wenn angegeben), zurueck kommt die Zahl.
    /// <paramref name="alle"/> = ohne Sichtkegel (Gegenprobe, wie <c>OhneSparenFuerTests</c>).
    /// </summary>
    public int ImBild(Matrix4x4 sichtProj, Matrix4x4[]? ziel, bool alle = false)
    {
        var n = 0;
        for (var i = 0; i < Lagen.Length; i++)
        {
            if (alle || FestUebergang.ImSichtkegel(sichtProj, Kugeln[i].Mitte, Kugeln[i].Halb))
            {
                if (ziel is not null)
                {
                    ziel[n] = Lagen[i];
                }
                n++;
            }
        }
        return n;
    }
}

/// <summary>Welle D2: welche Stuecke der Stadt mit Instancing gezeichnet werden (die Netze der Stadt bauen sie dann nicht ein).</summary>
public sealed record Instanzplan(IReadOnlySet<int> Stuecke, IReadOnlySet<int> Baeume, IReadOnlySet<int> Gruen, IReadOnlyList<InstanzSatz> Saetze)
{
    public static readonly Instanzplan Leer = new(new HashSet<int>(), new HashSet<int>(), new HashSet<int>(), []);
}

/// <summary>Welle D2 (Weisung P12-157, Nico 1a): Regeln fuer das Instancing.</summary>
public static class Instanzen
{
    /// <summary>Ein Modell kommt erst ab so vielen gleichen Stuecken in einen eigenen Aufruf (Nico 1a).</summary>
    public const int MindestLagen = 8;

    /// <summary>Hoechstens so viele zusaetzliche Zeichenaufrufe (Nico 1a); die groessten Gewinne zuerst.</summary>
    public const int HoechstensSaetze = 8;

    /// <summary>Farben gelten als gleich, wenn sie auf 1/<see cref="FarbStufen"/> gleich sind (weit unter einer Stufe der Anzeige, 1/255).</summary>
    public const int FarbStufen = 8192;

    public const string Stadt = "stadt";

    public const string Gruen = "gruen";

    /// <summary>Gegenprobe: ohne Instancing (alle Stuecke in den Netzen wie bis A 0.58.0).</summary>
    public static bool AusFuerTests
    {
        get => Aus.Value;
        set => Aus.Value = value;
    }

    private static readonly AsyncLocal<bool> Aus = new();

    /// <summary>
    /// Nur Lagen aus Drehung, gleichmaessiger Groesse und Verschiebung (sonst stimmte die Normale auf der Grafikkarte nicht,
    /// und ein gespiegeltes Stueck kehrte seine Dreiecke um).
    /// </summary>
    public static bool Geeignet(Matrix4x4 m)
    {
        if (m.M14 != 0 || m.M24 != 0 || m.M34 != 0 || m.M44 != 1)
        {
            return false;
        }
        Vector3 a = new(m.M11, m.M12, m.M13), b = new(m.M21, m.M22, m.M23), c = new(m.M31, m.M32, m.M33);
        float la = a.Length(), lb = b.Length(), lc = c.Length();
        if (la < 1e-6f)
        {
            return false;
        }
        const float g = 1e-4f;
        return MathF.Abs(lb - la) <= g * la && MathF.Abs(lc - la) <= g * la
            && MathF.Abs(Vector3.Dot(a, b)) <= g * la * la && MathF.Abs(Vector3.Dot(a, c)) <= g * la * la && MathF.Abs(Vector3.Dot(b, c)) <= g * la * la
            && Vector3.Dot(Vector3.Cross(a, b), c) > 0;
    }

    /// <summary>Ein Kandidat: Stueck Nummer <paramref name="Nr"/> in <paramref name="Liste"/> (s = Stuecke, b = Baeume, g = Kleingruen).</summary>
    internal sealed record Kandidat(string Ziel, char Liste, int Nr, string Modell, Matrix4x4 Lage, Netz Nah, Netz? Fern);

    /// <summary>Die Kandidaten nach gleichem Aussehen ordnen, Gruppen ab <see cref="MindestLagen"/>, die groessten <see cref="HoechstensSaetze"/>.</summary>
    internal static Instanzplan Planen(IEnumerable<Kandidat> kandidaten)
    {
        var gruppen = new Dictionary<string, List<Kandidat>>(StringComparer.Ordinal);
        var reihe = new List<string>();
        foreach (var k in kandidaten)
        {
            var schluessel = k.Ziel + "|" + k.Modell + "|" + Schluessel(k.Nah) + "|" + (k.Fern is null ? "-" : Schluessel(k.Fern));
            if (!gruppen.TryGetValue(schluessel, out var liste))
            {
                gruppen[schluessel] = liste = [];
                reihe.Add(schluessel);
            }
            liste.Add(k);
        }
        var gewaehlt = reihe.Where(s => gruppen[s].Count >= MindestLagen)
            .OrderByDescending(s => (long)gruppen[s].Count * (gruppen[s][0].Nah.Indizes.Count / 3))
            .ThenBy(s => reihe.IndexOf(s))
            .Take(HoechstensSaetze)
            .OrderBy(s => reihe.IndexOf(s))
            .ToList();
        var stuecke = new HashSet<int>();
        var baeume = new HashSet<int>();
        var gruen = new HashSet<int>();
        var saetze = new List<InstanzSatz>();
        foreach (var s in gewaehlt)
        {
            var g = gruppen[s];
            var kn = g[0].Nah.Kugel();
            var kf = g[0].Fern?.Kugel() ?? kn;
            var halbLokal = MathF.Max(kn.Halb, Vector3.Distance(kf.Mitte, kn.Mitte) + kf.Halb);
            var kugeln = new (Vector3, float)[g.Count];
            for (var i = 0; i < g.Count; i++)
            {
                var lage = g[i].Lage;
                var groesse = new Vector3(lage.M11, lage.M12, lage.M13).Length();
                kugeln[i] = (Vector3.Transform(kn.Mitte, lage), (halbLokal * groesse) + 1e-3f);
                (g[i].Liste switch { 's' => stuecke, 'b' => baeume, _ => gruen }).Add(g[i].Nr);
            }
            saetze.Add(new InstanzSatz(g[0].Ziel, g[0].Modell, g[0].Nah, g[0].Fern, [.. g.Select(k => k.Lage)], kugeln));
        }
        return new Instanzplan(stuecke, baeume, gruen, saetze);
    }

    /// <summary>Schluessel eines Netzes: Orte, Normalen und Indizes genau, Farben auf <see cref="FarbStufen"/> gerundet.</summary>
    private static string Schluessel(Netz netz)
    {
        var e = netz.EckenFeld;
        var werte = new int[e.Length];
        for (var i = 0; i < e.Length; i++)
        {
            werte[i] = i % Netz.Zahlen >= 6 ? (int)MathF.Round(e[i] * FarbStufen) : BitConverter.SingleToInt32Bits(e[i]);
        }
        using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        h.AppendData(MemoryMarshal.AsBytes(werte.AsSpan()));
        h.AppendData(MemoryMarshal.AsBytes(netz.IndizesFeld));
        return Convert.ToHexString(h.GetHashAndReset());
    }
}
