using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;
using Teil = Lernplattform.Core.Stadtbau.Bauteile.Teil;
using Eingang = Lernplattform.Core.Stadtbau.Bauteile.Eingang;
using Seiten = Lernplattform.Core.Stadtbau.Bauteile.Seiten;

namespace Stadtbau.Core;

/// <summary>
/// S 0.9.0 (Weisung P12-48, Plan P12-26b Abschnitte 1, 2, 4): ein Gebaeude im Editor „Gebäude entwerfen“, ohne
/// Oberflaeche. Teile aus Kenney Modular Buildings stehen in einem Bauraster von <see cref="Raster"/> x <see cref="Raster"/>
/// Feldern und auf den Ebenen 0 (EG) bis 5. Jeder Schritt (setzen, drehen, kopieren, verschieben, Ebene, loeschen, Eingang)
/// kommt in einen eigenen Verlauf (Rueckgaengig, Wiederholen). Die Pruefung (<see cref="Pruefen"/>) kennt alle Regeln aus
/// Abschnitt 4. Gespeichert wird als Blueprint (Format 2) mit Feld <c>bauteile</c> und <c>eingang</c>.
/// </summary>
public sealed class Entwurf
{
    /// <summary>Felder je Seite des Bauraster.</summary>
    public const int Raster = 16;

    public List<Teil> Teile { get; } = [];

    /// <summary>
    /// S 0.31.0 (Weisung P12-130 Frage 1 = a): Wunsch „Halle“, solange noch kein Teil die Gebaeudeart festlegt (die Oberflaeche
    /// setzt ihn mit der Gruppe „Industrie“). Kein Schritt im Verlauf.
    /// </summary>
    public bool HalleWunsch { get; set; }

    /// <summary>
    /// Ist das Gebaeude eine Halle? Ja, sobald ein Teil der Halle steht; ohne Teil einer Gebaeudeart gilt der Wunsch. Ein
    /// Gebaeude ist entweder Wohnbau oder Halle (gemischt = roter Fund).
    /// </summary>
    public bool Halle => Bauteile.IstHalle(Teile) || (HalleWunsch && !Teile.Any(t => Bauteile.IstWohnbau(t.Id)));

    /// <summary>Breite eines Felds dieses Gebaeudes (Wohnbau 1, Halle 1/3).</summary>
    public float Feld => Bauteile.Feld(Halle);

    /// <summary>Felder je Seite des Bauraster (Halle dreimal so viele, gleiche Flaeche).</summary>
    public static int RasterVon(bool halle) => halle ? Raster * 3 : Raster;

    public int RasterFelder => RasterVon(Halle);

    public List<Eingang> Eingaenge { get; } = [];

    /// <summary>Name des Blueprints, aus dem weitergebaut wird (null = neues Gebaeude).</summary>
    public string? Name { get; init; }

    /// <summary>Datei des Blueprints, aus dem weitergebaut wird.</summary>
    public string? Datei { get; init; }

    /// <summary>Modellname des Blueprints, aus dem weitergebaut wird (bleibt beim Speichern unter demselben Namen).</summary>
    public string? Modell { get; init; }

    /// <summary>Innenraum des Blueprints, aus dem weitergebaut wird (bleibt, solange Grundflaeche und Eingang gleich sind).</summary>
    public PyObjekt? Innenraum { get; init; }

    /// <summary>
    /// S 0.19.0 (Nico 3a): „Vorschlag neu“ im Editor gedrueckt: beim Speichern werden die Etagen des behaltenen Innenraums
    /// neu aus dem Entwurf angelegt (das Erdgeschoss bleibt).
    /// </summary>
    public bool EtagenNeu { get; set; }

    /// <summary>S 0.31.0 (Sammelliste 264): Farben des Gebaeudes <c>{fassade, dach, rahmen}</c> (leer = wie die Teile).</summary>
    public PyObjekt Farben { get; } = [];

    /// <summary>Farbe eines Teils setzen (null = wie vorher), ein Schritt im Verlauf; ungueltiger Wert = nichts.</summary>
    public bool FarbeSetzen(string teil, string? wert) => Schritt(() =>
    {
        if (!Gebaeudefarben.AlleSchluessel.Contains(teil) || (wert is not null && !Gebaeudefarben.WertGueltig(teil, wert)))
        {
            return false;
        }
        Farben.RemoveAll(f => f.Key == teil);
        if (wert is not null)
        {
            // feste Reihenfolge fassade, dach, rahmen
            Farben.Add(new(teil, teil == Gebaeudefarben.TuerSchluessel ? wert : wert.ToUpperInvariant()));
            var sortiert = Farben.OrderBy(f => Array.IndexOf(Gebaeudefarben.AlleSchluessel, f.Key)).ToList();
            Farben.Clear();
            Farben.AddRange(sortiert);
        }
        return true;
    });

    private readonly List<string> _zurueck = [];
    private readonly List<string> _vor = [];

    public bool KannZurueck => _zurueck.Count > 0;

    public bool KannWiederholen => _vor.Count > 0;

    /// <summary>Etwas hat sich geaendert (Schritt, Rueckgaengig, Wiederholen).</summary>
    public event Action? Geaendert;

    // ---- Verlauf ----------------------------------------------------------------------------------

    private string Stand() => PythonJson.Standard(new List<object?> { Daten(), Farben });

    private void StandSetzen(string json)
    {
        var beide = (List<object?>)Py.Lesen(json)!;
        var d = Objekt(beide[0]);
        Farben.Clear();
        Farben.AddRange(Objekt(beide[1]));
        Teile.Clear();
        Teile.AddRange(Bauteile.TeileLesen(d["teile"]));
        Eingaenge.Clear();
        Eingaenge.AddRange(Bauteile.EingaengeLesen(d["eingang"]));
    }

    private T Schritt<T>(Func<T> was)
    {
        var vorher = Stand();
        var r = was();
        if (Stand() != vorher)
        {
            _zurueck.Add(vorher);
            _vor.Clear();
            Geaendert?.Invoke();
        }
        return r;
    }

    public void Zurueck()
    {
        if (_zurueck.Count == 0)
        {
            return;
        }
        _vor.Add(Stand());
        StandSetzen(_zurueck[^1]);
        _zurueck.RemoveAt(_zurueck.Count - 1);
        Geaendert?.Invoke();
    }

    public void Wiederholen()
    {
        if (_vor.Count == 0)
        {
            return;
        }
        _zurueck.Add(Stand());
        StandSetzen(_vor[^1]);
        _vor.RemoveAt(_vor.Count - 1);
        Geaendert?.Invoke();
    }

    // ---- Schritte ---------------------------------------------------------------------------------

    public static bool ImRaster(int x, int y) => ImRaster(x, y, false);

    public static bool ImRaster(int x, int y, bool halle) => x >= 0 && x < RasterVon(halle) && y >= 0 && y < RasterVon(halle);

    /// <summary>Teil setzen; ausserhalb des Raster, unbekanntes Teil oder falsche Ebene: -1 (nichts geschieht).</summary>
    public int Setzen(string id, int x, int y, int stockwerk, int dreh) => Schritt(() =>
    {
        var t = new Teil(id, x, y, stockwerk, ((dreh % 4) + 4) % 4);
        var halle = Bauteile.IstHalle(id) || (!Bauteile.IstWohnbau(id) && Halle);
        if (!ImRaster(x, y, halle) || t.Art is null || stockwerk is < 0 or >= Bauteile.Ebenen)
        {
            return -1;
        }
        Teile.Add(t);
        return Teile.Count - 1;
    });

    /// <summary>Drehen um 90 Grad im Uhrzeigersinn. Ein Eingang an diesem Teil dreht mit.</summary>
    public void Drehen(int i) => Schritt(() =>
    {
        var t = Teile[i];
        var neu = t with { Dreh = (t.Dreh + 1) % 4 };
        EingangMitnehmen(t, neu);
        Teile[i] = neu;
        return 0;
    });

    public void Loeschen(int i) => Schritt(() =>
    {
        var t = Teile[i];
        Teile.RemoveAt(i);
        if (!Teile.Any(u => u.Art?.Tuer == true && u.X == t.X && u.Y == t.Y && u.Stockwerk == 0))
        {
            Eingaenge.RemoveAll(e => e.X == t.X && e.Y == t.Y);
        }
        return 0;
    });

    /// <summary>Kopie ins naechste freie Feld rechts (dann unten); Rueckgabe: neue Stelle oder -1 (kein Platz).</summary>
    public int Kopieren(int i) => Schritt(() =>
    {
        var t = Teile[i];
        foreach (var (dx, dy) in new[] { (1, 0), (0, 1), (-1, 0), (0, -1) })
        {
            var neu = t with { X = t.X + dx, Y = t.Y + dy };
            if (ImRaster(neu.X, neu.Y, Halle) && !Belegt(neu))
            {
                Teile.Add(neu);
                return Teile.Count - 1;
            }
        }
        return -1;
    });

    /// <summary>Um ein Feld verschieben (Pfeilknoepfe); ausserhalb des Raster geht nicht (false). Ein Eingang geht mit.</summary>
    public bool Verschieben(int i, int dx, int dy) => Schritt(() =>
    {
        var t = Teile[i];
        var neu = t with { X = t.X + dx, Y = t.Y + dy };
        if (!ImRaster(neu.X, neu.Y, Halle))
        {
            return false;
        }
        EingangMitnehmen(t, neu);
        Teile[i] = neu;
        return true;
    });

    /// <summary>Eine Ebene hoeher oder tiefer (Ebene 0 bis 5); ein Eingang gilt nur im Erdgeschoss und geht dann weg.</summary>
    public bool EbeneWechseln(int i, int d) => Schritt(() =>
    {
        var t = Teile[i];
        var neu = t with { Stockwerk = t.Stockwerk + d };
        if (neu.Stockwerk is < 0 or >= Bauteile.Ebenen)
        {
            return false;
        }
        EingangMitnehmen(t, neu);
        Teile[i] = neu;
        return true;
    });

    /// <summary>Kann das Teil Eingang werden (Tuer im Erdgeschoss, die nach genau einer Seite zeigt)?</summary>
    public static bool EingangMoeglich(Teil t) => t.Art is { Tuer: true } && t.Stockwerk == 0 && Bauteile.Alle.Contains(t.Fassade);

    /// <summary>Das Teil als einzigen Eingang festlegen (ersetzt einen vorherigen). false: das Teil kann es nicht sein.</summary>
    public bool EingangFestlegen(int i) => Schritt(() =>
    {
        var t = Teile[i];
        if (!EingangMoeglich(t))
        {
            return false;
        }
        Eingaenge.Clear();
        Eingaenge.Add(new Eingang(t.X, t.Y, t.Fassade));
        return true;
    });

    private void EingangMitnehmen(Teil alt, Teil neu)
    {
        var i = Eingaenge.FindIndex(e => e.X == alt.X && e.Y == alt.Y && alt.Stockwerk == 0 && alt.Art?.Tuer == true && e.Seite == alt.Fassade);
        if (i < 0)
        {
            return;
        }
        if (EingangMoeglich(neu))
        {
            Eingaenge[i] = new Eingang(neu.X, neu.Y, neu.Fassade);
        }
        else
        {
            Eingaenge.RemoveAt(i);
        }
    }

    /// <summary>Ist das Feld (bzw. bei einem Fassaden-Teil der Platz an seiner Seite) schon belegt?</summary>
    public bool Belegt(Teil t) => Teile.Any(u => !ReferenceEquals(u, t) && Platz(u) == Platz(t));

    /// <summary>Platz eines Teils: Feld und Ebene, Fassaden-Teile zusaetzlich die Seite (Dach-Teile die Seite Keine).</summary>
    public static (int X, int Y, int Ebene, int Art, Seiten Seite) Platz(Teil t) => t.Art switch
    {
        { Art: Bauteile.Art.Detail } => (t.X, t.Y, t.Stockwerk, 1, t.Fassade),
        { Art: Bauteile.Art.DachDetail } => (t.X, t.Y, t.Stockwerk, 2, Seiten.Keine),
        _ => (t.X, t.Y, t.Stockwerk, 0, Seiten.Keine),
    };

    /// <summary>Das Teil unter einem Feld der Ebene (Wand oder Dach, das das Feld belegt), sonst -1.</summary>
    public int TeilIm(int x, int y, int ebene) => Teile.FindIndex(t => t.X == x && t.Y == y && t.Stockwerk == ebene && t.Art?.BelegtFeld == true);

    // ---- Daten ------------------------------------------------------------------------------------

    public PyObjekt Daten() => new() { { "teile", Bauteile.TeileDaten(Teile) }, { "eingang", Bauteile.EingaengeDaten(Eingaenge) } };

    /// <summary>Weiterbauen: die Teile eines Blueprints aus dem Editor (Format 2 mit <c>bauteile</c>), sonst null.</summary>
    public static Entwurf? Aus(Blueprint b)
    {
        if (b.Bauteile is null)
        {
            return null;
        }
        var e = new Entwurf { Name = b.Name, Datei = b.Datei, Modell = b.Modell, Innenraum = b.Innenraum };
        e.Teile.AddRange(Bauteile.TeileLesen(b.Bauteile));
        e.Eingaenge.AddRange(Bauteile.EingaengeLesen(b.Eingang ?? []));
        if (b.Farben is not null)
        {
            e.Farben.AddRange((PyObjekt)Py.Kopie(b.Farben)!);
        }
        return e;
    }

    /// <summary>Modellname zu einem Blueprint-Namen: "bau:" und der Dateiname in kleinen Buchstaben, Leerzeichen als _.</summary>
    public static string ModellName(string name) => Bauteile.ModellPraefix + Projekt.DateiName(name).ToLowerInvariant().Replace(' ', '_');

    /// <summary>Das Gebaeude als ein Modell (alle Teile), wie Spiel und 3D-Ansicht es zeichnen.</summary>
    public GrafikModell Modell3D(string name, Func<string, GrafikModell?> modell, int? bisEbene = null) =>
        Bauteile.Zusammensetzen(name, [.. Teile.Where(t => bisEbene is null || t.Stockwerk <= bisEbene)], modell);

    /// <summary>Grundflaeche im Erdgeschoss (Felder, die ein Teil belegt) als Rechteck (x0, y0, x1, y1) oder null.</summary>
    public (int X0, int Y0, int X1, int Y1)? Grundflaeche()
    {
        var eg = Teile.Where(t => t.Stockwerk == 0 && t.Art?.BelegtFeld == true).ToList();
        return eg.Count == 0 ? null : (eg.Min(t => t.X), eg.Min(t => t.Y), eg.Max(t => t.X), eg.Max(t => t.Y));
    }

    /// <summary>
    /// Plan Abschnitt 2 (Frage 4 = a, nur Erdgeschoss): Grundriss als Vorschlag aus der Grundflaeche. Das Rechteck um die
    /// Grundflaeche, je Feld <see cref="Bauteile.InnenJeFeld"/> Einheiten. Wie bei allen Innenraeumen liegt die Eingangstuer
    /// auf der linken Aussenwand des Flurs: die Fassade mit dem Eingang wird die linke Wand (Hoehe des Plans = Breite der
    /// Fassade, Breite des Plans = Tiefe des Hauses), die Tuer liegt an der Stelle des Eingangs. Der Flur geht quer durchs
    /// Haus, darueber und darunter je ein Raum mit Tuer zum Flur (wo Platz ist). Nur ein Startwert: der Grundriss wird
    /// danach wie jeder Innenraum eines neuen Gebaeudes bearbeitet. Ohne Grundflaeche oder Eingang: null.
    /// </summary>
    public PyObjekt? GrundrissVorschlag()
    {
        if (Grundflaeche() is not { } g || Eingaenge.Count == 0)
        {
            return null;
        }
        var e = Eingaenge[0];
        var k = Bauteile.InnenJeFeld * Feld;
        var (w, t) = (g.X1 - g.X0 + 1, g.Y1 - g.Y0 + 1);
        // von draussen auf die Fassade gesehen: links -> rechts
        var (fassadeFelder, tiefeFelder, stelle) = e.Seite switch
        {
            Seiten.Sued => (w, t, e.X - g.X0),
            Seiten.Nord => (w, t, g.X1 - e.X),
            Seiten.Ost => (t, w, g.Y1 - e.Y),
            _ => (t, w, e.Y - g.Y0),
        };
        return GrundrissAus(fassadeFelder * k, tiefeFelder * k, (stelle * k) + (k / 2), k);
    }

    /// <summary>
    /// Grundriss wie <see cref="GrundrissVorschlag"/> aus den Massen: <paramref name="hoehe"/> = Breite der Fassade mit dem
    /// Eingang, <paramref name="breite"/> = Tiefe des Hauses, <paramref name="mitte"/> = Stelle der Tuer auf der Fassade (von
    /// draussen gesehen von links), <paramref name="k"/> = Einheiten je Feld. S 0.31.0 auch fuer ein uebernommenes
    /// Katalog-Stueck (<see cref="AussenBearbeitung.AlsGebaeude"/>).
    /// </summary>
    public static PyObjekt GrundrissAus(double hoehe, double breite, double mitte, double k)
    {
        var flurH = hoehe >= 2 * k ? 4.0 : hoehe / 2;
        var flurY = hoehe >= 2 * k ? Math.Clamp(mitte - (flurH / 2), 0, hoehe - flurH) : 0;
        var tuer = Math.Clamp(mitte, flurY + 0.7, flurY + flurH - 0.7);
        var flur = new PyObjekt
        {
            { "y", Z(flurY) }, { "h", Z(flurH) }, { "name", "Eingang" }, { "boden", "fliesen" },
            { "eingang", new PyObjekt { { "von", Z(tuer - 0.7) }, { "bis", Z(tuer + 0.7) } } },
            { "spieler", new List<object?> { Z(1.0), Z(tuer) } },
            { "deko", new List<object?>() },
        };
        var raeume = new List<object?>();
        PyObjekt Raum(string id, string name, double y, double h) => new()
        {
            { "id", id }, { "name", name }, { "x", Z(0) }, { "y", Z(y) }, { "w", Z(breite) }, { "h", Z(h) },
            { "tuer", new PyObjekt { { "von", Z((breite / 2) - 0.75) }, { "bis", Z((breite / 2) + 0.75) } } }, { "boden", "parkett" }, { "deko", new List<object?>() },
        };
        if (flurY >= 1)
        {
            raeume.Add(Raum("raum", "Raum", 0, flurY));
        }
        if (hoehe - flurY - flurH >= 1)
        {
            raeume.Add(Raum(raeume.Count == 0 ? "raum" : "raum2", raeume.Count == 0 ? "Raum" : "Raum 2", flurY + flurH, hoehe - flurY - flurH));
        }
        return new PyObjekt { { "breite", Z(breite) }, { "hoehe", Z(hoehe) }, { "fenster", new List<object?>() }, { "flur", flur }, { "raeume", raeume } };
    }

    private static object Z(double d) => Bearbeitung.Zahl(d);

    /// <summary>Lage der Tuer zum Eingang (Mitte der Aussenkante des Felds) relativ zur Ecke des Rasters.</summary>
    public static (double X, double Y) TuerPunkt(Eingang e) => TuerPunkt(e, 1);

    /// <summary>Wie oben mit der Breite eines Felds (Halle 1/3).</summary>
    public static (double X, double Y) TuerPunkt(Eingang e, double feld) => e.Seite switch
    {
        Seiten.Sued => ((e.X + 0.5) * feld, (e.Y + 1.0) * feld),
        Seiten.Nord => ((e.X + 0.5) * feld, e.Y * feld),
        Seiten.Ost => ((e.X + 1.0) * feld, (e.Y + 0.5) * feld),
        _ => (e.X * feld, (e.Y + 0.5) * feld),
    };

    /// <summary>Laenge des Fusswegs vor der Tuer im Blueprint (beim Setzen wird er bis zur Strasse verlaengert).</summary>
    public const double WegVorDerTuer = 0.6;

    /// <summary>
    /// Als Blueprint (Format 2): Modell = ein Block ueber dem zusammengesetzten Gebaeude, Tuer und kurzer Fussweg aus dem
    /// Eingang, Innenraum (vorhandener, wenn Grundflaeche und Eingang gleich blieben, sonst der Vorschlag), Feld
    /// <c>bauteile</c>. Nur bei roten Funden ist das nicht moeglich (<see cref="StadtbauFehler"/>).
    /// </summary>
    public Blueprint AlsBlueprint(string name, Func<string, GrafikModell?> modell, string projekt, string stadt, out bool grundrissNeu) =>
        AlsBlueprint(name, modell, projekt, stadt, out grundrissNeu, out _);

    /// <summary>
    /// Wie oben; S 0.19.0 (Weisung P12-80, Nico 1a 2a 3a): die Ebenen ueber dem Erdgeschoss werden Etagen mit Treppenhaus
    /// (<see cref="EtagenAusEntwurf.Neu"/>). Ein vorhandener Innenraum mit Etagen bleibt, wie er ist (3a: der Hinweis
    /// „Vorschlag neu“ kommt aus der Pruefung); ein vorhandener ohne Etagen bekommt sie dazu.
    /// <paramref name="etagenNeu"/> = Zahl der neu angelegten Etagen.
    /// </summary>
    public Blueprint AlsBlueprint(string name, Func<string, GrafikModell?> modell, string projekt, string stadt, out bool grundrissNeu, out int etagenNeu)
    {
        var rot = Pruefen(modell).Where(f => f.Rot).Select(f => f.Text).ToList();
        if (rot.Count > 0)
        {
            throw new StadtbauFehler(rot);
        }
        var modellName = Modell is not null && Name is not null && string.Equals(Name, name, StringComparison.CurrentCultureIgnoreCase) ? Modell : ModellName(name);
        var gm = Modell3D(modellName, modell);
        var e = Eingaenge[0];
        // die Tuer liegt auf der Aussenkante des Blocks (das Spiel setzt sie auf die Wand des Blocks; Simse, Balkone und
        // Dachueberstand machen ihn etwas groesser als die Felder)
        var (tx, ty) = TuerPunkt(e, Feld);
        (tx, ty) = e.Seite switch
        {
            Seiten.Sued => (tx, (double)gm.Max.Z),
            Seiten.Nord => (tx, (double)gm.Min.Z),
            Seiten.Ost => ((double)gm.Max.X, ty),
            _ => ((double)gm.Min.X, ty),
        };
        var (dx, dy) = Bauteile.Richtung(e.Seite);
        var vorschlag = GrundrissVorschlag()!;
        var alt = Innenraum is not null && Innenraum["breite"] is not null && Gleich(Innenraum, vorschlag);
        grundrissNeu = !alt;
        var innen = alt ? Innenraum! : vorschlag;
        etagenNeu = 0;
        if ((alt && EtagenNeu) || (!Etagen.Hat(innen) && EtagenAusEntwurf.Anzahl(Teile, Eingaenge) > 0))
        {
            innen = (PyObjekt)Py.Kopie(innen)!;
            etagenNeu = EtagenAusEntwurf.Neu(innen, Teile, Eingaenge);
        }
        return new Blueprint
        {
            Name = name,
            Herkunft = new PyObjekt { { "stadt", stadt }, { "ort", "" }, { "projekt", projekt } },
            Modell = modellName,
            Teile = Bauteile.Block(gm),
            Tuer = [Z(tx), Z(ty)],
            Weg = [new List<object?> { Z(tx), Z(ty) }, new List<object?> { Z(tx + (dx * WegVorDerTuer)), Z(ty + (dy * WegVorDerTuer)) }],
            Schild = null,
            Innenraum = innen,
            Personen = [],
            Bauteile = Bauteile.TeileDaten(Teile),
            Eingang = Bauteile.EingaengeDaten(Eingaenge),
            Farben = Farben.Count == 0 ? null : (PyObjekt)Py.Kopie(Farben)!,
        };
    }

    /// <summary>Gleiche Groesse und gleicher Eingang wie der Vorschlag (dann bleibt ein bearbeiteter Grundriss).</summary>
    private static bool Gleich(PyObjekt alt, PyObjekt vorschlag) =>
        PythonJson.Standard(new List<object?> { alt["breite"], alt["hoehe"], Get(Objekt(alt["flur"]), "eingang") })
        == PythonJson.Standard(new List<object?> { vorschlag["breite"], vorschlag["hoehe"], Get(Objekt(vorschlag["flur"]), "eingang") });

    // ---- Pruefung (Plan Abschnitt 4) --------------------------------------------------------------

    /// <summary>
    /// Alle Regeln aus Abschnitt 4. Rot: zwei Teile im selben Feld, ein Teil haengt in der Luft, Dach ohne Wand darunter,
    /// Wand ueber dem 4. OG, unbekanntes Teil, kein oder mehr als ein Eingang, Eingang ohne Tuer, vor dem Eingang belegt,
    /// ueber 8 000 Dreiecken, die Grundflaeche zerfaellt (oder ist leer). Gelb: eine Ebene oben offen, ein Fenster zeigt
    /// in ein Nachbarteil. Stelle eines Funds: Mitte des Felds (x, y relativ zum Raster).
    /// </summary>
    public List<StadtbauFund> Pruefen(Func<string, GrafikModell?> modell)
    {
        var funde = new List<StadtbauFund>();
        var f = (double)Bauteile.Feld(Halle);
        void Rot(string text, string tipp, Teil? t) => funde.Add(new StadtbauFund(t is null ? text : Texte.EntwurfStelle(t.Stockwerk, t.X, t.Y) + text, tipp, true, t is null ? null : (t.X + 0.5) * f, t is null ? null : (t.Y + 0.5) * f));
        void Gelb(string text, string tipp, Teil t) => funde.Add(new StadtbauFund(Texte.EntwurfStelle(t.Stockwerk, t.X, t.Y) + text, tipp, false, (t.X + 0.5) * f, (t.Y + 0.5) * f));

        // S 0.31.0: entweder Wohnbau oder Halle
        if (Teile.Any(t => Bauteile.IstHalle(t.Id)) && Teile.FirstOrDefault(t => Bauteile.IstWohnbau(t.Id)) is { } gemischt)
        {
            Rot(Texte.EntwurfGemischt, Texte.EntwurfGemischtTipp, gemischt);
        }

        foreach (var t in Teile.Where(t => t.Art is null))
        {
            Rot(Texte.EntwurfUnbekannt(t.Name), Texte.EntwurfUnbekanntTipp, t);
        }
        var bekannt = Teile.Where(t => t.Art is not null).ToList();
        var felder = bekannt.Where(t => t.Art!.BelegtFeld).ToList();
        bool Wand(int x, int y, int e) => felder.Any(t => t.X == x && t.Y == y && t.Stockwerk == e && t.Art!.IstWand);
        bool Dach(int x, int y, int e) => felder.Any(t => t.X == x && t.Y == y && t.Stockwerk == e && t.Art!.IstDach);
        bool Feld(int x, int y, int e) => felder.Any(t => t.X == x && t.Y == y && t.Stockwerk == e);

        if (felder.All(t => t.Stockwerk != 0))
        {
            Rot(Texte.EntwurfLeer, Texte.EntwurfLeerTipp, null);
        }
        foreach (var g in bekannt.GroupBy(Platz).Where(g => g.Count() > 1))
        {
            Rot(Texte.EntwurfDoppelt, Texte.EntwurfDoppeltTipp, g.Skip(1).First());
        }
        foreach (var t in bekannt)
        {
            switch (t.Art!.Art)
            {
                case Bauteile.Art.Dach when t.Stockwerk == 0 || !Wand(t.X, t.Y, t.Stockwerk - 1):
                    Rot(Texte.EntwurfDachOhneWand, Texte.EntwurfDachOhneWandTipp, t);
                    break;
                case Bauteile.Art.Detail when !Wand(t.X, t.Y, t.Stockwerk):
                case Bauteile.Art.DachDetail when !Dach(t.X, t.Y, t.Stockwerk):
                    Rot(Texte.EntwurfInDerLuft, Texte.EntwurfDetailTipp, t);
                    break;
                case Bauteile.Art.Wand or Bauteile.Art.Ecke or Bauteile.Art.Tuer or Bauteile.Art.Block when t.Stockwerk > Bauteile.HoechsteWandEbene:
                    Rot(Texte.EntwurfZuHoch, Texte.EntwurfZuHochTipp, t);
                    break;
                case Bauteile.Art.Wand or Bauteile.Art.Ecke or Bauteile.Art.Tuer or Bauteile.Art.Block when t.Stockwerk > 0 && !Wand(t.X, t.Y, t.Stockwerk - 1):
                    Rot(Texte.EntwurfInDerLuft, Texte.EntwurfInDerLuftTipp, t);
                    break;
            }
        }
        // Eingang
        if (Eingaenge.Count == 0)
        {
            Rot(Texte.EntwurfKeinEingang, Texte.EntwurfKeinEingangTipp, null);
        }
        else if (Eingaenge.Count > 1)
        {
            Rot(Texte.EntwurfMehrEingaenge, Texte.EntwurfMehrEingaengeTipp, null);
        }
        foreach (var e in Eingaenge)
        {
            var tuer = bekannt.FirstOrDefault(t => t.X == e.X && t.Y == e.Y && t.Stockwerk == 0 && t.Art!.Tuer && t.Fassade == e.Seite
                && (t.Art.Art == Bauteile.Art.Tuer || Wand(t.X, t.Y, 0)));
            var stelle = new Teil("", e.X, e.Y, 0, 0);
            if (tuer is null)
            {
                Rot(Texte.EntwurfEingangOhneTuer, Texte.EntwurfEingangOhneTuerTipp, stelle);
                continue;
            }
            var (dx, dy) = Bauteile.Richtung(e.Seite);
            if (Feld(e.X + dx, e.Y + dy, 0))
            {
                Rot(Texte.EntwurfVorEingangBelegt, Texte.EntwurfVorEingangBelegtTipp, stelle);
            }
        }
        // Grenze
        var dreiecke = Bauteile.Dreiecke(bekannt, modell);
        if (dreiecke > Bauteile.GrenzeDreiecke)
        {
            Rot(Texte.EntwurfZuVieleDreiecke(dreiecke), Texte.EntwurfZuVieleDreieckeTipp, null);
        }
        // Grundflaeche zusammenhaengend (Nachbarn ueber eine Seite)
        var eg = felder.Where(t => t.Stockwerk == 0).Select(t => (t.X, t.Y)).ToHashSet();
        if (eg.Count > 1)
        {
            var offen = new Stack<(int, int)>([eg.First()]);
            var gesehen = new HashSet<(int, int)> { eg.First() };
            while (offen.Count > 0)
            {
                var (x, y) = offen.Pop();
                foreach (var s in Bauteile.Alle)
                {
                    var (dx, dy) = Bauteile.Richtung(s);
                    if (eg.Contains((x + dx, y + dy)) && gesehen.Add((x + dx, y + dy)))
                    {
                        offen.Push((x + dx, y + dy));
                    }
                }
            }
            if (gesehen.Count < eg.Count)
            {
                var weg = eg.Except(gesehen).OrderBy(p => p.Item2).ThenBy(p => p.Item1).First();
                Rot(Texte.EntwurfZerfaellt, Texte.EntwurfZerfaelltTipp, new Teil("", weg.Item1, weg.Item2, 0, 0));
            }
        }
        // gelb: oben offen (je Ebene ein Fund), Fenster zum Nachbarteil
        foreach (var g in felder.Where(t => t.Art!.IstWand && !Feld(t.X, t.Y, t.Stockwerk + 1)).GroupBy(t => t.Stockwerk).OrderBy(g => g.Key))
        {
            Gelb(Texte.EntwurfObenOffen(g.Count()), Texte.EntwurfObenOffenTipp, g.OrderBy(t => t.Y).ThenBy(t => t.X).First());
        }
        foreach (var t in bekannt.Where(t => t.Art!.Fenster))
        {
            foreach (var s in Bauteile.Alle.Where(s => (t.Fassade & s) != 0))
            {
                var (dx, dy) = Bauteile.Richtung(s);
                if (Feld(t.X + dx, t.Y + dy, t.Stockwerk))
                {
                    Gelb(Texte.EntwurfFensterZuNachbar, Texte.EntwurfFensterZuNachbarTipp, t);
                    break;
                }
            }
        }
        funde.AddRange(EtagenHinweise());
        return funde;
    }

    /// <summary>
    /// S 0.19.0 (Nico 3a): behaelt das Speichern den Innenraum mit Etagen und passen sie nicht mehr zu den Ebenen, ein gelber
    /// Hinweis je Abweichung (im Editor auch die Regel 5a nur gelb: Speichern bleibt moeglich, nichts geht verloren; rot ist sie
    /// im Innenraum des gesetzten Gebaeudes). Nach „Vorschlag neu“ (<see cref="EtagenNeu"/>) keiner.
    /// </summary>
    public List<StadtbauFund> EtagenHinweise()
    {
        if (EtagenNeu || Innenraum is null || !Etagen.Hat(Innenraum) || Innenraum["breite"] is null || GrundrissVorschlag() is not { } v || !Gleich(Innenraum, v))
        {
            return [];
        }
        return [.. EtagenAusEntwurf.Pruefen(Innenraum, Teile, Eingaenge).Select(f => f.Fund with { Rot = false })];
    }

    /// <summary>
    /// Probehaus fuer Start-Test und Fotos: 3 × 2 Felder, Erdgeschoss und 1. OG mit Fensterwaenden und Fensterecken, Tuer vorne
    /// in der Mitte, Flachdach mit zwei Klimageraeten (20 Teile, 2 Stockwerke und Dach).
    /// </summary>
    public static Entwurf Probehaus() => Probehaus(2);

    /// <summary>
    /// Probehaus mit <paramref name="stockwerke"/> Stockwerken (1 bis 5; S 0.19.0: 3 = EG und zwei Obergeschosse fuer Fotos und
    /// Start-Test der Etagen), Flachdach darueber.
    /// </summary>
    public static Entwurf Probehaus(int stockwerke)
    {
        var e = new Entwurf();
        const string p = Bauteile.Praefix;
        for (var stock = 0; stock < stockwerke; stock++)
        {
            // vorne (Sued): Ecken links und rechts, Mitte Tuer bzw. Fenster
            e.Setzen(p + "building-corner-window", 0, 1, stock, 1);
            e.Setzen(p + (stock == 0 ? "building-door-window" : "building-window-balcony"), 1, 1, stock, 0);
            e.Setzen(p + "building-corner-window", 2, 1, stock, 0);
            // hinten (Nord)
            e.Setzen(p + "building-corner-window", 0, 0, stock, 2);
            e.Setzen(p + "building-window", 1, 0, stock, 2);
            e.Setzen(p + "building-corner-window", 2, 0, stock, 3);
        }
        for (var x = 0; x < 3; x++)
        {
            for (var y = 0; y < 2; y++)
            {
                e.Setzen(p + "roof-flat-center", x, y, stockwerke, 0);
            }
        }
        e.Setzen(p + "detail-ac-a", 0, 0, stockwerke, 0);
        e.Setzen(p + "detail-ac-a", 2, 0, stockwerke, 1);
        e.EingangFestlegen(e.Teile.FindIndex(t => t.Name == "building-door-window"));
        return e;
    }

    /// <summary>
    /// S 0.31.0: Probehalle fuer Tests, Start-Test und Fotos: 9 x 6 Hallenfelder (3 x 2 Felder), Waende ringsum mit vier Ecken,
    /// vorne drei Fenster und das Tuerteil in der Mitte (Eingang), hinten drei Fenster, innen Hallenboden, darueber das
    /// Hallendach und ein Schornstein.
    /// </summary>
    public static Entwurf Probehalle()
    {
        var e = new Entwurf { HalleWunsch = true };
        const string p = Bauteile.PraefixHalle;
        e.Setzen(p + "structure-corner-outer", 8, 5, 0, 0);
        e.Setzen(p + "structure-corner-outer", 0, 5, 0, 1);
        e.Setzen(p + "structure-corner-outer", 0, 0, 0, 2);
        e.Setzen(p + "structure-corner-outer", 8, 0, 0, 3);
        for (var x = 1; x <= 7; x++)
        {
            e.Setzen(p + (x == 4 ? "structure-doorway" : x % 2 == 0 ? "structure-window" : "structure-wall"), x, 5, 0, 0);
            e.Setzen(p + (x % 2 == 0 ? "structure-window" : "structure-wall"), x, 0, 0, 2);
        }
        for (var y = 1; y <= 4; y++)
        {
            e.Setzen(p + "structure-wall", 0, y, 0, 1);
            e.Setzen(p + "structure-wall", 8, y, 0, 3);
            for (var x = 1; x <= 7; x++)
            {
                e.Setzen(p + "floor", x, y, 0, 0);
            }
        }
        for (var x = 0; x <= 8; x++)
        {
            for (var y = 0; y <= 5; y++)
            {
                e.Setzen(p + "top", x, y, 1, 0);
            }
        }
        e.Setzen(Bauteile.PraefixAufsatz + "chimney-medium", 6, 2, 1, 0);
        e.EingangFestlegen(e.Teile.FindIndex(t => t.Name.EndsWith("structure-doorway", StringComparison.Ordinal)));
        return e;
    }

    /// <summary>Anzeige einer Ebene: EG, 1. OG ... 4. OG, 5. Ebene (nur Dach).</summary>
    public static string EbeneName(int e) => e == 0 ? "EG" : e <= Bauteile.HoechsteWandEbene ? e.ToString(CultureInfo.InvariantCulture) + ". OG" : Texte.EbeneDach;
}
