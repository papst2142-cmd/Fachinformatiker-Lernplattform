using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Phase 12 Welle 2 Kleidung (Weisung P12-206, Kurzplan P12-118 Abschnitte 1 und 2, Nico 2b 3a): neue Kleidung in vier
/// angehaengten Stellen (Kleid oder Rock, Jacke oder Mantel, Brille, Tasche), Frisur und Bart an der 3D-Figur, Bart nur beim
/// Anlegen. Alte Spielstaende, Zeilen und Fingerabdruecke bleiben gleich.
/// </summary>
public class SpielKleidungWelle2Tests
{
    private static PyObjekt Inhalt => SpielCharakterTests.InhaltMitNeuen.Value;

    private static PyObjekt Daten => SpielGeschaefteTests.Daten.Value;

    private static Geschaefte Neu(PyObjekt? daten = null) => new(Inhalt, daten ?? Daten);

    private static GrafikBibliothek Grafik => SpielGrafikTests.Bibliothek.Value;

    private static Spielablauf NeuesSpiel(PyObjekt? aussehen = null)
    {
        var zaehler = 0;
        var spiel = new Spielablauf(Inhalt, new SpielSpeicher(() =>
        {
            zaehler += 7;
            return new DateTime(2026, 11, 12).AddSeconds(zaehler).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }));
        spiel.Profil("Nico", aussehen ?? new PyObjekt { { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" } });
        spiel.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 1L }, { "richtig", false }, { "geld", 2000L } });
        return spiel;
    }

    /// <summary>Ein Stueck ohne Laden in den Stand bringen (Kaufzeile wie im Kaufhaus).</summary>
    private static void Gekauft(Spielablauf spiel, string id) =>
        spiel.Log(Kleidung.Gekauft, new PyObjekt { { "stueck", id }, { "geld", -Kleidung.Finden(Daten, id)!.Preis }, { "tag", 1L }, { "laden", Kleidung.Laden } });

    [Fact]
    public void Sortiment_NeueStellenUndStueckeNurAngehaengt()
    {
        Assert.Empty(Kleidung.Pruefen(SpielDaten.Objekt(Daten[Kleidung.Laden])));
        Assert.Empty(Neu().Pruefen(SpielInnenraumTests.Innenraeume.Value));
        var stellen = Kleidung.Stellen(Daten);
        // die vier alten Stellen vorne unveraendert, die neuen dahinter mit ihrer Auslage
        Assert.Equal(["oberteil", "hose", "schuhe", "kopf", "kleid", "jacke", "brille", "tasche"], stellen.Select(s => s.Id));
        Assert.All(stellen.Take(4), s => Assert.Null(s.Auslage));
        Assert.Equal(["oberteil", "oberteil", "kopf", "kopf"], stellen.Skip(4).Select(s => s.Auslage));
        Assert.Equal(["Kleider und Röcke", "Jacken und Mäntel", "Brillen", "Handtaschen"], stellen.Skip(4).Select(s => s.Abteilung));
        var alle = Kleidung.Alle(Daten);
        Assert.Equal(28 + 46, alle.Count);
        // die 28 Stuecke von vorher stehen vorne in derselben Reihenfolge
        Assert.Equal("fruehlingsshirt", alle[27].Id);
        var neue = alle.Skip(28).ToList();
        Assert.All(neue, k => Assert.Null(k.Festzeit));
        // Kurzplan P12-118: Preise je Stueck, 4 bis 6 Farben
        var preise = new Dictionary<string, long>
        {
            ["tshirt"] = 15, ["hemd_lang"] = 35, ["kurz"] = 25, ["rock"] = 35, ["kleid"] = 60, ["jacke"] = 70, ["mantel"] = 120, ["brille"] = 40, ["handtasche"] = 50,
        };
        Assert.Equal(preise.Keys.Order(), neue.Select(k => k.Form).Distinct().Order());
        Assert.All(neue, k => Assert.Equal(preise[k.Form], k.Preis));
        Assert.All(neue.GroupBy(k => k.Form), g => Assert.InRange(g.Count(), 4, 6));
        // jede Stelle hat Ware, jede neue Ware ist im Laden zu kaufen
        Assert.All(stellen, s => Assert.Contains(Kleidung.Sortiment(Daten), k => k.Slot == s.Id));
        Assert.All(neue, k => Assert.Contains(Neu().Waren(Kleidung.Laden), w => w.Id == k.Id && w.Typ == k.Slot));
    }

    /// <summary>Eine angehaengte Stelle braucht eine Auslage: eigene oder die ihrer Stelle "auslage" (Gegenprobe ohne beides).</summary>
    [Fact]
    public void Auslage_UeberDieAndereStelle_GegenprobeRot()
    {
        PyObjekt Kaputt(Action<List<object?>> aendern)
        {
            var d = (PyObjekt)Py.Lesen(PythonJson.Text(Daten))!;
            aendern((List<object?>)SpielDaten.Objekt(d[Kleidung.Laden])["slots"]!);
            return d;
        }
        PyObjekt Stelle(List<object?> s, string id) => s.OfType<PyObjekt>().Single(x => Py.Gleich(x["id"], id));
        // auf eine unbekannte Stelle
        Assert.Contains("kaufhaus: brille: Auslage ungueltig", Neu(Kaputt(s => Py.Setzen(Stelle(s, "brille"), "auslage", "guertel"))).Pruefen(SpielInnenraumTests.Innenraeume.Value));
        // auf eine Stelle, die selbst nur eine geliehene Auslage hat
        Assert.Contains("kaufhaus: brille: Auslage ungueltig", Neu(Kaputt(s => Py.Setzen(Stelle(s, "brille"), "auslage", "tasche"))).Pruefen(SpielInnenraumTests.Innenraeume.Value));
        // ohne Feld: die Stelle hat keine Auslage im Kaufhaus
        var ohne = Kaputt(s => Stelle(s, "jacke").Remove(Stelle(s, "jacke").First(p => p.Key == "auslage")));
        Assert.Contains("kaufhaus: Kleidung ohne Auslage: jacke", Neu(ohne).Pruefen(SpielInnenraumTests.Innenraeume.Value));
        Assert.Equal("oberteil", Kleidung.AuslageVon(Daten, "jacke"));
        Assert.Equal("hose", Kleidung.AuslageVon(Daten, "hose"));
    }

    /// <summary>
    /// Die Zeile kleidung_getragen nennt die neuen Stellen nur, wenn dort etwas getragen wird oder sie schon gespeichert waren:
    /// ein Spielstand ohne neue Kleidung schreibt dieselben Zeilen wie vorher (Fingerabdruck Kaufhaus bleibt).
    /// </summary>
    [Fact]
    public void Getragen_NeueStellenNurWennBelegt()
    {
        var spiel = NeuesSpiel();
        var g = Neu();
        Gekauft(spiel, "hoodie_gruen");
        spiel.KleidungAnziehen(g, "oberteil", "hoodie_gruen");
        Assert.Equal(["oberteil", "hose", "schuhe", "kopf"], SpielDaten.Objekt(spiel.Stand.Verlauf[^1].Daten["getragen"]).Select(p => p.Key));
        Gekauft(spiel, "kleid_rot");
        spiel.KleidungAnziehen(g, "kleid", "kleid_rot");
        var zeile = SpielDaten.Objekt(spiel.Stand.Verlauf[^1].Daten["getragen"]);
        Assert.Equal(["oberteil", "hose", "schuhe", "kopf", "kleid"], zeile.Select(p => p.Key));
        Assert.Equal("kleid_rot", Kleidung.Angezogen(spiel.Stand, Daten)["kleid"].Id);
        // ablegen: die Stelle bleibt mit null in der Zeile (sonst waere unklar, ob sie abgelegt ist)
        spiel.KleidungAnziehen(g, "kleid", null);
        zeile = SpielDaten.Objekt(spiel.Stand.Verlauf[^1].Daten["getragen"]);
        Assert.True(Py.Hat(zeile, "kleid"));
        Assert.Null(zeile["kleid"]);
        Assert.False(Kleidung.Angezogen(spiel.Stand, Daten).ContainsKey("kleid"));
        // ein anderes Stueck tragen haelt die anderen fest
        Gekauft(spiel, "brille_rot");
        spiel.KleidungAnziehen(g, "brille", "brille_rot");
        Assert.Equal(["hoodie_gruen", "brille_rot"], Kleidung.Angezogen(spiel.Stand, Daten).Values.Select(k => k.Id));
        Assert.Empty(spiel.Stand.Unterschiede(spiel.Neuaufbau()));
    }

    /// <summary>Jede neue Form zeigt sich an der Figur: eigene Teile oder umgefaerbte Dreiecke (Gegenprobe: ohne Stueck gleich).</summary>
    [Theory]
    [InlineData("oberteil", "tshirt")]
    [InlineData("oberteil", "hemd_lang")]
    [InlineData("hose", "kurz")]
    [InlineData("kleid", "kleid")]
    [InlineData("kleid", "rock")]
    [InlineData("jacke", "jacke")]
    [InlineData("jacke", "mantel")]
    [InlineData("brille", "brille")]
    [InlineData("tasche", "handtasche")]
    public void Figur_NeueFormSichtbar(string slot, string form)
    {
        var grund = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck(slot, slot is "oberteil" ? "shirt" : slot is "hose" ? "hose" : form, "#DC2626")]);
        var ohne = slot is "oberteil" or "hose" ? grund : Figur.Netz(Grafik, null, 1f, []);
        var mit = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck(slot, form, "#DC2626")]);
        Assert.True(mit.AnzahlEcken > ohne.AnzahlEcken || !Farben(mit).SequenceEqual(Farben(ohne)), form + " zeigt sich nicht");
        // gleicher Aufruf, gleiches Netz
        Assert.Equal(Farben(mit), Farben(Figur.Netz(Grafik, null, 1f, [new Figur.Stueck(slot, form, "#DC2626")])));
    }

    /// <summary>Kurze Aermel und kurze Hose zeigen Haut, das Kleid faerbt Rumpf und Beine oben in seiner Farbe.</summary>
    [Fact]
    public void Figur_KurzeAermelUndKleidFaerben()
    {
        var haut = Netz.Farbe(Figur.Haut["hell"]);
        int Hautnah(Netz n) => Farbton(n, haut);
        var lang = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("oberteil", "shirt", "#DC2626")]);
        var kurz = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("oberteil", "tshirt", "#DC2626")]);
        Assert.True(Hautnah(kurz) > Hautnah(lang), "T-Shirt kurzarm zeigt keine Arme");
        var hose = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("hose", "hose", "#1E3A8A")]);
        var kurzeHose = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("hose", "kurz", "#1E3A8A")]);
        Assert.True(Hautnah(kurzeHose) > Hautnah(hose), "kurze Hose zeigt keine Beine");
        // das Kleid deckt Oberteil und Hose: mit Kleid ist ein getragenes Oberteil nicht mehr zu sehen
        var kleid = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("kleid", "kleid", "#16A34A")]);
        var kleidUndHemd = Figur.Netz(Grafik, null, 1f, [new Figur.Stueck("oberteil", "shirt", "#DC2626"), new Figur.Stueck("kleid", "kleid", "#16A34A")]);
        Assert.Equal(Farben(kleid), Farben(kleidUndHemd));
    }

    /// <summary>
    /// Weisung P12-209: der Saum kurzer Aermel, kurzer Hosen und des Kleids ist eine gerade Kante. Kein Dreieck in Stofffarbe
    /// reicht unter den Saum, kein Dreieck in Hautfarbe ueber ihn (Grenze 0,001 der Hoehe). Gegenprobe: Farbe je ganzem
    /// Dreieck wie vor der Nachbesserung = Zacken.
    /// </summary>
    [Fact]
    public void Figur_SaumGeradeOhneZacken()
    {
        var haut = Netz.Farbe(Figur.Haut["hell"]);
        var stoff = Netz.Farbe("#DC2626");
        static System.Numerics.Vector3 Richtung(System.Numerics.Vector3 v) => v / MathF.Max(v.Length(), 1e-6f);
        // Zahl der Dreiecke im Band um den Saum, die auf der falschen Seite liegen
        var beispiel = new List<string>();
        int Zacken(Figur.Stueck stueck, float saum, bool arm)
        {
            var n = Figur.Netz(Grafik, null, 1f, [stueck]);
            var e = n.Ecken;
            var idx = n.Indizes;
            var falsch = 0;
            for (var t = 0; t + 2 < idx.Count; t += 3)
            {
                float yMin = float.MaxValue, yMax = float.MinValue, xMin = float.MaxValue, xMax = 0f;
                for (var k = 0; k < 3; k++)
                {
                    var i = (int)idx[t + k] * Netz.Zahlen;
                    yMin = MathF.Min(yMin, e[i + 1]);
                    yMax = MathF.Max(yMax, e[i + 1]);
                    xMin = MathF.Min(xMin, MathF.Abs(e[i]));
                    xMax = MathF.Max(xMax, MathF.Abs(e[i]));
                }
                // Arm: ganz aussen; Bein: ganz innen und nur nahe am Saum (die Haende haengen hoeher daneben)
                if (yMax < saum - 0.08f || yMin > saum + 0.08f || (arm ? xMin < Figur.ArmAb : xMax >= Figur.ArmAb))
                {
                    continue;
                }
                var f = Richtung(n.FarbeAn((int)idx[t]));
                var istStoff = System.Numerics.Vector3.Distance(f, Richtung(stoff)) < 0.05f;
                var istHaut = System.Numerics.Vector3.Distance(f, Richtung(haut)) < 0.05f;
                if ((istStoff && yMin < saum - 0.001f) || (istHaut && yMax > saum + 0.001f))
                {
                    falsch++;
                    if (beispiel.Count < 6)
                    {
                        beispiel.Add($"{stueck.Form} {(istStoff ? "Stoff" : "Haut")} y {yMin:0.000}-{yMax:0.000} x {xMin:0.000} Dreieck {t / 3}");
                    }
                }
            }
            return falsch;
        }
        var faelle = new (Figur.Stueck Stueck, float Saum, bool Arm)[]
        {
            (new("oberteil", "tshirt", "#DC2626"), Figur.KurzarmBis, true),
            (new("kleid", "kleid", "#DC2626"), Figur.KurzarmBis, true),
            (new("hose", "kurz", "#DC2626"), Figur.BeinFreiKurz, false),
        };
        foreach (var (stueck, saum, arm) in faelle)
        {
            var z = Zacken(stueck, saum, arm);
            Assert.True(z == 0, z + " Zacken: " + string.Join(" | ", beispiel));
        }
        Figur.SaumAltFuerTests = true;
        try
        {
            foreach (var (stueck, saum, arm) in faelle)
            {
                Assert.True(Zacken(stueck, saum, arm) > 0, "Gegenprobe ohne Zacken: " + stueck.Form);
            }
        }
        finally
        {
            Figur.SaumAltFuerTests = false;
        }
    }

    /// <summary>
    /// Weisung P12-209: der kurze Aermel endet rundum gerade, auch an der Innenseite des Arms (die liegt naeher an der Mitte
    /// als <see cref="Figur.ArmAb"/>; mit der Regel nach der Lage blieb sie unter dem Saum in Stoff und zeigte Zacken).
    /// Geprueft an Punkten in jedem Arm-Dreieck (erkannt an der Gehbewegung): ueber dem Saum Stoff, darunter Haut.
    /// Gegenprobe: Stand vor der Nachbesserung zeigt Stoff unter dem Saum.
    /// </summary>
    [Fact]
    public void Figur_KurzerAermel_RundumGerade()
    {
        var gm = Grafik.Modell(Figur.Modell)!;
        var arme = Figur.Arme(Grafik, gm);
        Assert.NotNull(arme);
        var groesse = gm.Max.Y - gm.Min.Y;
        var mitte = (gm.Min + gm.Max) / 2;
        System.Numerics.Vector3 Ecke(int d, int k) => new System.Numerics.Vector3(
            gm.Ecken[(d * 9) + (k * 3)] - mitte.X, gm.Ecken[(d * 9) + (k * 3) + 1] - gm.Min.Y, gm.Ecken[(d * 9) + (k * 3) + 2] - mitte.Z) / groesse;
        // Innenseite: Arm-Dreiecke naeher an der Mitte als ArmAb muss es geben (sonst prueft der Test nichts Neues)
        var innen = Enumerable.Range(0, gm.Dreiecke).Count(d => arme![d]
            && MathF.Abs((Ecke(d, 0).X + Ecke(d, 1).X + Ecke(d, 2).X) / 3) < Figur.ArmAb
            && MathF.Abs(((Ecke(d, 0).Y + Ecke(d, 1).Y + Ecke(d, 2).Y) / 3) - Figur.KurzarmBis) < 0.06f);
        Assert.True(innen >= 10, "Innenseite des Arms: " + innen);
        var haut = Netz.Farbe(Figur.Haut["hell"]);
        var stoff = Netz.Farbe("#DC2626");
        static System.Numerics.Vector3 Richtung(System.Numerics.Vector3 v) => v / MathF.Max(v.Length(), 1e-6f);
        var beispiel = new List<string>();
        int Falsch(Figur.Stueck stueck)
        {
            var n = Figur.Netz(Grafik, null, 1f, [stueck]);
            var e = n.Ecken;
            var idx = n.Indizes;
            System.Numerics.Vector3 P(int t, int k)
            {
                var i = (int)idx[t + k] * Netz.Zahlen;
                return new(e[i], e[i + 1], e[i + 2]);
            }
            // Farbe des Netzes an einem Punkt: das Dreieck gleicher Blickrichtung, in dessen Ebene und Flaeche der Punkt liegt
            // (wo der Arm am Rumpf anliegt, liegen Arm und Rumpf Ruecken an Ruecken in einer Ebene; das ist innen, unsichtbar)
            System.Numerics.Vector3? FarbeBei(System.Numerics.Vector3 p, System.Numerics.Vector3 richtung)
            {
                for (var t = 0; t + 2 < idx.Count; t += 3)
                {
                    var a = P(t, 0);
                    var ab = P(t, 1) - a;
                    var ac = P(t, 2) - a;
                    var nn = System.Numerics.Vector3.Cross(ab, ac);
                    var l = nn.Length();
                    if (l < 1e-9f || MathF.Abs(System.Numerics.Vector3.Dot(p - a, nn / l)) > 1e-4f
                        || System.Numerics.Vector3.Dot(nn / l, richtung) < 0.9f)
                    {
                        continue;
                    }
                    var ap = p - a;
                    float d00 = System.Numerics.Vector3.Dot(ab, ab), d01 = System.Numerics.Vector3.Dot(ab, ac), d11 = System.Numerics.Vector3.Dot(ac, ac);
                    float d20 = System.Numerics.Vector3.Dot(ap, ab), d21 = System.Numerics.Vector3.Dot(ap, ac);
                    var den = (d00 * d11) - (d01 * d01);
                    var v = ((d11 * d20) - (d01 * d21)) / den;
                    var w = ((d00 * d21) - (d01 * d20)) / den;
                    if (v >= -1e-4f && w >= -1e-4f && v + w <= 1 + 1e-4f)
                    {
                        return n.FarbeAn((int)idx[t]);
                    }
                }
                return null;
            }
            var falsch = 0;
            for (var d = 0; d < gm.Dreiecke; d++)
            {
                if (!arme![d])
                {
                    continue;
                }
                foreach (var (u, v) in new[] { (1f / 3, 1f / 3), (0.7f, 0.15f), (0.15f, 0.7f), (0.15f, 0.15f) })
                {
                    var p = Ecke(d, 0) + (u * (Ecke(d, 1) - Ecke(d, 0))) + (v * (Ecke(d, 2) - Ecke(d, 0)));
                    var abstand = p.Y - Figur.KurzarmBis;
                    var richtung = System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(Ecke(d, 1) - Ecke(d, 0), Ecke(d, 2) - Ecke(d, 0)));
                    if (MathF.Abs(abstand) < 0.003f || MathF.Abs(abstand) > 0.06f || FarbeBei(p, richtung) is not { } f)
                    {
                        continue;
                    }
                    var soll = abstand > 0 ? stoff : haut;
                    if (System.Numerics.Vector3.Distance(Richtung(f), Richtung(soll)) > 0.05f)
                    {
                        falsch++;
                        if (beispiel.Count < 6)
                        {
                            beispiel.Add($"{stueck.Form} Dreieck {d} y {p.Y:0.000} x {p.X:0.000} {(abstand > 0 ? "Stoff fehlt" : "Haut fehlt")}");
                        }
                    }
                }
            }
            return falsch;
        }
        var faelle = new Figur.Stueck[] { new("oberteil", "tshirt", "#DC2626"), new("kleid", "kleid", "#DC2626") };
        foreach (var st in faelle)
        {
            var z = Falsch(st);
            Assert.True(z == 0, z + " Punkte falsch: " + string.Join(" | ", beispiel));
        }
        Figur.SaumAltFuerTests = true;
        try
        {
            foreach (var st in faelle)
            {
                Assert.True(Falsch(st) > 0, "Gegenprobe ohne Zacken: " + st.Form);
            }
        }
        finally
        {
            Figur.SaumAltFuerTests = false;
        }
    }

    /// <summary>
    /// Weisung P12-210 (Nico): ohne Bart ist das Gesicht unten hautfarben. Kinn, Unterkiefer und Hals vorn haben die Farbe
    /// der Haut in der Helligkeit des Gesichts (kein dunkler Schatten, kein Haar-Rest, der wie ein Bart wirkt), bei jedem
    /// Hautton. Ein echter Bart hebt sich davon ab. Gegenprobe: vor der Nachbesserung war der Unterkiefer dunkel.
    /// </summary>
    [Fact]
    public void Figur_OhneBartKinnHautfarben_BartHebtSichAb()
    {
        static System.Numerics.Vector3 Richtung(System.Numerics.Vector3 v) => v / MathF.Max(v.Length(), 1e-6f);
        // Dreiecke vorn im Bereich Kinn bis Hals (Figur 1 hoch, Mitte bei x = z = 0), ohne Mund und Augen
        List<System.Numerics.Vector3> Kinnfarben(PyObjekt aussehen)
        {
            var n = Figur.Netz(Grafik, aussehen, 1f, []);
            var e = n.Ecken;
            var idx = n.Indizes;
            var liste = new List<System.Numerics.Vector3>();
            for (var t = 0; t + 2 < idx.Count; t += 3)
            {
                float yMin = float.MaxValue, yMax = float.MinValue, x = 0, z = 0;
                for (var k = 0; k < 3; k++)
                {
                    var i = (int)idx[t + k] * Netz.Zahlen;
                    yMin = MathF.Min(yMin, e[i + 1]);
                    yMax = MathF.Max(yMax, e[i + 1]);
                    x += e[i] / 3;
                    z += e[i + 2] / 3;
                }
                if (yMin >= Figur.KinnAb + 0.01f && yMax <= 0.8f && MathF.Abs(x) < 0.1f && z > 0.01f)
                {
                    liste.Add(n.FarbeAn((int)idx[t]));
                }
            }
            return liste;
        }
        PyObjekt Aussehen(string haut, string? bart) => bart is null
            ? new PyObjekt { { "haut", haut }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" } }
            : new PyObjekt { { "haut", haut }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "bart", bart } };
        // dunkel = nicht in Hautfarbe oder dunkler als 85 % der Gesichtsfarbe
        int Dunkel(string haut, List<System.Numerics.Vector3> f)
        {
            var h = Netz.Farbe(Figur.Haut[haut]) * Figur.GesichtHell;
            // der Mund (Figur.Gesicht) liegt im Bereich und zaehlt nicht
            var mund = System.Numerics.Vector3.Clamp((Netz.Farbe(Figur.Haut[haut]) * 0.55f) + new System.Numerics.Vector3(0.18f, 0.02f, 0.02f), System.Numerics.Vector3.Zero, System.Numerics.Vector3.One);
            return f.Count(c => System.Numerics.Vector3.Distance(c, mund) > 0.01f && (System.Numerics.Vector3.Distance(Richtung(c), Richtung(h)) > 0.03f || c.Length() < h.Length() * 0.85f));
        }
        foreach (var haut in Figur.Haut.Keys)
        {
            var ohne = Kinnfarben(Aussehen(haut, null));
            Assert.True(ohne.Count >= 10, haut + ": zu wenige Dreiecke am Kinn " + ohne.Count);
            Assert.True(Dunkel(haut, ohne) == 0, haut + ": " + Dunkel(haut, ohne) + " dunkle Dreiecke am Kinn ohne Bart: "
                + string.Join(" ", ohne.Select(c => c.ToString("0.00", CultureInfo.InvariantCulture))));
        }
        // Der Bart (Haarfarbe braun) liegt vorn am Kinn und ist klar dunkler als die Haut
        var haar = Netz.Farbe(Figur.Haare["braun"]) * 0.95f;
        foreach (var bart in (string[])["vollbart"])
        {
            var mit = Kinnfarben(Aussehen("hell", bart));
            var bartTeile = mit.Count(c => System.Numerics.Vector3.Distance(c, haar) < 0.02f);
            Assert.True(bartTeile > 0, "Bart nicht am Kinn");
            Assert.True(haar.Length() < Netz.Farbe(Figur.Haut["hell"]).Length() * Figur.GesichtHell * 0.6f, "Bart hebt sich nicht ab");
        }
        Figur.KinnAltFuerTests = true;
        try
        {
            Assert.True(Dunkel("hell", Kinnfarben(Aussehen("hell", null))) > 0, "Gegenprobe: Kinn war schon vorher hell");
        }
        finally
        {
            Figur.KinnAltFuerTests = false;
        }
    }

    /// <summary>
    /// Weisung P12-210: auch von der Seite kein Bartschatten. Am Unterkiefer seitlich bis hinter das Ohr ist die Haut so hell
    /// wie das Gesicht; am Hinterkopf unter dem Haaransatz bleibt kein dunkler Rest der Vorlage (bei Glatze Haut, bei Haar
    /// die Haarfarbe). Gegenprobe: vorher war der Unterkiefer seitlich dunkler und die Glatze hatte einen dunklen Fleck.
    /// </summary>
    [Fact]
    public void Figur_KopfSeiteUndNacken_OhneDunkleReste()
    {
        static System.Numerics.Vector3 Richtung(System.Numerics.Vector3 v) => v / MathF.Max(v.Length(), 1e-6f);
        // Farben der Dreiecke am Kopf zwischen KinnAb und oben mit Mitte in der Tiefe von zVon bis zBis (Figur 1 hoch)
        List<System.Numerics.Vector3> Farben(string frisur, string haarfarbe, float zVon, float zBis, float oben = 0.8f)
        {
            var aussehen = new PyObjekt { { "haut", "hell" }, { "frisur", frisur }, { "haarfarbe", haarfarbe }, { "oberteil", "cyan" } };
            var n = Figur.Netz(Grafik, aussehen, 1f, []);
            var e = n.Ecken;
            var idx = n.Indizes;
            var liste = new List<System.Numerics.Vector3>();
            for (var t = 0; t + 2 < idx.Count; t += 3)
            {
                float yMin = float.MaxValue, yMax = float.MinValue, z = 0;
                for (var k = 0; k < 3; k++)
                {
                    var i = (int)idx[t + k] * Netz.Zahlen;
                    yMin = MathF.Min(yMin, e[i + 1]);
                    yMax = MathF.Max(yMax, e[i + 1]);
                    z += e[i + 2] / 3;
                }
                if (yMin >= Figur.KinnAb + 0.01f && yMax <= oben && z > zVon && z <= zBis)
                {
                    liste.Add(n.FarbeAn((int)idx[t]));
                }
            }
            return liste;
        }
        var haut = Netz.Farbe(Figur.Haut["hell"]);
        var gesicht = haut * Figur.GesichtHell;
        var hemd = Richtung(Netz.Farbe(Figur.Oberteil["cyan"]));
        // Seite: hautfarben, aber dunkler als 85 % des Gesichts, oder weder Haut noch Haar noch Hemd
        int SeiteDunkel(string haarfarbe)
        {
            var haar = Richtung(Netz.Farbe(Figur.Haare[haarfarbe]));
            return Farben("kurz", haarfarbe, -Figur.KinnHinten + 0.02f, 0.01f).Count(c =>
                System.Numerics.Vector3.Distance(Richtung(c), Richtung(haut)) < 0.03f
                    ? c.Length() < gesicht.Length() * 0.85f
                    : System.Numerics.Vector3.Distance(Richtung(c), haar) > 0.03f && System.Numerics.Vector3.Distance(Richtung(c), hemd) > 0.03f);
        }
        // Hinterkopf bis KinnBis. Glatze hinten: alles in Hautfarbe (kein dunkler Rest); blond hinten: kein Dreieck in einer anderen Farbe als Haut oder Haar
        List<System.Numerics.Vector3> GlatzeListe() => Farben("glatze", "braun", -1f, 0f, Figur.KinnBis).FindAll(c =>
            System.Numerics.Vector3.Distance(Richtung(c), Richtung(haut)) > 0.03f && System.Numerics.Vector3.Distance(Richtung(c), hemd) > 0.03f);
        int GlatzeFremd() => GlatzeListe().Count;
        int BlondFremd()
        {
            var haar = Richtung(Netz.Farbe(Figur.Haare["blond"]));
            return Farben("kurz", "blond", -1f, 0f, Figur.KinnBis).Count(c => System.Numerics.Vector3.Distance(Richtung(c), Richtung(haut)) > 0.03f
                && System.Numerics.Vector3.Distance(Richtung(c), haar) > 0.03f && System.Numerics.Vector3.Distance(Richtung(c), hemd) > 0.03f);
        }
        Assert.True(Farben("kurz", "braun", -Figur.KinnHinten + 0.02f, 0.01f).Count >= 6, "zu wenige Dreiecke seitlich");
        Assert.Equal(0, SeiteDunkel("braun"));
        Assert.Equal(0, SeiteDunkel("blond"));
        Assert.True(GlatzeFremd() == 0, "Glatze hinten: " + string.Join(" ", GlatzeListe().Select(c => c.ToString("0.00", CultureInfo.InvariantCulture))));
        Assert.Equal(0, BlondFremd());
        Figur.KinnAltFuerTests = true;
        try
        {
            Assert.True(SeiteDunkel("braun") > 0, "Gegenprobe: Unterkiefer seitlich war schon vorher hell");
            Assert.True(GlatzeFremd() > 0, "Gegenprobe: Glatze hatte schon vorher keinen dunklen Rest");
        }
        finally
        {
            Figur.KinnAltFuerTests = false;
        }
    }

    /// <summary>
    /// Weisung P12-209: Locken sind auch von vorn und von der Seite zu sehen (Teile seitlich, hinten und vorn am Kopf, nicht
    /// nur oben). Gegenprobe: Locken vor der Nachbesserung haben dort nichts.
    /// </summary>
    [Fact]
    public void Figur_LockenVonVornUndSeiteSichtbar()
    {
        var kopf = Figur.Kopf(Grafik.Modell(Figur.Modell)!)!.Value;
        int Seitlich(List<Teil> teile) => teile.Count(t => t.Y1 < kopf.Max.Y && (t.X1 < kopf.Min.X || t.X2 > kopf.Max.X || t.Z2 > kopf.Max.Z));
        var haar = new System.Numerics.Vector3(0.4f, 0.25f, 0.1f);
        Assert.True(Seitlich(Figur.Frisur(kopf.Min, kopf.Max, "locken", haar, null)) >= 8);
        Figur.LockenAltFuerTests = true;
        try
        {
            Assert.Equal(0, Seitlich(Figur.Frisur(kopf.Min, kopf.Max, "locken", haar, null)));
        }
        finally
        {
            Figur.LockenAltFuerTests = false;
        }
    }

    /// <summary>Farben aller Ecken eines Netzes.</summary>
    private static List<System.Numerics.Vector3> Farben(Netz n) => [.. Enumerable.Range(0, n.AnzahlEcken).Select(n.FarbeAn)];

    /// <summary>Ecken mit dem Farbton von <paramref name="farbe"/> (gleiche Richtung, Helligkeit egal: die Vorlage schattiert).</summary>
    private static int Farbton(Netz n, System.Numerics.Vector3 farbe)
    {
        static System.Numerics.Vector3 Richtung(System.Numerics.Vector3 v) => v / MathF.Max(v.Length(), 1e-6f);
        return Farben(n).Count(f => System.Numerics.Vector3.Distance(Richtung(f), Richtung(farbe)) < 0.03f);
    }

    /// <summary>Nico 3a: die Frisur ist auch an der 3D-Figur zu sehen; „kurz“ bleibt die Figur von vorher (Gegenprobe).</summary>
    [Fact]
    public void Figur_FrisurUndBartSichtbar()
    {
        PyObjekt Mit(string teil, string wert)
        {
            var a = Aussehen.Voreinstellung();
            Py.Setzen(a, teil, wert);
            return a;
        }
        var kurz = Figur.Netz(Grafik, Aussehen.Voreinstellung(), 1f);
        Assert.Equal(Farben(kurz), Farben(Figur.Netz(Grafik, null, 1f)));
        foreach (var frisur in new[] { "lang", "zopf", "locken" })
        {
            Assert.True(Figur.Netz(Grafik, Mit("frisur", frisur), 1f).AnzahlEcken > kurz.AnzahlEcken, frisur);
        }
        var glatze = Figur.Netz(Grafik, Mit("frisur", "glatze"), 1f);
        Assert.Equal(kurz.AnzahlEcken, glatze.AnzahlEcken);
        Assert.NotEqual(Farben(kurz), Farben(glatze));
        foreach (var bart in new[] { "vollbart", "schnurrbart" })
        {
            Assert.True(Figur.Netz(Grafik, Mit("bart", bart), 1f).AnzahlEcken > kurz.AnzahlEcken, bart);
        }
        // Locken unter einer Kappe fallen weg (sie wuerden durch die Kappe stechen), lange Haare bleiben
        var kappe = new Figur.Stueck("kopf", "kappe", "#DC2626");
        Assert.Equal(Figur.Netz(Grafik, null, 1f, [kappe]).AnzahlEcken, Figur.Netz(Grafik, Mit("frisur", "locken"), 1f, [kappe]).AnzahlEcken);
        Assert.True(Figur.Netz(Grafik, Mit("frisur", "lang"), 1f, [kappe]).AnzahlEcken > Figur.Netz(Grafik, null, 1f, [kappe]).AnzahlEcken);
    }

    /// <summary>
    /// Der Bart ist ein Teil nur der C#-Linie: die Python-Teile (zufaellige Personen, G-Dateien) bleiben, im Aussehen steht er nur
    /// mit einer Wahl ausser „Keiner“, unbekannte Werte fallen weg.
    /// </summary>
    [Fact]
    public void Aussehen_BartNurWennGewaehlt()
    {
        Assert.Equal(["haut", "frisur", "haarfarbe", "oberteil", "extra", "kreis"], Aussehen.Teile.Select(t => t.Teil));
        Assert.Equal(["haut", "frisur", "haarfarbe", "oberteil", "extra", "kreis"], Aussehen.Voreinstellung().Select(p => p.Key));
        Assert.Equal(["keiner", "vollbart", "schnurrbart"], Aussehen.WahlFuer("bart").Select(w => w.Kennung));
        PyObjekt Mit(object? bart) => new() { { "haut", "hell" }, { "bart", bart } };
        Assert.Equal("vollbart", Aussehen.Normalisieren(Mit("vollbart"))["bart"]);
        Assert.False(Py.Hat(Aussehen.Normalisieren(Mit("keiner")), "bart"));
        Assert.False(Py.Hat(Aussehen.Normalisieren(Mit("ziegenbart")), "bart"));
        Assert.False(Py.Hat(Aussehen.Normalisieren(Mit(1L)), "bart"));
        Assert.Equal("keiner", Aussehen.Wert(Aussehen.Voreinstellung(), "bart"));
        // die Zeile profil_gesetzt traegt den Bart nur, wenn einer gewaehlt ist
        var mit = NeuesSpiel(new PyObjekt { { "haut", "hell" }, { "bart", "schnurrbart" } });
        Assert.Equal("schnurrbart", SpielDaten.Objekt(mit.Stand.Profil!["aussehen"])["bart"]);
        var ohne = NeuesSpiel(new PyObjekt { { "haut", "hell" }, { "bart", "keiner" } });
        Assert.False(Py.Hat(SpielDaten.Objekt(ohne.Stand.Profil!["aussehen"]), "bart"));
        Assert.Empty(mit.Stand.Unterschiede(mit.Neuaufbau()));
    }

    /// <summary>
    /// Jede Frisur, jeder Bart und jedes neue Stueck (auch alles zusammen) bleibt im festen Halbmesser der Figur
    /// (<see cref="StadtSpiel.FigurHalbmesser"/>, Weisung P12-197): beim Ansprechen durchdringen sich zwei Figuren nicht.
    /// Gegenprobe: ein Teil eine Kopfbreite neben dem Kopf liegt ausserhalb.
    /// </summary>
    [Fact]
    public void Figur_AllesImHalbmesser()
    {
        static float Halbmesser(Netz netz)
        {
            var e = netz.Ecken;
            var r = 0f;
            for (var i = 0; i < e.Count / Netz.Zahlen; i++)
            {
                r = MathF.Max(r, new System.Numerics.Vector2(e[i * Netz.Zahlen], e[(i * Netz.Zahlen) + 2]).Length());
            }
            return r * Innenraum.FigurHoehe;
        }
        var stuecke = new Figur.Stueck[]
        {
            new("oberteil", "hemd_lang", "#DC2626"), new("oberteil", "tshirt", "#DC2626"), new("hose", "kurz", "#DC2626"),
            new("kleid", "kleid", "#DC2626"), new("kleid", "rock", "#DC2626"), new("jacke", "jacke", "#DC2626"),
            new("jacke", "mantel", "#DC2626"), new("brille", "brille", "#DC2626"), new("tasche", "handtasche", "#DC2626"),
        };
        var geprueft = 0;
        foreach (var frisur in Aussehen.Teile.First(t => t.Teil == "frisur").Wahl.Select(w => w.Kennung))
        {
            foreach (var bart in Aussehen.WahlFuer("bart").Select(w => w.Kennung))
            {
                var a = new PyObjekt { { "frisur", frisur }, { "bart", bart } };
                foreach (var kleidung in stuecke.Select(s => (Figur.Stueck[])[s]).Append([]).Append(
                    [new("kleid", "kleid", "#DC2626"), new("jacke", "mantel", "#DC2626"), new("brille", "brille", "#DC2626"), new("tasche", "handtasche", "#DC2626")]))
                {
                    var r = Halbmesser(Figur.Netz(Grafik, a, 1f, kleidung));
                    Assert.True(r <= StadtSpiel.FigurHalbmesser, $"{frisur} {bart} {string.Join(",", kleidung.Select(k => k.Form))}: {r}");
                    geprueft++;
                }
            }
        }
        Assert.True(geprueft >= 5 * 3 * 11, "geprueft: " + geprueft);
        // Gegenprobe: ein Teil eine Kopfbreite neben dem Kopf
        var kopf = Figur.Kopf(Grafik.Modell(Figur.Modell)!)!.Value;
        var breit = kopf.Max.X - kopf.Min.X;
        var zuWeit = Figur.Frisur(kopf.Min + new System.Numerics.Vector3(breit, 0, 0), kopf.Max + new System.Numerics.Vector3(breit, 0, 0), "lang", System.Numerics.Vector3.One, null);
        var gm = Grafik.Modell(Figur.Modell)!;
        var s1 = 1f / (gm.Max.Y - gm.Min.Y);
        var mitte = (gm.Min + gm.Max) / 2;
        Assert.True(zuWeit.Max(t => MathF.Abs(t.X2 - mitte.X) * s1 * Innenraum.FigurHoehe) > StadtSpiel.FigurHalbmesser);
    }

    /// <summary>Der 2D-Avatar zeigt den Bart; ohne Bart sind die Formen wie in Python (Gegenprobe).</summary>
    [Fact]
    public void Avatar_BartNurMitBart()
    {
        var ohne = SpielBilder.Avatar(Aussehen.Voreinstellung());
        var keiner = Aussehen.Voreinstellung();
        keiner.Add("bart", "keiner");
        Assert.Equal(ohne, SpielBilder.Avatar(keiner));
        var voll = Aussehen.Voreinstellung();
        voll.Add("bart", "vollbart");
        Assert.Equal(ohne.Count + 2, SpielBilder.Avatar(voll).Count);
        var schnurr = Aussehen.Voreinstellung();
        schnurr.Add("bart", "schnurrbart");
        Assert.Equal(ohne.Count + 1, SpielBilder.Avatar(schnurr).Count);
    }
}
