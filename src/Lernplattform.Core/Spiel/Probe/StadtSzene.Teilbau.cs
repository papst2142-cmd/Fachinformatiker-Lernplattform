using System.Globalization;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 13 Welle 3 (Weisung P12-21, Frage 1 = a): Teilbau der Stadt fuer das Stadtbau-Werkzeug. Das Werkzeug baut die
/// Szene nach jeder Aenderung neu und gibt dabei einen <see cref="Vorrat"/> mit: was sich nicht geaendert hat (die Orte
/// mit Haus, Tuer und Fussweg, die Netze der Haeuser und das Paket-Netz), wird aus dem letzten Bau uebernommen, nur
/// Geaendertes neu gebaut. Uebernommen wird nur bei genau gleichen Eingaben (Inhalt als Text, Gleitkommazahlen als
/// Bitmuster, Modelle als dieselben Objekte); das Ergebnis ist so dasselbe wie beim vollen Bau (Test
/// <c>TeilbauTests</c> mit Gegenprobe). Das Spiel baut ohne Vorrat und damit genau wie vorher.
/// </summary>
public sealed partial class StadtSzene
{
    /// <summary>Vorrat des letzten Baus (nur Werkzeug). Ein Vorrat gehoert zu einer Grafik; Zugriff nur nacheinander (Sperre).</summary>
    public sealed class Vorrat
    {
        internal readonly object Sperre = new();

        internal GrafikBibliothek? Grafik;

        internal Dictionary<string, OrtBau> Orte = [];

        internal Dictionary<string, (HausEingang Ein, Netz Netz, Netz Fern)> Haeuser = [];

        internal (PaketEingang Ein, Netz Nah, Netz Fern)? Paket;

        /// <summary>Zaehlung des letzten Baus: uebernommene und neu gebaute Teile (Orte, Hausnetze, Paket-Netz).</summary>
        public int Uebernommen { get; internal set; }

        public int NeuGebaut { get; internal set; }

        /// <summary>Nur fuer die Gegenprobe der Tests: der erste Ort wird auch bei geaenderten Eingaben uebernommen.</summary>
        internal bool FalschUebernehmenFuerTests { get; set; }

        internal void Beginnen(GrafikBibliothek? grafik)
        {
            if (!ReferenceEquals(Grafik, grafik))
            {
                Orte = [];
                Haeuser = [];
                Paket = null;
                Grafik = grafik;
            }
            Uebernommen = 0;
            NeuGebaut = 0;
        }
    }

    /// <summary>Ein Ort aus einem Bau: alles, was die Schleife der Orte anhaengt, mit Stellen relativ zum Anfang des Orts.</summary>
    internal sealed record OrtBau(string Schluessel, Teil[] Teile, ModellStueck[] Stuecke, (double, double, double, double)[] Hindernisse,
        (Vector2 Mitte, float R)[] Baeume, (int Teil, Vector3 Wand)[] Fenster, (int Stueck, GrafikModell Modell)[] Umgefaerbt, StadtTuer[] Tueren,
        StadtHaus? Haus, StadtOrt Ort, string Signatur);

    /// <summary>Eingaben eines Hausnetzes (siehe <see cref="HausNetz"/>).</summary>
    internal sealed record HausEingang(string Id, int StueckeVon, Jahreszeit Jahreszeit, Teil[] Teile, bool[] Licht, ModellStueck[] Stuecke, GrafikModell?[] Modelle)
    {
        public bool GleichWie(HausEingang b) => Id == b.Id && StueckeVon == b.StueckeVon && Jahreszeit == b.Jahreszeit
            && TeileGleich(Teile, b.Teile) && Licht.AsSpan().SequenceEqual(b.Licht) && StueckeGleich(Stuecke, b.Stuecke) && ModelleGleich(Modelle, b.Modelle);
    }

    /// <summary>Eingaben der Paket-Netze (siehe <see cref="PaketNetzBauen"/>).</summary>
    internal sealed record PaketEingang(Jahreszeit Jahreszeit, string Kreuze, ModellStueck[] Stuecke, bool[] VorFest, string Laternen)
    {
        public bool GleichWie(PaketEingang b) => Jahreszeit == b.Jahreszeit && Kreuze == b.Kreuze && StueckeGleich(Stuecke, b.Stuecke)
            && VorFest.AsSpan().SequenceEqual(b.VorFest) && Laternen == b.Laternen;
    }

    private Vorrat? _vorrat;

    internal static bool TeileGleich(Teil[] a, Teil[] b) =>
        MemoryMarshal.AsBytes(a.AsSpan()).SequenceEqual(MemoryMarshal.AsBytes(b.AsSpan()));

    internal static bool StueckeGleich(ModellStueck[] a, ModellStueck[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (a[i].Name != b[i].Name || !LageGleich(a[i].Lage, b[i].Lage))
            {
                return false;
            }
        }
        return true;
    }

    private static bool LageGleich(Matrix4x4 a, Matrix4x4 b) =>
        MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in a)).SequenceEqual(MemoryMarshal.AsBytes(new ReadOnlySpan<Matrix4x4>(in b)));

    private static bool ModelleGleich(GrafikModell?[] a, GrafikModell?[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (var i = 0; i < a.Length; i++)
        {
            if (!ReferenceEquals(a[i], b[i]))
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Gleitkommazahl als Bitmuster (fuer die Schluessel: 0 und -0 sind verschieden).</summary>
    private static string Bits(float f) => BitConverter.SingleToInt32Bits(f).ToString("x8", CultureInfo.InvariantCulture);

    private static string Bits(Vector2 v) => Bits(v.X) + "," + Bits(v.Y);

    /// <summary>Pruefsumme der Baum-Sperren (Linien und Abstand): Baeume in einem Haus weichen ihnen aus.</summary>
    private static string SperrenText(List<(IReadOnlyList<Vector2> Linie, float Halb)> sperren)
    {
        var sb = new StringBuilder();
        foreach (var (linie, halb) in sperren)
        {
            sb.Append(Bits(halb)).Append(':');
            foreach (var p in linie)
            {
                sb.Append(Bits(p)).Append(';');
            }
            sb.Append('|');
        }
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    /// <summary>Alle Eingaben eines Orts als Text (Inhalt des Orts, sichtbare Teile seines Modells, Fussweg, Tuer, Sperren).</summary>
    private static string OrtSchluessel(Stadt stadt, PyObjekt ort, string id, List<(int Nr, PyObjekt Teil)> sichtbar, float? modellTuer, bool sichtbarerWeg, string sperren)
    {
        var sb = new StringBuilder();
        sb.Append(PythonJson.Standard(ort)).Append('\n');
        foreach (var (nr, teil) in sichtbar)
        {
            sb.Append(nr.ToString(CultureInfo.InvariantCulture)).Append('=').Append(PythonJson.Standard(teil)).Append('\n');
        }
        sb.Append(SpielDaten.Get(stadt.Zugaenge, id) is PyObjekt zugang ? PythonJson.Standard(zugang) : "-").Append('\n');
        sb.Append(stadt.Tuer(id) is { } zelle ? zelle.Item1.ToString(CultureInfo.InvariantCulture) + "," + zelle.Item2.ToString(CultureInfo.InvariantCulture) : "-");
        sb.Append('|').Append(BitConverter.DoubleToInt64Bits(stadt.Zelle).ToString(CultureInfo.InvariantCulture));
        sb.Append('|').Append(modellTuer is { } v ? Bits(v) : "-").Append('|').Append(sichtbarerWeg).Append('|').Append(sperren);
        // Welle E: die Drehung steht nicht am Ort, sondern in stadtdaten.darstellung.hausdreh
        sb.Append("|dreh").Append(Hausdrehung.VonOrt(stadt.Daten, id).ToString(CultureInfo.InvariantCulture));
        // S 0.31.0: Farbe der Tuer (die Farben von Fassade, Dach, Rahmen kommen erst nach dem Teilbau dazu)
        sb.Append("|tuer").Append(Gebaeudefarben.Tuer(stadt.Daten, id)?.ToString() ?? "-");
        return sb.ToString();
    }

    /// <summary>Den Ort aus dem Vorrat anhaengen (Stellen auf den heutigen Anfang verschoben).</summary>
    private static void OrtEinsetzen(OrtBau alt, List<Teil> teile, List<ModellStueck> stuecke, List<(double, double, double, double)> hindernisse,
        BaumPlaner baeume, List<(int Teil, Vector3 Wand)> alleFenster, Dictionary<int, GrafikModell> umgefaerbt, List<StadtTuer> tueren,
        List<StadtHaus> haeuser, List<StadtOrt> orte, List<string> signatur)
    {
        var (tv, sv) = (teile.Count, stuecke.Count);
        teile.AddRange(alt.Teile);
        stuecke.AddRange(alt.Stuecke);
        hindernisse.AddRange(alt.Hindernisse);
        baeume.Gesetzt.AddRange(alt.Baeume);
        alleFenster.AddRange(alt.Fenster.Select(f => (f.Teil + tv, f.Wand)));
        foreach (var (i, m) in alt.Umgefaerbt)
        {
            umgefaerbt[i + sv] = m;
        }
        tueren.AddRange(alt.Tueren);
        if (alt.Haus is { } h)
        {
            haeuser.Add(h with { TeileVon = h.TeileVon + tv, TeileBis = h.TeileBis + tv, StueckeVon = h.StueckeVon + sv, StueckeBis = h.StueckeBis + sv });
        }
        orte.Add(alt.Ort);
        signatur.Add(alt.Signatur);
    }

    /// <summary>Was die Schleife fuer einen Ort angehaengt hat, als <see cref="OrtBau"/> festhalten.</summary>
    private static OrtBau OrtFesthalten(string schluessel, int tv, int sv, int hv, int bv, int fv, int tuerVon, int hausVon,
        List<Teil> teile, List<ModellStueck> stuecke, List<(double, double, double, double)> hindernisse, BaumPlaner baeume,
        List<(int Teil, Vector3 Wand)> alleFenster, Dictionary<int, GrafikModell> umgefaerbt, List<StadtTuer> tueren, List<StadtHaus> haeuser,
        List<StadtOrt> orte, List<string> signatur)
    {
        var haus = haeuser.Count > hausVon ? haeuser[^1] : null;
        return new OrtBau(schluessel, [.. teile.Skip(tv)], [.. stuecke.Skip(sv)], [.. hindernisse.Skip(hv)], [.. baeume.Gesetzt.Skip(bv)],
            [.. alleFenster.Skip(fv).Select(f => (f.Teil - tv, f.Wand))],
            [.. umgefaerbt.Where(u => u.Key >= sv).OrderBy(u => u.Key).Select(u => (u.Key - sv, u.Value))],
            [.. tueren.Skip(tuerVon)],
            haus is null ? null : haus with { TeileVon = haus.TeileVon - tv, TeileBis = haus.TeileBis - tv, StueckeVon = haus.StueckeVon - sv, StueckeBis = haus.StueckeBis - sv },
            orte[^1], signatur[^1]);
    }

    private HausEingang HausEingangFuer(StadtHaus haus)
    {
        var n = haus.StueckeBis - haus.StueckeVon;
        var modelle = new GrafikModell?[n];
        for (var i = 0; i < n; i++)
        {
            modelle[i] = ImPaket(Stuecke[haus.StueckeVon + i].Name) ? null : ModellAn(haus.StueckeVon + i);
        }
        return new HausEingang(haus.Id, haus.StueckeVon, Jahreszeit,
            [.. Teile.Skip(haus.TeileVon).Take(haus.TeileBis - haus.TeileVon)],
            [.. Enumerable.Range(haus.TeileVon, haus.TeileBis - haus.TeileVon).Select(LichtTeile.Contains)],
            [.. Stuecke.Skip(haus.StueckeVon).Take(n)], modelle);
    }

    /// <summary>Hausnetz aus dem Vorrat, wenn alle Eingaben gleich sind, sonst neu gebaut und gemerkt.</summary>
    private (Netz Nah, Netz Fern) HausNetzMitVorrat(StadtHaus haus, Dictionary<string, (HausEingang, Netz, Netz)> neu)
    {
        if (_vorrat is not { } v)
        {
            return (HausNetz(haus), HausNetz(haus, fern: true));
        }
        var ein = HausEingangFuer(haus);
        Netz netz, fern;
        if (v.Haeuser.TryGetValue(haus.Id, out var alt) && alt.Ein.GleichWie(ein))
        {
            (netz, fern) = (alt.Netz, alt.Fern);
            v.Uebernommen++;
        }
        else
        {
            (netz, fern) = (HausNetz(haus), HausNetz(haus, fern: true));
            v.NeuGebaut++;
        }
        neu[haus.Id] = (ein, netz, fern);
        return (netz, fern);
    }

    private PaketEingang PaketEingangFuer()
    {
        var kreuze = new StringBuilder();
        foreach (var k in Andreaskreuze)
        {
            kreuze.Append(Bits(k.Fuss)).Append(';').Append(Bits(k.Blick)).Append(';').Append(Bits(k.ZurStrasse)).Append('|');
        }
        var laternen = new StringBuilder();
        foreach (var l in Laternen)
        {
            laternen.Append(l.Nr.ToString(CultureInfo.InvariantCulture)).Append(';').Append(Bits(l.Ort)).Append(';').Append(l.Doppelt)
                .Append(';').Append(Bits(l.Richtung)).Append(';').Append(Bits(l.Ziel)).Append('|');
        }
        var stellen = Enumerable.Range(0, Stuecke.Count).Where(i => ImPaket(Stuecke[i].Name)).ToList();
        return new PaketEingang(Jahreszeit, kreuze.ToString(), [.. stellen.Select(i => Stuecke[i])], [.. stellen.Select(i => i < FestStueckeVon)], laternen.ToString());
    }

    /// <summary>Paket-Netze (nah, fern) aus dem Vorrat, wenn alle Eingaben gleich sind.</summary>
    private (Netz? Nah, Netz? Fern) PaketMitVorrat()
    {
        if (Grafik is null)
        {
            return (null, null);
        }
        if (_vorrat is not { } v)
        {
            return (PaketNetzBauen(false), PaketNetzBauen(true));
        }
        var ein = PaketEingangFuer();
        if (v.Paket is { } alt && alt.Ein.GleichWie(ein))
        {
            v.Uebernommen++;
            return (alt.Nah, alt.Fern);
        }
        var (nah, fern) = (PaketNetzBauen(false), PaketNetzBauen(true));
        v.NeuGebaut++;
        v.Paket = (ein, nah, fern);
        return (nah, fern);
    }
}
