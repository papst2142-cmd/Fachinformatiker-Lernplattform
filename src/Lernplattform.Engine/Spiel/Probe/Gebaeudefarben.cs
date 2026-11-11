using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Lernplattform.Core.Daten;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Sammelliste 264 (Weisung P12-130 Frage 3 = c, Kurzplan Meldung P12-83 Abschnitt 3): Gebaeude faerben. Daten in
/// <c>stadtdaten.farben</c> <c>{Kennung: {fassade, dach, rahmen}}</c> mit Werten <c>"#RRGGBB"</c>; Kennung = Ort (Haus eines
/// Orts) oder Modellname eines Gebaeudes ohne Ort (auch eigene Gebaeude „bau:…“, dann alle Gebaeude dieses Modells).
/// Ohne Feld (Fassung 0) bleibt alles bitgleich.
/// <para>
/// Ein Teil ist eine Farbfamilie: der haeufigste Ton der Waende (Fassade), der Dachflaechen (Dach) bzw. der uebrigen
/// Wandflaechen (Rahmen) und alle Toene bis <see cref="FamilienAbstand"/> davon. Jeder Ton der Familie bekommt die neue
/// Farbe mit seiner eigenen Helligkeit im Verhaeltnis zum Hauptton (keine Flecken). Umgefaerbt werden nur die Eckfarben;
/// die Farbe des Dreiecks bleibt, darum erkennen Glas, Licht (Leuchtmarke), Pflanzen und Schnee alles wie vorher. Glas,
/// Pflanzen, das Tuerblatt und sehr dunkle Flaechen gehoeren nie zu einer Familie. Kein neuer Zeichenaufruf.
/// </para>
/// </summary>
public static class Gebaeudefarben
{
    /// <summary>Schluessel in <c>stadtdaten</c>.</summary>
    public const string Schluessel = "farben";

    /// <summary>Die drei Teile in fester Reihenfolge.</summary>
    public static readonly string[] Teile = ["fassade", "dach", "rahmen"];

    /// <summary>Anzeigename eines Teils.</summary>
    public static string TeilName(string teil) => teil switch { "fassade" => "Fassade", "dach" => "Dach", "rahmen" => "Rahmen", TuerSchluessel => "Tür", _ => teil };

    /// <summary>
    /// Nico 10.11.2026 (zu Meldung P12-92): die Tuer in vier Farben passend zum Haus. Wert in <c>farben[Kennung].tuer</c> =
    /// Kennung der Variante; ohne Wert Braun. Gilt fuer das Tuerblatt der Gebaeude aus dem Editor und den Rahmen der
    /// gezeichneten Tuer (Kastentuer). Sammelliste 292 (Weisung P12-196 Frage 4 = a): ist eine Farbe gewaehlt, faerbt sie auch das
    /// Tuerblatt der benutzten Tuer eines Stadt-Modells (Rahmen und Fenster bleiben); ohne Wahl bleibt diese Tuer wie im Modell.
    /// </summary>
    public const string TuerSchluessel = "tuer";

    /// <summary>Die vier Tuer-Farben (Kennung, Name, Wert). Werte so gewaehlt, dass kein Modell genau diese Farbe hat.</summary>
    public static readonly (string Id, string Name, string Wert)[] TuerVarianten =
    [
        ("braun", "Braun", "#4A3426"), ("schwarz", "Schwarz", "#1D1D21"), ("weiss", "Weiß", "#EBEBE7"), ("creme", "Cremefarben", "#E6D9BC"),
    ];

    /// <summary>Alle Schluessel eines Eintrags (drei Farbteile und die Tuer).</summary>
    public static readonly string[] AlleSchluessel = [.. Teile, TuerSchluessel];

    /// <summary>Passt <paramref name="wert"/> zum Schluessel <paramref name="teil"/> (Farbe bzw. Tuer-Variante)?</summary>
    public static bool WertGueltig(string teil, object? wert) =>
        teil == TuerSchluessel ? TuerGueltig(wert) : Teile.Contains(teil) && Gueltig(wert);

    /// <summary>Gueltige Kennung einer Tuer-Variante?</summary>
    public static bool TuerGueltig(object? wert) => wert is string s && TuerVarianten.Any(v => v.Id == s);

    /// <summary>Tuer-Farbe eines Gebaeudes (null = keine Angabe, dann wie bisher).</summary>
    public static Vector3? Tuer(PyObjekt? stadtdaten, string kennung) =>
        stadtdaten is not null && Get(stadtdaten, Schluessel) is PyObjekt alle && Get(alle, kennung) is PyObjekt e && Get(e, TuerSchluessel) is string id
            && TuerVarianten.FirstOrDefault(v => v.Id == id) is { Wert: not null } v ? Netz.Farbe(v.Wert) : null;

    /// <summary>Die 12 festen Farben (Name, Wert), in dieser Reihenfolge in Werkzeug und Editor.</summary>
    public static readonly (string Name, string Wert)[] Feste =
    [
        ("Weiß", "#F2EFE8"), ("Sand", "#E3CFA0"), ("Ocker", "#C99A3E"), ("Ziegelrot", "#A8452E"),
        ("Rosa", "#D99A9A"), ("Braun", "#7A5236"), ("Hellgrau", "#B9BCC0"), ("Anthrazit", "#4A4E54"),
        ("Hellblau", "#8FB4D6"), ("Blau", "#3D5F8F"), ("Salbeigrün", "#9DB38A"), ("Tannengrün", "#3E6B48"),
    ];

    /// <summary>Hoechster Abstand (RGB 0..1) eines Tons vom Hauptton, damit er zur Familie gehoert.</summary>
    public const float FamilienAbstand = 0.13f;

    /// <summary>Flaechen dunkler als das (Helligkeit 0..1) gehoeren nie zu einer Familie (Tueren, Schatten, Fugen).</summary>
    public const float Dunkel = 0.1f;

    /// <summary>Eine Rahmen-Familie braucht mindestens diesen Anteil der Wandflaeche (sonst gibt es keinen Rahmen).</summary>
    public const float RahmenMindestens = 0.02f;

    /// <summary>Gegenprobe (nur Tests, je Ablauf): nur der Hauptton wird ersetzt, ohne Familie und Helligkeit (fleckig).</summary>
    public static bool NurHaupttonFuerTests { get => NurHauptton.Value; set => NurHauptton.Value = value; }
    private static readonly AsyncLocal<bool> NurHauptton = new();

    /// <summary>Ist <paramref name="wert"/> eine Farbe <c>#RRGGBB</c>?</summary>
    public static bool Gueltig(object? wert) =>
        wert is string s && s.Length == 7 && s[0] == '#' && s.Skip(1).All(Uri.IsHexDigit);

    /// <summary>Wert als Farbe (0..1), null wenn ungueltig.</summary>
    public static Vector3? Farbe(object? wert) => Gueltig(wert) ? Netz.Farbe((string)wert!) : null;

    /// <summary>Farbe als <c>#RRGGBB</c> (Grossbuchstaben).</summary>
    public static string Wert(Vector3 f) => string.Create(CultureInfo.InvariantCulture,
        $"#{Byte(f.X):X2}{Byte(f.Y):X2}{Byte(f.Z):X2}");

    private static int Byte(float v) => (int)MathF.Round(Math.Clamp(v, 0f, 1f) * 255f);

    /// <summary>Farbton (0..360), Saettigung und Helligkeit (0..1) einer Farbe (HSV, fuer die Regler am Tablet).</summary>
    public static (float Ton, float Saettigung, float Hell) Hsv(Vector3 f)
    {
        var max = MathF.Max(f.X, MathF.Max(f.Y, f.Z));
        var min = MathF.Min(f.X, MathF.Min(f.Y, f.Z));
        var d = max - min;
        var ton = d <= 0 ? 0 : max == f.X ? 60 * (((f.Y - f.Z) / d) % 6) : max == f.Y ? 60 * (((f.Z - f.X) / d) + 2) : 60 * (((f.X - f.Y) / d) + 4);
        return (ton < 0 ? ton + 360 : ton, max <= 0 ? 0 : d / max, max);
    }

    /// <summary>Farbe aus Farbton (0..360), Saettigung und Helligkeit (0..1).</summary>
    public static Vector3 AusHsv(float ton, float saettigung, float hell)
    {
        ton = ((ton % 360) + 360) % 360;
        var c = hell * saettigung;
        var x = c * (1 - MathF.Abs((ton / 60 % 2) - 1));
        var m = hell - c;
        var (r, g, b) = ton switch
        {
            < 60 => (c, x, 0f),
            < 120 => (x, c, 0f),
            < 180 => (0f, c, x),
            < 240 => (0f, x, c),
            < 300 => (x, 0f, c),
            _ => (c, 0f, x),
        };
        return new Vector3(r + m, g + m, b + m);
    }

    /// <summary>Gelesene Farben je Kennung (nur gueltige Werte; Fehler meldet die Pruefung rot).</summary>
    public static Dictionary<string, Vector3?[]> Lesen(PyObjekt? stadtdaten)
    {
        var ergebnis = new Dictionary<string, Vector3?[]>(StringComparer.Ordinal);
        if (stadtdaten is null || Get(stadtdaten, Schluessel) is not PyObjekt alle)
        {
            return ergebnis;
        }
        foreach (var (kennung, wert) in alle)
        {
            if (wert is not PyObjekt teile)
            {
                continue;
            }
            var f = Teile.Select(t => Farbe(Get(teile, t))).ToArray();
            if (f.Any(x => x is not null))
            {
                ergebnis[kennung] = f;
            }
        }
        return ergebnis;
    }

    /// <summary>Haupttoene eines Hauses (Fassade, Dach, Rahmen; null = keiner).</summary>
    public sealed record Haupttoene(Vector3? Fassade, Vector3? Dach, Vector3? Rahmen);

    private static bool Ausgenommen(Vector3 f) =>
        StadtTueren.IstGlas(f) || StadtTueren.IstTuerblatt(f) || Jahreszeitfarben.IstPflanze(f) || Helligkeit(f) < Dunkel;

    private static float Helligkeit(Vector3 f) => (0.2126f * f.X) + (0.7152f * f.Y) + (0.0722f * f.Z);

    /// <summary>Normale (y-Anteil, Laenge 1) und Flaeche eines Dreiecks im Modell, Hoehe der Mitte.</summary>
    private static (float Ny, float Flaeche, float Y) Lage(GrafikModell m, int d)
    {
        var e = m.Ecken;
        var a = new Vector3(e[d * 9], e[(d * 9) + 1], e[(d * 9) + 2]);
        var b = new Vector3(e[(d * 9) + 3], e[(d * 9) + 4], e[(d * 9) + 5]);
        var c = new Vector3(e[(d * 9) + 6], e[(d * 9) + 7], e[(d * 9) + 8]);
        var k = Vector3.Cross(b - a, c - a);
        var l = k.Length();
        return l < 1e-12f ? (0, 0, 0) : (k.Y / l, l / 2, (a.Y + b.Y + c.Y) / 3);
    }

    /// <summary>Wand (fast senkrecht)?</summary>
    private static bool IstWand(float ny) => MathF.Abs(ny) < 0.3f;

    /// <summary>Dach (nach oben, in der oberen Haelfte des Hauses)?</summary>
    private static bool IstDach(float ny, float y, float oben) => ny > 0.3f && y >= oben * 0.5f;

    /// <summary>
    /// Haupttoene eines Hauses aus seinen Modellen (Flaeche je Ton, Ton mit der groessten Flaeche samt Familie gewinnt).
    /// <paramref name="modelle"/>: Modell und Skalierung (Flaechenfaktor) je Stueck.
    /// </summary>
    public static Haupttoene Bestimmen(IReadOnlyList<(GrafikModell Modell, float Faktor, float Unten)> modelle)
    {
        var oben = modelle.Count == 0 ? 0 : modelle.Max(m => (m.Modell.Max.Y * m.Faktor) + m.Unten);
        var wand = new Dictionary<int, float>();
        var dach = new Dictionary<int, float>();
        foreach (var (m, faktor, unten) in modelle)
        {
            for (var d = 0; d < m.Dreiecke; d++)
            {
                var f = m.Farbe(d);
                if (Ausgenommen(f))
                {
                    continue;
                }
                var (ny, a, y) = Lage(m, d);
                var schluessel = Schluessel3(m, d);
                if (IstWand(ny))
                {
                    wand[schluessel] = wand.GetValueOrDefault(schluessel) + (a * faktor * faktor);
                }
                else if (IstDach(ny, (y * faktor) + unten, oben))
                {
                    dach[schluessel] = dach.GetValueOrDefault(schluessel) + (a * faktor * faktor);
                }
            }
        }
        var fassade = Haupt(wand, null);
        var dachTon = Haupt(dach, null);
        Vector3? rahmen = null;
        if (fassade is { } fa)
        {
            var rest = wand.Where(w => Vector3.Distance(Ton(w.Key), fa) > FamilienAbstand).ToDictionary(w => w.Key, w => w.Value);
            var gesamt = wand.Values.Sum();
            if (Haupt(rest, null) is { } r && Familie(rest, r) >= gesamt * RahmenMindestens)
            {
                rahmen = r;
            }
        }
        return new Haupttoene(fassade, dachTon, rahmen);
    }

    private static int Schluessel3(GrafikModell m, int d) => (m.Farben[d * 3] << 16) | (m.Farben[(d * 3) + 1] << 8) | m.Farben[(d * 3) + 2];

    private static Vector3 Ton(int s) => new Vector3((s >> 16) & 255, (s >> 8) & 255, s & 255) / 255f;

    private static float Familie(Dictionary<int, float> flaechen, Vector3 ton) =>
        flaechen.Where(f => Vector3.Distance(Ton(f.Key), ton) <= FamilienAbstand).Sum(f => f.Value);

    private static Vector3? Haupt(Dictionary<int, float> flaechen, Vector3? ohne)
    {
        if (flaechen.Count == 0)
        {
            return null;
        }
        // der Ton, dessen Familie die groesste Flaeche hat (bei Gleichstand der hellere Schluessel, fest)
        return Ton(flaechen.Keys.OrderByDescending(k => Familie(flaechen, Ton(k))).ThenByDescending(k => flaechen[k]).ThenBy(k => k).First());
    }

    /// <summary>Teil eines Dreiecks (0 Fassade, 1 Dach, 2 Rahmen, -1 keiner) nach den Haupttoenen.</summary>
    public static int TeilVon(GrafikModell m, int d, Haupttoene h, float faktor, float unten, float oben)
    {
        var f = m.Farbe(d);
        if (Ausgenommen(f))
        {
            return -1;
        }
        var (ny, _, y) = Lage(m, d);
        bool Nah(Vector3? ton) => ton is { } t && (NurHauptton.Value ? f == t : Vector3.Distance(f, t) <= FamilienAbstand);
        if (IstDach(ny, (y * faktor) + unten, oben) && Nah(h.Dach))
        {
            return 1;
        }
        if (Nah(h.Fassade))
        {
            return 0;
        }
        return Nah(h.Rahmen) ? 2 : -1;
    }

    /// <summary>Neue Farbe eines Tons <paramref name="alt"/> der Familie mit Hauptton <paramref name="haupt"/>: Ziel mal Verhaeltnis der Helligkeiten.</summary>
    public static Vector3 Umgefaerbt(Vector3 alt, Vector3 haupt, Vector3 ziel)
    {
        if (NurHauptton.Value)
        {
            return ziel;
        }
        var verhaeltnis = Helligkeit(haupt) <= 0.001f ? 1f : Helligkeit(alt) / Helligkeit(haupt);
        return Vector3.Clamp(ziel * verhaeltnis, Vector3.Zero, Vector3.One);
    }

    private sealed class Vorrat
    {
        public readonly Dictionary<string, GrafikModell> Modelle = new(StringComparer.Ordinal);
    }

    // gleiche Eingaben = dasselbe Modell-Objekt (der Teilbau erkennt Hausnetze an den Modell-Objekten)
    private static readonly ConditionalWeakTable<GrafikModell, Vorrat> Gemerkt = [];

    /// <summary>
    /// Modell <paramref name="m"/> mit umgefaerbten Eckfarben: Familien nach <paramref name="h"/>, Ziele je Teil
    /// (null = bleibt). Unveraendert (gleiches Objekt), wenn kein Dreieck dazugehoert.
    /// </summary>
    public static GrafikModell Umfaerben(GrafikModell m, Haupttoene h, Vector3?[] ziele, float faktor, float unten, float oben)
    {
        var schluessel = string.Create(CultureInfo.InvariantCulture,
            $"{h}|{string.Join(",", ziele.Select(z => z?.ToString() ?? "-"))}|{faktor}|{unten}|{oben}|{NurHauptton.Value}");
        var vorrat = Gemerkt.GetOrCreateValue(m);
        lock (vorrat)
        {
            if (vorrat.Modelle.TryGetValue(schluessel, out var fertig))
            {
                return fertig;
            }
        }
        var haupt = new[] { h.Fassade, h.Dach, h.Rahmen };
        byte[]? eck = null;
        for (var d = 0; d < m.Dreiecke; d++)
        {
            var teil = TeilVon(m, d, h, faktor, unten, oben);
            if (teil < 0 || ziele[teil] is not { } ziel || haupt[teil] is not { } ton)
            {
                continue;
            }
            eck ??= m.EckFarben is { } alt ? (byte[])alt.Clone() : [.. Enumerable.Range(0, m.Dreiecke * 9).Select(i => m.Farben[(i / 9 * 3) + (i % 3)])];
            for (var k = 0; k < 3; k++)
            {
                var neu = Umgefaerbt(NurHauptton.Value ? m.Farbe(d) : m.EckFarbe(d, k), ton, ziel);
                var i = (d * 9) + (k * 3);
                (eck[i], eck[i + 1], eck[i + 2]) = ((byte)Byte(neu.X), (byte)Byte(neu.Y), (byte)Byte(neu.Z));
            }
        }
        var ergebnis = eck is null ? m : new GrafikModell(m.Name, m.Ecken, m.Farben, eck);
        lock (vorrat)
        {
            vorrat.Modelle[schluessel] = ergebnis;
        }
        return ergebnis;
    }
}
