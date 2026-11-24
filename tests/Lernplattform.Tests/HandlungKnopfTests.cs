using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Welle A 1.6.0 (Sammelliste 444, Weisung P12-360 Antwort 4, Nico: „ich sicht als kleines symbol ebenfalls links übern
/// Joystik“). Abnahmesatz: „Am Handy und Tablet steht der Handlungsknopf ueber dem linken Stick am linken Rand, das kleine Symbol
/// der Ich-Sicht rechts daneben; keiner verdeckt den anderen, keiner liegt im Kreis des Sticks, beide bleiben in der linken
/// Haelfte und unter der Kopfzeile. Ein Daumen in der Ecke setzt dort den linken Stick, ein Druck auf einen der Knoepfe setzt
/// keinen Stick.“ Gegenprobe: ohne die Sperre der Knoepfe legt ein Druck auf den Handlungsknopf dort einen Stick an.
/// </summary>
public sealed class HandlungKnopfTests
{
    private static readonly (string Name, double W, double H)[] Groessen =
        [("S24 quer", 832.0, 384.0), ("S24 hoch", 384.0, 832.0), ("Tab quer", 1280.0, 800.0), ("Tab hoch", 800.0, 1280.0), ("Emulator", 640.0, 320.0)];

    private static double Abstand((double X, double Y) a, (double X, double Y) b) => Math.Sqrt(((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

    [Fact]
    public void Lage_UeberDemLinkenStick_NichtImKreis_NichtUebereinander_SperreWirkt()
    {
        var funde = new List<string>();
        var gegen = 0;
        foreach (var (name, w, h) in Groessen)
        {
            foreach (var leiste in new[] { 180.0, 420.0, 0.0 })
            {
                var b = leiste > 0 ? new Sticks.Bereich(w, h, 64, 86, (w - leiste) / 2, (w + leiste) / 2) : new Sticks.Bereich(w, h, 64, 86);
                var wo = $"{name} Leiste {leiste}";
                if (HandlungKnopf.Lage(b) is not { } lage)
                {
                    funde.Add(wo + ": keine Lage");
                    continue;
                }
                var (ha, ich) = lage;
                var (g, k) = HandlungKnopf.Groesse(w, h);
                Assert.Equal(HaltungKnopf.Groesse(w, h), (g, k));
                var (sx, sy, sr) = Sticks.Lage(0, h, Sticks.Seite.Links, b);
                if (Abstand(ha, ich) < (g / 2) + (k / 2))
                {
                    funde.Add(wo + ": Handlung und Ich-Sicht ueberdecken sich");
                }
                if (!(ich.X > ha.X && ich.Y <= ha.Y))
                {
                    funde.Add(wo + ": Ich-Sicht nicht rechts neben/ueber der Handlung");
                }
                foreach (var ((x, y), d, was) in new[] { (ha, g, "Handlung"), (ich, k, "Ich-Sicht") })
                {
                    if (Abstand((x, y), (sx, sy)) < sr + (d / 2) + HaltungKnopf.Rand - 1e-6)
                    {
                        funde.Add($"{wo}: {was} im Kreis des Sticks");
                    }
                    if (x - (d / 2) < 0 || x + (d / 2) > w / 2 || y - (d / 2) < b.Oben || y + (d / 2) > h)
                    {
                        funde.Add($"{wo}: {was} ausserhalb ({x:F0}/{y:F0})");
                    }
                }
                // Handlung am linken Rand ueber dem Stick (nur im flachen Bild rechts neben ihm)
                if (Math.Abs(ha.X - (g / 2) - HaltungKnopf.Rand) > 1e-6 && ha.X - (g / 2) < sx + sr)
                {
                    funde.Add($"{wo}: Handlung weder am linken Rand noch neben dem Stick ({ha.X:F0}/{ha.Y:F0})");
                }
                // mit der Sperre: ein Daumen in der Ecke setzt den Stick dort, ein Druck auf die Knoepfe setzt keinen
                var mit = b with { SperreLinks = HandlungKnopf.Sperre(b) };
                var daumen = (X: sx - (sr / 2), Y: sy + (sr / 2));
                if (Sticks.SeiteFuer(daumen.X, daumen.Y, mit) != Sticks.Seite.Links)
                {
                    funde.Add(wo + ": in der Ecke beginnt kein linker Stick");
                }
                foreach (var p in new[] { ha, ich })
                {
                    if (!mit.Gesperrt(p.X, p.Y))
                    {
                        funde.Add($"{wo}: Knopf ({p.X:F0}/{p.Y:F0}) nicht gesperrt");
                    }
                }
                // ein Stick nahe den Knoepfen weicht ihnen aus (sein Kreis liegt nie auf ihnen)
                var nahe = (X: ha.X + (g / 2) + 6, Y: ha.Y + (g / 2) + 6);
                var (nx, ny, nr) = Sticks.Lage(nahe.X, nahe.Y, Sticks.Seite.Links, mit);
                if (Sticks.KreisInRechteck(nx, ny, nr, mit.SperreLinks))
                {
                    funde.Add($"{wo}: Stick bei ({nahe.X:F0}/{nahe.Y:F0}) liegt auf den Knoepfen");
                }
                // Gegenprobe: ohne Sperre setzt der Druck auf den Handlungsknopf einen Stick
                if (!b.Gesperrt(ha.X, ha.Y) && Sticks.SeiteFuer(ha.X, ha.Y, b) == Sticks.Seite.Links)
                {
                    gegen++;
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.True(gegen >= 6, "Gegenprobe greift nicht: " + gegen);
    }

    /// <summary>
    /// Weisung P12-361 (Nico: „wenn das sich alles um die Finger abspielt, also um den Daumen jeweils“). Nico 1a, Weisung
    /// P12-362. Abnahmesatz: „Das Handy-Symbol steht rechts neben dem linken Stick in der linken Haelfte, nicht in seinem Kreis, nicht auf Handlung, Ich-Sicht, Springen oder Haltung, nicht auf der Leiste und unter der Kopfzeile; ein Druck
    /// darauf setzt keinen Stick, ein Stick daneben weicht ihm aus.“ Gegenprobe: ohne die Sperre setzt der Druck dort einen Stick.
    /// </summary>
    [Fact]
    public void Handy_RechtsNebenDemLinkenStick_FreiUndGesperrt()
    {
        var funde = new List<string>();
        var gegen = 0;
        foreach (var (name, w, h) in Groessen)
        {
            foreach (var leiste in new[] { 180.0, 420.0, 0.0 })
            {
                var b0 = leiste > 0 ? new Sticks.Bereich(w, h, 64, 86, (w - leiste) / 2, (w + leiste) / 2) : new Sticks.Bereich(w, h, 64, 86);
                var wo = $"{name} Leiste {leiste}";
                if (HandlungKnopf.Telefon(b0) is not { } m)
                {
                    funde.Add(wo + ": keine Lage");
                    continue;
                }
                var (_, k) = HandlungKnopf.Groesse(w, h);
                var (sx, sy, sr) = Sticks.Lage(0, h, Sticks.Seite.Links, b0);
                if (Abstand(m, (sx, sy)) < sr + (k / 2) + HaltungKnopf.Rand - 1e-6)
                {
                    funde.Add(wo + ": im Kreis des Sticks");
                }
                if (m.X <= sx + sr || m.X + (k / 2) > w / 2)
                {
                    funde.Add($"{wo}: nicht neben dem Stick in seiner Haelfte ({m.X:F0}/{m.Y:F0}, Stick {sx:F0})");
                }
                if (m.Y - (k / 2) < b0.Oben || m.Y + (k / 2) > h)
                {
                    funde.Add($"{wo}: ausserhalb ({m.X:F0}/{m.Y:F0})");
                }
                if (b0.LeisteBis > b0.LeisteVon && m.X + (k / 2) > b0.LeisteVon && m.X - (k / 2) < b0.LeisteBis && m.Y + (k / 2) > h - b0.Unten)
                {
                    funde.Add($"{wo}: auf der Leiste ({m.X:F0}/{m.Y:F0})");
                }
                // nicht auf den anderen runden Knoepfen
                var andere = new List<(double X1, double Y1, double X2, double Y2)> { HaltungKnopf.Sperre(b0), HandlungKnopf.Sperre(b0) };
                foreach (var r in andere.Where(r => r != default))
                {
                    if (m.X + (k / 2) > r.X1 && m.X - (k / 2) < r.X2 && m.Y + (k / 2) > r.Y1 && m.Y - (k / 2) < r.Y2)
                    {
                        funde.Add($"{wo}: liegt auf anderen Knoepfen ({r.X1:F0}/{r.Y1:F0}-{r.X2:F0}/{r.Y2:F0})");
                    }
                }
                var mit = b0 with { Sperre = HaltungKnopf.Sperre(b0), SperreLinks = HandlungKnopf.Sperre(b0), SperreTelefon = HandlungKnopf.TelefonSperre(b0) };
                if (!mit.Gesperrt(m.X, m.Y) || Sticks.SeiteFuer(m.X, m.Y, mit) is not null)
                {
                    funde.Add($"{wo}: Druck auf das Handy setzt einen Stick");
                }
                var ecke = (X: sx - (sr / 2), Y: sy + (sr / 2));
                if (Sticks.SeiteFuer(ecke.X, ecke.Y, mit) != Sticks.Seite.Links)
                {
                    funde.Add(wo + ": in der Ecke beginnt kein Stick");
                }
                var nahe = (X: m.X - (k / 2) - 4, Y: m.Y + (k / 2) + 4);
                var (nx, ny, nr) = Sticks.Lage(nahe.X, nahe.Y, Sticks.Seite.Links, mit);
                if (Sticks.KreisInRechteck(nx, ny, nr, mit.SperreTelefon))
                {
                    funde.Add($"{wo}: Stick bei ({nahe.X:F0}/{nahe.Y:F0}) liegt auf dem Handy");
                }
                var ohne = mit with { SperreTelefon = default };
                if (!ohne.Gesperrt(m.X, m.Y) && Sticks.SeiteFuer(m.X, m.Y, ohne) is not null)
                {
                    gegen++;
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        Assert.True(gegen >= 6, "Gegenprobe greift nicht: " + gegen);
    }

    /// <summary>
    /// Weisung P12-363 (Nico 1a, Sammelliste 448): „ist über dem Knopf kein Platz, steht der Name rechts neben dem Auge“.
    /// Abnahmesatz: „Am Handy quer (832×384) und in allen anderen Groessen ueberlappen sich Handlungsknopf, Name, Auge,
    /// Handy-Symbol, Kopfzeile und X nicht; der Name steht ueber dem Knopf, wenn dort Platz ist, sonst rechts neben dem Auge.“
    /// Kopfzeile wie im Spiel am Handy quer 64 Punkte hoch (Bild P12-229), X 40×40 oben rechts in der Kopfzeile. Name bis 180 × 28
    /// Punkte (laengster Name mit Rand). Gegenprobe: der alte Platz (immer ueber dem Knopf) liegt am Handy quer auf der Kopfzeile.
    /// </summary>
    [Fact]
    public void Name_UeberDemKnopfOderNebenDemAuge_UeberlapptNichts_HandyQuer()
    {
        const double nameB = 180;
        const double nameH = 28;
        var funde = new List<string>();
        var gegen = new List<string>();
        static bool Ueber((double X1, double Y1, double X2, double Y2) a, (double X1, double Y1, double X2, double Y2) c) =>
            a.X1 < c.X2 && c.X1 < a.X2 && a.Y1 < c.Y2 && c.Y1 < a.Y2;
        static (double X1, double Y1, double X2, double Y2) Rund((double X, double Y) m, double d) => (m.X - (d / 2), m.Y - (d / 2), m.X + (d / 2), m.Y + (d / 2));
        // dazu der Fall aus dem Bild P12-229 (Handy quer am Ausgang: Kopfzeile 64, unten die Karte mit dem Ortstext)
        var faelle = Groessen.SelectMany(x => new[] { 180.0, 420.0, 0.0 }.Select(leiste => (x.Name, x.W, x.H, Unten: 86.0, Leiste: leiste)))
            .Append(("S24 quer Bild", 832.0, 384.0, 121.0, 720.0));
        foreach (var (name, w, h, unten, leiste) in faelle)
        {
            {
                var b = leiste > 0 ? new Sticks.Bereich(w, h, 64, unten, (w - leiste) / 2, (w + leiste) / 2) : new Sticks.Bereich(w, h, 64, unten);
                var wo = $"{name} Leiste {leiste}";
                if (HandlungKnopf.Lage(b) is not { } l || HandlungKnopf.Name(b, nameB, nameH) is not { } nl || HandlungKnopf.NameUeber(b, nameH) is not { } alt)
                {
                    funde.Add(wo + ": keine Lage");
                    continue;
                }
                var (g, k) = HandlungKnopf.Groesse(w, h);
                var teile = new List<(string Was, (double X1, double Y1, double X2, double Y2) R)>
                {
                    ("Handlungsknopf", Rund(l.Handlung, g)),
                    ("Auge", Rund(l.Ich, k)),
                    ("Kopfzeile", (0, 0, w, b.Oben)),
                    ("X", (w - 48, 8, w - 8, 48)),
                };
                if (HandlungKnopf.Telefon(b) is { } t)
                {
                    teile.Add(("Handy-Symbol", Rund(t, k)));
                }
                var n = (nl.Ecke.X, nl.Ecke.Y, nl.Ecke.X + nameB, nl.Ecke.Y + nameH);
                foreach (var (was, r) in teile)
                {
                    if (Ueber(n, r))
                    {
                        funde.Add($"{wo}: Name ({n.Item1:F0}/{n.Item2:F0}) liegt auf {was}");
                    }
                }
                for (var i = 0; i < teile.Count; i++)
                {
                    for (var j = i + 1; j < teile.Count; j++)
                    {
                        if (!(teile[i].Was is "Kopfzeile" && teile[j].Was is "X") && Ueber(teile[i].R, teile[j].R))
                        {
                            funde.Add($"{wo}: {teile[i].Was} liegt auf {teile[j].Was}");
                        }
                    }
                }
                if (n.Item1 < 0 || n.Item3 > w || n.Item4 > h)
                {
                    funde.Add($"{wo}: Name ausserhalb ({n.Item1:F0}/{n.Item2:F0})");
                }
                // Nicos Wortlaut: ueber dem Knopf, wo Platz ist; sonst rechts neben dem Auge
                if (alt.Y >= b.Oben + 4 && nl.Ecke != alt && !teile.Any(x => x.Was == "Handy-Symbol" && Ueber((alt.X, alt.Y, alt.X + nameB, alt.Y + nameH), x.R)))
                {
                    funde.Add($"{wo}: Name trotz Platz nicht ueber dem Knopf");
                }
                var neben = (l.Ich.X + (k / 2) + HandlungKnopf.NameAbstand + 2, l.Ich.Y - (nameH / 2));
                var nebenFrei = !teile.Any(x => x.Was == "Handy-Symbol" && Ueber((neben.Item1, neben.Item2, neben.Item1 + nameB, neben.Item2 + nameH), x.R));
                if (alt.Y < b.Oben + 4 && nebenFrei && (nl.Ueber || n.Item1 < l.Ich.X + (k / 2) || Math.Abs(n.Item2 + (nameH / 2) - l.Ich.Y) > 1e-6))
                {
                    funde.Add($"{wo}: Name nicht rechts neben dem Auge ({n.Item1:F0}/{n.Item2:F0})");
                }
                if (alt.Y < b.Oben)
                {
                    gegen.Add(wo);
                }
            }
        }
        Assert.True(funde.Count == 0, string.Join("\n", funde));
        // Gegenprobe: am Handy quer lag der alte Platz auf der Kopfzeile (Bild P12-229)
        Assert.True(gegen.Contains("S24 quer Bild Leiste 720") && gegen.Contains("S24 quer Leiste 0"), "Gegenprobe greift nicht: " + string.Join(", ", gegen));
        // ... und hoch, wo Platz ist, bleibt er ueber dem Knopf
        Assert.True(HandlungKnopf.Name(new Sticks.Bereich(384, 832, 64, 86), nameB, nameH) is { Ueber: true });
    }
}
