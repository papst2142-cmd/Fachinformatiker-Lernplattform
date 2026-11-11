using System.Globalization;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Diagnose;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Spiel.Ton;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 13 Welle 5 „Etagen“ (Weisung P12-73, Plan P12-23b Abschnitt 5, Nico 1a 2b): Treppe und Fahrstuhl im Spiel. Vor
/// einem Stueck erscheint ein Knopf („Treppe hoch zum 1. Obergeschoss“, „Fahrstuhl“); der Wechsel baut die Ziel-Etage wie
/// das Betreten (aus dem Vorrat, sonst jetzt), die Figur steht an der Ankunftsstelle mit Blick vom Stueck weg. Jeder
/// Wechsel und das Verlassen eines Hauses mit Etagen schreiben die Ereignisart <see cref="Etagen.Aufenthalt"/> (nur
/// angehaengt ueber den Spielablauf), damit die Figur nach einem Neustart wieder auf der Etage steht.
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>fehler.log fuer die Zeile „Etage fehlt“ (null = der Datenordner; Tests geben einen eigenen Pfad).</summary>
    private readonly string? _fehlerLog;

    /// <summary>Text der letzten Zeile, die der Neustart in fehler.log schrieb (null = keine; Tests).</summary>
    public string? NeustartFehler { get; private set; }

    private void FehlerMerken(string text)
    {
        NeustartFehler = text;
        Fehlerprotokoll.Schreiben(text, _fehlerLog);
    }

    /// <summary>Etage des Innenraums (0 = Erdgeschoss oder draussen).</summary>
    public int Etage => Innen is { } innen ? Etagen.Teilen(innen.Ansicht).Etage : 0;

    /// <summary>Kennung der Stadt fuer den Aufenthalt (Fassung, sonst „stadt1“ wie die Dateien).</summary>
    public string StadtKennung => Stadt.Fassung?.StadtId ?? "stadt1";

    /// <summary>Grundriss des Erdgeschosses des jetzigen Innenraums, wenn er Etagen hat (sonst null).</summary>
    private PyObjekt? EtagenGrundriss(string? ort) =>
        ort is not null && Innenraeume is not null && SpielDaten.Get(Innenraeume, ort) is PyObjekt laden
            && SpielDaten.Get(laden, "gebaeude") is PyObjekt eg && Etagen.Hat(eg) ? eg : null;

    /// <summary>Ansicht ohne Etage („geschaeft:moebelhaus@1“ → „geschaeft:moebelhaus“).</summary>
    private static string Basis(string ansicht) => Etagen.Teilen(ansicht).Ort;

    /// <summary>Wie oft ein Etagenwechsel den vorbereiteten Innenraum genommen hat (Start-Test, Tests).</summary>
    public int EtageVorbereitetGenommen { get; private set; }

    /// <summary>
    /// Nachbar-Etagen im Hintergrund vorbereiten (Vorgabe an). Nur der Start-Test schaltet es fuer die erste, einmalige
    /// Messung „Etage wechseln“ aus (Bauen ohne Vorrat, der schlechteste Fall).
    /// </summary>
    public bool EtagenVorrat { get; set; } = true;

    /// <summary>Knoepfe vor einem Uebergangs-Stueck (an die Knoepfe des Orts angehaengt).</summary>
    private List<(string Aktion, string Name)> EtagenKnoepfe(List<(string Aktion, string Name)> knoepfe)
    {
        if (Innen is not { } innen || Einrichten || EtagenGrundriss(innen.Ort) is not { } eg
            || Etagen.Nahe(eg, innen.Ort, Etage, FigurX, FigurZ) is not { } nahe)
        {
            return knoepfe;
        }
        var (u, _) = nahe;
        var name = Etagen.KnopfText(eg, u, Etage);
        if (knoepfe.Count == 0)
        {
            Zeile = Etagen.Name(eg, Etage) + "\n" + name;
        }
        var aktion = u.Art == Etagen.Fahrstuhl
            ? Etagen.AktionFahrstuhl + u.Id
            : Etagen.AktionEtage + u.Id + ":" + u.Ziele(Etage).FirstOrDefault().ToString(CultureInfo.InvariantCulture);
        return [.. knoepfe, (aktion, name)];
    }

    /// <summary>
    /// Knoepfe ohne 3D (unter der Ortsliste): alle Uebergaenge, die auf der jetzigen Etage halten (ohne 3D geht die Figur
    /// nicht hin, wie <see cref="Spielwelt.ErsatzKnoepfe"/>).
    /// </summary>
    public List<(string Aktion, string Name)> EtagenErsatzKnoepfe()
    {
        if (Innen is not { } innen || EtagenGrundriss(innen.Ort) is not { } eg)
        {
            return [];
        }
        var etage = Etage;
        return [.. Etagen.Uebergaenge(eg, innen.Ort).Where(u => Etagen.Arten.Contains(u.Art) && u.Auf(etage) is not null && u.Ziele(etage).Count > 0)
            .Select(u => (u.Art == Etagen.Fahrstuhl ? Etagen.AktionFahrstuhl + u.Id
                : Etagen.AktionEtage + u.Id + ":" + u.Ziele(etage)[0].ToString(CultureInfo.InvariantCulture), Etagen.KnopfText(eg, u, etage)))];
    }

    /// <summary>
    /// Aktion eines Etagen-Knopfs ausfuehren: Treppe („etage:id:ziel“) wechselt sofort, Fahrstuhl („fahrstuhl:id“) geht an
    /// die Oberflaeche (<see cref="Aktion"/>, Fenster mit der Liste der Etagen). false = keine Etagen-Aktion.
    /// </summary>
    public bool EtagenAktion(string aktion)
    {
        if (aktion.StartsWith(Etagen.AktionFahrstuhl, StringComparison.Ordinal))
        {
            Aktion?.Invoke(aktion);
            return true;
        }
        if (!aktion.StartsWith(Etagen.AktionEtage, StringComparison.Ordinal))
        {
            return false;
        }
        var rest = aktion[Etagen.AktionEtage.Length..];
        var i = rest.LastIndexOf(':');
        if (i > 0 && int.TryParse(rest.AsSpan(i + 1), NumberStyles.None, CultureInfo.InvariantCulture, out var ziel))
        {
            EtageWechseln(rest[..i], ziel);
        }
        return true;
    }

    /// <summary>Ziele des Fahrstuhls <paramref name="id"/> von der jetzigen Etage aus: (Etage, Name), aufsteigend.</summary>
    public List<(int Etage, string Name)> FahrstuhlZiele(string id)
    {
        if (Innen is not { } innen || EtagenGrundriss(innen.Ort) is not { } eg || Etagen.Von(eg, innen.Ort, id) is not { } u)
        {
            return [];
        }
        return [.. u.Ziele(Etage).Select(k => (k, Etagen.Name(eg, k)))];
    }

    /// <summary>
    /// Ueber den Uebergang <paramref name="id"/> auf Etage <paramref name="ziel"/> wechseln (gemessen wie das Betreten).
    /// Schreibt <see cref="Etagen.Aufenthalt"/>; laeuft ein anderer Vorgang, steht AbgleichTexte.Belegt in der Zeile und
    /// die Figur bleibt.
    /// </summary>
    public bool EtageWechseln(string id, int ziel) => Gemessen(() => EtageWechselnOhneMessung(id, ziel));

    private bool EtageWechselnOhneMessung(string id, int ziel)
    {
        if (Innen is not { } innen || Einrichten || EtagenGrundriss(innen.Ort) is not { } eg || Etagen.Von(eg, innen.Ort, id) is not { } u
            || !Etagen.Arten.Contains(u.Art) || u.Ziel(Etage, ziel) is not { } halt || !Etagen.Gibt(eg, ziel))
        {
            return false;
        }
        var ansicht = Etagen.Schluessel(Basis(innen.Ansicht), ziel);
        var neu = Vorbereitet(ansicht, innen.Fest);
        if (neu is not null)
        {
            EtageVorbereitetGenommen++;
        }
        else if (Probe.Innenraum.Bauen(ansicht, Ablauf.Stand, Innenraeume, Grafik, GeschaefteDaten, innen.Fest, Feste, innen.Jahreszeit) is { } gebaut)
        {
            neu = gebaut;
            InDenVorrat(gebaut);
        }
        if (neu is null)
        {
            return false;
        }
        try
        {
            MitEreignis(() => Ablauf.Ausfuehren([(Etagen.Aufenthalt, Etagen.AufenthaltDaten(Ablauf.Stand.Tag, StadtKennung, Jetzt.Ort, ziel, id))]));
        }
        catch (SpielBelegtAusnahme)
        {
            Zeile = AbgleichTexte.Belegt;
            Geaendert?.Invoke();
            return false;
        }
        catch (SpielFehler e)
        {
            Zeile = e.Message;
            Geaendert?.Invoke();
            return false;
        }
        Ankommen(neu, halt);
        return true;
    }

    /// <summary>Figur auf der neuen Etage an die Ankunftsstelle setzen, Text, Ton, Nachbar-Etagen vorbereiten.</summary>
    private void Ankommen(Innenraum neu, Halt halt)
    {
        _weg.Clear();
        Innen = neu;
        FigurX = (float)halt.X;
        FigurZ = (float)halt.Y;
        FigurRichtung = halt.Blick;
        InnenText();
        Ton?.Invoke(new Tonanlass(Anlass.TuerAuf));
        InnenGeaendert?.Invoke();
        Geaendert?.Invoke();
        EtagenVorbereiten();
    }

    /// <summary>
    /// Die Etagen, die man von hier aus erreicht, im Hintergrund vorbereiten (Plan Abschnitt 5: „benachbarte Etagen“;
    /// ein Fahrstuhl erreicht alle, auf denen er haelt).
    /// </summary>
    public void EtagenVorbereiten()
    {
        if (!EtagenVorrat || Innen is not { } innen || EtagenGrundriss(innen.Ort) is not { } eg)
        {
            return;
        }
        var etage = Etage;
        foreach (var k in Etagen.Uebergaenge(eg, innen.Ort).Where(u => Etagen.Arten.Contains(u.Art)).SelectMany(u => u.Ziele(etage)).Distinct())
        {
            VorbereitenAnsicht(Etagen.Schluessel(Basis(innen.Ansicht), k), innen.Fest);
        }
    }

    /// <summary>Den Innenraum zur Ansicht im Hintergrund bauen (wie <see cref="Vorbereiten"/>, ohne Ort der Stadt).</summary>
    private void VorbereitenAnsicht(string ansicht, string? fest)
    {
        if (InnenSignatur(ansicht, fest) is not { } signatur || (_vorrat.TryGetValue(ansicht, out var da) && da.Signatur == signatur))
        {
            return;
        }
        var kopie = Ablauf.Stand.Kopie();
        var (innenraeume, grafik, geschaefte, feste, jahreszeit) = (Innenraeume, Grafik, GeschaefteDaten, Feste, Szene.Jahreszeit);
        var aufgabe = Task.Run(() =>
        {
            var innen = Probe.Innenraum.Bauen(ansicht, kopie, innenraeume, grafik, geschaefte, fest, feste, jahreszeit);
            innen?.Vorbereiten();
            return innen;
        });
        lock (_vorrat)
        {
            _vorrat[ansicht] = (signatur, aufgabe);
        }
    }

    /// <summary>
    /// Beim Verlassen eines Hauses mit Etagen: steht im Verlauf zuletzt ein Aufenthalt im Haus, „draussen“ schreiben
    /// (Etage null), damit ein Neustart die Figur nicht wieder hineinsetzt. Scheitert das Schreiben (anderer Vorgang),
    /// bleibt es beim Verlassen (der naechste Weg in der Stadt zaehlt ohnehin als juenger).
    /// </summary>
    private void AufenthaltDraussen()
    {
        if (Innen is not { } innen || EtagenGrundriss(innen.Ort) is null || EtageZuletzt(Ablauf.Stand, StadtKennung) is null)
        {
            return;
        }
        try
        {
            MitEreignis(() => Ablauf.Ausfuehren([(Etagen.Aufenthalt, Etagen.AufenthaltDaten(Ablauf.Stand.Tag, StadtKennung, Jetzt.Ort, null, null))]));
        }
        catch (Exception e) when (e is SpielBelegtAusnahme or SpielFehler)
        {
            // nichts: die Figur geht trotzdem hinaus
        }
    }

    /// <summary>
    /// Nico 2b: stand die Figur zuletzt (heute, in dieser Stadt, am Ort der Stadt-Uhr) auf einer Etage, steht sie nach dem
    /// Neustart wieder dort an der Ankunftsstelle des Uebergangs. Fehlt die Etage (neue Fassung), steht sie vor dem Haus
    /// und fehler.log bekommt eine Zeile (kein Absturz). Alte Spielstaende ohne Aufenthalt bleiben wie bisher draussen.
    /// </summary>
    private void EtageNachNeustart()
    {
        if (EtageZuletzt(Ablauf.Stand, StadtKennung) is not { } zuletzt || zuletzt.Ort != Jetzt.Ort
            || Szene.Ort(Jetzt.Ort)?.Ansicht is not { } ansicht || Probe.Innenraum.OrtFuer(ansicht) is not { } ort)
        {
            return;
        }
        var eg = EtagenGrundriss(ort);
        if (eg is null || !Etagen.Gibt(eg, zuletzt.Etage))
        {
            FehlerMerken(string.Create(CultureInfo.InvariantCulture,
                $"Spiel: die Etage {zuletzt.Etage} in {ort} gibt es nicht mehr; die Figur steht vor dem Haus."));
            return;
        }
        if (!BetretenOhneMessung())
        {
            return;
        }
        if (zuletzt.Etage == 0 && zuletzt.Uebergang is null)
        {
            return;
        }
        var ziel = Etagen.Schluessel(Basis(ansicht), zuletzt.Etage);
        var u = zuletzt.Uebergang is { } id ? Etagen.Von(eg, ort, id) : null;
        var halt = u?.Auf(zuletzt.Etage) ?? Etagen.Uebergaenge(eg, ort).Select(x => x.Auf(zuletzt.Etage)).FirstOrDefault(h => h is not null);
        if (zuletzt.Etage == 0)
        {
            if (halt is not null)
            {
                FigurX = (float)halt.X;
                FigurZ = (float)halt.Y;
                FigurRichtung = halt.Blick;
                InnenText();
            }
            return;
        }
        var innen = Innen!;
        if (halt is null || Probe.Innenraum.Bauen(ziel, Ablauf.Stand, Innenraeume, Grafik, GeschaefteDaten, innen.Fest, Feste, innen.Jahreszeit) is not { } neu)
        {
            FehlerMerken(string.Create(CultureInfo.InvariantCulture,
                $"Spiel: die Etage {zuletzt.Etage} in {ort} laesst sich nicht betreten; die Figur steht im Erdgeschoss."));
            return;
        }
        InDenVorrat(neu);
        _weg.Clear();
        Innen = neu;
        FigurX = (float)halt.X;
        FigurZ = (float)halt.Y;
        FigurRichtung = halt.Blick;
        InnenText();
        EtagenVorbereiten();
    }

    /// <summary>
    /// Wo stand die Figur zuletzt (Nico 2b)? Der letzte Eintrag des heutigen Tags unter den Stadt-Schritten und
    /// <see cref="Etagen.Aufenthalt"/> zaehlt: ist er ein Aufenthalt mit Etage in dieser Stadt am Ort, an dem die Stadt-Uhr die Figur
    /// sieht, steht sie dort; sonst (alte Spielstaende, nach dem Verlassen, nach einem Weg, an einem neuen Tag) wie bisher
    /// vor dem Haus (null). Weisung P12-179: aus <c>Etagen</c> hierher (liest den Verlauf des Spielstands, Spielanbindung).
    /// </summary>
    internal static (string Ort, int Etage, string? Uebergang)? EtageZuletzt(Spielstand stand, string stadtId)
    {
        SpielEreignis? letzter = null;
        foreach (var e in stand.Verlauf)
        {
            if (e.Art is not (Etagen.Aufenthalt or Stadt.Gegangen or Stadt.Gelaufen or Stadt.Gewartet))
            {
                continue;
            }
            if (Py.IstNurInt(SpielDaten.Get(e.Daten, "tag")) && Py.Int(e.Daten["tag"]) == stand.Tag)
            {
                letzter = e;
            }
        }
        if (letzter is not { Art: Etagen.Aufenthalt } a || SpielDaten.Get(a.Daten, "stadt") as string != stadtId || SpielDaten.Get(a.Daten, "ort") is not string ort
            || !Py.IstNurInt(SpielDaten.Get(a.Daten, "etage")) || Py.Int(a.Daten["etage"]) < 0 || Py.Int(a.Daten["etage"]) > Etagen.Hoechstens)
        {
            return null;
        }
        return (ort, (int)Py.Int(a.Daten["etage"]), SpielDaten.Get(a.Daten, "uebergang") as string);
    }
}
