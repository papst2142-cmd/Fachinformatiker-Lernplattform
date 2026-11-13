using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Spiel;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// S 0.50.0 (Weisung P12-234 mit P12-231 Antwort 1b): Moebel im Innenraum in 3D bauen, mit echten Fingern auf der Ebene ueber
/// dem 3D-Bild des Elektromarkts. Antippen waehlt das Stueck unter dem Finger (Gegenprobe: freie Stelle waehlt ab), Ziehen
/// schiebt es als genau ein Schritt im Verlauf (Gegenprobe: Ziehen auf freier Stelle aendert nichts), R dreht, Entf loescht,
/// Setzen aus dem Katalog mit Schatten gruen (freie Stelle) und rot (auf einem Stueck, Gegenprobe), Antippen ausserhalb setzt
/// nichts. Messpunkte bis zum neuen Bild, wiederholbar, 400 ms: „Innenraum 3D Moebel verschieben/drehen/loeschen/setzen (bis
/// zum Bild)“, dazu „Innenraum 3D Schatten pruefen“. Zwei Finger zoomen mit derselben Probe wie draussen (Gegenprobe alte
/// Auswertung). Layout je Groesse hell und dunkel mit Auswahl und Schatten.
/// </summary>
public static partial class StadtbauMessung
{
    private const string InnenOrt = "elektromarkt";

    private static async Task InnenBauen3DAsync(Hauptansicht a, Window? f)
    {
        if (a.InnenBedienung is not { } b || a.Innen3DFlaeche.AktuelleKamera is null)
        {
            Fund("Innenraum 3D bauen: keine Bedienung ueber dem 3D-Bild");
            return;
        }
        var p = a.Projekt!;
        string Stand() => PythonJson.Text(p.Grundriss(InnenOrt));
        var auf = Suchen(b, x => b.TrefferAn(x) is not null);
        var frei = Suchen(b, x => b.TrefferAn(x) is null && BodenImRaum(a, b, x));
        if (auf is null || frei is null)
        {
            Fund("Innenraum 3D bauen: keine " + (auf is null ? "Stelle mit Moebel" : "freie Stelle am Boden") + " im Bild gefunden");
            return;
        }

        // Antippen waehlt das Stueck unter dem Finger, aendert nichts; Gegenprobe: freie Stelle waehlt ab
        var stand = Stand();
        var schritte = p.SchritteZurueck;
        await TippenAsync(b, frei.Value);
        if (a.Flaeche.Auswahl is not null)
        {
            Fund("Innenraum 3D bauen: Antippen einer freien Stelle waehlt ein Stueck (Gegenprobe)");
        }
        await TippenAsync(b, auf.Value);
        var gewaehlt = a.Flaeche.Auswahl;
        if (gewaehlt is null || gewaehlt != b.TrefferAn(auf.Value))
        {
            Fund("Innenraum 3D bauen: Antippen eines Moebels waehlt es nicht");
            return;
        }
        if (Stand() != stand || p.SchritteZurueck != schritte)
        {
            Fund("Innenraum 3D bauen: Antippen hat den Grundriss geaendert");
        }
        await BildAsync(a);
        if (b.AuswahlRahmen.Width <= 0)
        {
            Fund("Innenraum 3D bauen: das gewaehlte Moebel ist im Bild nicht markiert");
        }

        // Ziehen mit einem Finger = ein Schritt „Verschieben“ wie im Grundriss; Gegenprobe: Ziehen auf freier Stelle aendert nichts
        await MessenAsync(a, "Innenraum 3D Moebel verschieben (bis zum Bild)", stand, schritte, async () =>
        {
            await TippenAsync(b, auf.Value);
            await ZiehenAsync(b, auf.Value, 0, 60);
        }, "Verschieben");
        await ZiehenAsync(b, frei.Value, 0, 60);
        if (Stand() != stand || p.SchritteZurueck != schritte)
        {
            Fund("Innenraum 3D bauen: Ziehen auf freier Stelle hat ein Moebel verschoben (Gegenprobe)");
            await ZurueckAsync(a, stand);
        }

        // Drehen (R) und Loeschen (Entf) am gewaehlten Moebel, gleiche Tasten und Schritte wie im Grundriss
        await MessenAsync(a, "Innenraum 3D Moebel drehen (bis zum Bild)", stand, schritte, async () =>
        {
            await TippenAsync(b, auf.Value);
            a.Flaeche.Taste(Key.R, KeyModifiers.None);
        }, "Drehen");
        var anzahl = Bearbeitung.Alle(p.Grundriss(InnenOrt)).Count();
        await MessenAsync(a, "Innenraum 3D Moebel loeschen (bis zum Bild)", stand, schritte, async () =>
        {
            await TippenAsync(b, auf.Value);
            a.Flaeche.Taste(Key.Delete, KeyModifiers.None);
            if (Bearbeitung.Alle(p.Grundriss(InnenOrt)).Count() != anzahl - 1)
            {
                Fund("Innenraum 3D bauen: Entf loescht das gewaehlte Moebel nicht");
            }
        }, "Löschen");

        // Setzen aus dem Katalog: Schatten gruen auf freier Stelle, rot auf einem Stueck (Gegenprobe), dann Antippen setzt
        a.Flaeche.Waehlen(null);
        var typ = Py.Str(Bearbeitung.Deko(p.Grundriss(InnenOrt), gewaehlt)["typ"]);
        a.SetzenWaehlen(typ);
        var gruen = await SchattenAsync(b, frei.Value);
        if (gruen != true)
        {
            // die erste freie Stelle kann zu eng sein (Wand, Weg): weitere freie Stellen versuchen
            foreach (var x in Stellen(b, y => b.TrefferAn(y) is null && BodenImRaum(a, b, y)).Take(40))
            {
                if (await SchattenAsync(b, x) == true)
                {
                    (frei, gruen) = (x, true);
                    break;
                }
            }
        }
        if (gruen != true)
        {
            Fund("Innenraum 3D bauen: Schatten ist an keiner freien Stelle gruen (" + b.SchattenText + ")");
        }
        Messwerte.Notiere(string.Create(Inv, $"Innenraum 3D Schatten: Vorschau {b.SchattenMs:F1} ms"));
        await AlleGroessenAsync(a, f, "Innenraum 3D bauen Schatten");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        // Gegenprobe rot: der Boden unter der Mitte des gewaehlten Moebels (der Bildpunkt „auf“ trifft dessen Kasten, der
        // Schatten liegt aber am Boden dahinter, darum die Mitte selbst; dasselbe Stueck dort ueberschneidet sich)
        var dg = Bearbeitung.Deko(p.Grundriss(InnenOrt), gewaehlt);
        var mitteAuf = b.BildpunktAm(Py.F(dg["x"]) + (Py.F(dg["w"]) / 2), Py.F(dg["y"]) + (Py.F(dg["h"]) / 2));
        if (mitteAuf is null || await SchattenAsync(b, mitteAuf.Value) != false)
        {
            Fund("Innenraum 3D bauen: Schatten auf einem Moebel nicht rot (Gegenprobe; " + b.SchattenText + ")");
        }
        await SchattenAsync(b, frei.Value);
        await MessenAsync(a, "Innenraum 3D Moebel setzen (bis zum Bild)", stand, schritte, () => TippenAsync(b, frei.Value), "Setzen");

        // Gegenprobe: Antippen ausserhalb jedes Raums (oben im Bild) setzt nichts
        foreach (var draussen in Stellen(b, y => !BodenImRaum(a, b, y)).Take(1))
        {
            await TippenAsync(b, draussen);
            if (Stand() != stand || p.SchritteZurueck != schritte)
            {
                Fund("Innenraum 3D bauen: Antippen ausserhalb des Raums hat etwas gesetzt (Gegenprobe)");
                await ZurueckAsync(a, stand);
            }
        }
        a.Flaeche.SetzenTyp = null;
        b.SchattenWeg();

        // zwei Finger zoomen, ein Finger auf einem Moebel schiebt nichts mit (dieselbe Probe wie draussen, mit Gegenprobe)
        double Zoom3D() => 1 / a.Innen3DFlaeche.AktuelleKamera!.Abstand;
        await FlaecheAsync(a, "Innenraum 3D", b, Zoom3D, x => b.TrefferAn(x) is not null, Stand, alt => b.GesteAltFuerTests = alt);
        a.Flaeche.Waehlen(null);
        if (Stand() != stand)
        {
            Fund("Innenraum 3D bauen: nach allen Proben ist der Grundriss nicht wieder wie vorher");
        }
    }

    /// <summary>Trifft der Bildpunkt den Boden in einem Raum oder Flur des offenen Grundrisses?</summary>
    private static bool BodenImRaum(Hauptansicht a, InnenDreiBedienung b, Point x) =>
        a.Innen3DFlaeche.AktuelleKamera?.Boden((float)x.X, (float)x.Y, (float)b.Bounds.Width, (float)b.Bounds.Height) is { } boden
        && Bearbeitung.BereichBei(a.Projekt!.Grundriss(InnenOrt), boden.X, boden.Y) is not null;

    /// <summary>Alle Stellen im Bild (Raster 12) mit der Bedingung.</summary>
    private static IEnumerable<Point> Stellen(Control c, Func<Point, bool> bedingung)
    {
        for (var y = 60.0; y < c.Bounds.Height - 60; y += 12)
        {
            for (var x = 80.0; x < c.Bounds.Width - 180; x += 12)
            {
                var p = new Point(x, y);
                if (bedingung(p))
                {
                    yield return p;
                }
            }
        }
    }

    /// <summary>Schatten an der Stelle bis zum Ergebnis der Pruefung (Messpunkt „Innenraum 3D Schatten pruefen“).</summary>
    private static async Task<bool?> SchattenAsync(InnenDreiBedienung b, Point x)
    {
        var fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Ende() => fertig.TrySetResult();
        b.SchattenGeprueft += Ende;
        try
        {
            var werte = new List<double>();
            for (var i = 0; i < 3; i++)
            {
                var uhr = System.Diagnostics.Stopwatch.StartNew();
                b.SchattenWeg();
                b.SchattenAn(x);
                if (!b.SchattenDa)
                {
                    return null;
                }
                await Task.WhenAny(fertig.Task, Task.Delay(5000));
                werte.Add(uhr.Elapsed.TotalMilliseconds);
                if (werte[^1] <= Grenze)
                {
                    break;
                }
                fertig = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                await Task.Delay(200);
            }
            DreiGrenze("Innenraum 3D Schatten pruefen", werte, einmalig: false);
            await BildAsync(b);
            return b.Passt;
        }
        finally
        {
            b.SchattenGeprueft -= Ende;
        }
    }

    /// <summary>
    /// Ein Bauschritt in 3D bis zum neuen Bild, wiederholbar (bis zu 3 Mal, nach jedem Mal rueckgaengig): genau ein Schritt
    /// im Verlauf mit dem Namen wie im Grundriss, der Innenraum wird neu gebaut, rueckgaengig stellt den Stand wieder her.
    /// </summary>
    private static async Task MessenAsync(Hauptansicht a, string name, string stand, int schritte, Func<Task> tun, string was)
    {
        var p = a.Projekt!;
        var werte = new List<double>();
        for (var i = 0; i < 3; i++)
        {
            var innen = a.Innen3DFlaeche.Spiel?.Innen;
            var uhr = System.Diagnostics.Stopwatch.StartNew();
            await tun();
            await a.Innen3DFertig;
            await BildAsync(a);
            werte.Add(uhr.Elapsed.TotalMilliseconds);
            if (p.SchritteZurueck != schritte + 1 || p.Verlauf[^1].Was != was || PythonJson.Text(p.Grundriss(InnenOrt)) == stand)
            {
                Fund(name + ": nicht genau ein Schritt „" + was + "“ im Verlauf (" + (p.SchritteZurueck - schritte).ToString(Inv) + " Schritte, letzter „"
                    + (p.Verlauf.Count > 0 ? p.Verlauf[^1].Was : "") + "“)");
            }
            if (ReferenceEquals(innen, a.Innen3DFlaeche.Spiel?.Innen))
            {
                Fund(name + ": der Innenraum in 3D wurde nicht neu gebaut");
            }
            await ZurueckAsync(a, stand);
            if (werte[^1] <= Grenze)
            {
                break;
            }
            await Task.Delay(200);
        }
        DreiGrenze(name, werte, einmalig: false);
    }

    private static async Task ZurueckAsync(Hauptansicht a, string stand)
    {
        var p = a.Projekt!;
        for (var n = 0; n < 3 && PythonJson.Text(p.Grundriss(InnenOrt)) != stand; n++)
        {
            a.Rueckgaengig();
        }
        await a.Innen3DFertig;
        await BildAsync(a);
        if (PythonJson.Text(p.Grundriss(InnenOrt)) != stand)
        {
            Fund("Innenraum 3D bauen: Rueckgaengig stellt den Grundriss nicht wieder her");
        }
    }
}
