using System.Globalization;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Phase 7, W4 (Weisung P7-9): Ablauf der 3D-Stadt ohne Oberflaeche, auf dem gemergten Stadt-Kern
/// (<see cref="Spiel.Stadt"/>: Raster, Wege von Tuer zu Tuer, Tagesuhr, Oeffnungszeiten) und einem
/// <see cref="Spielablauf"/>. Ein Weg wird sofort als Ereignis <c>stadt_gegangen</c> geschrieben
/// (die Uhr springt), die Figur geht ihn danach sichtbar Zelle fuer Zelle ab. Innenraum ist bis W5
/// nur das Buero der Bitweiche (aus W1, freies Gehen ohne Spielzeit). Die Darstellung ruft
/// <see cref="Fortschreiten"/> je Bild.
/// </summary>
public sealed class StadtSpiel
{
    /// <summary>Gehgeschwindigkeit in Karteneinheiten je Sekunde (fisi_game_gui 7.0 / 2).</summary>
    public const float Tempo = 3.5f;

    /// <summary>Orte mit Innenraum (bis W5 nur das Buero der Bitweiche).</summary>
    public static readonly IReadOnlyList<string> MitInnenraum = ["bitweiche"];

    private readonly List<(double X, double Y)> _weg = [];
    private string? _zielOrt;

    public StadtSpiel(Stadt stadt, Spielablauf ablauf, ProbeFlaeche buero)
    {
        Stadt = stadt;
        Ablauf = ablauf;
        Buero = buero;
        Szene = StadtSzene.Bauen(stadt, ablauf.Stand);
        var hier = Szene.Ort(Jetzt.Ort);
        FigurX = hier?.TuerX ?? 0;
        FigurZ = hier?.TuerZ ?? 0;
        // Beim Oeffnen erklaert die Zeile die Bedienung (Weisung P7-9 Punkt 1); ab dem ersten Schritt Ort und Uhr.
        Zeile = SpielProbeTexte.Hinweis;
    }

    public Stadt Stadt { get; }

    public Spielablauf Ablauf { get; }

    public ProbeFlaeche Buero { get; }

    public StadtSzene Szene { get; private set; }

    public bool ImInnenraum { get; private set; }

    /// <summary>"stadt" oder "buero" (welche Flaeche die Darstellung zeigt).</summary>
    public string FlaecheId => ImInnenraum ? ProbeWelt.Buero : ProbeWelt.Stadt;

    public float FigurX { get; private set; }

    public float FigurZ { get; private set; }

    /// <summary>Blickrichtung der Figur in Grad um die Hochachse (0 = nach +Z).</summary>
    public float FigurRichtung { get; private set; }

    public string Zeile { get; private set; }

    public bool Geht => _weg.Count > 0;

    public (double X, double Y)? Ziel => _weg.Count > 0 ? _weg[^1] : null;

    /// <summary>Stand der Stadt aus dem Ereignisprotokoll (Tag, Ort, Uhr).</summary>
    public StadtStand Jetzt => Stadt.Stand(Ablauf.Stand);

    public string Kopfzeile
    {
        get
        {
            var jetzt = Jetzt;
            return string.Format(CultureInfo.InvariantCulture, StadtTexte.Kopfzeile, jetzt.Tag, Stadt.Uhrzeit(jetzt.Uhr));
        }
    }

    /// <summary>Wird bei jeder Aenderung von Ort, Uhr oder Zeile gerufen (Oberflaeche neu beschriften).</summary>
    public event Action? Geaendert;

    /// <summary>Wird gerufen, wenn sich die sichtbaren Orte oder Modellteile geaendert haben (Szene neu laden).</summary>
    public event Action? SzeneGeaendert;

    public string Name(string id) => Szene.Ort(id)?.Name ?? id;

    /// <summary>Zum Ort gehen: Ereignis schreiben (Uhr springt), dann den Weg sichtbar abgehen.</summary>
    public StadtGrund GeheZu(string ziel)
    {
        if (ImInnenraum)
        {
            Verlassen();
        }
        var von = Jetzt.Ort;
        if (Geht)
        {
            // laufender Weg: die Figur steht schon am Ziel im Protokoll, Sprung dorthin
            SofortAnkommen();
            von = Jetzt.Ort;
        }
        var ergebnis = Stadt.Gehen(Ablauf, ziel);
        if (!ergebnis.Ok)
        {
            Zeile = StadtTexte.Grund(ergebnis.Grund);
            Geaendert?.Invoke();
            return ergebnis.Grund;
        }
        var zellen = Stadt.Weg(von, ziel)!.Zellen;
        _weg.Clear();
        _weg.AddRange(zellen.Skip(1).Select(z => ((z.X + 0.5) * Stadt.Zelle, (z.Y + 0.5) * Stadt.Zelle)));
        _zielOrt = ziel;
        var daten = ergebnis.Daten!;
        Zeile = string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.Unterwegs, Name(ziel)) + " "
            + string.Format(CultureInfo.InvariantCulture, StadtTexte.Wegdauer, daten["schritte"], daten["minuten"]);
        if (_weg.Count == 0)
        {
            Ankommen();
        }
        Geaendert?.Invoke();
        return StadtGrund.Ok;
    }

    /// <summary>Im Innenraum: zum Bodenpunkt gehen (freie Wegsuche, ohne Spielzeit). In der Stadt nur von Ort zu Ort.</summary>
    public bool GeheZu(double x, double z)
    {
        if (!ImInnenraum)
        {
            return false;
        }
        var weg = Buero.Wege.Weg((FigurX, FigurZ), (x, z));
        _weg.Clear();
        _weg.AddRange(weg);
        Zeile = weg.Count == 0 ? SpielProbeTexte.NichtErreichbar : SpielProbeTexte.ImBuero;
        Geaendert?.Invoke();
        return weg.Count > 0;
    }

    /// <summary>Figur um <paramref name="sekunden"/> weiterbewegen; true, solange sie geht.</summary>
    public bool Fortschreiten(double sekunden)
    {
        if (_weg.Count == 0)
        {
            return false;
        }
        var rest = sekunden * Tempo;
        while (rest > 0 && _weg.Count > 0)
        {
            var (zx, zz) = _weg[0];
            var dx = zx - FigurX;
            var dz = zz - FigurZ;
            var weite = Math.Sqrt((dx * dx) + (dz * dz));
            if (weite > 1e-6)
            {
                FigurRichtung = (float)(Math.Atan2(dx, dz) * 180 / Math.PI);
            }
            if (weite <= rest)
            {
                FigurX = (float)zx;
                FigurZ = (float)zz;
                rest -= weite;
                _weg.RemoveAt(0);
            }
            else
            {
                FigurX += (float)(dx / weite * rest);
                FigurZ += (float)(dz / weite * rest);
                rest = 0;
            }
        }
        if (_weg.Count == 0)
        {
            Ankommen();
        }
        return _weg.Count > 0;
    }

    /// <summary>Sofort ans Ziel (ohne 3D und im Start-Test).</summary>
    public void SofortAnkommen()
    {
        while (Fortschreiten(1000))
        {
        }
    }

    private void Ankommen()
    {
        if (ImInnenraum)
        {
            Zeile = SpielProbeTexte.ImBuero;
        }
        else if (_zielOrt is { } ort)
        {
            Zeile = OrtZeile(ort);
            _zielOrt = null;
            NeueSzene();
        }
        Geaendert?.Invoke();
    }

    /// <summary>Zeile vor einem Ort: offen bis, immer offen, geschlossen (oeffnet um / heute nicht mehr).</summary>
    public string OrtZeile(string id)
    {
        var inv = CultureInfo.InvariantCulture;
        var name = Name(id);
        var uhr = Jetzt.Uhr;
        if (Stadt.Oeffnungszeit(id) is not { } zeit)
        {
            return string.Format(inv, SpielProbeTexte.ImmerOffen, name) + Innenraum(id);
        }
        if (Stadt.Offen(id, uhr))
        {
            return string.Format(inv, SpielProbeTexte.Offen, name, Stadt.Uhrzeit(zeit.Zu)) + Innenraum(id);
        }
        var oeffnet = zeit.Auf > uhr && zeit.Auf <= Stadt.Abend;
        return name + ": " + (oeffnet ? StadtTexte.Grund(StadtGrund.Geschlossen, Stadt.Uhrzeit(zeit.Auf)) : StadtTexte.OeffnetNichtMehr);
    }

    private string Innenraum(string id) =>
        MitInnenraum.Contains(id) ? "" : " " + string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.KeinInnenraum, Name(id));

    public bool KannWarten => !ImInnenraum && !Geht && Stadt.WartenPruefen(Ablauf.Stand).Ok;

    public bool KannBetreten => !ImInnenraum && !Geht && MitInnenraum.Contains(Jetzt.Ort) && Stadt.Betreten(Ablauf.Stand) == StadtGrund.Ok;

    /// <summary>Vor dem geschlossenen Ort bis zur Oeffnung warten (Ereignis stadt_gewartet).</summary>
    public StadtGrund Warten()
    {
        var ergebnis = Stadt.Warten(Ablauf);
        Zeile = ergebnis.Ok ? OrtZeile(Jetzt.Ort) : StadtTexte.Grund(ergebnis.Grund);
        Geaendert?.Invoke();
        return ergebnis.Grund;
    }

    public bool Betreten()
    {
        if (!KannBetreten)
        {
            return false;
        }
        _weg.Clear();
        ImInnenraum = true;
        FigurX = Buero.StartX;
        FigurZ = Buero.StartZ;
        FigurRichtung = 90;
        Zeile = SpielProbeTexte.ImBuero;
        Geaendert?.Invoke();
        return true;
    }

    public void Verlassen()
    {
        if (!ImInnenraum)
        {
            return;
        }
        _weg.Clear();
        ImInnenraum = false;
        var hier = Szene.Ort(Jetzt.Ort);
        FigurX = hier?.TuerX ?? 0;
        FigurZ = hier?.TuerZ ?? 0;
        FigurRichtung = 0;
        Zeile = OrtZeile(Jetzt.Ort);
        Geaendert?.Invoke();
    }

    /// <summary>Szene neu bauen, wenn sich sichtbare Orte oder Modellteile geaendert haben.</summary>
    private void NeueSzene()
    {
        var neu = StadtSzene.Bauen(Stadt, Ablauf.Stand);
        if (neu.Signatur != Szene.Signatur)
        {
            Szene = neu;
            SzeneGeaendert?.Invoke();
        }
    }
}
