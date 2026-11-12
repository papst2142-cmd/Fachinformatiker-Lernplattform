using System.Diagnostics;
using System.Globalization;
using Avalonia.Input;
using Avalonia.Interactivity;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;
using Lernplattform.Spiel;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Welle A 0.67.0 (Weisung P12-204, Sammelliste 301 und 302): Questlog im Handy und Busfahrt.
/// Questlog: Knopf unten links mit Zahl und Namen „Aufträge, n offen“, Messpunkt „Spiel: Questlog oeffnen (bis zum Bild)“
/// (wiederholbar, 400 ms), Reiter Erledigt, Taste L, „Hingehen“ fuehrt zum Ort des Auftrags.
/// Bus: die Stadt bekommt im Start-Test (nur im Speicher) Gehwege, Zebrastreifen und drei Haltestellen (wie
/// <c>SpielBusfahrtTests</c>); Messpunkt „Spiel: Ortsliste mit Buszeiten (bis zum Bild)“ (wiederholbar, 400 ms: je Ort Weg
/// zu Fuss und Busplan), dann eine Fahrt mit dem Bus: Zeitraffer hoechstens 8 s, die Figur sitzt im gezeichneten Bus,
/// eine Zeile <c>stadt_gegangen</c> mit <c>mit: bus</c>. Gegenprobe: ohne Haltestellen kein Knopf.
/// Layout: Questlog offen, Erledigt, Ortsliste mit Bus, im Bus.
/// </summary>
public static partial class AutoMessung
{
    /// <summary>Haltestellen und Zebrastreifen der Probe (Fassung 0, gleich <c>SpielBusfahrtTests</c>).</summary>
    private static readonly (double X, double Y)[] BusHalte = [(2.5, 20.3), (26.0, 20.3), (7.0, 4.0)];

    private static readonly (double X, double Y)[] BusZebras = [(7.5, 17.5), (18.4, 17.5), (29.8, 17.5), (12.0, 20.8), (7.5, 9.0), (12.0, 12.6)];

    /// <summary>Frisches Spiel in der Stadt mit Gehweg an jeder Strasse, Zebrastreifen und Haltestellen (nur im Speicher).</summary>
    internal static StadtSpiel MitBus(SpielSeite seite)
    {
        var alt = seite.Spiel!;
        var inhalt = (PyObjekt)Py.Kopie(alt.Stadt.Inhalt)!;
        var karte = SpielDaten.Objekt(inhalt["orte"]);
        foreach (var teil in SpielDaten.Liste(SpielDaten.Objekt(karte["welt"])["landschaft"]).Select(SpielDaten.Objekt))
        {
            if (Py.Gleich(teil["typ"], "strasse"))
            {
                Py.Setzen(teil, "gehweg", 0.35);
            }
        }
        var daten = (PyObjekt)Py.Kopie(alt.Stadt.Daten)!;
        Py.Setzen(daten, Haltestellen.Abschnitt, BusHalte.Select(h => (object?)new PyObjekt { { "x", h.X }, { "y", h.Y } }).ToList());
        Py.Setzen(daten, Zebrastreifen.Abschnitt, BusZebras.Select(h => (object?)new PyObjekt { { "x", h.X }, { "y", h.Y } }).ToList());
        var stadt = new Stadt(inhalt, daten) { Fassung = alt.Stadt.Fassung };
        var spiel = SpielStart.Neu(stadt, alt.Grafik, alt.Innenraeume, alt.GeschaefteDaten, alt.Feste, Festzeit.Heute, Jahreszeiten.Heute);
        seite.SpielSetzen(spiel);
        return spiel;
    }

    /// <summary>Questlog oeffnen und warten, bis das Fenster gezeichnet ist (der Aufruf endet erst beim Schliessen).</summary>
    private static async Task<Task?> QuestlogOeffnenAsync(MainView ansicht, SpielSeite seite, bool erledigt = false)
    {
        var offen = seite.QuestlogZeigenAsync(erledigt);
        await ansicht.BildFertigAsync();
        return Dialog.Offen is null ? null : offen;
    }

    private static async Task QuestlogSchliessenAsync(MainView ansicht, Task? offen)
    {
        Dialog.AlleSchliessen();
        if (offen is not null)
        {
            await offen;
        }
        await ansicht.BildFertigAsync();
    }

    private static async Task<int> QuestlogBusAsync(MainView ansicht)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Pruefe(string punkt, bool ok, string wert)
        {
            Messwerte.Notiere("Spiel: " + punkt + ": " + wert);
            if (!ok)
            {
                Messwerte.Notiere("  FEHLER: Spiel " + punkt + ": " + wert);
                funde++;
            }
        }
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            Messwerte.Notiere("  FEHLER: Spiel Questlog: Spielseite nicht gebaut");
            return 1;
        }
        var gl = await GlAbwartenAsync(seite);
        var vorher = seite.Spiel;
        try
        {
            // ---- Questlog ------------------------------------------------------------------------------------------
            var spiel = Frisch(seite);
            await ansicht.BildFertigAsync();
            var zahl = Questlog.ZahlOffen(spiel);
            var name = seite.QuestKnopf is { } k ? Avalonia.Automation.AutomationProperties.GetName(k) : null;
            Pruefe("Questlog Knopf", seite.QuestKnopf is { IsEffectivelyVisible: true } && zahl > 0
                && name == string.Format(inv, VorleseTexte.Auftraege, zahl), $"Name \"{name}\", offen {zahl}");
            async Task<double?> Oeffnen()
            {
                var u = Stopwatch.StartNew();
                var t = await QuestlogOeffnenAsync(ansicht, seite);
                var ms = u.Elapsed.TotalMilliseconds;
                return t is null ? null : ms;
            }
            var erstes = await Oeffnen();
            Pruefe("Questlog offen", Dialog.Offen is not null && !seite.QuestlogErledigt && seite.QuestlogEintraege.Count == zahl,
                $"{seite.QuestlogEintraege.Count} Eintraege, Hingehen {seite.QuestlogHingehen.Count}");
            funde += Ergebnis("Spiel: Questlog oeffnen (bis zum Bild)",
                await Messwiederholung.MessenAsync(erstes, Geraetegrenze, Oeffnen, () => QuestlogSchliessenAsync(ansicht, null), Task.Delay));
            // Hingehen: zum Ort des ersten Auftrags
            var ziel = seite.QuestlogEintraege.FirstOrDefault(e => e.Ziel is not null)?.Ziel;
            if (seite.QuestlogHingehen.FirstOrDefault() is { } hin && ziel is not null)
            {
                hin.RaiseEvent(new RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                await ansicht.BildFertigAsync();
                spiel.SofortAnkommen();
                await ansicht.BildFertigAsync();
                Pruefe("Questlog Hingehen", Dialog.Offen is null && spiel.Jetzt.Ort == ziel, $"Ziel {ziel}, jetzt {spiel.Jetzt.Ort}");
            }
            else
            {
                Pruefe("Questlog Hingehen", false, "kein Knopf Hingehen");
                await QuestlogSchliessenAsync(ansicht, null);
            }
            // Reiter Erledigt (frischer Spielstand: noch nichts geloest, die Zahl stimmt mit den Daten)
            var t2 = await QuestlogOeffnenAsync(ansicht, seite, erledigt: true);
            Pruefe("Questlog Erledigt", t2 is not null && seite.QuestlogErledigt && seite.QuestlogEintraege.Count == Questlog.Erledigte(spiel).Count,
                $"{seite.QuestlogEintraege.Count} erledigt");
            await QuestlogSchliessenAsync(ansicht, t2);
            // Taste L (Vorgabe der Tastenbelegung)
            seite.RaiseEvent(new KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Key.L, Source = seite });
            await ansicht.BildFertigAsync();
            Pruefe("Questlog Taste L", Dialog.Offen is not null, Dialog.Offen is null ? "zu" : "offen");
            await QuestlogSchliessenAsync(ansicht, null);

            // ---- Bus -----------------------------------------------------------------------------------------------
            var bus = MitBus(seite);
            await ansicht.BildFertigAsync();
            Pruefe("Bus Linie", bus.Linie is { NichtErreicht.Count: 0 } l && l.Halte.Count == BusHalte.Length,
                $"{bus.Szene.Haltestellen.Count} Haltestellen");
            async Task<double?> Liste()
            {
                var u = Stopwatch.StartNew();
                seite.OrtslisteZeigen(true);
                await ansicht.BildFertigAsync();
                return u.Elapsed.TotalMilliseconds;
            }
            async Task ListeZu()
            {
                seite.OrtslisteZeigen(false);
                // anderer Schluessel, damit die Zeiten beim naechsten Oeffnen neu gerechnet werden
                seite.BusZeitenVergessen();
                await ansicht.BildFertigAsync();
            }
            await ListeZu();
            var erste = await Liste();
            var lohnt = bus.BusLohnt("amt");
            Pruefe("Bus lohnt zum Amt", lohnt is { } z && z.Bus < z.ZuFuss && seite.BusKnopfSichtbar("amt"),
                lohnt is { } w ? string.Create(inv, $"zu Fuss {w.ZuFuss}, Bus {w.Bus}") : "kein Bus");
            funde += Ergebnis("Spiel: Ortsliste mit Buszeiten (bis zum Bild)",
                await Messwiederholung.MessenAsync(erste, Geraetegrenze, Liste, ListeZu, Task.Delay));
            var zeilen = bus.Ablauf.Stand.Verlauf.Count;
            var uhr = bus.Jetzt.Uhr;
            Pruefe("Bus Fahrt", bus.MitBusZu("amt") == StadtGrund.Ok && bus.Ablauf.Stand.Verlauf.Count == zeilen + 1
                && Py.Gleich(SpielDaten.Get(bus.Ablauf.Stand.Verlauf[^1].Daten, Stadt.Mit, null), Stadt.MitBus) && bus.Jetzt.Uhr == uhr + lohnt?.Bus,
                string.Create(inv, $"Uhr {uhr} -> {bus.Jetzt.Uhr}"));
            if (gl)
            {
                var u = Stopwatch.StartNew();
                var (imBus, gezeichnet) = (0.0, false);
                while (bus.Geht && u.Elapsed.TotalSeconds < 120)
                {
                    var b = Stopwatch.StartNew();
                    await seite.Flaeche.NaechstesBildAsync();
                    if (bus.ImBus)
                    {
                        imBus += b.Elapsed.TotalSeconds;
                        gezeichnet |= seite.Flaeche.BusMitFigur;
                    }
                }
                Pruefe("Bus Zeitraffer", imBus is > 0.5 and <= Busfahrt.ZeitrafferSekunden + 0.5 && gezeichnet && bus.Jetzt.Ort == "amt" && !bus.Geht,
                    string.Create(inv, $"{imBus:0.0} s im Bus, Figur im Bus gezeichnet {gezeichnet}, Ort {bus.Jetzt.Ort}"));
            }
            else
            {
                bus.SofortAnkommen();
                Messwerte.Notiere("Spiel: Bus Zeitraffer nicht geprueft (ohne 3D)");
            }
            // Gegenprobe: Stadt ohne Haltestellen (die Stadt von vorher), kein Bus-Knopf
            seite.SpielSetzen(vorher);
            var ohne = Frisch(seite);
            seite.OrtslisteZeigen(true);
            await ansicht.BildFertigAsync();
            Pruefe("Bus Gegenprobe ohne Haltestellen", ohne.Linie is null && !seite.BusKnopfSichtbar("amt"), "kein Knopf");
            seite.OrtslisteZeigen(false);
        }
        finally
        {
            Dialog.AlleSchliessen();
            seite.SpielSetzen(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }

    /// <summary>Layout: Questlog offen und Erledigt, Ortsliste mit Bus, im Bus.</summary>
    private static async IAsyncEnumerable<string> QuestlogBusAnsichten(MainView ansicht)
    {
        var seite = await SpielSeiteAsync(ansicht);
        if (seite?.Spiel is null)
        {
            P7W1AnsichtFehler.Add("spiel/questlog: Spielseite nicht gebaut");
            yield break;
        }
        var vorher = seite.Spiel;
        Frisch(seite);
        var t = await QuestlogOeffnenAsync(ansicht, seite);
        yield return "spiel/questlog-offen";
        await QuestlogSchliessenAsync(ansicht, t);
        var spiel = seite.Spiel!;
        if (spiel.Ablauf.Stand.OffeneTickets().FirstOrDefault() is { } auftrag)
        {
            spiel.Ablauf.Loesen(Py.Str(auftrag["id"]), Bewertung.Loesung(auftrag, null, spiel.Ablauf.Stand.Inhalt), false);
        }
        t = await QuestlogOeffnenAsync(ansicht, seite, erledigt: true);
        yield return "spiel/questlog-erledigt";
        await QuestlogSchliessenAsync(ansicht, t);
        var bus = MitBus(seite);
        seite.OrtslisteZeigen(true);
        await ansicht.BildFertigAsync();
        P7W1AnsichtFehler.AddRange(KnoepfeImBild(seite, "spiel/ortsliste-bus"));
        yield return "spiel/ortsliste-bus";
        seite.OrtslisteZeigen(false);
        if (bus.MitBusZu("amt") == StadtGrund.Ok && !seite.ErsatzSichtbar)
        {
            var u = Stopwatch.StartNew();
            while (!bus.ImBus && bus.Geht && u.Elapsed.TotalSeconds < 60)
            {
                await seite.Flaeche.NaechstesBildAsync();
            }
            await ansicht.BildFertigAsync();
            yield return "spiel/im-bus";
        }
        bus.SofortAnkommen();
        seite.SpielSetzen(vorher);
        await ansicht.BildFertigAsync();
    }
}
