using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtSzene"/> mit Baeumen, Farben der Modelle
/// und dem Netz des Kleingruens. Reiner Umzug aus StadtSzene.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtSzene
{
    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 1): schiebt einen Baum, dessen Krone eine gesperrte Linie beruehrt, quer
    /// von ihr weg, bis er frei steht (z. B. der Baum der Wohnung, der sonst auf der Kreuzung stand).
    /// </summary>
    private sealed class BaumPlaner(List<(IReadOnlyList<Vector2> Linie, float Halb)> sperren)
    {
        public List<(IReadOnlyList<Vector2> Linie, float Halb)> Sperren { get; } = sperren;

        public List<(Vector2 Mitte, float R)> Gesetzt { get; } = [];

        public Vector2 Setzen(float x, float z, float r)
        {
            var p = new Vector2(x, z);
            for (var runde = 0; runde < 20; runde++)
            {
                var schlimmste = 0f;
                var weg = Vector2.Zero;
                foreach (var (linie, halb) in Sperren)
                {
                    var (naechster, richtung) = Naechster(p, linie);
                    var fehlt = halb + r + 0.02f - Vector2.Distance(p, naechster);
                    if (fehlt > schlimmste)
                    {
                        var weg1 = p - naechster;
                        weg = (weg1.LengthSquared() > 1e-8f ? Vector2.Normalize(weg1) : new Vector2(-richtung.Y, richtung.X)) * fehlt;
                        schlimmste = fehlt;
                    }
                }
                if (schlimmste <= 0)
                {
                    break;
                }
                p += weg;
            }
            Gesetzt.Add((p, r));
            return p;
        }

        /// <summary>Naechster Punkt der Linie und die Richtung ihres Abschnitts dort.</summary>
        private static (Vector2 Punkt, Vector2 Richtung) Naechster(Vector2 p, IReadOnlyList<Vector2> linie)
        {
            var best = (Punkt: linie[0], Richtung: Vector2.UnitX);
            var abstand = float.MaxValue;
            for (var i = 1; i < linie.Count; i++)
            {
                var (a, b) = (linie[i - 1], linie[i]);
                var ab = b - a;
                var t = ab.LengthSquared() > 0 ? Math.Clamp(Vector2.Dot(p - a, ab) / ab.LengthSquared(), 0f, 1f) : 0f;
                var q = a + (ab * t);
                if (Vector2.Distance(p, q) < abstand)
                {
                    abstand = Vector2.Distance(p, q);
                    best = (q, ab.LengthSquared() > 0 ? Vector2.Normalize(ab) : Vector2.UnitX);
                }
            }
            return best;
        }
    }

    /// <summary>
    /// A 0.43.1 (Weisung P12-39, Nico P12-37/P12-38: leere Stadt ganz frei): <c>stadtdaten.darstellung.kleingruen</c>.
    /// Nur der Wert false schaltet das Kleingruen an Strassenraendern und auf freien Flaechen ab; fehlt er (Fassung 0 und
    /// jede Stadt vor A 0.43.1), gilt "an" wie bisher. Einen anderen Wert meldet <c>StadtbauPruefung.Aussen</c> rot.
    /// </summary>
    public static bool KleingruenAn(PyObjekt? stadtDaten) =>
        !(stadtDaten?["darstellung"] is PyObjekt dar && dar["kleingruen"] is false);

    /// <summary>Phase 8 W7: Kleingruen (Buesche, Graeser, Steine, Beete; eigenes Netz), null ohne Grafik.</summary>
    public Gruen.Ergebnis? GruenKlein { get; private init; }

    /// <summary>Phase 8 W10 (Nico 3b): Nummern der eigenen Fenster in <see cref="Teile"/>, die nachts leuchten.</summary>
    public IReadOnlySet<int> LichtTeile { get; private init; } = new HashSet<int>();

    /// <summary>Phase 8 W11 (Nico P8-45): die 21 Strassenlaternen nach Nicos Skizze (<see cref="Nachtlicht.Plaetze"/>), einmal je Szene.</summary>
    public IReadOnlyList<Nachtlicht.Laterne> Laternen => _laternen ??= Nachtlicht.Plaetze(this);

    private List<Nachtlicht.Laterne>? _laternen;

    /// <summary>Fund 305: Laterne ohne freien Platz auslassen statt abbrechen (nur Ansichten des Werkzeugs, <see cref="Stadtbau.Nachsicht"/>).</summary>
    public bool LaternenOhnePlatzAuslassen { get; private init; }

    /// <summary>Phase 13 Welle 1: Wunschplaetze der Laternen (x, z, doppelt) aus der Stadt-Fassung, Reihenfolge = Nummer.</summary>
    public IReadOnlyList<(float X, float Z, bool Doppelt)> LaternenSkizze { get; private init; } = Nachtlicht.Skizze;

    /// <summary>Phase 8 W7/W11: Plaetze der Fest-Deko aller Festzeiten (Mitte, Radius); frei fuer Gruen und Laternen.</summary>
    public IReadOnlyList<(Vector2 P, float R)> FestPlaetzeAlle { get; private init; } = [];

    /// <summary>Mittellinien der Strassen (fuer die Autos, Phase 8 W7).</summary>
    public IReadOnlyList<IReadOnlyList<Vector2>> Strassen { get; private init; } = [];

    /// <summary>P8-28: Stuecke, deren Fenster an einer Tuer die Wandfarbe bekommen haben (Nummer in <see cref="Stuecke"/>).</summary>
    public IReadOnlyDictionary<int, GrafikModell> Umgefaerbt { get; private init; } = new Dictionary<int, GrafikModell>();

    /// <summary>
    /// Sammelliste 264: Haeuser mit Eintrag in <c>stadtdaten.farben</c> (Kennung = Ort oder Modellname eines Gebaeudes ohne
    /// Ort) bekommen ihre Farben; nur die Eckfarben aendern sich (<see cref="Gebaeudefarben"/>).
    /// </summary>
    internal static void FarbenAnwenden(PyObjekt? stadtDaten, GrafikBibliothek grafik, List<ModellStueck> stuecke, List<StadtHaus> haeuser,
        Dictionary<int, GrafikModell> umgefaerbt)
    {
        var farben = Gebaeudefarben.Lesen(stadtDaten);
        if (farben.Count == 0)
        {
            return;
        }
        foreach (var haus in haeuser)
        {
            var kennung = haus.Id.Contains('#', StringComparison.Ordinal) ? haus.Id[..haus.Id.LastIndexOf('#')] : haus.Id;
            if (!farben.TryGetValue(kennung, out var ziele))
            {
                continue;
            }
            var liste = new List<(int I, GrafikModell M, float Faktor, float Unten)>();
            for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
            {
                if (!ImPaket(stuecke[i].Name) && (umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name)) is { } m)
                {
                    var l = stuecke[i].Lage;
                    liste.Add((i, m, new Vector3(l.M11, l.M12, l.M13).Length(), l.M42));
                }
            }
            var toene = Gebaeudefarben.Bestimmen([.. liste.Select(x => (x.M, x.Faktor, x.Unten))]);
            var oben = liste.Count == 0 ? 0 : liste.Max(x => (x.M.Max.Y * x.Faktor) + x.Unten);
            foreach (var (i, m, faktor, unten) in liste)
            {
                var neu = Gebaeudefarben.Umfaerben(m, toene, ziele, faktor, unten, oben);
                if (!ReferenceEquals(neu, m))
                {
                    umgefaerbt[i] = neu;
                }
            }
        }
    }

    /// <summary>Modell des Stuecks Nummer i (umgefaerbt oder aus der Bibliothek).</summary>
    public GrafikModell? ModellAn(int i) => Umgefaerbt.GetValueOrDefault(i) ?? Grafik?.Modell(Stuecke[i].Name);

    /// <summary>W8b: Tueren der Orte mit Fussweg (eigene aus Kaesten oder die des CC0-Modells).</summary>
    public IReadOnlyList<StadtTuer> Tueren { get; private init; } = [];

    /// <summary>
    /// Welle A 0.58.0 (Weisung P12-148 Frage 1 = a): Bloecke (Grundflaechen x1, z1, x2, z2) je Ort, wie sie das Spiel fuer die
    /// Tuer nimmt (naechster Block zum Anfang des Fusswegs). Nur Auskunft fuer das Werkzeug, aendert nichts an der Stadt.
    /// </summary>
    public IReadOnlyDictionary<string, IReadOnlyList<(double X1, double Z1, double X2, double Z2)>> OrtBloecke { get; private init; } =
        new Dictionary<string, IReadOnlyList<(double X1, double Z1, double X2, double Z2)>>();

    /// <summary>W8b: Haeuser ohne Ort mit Gehweg, Tuer und Namensschild (nicht betretbar).</summary>
    public IReadOnlyList<StadtZierhaus> Zierhaeuser { get; private init; } = [];

    /// <summary>Phase 8 W9: Jahreszeit der Farben (nur Darstellung, Sommer = Farben wie vorher).</summary>
    public Jahreszeit Jahreszeit { get; private init; } = Jahreszeit.Sommer;

    /// <summary>
    /// Welle A 0.51.0 (Weisung P12-84, Sammelliste 237): Spieltag fuer die Wege der automatischen Autostrecken
    /// (<see cref="Autostrecken.Anlegen"/>): <see cref="Stadt.VerkehrTag"/> (Werkzeug: 1), sonst der Tag des Spielstands,
    /// ohne Spielstand 1. Nur Darstellung.
    /// </summary>
    public long Spieltag { get; private init; } = 1;

    /// <summary>Phase 8 W9: ab diesem Teil und diesem Modell steht die Fest-Deko (behaelt ihre Farben).</summary>
    public int FestTeileVon { get; private init; } = int.MaxValue;

    public int FestStueckeVon { get; private init; } = int.MaxValue;

    /// <summary>Netz des Kleingruens (nur in der Naehe gezeichnet), null = keins.</summary>
    public Netz? GruenNetzBauen() => GruenNetzBauen(null);

    /// <summary>Welle D2: wie <see cref="GruenNetzBauen()"/>, ohne die Stuecke aus <paramref name="plan"/>.</summary>
    public Netz? GruenNetzBauen(Instanzplan? plan) => GruenNetzBauen(plan, null);

    /// <summary>Welle Nachladen: wie <see cref="GruenNetzBauen(Instanzplan?)"/>, mit <paramref name="kachel"/> nur deren Teil.</summary>
    private Netz? GruenNetzBauen(Instanzplan? plan, Stadtbau.Stadtteile.Kachel? kachel)
    {
        if (GruenKlein is not { } g || Grafik is null)
        {
            return null;
        }
        var auswahl = kachel is { } k ? new Auswahl(k, false, Breite, Tiefe) : null;
        // Phase 8 W9: Kleingruen nach der Jahreszeit, im Winter Schnee auf Bueschel, Steinen und Beeten
        var netz = new Netz { Faerbung = Jahreszeitfarben.FuerTeile(Jahreszeit, Faerbeart.Klein) };
        foreach (var t in g.Teile)
        {
            if (auswahl is null || Nimmt(auswahl, 2, t))
            {
                netz.Hinzu(t);
            }
        }
        netz.Faerbung = null;
        var ohne = plan?.Gruen ?? Instanzplan.Leer.Gruen;
        var nr = -1;
        foreach (var st in g.Stuecke)
        {
            nr++;
            if (!ohne.Contains(nr) && Grafik.Modell(st.Name) is { } m && Nimmt(auswahl, st.Lage, m))
            {
                netz.Hinzu(m, st.Lage, ModellFaerbung(m, st.Lage, Faerbeart.Klein));
            }
        }
        return kachel is { } kk ? netz.NurKachel(kk, Breite, Tiefe) : netz;
    }

    /// <summary>Phase 8 W11: Tanne an (x, z)? Fest nach Lage, gerade und krumm im Wechsel; null = Laubbaum wie bisher.</summary>
    public static string? Tanne(float x, float z) =>
        ((((int)MathF.Round((x * 5) + (z * 11)) % 3) + 3) % 3) != 0 ? null
        : ((int)MathF.Round((x * 3) + (z * 7)) & 1) == 1 ? "gy-pine-crooked" : "gy-pine";

    /// <summary>Phase 8 W11: Name der Tanne in der Jahreszeit (im Herbst das orange Modell).</summary>
    public static string TanneIn(string name, Jahreszeit j) =>
        j == Jahreszeit.Herbst && name.StartsWith("gy-pine", StringComparison.Ordinal) ? name.Replace("gy-pine", "gy-pine-fall", StringComparison.Ordinal) : name;

    private static void Baum(List<Teil> teile, List<ModellStueck> stuecke, GrafikBibliothek? grafik, BaumPlaner planer, float x, float z, float r)
    {
        (x, z) = planer.Setzen(x, z, r) is var p ? (p.X, p.Y) : default;
        if (grafik is not null && Tanne(x, z) is { } tname)
        {
            // Phase 8 W11 (Sammelliste 160): etwa jeder dritte Baum eine Tanne aus dem Graveyard Kit (eigenes Netz,
            // PaketNetzBauen; im Herbst das orange Modell)
            if (grafik.Modell(tname) is { } tanne)
            {
                var s = (0.4f + (2.2f * r)) / (tanne.Max.Y - tanne.Min.Y) * 1.25f;
                var anker = new Vector3((tanne.Min.X + tanne.Max.X) / 2, tanne.Min.Y, (tanne.Min.Z + tanne.Max.Z) / 2);
                stuecke.Add(new ModellStueck(tname, Lage(tanne, anker, new Vector3(s), new Vector3(x, 0, z), Vector2.UnitY)));
                return;
            }
        }
        if (grafik is not null && Gruen.Baum(stuecke, grafik, x, z, r))
        {
            return;
        }
        // Suburban-Baum (gross/klein im Wechsel nach Lage), so hoch wie der eigene Baum (0,4 + 2,2 r)
        var name = ((int)MathF.Round((x * 7) + (z * 3)) & 1) == 0 ? "tree_large" : "tree_small";
        if (grafik?.Modell(name) is { } baum)
        {
            var s = (0.4f + (2.2f * r)) / (baum.Max.Y - baum.Min.Y) * 1.15f;
            var anker = new Vector3((baum.Min.X + baum.Max.X) / 2, baum.Min.Y, (baum.Min.Z + baum.Max.Z) / 2);
            stuecke.Add(new ModellStueck(name, Lage(baum, anker, new Vector3(s), new Vector3(x, 0, z), Vector2.UnitY)));
            return;
        }
        teile.Add(Quader(x - 0.07f, 0, z - 0.07f, x + 0.07f, 0.5f, z + 0.07f, Farben.Holz));
        teile.Add(Quader(x - r, 0.4f, z - r, x + r, 0.4f + (1.6f * r), z + r, Netz.Farbe("#34D399") * 0.8f));
        teile.Add(Quader(x - (r * 0.6f), 0.4f + (1.6f * r), z - (r * 0.6f), x + (r * 0.6f), 0.4f + (2.2f * r), z + (r * 0.6f), Netz.Farbe("#34D399") * 0.9f));
    }

    /// <summary>Fester "Wuerfel" wie _lit: brennt in diesem Fenster Licht? (SHA-256 von "seed|nr")</summary>
    public static bool Licht(string seed, int nr, double anteil)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed + "|" + nr.ToString(CultureInfo.InvariantCulture)));
        var wert = (hash[0] << 16) | (hash[1] << 8) | hash[2];
        return wert / (double)(1 << 24) < anteil;
    }

    private sealed record Block(float X1, float Z1, float X2, float Z2, float H);

    /// <summary>Gegenprobe Sammelliste 294: Kopien zeichnen sich nur ueber ihren eigenen Namen (wie bis A 0.65.0, nur Tests, je Ablauf).</summary>
    public static bool KopieAltFuerTests { get => KopieAlt.Value; set => KopieAlt.Value = value; }
    private static readonly AsyncLocal<bool> KopieAlt = new();
}
