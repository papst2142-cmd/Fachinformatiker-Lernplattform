using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.56.0 (Sammelliste 362, Weisung P12-287, Nico 1a 2b 3a 4a 5a): Stadtteile Stueck fuer Stueck aneinandersetzen.
/// <list type="bullet">
/// <item>„Karte vergroessern“ (<see cref="KarteVergroessern"/>): neue Breite und Tiefe (ganze Plaetze bis 256, nie kleiner) und
/// der Platz (1 bis 16), auf dem die heutige Karte liegen soll; alles rueckt genau um die Lage dieses Platzes (Platz 1 = keine
/// Verschiebung). Die neue Flaeche ist Wiese. Ein Schritt.</item>
/// <item>„Stadtteil einsetzen“ (<see cref="StadtteilEinsetzen"/>): ein anderes Projekt (hoechstens 64 × 64) kommt ganz auf
/// einen freien Platz, alles um die Lage des Platzes verschoben, in einem Schritt. Vorschau und Schritt nutzen dieselbe
/// Rechnung (<see cref="EinsetzenPlanen"/>). Liegt auf dem Platz schon etwas, sind es zusammen mehr als 16 Huegel, hat ein
/// Modell denselben Namen mit anderen Teilen oder kennt das Werkzeug einen Teil nicht: nichts wird eingesetzt, der Grund
/// steht da (nie still). Ein Spielort, den die Karte schon hat, kommt als Gebaeude ohne Ort (mit seinem Innenraum); seine
/// Quests kommen nicht mit und stehen einzeln im gelben Hinweis. Der Paketplatz und die Uhr der Karte bleiben.</item>
/// </list>
/// Das eingesetzte Projekt wird nur gelesen.
/// </summary>
public sealed partial class Projekt
{
    /// <summary>Grund, warum die Karte nicht so vergroessert werden kann (null = geht).</summary>
    public string? VergroessernNicht(int breite, int hoehe, int platz)
    {
        if (NurLesen)
        {
            return Texte.NurLesen;
        }
        var (alt_b, alt_h) = Aussen.Groesse(AussenStand);
        if (!Plaetze.Seiten.Contains(breite) || !Plaetze.Seiten.Contains(hoehe))
        {
            return Texte.VergroessernGroesse;
        }
        if (breite < alt_b - 1e-9 || hoehe < alt_h - 1e-9)
        {
            return Texte.VergroessernKleiner;
        }
        if (breite <= alt_b + 1e-9 && hoehe <= alt_h + 1e-9)
        {
            return Texte.VergroessernGleich;
        }
        if (!Plaetze.Gibt(platz, breite, hoehe))
        {
            return Texte.PlatzFehlt(platz, Plaetze.Anzahl(breite, hoehe));
        }
        var (ox, oy) = Plaetze.Lage(platz, breite);
        if (ox + alt_b > breite + 1e-9 || oy + alt_h > hoehe + 1e-9)
        {
            return Texte.VergroessernPasstNicht(platz);
        }
        return KarteVerschieben.Unbekannt(AussenStand) is { Count: > 0 } u ? Texte.TeilUnbekannt(u[0]) : null;
    }

    /// <summary>Karte vergroessern (ein Schritt); null = vergroessert, sonst der Grund (nichts geaendert).</summary>
    public string? KarteVergroessern(int breite, int hoehe, int platz)
    {
        if (VergroessernNicht(breite, hoehe, platz) is { } grund)
        {
            return grund;
        }
        var (ox, oy) = Plaetze.Lage(platz, breite);
        AussenAendern(StadtOrt, Texte.KarteVergroessernSchritt(breite, hoehe, platz), a =>
        {
            KarteVerschieben.Alles(a, ox, oy);
            var welt = Aussen.Welt(a);
            Py.Setzen(welt, "breite", Bearbeitung.Zahl(breite));
            Py.Setzen(welt, "hoehe", Bearbeitung.Zahl(hoehe));
            return 0;
        }, ohneAutomatik: true);
        return null;
    }

    /// <summary>Ergebnis eines Einsetzens (oder seiner Vorschau): Grund = nichts eingesetzt; Hinweise gelb.</summary>
    public sealed record EinsetzenErgebnis(string? Grund, IReadOnlyList<string> Hinweise, int Platz, (int X, int Y) Ecke)
    {
        public bool Geht => Grund is null;
    }

    /// <summary>Vorschau: was „Stadtteil einsetzen“ mit diesem Projekt und Platz taete (aendert nichts).</summary>
    public EinsetzenErgebnis EinsetzenPruefen(Projekt quelle, int platz) => EinsetzenPlanen(quelle, platz).Ergebnis;

    /// <summary>Stadtteil einsetzen (ein Schritt). Bei einem Grund bleibt alles, wie es ist.</summary>
    public EinsetzenErgebnis StadtteilEinsetzen(Projekt quelle, int platz)
    {
        var (ergebnis, stand) = EinsetzenPlanen(quelle, platz);
        if (stand is null || NurLesen)
        {
            return ergebnis with { Grund = ergebnis.Grund ?? Texte.NurLesen };
        }
        AussenAendern(StadtOrt, Texte.EinsetzenSchritt(quelle.Name, platz), a =>
        {
            var neu = Aussen.TeileText(stand);
            foreach (var (pfad, wert) in Aussen.Teile(stand).ToList())
            {
                Aussen.TeilSetzen(a, pfad, Py.Kopie(wert), true);
            }
            foreach (var pfad in Aussen.TeileText(a).Keys.Where(k => !neu.ContainsKey(k)).ToList())
            {
                Aussen.TeilSetzen(a, pfad, null, false);
            }
            return 0;
        }, ohneAutomatik: true);
        return ergebnis;
    }

    /// <summary>Gleiche Rechnung fuer Vorschau und Schritt: Ergebnis und der neue Stand draussen (null bei einem Grund).</summary>
    internal (EinsetzenErgebnis Ergebnis, PyObjekt? Stand) EinsetzenPlanen(Projekt quelle, int platz)
    {
        var ziel = (PyObjekt)Py.Kopie(AussenStand)!;
        var (breite, hoehe) = Aussen.Groesse(ziel);
        var ecke = Plaetze.Gibt(platz, breite, hoehe) ? Plaetze.Lage(platz, breite) : (0, 0);
        EinsetzenErgebnis Nein(string grund) => new(grund, [], platz, ecke);
        if (NurLesen)
        {
            return (Nein(Texte.NurLesen), null);
        }
        if (ReferenceEquals(quelle, this) || string.Equals(quelle.Datei, Datei, StringComparison.Ordinal))
        {
            return (Nein(Texte.EinsetzenSichSelbst), null);
        }
        if (!Plaetze.Gibt(platz, breite, hoehe))
        {
            return (Nein(Texte.PlatzFehlt(platz, Plaetze.Anzahl(breite, hoehe))), null);
        }
        var q = (PyObjekt)Py.Kopie(quelle.AussenStand)!;
        var (qb, qh) = Aussen.Groesse(q);
        if (qb > Plaetze.Seite + 1e-9 || qh > Plaetze.Seite + 1e-9)
        {
            return (Nein(Texte.EinsetzenZuGross(quelle.Name, qb, qh)), null);
        }
        if (KarteVerschieben.Unbekannt(q).Concat(KarteVerschieben.Unbekannt(ziel)).FirstOrDefault() is { } unbekannt)
        {
            return (Nein(Texte.TeilUnbekannt(unbekannt)), null);
        }
        if (Belegt(ziel, ecke) is { } belegt)
        {
            return (Nein(Texte.PlatzBelegt(platz, belegt)), null);
        }
        var huegel = ListeOderLeer(Get(Aussen.Stadtdaten(ziel), Gelaende.Abschnitt) as PyObjekt ?? [], "huegel").Count
            + ListeOderLeer(Get(Aussen.Stadtdaten(q), Gelaende.Abschnitt) as PyObjekt ?? [], "huegel").Count;
        if (huegel > Gelaende.HuegelHoechstens)
        {
            return (Nein(Texte.EinsetzenHuegel(huegel)), null);
        }
        var hinweise = new List<string>();
        // Haeuser (Spielorte und Gebaeude ohne Ort) wie beim Einfuegen aus der Zwischenablage: mit Innenraum, Etagen, Farben
        var haeuser = AussenBearbeitung.Alle(quelle.AussenStand).Select(t => t.Stelle).Where(s => s.Art is AussenArt.Haus or AussenArt.Gebaeude).ToList();
        var doppelt = haeuser.Where(s => s.Art == AussenArt.Haus && AussenBearbeitung.Ort(ziel, s.Ort) is not null).Select(s => s.Ort).ToHashSet(StringComparer.Ordinal);
        var ablage = haeuser.Count == 0 ? null : AussenBearbeitung.InZwischenablage(quelle, haeuser, out _);
        if (haeuser.Count > 0 && (ablage is null || ablage.Stuecke.Count != haeuser.Count))
        {
            return (Nein(Texte.EinsetzenHausFehlt), null);
        }
        KarteVerschieben.Alles(q, ecke.Item1, ecke.Item2);
        if (Zusammenfuehren(ziel, q, doppelt, hinweise) is { } grund)
        {
            return (Nein(grund), null);
        }
        foreach (var st in ablage?.Stuecke.OfType<PyObjekt>() ?? [])
        {
            var (_, hausGrund, hinweis) = AussenBearbeitung.HausEinsetzen(ziel, st, ecke.Item1, ecke.Item2, Daten.Fassung);
            if (hausGrund is not null)
            {
                return (Nein(hausGrund), null);
            }
            if (hinweis is not null)
            {
                hinweise.Add(hinweis);
            }
        }
        // Paketplatz: genau einer je Stadt; der der Karte bleibt
        if (doppelt.Contains("zuhause"))
        {
            hinweise.Add(Texte.EinsetzenPaketplatz);
        }
        else if (haeuser.Any(s => s.Art == AussenArt.Haus && s.Ort == "zuhause")
            && Get(Objekt(Get(Aussen.Stadtdaten(quelle.AussenStand), "darstellung") ?? new PyObjekt()), "paket_platz") is { } paket)
        {
            Py.Setzen(Objekt(Aussen.Stadtdaten(ziel)["darstellung"]), "paket_platz", Py.Kopie(paket));
        }
        // Innenraeume der Stadt 1, die die Quelle geaendert hat und die hier Spielort werden: es gilt der der Stadt 1
        foreach (var ort in quelle.GeaenderteOrte.Select(o => Etagen.Teilen(o).Ort).Distinct().Where(o => !IstBau(o) && !doppelt.Contains(o)
            && haeuser.Any(s => s.Art == AussenArt.Haus && s.Ort == o) && Leer is not null))
        {
            hinweise.Add(Texte.EinsetzenInnenraumStadt1(ort));
        }
        if (Py.Gleich(Get(Objekt(Get(Aussen.Stadtdaten(q), "darstellung") ?? new PyObjekt()), "kleingruen"), false)
            != Py.Gleich(Get(Objekt(Get(Aussen.Stadtdaten(ziel), "darstellung") ?? new PyObjekt()), "kleingruen"), false))
        {
            hinweise.Add(Texte.EinsetzenKleingruen);
        }
        return (new EinsetzenErgebnis(null, hinweise, platz, ecke), ziel);
    }

    /// <summary>Liegt auf dem Platz schon etwas? Name der Art des ersten Stuecks, sonst null.</summary>
    private static string? Belegt(PyObjekt ziel, (int X, int Y) ecke)
    {
        var (x1, y1, x2, y2) = (ecke.X, ecke.Y, ecke.X + Plaetze.Seite, ecke.Y + Plaetze.Seite);
        bool Innen(double x, double y) => x > x1 + 1e-6 && x < x2 - 1e-6 && y > y1 + 1e-6 && y < y2 - 1e-6;
        foreach (var (s, x, y) in AussenBearbeitung.Alle(ziel))
        {
            if (Innen(x, y))
            {
                return Texte.ArtName(s.Art);
            }
            if (s.Art is AussenArt.Haus or AussenArt.Gebaeude)
            {
                var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(ziel, s);
                if (hx < x2 && hx + hw > x1 && hy < y2 && hy + hh > y1)
                {
                    return Texte.ArtName(s.Art);
                }
            }
        }
        // Linien (Strassen, Bahn, Fluss, Fusswege, Teiche, Tuerwege): ein Stueck, das ueber den Platz geht
        var linien = new List<List<Vector2>>();
        foreach (var l in Aussen.Landschaft(ziel).OfType<PyObjekt>())
        {
            linien.Add(AussenBearbeitung.Linie(Get(l, "punkte")));
        }
        var sd = Aussen.Stadtdaten(ziel);
        linien.AddRange(ListeOderLeer(sd, "fusswege").OfType<PyObjekt>().Select(f => AussenBearbeitung.Linie(Get(f, "punkte"))));
        linien.AddRange(ListeOderLeer(Get(sd, "wasser") as PyObjekt ?? [], "teiche").OfType<PyObjekt>().Select(t => AussenBearbeitung.Linie(Get(t, "punkte"))));
        foreach (var linie in linien)
        {
            for (var i = 0; i + 1 < linie.Count; i++)
            {
                if (StreckeInnen(linie[i], linie[i + 1], x1, y1, x2, y2))
                {
                    return Texte.EinsetzenLinie;
                }
            }
            if (linie.Count == 1 && Innen(linie[0].X, linie[0].Y))
            {
                return Texte.EinsetzenLinie;
            }
        }
        foreach (var e in ListeOderLeer(Get(sd, Gelaende.Abschnitt) as PyObjekt ?? [], "gemalt").OfType<List<object?>>())
        {
            if (e is [long i, long j, _] && i * Gelaende.Raster >= x1 && i * Gelaende.Raster < x2 && j * Gelaende.Raster >= y1 && j * Gelaende.Raster < y2)
            {
                return Texte.ArtName(AussenArt.Hoehe);
            }
        }
        foreach (var b in Stadtteile.Bereiche(sd))
        {
            if (b.X1 < x2 && b.X2 > x1 && b.Z1 < y2 && b.Z2 > y1)
            {
                return Texte.StadtteilNamen;
            }
        }
        return null;
    }

    /// <summary>Geht die Strecke a-b durch das Innere des Rechtecks (Raender zaehlen nicht)?</summary>
    private static bool StreckeInnen(Vector2 a, Vector2 b, double x1, double y1, double x2, double y2)
    {
        // Liang-Barsky auf das um 1e-6 verkleinerte Rechteck
        double t0 = 0, t1 = 1, dx = b.X - a.X, dy = b.Y - a.Y;
        var e = 1e-6;
        foreach (var (p, q) in new[] { (-dx, a.X - (x1 + e)), (dx, x2 - e - a.X), (-dy, a.Y - (y1 + e)), (dy, y2 - e - a.Y) })
        {
            if (Math.Abs(p) < 1e-12)
            {
                if (q < 0)
                {
                    return false;
                }
                continue;
            }
            var r = q / p;
            if (p < 0)
            {
                t0 = Math.Max(t0, r);
            }
            else
            {
                t1 = Math.Min(t1, r);
            }
            if (t0 > t1)
            {
                return false;
            }
        }
        return true;
    }

    /// <summary>Abschnitte, die mit den Haeusern kommen (ueber <see cref="AussenBearbeitung.HausEinsetzen"/>).</summary>
    private static readonly string[] HausAbschnitte = ["neue_orte", "zugaenge", "zugaenge_neu", "oeffnungszeiten", "neue_modelle", "bauteile", "farben"];

    /// <summary>Darstellung, die mit den Haeusern kommt.</summary>
    private static readonly string[] HausDarstellung = ["hausdreh", "hausgrad", "modell_tuer", "zierwege"];

    /// <summary>
    /// Alles ausser den Haeusern aus <paramref name="q"/> (schon verschoben) an <paramref name="ziel"/> anhaengen. Quests neuer
    /// Kennung, die es im Ziel schon gibt, bekommen die naechste freie Kennung (Hinweis); Quests an einem doppelten Spielort
    /// kommen nicht mit (Hinweis je Quest). Rueckgabe: Grund oder null.
    /// </summary>
    private string? Zusammenfuehren(PyObjekt ziel, PyObjekt q, IReadOnlySet<string> doppelt, List<string> hinweise)
    {
        var zl = Aussen.Landschaft(ziel);
        zl.AddRange(Aussen.Landschaft(q).OfType<PyObjekt>().Where(l => !Py.Gleich(l["typ"], "gebaeude")).Select(l => Py.Kopie(l)));
        var zsd = Aussen.Stadtdaten(ziel);
        var qsd = Aussen.Stadtdaten(q);
        foreach (var abschnitt in new[] { "fusswege", "haltestellen", "parkplaetze", "ampeln", "zebrastreifen", "katalog", Stadtteile.AbschnittNamen })
        {
            AnhaengenListe(zsd, qsd, abschnitt);
        }
        if (Get(qsd, "wasser") is PyObjekt qw)
        {
            var zw = Aussen.WasserAnlegen(ziel);
            foreach (var feld in new[] { "teiche", "stege" })
            {
                AnhaengenListe(zw, qw, feld);
            }
        }
        if (Get(qsd, Gelaende.Abschnitt) is PyObjekt qg)
        {
            if (Get(zsd, Gelaende.Abschnitt) is not PyObjekt zg)
            {
                zg = new PyObjekt();
                Py.Setzen(zsd, Gelaende.Abschnitt, zg);
            }
            foreach (var feld in new[] { "huegel", "gemalt" })
            {
                AnhaengenListe(zg, qg, feld);
            }
        }
        if (QuestsZusammenfuehren(zsd, qsd, doppelt, hinweise) is { } grund)
        {
            return grund;
        }
        ((List<object?>)ziel["laternen"]!).AddRange((q["laternen"] as List<object?> ?? []).Select(Py.Kopie));
        ((List<object?>)ziel["beete"]!).AddRange((q["beete"] as List<object?> ?? []).Select(Py.Kopie));
        if (Get(q, LaternenAutomatik.Teil) is PyObjekt qa)
        {
            if (Get(ziel, LaternenAutomatik.Teil) is not PyObjekt za)
            {
                za = new PyObjekt();
                Py.Setzen(ziel, LaternenAutomatik.Teil, za);
            }
            foreach (var (feld, liste) in qa)
            {
                if (Get(za, feld) is not List<object?> zl2)
                {
                    zl2 = [];
                    Py.Setzen(za, feld, zl2);
                }
                zl2.AddRange((liste as List<object?> ?? []).Select(Py.Kopie));
            }
        }
        if (Get(q, AussenBearbeitung.NotizTeil) is List<object?> qn && qn.Count > 0)
        {
            if (Get(ziel, AussenBearbeitung.NotizTeil) is not List<object?> zn)
            {
                zn = [];
                Py.Setzen(ziel, AussenBearbeitung.NotizTeil, zn);
            }
            zn.AddRange(qn.Select(Py.Kopie));
        }
        // Abschnitte, die nur mit den Haeusern kommen, und Uhr/Raster der Karte bleiben; unbekannte Abschnitte: Grund
        foreach (var (k, _) in qsd)
        {
            if (k is "fusswege" or "haltestellen" or "parkplaetze" or "ampeln" or "zebrastreifen" or "katalog" or "wasser" or "uhr" or "raster" or "darstellung" or "_hinweis"
                or Gelaende.Abschnitt or Stadtteile.AbschnittNamen or Questebene.AbschnittQuests or Questebene.AbschnittQuestgeber
                // S 0.57.0: Kennung und Name eines Pakets bleiben die des Ziels (kein Teil der Haeuser)
                or StadtPaket.Abschnitt || HausAbschnitte.Contains(k))
            {
                continue;
            }
            return Texte.TeilUnbekannt("stadtdaten/" + k);
        }
        return null;
    }

    private static void AnhaengenListe(PyObjekt ziel, PyObjekt quelle, string feld)
    {
        if (Get(quelle, feld) is not List<object?> { Count: > 0 } q)
        {
            return;
        }
        if (Get(ziel, feld) is not List<object?> z)
        {
            z = [];
            Py.Setzen(ziel, feld, z);
        }
        z.AddRange(q.Select(Py.Kopie));
    }

    /// <summary>Quests und Questgeber der Quelle anhaengen (siehe <see cref="Zusammenfuehren"/>).</summary>
    private string? QuestsZusammenfuehren(PyObjekt zsd, PyObjekt qsd, IReadOnlySet<string> doppelt, List<string> hinweise)
    {
        var qGeber = ListeOderLeer(qsd, Questebene.AbschnittQuestgeber).OfType<PyObjekt>().Select(e => (PyObjekt)Py.Kopie(e)!).ToList();
        var qQuests = ListeOderLeer(qsd, Questebene.AbschnittQuests).OfType<PyObjekt>().Select(e => (PyObjekt)Py.Kopie(e)!).ToList();
        if (qGeber.Count == 0 && qQuests.Count == 0)
        {
            return null;
        }
        // Questgeber an einem doppelten Spielort (drinnen): kommt nicht mit, seine Quests auch nicht
        var weg = qGeber.Where(g => Get(g, "platz") is PyObjekt p && !Py.Wahr(Get(p, "draussen")) && Get(p, "ort") is string o && doppelt.Contains(o))
            .Select(g => Py.Str(g["id"])).ToHashSet(StringComparer.Ordinal);
        var zGeberIds = ListeOderLeer(zsd, Questebene.AbschnittQuestgeber).OfType<PyObjekt>().Select(e => Py.Str(e["id"])).ToHashSet(StringComparer.Ordinal);
        var umGeber = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var g in qGeber.Where(g => !weg.Contains(Py.Str(g["id"]))))
        {
            var id = Py.Str(g["id"]);
            if (zGeberIds.Contains(id))
            {
                var n = 1;
                while (zGeberIds.Contains(QuestgeberDaten.Praefix + n.ToString(CultureInfo.InvariantCulture)) || qGeber.Any(x => Py.Gleich(x["id"], QuestgeberDaten.Praefix + n.ToString(CultureInfo.InvariantCulture))))
                {
                    n++;
                }
                umGeber[id] = QuestgeberDaten.Praefix + n.ToString(CultureInfo.InvariantCulture);
                Py.Setzen(g, "id", umGeber[id]);
                hinweise.Add(Texte.EinsetzenKennungNeu(id, umGeber[id]));
            }
            zGeberIds.Add(Py.Str(g["id"]));
        }
        var inhalt = Daten.Inhalt;
        var zQuests = ListeOderLeer(zsd, Questebene.AbschnittQuests).OfType<PyObjekt>().ToList();
        var zIds = zQuests.Select(e => Py.Str(e["id"])).ToHashSet(StringComparer.Ordinal);
        var umQuest = new Dictionary<string, string>(StringComparer.Ordinal);
        var neu = new List<PyObjekt>();
        foreach (var quest in qQuests)
        {
            var id = Py.Str(quest["id"]);
            var neueQuest = id.StartsWith(QuestDaten.NeuPraefix, StringComparison.Ordinal);
            var geber = Get(quest, "questgeber") as string;
            var arbeitsort = Get(quest, Questebene.FeldArbeitsort) is PyObjekt ao ? Get(ao, "ort") as string : null;
            if ((geber is not null && weg.Contains(geber)) || (arbeitsort is not null && doppelt.Contains(arbeitsort)))
            {
                hinweise.Add(Texte.EinsetzenQuestNicht(Py.Str(Get(quest, "titel") ?? id)));
                continue;
            }
            if (!neueQuest)
            {
                // Umhaengen einer Quest der Stadt 1: gibt es sie im Ziel anders umgehaengt, ist das ein Grund
                if (zQuests.FirstOrDefault(z => Py.Gleich(z["id"], id)) is { } da && PythonJson.Standard(da) != PythonJson.Standard(quest))
                {
                    return Texte.EinsetzenQuestDoppelt(id);
                }
                if (zIds.Contains(id))
                {
                    continue;
                }
            }
            else if (zIds.Contains(id))
            {
                var probe = new PyObjekt { { Questebene.AbschnittQuests, new List<object?>([.. zQuests, .. neu, .. qQuests]) } };
                umQuest[id] = QuestDaten.NeueKennung(inhalt, probe);
                hinweise.Add(Texte.EinsetzenKennungNeu(id, umQuest[id]));
                Py.Setzen(quest, "id", umQuest[id]);
            }
            if (geber is not null && umGeber.TryGetValue(geber, out var g2))
            {
                Py.Setzen(quest, "questgeber", g2);
            }
            zIds.Add(Py.Str(quest["id"]));
            neu.Add(quest);
        }
        foreach (var quest in neu)
        {
            if (Get(quest, "nach") is string nach && umQuest.TryGetValue(nach, out var n2))
            {
                Py.Setzen(quest, "nach", n2);
            }
        }
        if (neu.Count > 0)
        {
            AnhaengenListe(zsd, new PyObjekt { { Questebene.AbschnittQuests, new List<object?>(neu) } }, Questebene.AbschnittQuests);
        }
        var geberNeu = qGeber.Where(g => !weg.Contains(Py.Str(g["id"]))).ToList();
        if (geberNeu.Count > 0)
        {
            AnhaengenListe(zsd, new PyObjekt { { Questebene.AbschnittQuestgeber, new List<object?>(geberNeu) } }, Questebene.AbschnittQuestgeber);
        }
        return null;
    }

    /// <summary>Groesse der Karte im Text der Teile (Stand <c>"karte/welt": [{"breite": …</c>, Verlauf ohne Liste): Formatnummer 5, wenn eine Karte ueber 64 darin vorkommt.</summary>
    [GeneratedRegex("\"karte/welt\": \\[?\\{\"breite\": ([0-9.]+), \"hoehe\": ([0-9.]+)", RegexOptions.CultureInvariant)]
    private static partial Regex WeltImVerlauf();

    /// <summary>Kommt im Text (Stand oder Verlauf) eine Karte groesser als 64 vor?</summary>
    internal static bool GrosseKarteImText(string text) =>
        WeltImVerlauf().Matches(text).Any(m => double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) > Stadtteile.AltesFormatBis
            || double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) > Stadtteile.AltesFormatBis);
}
