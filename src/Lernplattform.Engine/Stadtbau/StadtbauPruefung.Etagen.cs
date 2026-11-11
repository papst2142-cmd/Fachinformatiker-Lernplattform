using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Stadtbau;

/// <summary>Ein Fund der Etagen-Pruefung mit der Etage, zu der er gehoert (0 = Erdgeschoss, auch fuer Funde am ganzen Haus).</summary>
public sealed record EtagenFund(int Etage, StadtbauFund Fund);

/// <summary>
/// Phase 13 Welle 5 „Etagen“ (Weisung P12-73, Plan P12-23b Abschnitt 3, Nico 3a 4a): Regeln fuer die Etagen eines
/// Innenraums und die Uebergaenge (Treppe, Fahrstuhl) zwischen ihnen. Gemeinsam fuer Werkzeug, CI und die Ladepruefung
/// des Spiels. Ohne Etagen (Fassung 0) gibt es keinen Fund.
/// </summary>
public static partial class StadtbauPruefung
{
    /// <summary>
    /// Alle Regeln der Etagen des Orts <paramref name="ort"/> mit dem Grundriss des Erdgeschosses <paramref name="eg"/>.
    /// <paramref name="innenPruefen"/> prueft den Grundriss einer Etage (Vorgabe <see cref="Innen"/> ohne Personen; das
    /// Werkzeug gibt die Pruefung mit Katalog-Stuecken mit).
    /// </summary>
    public static List<EtagenFund> Etagen(string ort, PyObjekt eg, GrafikBibliothek? grafik,
        Func<int, PyObjekt, List<StadtbauFund>>? innenPruefen = null)
    {
        var funde = new List<EtagenFund>();
        if (!Stadtbau.Etagen.Hat(eg))
        {
            return funde;
        }
        void Rot(int etage, string text, string tipp, double? x = null, double? y = null) =>
            funde.Add(new EtagenFund(etage, new StadtbauFund(text, tipp, true, x, y)));
        string Name(int k) => Stadtbau.Etagen.Name(eg, k);

        // 1. Daten lesbar
        if (Get(eg, Stadtbau.Etagen.FeldEtagen) is { } rohEtagen
            && (rohEtagen is not List<object?> etagenListe || etagenListe.Any(e => e is not PyObjekt o || Get(o, "gebaeude") is not PyObjekt
                || (Py.Hat(o, "name") && Get(o, "name") is not string))))
        {
            Rot(0, "Die Etagen sind falsch gespeichert.", "Letzten Schritt rückgängig machen.");
            return funde;
        }
        if (Get(eg, Stadtbau.Etagen.FeldUebergaenge) is { } rohU
            && (rohU is not List<object?> uListe || uListe.Any(u => u is not PyObjekt o || Get(o, "id") is not string || Get(o, "art") is not string
                || Get(o, "halte") is not List<object?> halte || halte.Any(h => h is not PyObjekt ho || !Py.IstNurInt(Get(ho, "etage"))
                    || Get(ho, "ankunft") is not List<object?> { Count: 3 } a || !Stadtbau.Etagen.IstZahl(a[0]) || !Stadtbau.Etagen.IstZahl(a[1]) || !Py.IstNurInt(a[2])))))
        {
            Rot(0, "Treppen oder Fahrstühle sind falsch gespeichert.", "Letzten Schritt rückgängig machen.");
            return funde;
        }

        // 2. hoechstens EG + 4 (Nico 4a)
        var anzahl = Stadtbau.Etagen.Anzahl(eg);
        if (anzahl > Stadtbau.Etagen.Hoechstens)
        {
            Rot(0, $"Das Haus hat {anzahl} Etagen über dem Erdgeschoss; mehr als {Stadtbau.Etagen.Hoechstens} gehen nicht.",
                "Die oberste Etage löschen.");
        }

        // 3. keine Etage groesser als das Erdgeschoss (Nico 3a), 4. Grundriss jeder Etage
        double egBreite = Py.F(eg["breite"]), egHoehe = Py.F(eg["hoehe"]);
        for (var k = 1; k <= anzahl; k++)
        {
            var g = Stadtbau.Etagen.Gebaeude(eg, k);
            if (!Stadtbau.Etagen.IstZahl(Get(g, "breite")) || !Stadtbau.Etagen.IstZahl(Get(g, "hoehe")) || Get(g, "flur") is not PyObjekt
                || Get(g, "raeume") is not List<object?>)
            {
                Rot(k, $"Das {Name(k)} ist falsch gespeichert.", "Letzten Schritt rückgängig machen.");
                continue;
            }
            if (Py.F(g["breite"]) > egBreite + 1e-9 || Py.F(g["hoehe"]) > egHoehe + 1e-9)
            {
                Rot(k, $"Das {Name(k)} ist größer als das Erdgeschoss.",
                    "Eine Etage liegt ganz in den Außenwänden des Erdgeschosses; Breite und Tiefe verkleinern.");
            }
            foreach (var f in innenPruefen?.Invoke(k, g) ?? Innen(ort, g, [], grafik, etage: k))
            {
                funde.Add(new EtagenFund(k, f));
            }
        }

        // 5. Uebergaenge: Art, Halte, Gegenstueck
        var uebergaenge = Stadtbau.Etagen.Uebergaenge(eg, ort);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var u in uebergaenge)
        {
            var erste = u.Halte.Count > 0 ? u.Halte[0].Etage : 0;
            if (!ids.Add(u.Id))
            {
                Rot(erste, "Zwei Übergänge haben dieselbe Kennung.", "Einen der beiden löschen und neu setzen.");
            }
            if (!Stadtbau.Etagen.Arten.Contains(u.Art))
            {
                Rot(erste, $"„{u.Art}“ ist keine bekannte Art von Übergang.", "Nur Treppe und Fahrstuhl verbinden Etagen.");
                continue;
            }
            var stueckName = InnenKatalog.Name(u.Art);
            if (u.Halte.Select(h => h.Etage).Distinct().Count() != u.Halte.Count)
            {
                Rot(erste, $"{Nominativ(u.Art, false)} hält zweimal auf derselben Etage.", "Stück löschen und neu setzen.");
                continue;
            }
            if (u.Art == Stadtbau.Etagen.Treppe && (u.Halte.Count != 2 || Math.Abs(u.Halte[0].Etage - u.Halte[1].Etage) != 1))
            {
                Rot(erste, "Eine Treppe verbindet genau zwei Etagen direkt übereinander.", "Bei „Ziel“ die Etage darüber oder darunter wählen.");
            }
            if (u.Art == Stadtbau.Etagen.Fahrstuhl && u.Halte.Count < 2)
            {
                Rot(erste, "Der Fahrstuhl hält nur auf einer Etage.", "Bei „Ziel“ eine weitere Etage wählen.");
            }
            foreach (var h in u.Halte)
            {
                if (!Stadtbau.Etagen.Gibt(eg, h.Etage))
                {
                    Rot(0, $"{stueckName} führt zu einer Etage, die es nicht gibt.", "Stück löschen und neu setzen.");
                    continue;
                }
                var stuecke = Stadtbau.Etagen.Stuecke(Stadtbau.Etagen.Gebaeude(eg, h.Etage), u.Id);
                if (stuecke.Count == 0)
                {
                    Rot(h.Etage, $"Zu einem Stück „{stueckName}“ fehlt das Gegenstück im {Name(h.Etage)}.",
                        "Das Werkzeug legt das Gegenstück beim Wählen des Ziels an; Stück löschen und neu setzen.", h.X, h.Y);
                }
                else if (stuecke.Count > 1 || stuecke.Any(s => Py.Str(s["typ"]) != u.Art))
                {
                    Rot(h.Etage, $"Im {Name(h.Etage)} gehören mehrere oder falsche Stücke zu einem Übergang.", "Stück löschen und neu setzen.",
                        Py.F(stuecke[0]["x"]), Py.F(stuecke[0]["y"]));
                }
            }
        }

        // 6. jedes Stueck fuehrt irgendwohin
        for (var k = 0; k <= anzahl; k++)
        {
            if (!Stadtbau.Etagen.Gibt(eg, k))
            {
                continue;
            }
            foreach (var d in Stadtbau.Etagen.Einrichtung(Stadtbau.Etagen.Gebaeude(eg, k)))
            {
                var typ = Py.Str(d["typ"]);
                var link = Get(d, Stadtbau.Etagen.FeldLink) as string;
                if (Stadtbau.Etagen.IstStueck(typ) && (link is null || uebergaenge.FirstOrDefault(u => u.Id == link) is not { } u || u.Auf(k) is null))
                {
                    Rot(k, $"{Nominativ(typ, true)} im {Name(k)} führt noch nirgends hin.", "Stück antippen und rechts bei „Ziel“ eine Etage wählen.",
                        Py.F(d["x"]) + (Py.F(d["w"]) / 2), Py.F(d["y"]) + (Py.F(d["h"]) / 2));
                }
                else if (!Stadtbau.Etagen.IstStueck(typ) && link is not null)
                {
                    Rot(k, $"„{InnenKatalog.Name(typ)}“ im {Name(k)} ist mit einem Übergang verknüpft.", "Stück löschen und neu setzen.",
                        Py.F(d["x"]), Py.F(d["y"]));
                }
            }
        }

        // 7. jede Etage ist vom Erdgeschoss aus erreichbar
        var erreicht = new HashSet<int> { 0 };
        for (var neu = true; neu;)
        {
            neu = false;
            foreach (var u in uebergaenge.Where(u => Stadtbau.Etagen.Arten.Contains(u.Art) && u.Halte.Any(h => erreicht.Contains(h.Etage))))
            {
                foreach (var h in u.Halte.Where(h => Stadtbau.Etagen.Gibt(eg, h.Etage)))
                {
                    neu |= erreicht.Add(h.Etage);
                }
            }
        }
        for (var k = 1; k <= anzahl; k++)
        {
            if (!erreicht.Contains(k))
            {
                Rot(k, $"Das {Name(k)} ist vom Erdgeschoss aus nicht erreichbar.", "Eine Treppe oder einen Fahrstuhl zu dieser Etage setzen.");
            }
        }

        // 8. Ankunft frei und 9. von der Ankunft aus ist jede Ware und jeder Uebergang erreichbar
        for (var k = 0; k <= anzahl; k++)
        {
            if (!Stadtbau.Etagen.Gibt(eg, k) || funde.Any(f => f.Etage == k && f.Fund.Rot && f.Fund.Text.Contains("falsch gespeichert", StringComparison.Ordinal)))
            {
                continue;
            }
            var g = Stadtbau.Etagen.Gebaeude(eg, k);
            Innenraum innen;
            try
            {
                innen = Innenraum.AusGrundrissMitPersonen(Ansicht(ort, k), g, [], grafik);
            }
            catch (Exception fehler) when (fehler is InvalidDataException or KeyNotFoundException or InvalidCastException or FormatException or NullReferenceException or ArgumentException)
            {
                continue;
            }
            var halte = uebergaenge.Where(u => Stadtbau.Etagen.Arten.Contains(u.Art)).Select(u => (u, h: u.Auf(k))).Where(p => p.h is not null).Select(p => (p.u, h: p.h!)).ToList();
            foreach (var (u, h) in halte)
            {
                var (bx, by) = (Py.F(g["breite"]), Py.F(g["hoehe"]));
                if (h.X < 0 || h.Y < 0 || h.X > bx || h.Y > by || !Wege.KannStehen(h.X, h.Y, innen.Lauf))
                {
                    Rot(k, $"Die Ankunft {Genitiv(u.Art)} im {Name(k)} ist nicht frei.",
                        "Den Pfeil der Ankunft auf eine freie Stelle ziehen.", h.X, h.Y);
                }
            }
            // Start: im Erdgeschoss der Eingang, oben die erste Ankunft
            (double X, double Y) start = k == 0 ? (innen.StartX, innen.StartZ) : halte.Count > 0 ? (halte[0].h.X, halte[0].h.Y) : (innen.StartX, innen.StartZ);
            var ziele = (k == 0 ? [] : SpielKern.Auslage(new PyObjekt { { "gebaeude", g } }).Select(d => (d, ware: true)))
                .Concat(halte.SelectMany(p => Stadtbau.Etagen.Stuecke(g, p.u.Id)).Select(d => (d, ware: false)));
            foreach (var (d, ware) in ziele)
            {
                double x = Py.F(d["x"]), z = Py.F(d["y"]), w = Py.F(d["w"]), t = Py.F(d["h"]);
                var (ex, ez) = WegEnde(innen, start.X, start.Y, x + (w / 2), z + (t / 2), SpielKern.MoebelReichweite + (Math.Min(w, t) / 2));
                var dx = Math.Max(Math.Max(x - ex, 0), ex - x - w);
                var dz = Math.Max(Math.Max(z - ez, 0), ez - z - t);
                if (Math.Sqrt((dx * dx) + (dz * dz)) > SpielKern.MoebelReichweite + 0.3)
                {
                    Rot(k, ware
                        ? $"Die Ware „{Py.Str(d["ware"])}“ ({InnenKatalog.Name(Py.Str(d["typ"]))}) im {Name(k)} ist zu Fuß nicht erreichbar."
                        : $"{Nominativ(Py.Str(d["typ"]), true)} im {Name(k)} ist zu Fuß nicht erreichbar.",
                        "Einen Gang frei machen: davorstehende Stücke verschieben.", x + (w / 2), z + (t / 2));
                }
            }
            foreach (var (u, h) in halte.Skip(k == 0 ? 0 : 1))
            {
                var (ex, ez) = WegEnde(innen, start.X, start.Y, h.X, h.Y, 0);
                if (Math.Sqrt(((ex - h.X) * (ex - h.X)) + ((ez - h.Y) * (ez - h.Y))) > 0.3)
                {
                    Rot(k, $"Die Ankunft {Genitiv(u.Art)} im {Name(k)} ist zu Fuß nicht erreichbar.",
                        "Den Pfeil der Ankunft in einen Gang ziehen.", h.X, h.Y);
                }
            }
        }
        return funde;
    }

    /// <summary>„Die Treppe“/„Der Fahrstuhl“ (bestimmt) oder „Eine Treppe“/„Ein Fahrstuhl“.</summary>
    private static string Nominativ(string art, bool bestimmt) => art == Stadtbau.Etagen.Fahrstuhl
        ? (bestimmt ? "Der Fahrstuhl" : "Ein Fahrstuhl")
        : (bestimmt ? "Die Treppe" : "Eine Treppe");

    private static string Genitiv(string art) => art == Stadtbau.Etagen.Fahrstuhl ? "des Fahrstuhls" : "der Treppe";

    /// <summary>Ladepruefung: rote Funde der Etagen aller Innenraeume einer Fassung als Saetze (leer = gut).</summary>
    public static List<string> EtagenAlle(PyObjekt innenraeume, GrafikBibliothek? grafik)
    {
        var saetze = new List<string>();
        foreach (var (ort, wert) in innenraeume)
        {
            if (wert is PyObjekt raum && Get(raum, "gebaeude") is PyObjekt eg && Stadtbau.Etagen.Hat(eg))
            {
                saetze.AddRange(Etagen(ort, eg, grafik).Where(f => f.Fund.Rot)
                    .Select(f => string.Create(CultureInfo.InvariantCulture, $"Innen {ort}, Etage {f.Etage}: {f.Fund.Text}")));
            }
        }
        return saetze;
    }
}
