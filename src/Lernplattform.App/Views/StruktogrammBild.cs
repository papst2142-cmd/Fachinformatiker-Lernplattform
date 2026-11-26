using Avalonia;
using Avalonia.Media;
using Lernplattform.Core.Pruefung.Echt;
using Lernplattform.Core.Texte;

namespace Lernplattform.Views;

/// <summary>
/// A 1.12.0 "Prüfung im echten Format" W2 (Weisung P12-390, Plan P12-250): Struktogramm nach DIN 66261 aus Daten gezeichnet.
/// Anweisung als Rechteck, Verzweigung mit Dreieck und "ja"/"nein", Fallauswahl mit Dreieck, Zeile der Fälle und "sonst",
/// Zählschleife und Solange-Schleife mit Rand links (Kopf oben), Wiederhole-Schleife mit Rand links und Fuß unten, Aufruf als
/// Rechteck mit doppelten senkrechten Seitenlinien. Texte stehen genau so da, wie sie eingegeben wurden (kein "?" dazu).
/// Ein leerer Zweig zeigt "∅", der Füllraum unter einem kürzeren Zweig bleibt leer (Auflagen C, D, E). Die Breite füllt den
/// Platz; reicht er nicht für die Texte, wird das Bild breiter und im <see cref="Bildkasten"/> seitlich gewischt.
/// </summary>
public sealed class StruktogrammBild : EchtBild
{
    private sealed record Teil(string Pfad, Rect Rahmen, EchtBlock Block, FormattedText Text, IReadOnlyList<Ast> Zweige, double Kopf);

    private sealed record Ast(string Pfad, Rect Rahmen, string Beschriftung, IReadOnlyList<Teil> Bloecke);

    private IReadOnlyList<EchtBlock> _bloecke;
    private string? _gewaehlt;
    private Ast _wurzel = new("", default, "", []);

    public StruktogrammBild(IReadOnlyList<EchtBlock> bloecke)
    {
        _bloecke = bloecke;
    }

    /// <summary>Neue Zeichnung (Baukasten): neu planen und zeichnen.</summary>
    public IReadOnlyList<EchtBlock> Daten
    {
        get => _bloecke;
        set
        {
            _bloecke = value;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <summary>Gewählte Stelle (Block oder Zweig, Pfad wie <see cref="EchtStruktogramm"/>), null = keine.</summary>
    public string? Gewaehlt
    {
        get => _gewaehlt;
        set
        {
            _gewaehlt = value;
            InvalidateVisual();
        }
    }

    private double P => 0.5 * F;

    private double Zeile => Text("Ag", Linie).Height + 0.8 * F;

    private double Rand => 1.6 * F;

    private double Doppel => 0.45 * F;

    private double Breite(string text) => text.Length == 0 ? 0 : Text(text, Linie).Width;

    // -- Breiten ---------------------------------------------------------------------------------------------------------

    private double Natur(IReadOnlyList<EchtBlock> folge) => folge.Count == 0 ? 3 * F : folge.Max(Natur);

    private double Natur(EchtBlock b) => b.Typ switch
    {
        EchtStruktogramm.Anweisung => Breite(b.Text) + 2 * P,
        EchtStruktogramm.Aufruf => Breite(b.Text) + 2 * P + 2 * Doppel,
        EchtStruktogramm.Verzweigung or EchtStruktogramm.Fallauswahl => Math.Max(Spalten(b).Sum(), (Breite(b.Text) + 2 * P) * 1.25),
        _ => Math.Max(Breite(b.Text) + 2 * P, Rand + Natur(b.Zweige[0].Bloecke)),
    };

    private IReadOnlyList<double> Spalten(EchtBlock b) =>
        [.. b.Zweige.Select((z, i) => Math.Max(Natur(z.Bloecke), Breite(Beschriftung(b, i)) + 2 * P))];

    private static string Beschriftung(EchtBlock b, int i) => b.Zweige[i].Art switch
    {
        EchtStruktogramm.Ja => "ja",
        EchtStruktogramm.Nein => "nein",
        EchtStruktogramm.Sonst => "sonst",
        EchtStruktogramm.Fall => b.Zweige[i].Text,
        _ => "",
    };

    protected override (double Mindest, double Bequem) Breiten()
    {
        var natur = Math.Max(Natur(_bloecke), 20 * F) + 2;
        return (natur, Math.Max(natur, 90 * F));
    }

    // -- Planen ----------------------------------------------------------------------------------------------------------

    protected override Size Planen(double breite)
    {
        var (ast, hoehe) = Folge(_bloecke, "", "", 1, 1, breite - 2, null);
        _wurzel = ast;
        return new Size(breite, hoehe + 2);
    }

    private static string Weiter(string pfad, int i) => pfad.Length == 0
        ? i.ToString(System.Globalization.CultureInfo.InvariantCulture)
        : pfad + "." + i.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Plant eine Folge ab (x, y) mit Breite w; <paramref name="hoehe"/> = feste Höhe der Spalte (Füllraum) oder null.</summary>
    private (Ast Ast, double Hoehe) Folge(IReadOnlyList<EchtBlock> folge, string pfad, string beschriftung, double x, double y, double w,
        double? hoehe)
    {
        var teile = new List<Teil>();
        var oben = y;
        for (var i = 0; i < folge.Count; i++)
        {
            var t = Block(folge[i], Weiter(pfad, i), x, oben, w);
            teile.Add(t);
            oben += t.Rahmen.Height;
        }
        var eigen = folge.Count == 0 ? Zeile : oben - y;
        var h = Math.Max(eigen, hoehe ?? 0);
        return (new Ast(pfad, new Rect(x, y, w, h), beschriftung, teile), eigen);
    }

    private Teil Block(EchtBlock b, string pfad, double x, double y, double w)
    {
        var text = Text(b.Text, Linie);
        switch (b.Typ)
        {
            case EchtStruktogramm.Verzweigung:
            case EchtStruktogramm.Fallauswahl:
            {
                var spalten = Spalten(b);
                var summe = spalten.Sum();
                var noetig = text.Width + 2 * P;
                // Dreieck so hoch, dass der Text in der ersten Zeile zwischen den Schrägen Platz hat (mindestens 2 Zeilen)
                var kopf = Math.Max(2 * Zeile, Zeile / Math.Max(0.2, 1 - (noetig / w)));
                var fall = b.Typ == EchtStruktogramm.Fallauswahl;
                var unten = y + kopf + (fall ? Zeile : 0);
                var hoehen = new List<double>();
                var links = x;
                var breiten = spalten.Select(s => s * w / summe).ToList();
                for (var i = 0; i < b.Zweige.Count; i++)
                {
                    hoehen.Add(Folge(b.Zweige[i].Bloecke, Weiter(pfad, i), "", links, unten, breiten[i], null).Hoehe);
                    links += breiten[i];
                }
                var rumpf = hoehen.Max();
                var zweige = new List<Ast>();
                links = x;
                for (var i = 0; i < b.Zweige.Count; i++)
                {
                    zweige.Add(Folge(b.Zweige[i].Bloecke, Weiter(pfad, i), Beschriftung(b, i), links, unten, breiten[i], rumpf).Ast);
                    links += breiten[i];
                }
                return new Teil(pfad, new Rect(x, y, w, unten - y + rumpf), b, text, zweige, kopf);
            }
            case EchtStruktogramm.Zaehlschleife:
            case EchtStruktogramm.Solange:
            {
                var (ast, h) = Folge(b.Zweige[0].Bloecke, Weiter(pfad, 0), "", x + Rand, y + Zeile, w - Rand, null);
                return new Teil(pfad, new Rect(x, y, w, Zeile + h), b, text, [ast], Zeile);
            }
            case EchtStruktogramm.Wiederhole:
            {
                var (ast, h) = Folge(b.Zweige[0].Bloecke, Weiter(pfad, 0), "", x + Rand, y, w - Rand, null);
                return new Teil(pfad, new Rect(x, y, w, h + Zeile), b, text, [ast], Zeile);
            }
            default:
                return new Teil(pfad, new Rect(x, y, w, Zeile), b, text, [], Zeile);
        }
    }

    // -- Antippen --------------------------------------------------------------------------------------------------------

    /// <summary>Stelle (Block oder Zweig) unter einem Punkt; "" = oberste Folge (nichts gewählt), null = außerhalb.</summary>
    public string? Stelle(Point p) => _wurzel.Rahmen.Contains(p) ? InAst(_wurzel, p) : null;

    private static string InAst(Ast a, Point p)
    {
        foreach (var t in a.Bloecke)
        {
            if (t.Rahmen.Contains(p))
            {
                return t.Zweige.FirstOrDefault(z => z.Rahmen.Contains(p)) is { } z ? InAst(z, p) : t.Pfad;
            }
        }
        return a.Pfad;
    }

    /// <summary>Mitte einer Stelle im Bild (Bedienprobe im Start-Test), null = gibt es nicht.</summary>
    public Point? Mitte(string pfad)
    {
        Point? Suche(Ast a)
        {
            if (a.Pfad == pfad)
            {
                // Zweig: leer = Mitte, sonst im Fuellraum unter dem letzten Block (ohne Fuellraum nicht antippbar)
                var frei = a.Bloecke.Count == 0 ? a.Rahmen.Y : a.Bloecke[^1].Rahmen.Bottom;
                return a.Rahmen.Bottom - frei < 4 ? null : new Point(a.Rahmen.Center.X, (frei + a.Rahmen.Bottom) / 2);
            }
            foreach (var t in a.Bloecke)
            {
                if (t.Pfad == pfad)
                {
                    return new Point(t.Rahmen.X + Math.Min(t.Rahmen.Width / 2, 2 * F), t.Rahmen.Y + Math.Min(t.Rahmen.Height, t.Kopf) / 2);
                }
                foreach (var z in t.Zweige)
                {
                    if (Suche(z) is { } m)
                    {
                        return m;
                    }
                }
            }
            return null;
        }
        return Suche(_wurzel);
    }

    /// <summary>Rahmen einer Stelle (Start-Test: Markierung sichtbar), null = gibt es nicht.</summary>
    public Rect? Rahmen(string pfad)
    {
        Rect? Suche(Ast a)
        {
            if (a.Pfad == pfad)
            {
                return a.Rahmen;
            }
            foreach (var t in a.Bloecke)
            {
                if (t.Pfad == pfad)
                {
                    return t.Rahmen;
                }
                foreach (var z in t.Zweige)
                {
                    if (Suche(z) is { } r)
                    {
                        return r;
                    }
                }
            }
            return null;
        }
        return Suche(_wurzel);
    }

    // -- Zeichnen --------------------------------------------------------------------------------------------------------

    public override void Render(DrawingContext dc)
    {
        dc.FillRectangle(Grund ?? Brushes.White, new Rect(Bounds.Size));
        var stift = Stift(1.2);
        Malen(dc, _wurzel, stift);
        if (_gewaehlt is { Length: > 0 } g && Rahmen(g) is { } r)
        {
            var dick = Math.Max(2.5, 0.25 * F);
            dc.DrawRectangle(null, new Pen(Akzent, dick), r.Deflate(dick / 2));
        }
    }

    private void Malen(DrawingContext dc, Ast a, Pen stift)
    {
        dc.DrawRectangle(null, stift, a.Rahmen);
        if (a.Bloecke.Count == 0)
        {
            Mitte(dc, Text(EchtTexte.LeerZeichen, Leise), a.Rahmen.Center.X, a.Rahmen.Y + Math.Min(a.Rahmen.Height, Zeile) / 2
                + Math.Max(0, (a.Rahmen.Height - Zeile) / 2));
            return;
        }
        foreach (var t in a.Bloecke)
        {
            Malen(dc, t, stift);
        }
    }

    private void Malen(DrawingContext dc, Teil t, Pen stift)
    {
        var r = t.Rahmen;
        dc.DrawRectangle(null, stift, r);
        var b = t.Block;
        switch (b.Typ)
        {
            case EchtStruktogramm.Verzweigung:
            case EchtStruktogramm.Fallauswahl:
            {
                var fall = b.Typ == EchtStruktogramm.Fallauswahl;
                var spitzeX = fall ? t.Zweige[^1].Rahmen.X : t.Zweige[0].Rahmen.Right;
                var spitzeY = r.Y + t.Kopf;
                dc.DrawLine(stift, r.TopLeft, new Point(spitzeX, spitzeY));
                dc.DrawLine(stift, r.TopRight, new Point(spitzeX, spitzeY));
                dc.DrawLine(stift, new Point(r.X, spitzeY), new Point(r.Right, spitzeY));
                // Text in der ersten Zeile mittig zwischen den Schrägen
                var z = Zeile;
                var linksFrei = r.X + ((spitzeX - r.X) * z / t.Kopf);
                var rechtsFrei = r.Right - ((r.Right - spitzeX) * z / t.Kopf);
                Mitte(dc, t.Text, (linksFrei + rechtsFrei) / 2, r.Y + (z / 2));
                if (fall)
                {
                    var zeileY = spitzeY + z;
                    dc.DrawLine(stift, new Point(r.X, zeileY), new Point(r.Right, zeileY));
                    foreach (var ast in t.Zweige)
                    {
                        dc.DrawLine(stift, new Point(ast.Rahmen.X, spitzeY), new Point(ast.Rahmen.X, zeileY));
                        Mitte(dc, Text(ast.Beschriftung, Linie), ast.Rahmen.Center.X, spitzeY + (z / 2));
                    }
                }
                else
                {
                    var ja = Text("ja", Linie);
                    var nein = Text("nein", Linie);
                    dc.DrawText(ja, new Point(r.X + P, spitzeY - (0.1 * F) - ja.Height));
                    dc.DrawText(nein, new Point(r.Right - P - nein.Width, spitzeY - (0.1 * F) - nein.Height));
                }
                foreach (var ast in t.Zweige)
                {
                    Malen(dc, ast, stift);
                }
                break;
            }
            case EchtStruktogramm.Zaehlschleife:
            case EchtStruktogramm.Solange:
                dc.DrawText(t.Text, new Point(r.X + P, r.Y + ((Zeile - t.Text.Height) / 2)));
                Malen(dc, t.Zweige[0], stift);
                break;
            case EchtStruktogramm.Wiederhole:
                Malen(dc, t.Zweige[0], stift);
                dc.DrawText(t.Text, new Point(r.X + P, r.Bottom - Zeile + ((Zeile - t.Text.Height) / 2)));
                break;
            case EchtStruktogramm.Aufruf:
                dc.DrawLine(stift, new Point(r.X + Doppel, r.Y), new Point(r.X + Doppel, r.Bottom));
                dc.DrawLine(stift, new Point(r.Right - Doppel, r.Y), new Point(r.Right - Doppel, r.Bottom));
                dc.DrawText(t.Text, new Point(r.X + Doppel + P, r.Y + ((Zeile - t.Text.Height) / 2)));
                break;
            default:
                dc.DrawText(t.Text, new Point(r.X + P, r.Y + ((Zeile - t.Text.Height) / 2)));
                break;
        }
    }
}
