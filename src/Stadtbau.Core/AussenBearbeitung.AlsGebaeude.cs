using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Stadtbau.Core;

/// <summary>
/// S 0.31.0 (Weisung P12-130 Frage 2 = a, P12-140 Punkt 2): Knopf „Als Gebäude übernehmen“ an einem Katalog-Stueck draussen.
/// Das Stueck wird ein Gebaeude ohne Ort wie ein gesetzter Blueprint: ein selbst gebautes Gebaeude mit genau einem Teil
/// (<see cref="Bauteile.GanzId"/>, das ganze Modell so gross und gedreht wie das Stueck) in <c>stadtdaten.bauteile</c>, sein
/// Block in <c>karte.modelle</c>, das Gebaeude an derselben Mitte in der Landschaft. Dazu Tuer und Weg zur naechsten Strasse
/// (<see cref="GebaeudeTuer.Dazu"/>, Wand zur Strasse) und ein Innenraum (<see cref="Entwurf.GrundrissAus"/>, Flur an der Tuer,
/// 4 Einheiten je Feld wie der Editor). Danach gehen „Tür setzen“, Innenraum und „Als Blueprint speichern“ wie bei jedem
/// Gebaeude ohne Ort. Ein Effekt wird kein Gebaeude.
/// </summary>
public static partial class AussenBearbeitung
{
    /// <summary>Warum das Stueck <paramref name="s"/> kein Gebaeude werden kann (null = geht).</summary>
    public static string? AlsGebaeudeNicht(PyObjekt aussen, AussenStelle s)
    {
        if (s.Art != AussenArt.Katalog || Aussen.KatalogStuecke(aussen).ElementAtOrDefault(s.Nummer) is not PyObjekt k)
        {
            return Texte.TuerNurGebaeude;
        }
        var id = Py.Str(k["id"]);
        return Katalogstueck.IstEffekt(Katalogstueck.Praefix + id) ? Texte.AlsGebaeudeEffekt
            : Katalog.Bauteil(Bauteile.GanzId(id, Py.F(k["w"]), Py.F(k["t"]))) is null ? Texte.AlsGebaeudeOhneModell
            : null;
    }

    /// <summary>
    /// Das Katalog-Stueck <paramref name="s"/> als Gebaeude uebernehmen (ein Schritt beim Aufrufer). Rueckgabe: Stelle des
    /// neuen Gebaeudes, oder null mit dem Grund in <paramref name="grund"/> (dann bleibt alles). <paramref name="hinweis"/>:
    /// gelber Hinweis, wenn der Weg keine Strasse erreicht.
    /// </summary>
    public static AussenStelle? AlsGebaeude(PyObjekt aussen, AussenStelle s, out string? grund, out string? hinweis)
    {
        hinweis = null;
        if ((grund = AlsGebaeudeNicht(aussen, s)) is not null)
        {
            return null;
        }
        var k = Objekt(Aussen.KatalogStuecke(aussen)[s.Nummer]);
        var id = Py.Str(k["id"]);
        var (mx, my, w, t) = (Py.F(k["x"]), Py.F(k["y"]), Py.F(k["w"]), Py.F(k["t"]));
        var dreh = Py.Hat(k, "dreh") ? (int)Py.Int(k["dreh"]) : 0;
        var anzeige = Katalog.Aktiv?.Eintrag(id)?.Anzeige ?? id;
        var modell = FreierModellName(aussen, Entwurf.ModellName(anzeige));
        var teile = new List<Bauteile.Teil> { new(Bauteile.GanzId(id, w, t), 0, 0, 0, dreh) };
        var gm = Bauteile.Zusammensetzen(modell, teile, Katalog.Bauteil);
        var sd = Aussen.Stadtdaten(aussen);
        if (Get(sd, "bauteile") is not PyObjekt alle)
        {
            alle = new PyObjekt();
            Py.Setzen(sd, "bauteile", alle);
        }
        Py.Setzen(alle, modell, new PyObjekt { { "teile", Bauteile.TeileDaten(teile) }, { "eingang", new List<object?>() } });
        var karte = Aussen.Karte(aussen);
        if (Get(karte, "modelle") is not PyObjekt kartenModelle)
        {
            kartenModelle = new PyObjekt();
            Py.Setzen(karte, "modelle", kartenModelle);
        }
        Py.Setzen(kartenModelle, modell, Bauteile.Block(gm));
        // Mitte bleibt (der Block liegt um die Ecke 0, 0 des Teils)
        var land = Aussen.Landschaft(aussen);
        land.Add(new PyObjekt { { "typ", "gebaeude" }, { "modell", modell }, { "x", Bearbeitung.Zahl(mx) }, { "y", Bearbeitung.Zahl(my) } });
        var neu = new AussenStelle(AussenArt.Gebaeude, land.Count - 1);
        Aussen.KatalogStuecke(aussen).RemoveAt(s.Nummer);
        hinweis = GebaeudeTuer.Dazu(aussen, neu, einrasten: false);
        // der Innenraum: Fassade = Wand mit der Tuer, Flur an der Tuer (wie der Vorschlag des Editors)
        var (x, y, bw, bh) = HausFlaeche(aussen, neu);
        var tuer = Blueprint.Zierweg(aussen, modell) is { } z && z["tuer"] is List<object?> p ? new Vector2((float)Py.F(p[0]), (float)Py.F(p[1])) : new Vector2((float)(x + (bw / 2)), (float)(y + bh));
        var (fassade, tiefe, stelle) = TuerSeite((x, y, bw, bh), tuer) switch
        {
            Bauteile.Seiten.Sued => (bw, bh, tuer.X - x),
            Bauteile.Seiten.Nord => (bw, bh, x + bw - tuer.X),
            Bauteile.Seiten.Ost => (bh, bw, y + bh - tuer.Y),
            _ => (bh, bw, tuer.Y - y),
        };
        const double k4 = Bauteile.InnenJeFeld;
        Py.Setzen(Aussen.Bauten(aussen), modell, new PyObjekt
        {
            { "name", anzeige },
            { "grundriss", Entwurf.GrundrissAus(fassade * k4, tiefe * k4, Math.Clamp(stelle, 0, fassade) * k4, k4) },
            { "personen", new List<object?>() },
        });
        return neu;
    }

    /// <summary>Seite des Rechtecks, auf der <paramref name="p"/> liegt (naechste Kante).</summary>
    internal static Bauteile.Seiten TuerSeite((double X, double Y, double W, double H) r, Vector2 p)
    {
        var abstand = new (Bauteile.Seiten Seite, double D)[]
        {
            (Bauteile.Seiten.Sued, Math.Abs(p.Y - (r.Y + r.H))),
            (Bauteile.Seiten.Nord, Math.Abs(p.Y - r.Y)),
            (Bauteile.Seiten.Ost, Math.Abs(p.X - (r.X + r.W))),
            (Bauteile.Seiten.West, Math.Abs(p.X - r.X)),
        };
        return abstand.MinBy(a => a.D).Seite;
    }

    /// <summary>Name, den es in der Stadt noch nicht gibt (<paramref name="name"/>, sonst mit _2, _3 ...).</summary>
    private static string FreierModellName(PyObjekt aussen, string name)
    {
        bool Belegt(string n) =>
            Get(Modelle(aussen), n) is not null || Get(Bauteile.Gebaeude(Aussen.Stadtdaten(aussen)), n) is not null || Get(Aussen.Bauten(aussen), n) is not null;
        if (!Belegt(name))
        {
            return name;
        }
        for (var i = 2; ; i++)
        {
            if (!Belegt(name + "_" + i))
            {
                return name + "_" + i;
            }
        }
    }
}
