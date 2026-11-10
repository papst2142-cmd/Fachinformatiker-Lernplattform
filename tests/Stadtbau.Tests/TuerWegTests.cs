using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Stadtbau.Core;
using Xunit;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Tests;

/// <summary>
/// Weisung P12-164: Sammelliste 278 (Weg rechtwinklig, Nico Frage 1 = a), 280 (Tuerwege mit runden Ecken wie Fusswege), 274 Rest
/// (Fund nennt den Namen statt der Kennung) und 279 („Stadt zu teuer“ zuerst), je mit Gegenprobe.
/// </summary>
public class TuerWegTests
{
    private static Spieldaten D => Daten.Spiel;

    /// <summary>Leere Stadt mit einer schraegen Strasse (rund 12,5 Grad) und einem Katalog-Gebaeude darueber.</summary>
    private static (Projekt P, AussenStelle G) Stadt(string id = "commercial/building-e")
    {
        var p = Projekt.Neu("Weg", D, Daten.NeueAblage(), leer: (40, 40));
        p.AussenAendern("Strasse", a => Aussen.Landschaft(a).Add(new PyObjekt
        {
            { "typ", "strasse" }, { "punkte", new List<object?> { new List<object?> { 2.0, 34.0 }, new List<object?> { 38.0, 26.0 } } }, { "gehweg", 0.35 },
        }));
        var s = p.AussenAendern("Stueck", a => AussenBearbeitung.Setzen(a, AussenArt.Katalog, 20, 24, raster: false, modell: id)!);
        var g = p.AussenAendern("Als Gebaeude", a => AussenBearbeitung.AlsGebaeude(a, s, out _, out _));
        Assert.NotNull(g);
        return (p, g!);
    }

    /// <summary>Tuer auf die Rueckseite (Nord, weg von der Strasse): der Weg muss um das Haus herum.</summary>
    private static void TuerHinten(Projekt p, AussenStelle g)
    {
        var f = TuerFassade.Rechnen(p, p.AussenStand, g)!;
        var nord = Enumerable.Range(0, f.Waende.Count).First(i => f.Waende[i].Aussen.Y < -0.5f);
        var wand = f.Waende[nord];
        var platz = Enumerable.Range(1, 19).Select(k => f.Finden(wand.Bei(wand.Laenge * k / 20f) + (wand.Aussen * 0.1f), nord, einrasten: false))
            .First(q => q.Art is TuerFassade.Stelle.Glatt or TuerFassade.Stelle.Modelltuer);
        p.AussenAendern("Tuer", a => TuerSetzen.SetzenAn(a, g, platz));
    }

    private static PyObjekt Weg(Projekt p, AussenStelle g) =>
        Blueprint.Zierweg(p.AussenStand, Py.Str(Objekt(Aussen.Landschaft(p.AussenStand)[g.Nummer])["modell"]))!;

    private static List<Vector2> Punkte(PyObjekt z) =>
        [.. ((List<object?>)z["weg"]!).Select(q => (List<object?>)q!).Select(q => new Vector2((float)Py.F(q[0]), (float)Py.F(q[1])))];

    /// <summary>Was am Weg nicht rechtwinklig ist (leer = alles richtig).</summary>
    private static List<string> Fehler(PyObjekt z, List<Vector2> strasse)
    {
        var f = new List<string>();
        var w = Punkte(z);
        if (Get(z, "radien") is not List<object?> r || r.Count != w.Count || Py.F(r[0]) != 0 || Py.F(r[^1]) != 0)
        {
            f.Add("radien fehlen oder passen nicht");
        }
        for (var i = 0; i + 2 < w.Count; i++)
        {
            if (MathF.Abs(w[i].X - w[i + 1].X) > 1e-3f && MathF.Abs(w[i].Y - w[i + 1].Y) > 1e-3f)
            {
                f.Add($"Stueck {i} weder parallel noch rechtwinklig zur Hauswand");
            }
        }
        var d = Vector2.Normalize(strasse[1] - strasse[0]);
        var letztes = Vector2.Normalize(w[^1] - w[^2]);
        var winkel = MathF.Acos(Math.Clamp(MathF.Abs(Vector2.Dot(d, letztes)), 0, 1)) * 180 / MathF.PI;
        if (MathF.Abs(winkel - 90) > 1)
        {
            f.Add($"letztes Stueck {winkel:F2} Grad zur Strasse");
        }
        var t = Vector2.Dot(w[^1] - strasse[0], d);
        if (Vector2.Distance(strasse[0] + (d * t), w[^1]) > 0.01f)
        {
            f.Add("Ende nicht auf der Strassenmitte");
        }
        return f;
    }

    private static readonly List<Vector2> Schraeg = [new(2, 34), new(38, 26)];

    [Fact]
    public void Weg_Rechtwinklig_LetztesStueckQuerAufDieStrasse()
    {
        var (p, g) = Stadt();
        var z = Weg(p, g);
        Assert.Empty(Fehler(z, Schraeg));
        // um die Ecke: Tuer auf die Rueckseite (Nord, weg von der Strasse)
        TuerHinten(p, g);
        var hinten = Weg(p, g);
        var w = Punkte(hinten);
        Assert.True(w.Count >= 4, $"um die Ecke erwartet, Weg {string.Join(" ", w)}");
        Assert.Empty(Fehler(hinten, Schraeg));
        // am Haus entlang, nur das letzte kurze Stueck quer zur schraegen Strasse (nicht lang schraeg ueber die Wiese)
        Assert.True(Vector2.Distance(w[^1], w[^2]) <= 1.5f, $"letztes Stueck {Vector2.Distance(w[^1], w[^2]):F2} lang, Weg {string.Join(" ", w)}");
        // kein Stueck schneidet das Haus
        var (x, y, bw, bh) = AussenBearbeitung.HausFlaeche(p.AussenStand, g);
        for (var i = 1; i + 1 < w.Count; i++)
        {
            var m = (w[i] + w[i + 1]) / 2;
            Assert.False(m.X > x + 0.01 && m.X < x + bw - 0.01 && m.Y > y + 0.01 && m.Y < y + bh - 0.01, $"Stueck {i} durch das Haus");
        }
    }

    [Fact]
    public void Gegenprobe_AlterWeg_UmDieEcke_NichtRechtwinklig()
    {
        GebaeudeTuer.WegAltFuerTests = true;
        try
        {
            var (p, g) = Stadt();
            TuerHinten(p, g);
            var fehler = Fehler(Weg(p, g), Schraeg);
            Assert.Contains(fehler, f => f.Contains("Stueck", StringComparison.Ordinal) || f.StartsWith("Ende", StringComparison.Ordinal));
            Assert.Contains("radien fehlen oder passen nicht", fehler);
        }
        finally
        {
            GebaeudeTuer.WegAltFuerTests = false;
        }
    }

    /// <summary>
    /// Schneiden sich zwei nicht benachbarte Stuecke des Linienzugs? Punkte naeher als 0,001 zaehlen als einer (eine Innenkante
    /// mit Radius genau der halben Breite faellt in der Ecke auf einen Punkt zusammen, das ist keine Ueberlappung).
    /// </summary>
    private static bool Kreuzt(List<Vector2> roh)
    {
        var l = new List<Vector2>();
        foreach (var q in roh)
        {
            if (l.Count == 0 || Vector2.Distance(l[^1], q) >= 1e-3f)
            {
                l.Add(q);
            }
        }
        static float Kreuz(Vector2 a, Vector2 b) => (a.X * b.Y) - (a.Y * b.X);
        for (var i = 0; i + 1 < l.Count; i++)
        {
            for (var j = i + 2; j + 1 < l.Count; j++)
            {
                var (p, r, q, s) = (l[i], l[i + 1] - l[i], l[j], l[j + 1] - l[j]);
                var n = Kreuz(r, s);
                if (MathF.Abs(n) < 1e-9f)
                {
                    continue;
                }
                var t = Kreuz(q - p, s) / n;
                var u = Kreuz(q - p, r) / n;
                if (t > 1e-4f && t < 1 - 1e-4f && u > 1e-4f && u < 1 - 1e-4f)
                {
                    return true;
                }
            }
        }
        return false;
    }

    [Fact]
    public void Tuerweg_RundeEcken_KeinUeberlappenderKnick()
    {
        var (p, g) = Stadt();
        TuerHinten(p, g);
        var z = Weg(p, g);
        var w = Punkte(z);
        Assert.True(w.Count >= 4);
        var breite = StadtSzene.WegBreite;
        var halb = breite / 2;
        // neu: EIN Band mit runden Ecken, beide Kanten ohne Kreuzung (kein Stueck liegt doppelt)
        var band = StadtTuerweg.Gezeichnet(z, breite);
        Assert.NotNull(band);
        Assert.True(band!.Count > w.Count + 4, "Ecken werden gerundet");
        Assert.False(Kreuzt(Kurve.Versetzt(band, halb)), "linke Kante kreuzt sich");
        Assert.False(Kreuzt(Kurve.Versetzt(band, -halb)), "rechte Kante kreuzt sich");
        // Enden wie bisher: Anfang halbe Breite in der Tuer, Ende halbe Breite hinter der Strassenmitte
        Assert.True(Vector2.Distance(band[0], w[0] - (Vector2.Normalize(w[1] - w[0]) * halb)) < 0.01f);
        Assert.True(Vector2.Distance(band[^1], w[^1] + (Vector2.Normalize(w[^1] - w[^2]) * halb)) < 0.01f);
        // Gegenprobe: alter Weg Stueck fuer Stueck; an jedem Knick liegt eine Flaeche in zwei Rechtecken (ueberlappender Knick)
        StadtTuerweg.AltFuerTests = true;
        try
        {
            Assert.Null(StadtTuerweg.Gezeichnet(z, breite));
        }
        finally
        {
            StadtTuerweg.AltFuerTests = false;
        }
        static bool ImRechteck(Vector2 a, Vector2 b, float h, Vector2 q)
        {
            var d = Vector2.Normalize(b - a);
            var t = Vector2.Dot(q - a, d);
            var s = Vector2.Dot(q - a, new Vector2(-d.Y, d.X));
            return t >= -h && t <= Vector2.Distance(a, b) + h && MathF.Abs(s) <= h;
        }
        for (var i = 1; i + 1 < w.Count; i++)
        {
            var ein = Vector2.Normalize(w[i] - w[i - 1]);
            var aus = Vector2.Normalize(w[i + 1] - w[i]);
            var q = w[i] + ((aus - ein) * (halb * 0.5f));
            Assert.True(ImRechteck(w[i - 1], w[i], halb, q) && ImRechteck(w[i], w[i + 1], halb, q), $"Knick {i}: alte Rechtecke ueberlappen nicht");
        }
        // Gegenprobe der Pruefung: ein Bogen enger als die halbe Breite (Radius nicht gekappt) ueberlappt innen und wird gefunden
        var eng = Kurve.Glatt(w, [.. w.Select((_, i) => i == 0 || i == w.Count - 1 ? 0f : halb * 0.4f)], halb * 0.4f);
        Assert.True(Kreuzt(Kurve.Versetzt(eng, halb)) || Kreuzt(Kurve.Versetzt(eng, -halb)), "zu enger Bogen nicht erkannt");
        // ohne radien (Fassung 0, aeltere Projekte) zeichnet das Spiel wie bisher
        var ohne = new PyObjekt { { "weg", z["weg"] } };
        Assert.Null(StadtTuerweg.Gezeichnet(ohne, breite));
    }

    [Fact]
    public void Fund_NenntDenNamen_NichtDieKennung()
    {
        var (p, _) = Stadt();
        var stand = p.Stand();
        var szene = Ansicht3D.Bauen(stand, D, Ansicht3D.Einstellung.Standard).Szene;
        Assert.Equal("Kaufhaus", TuerBuendig.Name(stand, szene, "kaufhaus"));
        TuerBuendig.NameAltFuerTests = true;
        try
        {
            Assert.Equal("kaufhaus", TuerBuendig.Name(stand, szene, "kaufhaus"));
        }
        finally
        {
            TuerBuendig.NameAltFuerTests = false;
        }
    }

    [Fact]
    public void StadtZuTeuer_StehtZuerst()
    {
        var teuer = new StadtbauFund("Stadt zu teuer", "", true, 1, 1);
        var andere = new[] { new StadtbauFund("A", "", true, 2, 2), new StadtbauFund("B", "", true, 3, 3) };
        Assert.Same(teuer, GrenzenLive.Zuerst(andere, teuer)[0]);
        Assert.Equal(3, GrenzenLive.Zuerst(andere, teuer).Count);
        Assert.Equal(2, GrenzenLive.Zuerst(andere, null).Count);
        GrenzenLive.ZuletztAltFuerTests = true;
        try
        {
            Assert.Same(teuer, GrenzenLive.Zuerst(andere, teuer)[^1]);
        }
        finally
        {
            GrenzenLive.ZuletztAltFuerTests = false;
        }
    }
}
