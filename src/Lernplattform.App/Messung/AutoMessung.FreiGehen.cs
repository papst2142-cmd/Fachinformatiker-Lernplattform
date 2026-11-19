using System.Globalization;
using System.Numerics;
using Avalonia.Input;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Texte;
using Lernplattform.Programm;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.95.0 „frei gehen“ (Sammelliste 385, Weisung P12-306, PC-Teil) ueber echte Tastenereignisse an der Spielseite
/// (Druecken und Loslassen, Standard W, A, S, D) in Echtzeit: gehen und stehen bleiben (ein Ereignis „gelaufen“ je Halt, Uhr springt),
/// Fokus verloren (alle Tasten los), gegen eine Wand (Ansage einmal, nie auf einer Sperre, Gleiten schraeg),
/// ueber einen Zebrastreifen, vor einer Tuer stehen bleiben (Weg zum Ort, „Vor dir: …“, Hineingehen), drinnen gehen ohne Zeit,
/// in der Ich-Sicht (Koerper im Blick), Schalter „Antippen zum Gehen“ (aus: Tippen in die 3D-Ansicht geht nicht, an: geht).
/// Gegenproben: ohne Sperre (Schalter nur fuer Tests) steht die Figur auf einer Sperre; ohne Schalter geht Tippen.
/// Bildzeiten beim Gehen nur als Info (die Grenzen und Messpunkte bleiben, wie sie sind).
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> FreiGehenAsync(MainView ansicht)
    {
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: frei gehen " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel frei gehen " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel frei gehen: Spielseite nicht gebaut");
            return 1;
        }
        var vorher = seite.Spiel;
        if (!await GlAbwartenAsync(seite))
        {
            Pruefe("ohne 3D keine Gehtasten", !seite.Flaeche.GehTaste(Key.W, true) && seite.Flaeche.GehTasten.Count == 0,
                seite.Flaeche.GehTasten.Count + " Tasten gemerkt");
            return funde;
        }
        try
        {
            await FreiBedienungAsync(ansicht, seite, Pruefe);
        }
        finally
        {
            StadtSpiel.FreiGehenAltFuerTests = false;
            StadtFlaeche.Mitschnitt = null;
            Gehbedienung.AntippenAn = Gehbedienung.Lesen(Zustand.Werte);
            seite.Flaeche.GehTastenLos();
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    private static string TafelKurz(string text) => text.Length <= 80 ? text : text[..80] + " …";

    private static async Task FreiBedienungAsync(MainView ansicht, SpielSeite seite, Action<string, bool, string> pruefe)
    {
        var inv = CultureInfo.InvariantCulture;
        var spiel = Frisch(seite);
        var fl = seite.Flaeche;
        await fl.NaechstesBildAsync();
        var k = fl.AktuelleKamera!;
        var sz = spiel.Szene;
        void Druecken(Key taste) => seite.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = taste, Source = fl });
        void Los(Key taste) => seite.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = taste, Source = fl });
        bool Gesperrt(double x, double z) => !sz.Laufraster.KannStehen(x, z) || sz.Laufraster.FeinGesperrt(x, z);
        void Richte(double dx, double dz) => k.Drehung = (float)(Math.Atan2(-dx, -dz) * 180 / Math.PI);
        // Taste halten, bis die Uhr so viele Sekunden zeigt; je Bild pruefen, dass die Figur nie auf einer Sperre steht
        async Task<(int Bilder, bool Sperre)> Halten(double sekunden, params Key[] tasten)
        {
            foreach (var t in tasten)
            {
                Druecken(t);
            }
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            var bilder = 0;
            var sperre = false;
            while (uhr.Elapsed.TotalSeconds < sekunden)
            {
                await fl.NaechstesBildAsync();
                bilder++;
                sperre |= !spiel.ImInnenraum && Gesperrt(spiel.FigurX, spiel.FigurZ);
            }
            foreach (var t in tasten)
            {
                Los(t);
            }
            return (bilder, sperre);
        }
        async Task<bool> BisHalt()
        {
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            while (spiel.FreiOffen && uhr.Elapsed.TotalSeconds < 3)
            {
                await fl.NaechstesBildAsync();
            }
            await fl.NaechstesBildAsync();
            return !spiel.FreiOffen;
        }

        // 1 W/A/S/D halten: die Figur geht (Bildzeiten als Info), kein Ereignis waehrend des Gehens; loslassen: genau eins
        var zeilen = spiel.Ablauf.Stand.Verlauf.Count;
        var uhr0 = spiel.Jetzt.Uhr;
        var start = new Vector2(spiel.FigurX, spiel.FigurZ);
        var mitschnitt = new List<BildZeiten.Bild>();
        StadtFlaeche.Mitschnitt = mitschnitt;
        var taste = Key.None;
        var bilder1 = 0;
        var sperre1 = false;
        var gemerkt = 0;
        foreach (var t in new[] { Key.W, Key.D, Key.S, Key.A })
        {
            Druecken(t);
            gemerkt = fl.GehTasten.Count;
            Los(t);
            var (b, s) = await Halten(0.8, t);
            bilder1 += b;
            sperre1 |= s;
            if (Vector2.Distance(start, new Vector2(spiel.FigurX, spiel.FigurZ)) > 0.5f)
            {
                taste = t;
                break;
            }
        }
        StadtFlaeche.Mitschnitt = null;
        var waehrend = spiel.Ablauf.Stand.Verlauf.Count;
        var halt = await BisHalt();
        var dauer = mitschnitt.Select(BildZeiten.Dauer).DefaultIfEmpty(0).ToList();
        Messwerte.Notiere(string.Create(inv,
            $"Spiel: frei gehen Bildzeiten (Info, keine Grenze): {mitschnitt.Count} Bilder, Mittel {dauer.Average():F1} ms, hoechstens {dauer.Max():F1} ms, Logik je Bild hoechstens {mitschnitt.Select(b => b.LogikMs).DefaultIfEmpty(0).Max():F2} ms, Auftraege zusammen {mitschnitt.Sum(b => b.AuftraegeMs):F1} ms"));
        var e1 = spiel.Ablauf.Stand.Verlauf[^1];
        pruefe("Taste halten und loslassen", taste != Key.None && gemerkt == 1 && waehrend == zeilen && halt && !sperre1
            && spiel.Ablauf.Stand.Verlauf.Count == zeilen + 1 && (e1.Art == Stadt.Gelaufen || e1.Art == Stadt.Gegangen) && spiel.Jetzt.Uhr > uhr0,
            string.Create(inv, $"Taste {taste} ({gemerkt} gemerkt), {bilder1} Bilder, {Vector2.Distance(start, new Vector2(spiel.FigurX, spiel.FigurZ)):F2} weit, waehrend {waehrend - zeilen} Ereignisse, danach {spiel.Ablauf.Stand.Verlauf.Count - zeilen} ({e1.Art}), Uhr {uhr0} -> {spiel.Jetzt.Uhr}, Sperre {sperre1}"));

        // 2 Fokus verloren (Alt+Tab): alle Tasten gelten als losgelassen, der Halt folgt
        Druecken(Key.W);
        Druecken(Key.D);
        await fl.NaechstesBildAsync();
        var vorFokus = fl.GehTasten.Count;
        seite.FokusVerlorenFuerMessung();
        var nachFokus = fl.GehTasten.Count;
        var haltFokus = await BisHalt();
        Los(Key.W);
        Los(Key.D);
        pruefe("Fokus verloren", vorFokus == 2 && nachFokus == 0 && haltFokus, $"{vorFokus} Tasten vorher, {nachFokus} danach, Halt {haltFokus}");

        // 3 gegen eine Wand: nie auf einer Sperre, „Hier geht es nicht weiter.“ einmal, schraeg gleitet die Figur weiter
        var wand = WandStelle(sz);
        if (wand is { } w && spiel.GeheZu(w.X, w.Z))
        {
            spiel.SofortAnkommen();
            Richte(w.Dx, w.Dz);
            await fl.NaechstesBildAsync();
            var ansagen = new List<string>();
            void Merken(string text) => ansagen.Add(text);
            spiel.GehAnsage += Merken;
            var (_, sperreW) = await Halten(0.8, Key.W);
            var gesagt = ansagen.Count(a => a == SpielProbeTexte.NichtWeiter);
            var vorGleiten = new Vector2(spiel.FigurX, spiel.FigurZ);
            var (_, sperreG) = await Halten(0.5, Key.W, Key.D);
            var geglitten = Vector2.Distance(vorGleiten, new Vector2(spiel.FigurX, spiel.FigurZ));
            await BisHalt();
            spiel.GehAnsage -= Merken;
            pruefe("gegen eine Wand", !sperreW && !sperreG && gesagt == 1 && geglitten > 0.3f,
                string.Create(inv, $"Wand bei ({w.X:F2}, {w.Z:F2}), Ansage \"{SpielProbeTexte.NichtWeiter}\" {gesagt} Mal, schraeg {geglitten:F2} geglitten, Sperre {sperreW || sperreG}"));
            // Gegenprobe: ohne Sperre geht die Figur hinein
            if (spiel.GeheZu(w.X, w.Z))
            {
                spiel.SofortAnkommen();
            }
            // (der Schalter gilt nur im eigenen Ablauf, darum geht die Gegenprobe die Bilder selbst, wie die Flaeche je Bild)
            StadtSpiel.FreiGehenAltFuerTests = true;
            var sperreAlt = false;
            var (gx, gz) = Steuerung.Richtung(1, 0, k.Drehung);
            for (var i = 0; i < 24; i++)
            {
                spiel.FreiGehen(gx, gz, 1.0 / 30);
                sperreAlt |= Gesperrt(spiel.FigurX, spiel.FigurZ);
            }
            spiel.FreiGehen(0, 0, 1);
            StadtSpiel.FreiGehenAltFuerTests = false;
            await BisHalt();
            pruefe("Gegenprobe ohne Sperre", sperreAlt, $"auf einer Sperre {sperreAlt}");
            if (sperreAlt)
            {
                Frisch(seite);
                spiel = seite.Spiel!;
                sz = spiel.Szene;
                await fl.NaechstesBildAsync();
                k = fl.AktuelleKamera!;
            }
        }
        else
        {
            pruefe("gegen eine Wand", false, "keine Stelle vor einer Wand gefunden");
        }

        // 4 ueber einen Zebrastreifen: die Figur kommt auf die andere Seite, nie auf der Fahrbahn neben dem Streifen
        var zebra = sz.Zebras.Select(z => (z, Seite: new[] { 1.2f, 1.0f, 1.4f }.Select(a => z.Mitte - (z.Quer * a))
                .FirstOrDefault(p => !Gesperrt(p.X, p.Y) && !Gesperrt(z.Mitte.X + (z.Quer.X * 1.2), z.Mitte.Y + (z.Quer.Y * 1.2)))))
            .FirstOrDefault(t => t.Seite != default);
        if (zebra.z is { } zb && spiel.GeheZu(zebra.Seite.X, zebra.Seite.Y))
        {
            spiel.SofortAnkommen();
            Richte(zb.Quer.X, zb.Quer.Y);
            await fl.NaechstesBildAsync();
            var (_, sperreZ) = await Halten(0.9, Key.W);
            await BisHalt();
            var drueben = Vector2.Dot(new Vector2(spiel.FigurX, spiel.FigurZ) - zb.Mitte, zb.Quer);
            pruefe("ueber einen Zebrastreifen", !sperreZ && drueben > 0.6f,
                string.Create(inv, $"Zebrastreifen {zb.Nr}, danach {drueben:F2} jenseits der Mitte, Sperre {sperreZ}"));
        }
        else
        {
            pruefe("ueber einen Zebrastreifen", false, "kein Zebrastreifen mit freien Seiten");
        }

        // 5 vor einer Tuer stehen bleiben: ein Ereignis „gegangen“ zu diesem Ort, Ansage „Vor dir: …“, Hineingehen geht
        var tuer = TuerStelle(spiel);
        if (tuer is { } t0 && spiel.GeheZu(t0.X, t0.Z))
        {
            spiel.SofortAnkommen();
            Richte(t0.Ort.StehX - t0.X, t0.Ort.StehZ - t0.Z);
            await fl.NaechstesBildAsync();
            var zeilenT = spiel.Ablauf.Stand.Verlauf.Count;
            var ansagen = new List<string>();
            void Merken(string text) => ansagen.Add(text);
            spiel.GehAnsage += Merken;
            // 0,6 weit in Echtzeit: hoechstens 0,8, danach steht die Figur nahe der Tuer (Sperre davor haelt sie auf)
            Druecken(Key.W);
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            while (Math.Sqrt(Math.Pow(spiel.FigurX - t0.X, 2) + Math.Pow(spiel.FigurZ - t0.Z, 2)) < 0.6 && uhr.Elapsed.TotalSeconds < 1)
            {
                await fl.NaechstesBildAsync();
            }
            Los(Key.W);
            await BisHalt();
            spiel.GehAnsage -= Merken;
            var eT = spiel.Ablauf.Stand.Verlauf[^1];
            var vorDir = string.Format(inv, SpielProbeTexte.VorDir, t0.Ort.Name);
            await ansicht.BildFertigAsync();
            var hinein = spiel.Betreten();
            await fl.NaechstesBildAsync();
            pruefe("vor einer Tuer stehen bleiben", spiel.Ablauf.Stand.Verlauf.Count >= zeilenT + 1 && eT.Art == Stadt.Gegangen
                && ansagen.Contains(vorDir) && hinein && spiel.ImInnenraum,
                $"{t0.Ort.Id}: Ereignis {eT.Art}, Ansagen \"{string.Join("\", \"", ansagen)}\", Hineingehen {hinein}");

            // 6 drinnen: gehen kostet keine Zeit und schreibt nichts
            if (spiel.ImInnenraum)
            {
                var zeilenI = spiel.Ablauf.Stand.Verlauf.Count;
                var uhrI = spiel.Jetzt.Uhr;
                var startI = new Vector2(spiel.FigurX, spiel.FigurZ);
                var weit = 0f;
                foreach (var ti in new[] { Key.W, Key.A, Key.S, Key.D })
                {
                    await Halten(0.3, ti);
                    weit = MathF.Max(weit, Vector2.Distance(startI, new Vector2(spiel.FigurX, spiel.FigurZ)));
                }
                await BisHalt();
                var wege = Core.Spiel.Wege.KannStehen(spiel.FigurX, spiel.FigurZ, spiel.Innen!.Lauf);
                pruefe("drinnen gehen", weit > 0.2f && spiel.Ablauf.Stand.Verlauf.Count == zeilenI && spiel.Jetzt.Uhr == uhrI && wege,
                    string.Create(inv, $"{weit:F2} weit, {spiel.Ablauf.Stand.Verlauf.Count - zeilenI} Ereignisse, Uhr {uhrI} -> {spiel.Jetzt.Uhr}, frei {wege}"));
                spiel.Verlassen();
                await fl.NaechstesBildAsync();
                k = fl.AktuelleKamera!;
            }
        }
        else
        {
            pruefe("vor einer Tuer stehen bleiben", false, "keine freie Stelle vor einer Tuer gefunden");
        }

        // 7 Ich-Sicht: rueckwaerts gehen, der Koerper bleibt im Blick (Drehung der Figur = Blick der Kamera)
        if (SpielKamera.IchUmschalten(k))
        {
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            while (k.IchAnteil < 1f && uhr.Elapsed.TotalSeconds < 3)
            {
                await fl.NaechstesBildAsync();
            }
            var abweichung = 0f;
            var startIch = new Vector2(spiel.FigurX, spiel.FigurZ);
            Druecken(Key.S);
            for (var i = 0; i < 20; i++)
            {
                await fl.NaechstesBildAsync();
                abweichung = MathF.Max(abweichung, MathF.Abs(((spiel.FigurRichtung - IchSicht.DrehungZu(k.Drehung) + 540f) % 360f) - 180f));
            }
            Los(Key.S);
            var weg = Vector2.Distance(startIch, new Vector2(spiel.FigurX, spiel.FigurZ));
            await BisHalt();
            pruefe("Ich-Sicht rueckwaerts", k.Ich && (weg < 0.05f || abweichung < 0.5f),
                string.Create(inv, $"{weg:F2} weit, Figur hoechstens {abweichung:F2} Grad neben dem Blick, Ich {k.IchAnteil:F2}"));
            SpielKamera.IchUmschalten(k);
            for (var i = 0; i < 60 && k.IchAnteil > 0f; i++)
            {
                await fl.NaechstesBildAsync();
            }
        }
        else
        {
            pruefe("Ich-Sicht rueckwaerts", false, "Ich-Sicht nicht erlaubt");
        }

        // 8 Schalter „Antippen zum Gehen“: aus = Tippen in die 3D-Ansicht geht nicht, an = geht (Gegenprobe)
        spiel.SofortAnkommen();
        var ziel = sz.Orte.OrderByDescending(o => MathF.Abs(o.StehX - spiel.FigurX) + MathF.Abs(o.StehZ - spiel.FigurZ)).First();
        k.Setzen(ziel.StehX, ziel.StehZ);
        await fl.NaechstesBildAsync();
        var boden = new Vector3(ziel.StehX, StadtSzene.Hoehe.Gehweg, ziel.StehZ);
        var punkt = k.Bildpunkt(boden, (float)fl.Bounds.Width, (float)fl.Bounds.Height);
        var zeilenS = spiel.Ablauf.Stand.Verlauf.Count;
        Gehbedienung.AntippenAn = false;
        if (punkt is { } p)
        {
            fl.Tippen(new Avalonia.Point(p.X, p.Y));
        }
        var ausGeht = spiel.Geht || spiel.Ablauf.Stand.Verlauf.Count != zeilenS;
        var listeGeht = spiel.GeheZu(ziel.Id) == StadtGrund.Ok;
        spiel.SofortAnkommen();
        Gehbedienung.AntippenAn = true;
        var zeilenAn = spiel.Ablauf.Stand.Verlauf.Count;
        k.Setzen(sz.Orte[0].StehX, sz.Orte[0].StehZ);
        await fl.NaechstesBildAsync();
        var punkt2 = k.Bildpunkt(new Vector3(sz.Orte[0].StehX, StadtSzene.Hoehe.Gehweg, sz.Orte[0].StehZ), (float)fl.Bounds.Width, (float)fl.Bounds.Height);
        if (punkt2 is { } p2)
        {
            fl.Tippen(new Avalonia.Point(p2.X, p2.Y));
        }
        var anGeht = spiel.Geht || spiel.Ablauf.Stand.Verlauf.Count != zeilenAn;
        spiel.SofortAnkommen();
        pruefe("Schalter Antippen zum Gehen", punkt is not null && punkt2 is not null && !ausGeht && listeGeht && anGeht,
            $"aus: Tippen geht {ausGeht}, Ortsliste geht {listeGeht}; an (Gegenprobe): Tippen geht {anGeht}");

        // 8b Weisung P12-307 Fund 2: mit Antippen aus verspricht die Tafel kein Antippen; an = wortgleich wie bisher (Gegenprobe)
        var tafelSpiel = Frisch(seite);
        var tafelRoh = tafelSpiel.Zeile;
        Gehbedienung.AntippenAn = false;
        // die Tafel schreibt der Takt der Seite (alle 0,5 s) neu: wie ein Mensch einen Takt abwarten
        await fl.NaechstesBildAsync();
        await Task.Delay(700);
        await ansicht.BildFertigAsync();
        var tafelAus = seite.TafelText;
        Gehbedienung.AntippenAn = true;
        await fl.NaechstesBildAsync();
        await Task.Delay(700);
        await ansicht.BildFertigAsync();
        var tafelAn = seite.TafelText;
        pruefe("Tafel bei Antippen aus", SpielProbeTexte.TippeHinzugehen.IsMatch(tafelRoh)
            && tafelAus.Contains(SpielProbeTexte.GehtastenStattTippen, StringComparison.Ordinal)
            && !SpielProbeTexte.TippeHinzugehen.IsMatch(tafelAus) && tafelAn == tafelRoh,
            $"aus: „{TafelKurz(tafelAus)}“; an (Gegenprobe): {(tafelAn == tafelRoh ? "wie bisher" : "anders: " + TafelKurz(tafelAn))}");

        // 9 der Schalter in den Optionen: Name fuer den Screenreader, Standard an, Hinweis darunter
        await ansicht.ZeigeAsync(Seiten.Optionen);
        if (ansicht.GebauteSeiten.GetValueOrDefault(Seiten.Optionen) is OptionenSeite optionen && optionen.Klappe(Optionen.Spiel) is { } klappe)
        {
            klappe.IsExpanded = true;
            await ansicht.BildFertigAsync();
            var schalter = optionen.AntippenSchalter;
            pruefe("Schalter in den Optionen", schalter is not null && schalter.IsChecked == true
                && Avalonia.Automation.AutomationProperties.GetName(schalter) == SpielProbeTexte.AntippenGehen && Gehbedienung.Lesen(Zustand.Werte),
                $"Schalter {schalter?.IsChecked}, Name \"{(schalter is null ? "" : Avalonia.Automation.AutomationProperties.GetName(schalter))}\"");
            klappe.IsExpanded = false;
        }
        else
        {
            pruefe("Schalter in den Optionen", false, "Karte Spiel fehlt");
        }
        await ansicht.ZeigeAsync(Seiten.Spiel);
    }

    /// <summary>Eine freie Stelle, vor der in Richtung (Dx, Dz) nach 0,3 eine Sperre kommt und nach rechts davon frei ist (Gleiten).</summary>
    private static (double X, double Z, double Dx, double Dz)? WandStelle(StadtSzene sz)
    {
        bool Frei(double x, double z) => sz.Laufraster.KannStehen(x, z) && !sz.Laufraster.FeinGesperrt(x, z);
        foreach (var (dx, dz) in new[] { (0.0, -1.0), (1.0, 0.0), (0.0, 1.0), (-1.0, 0.0) })
        {
            for (var x = 2.0; x < sz.Breite - 2; x += 0.5)
            {
                for (var z = 2.0; z < sz.Tiefe - 2; z += 0.5)
                {
                    if (Frei(x, z) && Frei(x + (dx * 0.2), z + (dz * 0.2)) && !Frei(x + (dx * 0.4), z + (dz * 0.4)) && !Frei(x + (dx * 0.6), z + (dz * 0.6))
                        && Enumerable.Range(0, 11).All(i => Frei(x + (dx * 0.2) - (dz * i * 0.1), z + (dz * 0.2) + (dx * i * 0.1))))
                    {
                        return (x, z, dx, dz);
                    }
                }
            }
        }
        return null;
    }

    /// <summary>Eine Stelle 0,8 vor dem Stehplatz eines Orts mit Innenraum (nicht der heutige), gerade frei bis dorthin.</summary>
    private static (double X, double Z, StadtOrt Ort)? TuerStelle(StadtSpiel spiel)
    {
        var sz = spiel.Szene;
        foreach (var ort in sz.Orte.Where(o => spiel.HatInnenraum(o.Id) && o.Id != spiel.Jetzt.Ort && spiel.Stadt.Offen(o.Id, spiel.Jetzt.Uhr) && spiel.Stadt.Offen(o.Id, spiel.Jetzt.Uhr + 60)))
        {
            if (sz.Orte.Any(o => o != ort && Math.Abs(o.StehX - ort.StehX) + Math.Abs(o.StehZ - ort.StehZ) < 1.2))
            {
                continue;
            }
            foreach (var g in Enumerable.Range(0, 24).Select(i => i * 15.0))
            {
                var (rx, rz) = (Math.Sin(g * Math.PI / 180), Math.Cos(g * Math.PI / 180));
                var (sx, sz0) = (ort.StehX - (rx * 0.8), ort.StehZ - (rz * 0.8));
                if (Enumerable.Range(0, 41).All(i => sz.Laufraster.KannStehen(sx + (rx * i * 0.02), sz0 + (rz * i * 0.02))
                    && !sz.Laufraster.FeinGesperrt(sx + (rx * i * 0.02), sz0 + (rz * i * 0.02))))
                {
                    return (sx, sz0, ort);
                }
            }
        }
        return null;
    }
}
