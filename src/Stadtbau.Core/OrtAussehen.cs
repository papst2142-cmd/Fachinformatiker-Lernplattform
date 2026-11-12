using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// Welle S 0.21.0 / A 0.52.0 (Weisungen P12-87/P12-88, Kurzplan Meldung P12-56, Nico 1a 2a 3a 4a 5a): ein Ort mit Rolle
/// bekommt ein anderes Aussehen (ein Haus aus einer Vorlage). Es bleiben Kennung, Name, Rolle, Kunde, Aufgaben,
/// Freischaltung, Innenraum, Spielstaende und Fingerabdruecke; es aendern sich nur Modell, Tuer, Fussweg, Schild und Drehung.
/// <list type="bullet">
/// <item>Modell: bei einem Ort der Karte (orte.json) in <c>karte.modelle</c> (dort sucht es die Ladepruefung des Spiels),
/// bei einem neuen Ort (Laeden, Bank, Amt) in <c>stadtdaten.neue_modelle</c>; das Feld <c>modell</c> des Orts zeigt auf den
/// neuen Namen. Das alte Modell bleibt in den Daten.</item>
/// <item>Tuer und Fussweg: <c>stadtdaten.zugaenge_neu[&lt;ort&gt;]</c>; der Tuerversatz <c>darstellung.modell_tuer</c> faellt weg.</item>
/// <item>Lage (Frage 1 = a): die Mitte des Hauses bleibt, das Haus dreht sich in Vierteln, bis die Tuer zur selben Seite
/// zeigt wie vorher, dann rastet es an den Gehweg. Der Fussweg fuehrt gerade aus der Tuer bis zum alten Weg und folgt ihm
/// (so bleibt ein Weg wie der umgelegte von Talheim-Nord erhalten); ohne passenden alten Weg wird der Weg der Vorlage bis
/// zur naechsten Strasse verlaengert.</item>
/// <item>Bahnsteig (P12-88, Vorschlag 6a): hatte das alte Haus einen Bahnsteig, bleibt er als eigenes Gebaeude ohne Ort
/// (<see cref="Bahnsteig.ModellName"/>) an der alten Stelle stehen; Nico kann ihn loeschen (dann gelb, nicht rot).</item>
/// <item>Zuhause (Frage 3 = a): nicht tauschbar (<see cref="TauschenNicht"/>).</item>
/// </list>
/// Fassung 0 und die Dateien des Spiels bleiben unberuehrt: getauscht wird nur im Projekt, der Export ist eine neue Fassung.
/// </summary>
public static class OrtAussehen
{
    /// <summary>So weit muss der alte Fussweg (sein zweiter Punkt) vor der neuen Tuer liegen, damit der neue Weg ihm folgt.</summary>
    public const double WegFolgenAb = 0.3;

    /// <summary>Eine Vorlage der Liste: Anzeigename, Blueprint, ob sie ein Gebaeude der Stadt ist, ob sie „Altes Aussehen zurueck“ ist.</summary>
    public sealed record Vorlage(string Name, Blueprint Blueprint, bool AusDerStadt, bool Zurueck);

    /// <summary>Grund, warum der Ort nicht tauschbar ist (Zuhause, kein Ort), sonst null.</summary>
    public static string? TauschenNicht(PyObjekt aussen, string ort) =>
        AussenBearbeitung.Ort(aussen, ort) is not { } o ? Texte.AussehenKeinOrt
        : Py.Gleich(Get(o, "ansicht"), "zuhause") ? Texte.AussehenZuhauseNicht
        // Welle A 0.70.0 (Sammelliste 314, E1): ein schraeg gedrehtes Haus erst gerade drehen
        : AussenBearbeitung.HausGrad(aussen, new AussenStelle(AussenArt.Haus, -1, 0, ort)) != 0 ? Texte.AussehenSchraegNicht
        : null;

    /// <summary>
    /// Vorlagen fuer einen Ort (Frage 5 = a): jeder eigene Blueprint mit Tuer, dazu jedes Gebaeude ohne Ort mit Tuer aus der
    /// Ausgangsstadt und der Stadt jetzt (z. B. das Stellwerk, auch wenn Nico es schon geloescht hat: so geht es ohne Umweg
    /// ueber „Als Blueprint speichern“), zuletzt „Altes Aussehen zurueck“, wenn der Ort ein anderes Haus hat als in der
    /// Ausgangsstadt. Vorlagen mit demselben Modell wie das jetzige Haus fehlen.
    /// </summary>
    public static List<Vorlage> Vorlagen(PyObjekt aussen, PyObjekt basis, string ort, IEnumerable<Blueprint.Eintrag> eigene)
    {
        var liste = new List<Vorlage>();
        if (AussenBearbeitung.Ort(aussen, ort) is not { } o)
        {
            return liste;
        }
        var jetzt = Py.Str(o["modell"]);
        foreach (var e in eigene)
        {
            if (e.Vorlage is { Tuer: not null } b && b.Modell != jetzt)
            {
                liste.Add(new Vorlage(e.Name, b, false, false));
            }
        }
        var gesehen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stand in new[] { aussen, basis })
        {
            var land = Aussen.Landschaft(stand);
            for (var i = 0; i < land.Count; i++)
            {
                if (land[i] is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && g["modell"] is string modell && modell != jetzt
                    && Blueprint.Zierweg(stand, modell) is { } z && gesehen.Add(modell)
                    && AusGebaeude(stand, i, z["name"] as string is { Length: > 0 } n ? n : modell) is { } b)
                {
                    liste.Add(new Vorlage(b.Name, b, true, false));
                }
            }
        }
        if (AussenBearbeitung.Ort(basis, ort) is { } bo && !Py.Gleich(bo["modell"], jetzt) && AusOrt(basis, ort, Texte.AussehenAltZurueck) is { } alt)
        {
            liste.Add(new Vorlage(Texte.AussehenAltZurueck, alt, false, true));
        }
        return liste;
    }

    /// <summary>Vorlage aus einem Gebaeude ohne Ort mit Tuer (Zierweg), ohne Innenraum.</summary>
    public static Blueprint? AusGebaeude(PyObjekt aussen, int nummer, string name)
    {
        if (Aussen.Landschaft(aussen).ElementAtOrDefault(nummer) is not PyObjekt g || g["modell"] is not string modell
            || Get(AussenBearbeitung.Modelle(aussen), modell) is not List<object?> teile || Blueprint.Zierweg(aussen, modell) is not { } z)
        {
            return null;
        }
        // gedreht stehend: die Vorlage ist das ungedrehte Haus (Tuer und Weg zurueckgedreht)
        var dreh = Hausdrehung.Von(g);
        double hx = Py.F(g["x"]), hy = Py.F(g["y"]);
        var mitte = new Vector2((float)hx, (float)hy) + Hausdrehung.Mitte(teile);
        List<object?> Zurueck(object? p)
        {
            var l = Liste(p);
            var (x, y) = Hausdrehung.Drehen(Py.F(l[0]), Py.F(l[1]), mitte.X, mitte.Y, -dreh);
            return [Bearbeitung.Zahl(x - hx), Bearbeitung.Zahl(y - hy)];
        }
        var gebaut = Get(Lernplattform.Core.Stadtbau.Bauteile.Gebaeude(Aussen.Stadtdaten(aussen)), modell) as PyObjekt;
        return new Blueprint
        {
            Name = name,
            Herkunft = new PyObjekt { { "stadt", "" }, { "ort", "" }, { "projekt", "" } },
            Modell = modell,
            Teile = (List<object?>)Py.Kopie(teile)!,
            Tuer = Zurueck(z["tuer"]),
            Weg = [.. Liste(z["weg"]).Select(p => (object?)Zurueck(p))],
            Schild = null,
            Innenraum = null,
            Personen = [],
            Bauteile = gebaut is null ? null : (List<object?>)Py.Kopie(gebaut["teile"])!,
            Eingang = gebaut is null ? null : (List<object?>)Py.Kopie(gebaut["eingang"])!,
        };
    }

    /// <summary>Vorlage aus dem Haus eines Orts (ungedreht, mit Tuer, Weg und Schild), ohne Innenraum.</summary>
    public static Blueprint? AusOrt(PyObjekt aussen, string ort, string name)
    {
        if (AussenBearbeitung.Ort(aussen, ort) is not { } o || Get(AussenBearbeitung.Modelle(aussen), Py.Str(o["modell"])) is not List<object?> teile
            || AussenBearbeitung.Zugang(aussen, ort) is not { } z)
        {
            return null;
        }
        var dreh = Hausdrehung.VonOrt(Aussen.Stadtdaten(aussen), ort);
        double hx = Py.F(o["x"]), hy = Py.F(o["y"]);
        var mitte = new Vector2((float)hx, (float)hy) + Hausdrehung.Mitte(teile);
        List<object?> Zurueck(object? p)
        {
            var l = Liste(p);
            var (x, y) = Hausdrehung.Drehen(Py.F(l[0]), Py.F(l[1]), mitte.X, mitte.Y, -dreh);
            return [Bearbeitung.Zahl(x - hx), Bearbeitung.Zahl(y - hy)];
        }
        return new Blueprint
        {
            Name = name,
            Herkunft = new PyObjekt { { "stadt", "" }, { "ort", ort }, { "projekt", "" } },
            Modell = Py.Str(o["modell"]),
            Teile = (List<object?>)Py.Kopie(teile)!,
            Tuer = Zurueck(z["tuer"]),
            Weg = [.. Liste(z["weg"]).Select(p => (object?)Zurueck(p))],
            Schild = o["schild"] is List<object?> s ? Zurueck(s) : null,
            Innenraum = null,
            Personen = [],
        };
    }

    /// <summary>Ergebnis eines Tauschs: der Grund, warum nichts geschah (null = getauscht), und die Hinweise (gelb).</summary>
    public sealed record Ergebnis(string? Grund, List<string> Hinweise);

    /// <summary>
    /// Das Aussehen von <paramref name="ort"/> durch die Vorlage ersetzen (ein Schritt, wenn ueber <see cref="Projekt.AussenAendern"/>
    /// gerufen). Bei einem Grund aendert sich nichts. <paramref name="basis"/>: die Ausgangsstadt (fuer „Altes Aussehen
    /// zurueck“: Tuerversatz des alten Modells).
    /// </summary>
    public static Ergebnis Tauschen(PyObjekt aussen, string ort, Blueprint b, PyObjekt? basis = null, bool einrasten = true)
    {
        var hinweise = new List<string>();
        if (TauschenNicht(aussen, ort) is { } nicht)
        {
            return new Ergebnis(nicht, hinweise);
        }
        if (b.Tuer is null)
        {
            return new Ergebnis(Texte.AussehenOhneTuer, hinweise);
        }
        var o = AussenBearbeitung.Ort(aussen, ort)!;
        var land = Aussen.Landschaft(aussen);
        // dasselbe Modell steht als Gebaeude ohne Ort mit Tuer: das Spiel setzt die Tuer des Zierwegs an das erste Gebaeude
        // dieses Modells (Weisung P12-87 Punkt 2b: solange das alte Stellwerk steht, rot mit Grund)
        if (Blueprint.Zierweg(aussen, b.Modell) is { } zw && land.OfType<PyObjekt>().Any(t => Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], b.Modell)))
        {
            return new Ergebnis(Texte.AussehenSchonDa(zw["name"] as string is { Length: > 0 } n ? n : b.Modell), hinweise);
        }
        var alle = AussenBearbeitung.Modelle(aussen);
        if (Get(alle, b.Modell) is { } vorhanden && PythonJson.Standard(vorhanden) != PythonJson.Standard(b.Teile))
        {
            return new Ergebnis(Texte.BlueprintModellAnders(b.Modell), hinweise);
        }
        var sd = Aussen.Stadtdaten(aussen);
        var gebaut = Get(Lernplattform.Core.Stadtbau.Bauteile.Gebaeude(sd), b.Modell);
        PyObjekt? bauteile = b.Bauteile is null ? null : new PyObjekt { { "teile", Py.Kopie(b.Bauteile) }, { "eingang", Py.Kopie(b.Eingang ?? []) } };
        if (gebaut is not null && bauteile is not null && PythonJson.Standard(gebaut) != PythonJson.Standard(bauteile))
        {
            return new Ergebnis(Texte.BlueprintModellAnders(b.Modell), hinweise);
        }

        var s = new AussenStelle(AussenArt.Haus, -1, 0, ort);
        var altModell = Py.Str(o["modell"]);
        var altTeile = Get(alle, altModell);
        var altDreh = AussenBearbeitung.HausDreh(aussen, s);
        var (fx, fy, fw, fh) = AussenBearbeitung.HausFlaeche(aussen, s);
        var mitte = new Vector2((float)(fx + (fw / 2)), (float)(fy + (fh / 2)));
        var seite = AussenBearbeitung.HausTuerSeite(aussen, s);
        var altZugang = AussenBearbeitung.Zugang(aussen, ort) is { } az ? (PyObjekt)Py.Kopie(az)! : null;

        // Bahnsteig bleibt als eigenes Stueck stehen (P12-88), solange das neue Haus keinen hat
        if (Bahnsteig.Hat(altTeile) && !Bahnsteig.Hat(b.Teile))
        {
            BahnsteigStehenLassen(aussen, ort, altTeile, Py.F(o["x"]), Py.F(o["y"]), altDreh);
            hinweise.Add(Texte.AussehenBahnsteigBleibt);
        }
        else if (Bahnsteig.Hat(b.Teile))
        {
            BahnsteigWeg(aussen, ort);
        }
        if ((altTeile as List<object?> ?? []).OfType<PyObjekt>().Any(t => t.Any(p => p.Key is "erledigt" or "offen" or "serverfarm_fertig" or "wohnung" or "gegruendet")))
        {
            hinweise.Add(Texte.AussehenZustandWeg);
        }
        if (b.Innenraum is not null || b.Personen.Count > 0)
        {
            hinweise.Add(Texte.AussehenInnenBleibt);
        }

        // Modell dorthin, wo das Spiel es fuer diesen Ort sucht
        var karteOrt = ListeOderLeer(Aussen.Karte(aussen), "orte").OfType<PyObjekt>().Any(t => ReferenceEquals(t, o));
        var ziel = karteOrt ? Abschnitt(Aussen.Karte(aussen), "modelle") : Abschnitt(sd, "neue_modelle");
        if (Get(ziel, b.Modell) is null)
        {
            Py.Setzen(ziel, b.Modell, Py.Kopie(b.Teile));
        }
        if (bauteile is not null && gebaut is null)
        {
            Py.Setzen(Abschnitt(sd, "bauteile"), b.Modell, bauteile);
        }
        Py.Setzen(o, "modell", b.Modell);

        // ungedreht mit der alten Mitte
        if (Get(sd, "darstellung") is PyObjekt d)
        {
            if (Get(d, Hausdrehung.Abschnitt) is PyObjekt h)
            {
                h.RemoveAll(p => p.Key == ort);
                if (h.Count == 0)
                {
                    d.RemoveAll(p => p.Key == Hausdrehung.Abschnitt);
                }
            }
            if (Get(d, "modell_tuer") is PyObjekt mt)
            {
                mt.RemoveAll(p => p.Key == ort);
            }
        }
        var (gx, gy, gw, gh) = AussenBearbeitung.Grundflaeche(aussen, b.Modell);
        var hx = Bearbeitung.Runden(mitte.X - (gw / 2) - gx);
        var hy = Bearbeitung.Runden(mitte.Y - (gh / 2) - gy);
        Py.Setzen(o, "x", Bearbeitung.Zahl(hx));
        Py.Setzen(o, "y", Bearbeitung.Zahl(hy));
        var zugaenge = Abschnitt(sd, "zugaenge_neu");
        Py.Setzen(zugaenge, ort, new PyObjekt
        {
            { "tuer", Absolut(b.Tuer, hx, hy) },
            { "weg", b.Weg.Select(p => (object?)Absolut(Liste(p), hx, hy)).ToList() },
        });
        if (b.Schild is not null)
        {
            Py.Setzen(o, "schild", Absolut(b.Schild, hx, hy));
        }
        else if (o["schild"] is List<object?> schild)
        {
            // ohne eigenes Schild: das alte bleibt, wo es war (die Mitte des Hauses bleibt auch)
            Py.Setzen(o, "schild", new List<object?> { Bearbeitung.Zahl(Py.F(schild[0])), Bearbeitung.Zahl(Py.F(schild[1])) });
        }
        // in Vierteln drehen, bis die Tuer zur selben Seite zeigt wie vorher (Frage 1 = a)
        for (var i = 0; i < 3 && AussenBearbeitung.HausTuerSeite(aussen, s) != seite; i++)
        {
            AussenBearbeitung.Drehen(aussen, s);
        }
        if (einrasten)
        {
            AussenBearbeitung.HausAnGehweg(aussen, s);
        }
        WegLegen(aussen, ort, altZugang);
        // „Altes Aussehen zurueck“: der Tuerversatz des alten Modells aus der Ausgangsstadt
        if (basis is not null && AussenBearbeitung.Ort(basis, ort) is { } bo && Py.Gleich(bo["modell"], b.Modell)
            && Get(Objekt(Get(Aussen.Stadtdaten(basis), "darstellung") ?? new PyObjekt()), "modell_tuer") is PyObjekt bmt && Get(bmt, ort) is { } versatz)
        {
            Py.Setzen(Abschnitt(Abschnitt(sd, "darstellung"), "modell_tuer"), ort, Py.Kopie(versatz));
        }
        return new Ergebnis(null, hinweise);
    }

    /// <summary>
    /// Fussweg des getauschten Hauses: gerade aus der Tuer bis auf die Hoehe des zweiten Punkts des alten Wegs, dann zu ihm
    /// und weiter auf dem alten Weg. Liegt der alte Weg nicht vor der Tuer (oder fehlt), bleibt der Weg der Vorlage und wird
    /// bis zur naechsten Strasse verlaengert.
    /// </summary>
    private static void WegLegen(PyObjekt aussen, string ort, PyObjekt? altZugang)
    {
        var z = AussenBearbeitung.Zugang(aussen, ort)!;
        var s = new AussenStelle(AussenArt.Haus, -1, 0, ort);
        var n = AussenBearbeitung.HausTuerSeite(aussen, s);
        var tuer = Punkt(z["tuer"]);
        var alt = altZugang is null ? [] : Liste(altZugang["weg"]).Select(Punkt).ToList();
        if (alt.Count >= 2)
        {
            var tiefe = Vector2.Dot(alt[1] - tuer, n);
            if (tiefe >= WegFolgenAb)
            {
                var weg = new List<Vector2> { tuer, tuer + (n * tiefe) };
                weg.AddRange(alt.Skip(1));
                // doppelte Punkte (der alte Weg lag schon genau vor der Tuer) fallen weg
                var sauber = new List<Vector2>();
                foreach (var p in weg)
                {
                    if (sauber.Count == 0 || Vector2.Distance(sauber[^1], p) > 0.005f)
                    {
                        sauber.Add(p);
                    }
                }
                Py.Setzen(z, "weg", sauber.Select(p => (object?)new List<object?> { Bearbeitung.Zahl(p.X), Bearbeitung.Zahl(p.Y) }).ToList());
                return;
            }
        }
        AussenBearbeitung.WegAnschliessen(Liste(z["weg"]), AussenBearbeitung.Strassen(aussen));
    }

    /// <summary>Bahnsteig-Flaechen des alten Hauses als eigenes Gebaeude ohne Ort (Modell in <c>karte.modelle</c>, wie das Stellwerk).</summary>
    internal static void BahnsteigStehenLassen(PyObjekt aussen, string ort, object? altTeile, double x, double y, int dreh)
    {
        var name = Bahnsteig.ModellName(ort);
        var land = Aussen.Landschaft(aussen);
        if (land.OfType<PyObjekt>().Any(t => Py.Gleich(t["typ"], "gebaeude") && Py.Gleich(t["modell"], name)))
        {
            return;
        }
        var rechtecke = Bahnsteig.Rechtecke(altTeile, x, y, dreh);
        var min = rechtecke.Aggregate(rechtecke[0].Min, (m, r) => Vector2.Min(m, r.Min));
        var (ex, ey) = (Bearbeitung.Runden(min.X), Bearbeitung.Runden(min.Y));
        var teile = rechtecke.Select(r => (object?)new PyObjekt
        {
            { "art", "flaeche" },
            { "x", Bearbeitung.Zahl(r.Min.X - ex) },
            { "y", Bearbeitung.Zahl(r.Min.Y - ey) },
            { "w", Bearbeitung.Zahl(r.Max.X - r.Min.X) },
            { "h", Bearbeitung.Zahl(r.Max.Y - r.Min.Y) },
            { "farbe", Bahnsteig.Farbe },
        }).ToList();
        Py.Setzen(Abschnitt(Aussen.Karte(aussen), "modelle"), name, teile);
        land.Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", name }, { "x", Bearbeitung.Zahl(ex) }, { "y", Bearbeitung.Zahl(ey) } });
    }

    /// <summary>Bahnsteig-Stueck des Orts weg (das alte Haus mit Bahnsteig ist wieder da); sein Modell geht mit.</summary>
    private static void BahnsteigWeg(PyObjekt aussen, string ort)
    {
        var name = Bahnsteig.ModellName(ort);
        Aussen.Landschaft(aussen).RemoveAll(t => t is PyObjekt g && Py.Gleich(g["typ"], "gebaeude") && Py.Gleich(g["modell"], name));
        if (Get(Aussen.Karte(aussen), "modelle") is PyObjekt m)
        {
            m.RemoveAll(p => p.Key == name);
        }
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

    private static Vector2 Punkt(object? p)
    {
        var l = Liste(p);
        return new Vector2((float)Py.F(l[0]), (float)Py.F(l[1]));
    }

    private static List<object?> Absolut(List<object?> p, double x, double y) => [Bearbeitung.Zahl(Py.F(p[0]) + x), Bearbeitung.Zahl(Py.F(p[1]) + y)];
}
