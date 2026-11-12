using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.45.0 (Weisung P12-212 Punkt 4, Sammelliste 318 = a): „Als Spielort festlegen“. Nico baut die erste Stadt als leere
/// Stadt mit eigenen Gebaeuden; ein Gebaeude ohne Ort wird hier zu einem Ort der Stadt 1. Das Aussehen bleibt Nicos Gebaeude
/// (Modell, Lage, Drehung, Tuer, Weg, Farben); alles andere kommt ueber die Kennung aus Stadt 1 (Fassung 0): Name, Rolle,
/// Kunde, Aufgaben, Geschichte, Oeffnungszeiten und der Innenraum (<see cref="InnenFuer"/>). Nur fuer die erste Stadt (Nico:
/// bei spaeteren Staedten baut man alles selbst).
/// <list type="bullet">
/// <item>Ein Schritt im Verlauf (Rueckgaengig geht). Das Gebaeude braucht eine Tuer (erst „Tür dazu“ oder „Tür setzen“).</item>
/// <item>Ort der Karte (orte.json) kommt nach <c>karte.orte</c> mit dem Modell in <c>karte.modelle</c>, ein neuer Ort (Laeden,
/// Bank, Amt) nach <c>stadtdaten.neue_orte</c> mit dem Modell in <c>stadtdaten.neue_modelle</c> (dort sucht es das Spiel, wie
/// beim Tausch des Aussehens). Reihenfolge wie in Stadt 1.</item>
/// <item>Tuer und Weg kommen nach <c>stadtdaten.zugaenge</c>, die Drehung nach <c>darstellung.hausdreh</c>, eine Tuer im Modell
/// (<c>modell_tuer</c> am Zierweg) nach <c>darstellung.modell_tuer</c> = 0.</item>
/// <item>Pruefung (<see cref="Pruefen"/>): rot, solange in einer Stadt mit Spielorten noch ein Ort der Stadt 1 fehlt.</item>
/// </list>
/// </summary>
public static class Spielort
{
    /// <summary>Orte der Stadt 1 in der Reihenfolge der Fassung: erst die Karte, dann die neuen Orte (Neu = true).</summary>
    public static List<(PyObjekt Ort, bool Neu)> Stadt1Orte(StadtFassung stadt1) =>
        [.. ListeOderLeer(stadt1.Karte, "orte").OfType<PyObjekt>().Select(o => (o, false))
            .Concat(ListeOderLeer(stadt1.Stadtdaten, "neue_orte").OfType<PyObjekt>().Select(o => (o, true)))];

    /// <summary>
    /// Weisung P12-214 (Nico: „Es sollte ja das Stellwerk werden und Stellwerk Talheim Nord heissen“): Orte der Stadt 1 mit
    /// neuem Namen und dem Modell, dessen Aussehen sie bekommen sollen. Die Kennung bleibt (Aufgaben, Kunden, Spielstand).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, (string Name, string Modell)> Neu = new Dictionary<string, (string, string)>(StringComparer.Ordinal)
    {
        ["talheim_nord"] = ("Stellwerk Talheim-Nord", "stellwerk"),
    };

    /// <summary>Orte der Stadt 1, die in dieser Stadt noch fehlen (Reihenfolge der Stadt 1).</summary>
    public static List<PyObjekt> Fehlende(PyObjekt aussen, StadtFassung stadt1)
    {
        var da = AussenBearbeitung.Orte(aussen).Select(o => Py.Str(o["id"])).ToHashSet(StringComparer.Ordinal);
        return [.. Stadt1Orte(stadt1).Select(x => x.Ort).Where(o => !da.Contains(Py.Str(o["id"])))];
    }

    /// <summary>
    /// Fehlende Orte fuer die Liste an Gebaeude <paramref name="s"/>: ein Ort, der dieses Aussehen bekommen soll (<see cref="Neu"/>,
    /// z. B. Stellwerk = Talheim-Nord), steht zuerst; sonst Reihenfolge der Stadt 1.
    /// </summary>
    public static List<PyObjekt> FehlendeFuer(PyObjekt aussen, AussenStelle s, StadtFassung stadt1)
    {
        var alle = Fehlende(aussen, stadt1);
        if (Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is not PyObjekt g || !Py.Gleich(g["typ"], "gebaeude"))
        {
            return alle;
        }
        var grund = GebaeudeTuer.Teile(aussen, Py.Str(g["modell"])).Grund;
        return [.. alle.OrderBy(o => Neu.TryGetValue(Py.Str(o["id"]), out var n) && n.Modell == grund ? 0 : 1)];
    }

    /// <summary>Wird Ort <paramref name="ort"/> fuer Gebaeude <paramref name="s"/> vorgeschlagen (gleiches Aussehen wie gewuenscht)?</summary>
    public static bool Vorschlag(PyObjekt aussen, AussenStelle s, PyObjekt ort) =>
        Neu.TryGetValue(Py.Str(ort["id"]), out var n) && Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is PyObjekt g
        && Py.Gleich(g["typ"], "gebaeude") && GebaeudeTuer.Teile(aussen, Py.Str(g["modell"])).Grund == n.Modell;

    /// <summary>Hat die Stadt schon Spielorte (dann gilt die Pruefung „Spielort fehlt“)?</summary>
    public static bool HatSpielorte(PyObjekt aussen) => AussenBearbeitung.Orte(aussen).Any();

    /// <summary>Zeigt die Auswahl den Knopf? Nur an einem Gebaeude ohne Ort, solange ein Ort der Stadt 1 fehlt.</summary>
    public static bool KnopfZeigen(PyObjekt aussen, AussenStelle s, StadtFassung stadt1) =>
        s.Art == AussenArt.Gebaeude && Fehlende(aussen, stadt1).Count > 0;

    /// <summary>Warum dieses Gebaeude kein Spielort werden kann (null = geht).</summary>
    public static string? FestlegenNicht(PyObjekt aussen, AussenStelle s, StadtFassung stadt1)
    {
        if (s.Art != AussenArt.Gebaeude || Aussen.Landschaft(aussen).ElementAtOrDefault(s.Nummer) is not PyObjekt g || !Py.Gleich(g["typ"], "gebaeude"))
        {
            return Texte.SpielortNurGebaeude;
        }
        var modell = Py.Str(g["modell"]);
        if (Bahnsteig.Nur(Get(AussenBearbeitung.Modelle(aussen), modell)))
        {
            return Texte.SpielortNurGebaeude;
        }
        if (Aussen.Bauten(aussen)[modell] is PyObjekt)
        {
            return Texte.SpielortEigenerInnenraum;
        }
        if (Fehlende(aussen, stadt1).Count == 0)
        {
            return Texte.SpielortAlleDa;
        }
        return Blueprint.ZierwegVon(aussen, s) is null ? Texte.SpielortOhneTuer : null;
    }

    /// <summary>
    /// Gebaeude <paramref name="s"/> wird der Ort <paramref name="id"/> der Stadt 1. Rueckgabe: Grund (dann bleibt alles) oder null.
    /// </summary>
    public static string? Festlegen(PyObjekt aussen, AussenStelle s, string id, StadtFassung stadt1)
    {
        if (FestlegenNicht(aussen, s, stadt1) is { } nicht)
        {
            return nicht;
        }
        var vorlage = Stadt1Orte(stadt1).FirstOrDefault(x => Py.Gleich(x.Ort["id"], id));
        if (vorlage.Ort is null)
        {
            return Texte.SpielortUnbekannt;
        }
        if (AussenBearbeitung.Ort(aussen, id) is not null)
        {
            return Texte.SpielortSchonDa;
        }
        var land = Aussen.Landschaft(aussen);
        var g = Objekt(land[s.Nummer]);
        var modell = Py.Str(g["modell"]);
        var dreh = Hausdrehung.Von(g);
        var zier = Blueprint.ZierwegVon(aussen, s)!;
        var (hx, hy, hw, hh) = AussenBearbeitung.HausFlaeche(aussen, s);
        var sd = Aussen.Stadtdaten(aussen);

        // Modell dorthin, wo das Spiel es fuer diesen Ort sucht (wie beim Tausch des Aussehens)
        var teile = Get(AussenBearbeitung.Modelle(aussen), modell);
        var ziel = vorlage.Neu ? Abschnitt(sd, "neue_modelle") : Abschnitt(Aussen.Karte(aussen), "modelle");
        if (Get(ziel, modell) is null)
        {
            Py.Setzen(ziel, modell, Py.Kopie(teile));
        }

        // Ort: Daten der Stadt 1 ueber die Kennung, Aussehen von Nicos Gebaeude
        var ort = (PyObjekt)Py.Kopie(vorlage.Ort)!;
        if (Neu.TryGetValue(id, out var neu))
        {
            Py.Setzen(ort, "name", neu.Name);
        }
        Py.Setzen(ort, "x", Py.Kopie(g["x"]));
        Py.Setzen(ort, "y", Py.Kopie(g["y"]));
        Py.Setzen(ort, "modell", modell);
        Py.Setzen(ort, "schild", new List<object?> { Bearbeitung.Zahl(Math.Round(hx + (hw / 2), 2)), Bearbeitung.Zahl(Math.Round(hy + (hh / 2), 2)) });
        var liste = vorlage.Neu ? ListeAnlegen(sd, "neue_orte") : ListeAnlegen(Aussen.Karte(aussen), "orte");
        var reihe = Stadt1Orte(stadt1).Where(x => x.Neu == vorlage.Neu).Select(x => Py.Str(x.Ort["id"])).ToList();
        var nr = reihe.IndexOf(id);
        var stelle = liste.FindIndex(o => o is PyObjekt p && reihe.IndexOf(Py.Str(p["id"])) is var k && (k < 0 || k > nr));
        liste.Insert(stelle < 0 ? liste.Count : stelle, ort);

        // Tuer und Weg, Tuer im Modell, Drehung, Oeffnungszeit, Farben
        var zugang = new PyObjekt { { "tuer", Py.Kopie(zier["tuer"]) }, { "weg", Py.Kopie(zier["weg"]) } };
        if (Get(zier, "radien") is { } radien)
        {
            Py.Setzen(zugang, "radien", Py.Kopie(radien));
        }
        Py.Setzen(Abschnitt(sd, "zugaenge"), id, zugang);
        var darstellung = Abschnitt(sd, "darstellung");
        if (Py.Wahr(Get(zier, "modell_tuer")))
        {
            Py.Setzen(Abschnitt(darstellung, "modell_tuer"), id, 0L);
        }
        if (dreh != 0)
        {
            Py.Setzen(Abschnitt(darstellung, Hausdrehung.Abschnitt), id, (long)dreh);
        }
        if (Get(Objekt(Get(stadt1.Stadtdaten, "oeffnungszeiten") ?? new PyObjekt()), id) is { } zeit)
        {
            Py.Setzen(Abschnitt(sd, "oeffnungszeiten"), id, Py.Kopie(zeit));
        }
        if (Get(sd, "farben") is PyObjekt farben && Get(farben, modell) is { } f
            && !land.OfType<PyObjekt>().Any(t => !ReferenceEquals(t, g) && Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], modell)))
        {
            farben.RemoveAll(p => p.Key == modell);
            Py.Setzen(farben, id, f);
        }

        // Gebaeude ohne Ort und sein Zierweg fallen weg (jetzt ist es der Ort)
        if (Get(darstellung, "zierwege") is List<object?> zw)
        {
            zw.RemoveAll(e => ReferenceEquals(e, zier));
        }
        land.RemoveAt(s.Nummer);
        return null;
    }

    /// <summary>Name eines Orts der Stadt 1 (fuer die Liste und die Pruefung).</summary>
    public static string Name(PyObjekt ort) => Neu.TryGetValue(Py.Str(ort["id"]), out var neu) ? neu.Name : Py.Str(Get(ort, "name") ?? ort["id"]);

    /// <summary>
    /// Pruefung: in einer leeren Stadt mit Spielorten (Nicos erste Stadt) fehlt noch ein Ort der Stadt 1 = rot, ein Fund mit der
    /// Liste. Ohne Spielorte (jede andere leere Stadt) und in Stadt 1 nichts.
    /// </summary>
    public static List<StadtbauFund> Pruefen(PyObjekt aussen, StadtFassung stadt1)
    {
        if (!HatSpielorte(aussen) || Fehlende(aussen, stadt1) is not { Count: > 0 } fehlen)
        {
            return [];
        }
        return [new StadtbauFund(Texte.SpielortFehlt(fehlen.Select(Name).ToList()), Texte.SpielortFehltTipp, true)];
    }

    /// <summary>
    /// Innenraeume der Stadt 1 fuer die Spielorte dieser Stadt (ueber die Kennung): je Ort sein Eintrag und seine Teile in den
    /// Zusatz-Abschnitten. Nur fuer eine leere Stadt mit Spielorten; die Basis selbst hat keine Innenraeume.
    /// </summary>
    public static PyObjekt InnenFuer(PyObjekt aussen, PyObjekt stadt1Innen)
    {
        // Kennung und Ansicht (der Lokschuppen heisst innen „serverfarm“)
        var ids = AussenBearbeitung.Orte(aussen).SelectMany(o => new[] { Py.Str(o["id"]), Py.Str(Get(o, "ansicht") ?? "") }).ToHashSet(StringComparer.Ordinal);
        var innen = new PyObjekt();
        foreach (var (k, v) in stadt1Innen)
        {
            if (k.StartsWith('_'))
            {
                continue;
            }
            if (k.StartsWith("zusatz", StringComparison.Ordinal) && v is PyObjekt z)
            {
                var teil = new PyObjekt();
                foreach (var (o, w) in z)
                {
                    // "buero" gehoert zur Bitweiche bzw. zur eigenen Firma (Gewerbehof)
                    if (ids.Contains(o) || (o == "buero" && (ids.Contains("bitweiche") || ids.Contains("gewerbehof"))))
                    {
                        teil.Add(o, Py.Kopie(w));
                    }
                }
                if (teil.Count > 0)
                {
                    innen.Add(k, teil);
                }
            }
            else if (ids.Contains(k))
            {
                innen.Add(k, Py.Kopie(v));
            }
        }
        return innen;
    }

    private static PyObjekt Abschnitt(PyObjekt o, string name)
    {
        if (Get(o, name) is not PyObjekt a)
        {
            a = new PyObjekt();
            Py.Setzen(o, name, a);
        }
        return a;
    }

    private static List<object?> ListeAnlegen(PyObjekt o, string name)
    {
        if (Get(o, name) is not List<object?> l)
        {
            l = [];
            Py.Setzen(o, name, l);
        }
        return l;
    }

    /// <summary>Mitte des Hauses (nur fuer Bilder und Tests).</summary>
    public static Vector2 Mitte(PyObjekt aussen, AussenStelle s)
    {
        var (x, y, w, h) = AussenBearbeitung.HausFlaeche(aussen, s);
        return new Vector2((float)(x + (w / 2)), (float)(y + (h / 2)));
    }
}
