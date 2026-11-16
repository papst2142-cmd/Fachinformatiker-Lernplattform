using Avalonia.Controls;
using Avalonia.Threading;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Design;
using Lernplattform.Core.Hilfe;
using Lernplattform.Design;
using Lernplattform.Programm;
using Lernplattform.Views;

namespace Lernplattform.Messung;

/// <summary>
/// Patch A 0.81.1 (Sammelliste 341, Weisung P12-277): nach jedem Wechsel hell/dunkel ist in jeder Leiste genau der aktuelle
/// Eintrag markiert. Je Eintrag der Seitenleiste: markieren, pruefen (Knopf, Strich, Symbol, Text, Pfeil, Pille der unteren
/// Leiste), Farbwelt wechseln, pruefen, zurueck, pruefen. Rundgang Seite 5: Fehler an AP1, dann an AP2; AP1 traegt danach
/// keinen roten Rahmen mehr, auch nicht nach hell/dunkel. Gegenprobe: der alte Weg (<see cref="Markierung.AltFuerTests"/>,
/// <c>Farbe</c> plus <c>ClearValue</c>) muss einen Fund liefern.
/// </summary>
public static partial class AutoMessung
{
    private static async Task<int> Markierung341Async(MainView ansicht)
    {
        var funde = 0;
        var werte = Zustand.Werte;
        var start = FarbenAnwenden.Aktuell.Hell ? Farbwelt.Hell : Farbwelt.Dunkel;
        var andere = start == Farbwelt.Hell ? Farbwelt.Dunkel : Farbwelt.Hell;
        void Farben(string darstellung) => FarbenAnwenden.Anwenden(Farbwelt.Berechne(darstellung, Einstellungen.Grundfarbe(werte), Einstellungen.Hintergrund(werte)));
        static async Task Warten()
        {
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        }
        var leisten = ansicht.Leisten;

        // Leisten: liefert die Abweichungen (leer = genau der aktuelle Eintrag markiert)
        List<string> Leiste(string id)
        {
            var ist = leisten.MarkiertFuerTests();
            var soll = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["Knopf"] = [id],
                ["Strich"] = [id],
                ["Symbol"] = [id],
                ["Text"] = [id],
                ["Pfeil"] = leisten.HatPfeilFuerTests(id) ? [id] : [],
                ["Pille"] = leisten.UntenZielFuerTests(id) is { } z ? [z] : [],
            };
            return soll.Where(s => !s.Value.SequenceEqual(ist[s.Key]))
                .Select(s => s.Key + " markiert: [" + string.Join(", ", ist[s.Key]) + "] statt [" + string.Join(", ", s.Value) + "]").ToList();
        }
        async Task<int> LeistenDurchgang(bool notieren)
        {
            var abweichungen = 0;
            foreach (var id in leisten.SeitenKennungenFuerTests)
            {
                foreach (var (wann, darstellung) in new[] { ("", (string?)null), (" " + andere, andere), (" wieder " + start, start) })
                {
                    if (darstellung is null)
                    {
                        leisten.Markiere(id);
                    }
                    else
                    {
                        Farben(darstellung);
                    }
                    await Warten();
                    var fehler = Leiste(id);
                    if (fehler.Count > 0)
                    {
                        abweichungen++;
                        if (notieren)
                        {
                            Messwerte.Notiere("  FEHLER: Markierung 341 Leisten " + id + wann + ": " + string.Join("; ", fehler));
                        }
                    }
                }
            }
            return abweichungen;
        }

        // Rundgang: liefert (AP1 rot, AP2 rot) nach Fehler an AP1, dann an AP2 und hell/dunkel
        async Task<List<string>?> Rundgang()
        {
            if (RundgangEbene.Probe() is not { } rundgang)
            {
                return null;
            }
            var fehler = new List<string>();
            try
            {
                for (var i = 0; i < RundgangAblauf.Anzahl && !rundgang.Elemente.ContainsKey(RundgangElement.TerminAp1); i++)
                {
                    rundgang.Springen(i);
                    await ansicht.BildFertigAsync();
                }
                if (rundgang.Elemente.GetValueOrDefault(RundgangElement.TerminAp1) is not TextBox ap1
                    || rundgang.Elemente.GetValueOrDefault(RundgangElement.TerminAp2) is not TextBox ap2)
                {
                    fehler.Add("Seite mit den Terminfeldern nicht gefunden");
                    return fehler;
                }
                bool Rot(TextBox feld) => ap1.TryFindResource("Pinsel.red", out var rot) && rot is not null && ReferenceEquals(feld.BorderBrush, rot);
                void Pruefen(string wann, bool ap1Rot, bool ap2Rot)
                {
                    if (Rot(ap1) != ap1Rot || Rot(ap2) != ap2Rot)
                    {
                        fehler.Add($"{wann}: AP1 {(Rot(ap1) ? "rot" : "ohne Rot")} (soll {(ap1Rot ? "rot" : "ohne Rot")}), AP2 {(Rot(ap2) ? "rot" : "ohne Rot")} (soll {(ap2Rot ? "rot" : "ohne Rot")})");
                    }
                }
                ap1.Text = "31.02.2027";
                rundgang.Fertig();
                await Warten();
                Pruefen("Fehler an AP1", true, false);
                ap1.Text = "";
                ap2.Text = "31.02.2027";
                rundgang.Fertig();
                await Warten();
                Pruefen("Fehler an AP2", false, true);
                Farben(andere);
                await Warten();
                Pruefen("Fehler an AP2, " + andere, false, true);
                Farben(start);
                await Warten();
                Pruefen("Fehler an AP2, wieder " + start, false, true);
            }
            finally
            {
                if (!rundgang.Geschlossen)
                {
                    rundgang.Ueberspringen();
                }
                await ansicht.BildFertigAsync();
            }
            return fehler;
        }

        var vorher = ansicht.AktuelleSeite;
        try
        {
            var leistenFunde = await LeistenDurchgang(true);
            Messwerte.Notiere($"Markierung 341 Leisten: {leisten.SeitenKennungenFuerTests.Count} Eintraege je {start} -> {andere} -> {start}, {leistenFunde} Abweichungen");
            funde += leistenFunde;
            var rund = await Rundgang();
            if (rund is null)
            {
                Messwerte.Notiere("  FEHLER: Markierung 341 Rundgang: Probe nicht moeglich");
                funde++;
            }
            else
            {
                rund.ForEach(f => Messwerte.Notiere("  FEHLER: Markierung 341 Rundgang " + f));
                Messwerte.Notiere("Markierung 341 Rundgang: " + (rund.Count == 0 ? "ok (AP1 ohne alten roten Rahmen, auch nach hell/dunkel)" : rund.Count + " Abweichungen"));
                funde += rund.Count;
            }

            // Gegenprobe: alter Weg muss auffallen
            Markierung.AltFuerTests = true;
            try
            {
                var alt = await LeistenDurchgang(false);
                var altRund = await Rundgang();
                var gefunden = alt > 0 || altRund is { Count: > 0 };
                Messwerte.Notiere($"Markierung 341 Gegenprobe alter Weg: Leisten {alt} Abweichungen, Rundgang {altRund?.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"} Abweichungen");
                if (!gefunden)
                {
                    Messwerte.Notiere("  FEHLER: Markierung 341 Gegenprobe: alter Weg (Farbe plus ClearValue) ohne Fund");
                    funde++;
                }
            }
            finally
            {
                Markierung.AltFuerTests = false;
                Markierung.AltAufraeumenFuerTests();
            }
            // nach der Gegenprobe ist wieder genau der aktuelle Eintrag markiert, auch nach hell/dunkel
            leisten.Markiere(vorher);
            foreach (var darstellung in new[] { andere, start })
            {
                Farben(darstellung);
                await Warten();
                if (Leiste(vorher) is { Count: > 0 } rest)
                {
                    Messwerte.Notiere("  FEHLER: Markierung 341 nach der Gegenprobe " + darstellung + ": " + string.Join("; ", rest));
                    funde++;
                }
            }
        }
        finally
        {
            Farben(start);
            leisten.Markiere(vorher);
            await ansicht.BildFertigAsync();
        }
        return funde;
    }
}
