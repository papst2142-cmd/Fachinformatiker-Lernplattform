using System.Numerics;
using System.Runtime.CompilerServices;
using Lernplattform.Core.Daten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 7, W5 (Weisung P7-10, Frage 1a): die Figur aus Kenney Animated Characters 3
/// (characterMedium.fbx mit der Haut humanMaleA, im Wandler als "figur" gespeichert, feste Haltung).
/// Gefaerbt nach dem Aussehen wie in der Python-Linie (SKIN_COLORS, HAIR_COLORS, SHIRT_COLORS):
/// Hemd, Haut und Haare werden an ihrer Farbe im Modell erkannt und umgefaerbt, die Helligkeit des
/// Dreiecks bleibt (Schatten der Vorlage). Ursprung unten in der Mitte, Blick nach +z.
/// Ohne Grafik-Bibliothek die Kloetzchen-Figur aus W1.
/// </summary>
public static class Figur
{
    /// <summary>RING_COLORS: Kreis um die Spielfigur (<c>SpielBilder.KreisFarben</c> verweist hierher, Weisung P12-179).</summary>
    public static readonly IReadOnlyDictionary<string, string> KreisFarben = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["pink"] = "#F472B6", ["cyan"] = "#22D3EE", ["gruen"] = "#34D399", ["violett"] = "#A78BFA", ["orange"] = "#FB923C", ["gelb"] = "#FBBF24",
    };

    /// <summary>Name des Modells in modelle.bin.</summary>
    public const string Modell = "figur";

    /// <summary>Hoehe der Figur in der Stadt: 1,8 m im Massstab der Stadt (Nachtrag 1 zu P7-12, Wunsch 2; vorher 1,24 = 6,2 m).</summary>
    public const float StadtHoehe = 1.8f * StadtSzene.Meter;

    public static readonly IReadOnlyDictionary<string, string> Haut = new Dictionary<string, string>
    {
        ["hell"] = "#F5D0B5", ["mittel"] = "#D9A47E", ["oliv"] = "#B98A5E", ["dunkel"] = "#7A5238",
    };

    public static readonly IReadOnlyDictionary<string, string> Haare = new Dictionary<string, string>
    {
        ["schwarz"] = "#2B2233", ["braun"] = "#6B4226", ["blond"] = "#E8C872", ["rot"] = "#C2562E", ["grau"] = "#B8B4C4",
    };

    public static readonly IReadOnlyDictionary<string, string> Oberteil = new Dictionary<string, string>
    {
        ["cyan"] = "#22D3EE", ["pink"] = "#F472B6", ["violett"] = "#A78BFA", ["gruen"] = "#34D399", ["orange"] = "#FB923C",
    };

    /// <summary>Wozu ein Dreieck der Vorlage gehoert (nach Farbe und Hoehe im Modell).</summary>
    public enum Bereich
    {
        Sonst,
        Hemd,
        Haut,
        Haare,
        Hose,
        Schuhe,
    }

    /// <summary>Phase 8 W5 (Kaufhaus): ein getragenes Kleidungsstueck, Slot (oberteil, hose, schuhe, kopf), Form und Farbe.</summary>
    public sealed record Stueck(string Slot, string Form, string Farbe);

    /// <summary>
    /// Hemd: blaugrau (Blau deutlich ueber Rot, hell) zwischen Huefte und Hals; Haut: Rot deutlich
    /// ueber Blau und hell; Haare: dunkel ueber Kinnhoehe. Hoehen relativ zur Modellhoehe.
    /// </summary>
    public static Bereich Erkennen(byte r, byte g, byte b, float hoeheAnteil)
    {
        if (b - r >= 8 && r >= 150 && hoeheAnteil > 0.33f && hoeheAnteil < 0.75f)
        {
            return Bereich.Hemd;
        }
        if (r - b >= 35 && r >= 170 && g >= 120)
        {
            return Bereich.Haut;
        }
        if (r < 110 && g < 100 && b < 100 && hoeheAnteil > 0.82f)
        {
            return Bereich.Haare;
        }
        return Bereich.Sonst;
    }

    /// <summary>Die Figur als Netz, <paramref name="hoehe"/> hoch, gefaerbt nach <paramref name="aussehen"/> (null = Voreinstellung).</summary>
    public static Netz Netz(GrafikBibliothek? grafik, PyObjekt? aussehen, float hoehe) => Netz(grafik, aussehen, hoehe, null);

    /// <summary>Hose: blaugrau unter der Huefte, Schuhe: grau ganz unten (nur wenn ein Stueck getragen wird).</summary>
    public static Bereich Kleidung(byte r, byte g, byte b, float hoeheAnteil)
    {
        if (b - r >= 30 && r < 140 && hoeheAnteil > 0.06f && hoeheAnteil < 0.36f)
        {
            return Bereich.Hose;
        }
        if (hoeheAnteil < 0.08f && Math.Abs(r - g) < 14 && Math.Abs(g - b) < 14 && r > 150)
        {
            return Bereich.Schuhe;
        }
        return Bereich.Sonst;
    }

    /// <summary>Die Figur mit getragener Kleidung (Kaufhaus): Oberteil, Hose und Schuhe umgefaerbt, Kopfbedeckung dazu.</summary>
    public static Netz Netz(GrafikBibliothek? grafik, PyObjekt? aussehen, float hoehe, IReadOnlyList<Stueck>? kleidung) =>
        Netz(grafik, aussehen, hoehe, kleidung, null, -1);

    /// <summary>Schrittbewegung Gehen (eigene Gelenk-Bewegung, in den Innenraeumen; Nico 1a, Weisung P8-14).</summary>
    public const string Gehen = "figur-gehen";

    /// <summary>Schrittbewegung Rennen (Animations/run.fbx aus dem Paket, in der Stadt; Nico 1a, Weisung P8-14).</summary>
    public const string Rennen = "figur-lauf";

    /// <summary>Phase 9 W1 (Weisung P9-3): sitzend am Schreibtisch, ein Bild (Name plus "00"), Personen auf einem Stuhl.</summary>
    public const string Sitzen = "figur-sitzen";

    /// <summary>Bilder je Bewegung (ein Doppelschritt), je Bild ein Modell Name plus zwei Ziffern in modelle.bin.</summary>
    public const int Bilder = 16;

    /// <summary>Modellname von Bild <paramref name="bild"/> der Bewegung <paramref name="bewegung"/>.</summary>
    public static string BildName(string bewegung, int bild) =>
        bewegung + bild.ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// Die Figur in Bild <paramref name="bild"/> der Bewegung <paramref name="bewegung"/> (null oder -1 = stehend).
    /// Farben, Groesse und Lage kommen immer von der stehenden Figur (sonst wuerde sie je Bild pulsieren), die Ecken
    /// vom Bild der Bewegung (gleiche Dreiecke in gleicher Reihenfolge, Sammelliste 125).
    /// </summary>
    /// <remarks>
    /// A 0.68.0: sofort voll uebersetzt (ohne gestufte Uebersetzung und ohne Wechsel mitten in der Schleife, "OSR"). Mit der
    /// Kleidung am Koerper liess die beim Laufen gewechselte Fassung dieser Methode den Prozess unter Linux und macOS mit
    /// AccessViolation in <see cref="Probe.Netz.Hinzu(Teil)"/> abstuerzen (Laeufe 869/870; nachgestellt mit vielen Figuren
    /// nebenher: ohne diesen Schalter rund jeder zweite Prozess, mit DOTNET_TC_OnStackReplacement=0 keiner).
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public static Netz Netz(GrafikBibliothek? grafik, PyObjekt? aussehen, float hoehe, IReadOnlyList<Stueck>? kleidung, string? bewegung, int bild)
    {
        var a = Aussehen.Normalisieren(aussehen);
        if (grafik?.Modell(Modell) is not { } gm)
        {
            return ProbeWelt.Figur();
        }
        var form = bild >= 0 && bewegung is not null && grafik.Modell(BildName(bewegung, bild)) is { } fm && fm.Dreiecke == gm.Dreiecke ? fm : gm;
        var oben = kleidung?.FirstOrDefault(k => k.Slot == "oberteil");
        var hose = kleidung?.FirstOrDefault(k => k.Slot == "hose");
        var schuhe = kleidung?.FirstOrDefault(k => k.Slot == "schuhe");
        var kopfStueck = kleidung?.FirstOrDefault(k => k.Slot == "kopf");
        // Phase 12 Welle 2 (Weisung P12-206): Kleid oder Rock, Jacke oder Mantel ueber dem Oberteil, Frisur und Bart an der 3D-Figur
        var kleid = kleidung?.FirstOrDefault(k => k.Slot == "kleid");
        var jacke = kleidung?.FirstOrDefault(k => k.Slot == "jacke");
        var frisur = Py.Str(a["frisur"]);
        var unter = Probe.Netz.Farbe(oben?.Farbe ?? Oberteil[Py.Str(a["oberteil"])]);
        var hemd = jacke is not null ? Probe.Netz.Farbe(jacke.Farbe) : kleid is { Form: "kleid" } ? Probe.Netz.Farbe(kleid.Farbe) : unter;
        var kurzarm = jacke is null && (kleid is { Form: "kleid" } || oben is { Form: "tshirt" });
        var beine = kleid is not null ? Probe.Netz.Farbe(kleid.Farbe) : (Vector3?)null;
        var kurzeBeine = kleid is not null || hose is { Form: "kurz" };
        var haut = Probe.Netz.Farbe(Haut[Py.Str(a["haut"])]);
        var haar = Probe.Netz.Farbe(Haare[Py.Str(a["haarfarbe"])]);
        var groesse = gm.Max.Y - gm.Min.Y;
        var s = hoehe / groesse;
        var mitte = (gm.Min + gm.Max) / 2;
        var lage = Matrix4x4.CreateTranslation(-mitte.X, -gm.Min.Y, -mitte.Z) * Matrix4x4.CreateScale(s);
        var farben = (byte[])gm.Farben.Clone();
        // A 0.68.0 Nachbesserung (Weisung P12-209): Dreiecke, die ueber einen Saum reichen (kurze Aermel, kurze Hose, Kleid),
        // werden an der Saumhoehe geteilt; so wird der Saum eine gerade Kante statt Zacken aus Haut und Stoff.
        List<float>? zusatzEcken = null;
        List<byte>? zusatzFarben = null;
        var geteilt = new bool[gm.Dreiecke];
        // Arm-Dreiecke aus der Gehbewegung (Weisung P12-209): die Innenseite des Arms liegt naeher an der Mitte als ArmAb
        var arme = SaumAltFuerTests ? null : Arme(grafik, gm);
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            var y = (gm.Ecken[(d * 9) + 1] + gm.Ecken[(d * 9) + 4] + gm.Ecken[(d * 9) + 7]) / 3;
            byte r = farben[d * 3], g = farben[(d * 3) + 1], b = farben[(d * 3) + 2];
            var bereich = Erkennen(r, g, b, (y - gm.Min.Y) / groesse);
            if (bereich == Bereich.Sonst && (hose is not null || schuhe is not null || kleid is not null))
            {
                bereich = Kleidung(r, g, b, (y - gm.Min.Y) / groesse);
            }
            var anteil = (y - gm.Min.Y) / groesse;
            var seitlich = MathF.Abs(((gm.Ecken[d * 9] + gm.Ecken[(d * 9) + 3] + gm.Ecken[(d * 9) + 6]) / 3) - mitte.X) / groesse;
            // A 0.68.0 Nachbesserung (Weisung P12-210, Nico): Kinn, Unterkiefer und Hals vorn tragen in der Vorlage eine
            // dunklere Hautfarbe und dunkle Reste der Koteletten; umgefaerbt wirkte das wie ein Bart. Dort jetzt Haut mit der
            // Helligkeit des Gesichts.
            var vorn = (((gm.Ecken[(d * 9) + 2] + gm.Ecken[(d * 9) + 5] + gm.Ecken[(d * 9) + 8]) / 3) - mitte.Z) / groesse;
            if (!KinnAltFuerTests && Nacken(r, g, b, bereich, anteil, vorn))
            {
                // Helligkeit wie das Haar am Hinterkopf (die Vorlage ist hier heller, umgefaerbt waere das fast weiss)
                bereich = Bereich.Haare;
                r = g = b = NackenGrau;
            }
            if (!KinnAltFuerTests && Kinn(r, g, b, bereich, anteil, seitlich, vorn))
            {
                var k = Vector3.Clamp(haut * GesichtHell, Vector3.Zero, Vector3.One) * 255f;
                farben[d * 3] = (byte)k.X;
                farben[(d * 3) + 1] = (byte)k.Y;
                farben[(d * 3) + 2] = (byte)k.Z;
                continue;
            }
            var arm = arme is not null ? arme[d] : seitlich > ArmAb;
            var (ziel, bezug) = bereich switch
            {
                // kurze Aermel (T-Shirt kurzarm, Kleid): Arm unter der Schulter in Hautfarbe
                Bereich.Hemd when kurzarm && arm && anteil < KurzarmBis => (haut, 212f),
                Bereich.Hemd => (hemd, 212f),
                Bereich.Haut => (haut, 230f),
                Bereich.Haare when kopfStueck is not null && Bedeckt(kopfStueck.Form, anteil) => (Probe.Netz.Farbe(kopfStueck.Farbe), 60f),
                Bereich.Haare when frisur == "glatze" => (haut, 60f),
                Bereich.Haare => (haar, 60f),
                // Kleid, Rock und kurze Hose: Beine unten in Hautfarbe; Kleid und Rock oben in ihrer Farbe
                Bereich.Hose when kurzeBeine && anteil < (kleid is not null ? BeinFreiKleid : BeinFreiKurz) => (haut, 115f),
                Bereich.Hose when beine is { } bf => (bf, 115f),
                Bereich.Hose when hose is not null => (Probe.Netz.Farbe(hose.Farbe), 115f),
                Bereich.Schuhe when schuhe is not null => (Probe.Netz.Farbe(schuhe.Farbe), 215f),
                _ => (Vector3.Zero, 0f),
            };
            // Saum: Hoehe (Anteil) und Farbe ueber dem Saum; unter dem Saum Haut
            (float Hoehe, Vector3 Stoff)? saum = SaumAltFuerTests ? null : bereich switch
            {
                Bereich.Hemd when kurzarm && arm => (KurzarmBis, hemd),
                Bereich.Hose when kurzeBeine => (kleid is not null ? BeinFreiKleid : BeinFreiKurz,
                    beine ?? (hose is not null ? Probe.Netz.Farbe(hose.Farbe) : haut)),
                _ => null,
            };
            if (saum is { } sm && bezug != 0)
            {
                var schnitt = gm.Min.Y + (sm.Hoehe * groesse);
                var y0 = gm.Ecken[(d * 9) + 1];
                var y1 = gm.Ecken[(d * 9) + 4];
                var y2 = gm.Ecken[(d * 9) + 7];
                var ueber = (y0 >= schnitt ? 1 : 0) + (y1 >= schnitt ? 1 : 0) + (y2 >= schnitt ? 1 : 0);
                if (ueber is 1 or 2)
                {
                    zusatzEcken ??= [];
                    zusatzFarben ??= [];
                    geteilt[d] = true;
                    var stoff = Einfaerben(sm.Stoff, r, g, b, bezug);
                    var nackt = Einfaerben(haut, r, g, b, bezug);
                    SaumTeilen(gm.Ecken, form.Ecken, d, schnitt, true, stoff, zusatzEcken, zusatzFarben);
                    SaumTeilen(gm.Ecken, form.Ecken, d, schnitt, false, nackt, zusatzEcken, zusatzFarben);
                    continue;
                }
                (ziel, bezug) = ueber == 3 ? (sm.Stoff, bezug) : (haut, bezug);
            }
            if (bezug == 0)
            {
                continue;
            }
            // Helligkeit der Vorlage behalten (Schatten), Farbe aus dem Aussehen
            var neu = Einfaerben(ziel, r, g, b, bezug);
            farben[d * 3] = neu.R;
            farben[(d * 3) + 1] = neu.G;
            farben[(d * 3) + 2] = neu.B;
        }
        var netz = new Probe.Netz();
        if (zusatzEcken is null)
        {
            netz.Hinzu(new GrafikModell(form.Name, form.Ecken, farben), lage);
        }
        else
        {
            var ecken = new List<float>(form.Ecken.Length + zusatzEcken.Count);
            var fb = new List<byte>(farben.Length + zusatzFarben!.Count);
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                if (geteilt[d])
                {
                    continue;
                }
                for (var i = 0; i < 9; i++)
                {
                    ecken.Add(form.Ecken[(d * 9) + i]);
                }
                fb.Add(farben[d * 3]);
                fb.Add(farben[(d * 3) + 1]);
                fb.Add(farben[(d * 3) + 2]);
            }
            ecken.AddRange(zusatzEcken);
            fb.AddRange(zusatzFarben);
            netz.Hinzu(new GrafikModell(form.Name, [.. ecken], [.. fb]), lage);
        }
        if (Kopf(gm, form) is { } kopf)
        {
            var teile = Gesicht(kopf.Min, kopf.Max, haut);
            if (kopfStueck is not null)
            {
                teile.AddRange(Kopfbedeckung(kopf.Min, kopf.Max, gm.Max.Y, kopfStueck));
            }
            if (oben is { Form: "kapuze" } && kleid is not { Form: "kleid" })
            {
                teile.AddRange(Kapuze(kopf.Min, kopf.Max, unter));
            }
            teile.AddRange(Frisur(kopf.Min, kopf.Max, frisur, haar, kopfStueck));
            teile.AddRange(Bart(kopf.Min, kopf.Max, Aussehen.Wert(a, "bart"), haar));
            if (kleidung?.FirstOrDefault(k => k.Slot == "brille") is { } brille)
            {
                teile.AddRange(Brille(kopf.Min, kopf.Max, Probe.Netz.Farbe(brille.Farbe)));
            }
            teile.AddRange(Koerper(gm, oben, kleid, jacke, kleidung?.FirstOrDefault(k => k.Slot == "tasche"), unter, hemd));
            foreach (var teil in teile)
            {
                netz.Hinzu(teil with
                {
                    X1 = (teil.X1 - mitte.X) * s, Y1 = (teil.Y1 - gm.Min.Y) * s, Z1 = (teil.Z1 - mitte.Z) * s,
                    X2 = (teil.X2 - mitte.X) * s, Y2 = (teil.Y2 - gm.Min.Y) * s, Z2 = (teil.Z2 - mitte.Z) * s,
                });
            }
        }
        return netz;
    }

    /// <summary>Kasten der Haut am Kopf (Haut-Dreiecke oberhalb von 75 % der Hoehe), null ohne Kopf.</summary>
    public static (Vector3 Min, Vector3 Max)? Kopf(GrafikModell gm) => Kopf(gm, gm);

    /// <summary>Kopf-Kasten im Bild <paramref name="form"/>, erkannt an den Dreiecken der stehenden Figur.</summary>
    public static (Vector3 Min, Vector3 Max)? Kopf(GrafikModell gm, GrafikModell form)
    {
        var groesse = gm.Max.Y - gm.Min.Y;
        Vector3? min = null, max = null;
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            var y = (gm.Ecken[(d * 9) + 1] + gm.Ecken[(d * 9) + 4] + gm.Ecken[(d * 9) + 7]) / 3;
            if (Erkennen(gm.Farben[d * 3], gm.Farben[(d * 3) + 1], gm.Farben[(d * 3) + 2], (y - gm.Min.Y) / groesse) != Bereich.Haut
                || (y - gm.Min.Y) / groesse < 0.75f)
            {
                continue;
            }
            for (var e = 0; e < 3; e++)
            {
                var p = new Vector3(form.Ecken[(d * 9) + (e * 3)], form.Ecken[(d * 9) + (e * 3) + 1], form.Ecken[(d * 9) + (e * 3) + 2]);
                min = min is { } a ? Vector3.Min(a, p) : p;
                max = max is { } b ? Vector3.Max(b, p) : p;
            }
        }
        return min is { } mi && max is { } ma ? (mi, ma) : null;
    }

    /// <summary>
    /// Nachtrag 1 zu P7-12 (Wunsch 1, Nico): Gesicht vorn am Kopf (+z) im Massstab des Kopfs: zwei Augen
    /// (dunkel, mit hellem Glanzpunkt) und ein Mund im Hautton, dunkler und roetlicher. Die Vorlage hat ihr
    /// Gesicht nur in der Textur, die die Grafik-Bibliothek nicht mitnimmt.
    /// </summary>
    public static List<Teil> Gesicht(Vector3 min, Vector3 max, Vector3 haut)
    {
        var b = max.X - min.X;
        var h = max.Y - min.Y;
        var vorn = max.Z;
        var t = b * 0.03f;
        var mitteX = (min.X + max.X) / 2;
        var auge = Probe.Netz.Farbe("#2B2233");
        var glanz = Probe.Netz.Farbe("#F4F1FA");
        var mund = Vector3.Clamp((haut * 0.55f) + new Vector3(0.18f, 0.02f, 0.02f), Vector3.Zero, Vector3.One);
        var teile = new List<Teil>();
        var augeY = min.Y + (h * 0.55f);
        var augeB = b * 0.11f;
        var augeH = h * 0.16f;
        foreach (var seite in (float[])[-1f, 1f])
        {
            var x = mitteX + (seite * b * 0.2f);
            teile.Add(new Teil(TeilArt.Quader, x - (augeB / 2), augeY, vorn - t, x + (augeB / 2), augeY + augeH, vorn + t, auge));
            teile.Add(new Teil(TeilArt.Quader, x, augeY + (augeH * 0.6f), vorn + t, x + (augeB * 0.4f), augeY + (augeH * 0.95f), vorn + (t * 1.6f), glanz));
        }
        var mundY = min.Y + (h * 0.3f);
        teile.Add(new Teil(TeilArt.Quader, mitteX - (b * 0.14f), mundY, vorn - t, mitteX + (b * 0.14f), mundY + (h * 0.05f), vorn + t, mund));
        return teile;
    }

    /// <summary>Gruenes Ausrufezeichen ueber einer Person mit Auftrag (wie das "!" der 2D-Ansicht), Ursprung unten.</summary>
    public static Netz Ausrufezeichen(float groesse)
    {
        var gruen = Probe.Netz.Farbe("#34D399");
        var w = groesse * 0.16f;
        return Probe.Netz.Aus(
        [
            new Teil(TeilArt.Quader, -w, groesse * 0.35f, -w, w, groesse, w, gruen),
            new Teil(TeilArt.Quader, -w, 0, -w, w, groesse * 0.22f, w, gruen),
        ]);
    }
    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 2): Kreis am Boden um die Spielfigur in der Stadt, in der Farbe "Kreis"
    /// des Aussehens (RING_COLORS, wie der Kreis im Buero der 2D-Ansicht). Ursprung in der Mitte, flach
    /// auf Hoehe 0; die Flaeche zeichnet ihn ohne Tiefenpruefung, damit ihn kein Haus verdeckt.
    /// </summary>
    public static Netz Ring(PyObjekt? aussehen, float radius, float breite)
    {
        var name = Aussehen.Normalisieren(aussehen)["kreis"] as string ?? "pink";
        var farbe = Probe.Netz.Farbe(KreisFarben.GetValueOrDefault(name, "#F472B6"));
        const int Stuecke = 24;
        var teile = new List<Teil>();
        for (var i = 0; i < Stuecke; i++)
        {
            // Sehnen etwas ueber die Ecke hinaus, damit zwischen den Stuecken keine Luecke bleibt
            float a = i * 2 * MathF.PI / Stuecke, b = (i + 1) * 2 * MathF.PI / Stuecke, mitte = (a + b) / 2, halb = (b - a) / 2 * 1.08f;
            teile.Add(new Teil(TeilArt.Band, radius * MathF.Sin(mitte - halb), 0f, radius * MathF.Cos(mitte - halb),
                radius * MathF.Sin(mitte + halb), breite, radius * MathF.Cos(mitte + halb), farbe));
        }
        return Probe.Netz.Aus(teile);
    }

    /// <summary>Kappe (mit Schirm nach vorn), Muetze (mit Bommel) oder Hut (mit Krempe) auf dem Kopf, Masse nach dem Kopf.</summary>
    public static List<Teil> Kopfbedeckung(Vector3 min, Vector3 max, float oben, Stueck stueck)
    {
        var f = Probe.Netz.Farbe(stueck.Farbe);
        var dunkel = f * 0.75f;
        float b = max.X - min.X, t = max.Z - min.Z, h = max.Y - min.Y, rand = b * 0.08f;
        float x1 = min.X - rand, x2 = max.X + rand, z1 = min.Z - rand, z2 = max.Z + rand;
        var teile = new List<Teil>();
        switch (stueck.Form)
        {
            case "kappe":
                // die Kappe ist das umgefaerbte Haar oben (Bedeckt), dazu der Schirm nach vorn
                teile.Add(new Teil(TeilArt.Quader, min.X + (b * 0.2f), max.Y + (h * 0.12f), max.Z - (t * 0.1f), max.X - (b * 0.2f), max.Y + (h * 0.15f), max.Z + (t * 0.12f), dunkel));
                break;
            case "muetze":
                // Umschlag am Rand und Bommel oben
                teile.AddRange(Rund(x1, z1, x2, z2, max.Y - (h * 0.06f), max.Y + (h * 0.04f), dunkel));
                teile.Add(new Teil(TeilArt.Quader, ((min.X + max.X) / 2) - (b * 0.08f), oben - (h * 0.02f), ((min.Z + max.Z) / 2) - (b * 0.08f),
                    ((min.X + max.X) / 2) + (b * 0.08f), oben + (h * 0.12f), ((min.Z + max.Z) / 2) + (b * 0.08f), Probe.Netz.Farbe("#F8FAFC")));
                break;
            case "spitzhut":
            {
                // Phase 8 W6: Hexenhut (Halloween): breite Krempe, Kegel aus vier Stufen, schraeg nach hinten, Band
                teile.AddRange(Rund(x1 - (b * 0.2f), z1 - (t * 0.2f), x2 + (b * 0.2f), z2 + (t * 0.2f), max.Y - (h * 0.02f), max.Y + (h * 0.03f), f));
                teile.AddRange(Rund(x1, z1, x2, z2, max.Y + (h * 0.03f), max.Y + (h * 0.12f), Probe.Netz.Farbe("#F97316")));
                float cx = (min.X + max.X) / 2, cz = (min.Z + max.Z) / 2;
                for (var i = 0; i < 4; i++)
                {
                    var r = b * (0.42f - (i * 0.09f));
                    var y = max.Y + (h * (0.12f + (i * 0.16f)));
                    var dz = -t * 0.06f * i;
                    teile.Add(new Teil(TeilArt.Quader, cx - r, y, cz + dz - r, cx + r, y + (h * 0.17f), cz + dz + r, f));
                }
                break;
            }
            case "zipfelmuetze":
            {
                // Phase 8 W6: Weihnachtsmuetze: weisser Rand, roter Zipfel zur Seite geneigt, weisser Bommel
                var weiss = Probe.Netz.Farbe("#F8FAFC");
                teile.AddRange(Rund(x1, z1, x2, z2, max.Y - (h * 0.08f), max.Y + (h * 0.05f), weiss));
                float cx = (min.X + max.X) / 2, cz = (min.Z + max.Z) / 2;
                for (var i = 0; i < 4; i++)
                {
                    var r = b * (0.4f - (i * 0.085f));
                    var y = max.Y + (h * (0.05f + (i * 0.09f)));
                    var dx = b * 0.16f * i;
                    teile.Add(new Teil(TeilArt.Quader, cx + dx - r, y, cz - r, cx + dx + r, y + (h * 0.12f), cz + r, f));
                }
                var bx = cx + (b * 0.68f);
                teile.Add(new Teil(TeilArt.Quader, bx - (b * 0.13f), max.Y + (h * 0.18f), cz - (b * 0.13f), bx + (b * 0.13f), max.Y + (h * 0.42f), cz + (b * 0.13f), weiss));
                break;
            }
            case "partyhut":
            {
                // Phase 8 W6: Silvester, bunter Kegel mit Streifen und Bommel
                float cx = (min.X + max.X) / 2, cz = (min.Z + max.Z) / 2;
                var streifen = Probe.Netz.Farbe("#FDE047");
                for (var i = 0; i < 5; i++)
                {
                    var r = b * (0.3f - (i * 0.055f));
                    var y = max.Y + (h * (i * 0.12f)) - (h * 0.04f);
                    teile.Add(new Teil(TeilArt.Quader, cx - r, y, cz - r, cx + r, y + (h * 0.13f), cz + r, i % 2 == 0 ? f : streifen));
                }
                teile.Add(new Teil(TeilArt.Quader, cx - (b * 0.08f), max.Y + (h * 0.6f), cz - (b * 0.08f), cx + (b * 0.08f), max.Y + (h * 0.76f), cz + (b * 0.08f), Probe.Netz.Farbe("#EC4899")));
                break;
            }
            case "hasenohren":
            {
                // Phase 8 W6: Ostern, Haarreif mit zwei langen Ohren (aussen Farbe, innen rosa)
                var rosa = Probe.Netz.Farbe("#F9A8D4");
                float cz = (min.Z + max.Z) / 2;
                teile.Add(new Teil(TeilArt.Quader, x1, max.Y - (h * 0.02f), cz - (t * 0.06f), x2, max.Y + (h * 0.04f), cz + (t * 0.06f), f * 0.85f));
                foreach (var seite in (float[])[-1f, 1f])
                {
                    var x = ((min.X + max.X) / 2) + (seite * b * 0.22f);
                    teile.Add(new Teil(TeilArt.Quader, x - (b * 0.1f), max.Y, cz - (t * 0.07f), x + (b * 0.1f), max.Y + (h * 0.75f), cz + (t * 0.07f), f));
                    teile.Add(new Teil(TeilArt.Quader, x - (b * 0.05f), max.Y + (h * 0.08f), cz + (t * 0.07f), x + (b * 0.05f), max.Y + (h * 0.68f), cz + (t * 0.09f), rosa));
                }
                break;
            }
            default:
                // Hut: Krempe rundum und Band, der Kopf des Huts ist das umgefaerbte Haar
                teile.AddRange(Rund(x1 - (b * 0.14f), z1 - (t * 0.14f), x2 + (b * 0.14f), z2 + (t * 0.14f), max.Y - (h * 0.01f), max.Y + (h * 0.03f), f));
                teile.AddRange(Rund(x1, z1, x2, z2, max.Y + (h * 0.03f), max.Y + (h * 0.09f), dunkel));
                break;
        }
        return teile;
    }

    /// <summary>Kaufhaus (Weisung P8-8c, Hinweis Chat): runde Scheibe aus vier versetzten Quadern (von oben fast rund statt eckig).</summary>
    private static List<Teil> Rund(float x1, float z1, float x2, float z2, float y1, float y2, Vector3 farbe)
    {
        float cx = (x1 + x2) / 2, cz = (z1 + z2) / 2, rx = (x2 - x1) / 2, rz = (z2 - z1) / 2;
        Teil Kasten(float fx, float fz) => new(TeilArt.Quader, cx - (rx * fx), y1, cz - (rz * fz), cx + (rx * fx), y2, cz + (rz * fz), farbe);
        return [Kasten(1f, 0.4f), Kasten(0.4f, 1f), Kasten(0.86f, 0.7f), Kasten(0.7f, 0.86f)];
    }

    /// <summary>Welche Haar-Dreiecke die Kopfbedeckung bedeckt (Hoehe relativ zur Figur).</summary>
    public static bool Bedeckt(string form, float hoeheAnteil) => form switch
    {
        "kappe" => hoeheAnteil > 0.93f,
        "muetze" => hoeheAnteil > 0.86f,
        "spitzhut" or "zipfelmuetze" => hoeheAnteil > 0.88f,
        "partyhut" or "hasenohren" => false,
        _ => hoeheAnteil > 0.89f,
    };

    /// <summary>Kapuze des Kapuzenpullis: Wulst hinten am Hals in der Farbe des Oberteils.</summary>
    public static List<Teil> Kapuze(Vector3 min, Vector3 max, Vector3 farbe)
    {
        float b = max.X - min.X, h = max.Y - min.Y;
        return
        [
            new Teil(TeilArt.Quader, min.X + (b * 0.05f), min.Y - (h * 0.25f), min.Z - (b * 0.18f), max.X - (b * 0.05f), min.Y + (h * 0.25f), min.Z + (b * 0.12f), farbe * 0.9f),
        ];
    }

    /// <summary>Gegenprobe (Weisung P12-209): Locken wie A 0.68.0 vor der Nachbesserung (nur Buckel oben).</summary>
    public static bool LockenAltFuerTests
    {
        get => LockenAlt.Value;
        set => LockenAlt.Value = value;
    }

    private static readonly System.Threading.AsyncLocal<bool> LockenAlt = new();

    /// <summary>Gegenprobe (Weisung P12-209): Saum wie A 0.68.0 vor der Nachbesserung (Farbe je ganzem Dreieck, Zacken).</summary>
    public static bool SaumAltFuerTests
    {
        get => SaumAlt.Value;
        set => SaumAlt.Value = value;
    }

    private static readonly System.Threading.AsyncLocal<bool> SaumAlt = new();

    /// <summary>Weisung P12-210: Gegenprobe, Kinn und Hals wie A 0.68.0 vor der Nachbesserung (dunkler Unterkiefer).</summary>
    internal static bool KinnAltFuerTests { get => KinnAlt.Value; set => KinnAlt.Value = value; }

    private static readonly System.Threading.AsyncLocal<bool> KinnAlt = new();

    /// <summary>Weisung P12-210: Kinn und Hals vorn von dieser Hoehe (Anteil) an ...</summary>
    public const float KinnAb = 0.66f;

    /// <summary>... bis unter diese Hoehe (unter den Augen; darueber Gesicht und Koteletten wie bisher).</summary>
    public const float KinnBis = 0.86f;

    /// <summary>Hoechstens so weit seitlich (Anteil der Hoehe) liegt der Kopf; Schultern und Arme liegen weiter aussen.</summary>
    public const float KinnSeitlich = 0.12f;

    /// <summary>Helligkeit der Haut an Kinn und Hals: wie das Gesicht der Vorlage (Mittel 206 zu Bezug 230, rund 0,9).</summary>
    public const float GesichtHell = 0.9f;

    /// <summary>
    /// Weisung P12-210: gehoert das Dreieck zu Kinn, Unterkiefer oder Hals vorn? Haut, Haar-Reste und hautbraune Teile
    /// (Vorlage ohne Hemd) in der Hoehe <see cref="KinnAb"/> bis <see cref="KinnBis"/>, nah an der Mitte, vor der Mitte.
    /// </summary>
    internal static bool Kinn(byte r, byte g, byte b, Bereich bereich, float anteil, float seitlich, float vorn) =>
        anteil >= KinnAb && anteil < KinnBis
        && ((bereich == Bereich.Haut && vorn > -KinnHinten)
            || (seitlich < KinnSeitlich && vorn > 0
                && (bereich == Bereich.Haare || (bereich == Bereich.Sonst && r - b >= 25 && r >= 90 && g >= 60))));

    /// <summary>
    /// Weisung P12-210: Haut am Unterkiefer auch seitlich bis knapp hinter das Ohr (Anteil der Hoehe hinter der Mitte); in der
    /// Seitenansicht war der Unterkiefer sonst grau wie ein Bartschatten.
    /// </summary>
    public const float KinnHinten = 0.08f;

    /// <summary>
    /// Weisung P12-210: dunkle Reste am Hinterkopf unter dem Haaransatz (Anteil 0,66 bis 0,82, hinter der Mitte, Mittel unter
    /// diesem Wert) sind Haar. Vorher blieben sie in der Farbe der Vorlage (dunkelbraun, bei Glatze ein schwarzer Fleck hinter
    /// dem Ohr, bei blonden Haaren ein dunkler Nacken).
    /// </summary>
    public const int NackenDunkel = 140;

    /// <summary>Weisung P12-210: Helligkeit dieser Dreiecke wie das Haar am Hinterkopf (Mittel der Vorlage dort rund 45).</summary>
    public const byte NackenGrau = 45;

    /// <summary>Weisung P12-210: gehoert das Dreieck zum Haar am Hinterkopf unter dem Haaransatz?</summary>
    internal static bool Nacken(byte r, byte g, byte b, Bereich bereich, float anteil, float vorn) =>
        bereich == Bereich.Sonst && anteil >= KinnAb && vorn < 0 && (r + g + b) / 3 < NackenDunkel;

    /// <summary>Farbe aus dem Aussehen mit der Helligkeit der Vorlage (Schatten).</summary>
    private static (byte R, byte G, byte B) Einfaerben(Vector3 ziel, byte r, byte g, byte b, float bezug)
    {
        var hell = Math.Clamp((r + g + b) / 3f / bezug, 0.55f, 1.25f);
        var neu = Vector3.Clamp(ziel * hell, Vector3.Zero, Vector3.One) * 255f;
        return ((byte)neu.X, (byte)neu.Y, (byte)neu.Z);
    }

    /// <summary>
    /// Weisung P12-209: Teil des Dreiecks <paramref name="d"/> ueber (<paramref name="oben"/>) bzw. unter der Hoehe
    /// <paramref name="schnitt"/> der stehenden Figur (<paramref name="ruhe"/>); die Teilpunkte liegen bei demselben Anteil der
    /// Kante auch im gezeigten Bild (<paramref name="bild"/>), so wandert der Saum mit dem Arm. Reihenfolge der Ecken bleibt
    /// (gleiche Vorderseite), das Vieleck (3 oder 4 Ecken) wird als Faecher in Dreiecke zerlegt.
    /// </summary>
    internal static void SaumTeilen(float[] ruhe, float[] bild, int d, float schnitt, bool oben, (byte R, byte G, byte B) farbe,
        List<float> ecken, List<byte> farben)
    {
        Span<Vector3> punkte = stackalloc Vector3[4];
        var n = 0;
        for (var i = 0; i < 3; i++)
        {
            var j = (i + 1) % 3;
            var yi = ruhe[(d * 9) + (i * 3) + 1];
            var yj = ruhe[(d * 9) + (j * 3) + 1];
            var innenI = oben ? yi >= schnitt : yi < schnitt;
            var innenJ = oben ? yj >= schnitt : yj < schnitt;
            var pi = new Vector3(bild[(d * 9) + (i * 3)], bild[(d * 9) + (i * 3) + 1], bild[(d * 9) + (i * 3) + 2]);
            var pj = new Vector3(bild[(d * 9) + (j * 3)], bild[(d * 9) + (j * 3) + 1], bild[(d * 9) + (j * 3) + 2]);
            if (innenI)
            {
                punkte[n++] = pi;
            }
            if (innenI != innenJ)
            {
                punkte[n++] = Vector3.Lerp(pi, pj, (schnitt - yi) / (yj - yi));
            }
        }
        for (var k = 1; k + 1 < n; k++)
        {
            foreach (var p in (ReadOnlySpan<Vector3>)[punkte[0], punkte[k], punkte[k + 1]])
            {
                ecken.Add(p.X);
                ecken.Add(p.Y);
                ecken.Add(p.Z);
            }
            farben.Add(farbe.R);
            farben.Add(farbe.G);
            farben.Add(farbe.B);
        }
    }

    /// <summary>Phase 12 Welle 2: Arm = seitlich weiter als dieser Anteil der Hoehe von der Mitte (Rumpf liegt innen).</summary>
    public const float ArmAb = 0.125f;

    /// <summary>
    /// Weisung P12-209: ein Dreieck gehoert zum Arm, wenn es sich in den Bildern der Gehbewegung um mehr als diesen Anteil der
    /// Hoehe nach vorn oder hinten bewegt (der Rumpf bewegt sich dort hoechstens 0,006, der Arm ab 0,035).
    /// </summary>
    public const float ArmBewegt = 0.02f;

    private static readonly ConditionalWeakTable<GrafikModell, bool[]?> ArmeVorrat = new();

    /// <summary>
    /// Weisung P12-209: Arm-Dreiecke der stehenden Figur, erkannt an der Gehbewegung (der Arm schwingt, der Rumpf nicht). Die
    /// Innenseite des Arms liegt naeher an der Mitte als <see cref="ArmAb"/>; mit der Regel nach der Lage blieb sie unter dem
    /// kurzen Aermel in Stoff (Zacken). Ohne Bilder der Gehbewegung null (dann gilt die Lage).
    /// </summary>
    public static bool[]? Arme(GrafikBibliothek grafik, GrafikModell gm) => ArmeVorrat.GetValue(gm, m =>
    {
        var bilder = Enumerable.Range(0, Bilder).Select(b => grafik.Modell(BildName(Gehen, b)))
            .Where(b => b is not null && b.Dreiecke == m.Dreiecke).ToList();
        if (bilder.Count == 0)
        {
            return null;
        }
        var groesse = m.Max.Y - m.Min.Y;
        var arme = new bool[m.Dreiecke];
        for (var d = 0; d < m.Dreiecke; d++)
        {
            var z = (m.Ecken[(d * 9) + 2] + m.Ecken[(d * 9) + 5] + m.Ecken[(d * 9) + 8]) / 3;
            float von = 0f, bis = 0f;
            foreach (var b in bilder)
            {
                var dz = ((b!.Ecken[(d * 9) + 2] + b.Ecken[(d * 9) + 5] + b.Ecken[(d * 9) + 8]) / 3) - z;
                von = MathF.Min(von, dz);
                bis = MathF.Max(bis, dz);
            }
            arme[d] = (bis - von) / groesse > ArmBewegt;
        }
        return arme;
    });

    /// <summary>Phase 12 Welle 2: kurze Aermel enden bei diesem Anteil der Hoehe (darunter Haut).</summary>
    public const float KurzarmBis = 0.53f;

    /// <summary>Phase 12 Welle 2: Beine frei unter diesem Anteil bei kurzer Hose bzw. bei Kleid und Rock.</summary>
    public const float BeinFreiKurz = 0.25f;

    /// <inheritdoc cref="BeinFreiKurz"/>
    public const float BeinFreiKleid = 0.30f;

    /// <summary>
    /// Phase 12 Welle 2 (Weisung P12-206, Nico 3a): die Frisur aus dem Aussehen auch an der 3D-Figur. „kurz“ ist das Haar der
    /// Vorlage, „glatze“ faerbt es in die Haut (im Farbdurchlauf); lang (Haar bis zur Schulter), Zopf (hinten) und Locken (Buckel
    /// oben) sind angesetzte Quader in der Haarfarbe. Locken unter einer Kopfbedeckung fallen weg (sie wuerden durchstechen).
    /// </summary>
    public static List<Teil> Frisur(Vector3 min, Vector3 max, string frisur, Vector3 haar, Stueck? kopfStueck)
    {
        float kb = max.X - min.X, kh = max.Y - min.Y, kt = max.Z - min.Z, kx = (min.X + max.X) / 2, kz = (min.Z + max.Z) / 2;
        var teile = new List<Teil>();
        switch (frisur)
        {
            case "lang":
                // hinten schmaler als der Kopf, seitlich nicht ganz bis hinten: die Figur bleibt im Halbmesser (SpielAnsprechenTests)
                teile.Add(new Teil(TeilArt.Quader, kx - (kb * 0.42f), min.Y - (kh * 0.55f), min.Z - (kt * 0.03f), kx + (kb * 0.42f), max.Y + (kh * 0.04f), kz - (kt * 0.05f), haar));
                foreach (var s in (float[])[-1f, 1f])
                {
                    teile.Add(new Teil(TeilArt.Quader, s < 0 ? min.X - (kb * 0.04f) : max.X - (kb * 0.02f), min.Y - (kh * 0.35f), min.Z + (kt * 0.1f),
                        s < 0 ? min.X + (kb * 0.02f) : max.X + (kb * 0.04f), max.Y, kz + (kt * 0.3f), haar));
                }
                break;
            case "zopf":
                teile.Add(new Teil(TeilArt.Quader, kx - (kb * 0.12f), min.Y + (kh * 0.45f), min.Z - (kt * 0.12f), kx + (kb * 0.12f), min.Y + (kh * 0.75f), min.Z + (kt * 0.02f), haar * 0.95f));
                teile.Add(new Teil(TeilArt.Quader, kx - (kb * 0.08f), min.Y - (kh * 0.45f), min.Z - (kt * 0.11f), kx + (kb * 0.08f), min.Y + (kh * 0.5f), min.Z + (kt * 0.02f), haar));
                break;
            case "locken" when kopfStueck is null || kopfStueck.Form is "partyhut" or "hasenohren":
                for (var i = 0; i < 5; i++)
                {
                    for (var j = 0; j < 3; j++)
                    {
                        var x = min.X + (kb * (0.1f + (0.2f * i)));
                        var z = min.Z + (kt * (0.15f + (0.35f * j)));
                        teile.Add(new Teil(TeilArt.Quader, x - (kb * 0.09f), max.Y - (kh * 0.02f), z - (kt * 0.1f), x + (kb * 0.09f),
                            max.Y + (kh * (0.12f + (0.04f * ((i + j) % 2)))), z + (kt * 0.1f), haar * (0.9f + (0.1f * ((i + j) % 2)))));
                    }
                }
                if (LockenAltFuerTests)
                {
                    break;
                }
                // Weisung P12-209: Locken auch an den Seiten, hinten und als Stirnlocken, damit sie von vorn und von der Seite
                // klar anders aussehen als kurzes Haar (abwechselnd hell und dunkel, versetzt in der Hoehe)
                for (var j = 0; j < 3; j++)
                {
                    var z = min.Z + (kt * (0.3f + (0.2f * j)));
                    var y = max.Y - (kh * (0.3f - (0.08f * (j % 2))));
                    foreach (var links in new[] { true, false })
                    {
                        var x1 = links ? min.X - (kb * 0.04f) : max.X - (kb * 0.06f);
                        teile.Add(new Teil(TeilArt.Quader, x1, y, z - (kt * 0.11f), x1 + (kb * 0.1f), y + (kh * 0.24f), z + (kt * 0.09f),
                            haar * (j % 2 == 0 ? 0.85f : 1.05f)));
                    }
                }
                for (var i = 0; i < 4; i++)
                {
                    var x = min.X + (kb * (0.2f + (0.2f * i)));
                    var y = max.Y - (kh * (0.42f - (0.08f * (i % 2))));
                    teile.Add(new Teil(TeilArt.Quader, x - (kb * 0.11f), y, min.Z - (kt * 0.04f), x + (kb * 0.11f), y + (kh * 0.36f), min.Z + (kt * 0.06f),
                        haar * (i % 2 == 0 ? 0.85f : 1.05f)));
                    var yv = max.Y - (kh * (0.14f + (0.04f * (i % 2))));
                    teile.Add(new Teil(TeilArt.Quader, x - (kb * 0.09f), yv, max.Z - (kt * 0.05f), x + (kb * 0.09f), max.Y + (kh * 0.02f), max.Z + (kt * 0.03f),
                        haar * (i % 2 == 0 ? 1.05f : 0.85f)));
                }
                break;
        }
        return teile;
    }

    /// <summary>Phase 12 Welle 2: Vollbart (Kinn und Wangen mit Oberlippe) oder Schnurrbart, in der Haarfarbe, vorn am Kopf.</summary>
    public static List<Teil> Bart(Vector3 min, Vector3 max, string bart, Vector3 haar)
    {
        float kb = max.X - min.X, kh = max.Y - min.Y, kt = max.Z - min.Z, kx = (min.X + max.X) / 2, kz = (min.Z + max.Z) / 2;
        var farbe = haar * 0.95f;
        var oberlippe = new Teil(TeilArt.Quader, kx - (kb * 0.2f), min.Y + (kh * 0.36f), max.Z, kx + (kb * 0.2f), min.Y + (kh * 0.42f), max.Z + (kt * 0.06f), farbe);
        return bart switch
        {
            // Wangen bis zur Vorderseite des Kopfs (unter dem Kinn sichtbar), vorn schmaler: im Halbmesser der Figur
            "vollbart" =>
            [
                new Teil(TeilArt.Quader, min.X + (kb * 0.08f), min.Y - (kh * 0.08f), kz, max.X - (kb * 0.08f), min.Y + (kh * 0.27f), max.Z - (kt * 0.01f), farbe),
                new Teil(TeilArt.Quader, kx - (kb * 0.3f), min.Y - (kh * 0.06f), kz, kx + (kb * 0.3f), min.Y + (kh * 0.25f), max.Z + (kt * 0.05f), farbe),
                oberlippe,
            ],
            "schnurrbart" => [oberlippe],
            _ => [],
        };
    }

    /// <summary>Phase 12 Welle 2: Brille, zwei Rahmen vor den Augen, Steg und Buegel bis zu den Ohren.</summary>
    public static List<Teil> Brille(Vector3 min, Vector3 max, Vector3 f)
    {
        float kb = max.X - min.X, kh = max.Y - min.Y, kt = max.Z - min.Z, kx = (min.X + max.X) / 2, kz = (min.Z + max.Z) / 2;
        var y = min.Y + (kh * 0.5f);
        var v = max.Z + (kt * 0.04f);
        var teile = new List<Teil>();
        foreach (var s in (float[])[-1f, 1f])
        {
            var x = kx + (s * kb * 0.2f);
            teile.Add(new Teil(TeilArt.Quader, x - (kb * 0.14f), y, v, x + (kb * 0.14f), y + (kh * 0.035f), v + (kt * 0.02f), f));
            teile.Add(new Teil(TeilArt.Quader, x - (kb * 0.14f), y + (kh * 0.22f), v, x + (kb * 0.14f), y + (kh * 0.255f), v + (kt * 0.02f), f));
            teile.Add(new Teil(TeilArt.Quader, x - (kb * 0.14f), y, v, x - (kb * 0.11f), y + (kh * 0.255f), v + (kt * 0.02f), f));
            teile.Add(new Teil(TeilArt.Quader, x + (kb * 0.11f), y, v, x + (kb * 0.14f), y + (kh * 0.255f), v + (kt * 0.02f), f));
            // Buegel: vom Rahmen zur Kopfseite, dann an der Seite nach hinten (im Halbmesser der Figur)
            teile.Add(new Teil(TeilArt.Quader, s < 0 ? kx - (kb * 0.36f) : kx + (kb * 0.3f), y + (kh * 0.2f), max.Z,
                s < 0 ? kx - (kb * 0.3f) : kx + (kb * 0.36f), y + (kh * 0.24f), v, f));
            teile.Add(new Teil(TeilArt.Quader, s < 0 ? kx - (kb * 0.47f) : kx + (kb * 0.3f), y + (kh * 0.2f), max.Z - (kt * 0.1f),
                s < 0 ? kx - (kb * 0.3f) : kx + (kb * 0.47f), y + (kh * 0.24f), max.Z, f));
            teile.Add(new Teil(TeilArt.Quader, s < 0 ? min.X - (kb * 0.01f) : max.X - (kb * 0.01f), y + (kh * 0.2f), kz,
                s < 0 ? min.X + (kb * 0.01f) : max.X + (kb * 0.01f), y + (kh * 0.24f), max.Z - (kt * 0.1f), f));
        }
        teile.Add(new Teil(TeilArt.Quader, kx - (kb * 0.06f), y + (kh * 0.2f), v, kx + (kb * 0.06f), y + (kh * 0.24f), v + (kt * 0.02f), f));
        return teile;
    }

    /// <summary>
    /// Phase 12 Welle 2: Teile am Koerper der stehenden Figur (Masse aus Huefte und Rumpf der Vorlage): Rock und Kleid als
    /// Glocke aus drei Stufen, Mantel bis zum Knie mit Knopfleiste und Kragen, offene Jacke mit dem Oberteil darunter als
    /// Streifen vorn und Saum, langes Hemd mit Knopfleiste und Kragen, Handtasche am Arm. Sie bewegen sich beim Gehen nicht
    /// mit (wie Hut und Kappe, Kurzplan P12-118).
    /// </summary>
    public static List<Teil> Koerper(GrafikModell gm, Stueck? oben, Stueck? kleid, Stueck? jacke, Stueck? tasche, Vector3 unter, Vector3 hemd)
    {
        var teile = new List<Teil>();
        if (oben is null && kleid is null && jacke is null && tasche is null)
        {
            return teile;
        }
        float g = gm.Max.Y - gm.Min.Y, cx = (gm.Min.X + gm.Max.X) / 2;
        float Y(float a) => gm.Min.Y + (a * g);
        Vector3 hMin = new(float.MaxValue), hMax = new(float.MinValue), rMin = new(float.MaxValue), rMax = new(float.MinValue);
        for (var d = 0; d < gm.Dreiecke; d++)
        {
            byte r = gm.Farben[d * 3], gg = gm.Farben[(d * 3) + 1], b = gm.Farben[(d * 3) + 2];
            for (var e = 0; e < 3; e++)
            {
                var p = new Vector3(gm.Ecken[(d * 9) + (e * 3)], gm.Ecken[(d * 9) + (e * 3) + 1], gm.Ecken[(d * 9) + (e * 3) + 2]);
                var a = (p.Y - gm.Min.Y) / g;
                if (Kleidung(r, gg, b, a) == Bereich.Hose && a > BeinFreiKurz)
                {
                    hMin = Vector3.Min(hMin, p);
                    hMax = Vector3.Max(hMax, p);
                }
                if (Erkennen(r, gg, b, a) == Bereich.Hemd && MathF.Abs(p.X - cx) / g < ArmAb - 0.02f)
                {
                    rMin = Vector3.Min(rMin, p);
                    rMax = Vector3.Max(rMax, p);
                }
            }
        }
        if (hMin.X > hMax.X || rMin.X > rMax.X)
        {
            return teile;
        }
        float hz = (hMin.Z + hMax.Z) / 2, hw = (hMax.X - hMin.X) / 2, ht = (hMax.Z - hMin.Z) / 2;
        void Glocke(float von, float bis, float weit, Vector3 f)
        {
            for (var i = 0; i < 3; i++)
            {
                float a1 = von - ((von - bis) * i / 3f), a2 = von - ((von - bis) * (i + 1) / 3f);
                var w = hw * (1.03f + (weit * (i + 1) / 3f));
                var t = ht * (1.08f + (weit * (i + 1) / 3f));
                teile.Add(new Teil(TeilArt.Quader, cx - w, Y(a2), hz - t, cx + w, Y(a1), hz + t, f * (1f - (0.05f * i))));
            }
        }
        switch (kleid?.Form)
        {
            case "rock":
                Glocke(0.37f, 0.22f, 0.22f, Probe.Netz.Farbe(kleid.Farbe));
                break;
            case "kleid":
                Glocke(0.37f, 0.20f, 0.28f, Probe.Netz.Farbe(kleid.Farbe));
                break;
        }
        switch (jacke?.Form)
        {
            case "mantel":
                Glocke(0.40f, 0.16f, 0.12f, hemd);
                teile.Add(new Teil(TeilArt.Quader, cx - (hw * 0.12f), Y(0.17f), rMax.Z, cx + (hw * 0.12f), Y(0.66f), rMax.Z + (g * 0.012f), hemd * 0.7f));
                teile.Add(new Teil(TeilArt.Quader, rMin.X - (g * 0.01f), Y(0.645f), rMin.Z - (g * 0.01f), rMax.X + (g * 0.01f), Y(0.68f), rMax.Z + (g * 0.01f), hemd * 0.8f));
                break;
            case "jacke":
                teile.Add(new Teil(TeilArt.Quader, cx - (hw * 0.25f), Y(0.36f), rMax.Z - (g * 0.002f), cx + (hw * 0.25f), Y(0.655f), rMax.Z + (g * 0.01f), unter));
                teile.Add(new Teil(TeilArt.Quader, rMin.X - (g * 0.008f), Y(0.355f), rMin.Z - (g * 0.008f), rMax.X + (g * 0.008f), Y(0.385f), rMax.Z + (g * 0.008f), hemd * 0.75f));
                break;
            case null when oben is { Form: "hemd_lang" } && kleid is not { Form: "kleid" }:
                teile.Add(new Teil(TeilArt.Quader, cx - (hw * 0.05f), Y(0.40f), rMax.Z, cx + (hw * 0.05f), Y(0.66f), rMax.Z + (g * 0.006f), hemd * 0.8f));
                teile.Add(new Teil(TeilArt.Quader, rMin.X + ((rMax.X - rMin.X) * 0.15f), Y(0.655f), rMin.Z + ((rMax.Z - rMin.Z) * 0.2f),
                    rMax.X - ((rMax.X - rMin.X) * 0.15f), Y(0.69f), rMax.Z + (g * 0.012f), hemd * 1.08f));
                break;
        }
        if (tasche is not null)
        {
            var f = Probe.Netz.Farbe(tasche.Farbe);
            // unter der Hand, innerhalb des Halbmessers der Figur (SpielAnsprechenTests)
            var x = cx + (g * 0.145f);
            teile.Add(new Teil(TeilArt.Quader, x - (g * 0.015f), Y(0.24f), hz - (g * 0.045f), x + (g * 0.015f), Y(0.31f), hz + (g * 0.045f), f));
            teile.Add(new Teil(TeilArt.Quader, x + (g * 0.01f), Y(0.31f), hz - (g * 0.006f), x + (g * 0.017f), Y(0.36f), hz + (g * 0.006f), f * 0.7f));
        }
        return teile;
    }
}
