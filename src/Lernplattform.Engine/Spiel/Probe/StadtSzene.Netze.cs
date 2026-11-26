using System.Globalization;
using System.Numerics;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Engine festigen 3 (A 1.16.0, Weisung P12-395/P12-399): Teil von <see cref="StadtSzene"/> mit dem Bau der Netze (Netz der
/// Stadt, Instancing-Plan, Hausnetze, Paket-Netz, Questgeber, Fernkaesten). Reiner Umzug aus StadtSzene.cs, nichts geaendert.
/// </summary>
public sealed partial class StadtSzene
{
    private Netz NetzBauenRoh(bool fern, Instanzplan? plan, Auswahl? auswahl)
    {
        var ohneStueck = plan?.Stuecke ?? Instanzplan.Leer.Stuecke;
        var ohneBaum = plan?.Baeume ?? Instanzplan.Leer.Baeume;
        var teileHaus = new bool[Teile.Count];
        var stueckeHaus = new bool[Stuecke.Count];
        foreach (var h in Haeuser)
        {
            Array.Fill(teileHaus, true, h.TeileVon, h.TeileBis - h.TeileVon);
            Array.Fill(stueckeHaus, true, h.StueckeVon, h.StueckeBis - h.StueckeVon);
        }
        if (Jahreszeit == Jahreszeit.Sommer)
        {
            var netz = Netz.Aus(Teile.Where((t, i) => !teileHaus[i] && i < FestTeileVon && Nimmt(auswahl, i, t)));
            // Weisung P8-56: die Fest-Teile danach, das Licht der eigenen Laternen leuchtet nachts
            for (var i = FestTeileVon; i < Teile.Count; i++)
            {
                if (!teileHaus[i] && Nimmt(auswahl, i, Teile[i]))
                {
                    var von = netz.AnzahlEcken;
                    netz.Hinzu(Teile[i]);
                    if (Teile[i].Farbe == FestDeko.LaternenLicht)
                    {
                        netz.Leuchten(von, netz.AnzahlEcken);
                    }
                }
            }
            foreach (var streifen in Streifen)
            {
                if (Nimmt(auswahl, streifen))
                {
                    netz.Hinzu(streifen);
                }
            }
            // S 0.8.0 (Weisung P12-47): Ufer und Wasser der Teiche im selben Netz (ohne Teiche nichts, bitgleich wie vorher)
            foreach (var flaeche in Flaechen)
            {
                if (Nimmt(auswahl, flaeche))
                {
                    netz.Hinzu(flaeche);
                }
            }
            // Welle A 0.66.0: das Gelaende im selben Netz (kein neuer Zeichenaufruf; aus der Ferne Raster 1,0)
            if (auswahl is null || auswahl.NurBoden)
            {
                Gelaende?.Hinzu(netz, Breite, Tiefe, fern);
            }
            netz.UnterseiteBis = Unterseiten;
            for (var i = 0; i < Stuecke.Count; i++)
            {
                if (!stueckeHaus[i] && !ohneStueck.Contains(i) && !ImPaket(Stuecke[i].Name) && ModellAn(i) is { } original && Nimmt(auswahl, Stuecke[i].Lage, original))
                {
                    var von = netz.AnzahlEcken;
                    var modell = fern && i >= FestStueckeVon ? Fernform.Von(original) : original;
                    netz.Hinzu(modell, Stuecke[i].Lage, i < FestStueckeVon ? null : FestLicht(modell));
                    if (Katalogstueck.IstEffekt(modell.Name))
                    {
                        // Phase 13 Welle 4a: ein Effekt leuchtet nachts wie die Laternen
                        netz.Leuchten(von, netz.AnzahlEcken);
                    }
                }
            }
            var nb = -1;
            foreach (var baum in GruenKlein?.Baeume ?? [])
            {
                nb++;
                if (!ohneBaum.Contains(nb) && Grafik?.Modell(baum.Name) is { } modell && Nimmt(auswahl, baum.Lage, modell))
                {
                    netz.Hinzu(modell, baum.Lage);
                }
            }
            return netz;
        }
        // Phase 8 W9: Farben nach der Jahreszeit; die Fest-Deko (ab FestTeileVon/FestStueckeVon) behaelt ihre Farben
        var gefaerbt = new Netz();
        for (var i = 0; i < Teile.Count; i++)
        {
            if (!teileHaus[i] && Nimmt(auswahl, i, Teile[i]))
            {
                var t = Teile[i];
                gefaerbt.Faerbung = i < FestTeileVon
                    ? Jahreszeitfarben.FuerTeile(Jahreszeit, Faerbeart.Stadt, Jahreszeitfarben.Samen(new Vector3((t.X1 + t.X2) / 2, 0, (t.Z1 + t.Z2) / 2)))
                    : null;
                var von = gefaerbt.AnzahlEcken;
                gefaerbt.Hinzu(t);
                if (i >= FestTeileVon && t.Farbe == FestDeko.LaternenLicht)
                {
                    gefaerbt.Leuchten(von, gefaerbt.AnzahlEcken);
                }
            }
        }
        gefaerbt.Faerbung = Jahreszeitfarben.FuerTeile(Jahreszeit, Faerbeart.Stadt);
        // S 0.8.0 (Weisung P12-47, Frage 6 = a): mit dem Schalter eis friert im Winter das Wasser (Fluss und Teiche) zu,
        // das Ufer ist im Winter weiss wie bisher; ohne Schalter bleibt alles wie vorher
        var eis = Eis && Jahreszeit == Jahreszeit.Winter;
        foreach (var streifen in Streifen)
        {
            if (Nimmt(auswahl, streifen))
            {
                gefaerbt.Hinzu(eis && streifen.Farbe == Farben.Wasser ? streifen with { Farbe = EisFarbe } : streifen);
            }
        }
        foreach (var flaeche in Flaechen)
        {
            if (Nimmt(auswahl, flaeche))
            {
                gefaerbt.Hinzu(eis && flaeche.Farbe == Farben.Wasser ? flaeche with { Farbe = EisFarbe } : flaeche);
            }
        }
        if (auswahl is null || auswahl.NurBoden)
        {
            Gelaende?.Hinzu(gefaerbt, Breite, Tiefe, fern);
        }
        gefaerbt.Faerbung = null;
        gefaerbt.UnterseiteBis = Unterseiten;
        for (var i = 0; i < Stuecke.Count; i++)
        {
            if (!stueckeHaus[i] && !ohneStueck.Contains(i) && !ImPaket(Stuecke[i].Name) && ModellAn(i) is { } original && Nimmt(auswahl, Stuecke[i].Lage, original))
            {
                var modell = fern && i >= FestStueckeVon ? Fernform.Von(original) : original;
                if (Katalogstueck.IstEffekt(modell.Name))
                {
                    // Phase 13 Welle 4a: ein Effekt behaelt seine Farbe (kein Schnee) und leuchtet nachts
                    var von = gefaerbt.AnzahlEcken;
                    gefaerbt.Hinzu(modell, Stuecke[i].Lage, null);
                    gefaerbt.Leuchten(von, gefaerbt.AnzahlEcken);
                    continue;
                }
                gefaerbt.Hinzu(modell, Stuecke[i].Lage, i < FestStueckeVon ? ModellFaerbung(modell, Stuecke[i].Lage, Faerbeart.Stadt) : FestLicht(modell));
            }
        }
        var nbf = -1;
        foreach (var baum in GruenKlein?.Baeume ?? [])
        {
            nbf++;
            if (!ohneBaum.Contains(nbf) && Grafik?.Modell(baum.Name) is { } modell && Nimmt(auswahl, baum.Lage, modell))
            {
                gefaerbt.Hinzu(modell, baum.Lage, ModellFaerbung(modell, baum.Lage, Faerbeart.Stadt));
            }
        }
        return gefaerbt;
    }

    /// <summary>
    /// Welle D2 (Weisung P12-157, Nico 1a): welche CC0-Stuecke draussen (nicht am Haus, nicht im Paket-Netz, kein Effekt)
    /// mit Instancing gezeichnet werden. Jedes Stueck wird genau wie in <see cref="NetzBauen(bool, Instanzplan?)"/> bzw.
    /// <see cref="GruenNetzBauen(Instanzplan?)"/> entschieden und gefaerbt (Jahreszeit nach seiner Lage), nur im Raum des
    /// Modells; gleiche Netze bilden einen Satz (<see cref="Instanzen.Planen"/>). Fern zeigt jedes Stueck seine
    /// <see cref="Fernform"/> (Nico 1a: Fernform je Stueck), das Kleingruen wird fern nicht gezeichnet.
    /// </summary>
    public Instanzplan InstanzplanBauen()
    {
        if (Grafik is null)
        {
            return Instanzplan.Leer;
        }
        var stueckeHaus = new bool[Stuecke.Count];
        foreach (var h in Haeuser)
        {
            Array.Fill(stueckeHaus, true, h.StueckeVon, h.StueckeBis - h.StueckeVon);
        }
        // Welle Nachladen A 0.82.0: jedes Stueck fuer sich (auf allen Kernen), Reihenfolge wie bisher (Stuecke, Baeume, Kleingruen)
        Netz Lokal(GrafikModell m, Matrix4x4 lage, Modellfaerbung? f, float? unterseite)
        {
            var n = new Netz { UnterseiteBis = unterseite };
            n.HinzuLokal(m, lage, f);
            return n;
        }
        // Engine-Welle A 1.4.0 (Weisung P12-351): Stuecke, Baeume und Kleingruen in EINEM Durchgang auf allen Kernen (vorher
        // drei nacheinander), der Schluessel jedes Kandidaten gleich mit (vorher ein vierter Durchgang); Reihenfolge wie bisher
        var baeume = GruenKlein?.Baeume ?? [];
        var klein = GruenKlein?.Stuecke ?? [];
        var anzahl = Stuecke.Count + baeume.Count + klein.Count;
        var aus = new Instanzen.Kandidat?[anzahl];
        var schluessel = new string?[anzahl];
        void Kandidat(int nr)
        {
            Instanzen.Kandidat? k = null;
            if (nr < Stuecke.Count)
            {
                var i = nr;
                if (stueckeHaus[i] || ImPaket(Stuecke[i].Name) || ModellAn(i) is not { } original || Katalogstueck.IstEffekt(original.Name)
                    || !Instanzen.Geeignet(Stuecke[i].Lage))
                {
                    return;
                }
                var lage = Stuecke[i].Lage;
                var fernModell = Fernform.Von(original);
                var nah = Lokal(original, lage, i < FestStueckeVon ? ModellFaerbung(original, lage, Faerbeart.Stadt) : FestLicht(original), Unterseiten);
                var fern = Lokal(fernModell, lage, i < FestStueckeVon ? ModellFaerbung(fernModell, lage, Faerbeart.Stadt) : FestLicht(fernModell), Unterseiten);
                if (nah.AnzahlEcken > 0)
                {
                    k = new(Instanzen.Stadt, 's', i, original.Name, lage, nah, fern);
                }
            }
            else if (nr < Stuecke.Count + baeume.Count)
            {
                var b = nr - Stuecke.Count;
                var baum = baeume[b];
                if (Grafik.Modell(baum.Name) is not { } modell || !Instanzen.Geeignet(baum.Lage))
                {
                    return;
                }
                var fernModell = Fernform.Von(modell);
                var nah = Lokal(modell, baum.Lage, ModellFaerbung(modell, baum.Lage, Faerbeart.Stadt), Unterseiten);
                var fern = Lokal(fernModell, baum.Lage, ModellFaerbung(fernModell, baum.Lage, Faerbeart.Stadt), Unterseiten);
                if (nah.AnzahlEcken > 0)
                {
                    k = new(Instanzen.Stadt, 'b', b, modell.Name, baum.Lage, nah, fern);
                }
            }
            else
            {
                var g = nr - Stuecke.Count - baeume.Count;
                var st = klein[g];
                if (Grafik.Modell(st.Name) is not { } m || !Instanzen.Geeignet(st.Lage))
                {
                    return;
                }
                var nah = Lokal(m, st.Lage, ModellFaerbung(m, st.Lage, Faerbeart.Klein), null);
                if (nah.AnzahlEcken > 0)
                {
                    k = new(Instanzen.Gruen, 'g', g, m.Name, st.Lage, nah, null);
                }
            }
            if (k is not null)
            {
                aus[nr] = k;
                schluessel[nr] = Instanzen.SchluesselVon(k);
            }
        }
        if (Nacheinander.Value)
        {
            for (var nr = 0; nr < anzahl; nr++)
            {
                Kandidat(nr);
            }
        }
        else
        {
            Parallel.For(0, anzahl, Kandidat);
        }
        var kandidaten = new List<Instanzen.Kandidat>();
        var schluesselListe = new List<string>();
        for (var nr = 0; nr < anzahl; nr++)
        {
            if (aus[nr] is { } k)
            {
                kandidaten.Add(k);
                schluesselListe.Add(schluessel[nr]!);
            }
        }
        return Instanzen.Planen(kandidaten, schluesselListe);
    }

    /// <summary>
    /// Weisung P8-56 (Nico, Bild 4): die Lampen der Fest-Deko an den Tueren leuchten nachts wie die Laternen: bei
    /// Laternen-Modellen nur Glas und Kerze (<see cref="FestVerkehr.Glut"/>), alles andere behaelt seine Farbe.
    /// </summary>
    private static Modellfaerbung? FestLicht(GrafikModell modell) =>
        modell.Name.Contains("lantern", StringComparison.Ordinal) ? (d, grund, _, _) => FestVerkehr.Glut(modell, d, grund) : null;

    /// <summary>
    /// Das Netz eines Hauses (eigene Teile und CC0-Modelle). Welle D1: ohne Unterseiten am Boden; <paramref name="fern"/> =
    /// die CC0-Modelle in ihrer <see cref="Fernform"/> (Teil B), die eigenen Teile bleiben.
    /// </summary>
    public Netz HausNetz(StadtHaus haus, bool fern = false)
    {
        if (Jahreszeit == Jahreszeit.Sommer)
        {
            var netz = new Netz { UnterseiteBis = Unterseiten };
            for (var i = haus.TeileVon; i < haus.TeileBis; i++)
            {
                TeilHinzu(netz, i);
            }
            for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
            {
                if (!ImPaket(Stuecke[i].Name) && ModellAn(i) is { } original)
                {
                    var modell = fern ? Fernform.Von(original) : original;
                    var von = netz.Indizes.Count;
                    var flaechen = new List<Vector3>();
                    netz.Hinzu(modell, Stuecke[i].Lage, null, flaechen);
                    Nachtlicht.GlasMarkieren(netz, von, haus.Id + "|" + i.ToString(CultureInfo.InvariantCulture), flaechen);
                }
            }
            QuestgeberAmHaus(netz, haus, fern);
            return netz;
        }
        // Phase 8 W9: am Haus Schnee nur auf dem Dach; Baeume am Haus (Modellname mit "tree") wie die Baeume der Stadt
        var gefaerbt = new Netz { Faerbung = Jahreszeitfarben.FuerTeile(Jahreszeit, Faerbeart.Haus), UnterseiteBis = Unterseiten };
        for (var i = haus.TeileVon; i < haus.TeileBis; i++)
        {
            TeilHinzu(gefaerbt, i);
        }
        gefaerbt.Faerbung = null;
        for (var i = haus.StueckeVon; i < haus.StueckeBis; i++)
        {
            if (!ImPaket(Stuecke[i].Name) && ModellAn(i) is { } original)
            {
                var modell = fern ? Fernform.Von(original) : original;
                var art = Stuecke[i].Name.Contains("tree", StringComparison.Ordinal) ? Faerbeart.Stadt : Faerbeart.Haus;
                var von = gefaerbt.Indizes.Count;
                var flaechen = new List<Vector3>();
                gefaerbt.Hinzu(modell, Stuecke[i].Lage, ModellFaerbung(modell, Stuecke[i].Lage, art), flaechen);
                Nachtlicht.GlasMarkieren(gefaerbt, von, haus.Id + "|" + i.ToString(CultureInfo.InvariantCulture), flaechen);
            }
        }
        QuestgeberAmHaus(gefaerbt, haus, fern);
        return gefaerbt;
    }

    /// <summary>Welle Q2: die Questgeber draussen, die zu diesem Haus gehoeren, nah als Figur, fern als Kaesten.</summary>
    private void QuestgeberAmHaus(Netz netz, StadtHaus haus, bool fern)
    {
        if (QuestgeberJeHaus.TryGetValue(haus.Id, out var liste))
        {
            QuestgeberHinzu(netz, fern, liste);
        }
    }

    /// <summary>
    /// W10: eigenes Teil ins Netz, Fenster mit Licht mit der Marke "leuchtet nachts", aber nur die Scheibe selbst
    /// (Flaechen quer zur duennsten Seite): die schmalen Kanten des Fensterblocks (Seitenkante der Bank, Weisung
    /// P8-34) bleiben dunkel.
    /// </summary>
    private void TeilHinzu(Netz netz, int i)
    {
        var von = netz.AnzahlEcken;
        var t = Teile[i];
        netz.Hinzu(t);
        if (!LichtTeile.Contains(i))
        {
            return;
        }
        var mass = new Vector3(MathF.Abs(t.X2 - t.X1), MathF.Abs(t.Y2 - t.Y1), MathF.Abs(t.Z2 - t.Z1));
        // das Zifferblatt der Bahnhofsuhr steht quer zur Suedwand: es leuchtet nach Osten und Westen (aus Streifen, die flacher als dick sind)
        var duenn = t.Farbe == UhrBlatt ? Vector3.UnitX
            : mass.X <= mass.Y && mass.X <= mass.Z ? Vector3.UnitX : mass.Y <= mass.Z ? Vector3.UnitY : Vector3.UnitZ;
        if (t.Dreh.Z != 0f && !LichtDrehAlt.Value)
        {
            // Welle A 0.70.0: das Netz hat den Teil gedreht, die duenne Seite dreht mit (sonst leuchtet ein schraeges Fenster nie)
            var (sin, cos) = MathF.SinCos(t.Dreh.Z * MathF.PI / 180);
            duenn = new Vector3((duenn.X * cos) - (duenn.Z * sin), duenn.Y, (duenn.X * sin) + (duenn.Z * cos));
        }
        for (var e = von; e < netz.AnzahlEcken; e++)
        {
            if (MathF.Abs(Vector3.Dot(netz.Normale(e), duenn)) > 0.9f)
            {
                netz.Leuchten(e, e + 1);
            }
        }
    }

    /// <summary>Gegenprobe A 0.70.0: duenne Seite eines gedrehten Teils ungedreht wie im ersten Entwurf (nur Tests, je Ablauf).</summary>
    public static bool LichtDrehAltFuerTests { get => LichtDrehAlt.Value; set => LichtDrehAlt.Value = value; }
    private static readonly AsyncLocal<bool> LichtDrehAlt = new();

    /// <summary>
    /// Phase 8 W11 (Nico 5a, P8-42): Modelle aus dem Graveyard Kit (Tannen, Kuerbisse) stehen nicht im Netz der Stadt
    /// oder eines Hauses, sondern mit den Laternen im eigenen Netz (<see cref="PaketNetzBauen"/>).
    /// </summary>
    public static bool ImPaket(string modell) => modell.StartsWith("gy-", StringComparison.Ordinal);

    /// <summary>
    /// Phase 8 W11 (Nico 5a, P8-42): eigenes Netz fuer Laternen, Tannen, Kuerbisse und Bahnuebergaenge (P8-49) (ein Zeichenaufruf mehr, die
    /// Grenze der Gesamtansicht bleibt): nah die Paket-Modelle, fern (<paramref name="fern"/>) einfache Kaesten.
    /// Tannen nach der Jahreszeit gefaerbt (im Herbst das orange Modell), Kuerbisse und Laternen behalten ihre Farben.
    /// </summary>
    /// <remarks>
    /// Welle 327 (Weisung P12-244, Weg a): das nahe Netz ist nach Zellen von <see cref="PaketZelle"/> geordnet
    /// (<see cref="Netz.NachZellenOrdnen"/>); gezeichnet und gezaehlt werden nur die Zellen im Bild. Ecken und Aussehen wie vorher.
    /// </remarks>
    public Netz PaketNetzBauen(bool fern) => PaketNetzBauen(fern, zellen: true);

    /// <summary>Welle 327: <paramref name="zellen"/> = false: das nahe Netz ungeteilt wie bis A 0.73.0 (Tests: dieselben Dreiecke).</summary>
    public Netz PaketNetzBauen(bool fern, bool zellen)
    {
        var netz = PaketNetzRoh(fern);
        if (!fern && zellen)
        {
            // die Fernform ist klein (alte Stadt 1 500, Stadt 1 5 448 Dreiecke) und meist ganz im Bild: ungeteilt ein Aufruf
            netz.NachZellenOrdnen(PaketZelle);
        }
        return netz;
    }

    /// <summary>Welle 327: Kantenlaenge der Zellen des Paket-Netzes (Felder).</summary>
    public const float PaketZelle = 8f;

    private Netz PaketNetzRoh(bool fern) => PaketNetzRoh(fern, null);

    /// <summary>Welle Nachladen: wie <see cref="PaketNetzRoh(bool)"/>, mit <paramref name="auswahl"/> nur die Stuecke nahe der Kachel (zuschneiden danach).</summary>
    private Netz PaketNetzRoh(bool fern, Auswahl? auswahl)
    {
        var netz = new Netz();
        // Welle Nachladen (grosse Karten): fuer eine Kachel nur Ausstattung, die sie beruehren kann (der Zuschnitt danach
        // liesse die anderen ohnehin ganz weg; gleiches Ergebnis, ohne jede Kachel die ganze Karte zu bauen)
        Func<Netz, Matrix4x4, bool>? nimmt = auswahl is { Kachel: { } kachel }
            ? (muster, lage) => muster.KannInKachel(lage, kachel, auswahl.Breite, auswahl.Tiefe)
            : null;
        // P8-49: Bahnuebergaenge nah und fern ganz (klein), ihre Lichter leuchten nachts rot
        Bahnuebergang.Hinzu(netz, Andreaskreuze, nimmt);
        // Welle A 0.60.0: Masten der Ampeln wie die Bahnuebergaenge (nah und fern ganz, klein); die Lampen leuchten im eigenen Aufruf
        Probe.Ampeln.Hinzu(netz, Ampeln, nimmt);
        // Welle A 0.61.0: Haeuschen und Mast der Haltestellen wie die Masten der Ampeln (nah und fern ganz, klein)
        Probe.Haltestellen.Hinzu(netz, Haltestellen, nimmt);
        // Welle Q2: Questgeber draussen (nah die Figur mit ihrem Aussehen, fern zwei Kaesten wie die wartenden Figuren)
        QuestgeberHinzu(netz, fern);
        if (Grafik is null)
        {
            return netz;
        }
        for (var i = 0; i < Stuecke.Count; i++)
        {
            if (!ImPaket(Stuecke[i].Name) || Grafik.Modell(TanneIn(Stuecke[i].Name, Jahreszeit)) is not { } m || !Nimmt(auswahl, Stuecke[i].Lage, m))
            {
                continue;
            }
            var lage = Stuecke[i].Lage;
            var faerbung = i < FestStueckeVon ? ModellFaerbung(m, lage, Faerbeart.Stadt) : null;
            if (fern)
            {
                FernKaesten(netz, m, lage, faerbung);
            }
            else
            {
                netz.Hinzu(m, lage, faerbung);
            }
        }
        foreach (var l in Laternen)
        {
            if (auswahl is not { Kachel: { } k } || Beruehrt(auswahl, k, l.Ort.X, l.Ort.X, l.Ort.Y, l.Ort.Y, LaterneUmkreis))
            {
                Nachtlicht.Hinzu(netz, Grafik, l, fern);
            }
        }
        return netz;
    }

    /// <summary>Welle Nachladen: so weit reicht eine Laterne samt Arm und Licht hoechstens von ihrem Fuss (grosszuegig).</summary>
    private const float LaterneUmkreis = 3f;

    /// <summary>Groesster Radius, fuer den die Raster-Pruefung des Kleingruens gilt (groesser: ganze Listen).</summary>
    private const float FreiRMax = 0.35f;

    /// <summary>
    /// Welle Q2: Questgeber draussen in ein Netz (Paket-Netz oder Netz ihres Hauses); Fuesse auf dem Boden an ihrem Platz, Blick wie im Werkzeug
    /// (0 = +y, 90 = +x). Ohne Grafik (Tests) die einfache Probefigur.
    /// </summary>
    private void QuestgeberHinzu(Netz netz, bool fern) => QuestgeberHinzu(netz, fern, QuestgeberImPaket);

    private void QuestgeberHinzu(Netz netz, bool fern, IEnumerable<Stadtbau.Questebene.Draussen> questgeber)
    {
        foreach (var q in questgeber)
        {
            var (bx, by) = Stadtbau.Etagen.Richtung(q.Blick);
            var lage = Matrix4x4.CreateRotationY(MathF.Atan2((float)bx, (float)by)) * Matrix4x4.CreateTranslation((float)q.X, 0f, (float)q.Z);
            netz.Hinzu(QuestgeberNetz(Grafik, q.Aussehen, fern), lage);
        }
    }

    /// <summary>Welle Q2: Netz eines Questgebers draussen (nah die Figur in Stadthoehe, fern Beine und Oberteil als Kaesten).</summary>
    public static Netz QuestgeberNetz(GrafikBibliothek? grafik, PyObjekt? aussehen, bool fern)
    {
        if (!fern)
        {
            return Figur.Netz(grafik, aussehen, Figur.StadtHoehe);
        }
        var a = Lernplattform.Core.Spiel.Aussehen.Normalisieren(aussehen);
        var h = Figur.StadtHoehe;
        var b = h * 0.14f;
        var oben = Netz.Farbe(Figur.Oberteil.GetValueOrDefault(Py.Str(a["oberteil"]), "#22D3EE"));
        var haut = Netz.Farbe(Figur.Haut.GetValueOrDefault(Py.Str(a["haut"]), "#F1C27D"));
        return Netz.Aus(
        [
            new Teil(TeilArt.Quader, -b * 0.8f, 0, -b * 0.5f, b * 0.8f, h * 0.45f, b * 0.5f, Netz.Farbe("#334155")),
            new Teil(TeilArt.Quader, -b, h * 0.45f, -b * 0.6f, b, h * 0.82f, b * 0.6f, oben),
            new Teil(TeilArt.Quader, -b * 0.55f, h * 0.82f, -b * 0.55f, b * 0.55f, h, b * 0.55f, haut),
        ]);
    }

    /// <summary>Ferne-Form eines Modells: untere und obere Haelfte als Kasten in der mittleren Farbe der Haelfte (oben schmaler).</summary>
    private static void FernKaesten(Netz netz, GrafikModell m, Matrix4x4 lage, Modellfaerbung? faerbung)
    {
        var mitteY = (m.Min.Y + m.Max.Y) / 2;
        var summe = new Vector3[2];
        var zahl = new int[2];
        for (var d = 0; d < m.Dreiecke; d++)
        {
            var y = (m.Ecken[(d * 9) + 1] + m.Ecken[(d * 9) + 4] + m.Ecken[(d * 9) + 7]) / 3;
            var h = y < mitteY ? 0 : 1;
            summe[h] += faerbung is null
                ? m.Farbe(d)
                : faerbung(d, m.Farbe(d), 0.5f, Vector3.Transform(new Vector3(0, y, 0), lage).Y);
            zahl[h]++;
        }
        var unten = Vector3.Transform(new Vector3((m.Min.X + m.Max.X) / 2, m.Min.Y, (m.Min.Z + m.Max.Z) / 2), lage);
        var oben = Vector3.Transform(new Vector3((m.Min.X + m.Max.X) / 2, m.Max.Y, (m.Min.Z + m.Max.Z) / 2), lage);
        var breite = MathF.Max(m.Max.X - m.Min.X, m.Max.Z - m.Min.Z) * new Vector3(lage.M11, lage.M12, lage.M13).Length() / 2;
        var mitte = (unten.Y + oben.Y) / 2;
        for (var h = 0; h < 2; h++)
        {
            if (zahl[h] == 0)
            {
                continue;
            }
            var b = breite * (h == 0 ? 1f : 0.55f);
            netz.Hinzu(new Teil(TeilArt.Quader, unten.X - b, h == 0 ? unten.Y : mitte, unten.Z - b, unten.X + b, h == 0 ? mitte : oben.Y, unten.Z + b, summe[h] / zahl[h]));
        }
    }
}
