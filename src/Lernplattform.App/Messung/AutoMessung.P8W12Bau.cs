using System.Diagnostics;
using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Spiel;
using Lernplattform.Core.Spiel.Probe;
using Lernplattform.Spiel;
using Lernplattform.Views;
using static Lernplattform.Core.Spiel.SpielDaten;

namespace Lernplattform.Messung;

/// <summary>
/// Phase 8 W12 (Weisung P8-55, Teilplan P8-51 Abschnitt 6): Vorladen beim Oeffnen der Spielseite. Messpunkte
/// "Spiel vorladen bis fertig" (nur Info, ohne Grenze) und "1. Betreten nach Vorladen" (einmalig, streng 400 ms,
/// der vorgeladene Innenraum muss genommen werden); Wiederkehr auf die Seite laedt nur neu hoch (wie Android nach dem
/// Hintergrund); Fest- und Jahreszeitwechsel im Haus: die Szene ist beim Hinausgehen schon gebaut; Gesamtansicht mit
/// allen Orten (Weihnachten, Winter, nachts) in den Grenzen 160 000 Dreiecke und 40 Zeichenaufrufe (Sammelliste 143).
/// </summary>
public static partial class AutoMessung
{
    /// <summary>So lange darf ein Vorladen hoechstens dauern, sonst haengt die Seite (Fund).</summary>
    private static readonly TimeSpan VorladenWarten = TimeSpan.FromSeconds(60);

    /// <summary>Wartet, bis die Flaeche fertig vorgeladen hat; false = nach <see cref="VorladenWarten"/> noch nicht.</summary>
    private static async Task<bool> VorladenAbwartenAsync(MainView ansicht, SpielSeite seite)
    {
        var uhr = Stopwatch.StartNew();
        while (seite.Flaeche.LaedtVor && uhr.Elapsed < VorladenWarten)
        {
            await ansicht.BildFertigAsync();
            await Task.Delay(10);
        }
        return !seite.Flaeche.LaedtVor;
    }

    /// <summary>
    /// Direkt nach dem Oeffnen der Seite (aus <see cref="P7W1MesspunkteAsync"/>): Vorladen bis fertig (Info) und das
    /// erste Betreten danach (streng 400 ms, aus dem Vorrat).
    /// </summary>
    private static async Task<int> P8W12VorladenAsync(MainView ansicht, SpielSeite seite)
    {
        var inv = CultureInfo.InvariantCulture;
        var funde = 0;
        void Fund(string text)
        {
            Messwerte.Notiere("  FEHLER: Spiel " + text);
            funde++;
        }
        if (!await VorladenAbwartenAsync(ansicht, seite))
        {
            Fund("Vorladen: nach 60 s nicht fertig (Ladeanzeige bleibt stehen)");
            return funde;
        }
        Messwerte.Notiere(string.Create(inv, $"Spiel vorladen bis fertig (nur Info): {seite.Flaeche.VorladenMs ?? -1:F0} ms, Ladeanzeige jetzt {(seite.LadeanzeigeSichtbar ? "SICHTBAR" : "weg")}"));
        if (seite.LadeanzeigeSichtbar)
        {
            Fund("Ladeanzeige: nach dem Vorladen noch sichtbar");
        }

        // 1. Betreten nach Vorladen: ein frisches Spiel vor der Wohnung, neu vorgeladen wie beim Oeffnen
        var spiel = Frisch(seite);
        seite.Flaeche.Vorladen();
        if (!await VorladenAbwartenAsync(ansicht, seite))
        {
            Fund("Vorladen (frisches Spiel): nach 60 s nicht fertig");
            return funde;
        }
        await seite.Flaeche.NaechstesBildAsync();
        if (!spiel.KannBetreten)
        {
            Fund("1. Betreten nach Vorladen: Wohnung nicht betretbar (" + spiel.Zeile + ")");
            return funde;
        }
        var genommen = spiel.VorbereitetGenommen;
        var uhr = Stopwatch.StartNew();
        spiel.Betreten();
        await seite.Flaeche.NaechstesBildAsync();
        await ansicht.BildFertigAsync();
        var ms = uhr.Elapsed.TotalMilliseconds;
        funde += Ergebnis("Spiel 1. Betreten nach Vorladen", Messwiederholung.Einmal(ms, Geraetegrenze));
        var aus = spiel.VorbereitetGenommen == genommen + 1 && spiel.FlaecheId == "zuhause";
        Messwerte.Notiere("Spiel: 1. Betreten nach Vorladen aus dem Vorrat: " + (aus ? "ja" : "NEIN") + " (Flaeche " + spiel.FlaecheId + ")");
        if (!aus)
        {
            Fund("1. Betreten nach Vorladen: vorgeladener Innenraum nicht genommen");
        }
        spiel.Verlassen();
        await seite.Flaeche.NaechstesBildAsync();
        return funde;
    }

    /// <summary>Wiederkehr, Fest im Haus, Gesamtansicht mit allen Orten (am Ende des Spiel-Teils).</summary>
    private static async Task<int> P8W12SpielAsync(MainView ansicht)
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
            Messwerte.Notiere("  FEHLER: Spiel W12: Spielseite nicht gebaut");
            return 1;
        }
        if (!await GlAbwartenAsync(seite))
        {
            Messwerte.Notiere("Spiel: Vorladen ohne GL nicht gemessen");
            return funde;
        }
        var vorher = seite.Spiel;
        try
        {
            // Wiederkehr auf die Seite (OpenGL richtet sich neu ein wie unter Android nach dem Hintergrund): nur neu hochladen
            await VorladenAbwartenAsync(ansicht, seite);
            var zahl = seite.Flaeche.VorladenZahl;
            await ansicht.ZeigeAsync(Seiten.Dashboard);
            await ansicht.BildFertigAsync();
            await ansicht.ZeigeAsync(Seiten.Spiel);
            for (var i = 0; i < 100 && seite.Flaeche.VorladenZahl == zahl; i++)
            {
                await ansicht.BildFertigAsync();
                await Task.Delay(20);
            }
            var fertig = await VorladenAbwartenAsync(ansicht, seite);
            Pruefe("Wiederkehr laedt neu hoch", fertig && seite.Flaeche.VorladenZahl == zahl + 1,
                string.Create(inv, $"{seite.Flaeche.VorladenZahl - zahl} Mal vorgeladen, {seite.Flaeche.VorladenMs ?? -1:F0} ms (nur Info)"));

            // Fest und Jahreszeit wechseln, waehrend die Figur im Haus ist: die Szene ist beim Hinausgehen schon gebaut
            Jahreszeiten.Probe(Jahreszeit.Sommer);
            Festzeit.Probe(null);
            var spiel = Frisch(seite);
            await seite.Flaeche.NaechstesBildAsync();
            if (!spiel.Betreten())
            {
                Pruefe("Fest im Haus", false, "Wohnung nicht betretbar (" + spiel.Zeile + ")");
            }
            else
            {
                await seite.Flaeche.NaechstesBildAsync();
                Jahreszeiten.Probe(Jahreszeit.Winter);
                Festzeit.Probe(Festzeit.Weihnachten);
                // wie der Takt der Seite (alle 0,5 s im Haus)
                spiel.SzeneVorbereiten();
                seite.Flaeche.Vorausbauen();
                var bis = Stopwatch.StartNew();
                while (spiel.SzeneDraussen == spiel.Szene && bis.Elapsed < TimeSpan.FromSeconds(30))
                {
                    await Task.Delay(20);
                }
                var uhr = Stopwatch.StartNew();
                spiel.Verlassen();
                await seite.Flaeche.NaechstesBildAsync();
                await ansicht.BildFertigAsync();
                var ms = uhr.Elapsed.TotalMilliseconds;
                Pruefe("Fest im Haus vorbereitet", spiel.SzeneVorbereitetGenommen == 1 && spiel.Szene.Fest == Festzeit.Weihnachten && spiel.Szene.Jahreszeit == Jahreszeit.Winter,
                    string.Create(inv, $"genommen {spiel.SzeneVorbereitetGenommen}, Fest {spiel.Szene.Fest ?? "keins"}, {spiel.Szene.Jahreszeit}, Hinausgehen {ms:F0} ms (nur Info)"));
            }

            // Sammelliste 143: Gesamtansicht mit allen Orten nachts im Winter mit Weihnachten
            Tageslicht.FesteUhr = 1260;
            var alle = SpielMitAllenOrten(seite.Spiel!);
            seite.SpielSetzen(alle);
            await seite.Flaeche.NaechstesBildAsync();
            var regeln = alle.Stadt.Regeln.Count;
            Pruefe("Gesamtansicht mit allen Orten: Orte sichtbar", alle.Szene.Orte.Count == regeln,
                string.Create(inv, $"{alle.Szene.Orte.Count} von {regeln} ({string.Join(", ", alle.Szene.Orte.Select(o => o.Id))})"));
            Messwerte.Notiere("Spiel: Gesamtansicht mit allen Orten (Weihnachten, Winter, nachts)");
            funde += await GesamtansichtAsync(seite);
        }
        finally
        {
            Tageslicht.FesteUhr = null;
            Jahreszeiten.ProbeEnde();
            Festzeit.ProbeEnde();
            seite.SpielSetzen(vorher);
            seite.Flaeche.KameraAufFigur();
            await seite.Flaeche.NaechstesBildAsync();
        }
        return funde;
    }

    /// <summary>
    /// Sammelliste 143: ein Spiel im Arbeitsspeicher, in dem alle Orte der Stadt zu sehen sind (Firma auf Stufe 4 mit
    /// Serverraum, Filiale, Serverfarm frei, alle Kundenorte). Weg wie <c>SpielTafel.ProbeFirmaAnlegen</c>, dazu Arbeitstage
    /// bis Filiale und Serverfarm moeglich sind; nichts wird gespeichert.
    /// </summary>
    private static StadtSpiel SpielMitAllenOrten(StadtSpiel vorlage)
    {
        var inhalt = vorlage.Stadt.Inhalt;
        var a = new Spielablauf(inhalt);
        a.Profil("Nico", new PyObjekt
        {
            { "haut", "hell" }, { "frisur", "kurz" }, { "haarfarbe", "braun" }, { "oberteil", "cyan" }, { "kreis", "cyan" },
        });
        var aufgaben = Liste(inhalt["aufgaben"]).Select(Objekt).ToList();
        for (var i = 0; i < aufgaben.Count; i++)
        {
            var reputation = new PyObjekt();
            foreach (var achse in Ereignisse.Achsen)
            {
                reputation.Add(achse, 1L);
            }
            a.Log(Ereignisse.Erledigt, new PyObjekt
            {
                { "aufgabe", aufgaben[i]["id"] }, { "tag", (long)Math.Min(113, 1 + (i / 4)) }, { "richtig", true }, { "geld", 60L }, { "reputation", reputation },
            });
        }
        for (long tag = 1; tag < 115; tag++)
        {
            a.Log(Ereignisse.Feierabend, new PyObjekt { { "tag", tag }, { "gehalt", tag > 60 ? 250L : 140L }, { "rang", "Senior" } });
        }
        a.Log(Ereignisse.Erledigt, new PyObjekt { { "aufgabe", "probe-erspartes" }, { "tag", 114L }, { "richtig", false }, { "geld", 4000000L } });
        a.Neuaufbau();
        a.Gruenden("Nico IT-Service");
        for (var i = 0; i < 3; i++)
        {
            a.Ausbauen();
        }
        a.RaumBauen("serverraum");
        var zertifikate = ListeOderLeer(Objekt(Betrieb.FarmRegeln(inhalt)["freischaltung"]), "zertifizierungen");
        a.PersonalEreignisse = (_, _, _) => [];
        for (var tag = 0; tag < 120 && (!Betrieb.FilialeMoeglich(a.Stand, inhalt) || a.Stand.FarmFrei is null); tag++)
        {
            foreach (var bewerbung in Betrieb.Bewerbungen(a.Stand, inhalt))
            {
                if (a.Stand.Personal.Count < a.Stand.Kapazitaet)
                {
                    a.Einstellen(bewerbung["id"]);
                }
            }
            foreach (var entscheidung in a.Stand.OffeneEntscheidungen())
            {
                a.Entscheiden(entscheidung["id"], Objekt(Liste(entscheidung["optionen"])[0])["id"]);
            }
            // je Tag hoechstens eine Zertifizierung (die naechste fehlende); laeuft schon eine, geht es weiter
            if (zertifikate.FirstOrDefault(z => !a.Stand.ZertifikateFertig().Contains(z)) is { } naechste)
            {
                try
                {
                    a.ZertifizierungStarten(naechste);
                }
                catch (SpielFehler)
                {
                }
            }
            a.Feierabend();
        }
        if (Betrieb.FilialeMoeglich(a.Stand, inhalt) && !Py.Wahr(a.Stand.Zweigstelle))
        {
            a.FilialeEroeffnen("Filiale");
        }
        a.Neuaufbau();
        return new StadtSpiel(vorlage.Stadt, a, vorlage.Grafik, vorlage.Innenraeume, vorlage.GeschaefteDaten, vorlage.Feste, Festzeit.Heute, Jahreszeiten.Heute);
    }
}
