using System.Numerics;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Core.Spiel;

/// <summary>
/// Welle A 0.80.0 (Weisung P12-268, Nico Antwort 5, P12-258): Karte der Stadt im Spiel-Handy, nur aus den Daten der Szene
/// (Strassen, Gleise, Wasser, Parks, Haeuser, Orte, Haltestellen). Jeder Ort bekommt eine Nummer in der Reihenfolge von
/// <see cref="StadtSzene.Orte"/> (wie die Liste unter der Karte); Antippen einer Nummer oder eines Orts-Hauses waehlt den Ort,
/// die Figur geht sofort los. Nur Darstellung und Bedienung: nichts davon geht in den Spielstand.
/// </summary>
public sealed class StadtKarte
{
    /// <summary>Ein Ort auf der Karte: Nummer (ab 1), Mitte und Kasten am Boden (x, z), ob dort ein offener Auftrag wartet.</summary>
    public sealed record Ort(int Nr, string Id, string Name, Vector2 Mitte, Vector2 Min, Vector2 Max, bool Aufgabe);

    /// <summary>Halbmesser der Nummernmarke in Bildpunkten (bei Schriftfaktor 1).</summary>
    public const float MarkeRadius = 11f;

    /// <summary>Antippen trifft eine Marke bis zu diesem Abstand in Bildpunkten (Finger).</summary>
    public const float TreffRadius = 22f;

    public StadtKarte(StadtSzene szene, IReadOnlySet<string> aufgaben)
    {
        Szene = szene;
        Orte = szene.Orte.Select((o, i) => new Ort(i + 1, o.Id, o.Name,
            new Vector2((o.KastenMin.X + o.KastenMax.X) / 2, (o.KastenMin.Z + o.KastenMax.Z) / 2),
            new Vector2(o.KastenMin.X, o.KastenMin.Z), new Vector2(o.KastenMax.X, o.KastenMax.Z), aufgaben.Contains(o.Id))).ToList();
    }

    public StadtSzene Szene { get; }

    public IReadOnlyList<Ort> Orte { get; }

    /// <summary>Massstab (Bildpunkte je Feld), damit die ganze Stadt in <paramref name="breite"/> x <paramref name="hoehe"/> passt.</summary>
    public float Massstab(float breite, float hoehe) =>
        MathF.Max(0.01f, MathF.Min(breite / MathF.Max(1, Szene.Breite), hoehe / MathF.Max(1, Szene.Tiefe)));

    /// <summary>Bildpunkt eines Punkts am Boden (x, z): Norden oben wie die Draufsicht des Werkzeugs, Ursprung oben links.</summary>
    public static Vector2 Bild(Vector2 welt, float massstab) => welt * massstab;

    /// <summary>
    /// Welcher Ort liegt unter dem Bildpunkt? Zuerst die naechste Nummernmarke bis <see cref="TreffRadius"/> (× Schriftfaktor),
    /// sonst das Haus des Orts unter dem Punkt; sonst null (freie Stelle: nichts).
    /// </summary>
    public Ort? Treffer(Vector2 bild, float massstab, float schrift = 1f)
    {
        var marken = Marken(massstab, schrift);
        var naechster = Orte.Select((o, i) => (o, d: Vector2.Distance(marken[i], bild))).OrderBy(t => t.d).ThenBy(t => t.o.Nr).FirstOrDefault();
        if (naechster.o is not null && naechster.d <= TreffRadius * schrift)
        {
            return naechster.o;
        }
        var welt = bild / massstab;
        return Orte.Where(o => welt.X >= o.Min.X && welt.X <= o.Max.X && welt.Y >= o.Min.Y && welt.Y <= o.Max.Y)
            .OrderBy(o => Vector2.Distance(o.Mitte, welt)).FirstOrDefault();
    }

    /// <summary>Halbmesser der Marke „!“ (offener Auftrag) oben rechts an der Nummernmarke, in Bildpunkten (Schriftfaktor 1).</summary>
    public const float AufgabeRadius = 7f;

    /// <summary>Breite des weissen Rands um Marken (Bildpunkte, ausserhalb des Halbmessers zur Haelfte).</summary>
    public const float MarkeRand = 1.5f;

    /// <summary>Abstand zwischen zwei Nummernmarken (Bildpunkte) nach dem Auseinanderschieben.</summary>
    public const float MarkeLuecke = 2f;

    /// <summary>Gegenprobe Fund P12-283: Marken wie bis A 0.82.0 genau in der Mitte des Orts (nur Tests).</summary>
    public static bool MarkenAltFuerTests { get => MarkenAlt.Value; set => MarkenAlt.Value = value; }
    private static readonly AsyncLocal<bool> MarkenAlt = new();

    private (float Massstab, float Schrift, bool Alt, IReadOnlyList<Vector2> Punkte)? _marken;

    /// <summary>
    /// Welle A 0.83.0 (Weisung P12-283 Fund 1): Mitte jeder Nummernmarke im Bild, in der Reihenfolge von <see cref="Orte"/>.
    /// Ausgang ist die Mitte des Orts; liegt eine Marke (samt Rand und Marke „!“) ueber dem Rand der Karte, rueckt sie nach
    /// innen; ueberlappen sich zwei Nummernmarken, werden sie Schritt fuer Schritt auseinandergeschoben (gleich weit, immer
    /// wieder nach innen gerueckt), bis jede mindestens <see cref="MarkeLuecke"/> Abstand hat. Zeichnen und Antippen nutzen
    /// dieselben Punkte. Nur Darstellung.
    /// </summary>
    public IReadOnlyList<Vector2> Marken(float massstab, float schrift = 1f)
    {
        var alt = MarkenAlt.Value;
        if (_marken is { } m && m.Massstab == massstab && m.Schrift == schrift && m.Alt == alt)
        {
            return m.Punkte;
        }
        var punkte = Orte.Select(o => Bild(o.Mitte, massstab)).ToArray();
        if (!alt)
        {
            MarkenOrdnen(punkte, massstab, schrift);
        }
        _marken = (massstab, schrift, alt, punkte);
        return punkte;
    }

    private void MarkenOrdnen(Vector2[] p, float massstab, float schrift)
    {
        var breite = Szene.Breite * massstab;
        var hoehe = Szene.Tiefe * massstab;
        var r = (MarkeRadius * schrift) + (MarkeRand / 2);
        var b = (AufgabeRadius * schrift) + (MarkeRand / 2);
        void Innen(int i)
        {
            // die Marke „!“ sitzt oben rechts (Mitte um r nach rechts und oben verschoben)
            var rechts = Orte[i].Aufgabe ? MathF.Max(r, (MarkeRadius * schrift) + b) : r;
            var oben = Orte[i].Aufgabe ? MathF.Max(r, (MarkeRadius * schrift) + b) : r;
            p[i] = new Vector2(Math.Clamp(p[i].X, r, MathF.Max(r, breite - rechts)), Math.Clamp(p[i].Y, oben, MathF.Max(oben, hoehe - r)));
        }
        for (var i = 0; i < p.Length; i++)
        {
            Innen(i);
        }
        var noetig = (2 * MarkeRadius * schrift) + MarkeRand + MarkeLuecke;
        for (var runde = 0; runde < 400; runde++)
        {
            var bewegt = false;
            for (var i = 0; i < p.Length; i++)
            {
                for (var j = i + 1; j < p.Length; j++)
                {
                    var d = p[j] - p[i];
                    var laenge = d.Length();
                    if (laenge >= noetig - 0.01f)
                    {
                        continue;
                    }
                    // gleiche Stelle: feste Richtung je Paar (nie Zufall)
                    var richtung = laenge > 1e-4f ? d / laenge : new Vector2(MathF.Cos(j * 2.399f), MathF.Sin(j * 2.399f));
                    var schub = ((noetig - laenge) / 2) + 0.05f;
                    p[i] -= richtung * schub;
                    p[j] += richtung * schub;
                    Innen(i);
                    Innen(j);
                    bewegt = true;
                }
            }
            if (!bewegt)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Pruefung (Start-Test und Tests): je Marke, die ueber den Rand der Karte ragt (samt Rand und „!“), und je Paar sich
    /// ueberlappender Nummernmarken ein Text. Leer = alles ganz zu sehen.
    /// </summary>
    public List<string> MarkenFund(IReadOnlyList<Vector2> p, float massstab, float schrift = 1f)
    {
        var funde = new List<string>();
        var breite = Szene.Breite * massstab;
        var hoehe = Szene.Tiefe * massstab;
        var r = (MarkeRadius * schrift) + (MarkeRand / 2);
        var b = (AufgabeRadius * schrift) + (MarkeRand / 2);
        const float zugabe = 0.01f;
        for (var i = 0; i < p.Count; i++)
        {
            var o = Orte[i];
            var links = p[i].X - r;
            var rechts = p[i].X + (o.Aufgabe ? MathF.Max(r, (MarkeRadius * schrift) + b) : r);
            var oben = p[i].Y - (o.Aufgabe ? MathF.Max(r, (MarkeRadius * schrift) + b) : r);
            var unten = p[i].Y + r;
            if (links < -zugabe || oben < -zugabe || rechts > breite + zugabe || unten > hoehe + zugabe)
            {
                funde.Add($"Marke {o.Nr} ({o.Name}) ragt über den Rand der Karte");
            }
            for (var j = i + 1; j < p.Count; j++)
            {
                if (Vector2.Distance(p[i], p[j]) < (2 * MarkeRadius * schrift) + MarkeRand - zugabe)
                {
                    funde.Add($"Marken {o.Nr} und {Orte[j].Nr} überlappen sich");
                }
            }
        }
        return funde;
    }

    /// <summary>Wo die Figur auf der Karte steht: draussen ihre Stelle, an oder in einem Ort dessen Stehplatz.</summary>
    public static Vector2 Figur(StadtSpiel spiel) =>
        !spiel.ImInnenraum && spiel.ImFreien ? new Vector2(spiel.FigurX, spiel.FigurZ)
        : spiel.Szene.Ort(spiel.Jetzt.Ort) is { } ort ? new Vector2(ort.StehX, ort.StehZ)
        : new Vector2(spiel.FigurX, spiel.FigurZ);
}
