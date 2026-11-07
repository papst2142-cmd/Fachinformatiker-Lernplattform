using System.Diagnostics;
using System.Numerics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

using Point = Avalonia.Point;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.9.0 (Weisung P12-48): Gebaeude-Editor. Oeffnen bis zum ersten Bild (Messpunkt „Gebaeude entwerfen oeffnen“,
/// einmalig), Teil setzen ueber den Strahl bis zum Bild (Messpunkt „Teil setzen (bis zum Bild)“, wiederholbar, 400 ms),
/// Schatten gruen auf freiem Feld und rot auf belegtem, Probehaus mit 20 Teilen ohne Fund und unter 8 000 Dreiecken, ein
/// roter Fund, ein Finger setzt, zwei Finger zoomen (Gegenprobe mit der alten Auswertung), Speichern als Blueprint, Weiterbauen,
/// Setzen in Stadt 1 (Export gelb gesperrt, Innenraum aus dem Vorschlag ohne Fund) und Grenzen der Gesamtansicht mit dem
/// Probehaus (hoechstens 160 000 Dreiecke, 40 Aufrufe). Layout PC und Tablet, hell und dunkel.
/// </summary>
public static partial class StadtbauMessung
{
    private const string HausName = "Starttest Haus";

    private static Point? BildpunktVon(EntwurfAnsicht e, int x, int y, int ebene)
    {
        if (e.Drei.AktuelleKamera is not { } k)
        {
            return null;
        }
        var h = ebene * Bauteile.Stockwerk;
        var mitte = new Vector3(x + EntwurfAnsicht3D.Rand + 0.5f, h, y + EntwurfAnsicht3D.Rand + 0.5f);
        return k.Bildpunkt(mitte, (float)e.Bedienung.Bounds.Width, (float)e.Bedienung.Bounds.Height) is { } b ? new Point(b.X, b.Y) : null;
    }

    private static async Task EntwurfAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Gebaeude", null);
        await a.AussenGeprueft;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        await BildAsync(a);
        EntwurfAnsicht? e = null;
        var uhr = Stopwatch.StartNew();
        e = a.EntwurfOeffnen(null);
        if (e is null)
        {
            Fund("Gebaeude entwerfen: Editor oeffnet nicht");
            return;
        }
        e.MessungAb(Stopwatch.GetTimestamp() - (long)(uhr.Elapsed.TotalSeconds * Stopwatch.Frequency));
        await e.Fertig;
        await BildAsync(a);
        DreiGrenze("Gebaeude entwerfen oeffnen", [uhr.Elapsed.TotalMilliseconds], einmalig: true);
        var gl = e.Drei.GlBereit && e.Drei.AktuelleKamera is not null;
        if (!gl)
        {
            Messwerte.Notiere("Gebaeude entwerfen: kein OpenGL auf diesem Rechner, 3D nicht gemessen");
        }
        if (a.KopfKnopfFrei(Texte.Neu) || a.KopfKnopfFrei(Texte.DateiSpeichern))
        {
            Fund("Gebaeude entwerfen: Kopfleiste laesst die Knoepfe fuer das Projekt frei");
        }
        await AlleGroessenAsync(a, f, "Gebaeude entwerfen leer");

        // Teil setzen ueber den Strahl bis zum Bild (wiederholbar)
        const string tuer = Bauteile.Praefix + "building-door";
        if (gl)
        {
            e.TeilWaehlen(tuer);
            await MessenAsync("Teil setzen (bis zum Bild)", a, async () =>
            {
                var vorher = e.Entwurf.Teile.Count;
                if (BildpunktVon(e, 7, 7, 0) is not { } p || e.Bedienung.SetzenAn(p) < 0 || e.Entwurf.Teile.Count != vorher + 1)
                {
                    Fund("Teil setzen: kein Teil auf Feld 7,7 gesetzt");
                    return;
                }
                await e.Fertig;
            }, () =>
            {
                a.Rueckgaengig();
                return e.Fertig;
            });
            var t = e.Entwurf.Teile.LastOrDefault();
            if (t is null || (t.X, t.Y, t.Stockwerk) != (7, 7, 0))
            {
                Fund("Teil setzen: Strahl trifft das falsche Feld (" + (t is null ? "keins" : t.X + "," + t.Y) + ")");
            }
            a.Rueckgaengig();
            await e.Fertig;
        }

        // Probehaus: 20 Teile, kein Fund, unter der Grenze
        var haus = Entwurf.Probehaus();
        foreach (var t in haus.Teile)
        {
            e.Entwurf.Setzen(t.Id, t.X, t.Y, t.Stockwerk, t.Dreh);
        }
        e.Entwurf.EingangFestlegen(e.Entwurf.Teile.FindIndex(t => t.Name == "building-door-window"));
        await e.Fertig;
        if (e.Funde.Count > 0)
        {
            Fund("Probehaus: Funde " + string.Join(" | ", e.Funde.Select(x => x.Text)));
        }
        Messwerte.Notiere(string.Create(Inv, $"Probehaus: {e.Entwurf.Teile.Count} Teile, {e.Dreiecke} Dreiecke (Grenze {Bauteile.GrenzeDreiecke})"));
        if (e.Entwurf.Teile.Count < 20 || e.Dreiecke > Bauteile.GrenzeDreiecke)
        {
            Fund(string.Create(Inv, $"Probehaus: {e.Entwurf.Teile.Count} Teile / {e.Dreiecke} Dreiecke"));
        }
        if (gl)
        {
            e.GanzZeigen();
            await BildAsync(a);
            // Schatten: gruen auf dem freien Feld neben dem Haus, rot auf einem belegten Feld (zwei Teile im selben Feld)
            e.TeilWaehlen(Bauteile.Praefix + "building-block");
            if (BildpunktVon(e, 3, 0, 0) is { } frei)
            {
                e.Bedienung.SchattenAn(frei);
            }
            if (e.Bedienung.Passt != true)
            {
                Fund("Schatten: auf freiem Feld nicht gruen (" + e.Bedienung.SchattenText + ")");
            }
            if (BildpunktVon(e, 2, 1, 0) is { } belegt)
            {
                e.Bedienung.SchattenAn(belegt);
            }
            if (e.Bedienung.Passt != false || e.Bedienung.SchattenFeld != (2, 1))
            {
                Fund("Schatten: auf belegtem Feld nicht rot (Gegenprobe; " + e.Bedienung.SchattenText + ")");
            }
            await BildAsync(a);
            await AlleGroessenAsync(a, f, "Gebaeude entwerfen Schatten");
            e.TeilWaehlen(null);

            // ein Finger setzt (Tippen), zwei Finger zoomen ohne Aenderung (Gegenprobe mit der Auswertung vor S 0.5.1)
            e.TeilWaehlen(Bauteile.Praefix + "building-block");
            if (BildpunktVon(e, 9, 3, 0) is { } punkt)
            {
                var vorher = e.Entwurf.Teile.Count;
                var finger = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, true);
                Druck(e.Bedienung, finger, punkt);
                Los(e.Bedienung, finger, punkt);
                await e.Fertig;
                if (e.Entwurf.Teile.Count != vorher + 1)
                {
                    Fund("Gebaeude entwerfen Finger: Tippen setzt kein Teil");
                }
                else
                {
                    a.Rueckgaengig();
                }
            }
            e.TeilWaehlen(null);
            await FlaecheAsync(a, "Gebaeude entwerfen", e.Bedienung, () => 1 / e.Drei.AktuelleKamera!.Abstand,
                p => e.Bedienung.TeilAn(p) >= 0, () => Lernplattform.Core.Daten.PythonJson.Text(e.Entwurf.Daten()), alt => e.Bedienung.GesteAltFuerTests = alt);
            e.GanzZeigen();
            await BildAsync(a);

            // Auswahl antippen
            if (BildpunktVon(e, 1, 1, 0) is { } amHaus)
            {
                e.Bedienung.Tippen(amHaus);
            }
            if (e.Auswahl < 0)
            {
                Fund("Gebaeude entwerfen: Antippen des Hauses waehlt kein Teil");
            }
        }
        if (e.Auswahl < 0)
        {
            e.Waehlen(e.Entwurf.Teile.FindIndex(t => t.Name == "building-door-window"));
        }
        await AlleGroessenAsync(a, f, "Gebaeude entwerfen Auswahl");

        // roter Fund: Dach im Erdgeschoss ohne Wand
        e.Entwurf.Setzen(Bauteile.Praefix + "roof-flat-center", 3, 1, 0, 0);
        await e.Fertig;
        if (!e.Funde.Any(x => x.Rot && x.Text.EndsWith(Texte.EntwurfDachOhneWand, StringComparison.Ordinal)))
        {
            Fund("Gebaeude entwerfen: Dach ohne Wand ist nicht rot");
        }
        if (a.EntwurfSpeichern(HausName) is null)
        {
            Fund("Gebaeude entwerfen: mit rotem Fund gespeichert (Gegenprobe)");
        }
        await AlleGroessenAsync(a, f, "Gebaeude entwerfen rot");
        a.Rueckgaengig();
        await e.Fertig;

        // speichern, schliessen, weiterbauen
        if (a.EntwurfSpeichern(HausName) is { } grund)
        {
            Fund("Gebaeude entwerfen: Speichern ging nicht: " + grund);
            a.EntwurfSchliessen();
            return;
        }
        a.EntwurfSchliessen();
        var bp = a.EigeneBlueprints().FirstOrDefault(b => b.Name == HausName);
        if (bp is null)
        {
            Fund("Gebaeude entwerfen: gespeicherter Blueprint fehlt in der Liste");
            return;
        }
        var weiter = a.EntwurfOeffnen(bp);
        if (weiter is null || weiter.Entwurf.Teile.Count != haus.Teile.Count || weiter.Entwurf.Eingaenge.Count != 1)
        {
            Fund("Gebaeude entwerfen: Weiterbauen zeigt nicht dasselbe Gebaeude");
        }
        else
        {
            await weiter.Fertig;
        }
        a.EntwurfSchliessen();

        // in Stadt 1 setzen: gelber Hinweis, kein neuer roter Fund, Export gesperrt, Innenraum ohne roten Fund
        var p1 = a.Projekt!;
        AussenStelle? gesetzt = null;
        foreach (var (x, y) in new[] { (18.0, 6.0), (18.0, 4.0), (20.0, 6.0) })
        {
            p1.AussenAendern(Texte.BlueprintSetzen, aa => gesetzt = bp.Setzen(aa, x, y, out _));
            if (gesetzt is null)
            {
                continue;
            }
            await a.AussenGeprueft;
            if (!a.AussenFunde.Any(z => z.Rot && z.Text != Laterne20Fund))
            {
                break;
            }
            a.Rueckgaengig();
            await a.AussenGeprueft;
            gesetzt = null;
        }
        if (gesetzt is null)
        {
            Fund("Probehaus: kein freier Platz in Stadt 1 ohne roten Fund");
            return;
        }
        Messwerte.Notiere("Probehaus in Stadt 1 bei " + PythonJsonText(Lage(p1, gesetzt)));
        if (!a.AussenFunde.Any(z => !z.Rot && z.Text == Texte.EigenesGebaeudeNochNicht(HausName)))
        {
            Fund("Probehaus in Stadt 1: gelber Hinweis \"" + Texte.EigenesGebaeudeNochNicht(HausName) + "\" fehlt");
        }
        try
        {
            _ = p1.Exportieren(DateTimeOffset.Now);
            Fund("Probehaus in Stadt 1: Export nicht gesperrt");
        }
        catch (StadtbauFehler)
        {
        }
        var bau = Projekt.BauPraefix + bp.Modell;
        if (p1.Innenorte.Contains(bau))
        {
            a.OrtOeffnen(bau);
            await a.Geprueft;
            if (a.Funde.Any(z => z.Rot))
            {
                Fund("Probehaus innen (Vorschlag EG): rote Funde " + string.Join(" | ", a.Funde.Where(z => z.Rot).Select(z => z.Text)));
            }
            await BildAsync(a);
            await AlleGroessenAsync(a, f, "Probehaus innen");
        }
        else
        {
            Fund("Probehaus in Stadt 1: Innenraum fehlt");
        }
        a.ReiterZeigen(innen: false);
        a.DreiZeigen();
        await a.DreiFertig;
        if (a.Drei.GlBereit)
        {
            await a.GrenzenMessenAsync();
            if (a.GrenzenDraussen is { } gz)
            {
                Messwerte.Notiere(string.Create(Inv, $"Grenzen Gesamtansicht Stadt 1 mit Probehaus (Nacht, Winter, Weihnachten): {gz.Dreiecke} Dreiecke, {gz.Aufrufe} Aufrufe"));
                if (gz.Dreiecke > StadtbauGrenzen.Dreiecke || gz.Aufrufe > StadtbauGrenzen.Aufrufe)
                {
                    Fund(string.Create(Inv, $"Grenzen mit Probehaus: {gz.Dreiecke} Dreiecke / {gz.Aufrufe} Aufrufe ueber {StadtbauGrenzen.Dreiecke} / {StadtbauGrenzen.Aufrufe}"));
                }
            }
            else
            {
                Fund("Grenzen mit Probehaus: keine Messung");
            }
            if (a.Drei.Spiel?.Szene.Haeuser.Count is null or 0)
            {
                Fund("Probehaus 3D: Szene ohne Haeuser");
            }
        }
        else
        {
            Messwerte.Notiere("Probehaus 3D: kein OpenGL auf diesem Rechner, nicht gemessen");
        }
        a.ReiterZeigen(innen: false);
        await BildAsync(a);
    }

    private static (double X, double Y) Lage(Projekt p, AussenStelle s) => AussenBearbeitung.Lage(p.AussenStand, s);

    private static string PythonJsonText((double X, double Y) l) => string.Create(Inv, $"{l.X:F1}, {l.Y:F1}");
}
