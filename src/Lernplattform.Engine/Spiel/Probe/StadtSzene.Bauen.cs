using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtSzene"/> mit dem Bau der ganzen Stadt
/// (<c>BauenOhneSperre</c>). Reiner Umzug aus StadtSzene.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtSzene
{
    private static StadtSzene BauenOhneSperre(IStadtSicht stadt, ISpielSicht? stand, GrafikBibliothek? grafik, string? fest, PyObjekt? feste,
        Jahreszeit jahreszeit, Vorrat? vorrat)
    {
        var stuecke = new List<ModellStueck>();
        var inhalt = stadt.Inhalt;
        // Phase 13 Welle 1 (Weisung P12-16): Laternenplaetze und Beete aus der Stadt-Fassung (vorher fest im Programm)
        var laternenSkizze = stadt.Fassung?.Laternen ?? Nachtlicht.Skizze;
        var beeteFest = stadt.Fassung?.Beete ?? Nachtlicht.Beete;
        var welt = Objekt(Objekt(inhalt["orte"])["welt"]);
        var breite = (float)Py.F(welt["breite"]);
        var tiefe = (float)Py.F(welt["hoehe"]);
        // Welle A 0.66.0 (Weisung P12-192): Gelaende (ohne Abschnitt null, dann aendert sich nichts)
        var gelaende = Gelaende.Aus(stadt.Daten);
        var teile = new List<Teil>
        {
            // Boden weit ueber den Rand, damit die geneigte Kamera keinen leeren Himmel zeigt
            Quader(-30, -0.06f, -30, breite + 30, 0f, tiefe + 30, Mischen(Farben.Boden, Schwarz, 0.12f)),
            Quader(0, -0.05f, 0, breite, 0.001f, tiefe, Farben.Boden),
        };
        var streifen = new List<Streifen>();
        var hindernisse = new List<(double, double, double, double)>();
        var wasser = new List<List<Vector2>>();
        var parkFlaechen = new List<(float X1, float Z1, float X2, float Z2)>();
        var gleise = new List<List<Vector2>>();
        var gleisWerkzeug = new List<bool>();
        var andreaskreuze = new List<Bahnuebergang.Kreuz>();
        var landschaft = ListeOderLeer(welt, "landschaft").Select(Objekt).ToList();
        var strassen = landschaft.Where(t => Py.Gleich(t["typ"], "strasse")).Select(t => Vektoren(t["punkte"])).ToList();
        // Phase 13 Welle 4a (Weisung P12-28): Strassen mit Gehweg (Feld "gehweg" = Breite je Seite, nur aus dem Stadtbau-
        // Werkzeug; Fassung 0 hat keine): Gehweg gezeichnet, frei zum Gehen, kein Kleingruen darauf (Laternen stehen wie
        // bisher 0,5 neben der Strassenmitte, also auf dem Gehweg)
        var gehwege = landschaft.Where(t => Py.Gleich(t["typ"], "strasse") && GehwegBreite(t) > 0)
            .Select(t => (Linie: Vektoren(t["punkte"]), Halb: (StrassenBreite / 2) + GehwegBreite(t))).ToList();
        // Welle A 0.54.0 (Weisung P12-115, Sammelliste 254/255): Zebrastreifen aus stadtdaten.zebrastreifen; die Fahrbahn
        // einer Strasse mit Gehweg ist nur dort frei zum Gehen (Fassung 0: keine Zebrastreifen, keine Strasse mit Gehweg)
        var zebras = Zebrastreifen.Alle(stadt.Daten, strassen);
        // Welle A 0.60.0 (Weisung P12-166): Ampeln aus stadtdaten.ampeln; ihre Furten sind fuer die Figur frei wie ein
        // Zebrastreifen (Fassung 0: keine Ampeln)
        var ampeln = Probe.Ampeln.Alle(stadt.Daten, strassen);
        var furten = ampeln.SelectMany(a => a.Furten).ToList();
        // Engine-Welle A 1.4.0 (Weisung P12-351): die Mittellinien haengen nur von Strassen, Zebrastreifen und Furten ab und
        // entstehen darum nebenher; angehaengt werden sie an derselben Stelle wie bisher
        var mittellinien = MittellinienSpaeter(strassen, zebras, furten);
        // Welle A 0.61.0 (Weisung P12-171): Haltestellen und Parkplaetze an geraden Strassen mit Gehweg (Fassung 0: keine)
        var gehwegLinien = gehwege.Select(g => (g.Linie, g.Halb)).ToList();
        var haltestellen = Probe.Haltestellen.Alle(stadt.Daten, gehwegLinien);
        var parkplaetze = Probe.Parkplaetze.Alle(stadt.Daten, gehwegLinien);
        var zugangswege = stadt.Zugaenge.Select(z => Vektoren(Objekt(z.Value)["weg"])).ToList();
        // W8b: Gehwege an Haeusern ohne Ort (Stellwerk) halten Baeume fern und sind frei zum Gehen
        zugangswege.AddRange(StadtTueren.Zierwege(stadt).Select(z => Vektoren(z["weg"])));
        // Welle Z (Weisung P12-71): eigene Fusswege aus stadtdaten.fusswege wie die Wege zu den Tueren (frei zum Gehen,
        // Baeume und Kleingruen halten Abstand); Fassung 0 hat keine
        var fusswege = Fusswege.Alle(stadt.Daten);
        zugangswege.AddRange(fusswege.Select(f => f.Glatt));
        // Nachtrag 1 zu P7-13 (Chat, Foto 02): gezeichnet (mit Bahnuebergang) wird nur der Fussweg eines
        // sichtbaren Orts; Weg zu Talheim-Nord und zur Werkstatt erscheinen mit dem Ort (Tag 12 bzw. 22),
        // vorher lag er als Ring ohne Ziel auf der Wiese. Laufraster und Baumsperren nutzen alle Wege.
        var sichtbareOrte = stadt.Orte(stand).Select(o => Py.Str(o["id"])).ToHashSet();
        var sichtbareWege = stadt.Zugaenge.Where(z => sichtbareOrte.Contains(z.Key)).Select(z => Vektoren(Objekt(z.Value)["weg"])).ToList();
        var bruecken = new List<Vector2>();
        var brueckenKaesten = new List<(Vector2, Vector2, float, float)>();
        var strassenEnden = new List<(Vector2 Punkt, Vector2 Richtung, float Verlaengert)>();
        var uebergaenge = new List<(Vector2 Mitte, bool Strasse)>();
        List<List<Vector2>>? alleStrassen = null;
        foreach (var art in (string[])["park", "fluss", "strasse", "bahn"])
        {
            foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], art)))
            {
                switch (art)
                {
                    case "park":
                    {
                        float x = Zahl(item, "x"), z = Zahl(item, "y");
                        teile.Add(Quader(x, 0f, z, x + Zahl(item, "w"), Hoehe.Park, z + Zahl(item, "h"), Farben.Park));
                        parkFlaechen.Add((x, z, x + Zahl(item, "w"), z + Zahl(item, "h")));
                        break;
                    }
                    case "fluss":
                    {
                        // Bruecken (Wunsch Nico 25.10.2026): wo eine Strasse den Fluss kreuzt
                        // Weisung P12-214 (Nico): die Bruecke umrahmt auch den Gehweg; Strassen ohne Gehweg wie bisher (gw = 0)
                        var strassenGehweg = landschaft.Where(t => Py.Gleich(t["typ"], "strasse")).Select(GehwegBreite).ToList();
                        // Welle Nachladen: der Linienzug des Flusses einmal (vorher je Strasse neu, gleiches Ergebnis)
                        var flussLinie = Linienzug(item);
                        for (var si = 0; si < strassen.Count; si++)
                        {
                            var gw = BrueckeSchmalAlt.Value ? 0f : strassenGehweg[si];
                            foreach (var (mitte, richtung, sinus) in Kreuzungen(flussLinie, strassen[si]))
                            {
                                Bruecke(teile, streifen, mitte, richtung, sinus, gw);
                                bruecken.Add(mitte);
                                brueckenKaesten.Add((mitte, richtung, BrueckeHalb(sinus) + 0.07f, (StrassenBreite / 2) + gw + 0.1f));
                            }
                        }
                        // glatter Bogen, gleich breit, mit Ufer (Nachtrag 1, Wunsch 2); Welle K1: Radius je Eckpunkt
                        var glatt = flussLinie;
                        streifen.Add(new Streifen(glatt, FlussBreite + 0.3f, Hoehe.Ufer, Hoehe.Ufer, Farben.Ufer));
                        streifen.Add(new Streifen(glatt, FlussBreite, Hoehe.Wasser, Hoehe.Wasser, Farben.Wasser));
                        wasser.Add(glatt);
                        break;
                    }
                    case "strasse":
                        {
                            // Phase 8 W11 (Sammelliste 157): ein Strassenende, das weder am Kartenrand noch an einer
                            // anderen Strasse liegt, endet buendig an der Gehwegkante (halbe Gehwegbreite statt halber Strassenbreite)
                            var pp = Punkte(item["punkte"]);
                            // Welle Nachladen A 0.82.0 (grosse Karten): alle Strassen einmal je Bau gelesen, nicht je Strasse und
                            // je Ende neu (gleiche Werte, die Daten aendern sich waehrend des Baus nicht)
                            var alleStr = alleStrassen ??= landschaft.Where(t => Py.Gleich(t["typ"], "strasse")).Select(t => Vektoren(t["punkte"])).ToList();
                            var eigeneLinie = Vektoren(item["punkte"]);
                            float Ende(Vector2 e)
                            {
                                var amRand = e.X <= 0.01f || e.Y <= 0.01f || e.X >= breite - 0.01f || e.Y >= tiefe - 0.01f;
                                var anStrasse = alleStr.Any(a => !a.SequenceEqual(eigeneLinie) && Kurve.Abstand(e, a) <= 0.05f);
                                return amRand || anStrasse ? StrassenBreite / 2 : WegBreite / 2;
                            }
                            if (Kreisverkehr.IstRing(item) && Kreisverkehr.Geschlossen(eigeneLinie))
                            {
                                // Welle K3 (Weisung P12-59): der Ring als geschlossenes Band mit Gehrung auch am Stoss (je
                                // ein Stueck ueber Anfang und Ende hinaus, gleiche Hoehe und Farbe), ohne Strassenenden
                                var ring = new List<Vector2>(eigeneLinie.Count + 2) { eigeneLinie[^2] };
                                ring.AddRange(eigeneLinie);
                                ring.Add(eigeneLinie[1]);
                                if (GehwegBreite(item) is var gwr and > 0)
                                {
                                    streifen.Add(new Streifen(ring, StrassenBreite + (2 * gwr), Hoehe.Gehweg, Hoehe.Gehweg, Mischen(Farben.Strasse, Weiss, 0.45f)));
                                }
                                streifen.Add(new Streifen(ring, StrassenBreite, Hoehe.Strasse, Hoehe.Strasse, Farben.Strasse));
                                break;
                            }
                            if (AlsBand(eigeneLinie))
                            {
                                // Welle K1 (Weisung P12-55): Kurve als ein Band mit Gehrung, Enden wie bei der geraden Strasse
                                var r0 = Vector2.Normalize(eigeneLinie[1] - eigeneLinie[0]);
                                var rn = Vector2.Normalize(eigeneLinie[^1] - eigeneLinie[^2]);
                                var w0 = Weiter(eigeneLinie[0], -r0, eigeneLinie, alleStr);
                                var wn = Weiter(eigeneLinie[^1], rn, eigeneLinie, alleStr);
                                var e0 = w0 is null ? Ende(eigeneLinie[0]) : 0f;
                                var en = wn is null ? Ende(eigeneLinie[^1]) : 0f;
                                strassenEnden.Add((eigeneLinie[0], -r0, e0));
                                strassenEnden.Add((eigeneLinie[^1], rn, en));
                                var band = new List<Vector2>(eigeneLinie);
                                // stumpf angesetzt: ein kurzes Stueck (StumpfZugabe) in Richtung der anschliessenden Strasse, damit
                                // die Kante genau quer zu ihr liegt wie das Ende ihres Bands (sonst ein Keil von 3,75 Grad)
                                band.Insert(0, w0 is { } a0 ? eigeneLinie[0] + (a0 * StumpfZugabe) : eigeneLinie[0] - (r0 * e0));
                                band.Add(wn is { } an ? eigeneLinie[^1] + (an * StumpfZugabe) : eigeneLinie[^1] + (rn * en));
                                if (GehwegBreite(item) is var gwk and > 0)
                                {
                                    streifen.Add(new Streifen(band, StrassenBreite + (2 * gwk), Hoehe.Gehweg, Hoehe.Gehweg, Mischen(Farben.Strasse, Weiss, 0.45f)));
                                }
                                streifen.Add(new Streifen(band, StrassenBreite, Hoehe.Strasse, Hoehe.Strasse, Farben.Strasse));
                                break;
                            }
                            for (var i = 0; i + 1 < pp.Count; i++)
                            {
                                var a = new Vector2(pp[i].X, pp[i].Z);
                                var b2 = new Vector2(pp[i + 1].X, pp[i + 1].Z);
                                var r = Vector2.Normalize(b2 - a);
                                var ea = i == 0 ? (Fortgesetzt(a, -r, eigeneLinie, alleStr) ? 0f : Ende(a)) : StrassenBreite / 2;
                                var eb = i + 2 == pp.Count ? (Fortgesetzt(b2, r, eigeneLinie, alleStr) ? 0f : Ende(b2)) : StrassenBreite / 2;
                                if (i == 0)
                                {
                                    strassenEnden.Add((a, -r, ea));
                                }
                                if (i + 2 == pp.Count)
                                {
                                    strassenEnden.Add((b2, r, eb));
                                }
                                if (GehwegBreite(item) is var gw and > 0)
                                {
                                    teile.Add(Band(a.X - (r.X * ea), a.Y - (r.Y * ea), b2.X + (r.X * eb), b2.Y + (r.Y * eb), Hoehe.Gehweg,
                                        StrassenBreite + (2 * gw), Mischen(Farben.Strasse, Weiss, 0.45f)));
                                }
                                teile.Add(Band(a.X - (r.X * ea), a.Y - (r.Y * ea), b2.X + (r.X * eb), b2.Y + (r.Y * eb), Hoehe.Strasse, StrassenBreite, Farben.Strasse));
                            }
                        }
                        break;
                    case "bahn":
                    {
                        var glatt = Linienzug(item);
                        uebergaenge.AddRange(Gleis(teile, streifen, andreaskreuze, glatt, strassen, sichtbareWege));
                        gleise.Add(glatt);
                        gleisWerkzeug.Add(Radien(item) is not null);
                        break;
                    }
                }
            }
        }
        // S 0.8.0 (Weisung P12-47): Teiche (Ufer und Wasser als Flaechen im Netz der Stadt) und Stege (Holzkaesten)
        var teiche = Wasser.Teiche(stadt.Daten);
        var stege = Wasser.Stege(stadt.Daten);
        var flaechen = new List<Bodenflaeche>();
        foreach (var teich in teiche)
        {
            flaechen.Add(new Bodenflaeche(teich.MitUfer, Hoehe.Ufer, Farben.Ufer));
            flaechen.Add(new Bodenflaeche(teich.Rund, Hoehe.Wasser, Farben.Wasser));
        }
        foreach (var steg in stege)
        {
            teile.AddRange(Wasser.StegTeile(steg));
        }
        // Mittellinien und Fusswege der Stadt (Zugaenge von der Tuer zur Strasse); keine Striche
        // mitten auf einer Kreuzung (dort lagen sonst zwei Striche als "+" uebereinander)
        // Welle K3 (Sammelliste 231): Strassen, die nach Weiter aneinander anschliessen (Kurve an gerade Strasse, Kurve an
        // Kurve), bekommen EINE Mittellinie im gleichen Takt; vorher lief der Takt je Strasse ab 0 und die Kreuzungsregel nahm
        // am Anschluss auf beiden Seiten Striche weg (Luecke). Eine Strasse ohne solchen Anschluss (alle in Fassung 0) wie bisher.
        // Engine-Welle A 1.4.0: die Striche entstehen schon neben dem Bau der Landschaft (nur Strassen, Zebrastreifen, Furten)
        teile.AddRange(Vertauscht.Value ? Enumerable.Reverse(mittellinien.Wert) : mittellinien.Wert);
        // Welle A 0.54.0: weisse Balken je Zebrastreifen (flach auf der Hoehe der Mittellinie, je Balken 2 Dreiecke)
        foreach (var z in zebras)
        {
            foreach (var (a, b) in Zebrastreifen.BalkenVon(z))
            {
                teile.Add(Band(a.X, a.Y, b.X, b.Y, Hoehe.Mittellinie, Zebrastreifen.BalkenBreite, Weiss));
            }
        }
        // Welle A 0.60.0: je Arm einer Ampel die Furt (zwei gestrichelte Linien) und die Haltelinie
        foreach (var amp in ampeln)
        {
            foreach (var arm in amp.Arme)
            {
                var (h1, h2) = Probe.Ampeln.Haltelinie(amp, arm);
                teile.Add(Band(h1.X, h1.Y, h2.X, h2.Y, Hoehe.Mittellinie, Probe.Ampeln.HalteBreite, Weiss));
            }
            foreach (var furt in amp.Furten)
            {
                foreach (var (a, b) in Probe.Ampeln.FurtStriche(furt))
                {
                    teile.Add(Band(a.X, a.Y, b.X, b.Y, Hoehe.Mittellinie, Probe.Ampeln.FurtStrichBreite, Weiss));
                }
            }
        }
        // Welle A 0.61.0: gelbe Zickzack-Linie je Haltestelle; Parkplatz als Flaeche mit weissen Linien, Einfahrt ueber den Gehweg
        foreach (var h in haltestellen)
        {
            foreach (var (a, b) in Probe.Haltestellen.Zickzack(h))
            {
                teile.Add(Band(a.X, a.Y, b.X, b.Y, Hoehe.Mittellinie, Probe.Haltestellen.ZickzackBreite, ZickzackGelb));
            }
            hindernisse.Add(Rechteck(h.HaeuschenEcken()));
        }
        foreach (var pp in parkplaetze)
        {
            flaechen.Add(new Bodenflaeche(GegenUhr(pp.Ecken()), Probe.Parkplaetze.FlaecheHoehe + 0.002f, Probe.Parkplaetze.Flaeche));
            var (e1, e2) = pp.Einfahrt;
            teile.Add(Band(e1.X, e1.Y, e2.X, e2.Y, Probe.Parkplaetze.FlaecheHoehe + 0.002f, Probe.Parkplaetze.Gasse, Probe.Parkplaetze.Flaeche));
            foreach (var (a, b) in pp.Linien())
            {
                teile.Add(Band(a.X, a.Y, b.X, b.Y, Probe.Parkplaetze.LinieHoehe, 0.025f, Weiss));
            }
        }
        foreach (var (_, zugang) in stadt.Zugaenge.Where(z => sichtbareOrte.Contains(z.Key)))
        {
            // Sammelliste 280 (A 0.59.1): ein Weg mit Feld radien als ein Band mit runden Ecken (ohne Feld wie bisher)
            if (StadtTuerweg.Gezeichnet(zugang, WegBreite) is { } rund)
            {
                streifen.Add(new Streifen(rund, WegBreite, Hoehe.Weg, Hoehe.Weg, Mischen(Farben.Strasse, Weiss, 0.45f)));
                continue;
            }
            Linie(teile, Punkte(Objekt(zugang)["weg"]), Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
        }
        // Welle Z: eigene Fusswege als ein Band mit Gehrung (gleiche Hoehe und Farbe wie die Wege zu den Tueren)
        foreach (var f in fusswege)
        {
            streifen.Add(new Streifen(f.Glatt, Fusswege.Breite, Hoehe.Weg, Hoehe.Weg, Mischen(Farben.Strasse, Weiss, 0.45f)));
        }
        // Nachtrag 1 zu P7-13 (Wunsch 1): Baeume weichen Strassen, Fusswegen, Gleisen und dem Fluss aus
        var baeume = new BaumPlaner([
            .. strassen.Select(l => ((IReadOnlyList<Vector2>)l, StrassenBreite / 2)),
            .. zugangswege.Select(l => ((IReadOnlyList<Vector2>)l, WegBreite / 2)),
            .. gleise.Select(l => ((IReadOnlyList<Vector2>)l, (GleisBreite / 2) + 0.1f)),
            .. wasser.Select(l => ((IReadOnlyList<Vector2>)l, (FlussBreite + 0.3f) / 2)),
        ]);
        foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], "baum")))
        {
            var r = Zahl(item, "r", 0.45f);
            foreach (var (x, z) in Punkte(item["punkte"]))
            {
                Baum(teile, stuecke, grafik, baeume, x, z, r);
            }
        }
        // Welle K3 (Weisung P12-59, Frage 2 = a): Mittelinsel jedes Kreisverkehrs (heller Rand, Rasen, Baum in der Mitte),
        // danach fuer Baeume, Kleingruen und Laufraster gesperrt; Fassung 0 hat keinen Kreisverkehr
        var inseln = new List<List<Vector2>>();
        foreach (var (ring, radius) in Kreisverkehr.Alle(landschaft))
        {
            var m = Kreisverkehr.Mitte(ring);
            var ri = Kreisverkehr.InselRadius(radius);
            flaechen.Add(new Bodenflaeche(Kreisverkehr.Kreis(m, ri), Kreisverkehr.RandHoehe, InselRand));
            flaechen.Add(new Bodenflaeche(Kreisverkehr.Kreis(m, ri - Kreisverkehr.RandBreite), Kreisverkehr.RasenHoehe, Farben.Park));
            Baum(teile, stuecke, grafik, baeume, m.X, m.Y, Kreisverkehr.BaumRadius);
            baeume.Sperren.Add(([m, m], ri));
            inseln.Add(Kreisverkehr.Kreis(m, ri - 0.02f));
        }

        var k = StadtModelle.Kontext(stand, inhalt);
        var modelle = StadtModelle.Modelle(inhalt, stadt.Daten);
        var signatur = new List<string>();
        var haeuser = new List<StadtHaus>();
        var fensterJeHaus = new Dictionary<string, List<(int, Vector3)>>();
        var alleFenster = new List<(int Teil, Vector3 Wand)>();
        // Welle A 0.70.0 (Sammelliste 314): fein gedrehte Gebaeude ohne Ort werden im Rahmen des Hauses gebaut und erst nach ihrer
        // Tuer (Zierweg unten) als Ganzes gedreht
        var feinOffen = new List<FeinBau>();
        var hausBloecke = new Dictionary<string, List<(double, double, double, double)>>();
        foreach (var item in landschaft.Where(t => Py.Gleich(t["typ"], "gebaeude")))
        {
            var modell = Py.Str(item["modell"]);
            var sichtbar = OhneGeruest(StadtModelle.Teile(modelle, modell, k));
            var farbe = Py.Hat(item, "farbe") ? Netz.Farbe(Py.Str(item["farbe"])) : RollenFarbe(null);
            var (tv, sv) = (teile.Count, stuecke.Count);
            var fenster = new List<(int, Vector3)>();
            var fein = new Hausdrehung.Feinlage(new Vector2(Zahl(item, "x"), Zahl(item, "y")) + Hausdrehung.Mitte(Get(modelle, modell)), Hausdrehung.GradVon(item));
            var feinBau = fein.Aktiv ? new FeinBau(fein, tv, sv) : null;
            var hListe = feinBau?.Hindernisse ?? hindernisse;
            var hVorher = hListe.Count;
            var (hmin, hmax) = Modell(teile, stuecke, grafik, modell, sichtbar, Zahl(item, "x"), Zahl(item, "y"), farbe, modell, feinBau?.Hindernisse ?? hindernisse, baeume, fenster: fenster,
                dreh: Hausdrehung.Von(item), drehMitte: Hausdrehung.Mitte(Get(modelle, modell)), grundVon: KopieGrund(modelle, modell), baeumeSpaeter: feinBau?.Baeume);
            alleFenster.AddRange(fenster);
            if (feinBau is not null)
            {
                feinBau.TeileBis = teile.Count;
                feinBau.StueckeBis = stuecke.Count;
                feinOffen.Add(feinBau);
            }
            if (hmin.X <= hmax.X)
            {
                var hausId = modell + "#" + haeuser.Count.ToString(CultureInfo.InvariantCulture);
                if (feinBau is not null)
                {
                    feinBau.Haus = haeuser.Count;
                }
                haeuser.Add(new StadtHaus(hausId, tv, teile.Count, sv, stuecke.Count, hmin, hmax));
                fensterJeHaus[hausId] = fenster;
                hausBloecke[hausId] = [.. hListe.Skip(hVorher)];
            }
            signatur.Add(modell + ":" + string.Join(",", sichtbar.Select(t => t.Nr)));
        }
        // W8b (Nico 3a): Haeuser ohne Ort (Stellwerk) mit Gehweg zur Strasse, Tuer und Namensschild, nicht betretbar
        var zierhaeuser = new List<StadtZierhaus>();
        var tueren = new List<StadtTuer>();
        var umgefaerbt = new Dictionary<int, GrafikModell>();
        // P8-28 (Nico: Tueren sitzen nicht am Haus, Fenster halb verdeckt): Tuer auf die echte Wand des CC0-Modells
        // (gemessen, StadtTueren.Wandtiefe), Gehweg bis dorthin, Rahmen nach hinten bis an die Wand, Fenster an der
        // Tuer weg (Glas der Modelle in Wandfarbe, eigene Fensterscheiben in der Farbe ihres Blocks); P8-30: Tueren der
        // Modelle daneben und an den Seitenwaenden samt Rahmen in Wandfarbe (nur eine Tuer je Haus), kein eigenes
        // Vordach unter einem Dach des Modells
        StadtTuer TuerSetzen(string ort, Vector2 wand, Vector2 aussen, Vector2 anfang, float hoehe, bool modellTuer, bool gehweg,
            int stueckeVon, int stueckeBis, List<(int Teil, Vector3 Wand)> fenster, Vector3 hausMin, Vector3 hausMax, bool windfang = false)
        {
            if (windfang && !WindfangAlt.Value)
            {
                // Welle A 0.70.0 (Weisung P12-218 Punkt 1 = a): kleiner Vorbau mit Tuer vorn, von der tiefsten Stelle der Wand bis vor
                // alles, was an dieser Stelle aus der Fassade ragt (Band der Betriebswerkstatt); die Tuer liegt buendig auf ihm
                var (vor, tief) = grafik is not null && stueckeBis > stueckeVon
                    ? StadtTueren.Ausdehnung(stuecke.GetRange(stueckeVon, stueckeBis - stueckeVon), grafik, wand, aussen, hoehe)
                    : (0f, 0f);
                var vorn = wand + (aussen * (MathF.Max(0f, -vor) + StadtTueren.WindfangVor));
                StadtTueren.Windfang(teile, wand - (aussen * (tief + 0.02f)), vorn, aussen, hoehe, Gebaeudefarben.Tuer(stadt.Daten, ort));
                if (gehweg && Vector2.Distance(vorn, anfang) > 0.02f && Vector2.Dot(anfang - vorn, aussen) > 0)
                {
                    Linie(teile, [(anfang.X, anfang.Y), (vorn.X, vorn.Y)], Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
                }
                return new StadtTuer(ort, vorn, aussen, anfang, false, hoehe, 0f) { Windfang = true, AnWand = wand };
            }
            // Fund 262 (Weisung P12-122): hat das Modell an dieser Stelle selbst eine Tuer (Stadt-Modell: genau dort, Gebaeude
            // aus dem Editor: jede Glastuer, die die gezeichnete Tuer ueberdecken wuerde), ist sie die Tuer; keine zweite darueber
            if (!modellTuer && grafik is not null && stueckeBis > stueckeVon
                && StadtTueren.EigeneTuer(stuecke, stueckeVon, stueckeBis, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), wand, aussen,
                    Enumerable.Range(stueckeVon, stueckeBis - stueckeVon).Any(i => StadtTueren.MitGlasTuer(stuecke[i].Name))) is { } eigene)
            {
                var querE = new Vector2(-aussen.Y, aussen.X);
                // liegt der Tuerpunkt auf der Tuer, bleibt er (Weg gerade auf die Tuer), sonst an ihren naechsten Rand
                var mitteE = wand + (querE * Math.Clamp(0f, eigene.A1, eigene.A2)) - (aussen * eigene.Tiefe);
                if (gehweg && Vector2.Distance(mitteE, anfang) > 0.02f && Vector2.Dot(anfang - mitteE, aussen) > 0)
                {
                    Linie(teile, [(anfang.X, anfang.Y), (mitteE.X, mitteE.Y)], Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
                }
                // Sammelliste 267: die Glastuer eines Gebaeudes aus dem Editor wird als Tuer erkennbar (Tuerblatt und Klinke)
                if (StadtTueren.MitGlasTuer(stuecke[eigene.Stueck].Name))
                {
                    StadtTueren.TuerblattSetzen(teile, stuecke, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), umgefaerbt, eigene, wand, aussen,
                        Gebaeudefarben.Tuer(stadt.Daten, ort));
                }
                // Sammelliste 292 (Nico 4a): eine gewaehlte Tuer-Farbe faerbt auch die Tuer eines Stadt-Modells (nur das Tuerblatt)
                else if (Gebaeudefarben.Tuer(stadt.Daten, ort) is { } tuerFarbe)
                {
                    StadtTueren.ModellTuerFaerben(stuecke, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), umgefaerbt, eigene, tuerFarbe);
                }
                return new StadtTuer(ort, mitteE, aussen, anfang, true, hoehe, 0f);
            }
            var (tiefe, hinten) = grafik is not null && stueckeBis > stueckeVon
                ? StadtTueren.Wandtiefe(stuecke.GetRange(stueckeVon, stueckeBis - stueckeVon), grafik, wand, aussen, hoehe) ?? (0f, 0f)
                : (0f, 0f);
            var mitte = wand - (aussen * tiefe);
            if (gehweg && Vector2.Distance(mitte, anfang) > 0.02f && Vector2.Dot(anfang - mitte, aussen) > 0)
            {
                Linie(teile, [(anfang.X, anfang.Y), (mitte.X, mitte.Y)], Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
            }
            if (modellTuer)
            {
                // Sammelliste 292 (Nico 4a): gewaehlte Tuer-Farbe auch auf der Tuer des Modells (Ort mit modell_tuer)
                if (grafik is not null && stueckeBis > stueckeVon && Gebaeudefarben.Tuer(stadt.Daten, ort) is { } farbeM
                    && StadtTueren.ModellTueren(stuecke, stueckeVon, stueckeBis, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), wand, aussen,
                        weite: StadtTueren.EigeneTuerFang + StadtTueren.GlasTuerBreit).Where(t => MathF.Abs(t.Mitte) <= StadtTueren.HalbRahmen).MinBy(t => MathF.Abs(t.Mitte)) is { } mtF)
                {
                    StadtTueren.ModellTuerFaerben(stuecke, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), umgefaerbt, mtF, farbeM);
                }
                return new StadtTuer(ort, mitte, aussen, anfang, true, hoehe, 0f);
            }
            var vordach = grafik is null || stueckeBis == stueckeVon
                || !StadtTueren.Ueberdacht(stuecke.GetRange(stueckeVon, stueckeBis - stueckeVon), grafik, mitte, aussen, hoehe);
            StadtTueren.Tuer(teile, mitte.X + (aussen.X * 0.005f), mitte.Y + (aussen.Y * 0.005f), aussen, hoehe, hinten, vordach,
                Gebaeudefarben.Tuer(stadt.Daten, ort));
            if (grafik is not null)
            {
                StadtTueren.FensterWeg(stuecke, stueckeVon, stueckeBis, grafik, umgefaerbt, mitte, aussen, hoehe, hinten);
                StadtTueren.TuerenWeg(stuecke, stueckeVon, stueckeBis, grafik, umgefaerbt, mitte, aussen);
                StadtTueren.SeitenTuerenWeg(stuecke, stueckeVon, stueckeBis, grafik, umgefaerbt, aussen, hausMin, hausMax);
            }
            foreach (var (nr, wandFarbe) in fenster)
            {
                var f = teile[nr];
                // Kasten: die zwei Ecken decken quer, Hoehe und Tiefe ganz ab (Waende laufen in x oder z)
                if (StadtTueren.AufTuerFlaeche(new(f.X1, f.Y1, f.Z1), new(f.X2, f.Y2, f.Z2), new(f.X1, f.Y1, f.Z1), mitte, aussen, hoehe, hinten))
                {
                    teile[nr] = f with { Farbe = wandFarbe };
                }
            }
            return new StadtTuer(ort, mitte, aussen, anfang, false, hoehe, hinten);
        }
        foreach (var zier in StadtTueren.Zierwege(stadt))
        {
            var weg = Vektoren(zier["weg"]);
            if (StadtTuerweg.Gezeichnet(zier, WegBreite) is { } rund)
            {
                streifen.Add(new Streifen(rund, WegBreite, Hoehe.Weg, Hoehe.Weg, Mischen(Farben.Strasse, Weiss, 0.45f)));
            }
            else
            {
                Linie(teile, [.. weg.Select(v => (v.X, v.Y))], Hoehe.Weg, WegBreite, Mischen(Farben.Strasse, Weiss, 0.45f));
            }
            if (haeuser.FirstOrDefault(h => h.Id.StartsWith(Py.Str(zier["modell"]) + "#", StringComparison.Ordinal)) is not { } haus)
            {
                continue;
            }
            // Welle A 0.70.0: Tuer eines fein gedrehten Gebaeudes im Rahmen des Hauses (Tuer und Wegpunkt zurueckgedreht)
            var zierFein = feinOffen.FirstOrDefault(f => f.Haus == haeuser.IndexOf(haus));
            var zierLage = zierFein?.Lage ?? default;
            var (zierTeile, zierTueren) = (teile.Count, tueren.Count);
            var zierAnfang = zierLage.Zurueck(weg[0]);
            var zierTuer = zierLage.Zurueck(Vektoren(new List<object?> { zier["tuer"] })[0]);
            var (wand, aussen) = StadtTueren.Wand(haus.Min, haus.Max, zierTuer);
            // Welle A 0.70.0 (Haltepunkt mit freiem Bahnsteig): liegt die Tuer innen im Kasten des Hauses (ein Bahnsteig gehoert
            // fest zum Modell), sitzt sie auf der naechsten Wand der Bloecke wie bei einem Ort; Tueren auf dem Kasten wie bisher
            // (Fassung 0 bitgleich), Gegenprobe ZierKastenAltFuerTests
            if (!ZierKastenAlt.Value && ImKastenInnen(haus.Min, haus.Max, zierTuer)
                && hausBloecke.GetValueOrDefault(haus.Id) is { Count: > 0 } zb)
            {
                var block = zb.MinBy(bk => Kurve.Abstand(zierTuer, [new((float)bk.Item1, (float)bk.Item2), new((float)bk.Item3, (float)bk.Item2),
                    new((float)bk.Item3, (float)bk.Item4), new((float)bk.Item1, (float)bk.Item4), new((float)bk.Item1, (float)bk.Item2)]));
                (wand, aussen) = StadtTueren.Wand(new Vector3((float)block.Item1, 0, (float)block.Item2), new Vector3((float)block.Item3, haus.Max.Y, (float)block.Item4), zierTuer);
            }
            // Sammelliste 312 (Weisung P12-207): ein Zierweg mit „modell_tuer“: true fuehrt auf die eigene Tuer des Modells (wie ein Ort
            // mit darstellung.modell_tuer, z. B. das helle Rolltor der Betriebswerkstatt); ohne das Feld wie bisher (Fassung 0 bitgleich)
            var zierModellTuer = !ZierModellTuerAlt.Value && Py.Wahr(zier["modell_tuer"]) && haus.StueckeBis > haus.StueckeVon;
            tueren.Add(TuerSetzen(Py.Str(zier["modell"]), wand, aussen, zierAnfang, MathF.Min(TuerHoehe, haus.Max.Y * 0.7f), zierModellTuer, true,
                haus.StueckeVon, haus.StueckeBis, fensterJeHaus.GetValueOrDefault(haus.Id) ?? [], haus.Min, haus.Max, Py.Wahr(zier["windfang"])));
            if (grafik is not null)
            {
                // Phase 8 W11 (Sammelliste 156): die Tuer des Modells auf der Rueckseite (ohne Gehweg) samt Glas und
                // Rahmen in Wandfarbe; die vordere Tuer mit Gehweg bleibt
                var c = new Vector2((haus.Min.X + haus.Max.X) / 2, (haus.Min.Z + haus.Max.Z) / 2);
                var tief = MathF.Abs(aussen.X) > 0.5f ? (haus.Max.X - haus.Min.X) / 2 : (haus.Max.Z - haus.Min.Z) / 2;
                var lang = MathF.Abs(aussen.X) > 0.5f ? (haus.Max.Z - haus.Min.Z) / 2 : (haus.Max.X - haus.Min.X) / 2;
                var rm = c - (aussen * tief);
                var querR = new Vector2(aussen.Y, -aussen.X);
                // Sammelliste 292 (Weisung P12-196): nur bis zur Hausmitte suchen (vorher 2 * tief = bis zur Vorderwand, das malte
                // die benutzte Modelltuer und die Fenster daneben zu); Gegenprobe RueckseiteAltFuerTests
                var suchTief = RueckseiteAlt.Value ? 2 * tief : tief;
                var gefunden = StadtTueren.ModellTueren(stuecke, haus.StueckeVon, haus.StueckeBis, i => umgefaerbt.GetValueOrDefault(i) ?? grafik.Modell(stuecke[i].Name), rm, -aussen, weite: lang, tief: suchTief);
                foreach (var mt in gefunden)
                {
                    StadtTueren.FensterWeg(stuecke, haus.StueckeVon, haus.StueckeBis, grafik, umgefaerbt, rm + (querR * mt.Mitte), -aussen, mt.Oben, mt.Tiefe + 0.1f);
                }
                StadtTueren.TuerenWeg(stuecke, haus.StueckeVon, haus.StueckeBis, grafik, umgefaerbt, rm, -aussen, lang, suchTief);
            }
            var ende = weg[^1];
            var (zMin, zMax) = zierFein is null ? (haus.Min, haus.Max) : zierLage.Kasten(haus.Min, haus.Max);
            zierhaeuser.Add(new StadtZierhaus(Py.Str(zier["name"]), new Vector3((zMin.X + zMax.X) / 2, zMax.Y + 0.55f, (zMin.Z + zMax.Z) / 2),
                zMin, zMax, ende.X, ende.Y));
            if (zierFein is not null)
            {
                zierFein.Weitere.Add((zierTeile, teile.Count));
                zierFein.Tueren.AddRange(Enumerable.Range(zierTueren, tueren.Count - zierTueren));
            }
        }
        foreach (var f in feinOffen)
        {
            FeinDrehen(f, teile, stuecke, hindernisse, tueren);
            foreach (var (bx, bz, br) in f.Baeume)
            {
                var p = f.Lage.Punkt(new Vector2(bx, bz));
                Baum(teile, stuecke, grafik, baeume, p.X, p.Y, br);
            }
            if (f.Haus >= 0)
            {
                var h = haeuser[f.Haus];
                var (wMin, wMax) = f.Lage.Kasten(h.Min, h.Max);
                haeuser[f.Haus] = h with { Min = wMin, Max = wMax, Fein = f.Lage, LokalMin = h.Min, LokalMax = h.Max };
            }
        }
        var orte = new List<StadtOrt>();
        // Phase 13 W3 (Weisung P12-21): mit Vorrat (nur Werkzeug) kommt ein Ort mit genau gleichen Eingaben aus dem letzten Bau
        var sperrenText = vorrat is null ? "" : SperrenText(baeume.Sperren);
        var orteNeu = new Dictionary<string, OrtBau>();
        // Welle A 0.58.0 (Weisung P12-148 Frage 1 = a): Bloecke je Ort, auf deren Wand das Spiel die Tuer setzt (nur lesend fuer das Werkzeug)
        var ortBloecke = new Dictionary<string, IReadOnlyList<(double X1, double Z1, double X2, double Z2)>>();
        foreach (var ort in stadt.Orte(stand))
        {
            var id = Py.Str(ort["id"]);
            var modell = Py.Str(ort["modell"]);
            var sichtbar = StadtModelle.Teile(modelle, modell, k);
            string? ortSchluessel = null;
            var (ortTv, ortSv, ortHv, ortBv, ortFv, ortTuer, ortHaus) = (teile.Count, stuecke.Count, hindernisse.Count, baeume.Gesetzt.Count, alleFenster.Count, tueren.Count, haeuser.Count);
            // Welle A 0.70.0 (Sammelliste 314): ein fein gedrehter Ort wird im Rahmen des Hauses gebaut (mit Tuer) und danach gedreht;
            // er kommt nie aus dem Vorrat des Werkzeugs (seine Bloecke liegen im Rahmen des Hauses, der Vorrat kennt nur die Welt)
            var ortFein = new Hausdrehung.Feinlage(new Vector2((float)Py.F(ort["x"]), (float)Py.F(ort["y"])) + Hausdrehung.Mitte(Get(modelle, modell)),
                Hausdrehung.GradVonOrt(stadt.Daten, id));
            if (vorrat is not null && !ortFein.Aktiv)
            {
                ortSchluessel = OrtSchluessel(stadt, ort, id, sichtbar, StadtTueren.ModellVersatz(stadt, id), sichtbareOrte.Contains(id), sperrenText);
                if (vorrat.Orte.TryGetValue(id, out var alt) && (alt.Schluessel == ortSchluessel || (vorrat.FalschUebernehmenFuerTests && orteNeu.Count == 0)))
                {
                    OrtEinsetzen(alt, teile, stuecke, hindernisse, baeume, alleFenster, umgefaerbt, tueren, haeuser, orte, signatur);
                    orteNeu[id] = alt;
                    ortBloecke[id] = [.. hindernisse.Skip(ortHv)];
                    vorrat.Uebernommen++;
                    continue;
                }
                vorrat.NeuGebaut++;
            }
            float ox = (float)Py.F(ort["x"]), oz = (float)Py.F(ort["y"]);
            var rolle = ort["rolle"] as string ?? "";
            var vorher = stuecke.Count;
            var teileVorher = teile.Count;
            var feinBau = ortFein.Aktiv ? new FeinBau(ortFein, teileVorher, vorher) : null;
            var ortHindernisse = feinBau?.Hindernisse ?? hindernisse;
            var hindernisVorher = ortHindernisse.Count;
            var modellTuer = StadtTueren.ModellVersatz(stadt, id);
            var fenster = new List<(int, Vector3)>();
            var (min, max) = Modell(teile, stuecke, grafik, modell, sichtbar, ox, oz, RollenFarbe(rolle), id, ortHindernisse, baeume, modellTuer ?? 0f, fenster,
                Hausdrehung.VonOrt(stadt.Daten, id), Hausdrehung.Mitte(Get(modelle, modell)), KopieGrund(modelle, modell), feinBau?.Baeume);
            alleFenster.AddRange(fenster);
            var mitGrafik = stuecke.Count > vorher;
            var (tx, tz) = stadt.Tuer(id) is { } zelle
                ? ((float)((zelle.Item1 + 0.5) * stadt.Zelle), (float)((zelle.Item2 + 0.5) * stadt.Zelle))
                : ((min.X + max.X) / 2, max.Z);
            if (Get(stadt.Zugaenge, id) is PyObjekt zugang)
            {
                // W8b (Nico 4a): die Tuer sitzt genau am Anfang des Fusswegs auf der naechsten Wand des Hauses
                // (Bloecke dieses Orts); bleibt eine Luecke, laeuft der Gehweg bis an die Wand
                var anfang = Vektoren(zugang["weg"])[0];
                (tx, tz) = (anfang.X, anfang.Y);
                anfang = ortFein.Zurueck(anfang);
                var bloecke = ortHindernisse.Skip(hindernisVorher).ToList();
                if (bloecke.Count > 0)
                {
                    var block = bloecke.MinBy(bk => Kurve.Abstand(anfang, [new((float)bk.Item1, (float)bk.Item2), new((float)bk.Item3, (float)bk.Item2),
                        new((float)bk.Item3, (float)bk.Item4), new((float)bk.Item1, (float)bk.Item4), new((float)bk.Item1, (float)bk.Item2)]));
                    var (wand, aussen) = StadtTueren.Wand(new Vector3((float)block.Item1, 0, (float)block.Item2), new Vector3((float)block.Item3, max.Y, (float)block.Item4), anfang);
                    // niedrige Haeuser (Kiosk, Bahnhof) bekommen eine niedrigere Tuer
                    var hBlock = sichtbar.Where(t => Py.Gleich(t.Teil["art"], "block")).Select(t => Zahl(t.Teil, "h")).DefaultIfEmpty(1f).Max();
                    tueren.Add(TuerSetzen(id, wand, aussen, anfang, MathF.Min(TuerHoehe, hBlock * 0.7f), modellTuer is not null && mitGrafik,
                        sichtbareOrte.Contains(id), vorher, stuecke.Count, fenster, min, max, StadtTueren.MitWindfang(stadt, id)));
                }
            }
            else if (!mitGrafik)
            {
                // ohne Fussweg: kleines Rechteck vorne oder hinten an der Front
                var lt = ortFein.Zurueck(new Vector2(tx, tz));
                var vorne = lt.Y >= (min.Z + max.Z) / 2;
                var seite = vorne ? max.Z : min.Z;
                teile.Add(Quader(lt.X - (TuerBreite / 2), 0f, vorne ? seite - 0.02f : seite - 0.03f, lt.X + (TuerBreite / 2), TuerHoehe, vorne ? seite + 0.03f : seite + 0.02f, Farben.Tuer));
            }
            var (lokalMin, lokalMax) = (min, max);
            if (feinBau is not null)
            {
                feinBau.TeileBis = teile.Count;
                feinBau.StueckeBis = stuecke.Count;
                feinBau.Tueren.AddRange(Enumerable.Range(ortTuer, tueren.Count - ortTuer));
                FeinDrehen(feinBau, teile, stuecke, hindernisse, tueren);
                foreach (var (bx, bz, br) in feinBau.Baeume)
                {
                    var p = ortFein.Punkt(new Vector2(bx, bz));
                    Baum(teile, stuecke, grafik, baeume, p.X, p.Y, br);
                }
                if (min.X <= max.X)
                {
                    (min, max) = ortFein.Kasten(min, max);
                }
            }
            if (min.X <= max.X)
            {
                haeuser.Add(feinBau is null
                    ? new StadtHaus(id, teileVorher, teile.Count, vorher, stuecke.Count, min, max)
                    : new StadtHaus(id, teileVorher, teile.Count, vorher, stuecke.Count, min, max) { Fein = ortFein, LokalMin = lokalMin, LokalMax = lokalMax });
            }
            ortBloecke[id] = [.. ortHindernisse.Skip(hindernisVorher)];
            var schild = new Vector3((min.X + max.X) / 2, max.Y + 0.55f, (min.Z + max.Z) / 2);
            var (sx, sz) = Stehplatz(stadt, id, tx, tz);
            orte.Add(new StadtOrt(id, ort["name"] as string ?? id, ort["kurz"] as string ?? ort["name"] as string ?? id, rolle,
                tx, tz, min, max, schild) { StehX = sx, StehZ = sz, Ansicht = ort["ansicht"] as string });
            signatur.Add(id + ":" + string.Join(",", sichtbar.Select(t => t.Nr)));
            if (ortSchluessel is not null)
            {
                orteNeu[id] = OrtFesthalten(ortSchluessel, ortTv, ortSv, ortHv, ortBv, ortFv, ortTuer, ortHaus, teile, stuecke, hindernisse, baeume,
                    alleFenster, umgefaerbt, tueren, haeuser, orte, signatur);
            }
        }
        if (vorrat is not null)
        {
            vorrat.Orte = orteNeu;
        }
        if (grafik is not null)
        {
            // Welle A 0.70.0 (Weisung P12-220): ist ein Bahnsteig mit einem Bahnhof verknuepft, haelt der Zug an ihm (ohne
            // Verknuepfung wie bisher, Fassung 0 bitgleich)
            Zug(stuecke, grafik, gleise, ZugHaltAlt.Value ? null : VerknuepfterBahnsteig(landschaft, modelle));
        }
        // Phase 13 Welle 4a (Weisung P12-28): Stuecke aus dem Katalog des Stadtbau-Werkzeugs (stadtdaten.katalog) mit den
        // mitgelieferten Modellen; ein Modell versperrt den Weg mit seinem Rechteck, ein Effekt (Stern) nichts. Fehlt ein
        // Modell, zeichnet die Stadt nichts dafuer (die Ladepruefung StadtbauPruefung.Verbaut meldet das vorher rot).
        foreach (var k2 in Katalogstueck.AusStadtdaten(stadt.Daten))
        {
            if (grafik?.Modell(k2.Typ) is not { } km)
            {
                continue;
            }
            stuecke.Add(new ModellStueck(k2.Typ, Katalogstueck.Lage(km, k2.X, k2.Z, k2.W, k2.T, k2.Dreh, 0f, k2.Effekt)));
            if (!k2.Effekt)
            {
                var (x1, z1, x2, z2) = k2.Rechteck;
                hindernisse.Add((x1, z1, x2, z2));
            }
        }
        // Welle A 0.61.0 (Nico 3a): geparkte Autos (je Spieltag gezogen, im Werkzeug alle Plaetze belegt) als Stuecke der Stadt;
        // jeder Stellplatz versperrt den Weg (die Fahrgasse bleibt frei)
        var parkTag = stadt.VerkehrTag ?? stand?.Tag ?? 1;
        foreach (var pp in parkplaetze)
        {
            var nr = 0;
            foreach (var (mitte, blick) in pp.Stellplaetze())
            {
                var quer = Bahnuebergang.Rechts(blick);
                var t = blick * (Probe.Parkplaetze.PlatzTief / 2);
                var q = quer * (Probe.Parkplaetze.PlatzBreit / 2);
                hindernisse.Add(Rechteck([mitte - t - q, mitte + t - q, mitte + t + q, mitte - t + q]));
                if (Probe.Parkplaetze.Belegung(pp.Nr, nr++, parkTag, stadt.ParkplaetzeVoll) is { } auto
                    && Fahrzeugmodelle.Zeichnen(auto) is var am && grafik?.Modell(am) is { } pm)
                {
                    var anker = new Vector3((pm.Min.X + pm.Max.X) / 2, pm.Min.Y, (pm.Min.Z + pm.Max.Z) / 2);
                    stuecke.Add(new ModellStueck(am, Lage(pm, anker, new Vector3(Meter * 1.1f), new Vector3(mitte.X, Probe.Parkplaetze.LinieHoehe, mitte.Y), blick)));
                }
            }
        }
        // Welle A 0.70.0 (Weisung P12-218 Punkt 3, Nico b, „Prellbock wie im Entwurf“): ein frei endendes Gleis aus dem Werkzeug
        // (Feld radien) bekommt einen Prellbock; nicht am Kartenrand, nicht an einem anderen Gleis (Stoss, Weiche), nicht an
        // einem Haus (Abstellgleis im Schuppen). Das Gleis der Stadt 1 (Fassung 0) hat kein Feld radien: dort bleibt alles gleich.
        if (!PrellbockAlt.Value)
        {
            for (var g = 0; g < gleise.Count; g++)
            {
                if (!gleisWerkzeug[g] || gleise[g].Count < 2)
                {
                    continue;
                }
                foreach (var (ende, hinaus) in GleisEnden(gleise[g]))
                {
                    if (ende.X <= PrellbockRand || ende.Y <= PrellbockRand || ende.X >= breite - PrellbockRand || ende.Y >= tiefe - PrellbockRand
                        || gleise.Where((_, i) => i != g).Any(o => Kurve.Abstand(ende, o) <= PrellbockRand)
                        || Vector2.Distance(ende, Vector2.Distance(gleise[g][0], ende) < 1e-4f ? gleise[g][^1] : gleise[g][0]) <= PrellbockRand
                        || haeuser.Any(h => h.Abstand(ende) <= PrellbockHaus))
                    {
                        continue;
                    }
                    foreach (var t in Prellbock(ende, hinaus))
                    {
                        teile.Add(gelaende is null ? t : Anheben(gelaende, t) with { Dreh = t.Dreh });
                    }
                    var quer = new Vector2(-hinaus.Y, hinaus.X) * 0.25f;
                    var vorn = ende - (hinaus * 0.1f);
                    var hinten = ende + (hinaus * 0.16f);
                    hindernisse.Add(Rechteck([vorn - quer, hinten - quer, hinten + quer, vorn + quer]));
                }
            }
        }
        // Hindernisse vor der Fest-Deko: das Gruen haengt so nicht an der heutigen Festzeit
        var hindernisseVorFest = hindernisse.ToList();
        var festTeileVon = teile.Count;
        var festStueckeVon = stuecke.Count;
        // Weisung P8-56 (Nico, Bild 4): auch das Stellwerk (Haus ohne Ort mit Tuer) bekommt die Deko an der Tuer wie die
        // Haeuser der Orte; es steht hinten in der Liste, die Deko der Orte bleibt so an ihrem Platz
        var festOrte = orte.Concat(tueren.Where(t => !t.Modell && orte.All(o => o.Id != t.Ort))
            .Select(t => (Tuer: t, Haus: zierhaeuser.FirstOrDefault(z => t.Mitte.X >= z.KastenMin.X - 0.3f && t.Mitte.X <= z.KastenMax.X + 0.3f
                && t.Mitte.Y >= z.KastenMin.Z - 0.3f && t.Mitte.Y <= z.KastenMax.Z + 0.3f)))
            .Where(p => p.Haus is not null)
            .Select(p => new StadtOrt(p.Tuer.Ort, p.Haus!.Name, p.Haus.Name, "zier", p.Tuer.Mitte.X, p.Tuer.Mitte.Y, p.Haus.KastenMin, p.Haus.KastenMax, p.Haus.Schild)
            {
                StehX = p.Tuer.WegAnfang.X,
                StehZ = p.Tuer.WegAnfang.Y,
            })).ToList();
        var festGesetzt = FestDeko.Stadt(teile, stuecke, grafik, FestDeko.Muster(feste, fest), festOrte, baeume.Sperren, hindernisse, tueren);
        if (fest is not null)
        {
            signatur.Add("fest:" + fest);
        }
        // Phase 8 W7 (Sammelliste 129): Gruen nach der Festzeit-Deko; frei bleiben auch die Plaetze JEDER Festzeit,
        // damit das Gruen an jedem Tag gleich steht und die Deko aus W6 unveraendert bleibt
        Gruen.Ergebnis? gruen = null;
        var festPlaetze = new List<(Vector2 P, float R)>();
        if (grafik is not null)
        {
            foreach (var f in Festzeit.Alle)
            {
                foreach (var g in FestDeko.Stadt([], [], grafik, FestDeko.Muster(feste, f), festOrte, baeume.Sperren, hindernisseVorFest.ToList(), tueren))
                {
                    festPlaetze.Add((new Vector2(g.X, g.Z), Math.Max(g.R, 0.12f)));
                }
            }
            var haeuserKaesten = haeuser.Select(h => (h.Min, h.Max)).ToList();
            // W11 (Weisung P8-47): die Laternenplaetze aus Nicos Skizze bleiben frei von Kleingruen
            var laternenPlaetze = Nachtlicht.Wunschplaetze(strassen, breite, tiefe, laternenSkizze).ToList();
            // Welle Nachladen A 0.82.0: dieselbe Pruefung ueber Raster (nur nahe Eintraege, gleiches Ergebnis; Radius ueber
            // FreiRMax: ganze Listen wie bisher). Die Raster entstehen beim ersten Aufruf (alle Listen stehen dann fest).
            NahRaster<int>? linienNah = null, punkteNah = null;
            bool Frei(float x, float z, float r)
            {
                if (r > FreiRMax)
                {
                    return FreiGanz(x, z, r);
                }
                if (linienNah is null || punkteNah is null)
                {
                    (linienNah, punkteNah) = FreiRaster();
                }
                var q = new Vector2(x, z);
                // Linien: 0..n Sperren, dann Gehwege, dann Laternenplaetze; Punkte/Kaesten: Orte, Haeuser, Hindernisse, Festplaetze, Kreuze
                return linienNah.Alle(x, z, i => i < baeume.Sperren.Count
                        ? Kurve.Abstand(q, baeume.Sperren[i].Linie) >= baeume.Sperren[i].Halb + r + 0.02f
                        : i < baeume.Sperren.Count + gehwege.Count
                            ? Kurve.Abstand(q, gehwege[i - baeume.Sperren.Count].Linie) >= gehwege[i - baeume.Sperren.Count].Halb + r + 0.02f
                            : LaterneFrei(laternenPlaetze[i - baeume.Sperren.Count - gehwege.Count], q, r))
                    && punkteNah.Alle(x, z, i => PunktFrei(i, x, z, r))
                    && teiche.All(t => !Wasser.Drin(t.MitUfer, q) && Wasser.RandAbstand(t.MitUfer, q) >= r + 0.02f)
                    && (gelaende is null || gelaende.Flach(x, z, r));
            }
            static bool LaterneFrei((Vector2 A, Vector2 B) lp, Vector2 q, float r) =>
                Kurve.Abstand(q, [lp.A, lp.B]) >= Nachtlicht.AbstandModell + r + 0.05f;
            (NahRaster<int>, NahRaster<int>) FreiRaster()
            {
                var linien = new NahRaster<int>();
                var nr = 0;
                foreach (var (linie, halb) in baeume.Sperren.Concat(gehwege.Select(g => ((IReadOnlyList<Vector2>)g.Linie, g.Halb))))
                {
                    var (min, max) = Rahmen(linie);
                    var w = halb + FreiRMax + 0.02f;
                    linien.Hinzu(nr++, min.X - w, min.Y - w, max.X + w, max.Y + w);
                }
                foreach (var lp in laternenPlaetze)
                {
                    var w = Nachtlicht.AbstandModell + FreiRMax + 0.05f;
                    linien.Hinzu(nr++, MathF.Min(lp.A.X, lp.B.X) - w, MathF.Min(lp.A.Y, lp.B.Y) - w, MathF.Max(lp.A.X, lp.B.X) + w, MathF.Max(lp.A.Y, lp.B.Y) + w);
                }
                var punkte = new NahRaster<int>();
                nr = 0;
                foreach (var o in orte)
                {
                    var w = 0.45f + FreiRMax;
                    punkte.Hinzu(nr++, o.StehX - w, o.StehZ - w, o.StehX + w, o.StehZ + w);
                }
                foreach (var k in haeuserKaesten)
                {
                    var w = FreiRMax + 0.03f;
                    punkte.Hinzu(nr++, k.Min.X - w, k.Min.Z - w, k.Max.X + w, k.Max.Z + w);
                }
                foreach (var h in hindernisseVorFest)
                {
                    punkte.Hinzu(nr++, (float)h.Item1 - FreiRMax, (float)h.Item2 - FreiRMax, (float)h.Item3 + FreiRMax, (float)h.Item4 + FreiRMax);
                }
                foreach (var fp in festPlaetze)
                {
                    var w = fp.R + FreiRMax + 0.05f;
                    punkte.Hinzu(nr++, fp.P.X - w, fp.P.Y - w, fp.P.X + w, fp.P.Y + w);
                }
                foreach (var k in andreaskreuze)
                {
                    var w = Bahnuebergang.Grundriss + FreiRMax + 0.05f;
                    punkte.Hinzu(nr++, k.Fuss.X - w, k.Fuss.Y - w, k.Fuss.X + w, k.Fuss.Y + w);
                }
                return (linien, punkte);
            }
            bool PunktFrei(int i, float x, float z, float r)
            {
                if (i < orte.Count)
                {
                    var o = orte[i];
                    return Vector2.Distance(new Vector2(x, z), new Vector2(o.StehX, o.StehZ)) >= 0.45f + r;
                }
                i -= orte.Count;
                if (i < haeuserKaesten.Count)
                {
                    var k = haeuserKaesten[i];
                    return x + r < k.Min.X - 0.03f || x - r > k.Max.X + 0.03f || z + r < k.Min.Z - 0.03f || z - r > k.Max.Z + 0.03f;
                }
                i -= haeuserKaesten.Count;
                if (i < hindernisseVorFest.Count)
                {
                    var h = hindernisseVorFest[i];
                    return x + r < h.Item1 || x - r > h.Item3 || z + r < h.Item2 || z - r > h.Item4;
                }
                i -= hindernisseVorFest.Count;
                if (i < festPlaetze.Count)
                {
                    var fp = festPlaetze[i];
                    return Vector2.Distance(fp.P, new Vector2(x, z)) >= fp.R + r + 0.05f;
                }
                i -= festPlaetze.Count;
                return Vector2.Distance(andreaskreuze[i].Fuss, new Vector2(x, z)) >= Bahnuebergang.Grundriss + r + 0.05f;
            }
            bool FreiGanz(float x, float z, float r) =>
                baeume.Sperren.All(sp => Kurve.Abstand(new Vector2(x, z), sp.Linie) >= sp.Halb + r + 0.02f)
                && gehwege.All(g => Kurve.Abstand(new Vector2(x, z), g.Linie) >= g.Halb + r + 0.02f)
                && orte.All(o => Vector2.Distance(new Vector2(x, z), new Vector2(o.StehX, o.StehZ)) >= 0.45f + r)
                && haeuserKaesten.All(k => x + r < k.Min.X - 0.03f || x - r > k.Max.X + 0.03f || z + r < k.Min.Z - 0.03f || z - r > k.Max.Z + 0.03f)
                && hindernisseVorFest.All(h => x + r < h.Item1 || x - r > h.Item3 || z + r < h.Item2 || z - r > h.Item4)
                && festPlaetze.All(fp => Vector2.Distance(fp.P, new Vector2(x, z)) >= fp.R + r + 0.05f)
                && laternenPlaetze.All(lp => Kurve.Abstand(new Vector2(x, z), [lp.A, lp.B]) >= Nachtlicht.AbstandModell + r + 0.05f)
                && andreaskreuze.All(k => Vector2.Distance(k.Fuss, new Vector2(x, z)) >= Bahnuebergang.Grundriss + r + 0.05f)
                && teiche.All(t => !Wasser.Drin(t.MitUfer, new Vector2(x, z)) && Wasser.RandAbstand(t.MitUfer, new Vector2(x, z)) >= r + 0.02f)
                // Welle A 0.66.0: Kleingruen nur, wo sein Fuss rundum hoechstens 0,02 von der Hoehe der Mitte abweicht
                && (gelaende is null || gelaende.Flach(x, z, r));
            var parks = landschaft.Where(t => Py.Gleich(t["typ"], "park")).Select(t => (Zahl(t, "x"), Zahl(t, "y"), Zahl(t, "w"), Zahl(t, "h")));
            // die Parkbaeume stehen nicht in den Stuecken (die bleiben wie in W6), sie kommen in NetzBauen dazu
            gruen = Gruen.Verteilen(grafik, breite, tiefe, parks, strassen, Frei, baeume.Gesetzt, beeteFest, KleingruenAn(stadt.Daten));
            // Bueschel, Steine, Beete und Parkbaeume sind Hindernisse des freien Gehens (wie die Fest-Deko, E2 P8-14);
            // ueber Gras geht die Figur
            hindernisse.AddRange(gruen.Hindernisse.Select(h => ((double)(h.X - h.R), (double)(h.Z - h.R), (double)(h.X + h.R), (double)(h.Z + h.R))));
            if (gelaende is not null)
            {
                Anheben(gelaende, gruen.Stuecke, 0, gruen.Stuecke.Count);
                Anheben(gelaende, gruen.Baeume, 0, gruen.Baeume.Count);
                for (var i = 0; i < gruen.Teile.Count; i++)
                {
                    gruen.Teile[i] = Anheben(gelaende, gruen.Teile[i]);
                }
            }
        }
        // Welle A 0.66.0 (Weisung P12-192): jedes Stueck ausserhalb der Haeuser (Baum, Katalog-Stueck, Effekt) steht auf der
        // Hoehe des Gelaendes an seiner Mitte und kippt nicht; ohne Gelaende nichts
        if (gelaende is not null)
        {
            var imHaus = new bool[stuecke.Count];
            foreach (var h in haeuser)
            {
                Array.Fill(imHaus, true, h.StueckeVon, Math.Min(h.StueckeBis, stuecke.Count) - h.StueckeVon);
            }
            for (var i = 0; i < stuecke.Count; i++)
            {
                if (!imHaus[i])
                {
                    Anheben(gelaende, stuecke, i, i + 1);
                }
            }
        }
        // Sammelliste 264 (Weisung P12-130 Frage 3 = c): Gebaeude faerben (stadtdaten.farben, ohne Feld nichts)
        if (grafik is not null)
        {
            FarbenAnwenden(stadt.Daten, grafik, stuecke, haeuser, umgefaerbt);
        }
        // Engine-Welle A 1.4.0: das Laufraster entsteht neben der Grundlage weiter (die Eingaben stehen jetzt fest, keine wird
        // danach geaendert); wer Laufraster liest, wartet darauf (der Reihe nach: sofort hier wie bisher)
        var laufraster = LaufrasterSpaeter(breite, tiefe, hindernisse, wasser, gleise, strassen, zugangswege, orte, gehwege, teiche, stege, inseln, [.. zebras, .. furten], gelaende);
        return new StadtSzene(teile, streifen, stuecke, grafik, orte, string.Join(";", signatur), breite, tiefe, laufraster, haeuser)
        {
            _vorrat = vorrat,
            GehWege = zugangswege,
            Bruecken = bruecken,
            BrueckenKaesten = brueckenKaesten,
            StrassenEnden = strassenEnden,
            Baeume = baeume.Gesetzt,
            BaumSperren = baeume.Sperren,
            Uebergaenge = uebergaenge,
            Andreaskreuze = andreaskreuze,
            Gleise = gleise,
            Fest = fest,
            FestGesetzt = festGesetzt,
            FestPlaetzeAlle = festPlaetze,
            FestTeileVon = festTeileVon,
            FestStueckeVon = festStueckeVon,
            Jahreszeit = jahreszeit,
            Spieltag = stadt.VerkehrTag ?? stand?.Tag ?? 1,
            GruenKlein = gruen,
            Strassen = strassen,
            LaternenSkizze = laternenSkizze,
            // W10 (Nico 3b): Fenster mit Licht (nicht die dunklen und nicht die an der Tuer in Wandfarbe)
            // W11: dazu das Zifferblatt der Bahnhofsuhr
            LichtTeile = alleFenster.Where(f => teile[f.Teil].Farbe != Farben.Fenster && teile[f.Teil].Farbe != f.Wand).Select(f => f.Teil)
                .Concat(Enumerable.Range(0, teile.Count).Where(i => teile[i].Farbe == UhrBlatt || teile[i].Farbe == AnzeigeSchrift)).ToHashSet(),
            Zierhaeuser = zierhaeuser,
            Tueren = tueren,
            OrtBloecke = ortBloecke,
            Umgefaerbt = umgefaerbt,
            Parks = parkFlaechen,
            Fluesse = [.. wasser],
            StadtteilNamen = Stadtbau.Stadtteile.Bereiche(stadt.Daten),
            Teiche = teiche,
            Stege = stege,
            Flaechen = flaechen,
            Eis = Wasser.EisAn(stadt.Daten),
            Zebras = zebras,
            Ampeln = ampeln,
            Haltestellen = haltestellen,
            Questgeber = Stadtbau.Questebene.DraussenListe(inhalt),
            Parkplaetze = parkplaetze,
            MitGehweg = gehwege.Count > 0,
            Gelaende = gelaende,
            LaternenOhnePlatzAuslassen = Stadtbau.Nachsicht.An,
        };
    }

    /// <summary>Phase 13 Welle 4a: Breite des Gehwegs je Seite einer Strasse (Feld "gehweg", fehlt = 0).</summary>
    public static float GehwegBreite(PyObjekt strasse) => Py.Hat(strasse, "gehweg") ? (float)Py.F(strasse["gehweg"]) : 0f;

    /// <summary>
    /// Welle K1 (A 0.44.0, Weisung P12-55): Wunschradius je Eckpunkt eines Flusses oder Gleises (Feld "radien", gleich viele
    /// Zahlen wie Punkte, nur aus dem Stadtbau-Werkzeug); ohne Feld null (dann gilt <see cref="Kurve.Radius"/> wie bisher).
    /// </summary>
    public static List<float>? Radien(PyObjekt linie) =>
        Py.Hat(linie, "radien") && linie["radien"] is List<object?> r ? [.. r.Select(w => (float)Py.F(w))] : null;

    /// <summary>Welle K1: der geglaettete Linienzug eines Flusses oder Gleises (Boegen mit dem Radius je Eckpunkt).</summary>
    public static List<Vector2> Linienzug(PyObjekt linie) => Kurve.Glatt(Vektoren(linie["punkte"]), Radien(linie));

    /// <summary>
    /// Welle K1: eine Strasse mit mehr als zwei Punkten (Kurve aus dem Stadtbau-Werkzeug, Feld "kurve") zeichnet das Spiel
    /// als ein Band mit Gehrung (wie Fluss und Gleis), damit zwischen den kurzen Stuecken kein Spalt bleibt; eine Strasse
    /// mit zwei Punkten (alle Strassen der Fassung 0) bleibt ein gerades Band wie bisher.
    /// </summary>
    public static bool AlsBand(IReadOnlyList<Vector2> strasse) => strasse.Count > 2;
}
