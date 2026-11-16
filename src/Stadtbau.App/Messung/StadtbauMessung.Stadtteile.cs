using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Messung;
using Stadtbau.Core;
using Stadtbau.Oberflaeche;

namespace Stadtbau.Messung;

/// <summary>
/// Welle Nachladen S 0.54.0 (Weisung P12-280): „Stadtteile zeigen“ an der Grundlage des Werkzeugs.
/// <list type="bullet">
/// <item>Knopf mit Maus, Finger und ueber die Automation (wie ein Screenreader), aus mit Taste T: Raster (Spalten + 1 und Reihen + 1 Linien),
/// Namen jeder sichtbaren Kachel, nach dem Rechnen Dreiecke und MB je Kachel; Name des Knopfs mit „gewählt“.</item>
/// <item>Werte = Bau einer Kachel im Spiel (<see cref="StadtteilWert"/>): Summe der Dreiecke aller Kacheln gleich der
/// Summe der Netze der Kacheln, jede Kachel einmal.</item>
/// <item>In 3D meldet der Knopf den Stadtteil in der Mitte der Ansicht.</item>
/// </list>
/// </summary>
public static partial class StadtbauMessung
{
    private static async Task StadtteileAsync(Hauptansicht a, Window? f)
    {
        a.NeuAnlegen("Starttest Stadtteile", null);
        await a.AussenGeprueft;
        await a.GrenzenLiveFertig;
        a.ReiterZeigen(innen: false);
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        var p = a.Projekt!;
        var (b, h) = Aussen.Groesse(p.AussenStand);
        var (spalten, reihen) = Stadtteile.Raster((float)b, (float)h);
        var linien = spalten + 1 + reihen + 1;
        var knopf = a.StadtteileKnopf;

        async Task<bool> AnPruefen(string wie)
        {
            // nie ewig warten: ist der Knopf nicht an, rechnet nichts (dann Fund unten)
            if (a.Karte.StadtteileAn)
            {
                await Task.WhenAny(a.StadtteileFertig, Task.Delay(TimeSpan.FromMinutes(2)));
            }
            await BildAsync(a);
            var (namen, werte, l) = a.Karte.StadtteileGezeichnet;
            var name = AutomationProperties.GetName(knopf) ?? "";
            Messwerte.Notiere(string.Create(Inv, $"Stadtteile ({wie}): {l} Linien, {namen} Namen, {werte} Wertezeilen, Knopf \"{name}\""));
            if (!a.Karte.StadtteileAn || l != linien || namen < 1 || namen > spalten * reihen || werte != namen || name != Texte.Gewaehlt(Texte.Stadtteile))
            {
                Fund(string.Create(Inv, $"Stadtteile ({wie}): an {a.Karte.StadtteileAn}, {l} statt {linien} Linien, {namen} Namen, {werte} Wertezeilen, Knopf \"{name}\""));
                return false;
            }
            return true;
        }

        async Task AusPruefen(string wie)
        {
            await BildAsync(a);
            var (namen, _, l) = a.Karte.StadtteileGezeichnet;
            if (a.Karte.StadtteileAn || namen != 0 || l != 0 || AutomationProperties.GetName(knopf) != Texte.Stadtteile)
            {
                Fund("Stadtteile (" + wie + "): nicht aus");
            }
        }

        async Task DrueckenAsync(PointerType art)
        {
            var zeiger = new Pointer(Pointer.GetNextFreeId(), art, true);
            var mitte = new Point(knopf.Bounds.Width / 2, knopf.Bounds.Height / 2);
            var wurzel = Wurzel(knopf);
            var ort = knopf.TranslatePoint(mitte, wurzel) ?? mitte;
            knopf.RaiseEvent(new PointerPressedEventArgs(knopf, zeiger, wurzel, ort, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
            knopf.RaiseEvent(new PointerReleasedEventArgs(knopf, zeiger, wurzel, ort, 0,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
            await BildAsync(a);
        }

        // Maus an und aus
        await DrueckenAsync(PointerType.Mouse);
        await AnPruefen("Maus");
        if (a.StatusText != Texte.StadtteileFertig(spalten * reihen))
        {
            Fund("Stadtteile: Meldung nach dem Rechnen fehlt (\"" + a.StatusText + "\")");
        }
        // Werte = Bau der Kacheln im Spiel (eigene Rechnung im Test)
        if (a.Karte.Stadtteile is { } werte)
        {
            var alle = Stadtteile.Alle((float)b, (float)h).ToList();
            Messwerte.Notiere("Stadtteile Werte: " + string.Join(", ", werte.Select(w => string.Create(Inv, $"{w.Name} {w.Dreiecke} Dreiecke {w.Mb:F2} MB"))));
            if (!werte.Select(w => w.Kachel).SequenceEqual(alle) || werte.Sum(w => w.Dreiecke) == 0)
            {
                Fund("Stadtteile: Werte nicht fuer jede Kachel genau einmal oder alle leer");
            }
            var spiel = Ansicht3D.Bauen(p.Stand(), p.Daten, Ansicht3D.Einstellung.Grenzen);
            foreach (var w in werte)
            {
                var n = spiel.Szene.KachelVorbereiten(w.Kachel);
                var dreiecke = n.Dreiecke;
                if (dreiecke != w.Dreiecke)
                {
                    Fund(string.Create(Inv, $"Stadtteile: {w.Name} zeigt {w.Dreiecke} Dreiecke, die Kachel hat {dreiecke}"));
                }
            }
        }
        else
        {
            Fund("Stadtteile: nach dem Rechnen keine Werte");
        }
        await AlleGroessenAsync(a, f, "Stadtteile");
        await GroesseAsync(a, f, Groessen[0].B, Groessen[0].H);
        await DrueckenAsync(PointerType.Mouse);
        await AusPruefen("Maus");

        // Finger an, Taste T aus
        await DrueckenAsync(PointerType.Touch);
        await AnPruefen("Finger");
        a.TasteFuerTests(Key.T);
        await AusPruefen("Taste T");

        // Screenreader: Aufruf ueber die Automation, Name mit „gewählt“
        if (ControlAutomationPeer.CreatePeerForElement(knopf).GetProvider<IInvokeProvider>() is { } aufruf)
        {
            aufruf.Invoke();
            await AnPruefen("Screenreader");
            aufruf.Invoke();
            await AusPruefen("Screenreader");
        }
        else
        {
            Fund("Stadtteile: Knopf ohne Aufruf fuer den Screenreader");
        }

        // 3D: der Knopf nennt den Stadtteil in der Mitte der Ansicht
        a.DreiZeigen();
        await a.DreiFertig;
        await DrueckenAsync(PointerType.Mouse);
        Messwerte.Notiere("Stadtteile in 3D: \"" + a.StatusText + "\"");
        if (!a.StatusText.StartsWith("In der Mitte der Ansicht: ", StringComparison.Ordinal) || a.Karte.StadtteileAn)
        {
            Fund("Stadtteile in 3D: keine Meldung des Stadtteils (\"" + a.StatusText + "\")");
        }
        a.ReiterZeigen(innen: false);
    }
}
