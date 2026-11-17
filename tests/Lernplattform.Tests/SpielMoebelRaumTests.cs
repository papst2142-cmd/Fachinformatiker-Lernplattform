using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Sammelliste 383 (Weisung P12-298, Nico): ein Moebel ist nur aus seinem Raum benutzbar. Fuer jede Wohnung, jedes Moebel
/// des Moebelhauses, jeden Raum (mit Diele), jede Drehung und jede Wand: steht die Figur auf der anderen Seite der Wand (in
/// Reichweite, ein Wandstueck dazwischen), bietet das Moebel nichts an (kein Text mit seinem Namen, kein Knopf: Lernen,
/// Schlafen, Kleiderschrank, PC, Reise, Hinsetzen, Hinlegen). Tippt man es von dort an, geht die Figur durch die Tuer in
/// seinen Raum (nicht mehr hinter der Wand) und erst dort bietet es sich an. Gegenprobe (Stand bis A 0.89.0): ueber die Wand
/// benutzbar.
/// </summary>
public class SpielMoebelRaumTests
{
    private static PyObjekt Inhalt => SpielCharakterTests.InhaltMitNeuen.Value;

    private static readonly string[] Angebote =
        ["lernen", "schlafen", Charakter.Aktion, Spielwelt.AktionComputer, Spielwelt.AktionReise, StadtSpiel.AktionHinsetzen, StadtSpiel.AktionHinlegen];

    private static Spielablauf NeuerAblauf()
    {
        var zaehler = 0;
        var a = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 11, 6).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        a.Profil("Nico", new PyObjekt { { "haut", "mittel" }, { "frisur", "locken" }, { "haarfarbe", "braun" }, { "oberteil", "gruen" }, { "kreis", "cyan" } });
        a.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 90000L } });
        return a;
    }

    private static void Ankommen(StadtSpiel s)
    {
        for (var i = 0; i < 4000 && s.Fortschreiten(0.05); i++)
        {
        }
    }

    /// <summary>Bietet das Moebel (das einzige aufgestellte) an der Stelle der Figur etwas an?</summary>
    private static bool Bietet(StadtSpiel s, string name) =>
        s.Zeile.Split('\n').Any(z => z == name) || s.Knoepfe.Any(k => Angebote.Contains(k.Aktion));

    private static (int Geprueft, List<string> Funde, int UeberWand) Pruefen(int wohnungenHoechstens = int.MaxValue)
    {
        var funde = new List<string>();
        var geprueft = 0;
        var ueberWand = 0;
        var gesehen = new HashSet<string>();
        foreach (var bleibe in SpielDaten.Liste(SpielDaten.Objekt(Inhalt["wohnungen"])["wohnungen"]).Select(SpielDaten.Objekt).Take(wohnungenHoechstens))
        {
            var g = SpielDaten.Objekt(bleibe["gebaeude"]);
            var bereiche = SpielDaten.Liste(g["raeume"]).Select(SpielDaten.Objekt)
                .Select(r => (Id: Py.Str(r["id"]), X: Py.F(r["x"]), Y: Py.F(r["y"]), W: Py.F(r["w"]), H: Py.F(r["h"])))
                .Append((Id: "diele", X: 0.0, Y: Py.F(SpielDaten.Objekt(g["flur"])["y"]), W: Py.F(g["breite"]), H: Py.F(SpielDaten.Objekt(g["flur"])["h"])))
                .ToList();
            foreach (var moebel in Wohnung.Laden(Inhalt))
            {
                var id = Py.Str(moebel["id"]);
                var name = Py.Str(moebel["name"]);
                var ablauf = NeuerAblauf();
                if (!Py.Gleich(bleibe["id"], ablauf.Stand.Wohnung))
                {
                    ablauf.Umziehen(bleibe["id"], false);
                }
                foreach (var m in Wohnung.AufgestellteMoebel(ablauf.Stand))
                {
                    ablauf.Ausfuehren(SpielAktionen.Einpacken(ablauf.Stand, m.Stueck));
                }
                var probe = "probe-" + id;
                ablauf.Ausfuehren(SpielAktionen.MoebelKaufen(ablauf.Stand, id, probe));
                var s = new StadtSpiel(new Stadt(Inhalt, SpielStadtTests.Daten.Value), ablauf, SpielGrafikTests.Bibliothek.Value,
                    SpielInnenraumTests.Innenraeume.Value, SpielGeschaefteTests.Daten.Value, null, () => null, () => Jahreszeit.Sommer);
                Assert.True(s.Betreten(), s.Zeile);
                foreach (var b in bereiche)
                {
                    for (var dreh = 0L; dreh < 4; dreh++)
                    {
                        foreach (var (cx, cy) in new[] { (b.X + (b.W / 2), b.Y), (b.X + (b.W / 2), b.Y + b.H), (b.X, b.Y + (b.H / 2)), (b.X + b.W, b.Y + (b.H / 2)) })
                        {
                            var (x, y) = Wohnung.Andocken(ablauf.Stand, id, cx, cy, dreh);
                            if (Wohnung.PlatzProblem(ablauf.Stand, probe, id, x, y, dreh) != ""
                                || !gesehen.Add($"{Py.Str(bleibe["id"])}|{id}|{Py.Str(x)}|{Py.Str(y)}|{dreh}"))
                            {
                                continue;
                            }
                            ablauf.Ausfuehren(SpielAktionen.AufstellenBei(ablauf.Stand, probe, cx, cy, dreh));
                            s.Verlassen();
                            Assert.True(s.Betreten(), s.Zeile);
                            var m = Wohnung.AufgestellteMoebel(ablauf.Stand).Single(a => a.Stueck == probe);
                            var (rw, rh) = (Py.F(m.W), Py.F(m.H));
                            var wo = string.Create(CultureInfo.InvariantCulture, $"{Py.Str(bleibe["id"])} {b.Id} {id} ({m.X}, {m.Y}, dreh {dreh})");
                            // Stellen hinter jeder Wand, an der das Moebel steht: Kantenmitte + (Wand + 0,45) nach aussen
                            foreach (var (sx, sy) in new[] { (0, 1), (0, -1), (1, 0), (-1, 0) })
                            {
                                var px = m.X + (rw / 2) + (sx * ((rw / 2) + Wege.WandInnen + Wohnung.VorDemMoebel));
                                var py = m.Y + (rh / 2) + (sy * ((rh / 2) + Wege.WandInnen + Wohnung.VorDemMoebel));
                                if (!Wohnung.HinterWand(g, m, px, py) || !Wege.KannStehen(px, py, s.Innen!.Lauf))
                                {
                                    continue;
                                }
                                s.GeheZu(px, py);
                                Ankommen(s);
                                if (Math.Abs(s.FigurX - px) > 0.3 || Math.Abs(s.FigurZ - py) > 0.3 || !Wohnung.HinterWand(g, m, s.FigurX, s.FigurZ))
                                {
                                    continue;
                                }
                                geprueft++;
                                if (Bietet(s, name))
                                {
                                    ueberWand++;
                                    funde.Add($"{wo} von ({px:0.00}, {py:0.00}) ueber die Wand: {s.Zeile.Replace('\n', ' ')} | {string.Join(",", s.Knoepfe.Select(k => k.Aktion))}");
                                }
                                // antippen: durch die Tuer in den Raum des Moebels. Ausnahme: ein Stueck ohne Nutzen (ohne Aktion
                                // und Text, begehbar), vor dem keine Stelle frei ist (Bild in der Wand): dort gibt es nichts zu benutzen
                                if (Wohnung.Begehbar.Contains(m.Typ) && !Py.Wahr(SpielDaten.Get(moebel, "aktion")) && !Py.Wahr(SpielDaten.Get(moebel, "texte"))
                                    && !Wohnung.Standplaetze(g, m).SelectMany(l => l).Any(p => Wege.KannStehen(p.X, p.Y, s.Innen!.Lauf)))
                                {
                                    continue;
                                }
                                s.GeheZu(m.X + (rw / 2), m.Y + (rh / 2));
                                Ankommen(s);
                                var (fx, fz) = s.MeinPlatz is null ? ((double)s.FigurX, (double)s.FigurZ) : (px, py);
                                if (s.MeinPlatz is null && Wohnung.HinterWand(g, m, fx, fz))
                                {
                                    funde.Add($"{wo} von ({px:0.00}, {py:0.00}) angetippt: Figur bleibt hinter der Wand ({fx:0.00}, {fz:0.00})");
                                }
                                s.GeheZu(px, py);
                                Ankommen(s);
                            }
                        }
                    }
                }
            }
        }
        return (geprueft, funde, ueberWand);
    }

    [Fact]
    public void JedesMoebelJederWohnung_NieUeberDieWand_AntippenFuehrtDurchDieTuer()
    {
        var (geprueft, funde, _) = Pruefen();
        Assert.True(funde.Count == 0, funde.Count + " Funde:\n" + string.Join("\n", funde.Take(40)));
        Assert.True(geprueft >= 100, "geprueft: " + geprueft);
    }

    /// <summary>Gegenprobe: mit dem Stand bis A 0.89.0 bietet sich ein Moebel ueber die Wand an.</summary>
    [Fact]
    public void Gegenprobe_StandBisA0890_UeberDieWandBenutzbar()
    {
        StadtSpiel.MoebelWandAltFuerTests.Value = true;
        try
        {
            var (geprueft, _, ueberWand) = Pruefen(1);
            Assert.True(ueberWand > 0, $"geprueft {geprueft}, ueber die Wand {ueberWand}");
        }
        finally
        {
            StadtSpiel.MoebelWandAltFuerTests.Value = false;
        }
    }
}
