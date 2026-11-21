using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Sammelliste 383 (Weisung P12-298, Nico): in der Wohnung ist ein Moebel nur aus seinem Raum benutzbar. Steht die Figur
/// hinter einer Wand (<see cref="Wohnung.HinterWand(PyObjekt, double, double, double, double, double, double)"/>), bietet es
/// weder Text noch Knopf (auch kein „Hinsetzen“/„Hinlegen“); tippt man es von dort an, geht die Figur durch die Tuer vor
/// seine Vorderseite (<see cref="Wohnung.Standplaetze(PyObjekt, Wohnung.Aufgestellt)"/>) und erst dort oeffnet es sich.
/// Nur Darstellung und Bedienung: keine Spielzeit, kein Ereignis.
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>Gegenprobe (AsyncLocal): wie bis A 0.89.0, Moebel auch ueber die Wand benutzbar.</summary>
    public static readonly AsyncLocal<bool> MoebelWandAltFuerTests = new();

    private bool RaumRegel => Ort == SpielDaten.OrtZuhause && !MoebelWandAltFuerTests.Value;

    /// <summary>Filter fuer die Python-Regeln furniture_near (null = ohne Filter, wie Python).</summary>
    private Func<Wohnung.Aufgestellt, bool>? Nutzbar(double x, double z)
    {
        if (!RaumRegel)
        {
            return null;
        }
        var g = Wohnung.GrundrissVon(Ablauf.Stand);
        return m => !Wohnung.HinterWand(g, m, x, z);
    }

    /// <summary>Ein Platz zum Sitzen oder Liegen ist von (x, z) aus benutzbar (nicht hinter einer Wand).</summary>
    private bool PlatzNutzbar(Sitzgelegenheit s, double x, double z) =>
        !RaumRegel || !Wohnung.HinterWand(Wohnung.GrundrissVon(Ablauf.Stand), s.Rechteck.X1, s.Rechteck.Z1, s.Rechteck.X2 - s.Rechteck.X1,
            s.Rechteck.Z2 - s.Rechteck.Z1, x, z);

    /// <summary>
    /// Weg vor ein Rechteck (Moebel, Platz) im eigenen Raum: je Seite die erste Stelle, auf der die Figur stehen kann
    /// (<see cref="Wohnung.Standplaetze(PyObjekt, double, double, double, double, (double X, double Y)?, bool)"/>); die
    /// Vorderseite (bzw. die Stelle auf einem Teppich) gewinnt, wenn sie erreichbar ist, sonst der kuerzeste Weg.
    /// Leer = die Figur steht schon dort; null = keine Stelle erreichbar.
    /// </summary>
    private List<(double X, double Y)>? WegInDenRaum(Innenraum innen, double rx, double ry, double rw, double rh, (double X, double Y)? vorn,
        bool begehbar = false)
    {
        var g = Wohnung.GrundrissVon(Ablauf.Stand);
        if (SpielKern.Abstand(rx, ry, rw, rh, FigurX, FigurZ) <= Wohnung.MoebelReichweite && !Wohnung.HinterWand(g, rx, ry, rw, rh, FigurX, FigurZ))
        {
            return [];
        }
        List<(double X, double Y)>? bester = null;
        var erste = true;
        foreach (var seite in Wohnung.Standplaetze(g, rx, ry, rw, rh, vorn, begehbar))
        {
            var vorzug = erste && (vorn is not null || begehbar);
            erste = false;
            foreach (var (px, py) in seite)
            {
                if (!Wege.KannStehen(px, py, innen.Lauf))
                {
                    continue;
                }
                var weg = MitWeg(() => innen.Weg(FigurX, FigurZ, px, py));
                if (weg.Count == 0 || (weg[^1].X - px) * (weg[^1].X - px) + ((weg[^1].Y - py) * (weg[^1].Y - py)) > 0.25 * 0.25)
                {
                    continue;
                }
                if (vorzug)
                {
                    return weg;
                }
                if (bester is null || weg.Count < bester.Count)
                {
                    bester = weg;
                }
                break;
            }
        }
        return bester;
    }
}
