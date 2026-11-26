using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Mathe;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="Innenraum"/> mit der Einrichtung (Deko,
/// Sessel, Regalware, Kleidung, Kartons, Uhr). Reiner Umzug aus Innenraum.cs, nichts geaendert.
/// </summary>
public sealed partial class Innenraum
{
    /// <summary>Alle Einrichtungsstuecke des Hauses (fuer <see cref="SesselDrehung"/>), gesetzt vor dem Einrichten.</summary>
    private List<PyObjekt> _alleDeko = [];

    /// <summary>Welle A 1.0.0 (Sammelliste 407), Gegenprobe: Sessel ohne eigene Drehung wie bis A 0.99.0 (immer Blick nach Sueden).</summary>
    internal static readonly AsyncLocal<bool> SesselAltFuerTests = new();

    /// <summary>
    /// Welle A 1.3.1 (Sammelliste 432): ein Bild haengt an der Wand, wenn bis so weit (Welt) hinter seiner Rueckseite eine Wand
    /// ist (oder das Haus endet); sonst steht es auf einer Staffelei.
    /// </summary>
    public const float BildWandAbstand = 0.15f;

    /// <summary>Welle A 1.3.1 (Sammelliste 432), Gegenprobe: jedes Bild als Tafel in Augenhoehe wie bis A 1.3.0 (schwebt ohne Wand).</summary>
    internal static readonly AsyncLocal<bool> BildAltFuerTests = new();

    /// <summary>Flache Stuecke am Boden, die vor einem Sessel nicht stoeren.</summary>
    private static readonly HashSet<string> SesselFlach = ["teppich", "fussmatte"];

    /// <summary>Kleinste gezeichnete Breite eines Sessels (wie der Sessel im Moebelhaus), damit die Figur zwischen den Lehnen sitzt.</summary>
    internal const float SesselMinBreite = 1.1f;

    /// <summary>Tiefe des Streifens vor einem Sessel, der frei sein soll (Platz fuer die Beine).</summary>
    internal const float SesselVorn = 0.6f;

    /// <summary>
    /// Welle A 1.0.0 (Sammelliste 407, Nico: „Sessel im Buero der Chefin verkehrt herum“): ein Sessel ohne eigene Drehung stellt
    /// die Rueckseite an die naechste Wand seines Raums und blickt in den Raum; ist der Streifen vor ihm (<see cref="SesselVorn"/>)
    /// durch ein anderes Stueck oder die Wand belegt, nimmt er die naechste Wand, deren Vorderseite frei ist. Bis A 0.99.0 blickte
    /// jeder Sessel nach Sueden, auch mit dem Gesicht zur Wand. Drehung wie <see cref="AutoDrehung"/>: 0 Blick Sueden, 1 Westen,
    /// 2 Norden, 3 Osten.
    /// </summary>
    private int SesselDrehung(PyObjekt selbst, float x, float z, float w, float t)
    {
        var cx = x + (w / 2);
        var cz = z + (t / 2);
        var flaeche = Liste(Gebaeude["raeume"]).Select(Objekt).FirstOrDefault(r =>
            Py.F(r["x"]) <= cx && cx <= Py.F(r["x"]) + Py.F(r["w"]) && Py.F(r["y"]) <= cz && cz <= Py.F(r["y"]) + Py.F(r["h"]));
        float rx, rz, rw, rt;
        if (flaeche is not null)
        {
            (rx, rz, rw, rt) = ((float)Py.F(flaeche["x"]), (float)Py.F(flaeche["y"]), (float)Py.F(flaeche["w"]), (float)Py.F(flaeche["h"]));
        }
        else
        {
            var flur = Objekt(Gebaeude["flur"]);
            (rx, rz, rw, rt) = (0, (float)Py.F(flur["y"]), Breite, (float)Py.F(flur["h"]));
        }
        // (Drehung, Abstand der Rueckseite zur Wand, Streifen vor dem Sessel)
        var wahl = new List<(int Dreh, float Abstand, (float X, float Z, float W, float T) Vorn)>
        {
            (0, z - rz, (x, z + t, w, SesselVorn)),
            (2, rz + rt - (z + t), (x, z - SesselVorn, w, SesselVorn)),
            (3, x - rx, (x + w, z, SesselVorn, t)),
            (1, rx + rw - (x + w), (x - SesselVorn, z, SesselVorn, t)),
        };
        bool Belegt((float X, float Z, float W, float T) q)
        {
            if (q.X < rx || q.Z < rz || q.X + q.W > rx + rw || q.Z + q.T > rz + rt || InWand(q))
            {
                return true;
            }
            foreach (var d in _alleDeko)
            {
                if (ReferenceEquals(d, selbst) || SesselFlach.Contains(Py.Str(d["typ"])))
                {
                    continue;
                }
                float dx = (float)Py.F(d["x"]), dz = (float)Py.F(d["y"]), dw = (float)Py.F(d["w"]), dt = (float)Py.F(d["h"]);
                if (dx < q.X + q.W && q.X < dx + dw && dz < q.Z + q.T && q.Z < dz + dt)
                {
                    return true;
                }
            }
            return false;
        }
        var geordnet = wahl.OrderBy(v => v.Abstand).ThenBy(v => v.Dreh).ToList();
        return geordnet.FirstOrDefault(v => !Belegt(v.Vorn), geordnet[0]).Dreh;
    }

    private int AutoDrehung(string typ, float x, float z, float w, float t)
    {
        var cx = x + (w / 2);
        var cz = z + (t / 2);
        var flaeche = Liste(Gebaeude["raeume"]).Select(Objekt).FirstOrDefault(r =>
            Py.F(r["x"]) <= cx && cx <= Py.F(r["x"]) + Py.F(r["w"]) && Py.F(r["y"]) <= cz && cz <= Py.F(r["y"]) + Py.F(r["h"]));
        float rx, rz, rw, rt;
        if (flaeche is not null)
        {
            (rx, rz, rw, rt) = ((float)Py.F(flaeche["x"]), (float)Py.F(flaeche["y"]), (float)Py.F(flaeche["w"]), (float)Py.F(flaeche["h"]));
        }
        else
        {
            var flur = Objekt(Gebaeude["flur"]);
            (rx, rz, rw, rt) = (0, (float)Py.F(flur["y"]), Breite, (float)Py.F(flur["h"]));
        }
        if (Liegemoebel.Contains(typ) && !BettQuerAltFuerTests.Value)
        {
            // Sammelliste 380: das Kopfteil steht an einer kurzen Seite (das Bett ist laenger als breit), an der naeheren Wand
            return w >= t
                ? (x - rx <= rx + rw - (x + w) ? 3 : 1)
                : (z - rz <= rz + rt - (z + t) ? 0 : 2);
        }
        if (w >= t)
        {
            // Rueckseite an die naehere Wand oben (Norden) oder unten
            return z - rz <= rz + rt - (z + t) ? 0 : 2;
        }
        // hochkant: Rueckseite links (Vorderseite nach Osten = 3) oder rechts (1)
        return x - rx <= rx + rw - (x + w) ? 3 : 1;
    }

    private void Deko(PyObjekt d)
    {
        var typ = Py.Str(d["typ"]);
        float x = (float)Py.F(d["x"]), z = (float)Py.F(d["y"]), w = (float)Py.F(d["w"]), t = (float)Py.F(d["h"]);
        int dreh;
        if (Py.Hat(d, "dreh") || Py.Hat(d, "blick"))
        {
            dreh = (int)(((Py.Int(Get(d, "dreh", 0L)) % 4) + 4) % 4);
            if (Py.Gleich(Get(d, "blick", "s"), "n"))
            {
                // Blick nach Norden: der Stuhl steht oben (Python chair_y = y - 0.3)
                dreh = (dreh + 2) % 4;
                // das ungedrehte Stueck hat dieselben Masse
            }
        }
        else
        {
            dreh = AnDerWand.Contains(typ) ? AutoDrehung(typ, x, z, w, t) : typ == "sessel" && !SesselAltFuerTests.Value ? SesselDrehung(d, x, z, w, t) : 0;
            if (typ == "rack" && w < t && !WerkstattAltFuerTests.Value)
            {
                // Welle A 0.99.0 (E1): Serverschraenke zeigen mit der Front zum Gang (lange Seite), nicht seitlich in die
                // schmale Luecke zum Nachbarschrank; Rechteck und Laufraster bleiben gleich
                dreh = AutoDrehung("bett", x, z, w, t);
            }
        }
        var r = new Rahmen(x, z, w, t, dreh);
        var ohneStuhl = false;
        if (typ == "schreibtisch" && !WandAltFuerTests.Value && InWand(r.Rechteck((r.B0 / 2) - 0.38f, r.T0, 0.76f, 0.76f)))
        {
            // Sammelliste 347: zeigt die Vorderseite eines Schreibtischs zur Wand, stuende der Stuhl in der Wand; der Tisch wird
            // dann umgekehrt gezeichnet (Stuhl auf der freien Seite), ist auch dort kein Platz, ohne Stuhl. Rechteck und
            // Laufraster bleiben gleich.
            var gedreht = new Rahmen(x, z, w, t, (dreh + 2) % 4);
            ohneStuhl = InWand(gedreht.Rechteck((gedreht.B0 / 2) - 0.38f, gedreht.T0, 0.76f, 0.76f));
            r = ohneStuhl ? r : gedreht;
        }
        float b = r.B0, tt = r.T0;
        const float M = Meter;
        switch (typ)
        {
            case "schreibtisch":
                Modell(r, "desk", 0, 0, b, tt, 0.75f * M, 0, Farben.Holz);
                Modell(r, "computerScreen", (b / 2) - 0.55f, 0.08f, 1.1f, 0.25f, 0.45f * M, 0.75f * M, Farben.Geraet);
                // Stuhl vor dem Tisch, mit dem Gesicht zum Tisch (Python: 0,3 vor der Kante); Phase 9 W1 (Weisung P9-3):
                // direkt an der Tischkante statt 0,05 darunter, damit die Knie der sitzenden Figur die Schublade nicht beruehren
                if (!ohneStuhl)
                {
                    Modell(r, "chairDesk", (b / 2) - 0.38f, tt, 0.76f, 0.76f, 1.0f * M, 0, Farben.Polster, 2);
                    StuhlMerken(r.Rechteck((b / 2) - 0.38f, tt, 0.76f, 0.76f), (x + (w / 2), z + (t / 2)));
                    PcStuhlMerken(_stuehle[^1], r.Rechteck((b / 2) - 0.38f, tt, 0.76f, 0.76f));
                }
                break;
            case "leitstand":
                Modell(r, "desk", 0, 0, b, tt, 0.75f * M, 0, Farben.Holz);
                for (var i = 0; i < 3; i++)
                {
                    Modell(r, "computerScreen", (b * (i + 0.5f) / 3) - 0.5f, 0.08f, 1.0f, 0.25f, 0.45f * M, 0.75f * M, Farben.Geraet);
                }
                break;
            case "tisch" when (!PcTischAltFuerTests.Value && PcDarunter(x, z, w, t)) || (!KartonsAltFuerTests.Value && Darunter("kartons", x, z, w, t)):
                // Welle A 1.2.0 (Sammelliste 411): auch ueber Kartons ein Tisch ohne Querstreben (die Streben steckten im Karton)
                // Welle A 1.0.0 (Sammelliste 408, Nico: „PCs unter dem Tisch stecken im Tisch“): steht ein PC unter dem Tisch,
                // ein Tisch ohne Querstreben (Platte, Zarge, vier Beine an den Ecken); das Modell „table“ hat Streben in der Mitte
                TischOhneStreben(r, b, tt, 0.75f * M);
                break;
            case "tisch":
            case "werkbank":
                Modell(r, "table", 0, 0, b, tt, (typ == "werkbank" ? 0.9f : 0.75f) * M, 0, Farben.Holz);
                if (typ == "werkbank" && WerkstattAltFuerTests.Value)
                {
                    Kasten(r, 0.3f, 0.2f, 0.8f, 0.6f, 0.9f * M, (0.9f + 0.4f) * M, Farben.GeraetHell);
                }
                else if (typ == "werkbank")
                {
                    // Welle A 0.99.0 (Sammelliste 356): PC-Tower, ESD-Matte, Mainboard, RAM, CPU, Werkzeug
                    WerkbankBauen(KastenIn(r), b, tt, 0.9f * M);
                }
                break;
            case "besprechungstisch":
            case "esstisch":
            {
                Modell(r, "table", 0, 0, b, tt, 0.75f * M, 0, Farben.Holz);
                var plaetze = typ == "esstisch" ? Math.Max(1, (int)PythonRunden.Runde(b / 1.2)) : 3;
                float Mitte(int i) => typ == "esstisch" ? b * (i + 0.5f) / plaetze : b * (i + 1) / 4f;
                // Sammelliste 347: die Stuehle stehen ausserhalb der Tischflaeche; steht der Tisch an einer Wand, bleibt die
                // Stuhlreihe auf dieser Seite weg (sonst ragen die Stuehle durch die Wand). Ganze Reihe, nie einzelne Stuehle.
                bool ReiheFrei(float lz) => WandAltFuerTests.Value
                    || Enumerable.Range(0, plaetze).All(i => !InWand(r.Rechteck(Mitte(i) - 0.32f, lz, 0.64f, 0.64f)));
                var (vorn, hinten) = (ReiheFrei(-0.7f), ReiheFrei(tt + 0.06f));
                for (var i = 0; i < plaetze; i++)
                {
                    var cx = Mitte(i);
                    if (vorn)
                    {
                        Modell(r, "chair", cx - 0.32f, -0.7f, 0.64f, 0.64f, 0.9f * M, 0, Farben.Holz, 0);
                    }
                    if (hinten)
                    {
                        Modell(r, "chair", cx - 0.32f, tt + 0.06f, 0.64f, 0.64f, 0.9f * M, 0, Farben.Holz, 2);
                    }
                }
                break;
            }
            case "treppe" when Py.Gleich(Get(d, Stadtbau.Etagen.FeldRichtung), Stadtbau.Etagen.NachUnten) && !TreppeAltFuerTests.Value:
                TreppeNachUnten(r, b, tt);
                break;
            case "treppe":
                TreppeNachOben(r, b, tt);
                break;
            case "fahrstuhl":
            {
                // Phase 13 Welle 5: Kabine mit Rueckwand, Seitenwaenden, zwei Tueren vorn und Anzeige darueber
                const float wand = 0.1f;
                Kasten(r, 0, 0, b, wand, 0, WandHoehe, Farben.Metall);
                Kasten(r, 0, 0, wand, tt, 0, WandHoehe, Farben.Metall);
                Kasten(r, b - wand, 0, wand, tt, 0, WandHoehe, Farben.Metall);
                Kasten(r, wand, tt - wand, (b / 2) - wand - 0.01f, wand, 0, WandHoehe - 0.35f, Farben.Hell);
                Kasten(r, (b / 2) + 0.01f, tt - wand, (b / 2) - wand - 0.01f, wand, 0, WandHoehe - 0.35f, Farben.Hell);
                Kasten(r, wand, tt - wand, b - (2 * wand), wand, WandHoehe - 0.35f, WandHoehe, Farben.MetallDunkel);
                Kasten(r, (b / 2) - 0.2f, tt - 0.02f, 0.4f, 0.04f, WandHoehe - 0.3f, WandHoehe - 0.12f, Farben.Bildschirm);
                break;
            }
            case "tresen":
                Modell(r, "kitchenBar", 0, 0, b, tt, 1.05f * M, 0, Farben.Holz);
                break;
            case "kassentisch":
                // Kaufhaus (Weisung P8-8c): freistehende kleine Kasse (Tisch mit Kasse und Bildschirm), keine Tresenwand
                Modell(r, "table", 0, 0, b, tt, 0.95f * M, 0, Farben.Hell);
                Kasten(r, (b / 2) - 0.25f, (tt / 2) - 0.2f, 0.5f, 0.4f, 0.95f * M, 1.1f * M, Farben.Geraet);
                Kasten(r, (b / 2) - 0.2f, (tt / 2) - 0.25f, 0.4f, 0.05f, 1.1f * M, 1.35f * M, Farben.Geraet);
                break;
            case "kasse":
                // Weisung P8-56: Kasse obenauf auf dem Tresen (Hoehe wie "tresen"), Bildschirm zur hinteren Kante
                Kasten(r, 0.05f, 0.05f, b - 0.1f, tt - 0.1f, 1.05f * M, 1.2f * M, Farben.Geraet);
                Kasten(r, 0.1f, 0.02f, b - 0.2f, 0.05f, 1.2f * M, 1.45f * M, Farben.Geraet);
                break;
            case "kleiderstaender":
                Kleiderstaender(r, b, tt, Get(d, "ware") as string);
                break;
            case "kleiderregal":
                Modell(r, "bookcaseOpen", 0, 0, b, tt, 1.8f * M, 0, Farben.Hell);
                Kleiderfaecher(r, b, tt, Get(d, "ware") as string);
                break;
            case "spiegel":
                Kasten(r, 0, 0, b, tt, 0.1f * M, 1.9f * M, Farben.HolzDunkel);
                Kasten(r, 0.06f, tt, b - 0.12f, 0.01f, 0.15f * M, 1.85f * M, Netz.Farbe("#CFE8F5"));
                break;
            case "umkleide":
                Umkleide(r, b, tt);
                break;
            case "puppe":
                Puppe(x, z, w, t, dreh, PuppenKleidung(d));
                break;
            case "aktenregal":
                Modell(r, "bookcaseClosedWide", 0, 0, b, tt, 1.8f * M, 0, Farben.HolzDunkel);
                break;
            case "regal":
                Modell(r, "bookcaseOpen", 0, 0, b, tt, 1.8f * M, 0, Farben.HolzDunkel);
                // Nachtrag 1 zu P7-13 (Wunsch 5): volle Regale im Laden, die Ware je Art sichtbar
                Regalware(r, b, tt, Get(d, "ware") as string);
                break;
            case "auslage":
                // Praesentationstisch im Laden (Nachtrag 1 zu P7-13, Wunsch 5): Ware obenauf
                Modell(r, "table", 0, 0, b, tt, 0.75f * M, 0, Farben.Hell);
                Auslage(r, b, tt, Get(d, "ware") as string);
                break;
            case "hochregal":
                Modell(r, "bookcaseOpen", 0, 0, b, tt, 2.4f * M, 0, Farben.Metall);
                break;
            case "kleiderschrank" when Py.Gleich(Get(d, "moebel"), Begehbar.Id):
                // Phase 12 Welle 2 (Nico 2b, Weisung P12-8): eigenes Bild fuer den begehbaren Kleiderschrank
                BegehbarerSchrank(r, b, tt);
                break;
            case "kleiderschrank":
                Modell(r, "bookcaseClosedDoors", 0, 0, b, tt, 2.0f * M, 0, Farben.Holz);
                break;
            case "pflanze":
                Modell(r, "pottedPlant", 0, 0, b, tt, 1.0f * M, 0, Farben.Pflanze);
                break;
            case "sessel" when !SesselAltFuerTests.Value && b < SesselMinBreite:
                // Welle A 1.0.0 (Sammelliste 407): ein schmaler Sessel (0,95) blickt jetzt in den Raum und wird damit zum Sitzplatz;
                // zwischen seinen Lehnen braucht die sitzende Figur die Breite des Sessels aus dem Moebelhaus. Er wird darum
                // mittig auf diese Breite gezeichnet (Grundriss und Laufraster bleiben gleich)
                Modell(r, "loungeChair", (b - SesselMinBreite) / 2, 0, SesselMinBreite, tt, 0.8f * M, 0, Farben.Polster);
                break;
            case "sessel":
                Modell(r, "loungeChair", 0, 0, b, tt, 0.8f * M, 0, Farben.Polster);
                break;
            case "sofa":
                Modell(r, "loungeSofa", 0, 0, b, tt, 0.8f * M, 0, Farben.Polster);
                break;
            case "sitzsack":
                Modell(r, "loungeSofaOttoman", 0, 0, b, tt, 0.45f * M, 0, Netz.Farbe("#FB923C"));
                break;
            case "couchtisch":
                Modell(r, "tableCoffee", 0, 0, b, tt, 0.4f * M, 0, Farben.Holz);
                break;
            case "bank":
                Modell(r, "bench", 0, 0, b, tt, 0.45f * M, 0, Farben.Holz);
                break;
            case "kaffeemaschine":
                Modell(r, "kitchenCabinet", 0, 0, b, tt, 0.9f * M, 0, Farben.Hell);
                Modell(r, "kitchenCoffeeMachine", b * 0.15f, tt * 0.1f, b * 0.7f, tt * 0.7f, 0.35f * M, 0.9f * M, Farben.MetallDunkel);
                break;
            case "kuechenzeile":
                Modell(r, "kitchenCabinet", 0, 0, b, tt, 0.9f * M, 0, Farben.Hell);
                break;
            case "kuehlschrank":
                Modell(r, "kitchenFridge", 0, 0, b, tt, 1.8f * M, 0, Farben.Weiss);
                break;
            case "waschmaschine":
                Modell(r, "washer", 0, 0, b, tt, 0.85f * M, 0, Farben.Hell);
                break;
            case "wc":
                Modell(r, "toilet", 0, 0, b, tt, 0.8f * M, 0, Farben.Weiss);
                break;
            case "waschbecken":
                Modell(r, "bathroomSink", 0, 0, b, tt, 0.9f * M, 0, Farben.Weiss);
                break;
            case "dusche":
                Modell(r, "shower", 0, 0, b, tt, 2.0f * M, 0, Farben.Weiss);
                break;
            case "badewanne":
                Modell(r, "bathtub", 0, 0, b, tt, 0.6f * M, 0, Farben.Weiss);
                break;
            case "teppich":
                Modell(r, "rugRectangle", 0, 0, b, tt, 0.02f, 0.04f, Netz.Farbe("#F472B6"));
                break;
            case "kartons":
                Kartons(r, b, tt, !KartonsAltFuerTests.Value && TischDarueber(x, z, w, t));
                break;
            case "bett":
                Modell(r, b >= 2.4f ? "bedDouble" : "bedSingle", 0, 0, b, tt, 0.55f * M, 0, Farben.Holz);
                break;
            case "matratze":
                Modell(r, "bedSingle", 0, 0, b, tt, 0.3f * M, 0, Farben.Weiss);
                break;
            case "fernseher":
                Modell(r, "cabinetTelevision", 0, 0, b, tt, 0.5f * M, 0, Farben.HolzDunkel);
                Modell(r, "televisionModern", b * 0.1f, 0.05f, b * 0.8f, Math.Min(0.25f, tt * 0.4f), 0.6f * M, 0.5f * M, Farben.Geraet);
                break;
            case "kommode":
                Modell(r, "sideTableDrawers", 0, 0, b, tt, 0.8f * M, 0, Farben.HolzDunkel);
                break;
            case "stehlampe":
                Modell(r, "lampRoundFloor", 0, 0, b, tt, 1.6f * M, 0, Farben.Gelb);
                break;
            case "liegestuhl":
                Modell(r, "loungeChairRelax", 0, 0, b, tt, 0.8f * M, 0, Farben.Hell);
                break;
            case "stuhlreihe":
            {
                // Wartestuehle nebeneinander (KayKit, P7-15 Nachtrag 1), so viele, wie in die Breite passen
                var n = Math.Max(1, (int)(b / 0.8f));
                for (var i = 0; i < n; i++)
                {
                    Modell(r, "kkChairA", (b * i / n) + (b / n / 2) - 0.32f, 0, 0.64f, tt, 0.95f * M, 0, Farben.Polster);
                }
                break;
            }
            case "wartesessel":
                Modell(r, "kkArmchair", 0, 0, b, tt, 0.85f * M, 0, Farben.Polster);
                break;
            case "bahnhofsuhr":
                Bahnhofsuhr(r, b, tt);
                break;
            case "gemaelde":
                Modell(r, "kkPictureframeLargeB", 0, 0, b, Math.Min(tt, 0.12f), 0.6f * M, 1.0f * M, Netz.Farbe("#F472B6"));
                break;
            case "platznummer":
                Platznummer(r, b, tt, (int)Py.Int(Get(d, "nummer", 1L)), (float)Py.F(Get(d, "unten", 0.75)) * M);
                break;
            case var _ when Katalogstueck.IstTyp(typ):
                KatalogStueck(typ, x, z, w, t, dreh);
                break;
            default:
                Eigen(typ, r, b, tt);
                break;
        }
    }

    /// <summary>
    /// Phase 13 Welle 4a (Weisung P12-28): ein Stueck aus dem Katalog des Stadtbau-Werkzeugs ("cc0:paket/name") im Rechteck
    /// der Einrichtung, mit seinem Verhaeltnis (<see cref="Katalogstueck.Lage"/>); ein Effekt schwebt als leuchtender Stern.
    /// Ohne mitgeliefertes Modell zeichnet der Raum nichts dafuer (die Ladepruefung meldet das vorher rot).
    /// </summary>
    private void KatalogStueck(string typ, float x, float z, float w, float t, int dreh)
    {
        if (Grafik?.Modell(typ) is not { } gm)
        {
            return;
        }
        _stuecke.Add(new ModellStueck(typ, Katalogstueck.Lage(gm, x + (w / 2), z + (t / 2), w, t, dreh, 0f, Katalogstueck.IstEffekt(typ))));
    }

    /// <summary>Aussehen einer Teile-Art im Laden: Breite und Hoehe eines Stuecks, zwei Farben im Wechsel.</summary>
    private static (float Breite, float Hoehe, Vector3 Farbe, Vector3 Zweite) Warenform(string? ware) => ware switch
    {
        "teile:mainboard" => (0.42f, 0.1f, Netz.Farbe("#166534"), Netz.Farbe("#1E3A8A")),
        "teile:cpu" => (0.2f, 0.22f, Netz.Farbe("#DC2626"), Netz.Farbe("#2563EB")),
        "teile:ram" => (0.28f, 0.12f, Netz.Farbe("#15803D"), Netz.Farbe("#111827")),
        "teile:ssd" => (0.16f, 0.1f, Netz.Farbe("#1F2937"), Netz.Farbe("#7C3AED")),
        "teile:gpu" => (0.5f, 0.2f, Netz.Farbe("#111827"), Netz.Farbe("#4B5563")),
        "teile:netzteil" => (0.28f, 0.24f, Netz.Farbe("#374151"), Netz.Farbe("#1F2937")),
        "teile:gehaeuse" => (0.42f, 0.7f, Netz.Farbe("#1F2937"), Netz.Farbe("#E5E7EB")),
        "teile:notebook" => (0.5f, 0.06f, Netz.Farbe("#9CA3AF"), Netz.Farbe("#1F2937")),
        "teile:usv_akku" => (0.24f, 0.26f, Netz.Farbe("#111827"), Netz.Farbe("#B91C1C")),
        "teile:monitor" => (0.55f, 0.42f, Netz.Farbe("#1F2937"), Netz.Farbe("#374151")),
        "teile:patchkabel" => (0.2f, 0.16f, Netz.Farbe("#FACC15"), Netz.Farbe("#2563EB")),
        "teile:luefter" => (0.2f, 0.2f, Netz.Farbe("#1F2937"), Netz.Farbe("#0EA5E9")),
        "teile:toner" => (0.38f, 0.18f, Netz.Farbe("#111827"), Netz.Farbe("#DB2777")),
        "teile:sfp" => (0.12f, 0.06f, Netz.Farbe("#9CA3AF"), Netz.Farbe("#2563EB")),
        "teile:drucker" => (0.55f, 0.38f, Netz.Farbe("#E5E7EB"), Netz.Farbe("#6B7280")),
        "regal" => (0.07f, 0.42f, Netz.Farbe("#B91C1C"), Netz.Farbe("#1D4ED8")),
        _ => (0.3f, 0.26f, Farben.Karton, Mischen(Farben.Karton, Vector3.Zero, 0.2f)),
    };

    /// <summary>Ware in den drei Boeden eines offenen Regals (ohne Ware bleibt es leer, z. B. im Buero).</summary>
    private void Regalware(Rahmen r, float b, float t, string? ware)
    {
        if (ware is null)
        {
            return;
        }
        var (breite, hoehe, farbe, zweite) = Warenform(ware);
        var tief = Math.Min(0.42f, t * 0.7f);
        var boden = 1.8f * Meter;
        var anzahl = Math.Max(1, (int)((b - 0.12f) / (breite + 0.05f)));
        var abstand = (b - 0.12f - (anzahl * breite)) / Math.Max(1, anzahl);
        var nr = 0;
        foreach (var anteil in (ReadOnlySpan<float>)[0.03f, 0.36f, 0.69f])
        {
            var unten = boden * anteil;
            var oben = Math.Min(unten + hoehe, unten + (boden * 0.3f));
            for (var i = 0; i < anzahl; i++)
            {
                var lx = 0.06f + (abstand / 2) + (i * (breite + abstand));
                Kasten(r, lx, t - tief - 0.04f, breite, tief, unten, oben, nr++ % 3 == 1 ? zweite : farbe);
            }
        }
    }

    /// <summary>Ware auf einem Auslagetisch: eine Reihe (Notebooks aufgeklappt, Monitore mit Bild).</summary>
    private void Auslage(Rahmen r, float b, float t, string? ware)
    {
        var (breite, hoehe, farbe, zweite) = Warenform(ware);
        var tisch = 0.75f * Meter;
        if (ware is not null && ware.StartsWith("kleidung:", StringComparison.Ordinal))
        {
            // Kaufhaus: Kappen und Muetzen in den Farben des Sortiments
            var farben = SpielKern.Regeln.KleidungFarben(Kleiderdaten, ware);
            var n = Math.Max(1, (int)((b - 0.2f) / 0.32f));
            for (var i = 0; i < n; i++)
            {
                var lx = 0.1f + (i * (b - 0.2f) / n);
                Kasten(r, lx, (t / 2) - 0.12f, 0.24f, 0.24f, tisch, tisch + 0.12f, farben[i % farben.Count]);
                Kasten(r, lx + 0.04f, (t / 2) + 0.12f, 0.16f, 0.1f, tisch, tisch + 0.03f, farben[i % farben.Count] * 0.75f);
            }
            return;
        }
        var anzahl = Math.Max(1, (int)((b - 0.2f) / (breite + 0.15f)));
        var abstand = (b - 0.2f - (anzahl * breite)) / Math.Max(1, anzahl);
        for (var i = 0; i < anzahl; i++)
        {
            var lx = 0.1f + (abstand / 2) + (i * (breite + abstand));
            if (ware == "teile:notebook")
            {
                // Unterteil flach, Deckel hochkant mit Bildschirm
                Kasten(r, lx, (t / 2) - 0.05f, breite, 0.32f, tisch, tisch + 0.04f, i % 2 == 0 ? farbe : zweite);
                Kasten(r, lx, (t / 2) - 0.09f, breite, 0.04f, tisch, tisch + 0.32f, i % 2 == 0 ? farbe : zweite);
                Kasten(r, lx + 0.03f, (t / 2) - 0.05f, breite - 0.06f, 0.005f, tisch + 0.05f, tisch + 0.29f, Farben.Bildschirm);
            }
            else if (ware == "teile:monitor")
            {
                Kasten(r, lx + (breite * 0.4f), (t / 2) - 0.05f, breite * 0.2f, 0.12f, tisch, tisch + 0.12f, zweite);
                Kasten(r, lx, (t / 2) - 0.05f, breite, 0.05f, tisch + 0.12f, tisch + 0.12f + hoehe, farbe);
                Kasten(r, lx + 0.03f, t / 2, breite - 0.06f, 0.005f, tisch + 0.15f, tisch + 0.09f + hoehe, Farben.Bildschirm);
            }
            else
            {
                Kasten(r, lx, (t / 2) - 0.15f, breite, 0.3f, tisch, tisch + hoehe, i % 3 == 1 ? zweite : farbe);
            }
        }
    }

    /// <summary>
    /// Begehbarer Kleiderschrank, seit Phase 12 Welle 2 Kleidung (Weisung P12-206, Nico 2b zu P12-118) Entwurf B „Schrankwand mit
    /// Spiegel“: links und rechts je ein Schrank mit geschlossenen Tueren (Kenney „bookcaseClosedDoors“), auf der rechten Tuer ein
    /// Spiegel; in der Mitte offen mit Rueckwand, Deckboden, Kleiderstange mit haengender Kleidung und einer Kommode
    /// („sideTableDrawers“) mit gefalteten Stapeln. Gleiche Grundflaeche wie vorher (w × h aus geschaefte.json, 4,4 × 1,2), damit
    /// aufgestellte Schraenke alter Spielstaende passen; Rueckseite an der Wand.
    /// </summary>
    private void BegehbarerSchrank(Rahmen r, float b, float t)
    {
        const float M = Meter;
        var seite = MathF.Min(SchrankSeite, b * 0.3f);
        Modell(r, "bookcaseClosedDoors", 0, 0, seite, t, 2.1f * M, 0, Farben.HolzDunkel);
        Modell(r, "bookcaseClosedDoors", b - seite, 0, seite, t, 2.1f * M, 0, Farben.HolzDunkel);
        // Spiegel vorn auf der rechten Tuer
        Kasten(r, b - seite + 0.12f, t - 0.02f, seite - 0.24f, 0.03f, 0.25f * M, 1.95f * M, SpiegelFarbe);
        // Mitte: Rueckwand und Deckboden
        Kasten(r, seite, 0, b - (2 * seite), 0.06f, 0, 2.1f * M, Farben.Holz);
        Kasten(r, seite, 0, b - (2 * seite), t, 2.05f * M, 2.1f * M, Farben.HolzDunkel);
        Modell(r, "sideTableDrawers", seite + 0.1f, 0.15f, b - (2 * seite) - 0.2f, t - 0.25f, 0.6f * M, 0, Farben.Holz);
        // Kleiderstange mit haengender Kleidung
        var links = seite + 0.08f;
        var rechts = b - seite - 0.08f;
        var stange = 1.8f * M;
        Kasten(r, links, (t / 2) - 0.02f, rechts - links, 0.04f, stange - 0.04f, stange, Farben.Metall);
        var anzahl = Math.Max(1, (int)((rechts - links - 0.16f) / 0.13f));
        for (var i = 0; i < anzahl; i++)
        {
            var lx = links + 0.08f + (i * (rechts - links - 0.16f) / anzahl);
            Kasten(r, lx, (t / 2) - 0.2f, 0.07f, 0.4f, stange - (0.7f * M), stange - 0.05f, Begehbar.Farben[(i + 3) % Begehbar.Farben.Count]);
            Kasten(r, lx + 0.025f, (t / 2) - 0.01f, 0.02f, 0.02f, stange - 0.05f, stange + 0.03f, Farben.Metall);
        }
        // gefaltete Stapel auf der Kommode
        for (var i = 0; i < 4; i++)
        {
            Kasten(r, seite + 0.15f + (i * 0.5f), (t / 2) - 0.2f, 0.36f, 0.4f, 0.6f * M, (0.6f * M) + 0.12f + (0.06f * (i % 2)), Begehbar.Farben[(i + 1) % Begehbar.Farben.Count]);
        }
    }

    /// <summary>Phase 12 Welle 2: Breite der beiden geschlossenen Schraenke des begehbaren Schranks (Entwurf B).</summary>
    public const float SchrankSeite = 1.15f;

    /// <summary>Phase 12 Welle 2: Farbe des Spiegels auf der rechten Schranktuer (hell, kuehl).</summary>
    public static readonly Vector3 SpiegelFarbe = Netz.Farbe("#CBD5E1");

    /// <summary>Kaufhaus (Phase 8 W5): Kleiderstange auf zwei Fuessen, Oberteile (Hosen) haengend in den Farben des Sortiments.</summary>
    private void Kleiderstaender(Rahmen r, float b, float t, string? ware)
    {
        var stange = 1.5f * Meter;
        Kasten(r, 0.02f, (t / 2) - 0.03f, 0.06f, 0.06f, 0, stange, Farben.Metall);
        Kasten(r, b - 0.08f, (t / 2) - 0.03f, 0.06f, 0.06f, 0, stange, Farben.Metall);
        Kasten(r, 0, (t / 2) - 0.2f, 0.1f, 0.4f, 0, 0.04f, Farben.Metall);
        Kasten(r, b - 0.1f, (t / 2) - 0.2f, 0.1f, 0.4f, 0, 0.04f, Farben.Metall);
        Kasten(r, 0, (t / 2) - 0.02f, b, 0.04f, stange - 0.04f, stange, Farben.Metall);
        var farben = SpielKern.Regeln.KleidungFarben(Kleiderdaten, ware);
        var anzahl = Math.Max(1, (int)((b - 0.3f) / 0.12f));
        for (var i = 0; i < anzahl; i++)
        {
            var lx = 0.15f + (i * (b - 0.3f) / anzahl);
            var lang = ware == "kleidung:hose" ? 0.95f : 0.7f;
            Kasten(r, lx, (t / 2) - 0.22f, 0.05f, 0.44f, stange - (lang * Meter), stange - 0.06f, farben[i % farben.Count]);
        }
    }

    /// <summary>Kaufhaus (Phase 8 W5): gefaltete Stapel (Hosen) oder Schuhkartons und Schuhe in drei Boeden.</summary>
    private void Kleiderfaecher(Rahmen r, float b, float t, string? ware)
    {
        var farben = SpielKern.Regeln.KleidungFarben(Kleiderdaten, ware);
        var schuhe = ware == "kleidung:schuhe";
        var (breite, hoehe) = schuhe ? (0.28f, 0.12f) : (0.36f, 0.16f);
        var tief = Math.Min(0.4f, t * 0.7f);
        var boden = 1.8f * Meter;
        var anzahl = Math.Max(1, (int)((b - 0.12f) / (breite + 0.06f)));
        var abstand = (b - 0.12f - (anzahl * breite)) / anzahl;
        var nr = 0;
        foreach (var anteil in (ReadOnlySpan<float>)[0.03f, 0.36f, 0.69f])
        {
            var unten = boden * anteil;
            for (var i = 0; i < anzahl; i++)
            {
                var lx = 0.06f + (abstand / 2) + (i * (breite + abstand));
                var f = farben[nr++ % farben.Count];
                Kasten(r, lx, t - tief - 0.04f, breite, tief, unten, unten + hoehe, f);
                if (!schuhe)
                {
                    Kasten(r, lx, t - tief - 0.04f, breite, tief, unten + hoehe + 0.01f, unten + (hoehe * 2), f * 0.85f);
                }
            }
        }
    }

    /// <summary>Kaufhaus (Phase 8 W5): Umkleidekabine mit drei Waenden und Vorhang (halb offen).</summary>
    private void Umkleide(Rahmen r, float b, float t)
    {
        var hoch = 2.0f * Meter;
        var wand = Netz.Farbe("#E8E2F4");
        Kasten(r, 0, 0, b, 0.06f, 0, hoch, wand);
        Kasten(r, 0, 0, 0.06f, t, 0, hoch, wand);
        Kasten(r, b - 0.06f, 0, 0.06f, t, 0, hoch, wand);
        Kasten(r, 0, t - 0.04f, b, 0.04f, hoch - 0.05f, hoch, Farben.Metall);
        Kasten(r, 0.06f, t - 0.06f, b * 0.55f, 0.04f, 0.15f * Meter, hoch - 0.05f, Netz.Farbe("#DB2777"));
        Kasten(r, 0.12f, 0.08f, 0.5f, 0.3f, 0, 0.45f * Meter, Farben.Holz);
    }

    private List<PyObjekt> _festPuppen = [];

    /// <summary>
    /// Kleidung einer Puppe; in der Festzeit tragen die ersten Puppen im Flur (festzeiten.json innen.puppen) Kopf und
    /// Oberteil der Festkleidung, der Rest bleibt.
    /// </summary>
    private List<string> PuppenKleidung(PyObjekt d)
    {
        var kleidung = ListeOderLeer(d, "kleidung").Select(Py.Str).ToList();
        if (Kleiderdaten is null || !_festPuppen.Any(p => ReferenceEquals(p, d)))
        {
            return kleidung;
        }
        var alle = SpielKern.Regeln.KleidungAlle(Kleiderdaten);
        var fest = alle.Where(k => k.Festzeit == Fest && k.Slot is ("oberteil" or "kopf")).ToList();
        // Kleidung.Finden = erstes Stueck mit dieser Kennung
        return [.. fest.Select(k => k.Id), .. kleidung.Where(id => alle.FindIndex(k => k.Id == id) is var i and >= 0 && !fest.Any(f => f.Slot == alle[i].Slot))];
    }

    /// <summary>Kaufhaus (Phase 8 W5): Schaufensterpuppe (die Figur ohne Gesichtsfarbe) mit Kleidung aus dem Sortiment.</summary>
    private void Puppe(float x, float z, float w, float t, int dreh, List<string> kleidung)
    {
        var stuecke = kleidung.Select(id => SpielKern.Regeln.FigurStueck(Kleiderdaten, id)).OfType<Figur.Stueck>().ToList();
        var aussehen = new PyObjekt { { "haut", "hell" }, { "frisur", "glatze" }, { "haarfarbe", "grau" }, { "oberteil", "cyan" } };
        var netz = Figur.Netz(Grafik, aussehen, FigurHoehe * 0.98f, stuecke);
        var lage = Matrix4x4.CreateRotationY(-MathF.PI / 2 * dreh) * Matrix4x4.CreateTranslation(x + (w / 2), 0.1f, z + (t / 2));
        _puppen.Add((netz, lage));
        _teile.Add(Quader(x + (w / 2) - 0.25f, 0, z + (t / 2) - 0.25f, x + (w / 2) + 0.25f, 0.1f, z + (t / 2) + 0.25f, Farben.Metall));
    }

    /// <summary>Welle A 1.2.0 (Sammelliste 411), Gegenprobe: Kartons unter einem Tisch wie bis A 1.1.0 (zwei uebereinander).</summary>
    internal static readonly AsyncLocal<bool> KartonsAltFuerTests = new();

    /// <summary>Tische, unter denen etwas stehen kann (Platte oben, darunter frei).</summary>
    private static readonly HashSet<string> TischTypen = ["tisch", "werkbank", "schreibtisch", "besprechungstisch", "esstisch"];

    /// <summary>Welle A 1.2.0 (Sammelliste 411): steht ein Tisch ueber dem Rechteck (x, z, w, t)?</summary>
    private bool TischDarueber(float x, float z, float w, float t) => _alleDeko.Any(d => TischTypen.Contains(Py.Str(Get(d, "typ", "")))
        && Py.F(d["x"]) < x + w && x < Py.F(d["x"]) + Py.F(d["w"]) && Py.F(d["y"]) < z + t && z < Py.F(d["y"]) + Py.F(d["h"]));

    /// <summary>
    /// Gestapelte Umzugskartons: so viele, wie in das Rechteck passen (hoechstens zwei uebereinander). Welle A 1.2.0 (Sammelliste
    /// 411, Nico: „der Karton glitcht durch den Tisch“): unter einem Tisch nur eine Lage, sie endet unter der Tischplatte.
    /// </summary>
    private void Kartons(Rahmen r, float b, float t, bool unterTisch = false)
    {
        var nx = Math.Max(1, (int)(b / 0.6f));
        var nz = Math.Max(1, (int)(t / 0.6f));
        float kb = b / nx, kt = t / nz;
        var k = 0;
        for (var i = 0; i < nx; i++)
        {
            for (var j = 0; j < nz; j++)
            {
                Modell(r, "cardboardBoxClosed", (i * kb) + 0.03f, (j * kt) + 0.03f, kb - 0.06f, kt - 0.06f, 0.5f * Meter, 0, Farben.Karton);
                if (k++ % 2 == 0 && !unterTisch)
                {
                    Modell(r, "cardboardBoxClosed", (i * kb) + 0.08f, (j * kt) + 0.08f, kb - 0.16f, kt - 0.16f, 0.4f * Meter, 0.5f * Meter, Farben.Karton);
                }
            }
        }
    }

    /// <summary>Ziffern als Siebensegment (a oben, b rechts oben, c rechts unten, d unten, e links unten, f links oben, g Mitte).</summary>
    private static readonly string[] Segmente = ["abcdef", "bc", "abdeg", "abcdg", "bcfg", "acdfg", "acdefg", "abc", "abcdefg", "abcdfg"];

    /// <summary>Nummernschild eines Schalterplatzes (P7-15): Schild auf dem Tisch, Ziffer zur Vorderseite.</summary>
    private void Platznummer(Rahmen r, float b, float t, int nummer, float unten)
    {
        var tafel = Netz.Farbe("#1D4ED8");
        Kasten(r, b * 0.3f, 0, b * 0.4f, t, unten, unten + 0.06f, Farben.Geraet);
        float hoehe = 0.42f, tafelUnten = unten + 0.06f;
        Kasten(r, 0, 0, b, t, tafelUnten, tafelUnten + hoehe, tafel);
        float s = 0.035f, zb = Math.Min(b * 0.5f, 0.2f), zh = hoehe * 0.7f;
        float lx = (b - zb) / 2, y0 = tafelUnten + ((hoehe - zh) / 2), y1 = y0 + (zh / 2), y2 = y0 + zh;
        foreach (var seg in Segmente[((nummer % 10) + 10) % 10])
        {
            var (a, bis, von, ober) = seg switch
            {
                'a' => (lx, lx + zb, y2 - s, y2),
                'b' => (lx + zb - s, lx + zb, y1, y2),
                'c' => (lx + zb - s, lx + zb, y0, y1),
                'd' => (lx, lx + zb, y0, y0 + s),
                'e' => (lx, lx + s, y0, y1),
                'f' => (lx, lx + s, y1, y2),
                _ => (lx, lx + zb, y1 - (s / 2), y1 + (s / 2)),
            };
            Kasten(r, a, t, bis - a, 0.015f, von, ober, Farben.Weiss);
        }
    }

    /// <summary>Grosse Bahnhofsuhr am Mast (W8b): runde Scheibe aus Streifen, beidseitig, Zeiger auf 10:10.</summary>
    private void Bahnhofsuhr(Rahmen r, float b, float t)
    {
        const float M = Meter;
        var mitteX = b / 2;
        var mitteZ = t / 2;
        var radius = 0.9f * M;
        var hoehe = 3.0f * M;
        Kasten(r, mitteX - 0.06f, mitteZ - 0.06f, 0.12f, 0.12f, 0, hoehe - radius, Farben.MetallDunkel);
        const int streifen = 14;
        for (var i = 0; i < streifen; i++)
        {
            var y1 = -radius + (2 * radius * i / streifen);
            var y2 = -radius + (2 * radius * (i + 1) / streifen);
            var ym = (y1 + y2) / 2;
            var halb = MathF.Sqrt(MathF.Max(0, (radius * radius) - (ym * ym)));
            var innen = MathF.Sqrt(MathF.Max(0, (radius * 0.86f * (radius * 0.86f)) - (ym * ym)));
            Kasten(r, mitteX - halb, mitteZ - 0.07f, 2 * halb, 0.14f, hoehe + y1, hoehe + y2, Farben.MetallDunkel);
            if (innen > 0)
            {
                Kasten(r, mitteX - innen, mitteZ - 0.085f, 2 * innen, 0.17f, hoehe + y1, hoehe + y2, Farben.Weiss);
            }
        }
        // Zeiger (10:10), roter Sekundenzeiger und Striche bei 12, 3, 6, 9
        Kasten(r, mitteX - 0.03f, mitteZ - 0.1f, 0.06f, 0.2f, hoehe, hoehe + (radius * 0.75f), Farben.Geraet);
        Kasten(r, mitteX - (radius * 0.55f), mitteZ - 0.1f, radius * 0.55f, 0.2f, hoehe - 0.03f, hoehe + 0.03f, Farben.Geraet);
        Kasten(r, mitteX - 0.02f, mitteZ - 0.11f, 0.04f, 0.22f, hoehe - (radius * 0.8f), hoehe, Farben.Rot);
        foreach (var (dx, dy) in new[] { (0f, 0.78f), (0.78f, 0f), (0f, -0.78f), (-0.78f, 0f) })
        {
            Kasten(r, mitteX + (dx * radius) - 0.025f, mitteZ - 0.095f, 0.05f, 0.19f, hoehe + (dy * radius) - 0.025f, hoehe + (dy * radius) + 0.025f, Farben.Geraet);
        }
    }
}
