using System.Diagnostics;
using Avalonia.Controls;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle S 0.55.0 (Weisung P12-282 Punkt 3): die grosse Testkarte (256 × 256) im Werkzeug.
/// <list type="bullet">
/// <item>Pruefung der Testkarte: Messwert und Ziel <see cref="PruefungTestkarteZielMs"/> (vorher rund 50 s).</item>
/// <item>Grenzen live mit Nachladen: jeder Stadtteil gerechnet, kein „Stadt zu teuer“ (jede Kachel und das gleichzeitig
/// Geladene unter den Grenzen); Gegenprobe: viele Stuecke in einem Stadtteil = roter Fund mit seinem Namen, Export gesperrt.</item>
/// <item>„Stadt bauen Testkarte (geladen)“ wie im Spiel: Bauen, Grundlage und die noetigen Stadtteile, einmalig, Grenze
/// <see cref="StadtLast.BauenGrenzeMs"/>.</item>
/// <item>3D-Ansicht laedt nach (nie ein Loch) und zeichnet genau, was die Zaehlung ohne Grafikkarte rechnet; alle Groessen
/// (PC und Tablet), hell und dunkel.</item>
/// </list>
/// </summary>
public static partial class StadtbauMessung
{
    /// <summary>Pfad der Testkarte im Repo (Start-Test laeuft in der Wurzel des Repos), sonst aus LERNPLATTFORM_TESTKARTE.</summary>
    private const string Testkarte = "tests/Lernplattform.Tests/Testdaten/testkarte_256.fistadt";

    /// <summary>Ziel fuer die Pruefung der Testkarte (Weisung P12-282: deutlich unter den bisherigen rund 45 bis 50 s).</summary>
    public const double PruefungTestkarteZielMs = 25000;

    private static async Task TestkarteAsync(Hauptansicht a, Window? f)
    {
        var pfad = Environment.GetEnvironmentVariable("LERNPLATTFORM_TESTKARTE") is { Length: > 0 } u && File.Exists(u) ? u : Testkarte;
        if (!File.Exists(pfad) || a.Daten is not { } daten)
        {
            Fund("Testkarte: Datei fehlt (" + Path.GetFullPath(pfad) + ")");
            return;
        }
        var uhr = Stopwatch.StartNew();
        var p = Projekt.AusStadtDatei(File.ReadAllBytes(pfad), daten, a.Ablage, "Starttest Testkarte");
        a.ProjektSetzen(p, null, innen: false);
        a.ReiterZeigen(innen: false);
        await a.AussenGeprueft;
        var pruefung = uhr.Elapsed.TotalMilliseconds;
        await a.GrenzenLiveFertig;
        var grenzen = uhr.Elapsed.TotalMilliseconds;
        Messwerte.Notiere(string.Create(Inv, $"Werkzeug: Pruefung Testkarte (oeffnen bis geprueft): {pruefung:F0} ms (Ziel {PruefungTestkarteZielMs:F0} ms, vor S 0.55.0 rund 45 000 bis 50 000 ms); Grenzen live fertig nach {grenzen:F0} ms"));
        if (pruefung > PruefungTestkarteZielMs)
        {
            Fund(string.Create(Inv, $"Werkzeug: Pruefung Testkarte {pruefung:F0} ms ueber dem Ziel {PruefungTestkarteZielMs:F0} ms"));
        }
        var g = a.GrenzenLiveStand;
        Messwerte.Notiere("Werkzeug: Grenzen live Testkarte: " + g);
        var kacheln = Stadtteile.Alle(256, 256).Count();
        if (g?.Kacheln is not { } kk || kk.Count != kacheln || kk.Any(k => k.Ueber) || g.Ueber
            || a.AussenFunde.Any(x => x.Text.StartsWith("Stadt zu teuer", StringComparison.Ordinal)))
        {
            Fund("Testkarte: Grenzen live nicht je Stadtteil gerechnet oder \"Stadt zu teuer\" (" + g + ")");
        }
        else
        {
            var teuerst = kk.MaxBy(k => StadtbauGrenzen.Anteil(k.Dreiecke, k.Aufrufe))!;
            Messwerte.Notiere(string.Create(Inv, $"Werkzeug: Testkarte teuerster Stadtteil {teuerst.Kachel.Name}: {teuerst.Dreiecke} Dreiecke, {teuerst.Aufrufe} Aufrufe (Grenzen {StadtbauGrenzen.Dreiecke}, {StadtbauGrenzen.Aufrufe}); gleichzeitig geladen hoechstens {g.GeladenBytes / 1048576.0:F1} MB bei {g.GeladenBei?.Name} (Grenze {StadtLast.NetzdatenGrenzeMb:F0} MB)"));
        }

        // Stadt bauen Testkarte (geladen) wie im Spiel: Bauen, Grundlage, noetige Stadtteile in der Mitte
        var spiel = await Task.Run(() => Ansicht3D.Bauen(p.Stand(), daten, Ansicht3D.Einstellung.Grenzen));
        var (bauen, grundlage, start, anzahl, mb) = await Task.Run(() =>
        {
            var u2 = Stopwatch.StartNew();
            var szene = StadtSzene.Bauen(spiel.Stadt, spiel.Ablauf.Stand, spiel.Grafik, Festzeit.Weihnachten, spiel.Feste, Jahreszeit.Winter);
            var b = u2.Elapsed.TotalMilliseconds;
            u2.Restart();
            var lader = new Nachlader(szene, mitGruen: true, hintergrund: false);
            var gr = u2.Elapsed.TotalMilliseconds;
            u2.Restart();
            var noetig = Ladeplan.Noetig(szene.Breite / 2, szene.Tiefe / 2, szene.Breite, szene.Tiefe);
            lader.StartBauen(noetig);
            foreach (var k in noetig)
            {
                lader.Hochgeladen(lader.JetztBauen(k) ?? throw new InvalidOperationException(Nachlader.FehlerText(k, null)));
            }
            // Engine-Welle A 1.4.0: auch was nebenher entsteht (Laufraster) gehoert zur Messung
            szene.FertigWarten();
            return (b, gr, u2.Elapsed.TotalMilliseconds, noetig.Count, lader.Bytes / 1048576.0);
        });
        var gesamt = bauen + grundlage + start;
        Messwerte.Notiere(string.Create(Inv, $"Werkzeug: Stadt bauen Testkarte (geladen, Nacht, Winter, Weihnachten): Bauen {bauen:F0} ms, Grundlage {grundlage:F0} ms, {anzahl} Stadtteile am Start {start:F0} ms, zusammen {gesamt:F0} ms (Grenze {StadtLast.BauenGrenzeMs:F0} ms); gleichzeitig geladen {mb:F1} MB"));
        Messwerte.Reihe("Werkzeug: Stadt bauen Testkarte (geladen, einmalig)", gesamt);
        if (gesamt > StadtLast.BauenGrenzeMs)
        {
            Fund(string.Create(Inv, $"Werkzeug: Stadt bauen Testkarte (geladen, einmalig): {gesamt:F0} ms ueber Grenze {StadtLast.BauenGrenzeMs:F0} ms; Einzelmessungen {gesamt:F0} ms (einmaliger Messpunkt, nicht wiederholt)"));
        }

        // 3D-Ansicht: laedt nach, nie ein Loch, Zaehlung = gezeichnetes Bild
        a.DreiZeigen();
        await a.DreiFertig;
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        for (var i = 0; i < 600 && a.Drei.GlBereit && !a.Drei.NachladenAktiv; i++)
        {
            await a.Drei.NaechstesBildAsync();
        }
        if (a.Drei.GlBereit)
        {
            if (!a.Drei.NachladenAktiv)
            {
                Fund("Testkarte: die 3D-Ansicht laedt nicht nach (ganze Karte geladen)");
            }
            if (await a.ZaehlungVergleichenAsync() is { } v)
            {
                Messwerte.Notiere(string.Create(Inv, $"Werkzeug: Testkarte Zaehlung mit Nachladen gegen das Bild der 3D-Ansicht: {v.Bilder} Bilder, {v.Abweichungen.Count} Abweichungen, Loecher {a.Drei.KachelnLoch}, geladen {a.Drei.Lader?.Geladen.Count} Stadtteile"));
                foreach (var z in v.Abweichungen.Take(10))
                {
                    Fund("Testkarte: Zaehlung weicht vom gezeichneten Bild ab: " + z);
                }
                if (v.Bilder == 0)
                {
                    Fund("Testkarte: kein Bild verglichen");
                }
            }
            if (a.Drei.KachelnLoch != 0)
            {
                Fund(string.Create(Inv, $"Testkarte: {a.Drei.KachelnLoch} Stadtteile im Bild weder nah noch fern gezeichnet (Loch)"));
            }
        }
        else
        {
            Messwerte.Notiere("Werkzeug: Testkarte 3D nicht geprueft (kein OpenGL auf diesem Rechner)");
        }
        await AlleGroessenAsync(a, f, "Testkarte 3D");

        // Gegenprobe: viele Stuecke dicht in einem Stadtteil = roter Fund mit seinem Namen, Export gesperrt
        a.ReiterZeigen(innen: false);
        var gross = Katalog.Aktiv!.Eintraege.Where(e => e.Art == KatalogArt.Modell).MaxBy(e => e.Dreiecke)!;
        var zahl = (int)Math.Min(400, (StadtbauGrenzen.Dreiecke / Math.Max(1, gross.Dreiecke)) + 20);
        var ziel = Stadtteile.Von(40, 40, 256, 256);
        p.AussenAendern("Viele Stuecke", aa =>
        {
            for (var i = 0; i < zahl; i++)
            {
                AussenBearbeitung.Setzen(aa, AussenArt.Katalog, 38 + (i % 20 * 0.1), 38 + (i / 20 * 0.1), raster: false, modell: gross.Id);
            }
            return 0;
        });
        await a.AussenGeprueft;
        await a.GrenzenLiveFertig;
        var teuer = a.AussenFunde.FirstOrDefault(x => x.Rot && x.Text.StartsWith("Stadt zu teuer", StringComparison.Ordinal));
        Messwerte.Notiere(string.Create(Inv, $"Werkzeug: Testkarte Gegenprobe {zahl} x {gross.Id} in {ziel.Name}: {a.GrenzenLiveStand}; Fund \"{teuer?.Text}\""));
        if (teuer is null || !teuer.Text.Contains("Stadtteil " + ziel.Name, StringComparison.Ordinal) || teuer.X is null
            || p.RoteFunde().All(r => !r.Fund.Text.StartsWith("Stadt zu teuer", StringComparison.Ordinal)))
        {
            Fund("Testkarte Gegenprobe: viele Stuecke in " + ziel.Name + " ergeben keinen roten Fund mit dem Namen des Stadtteils oder sperren den Export nicht");
        }
        else
        {
            await AlleGroessenAsync(a, f, "Testkarte zu teuer");
        }
    }
}
