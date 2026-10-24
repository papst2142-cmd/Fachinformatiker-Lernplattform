using System.Globalization;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Ablauf der 3D-Stichprobe ohne Oberflaeche: Ort (Stadt oder Buero), Figur, Weg, Probe-Uhr
/// und Hinweiszeile. Die Darstellung ruft <see cref="Fortschreiten"/> je Bild. Wege kosten
/// Spielzeit nach ihrer Laenge (<see cref="ProbeWelt.Minuten"/>), gezaehlt beim Ankommen.
/// </summary>
public sealed class ProbeSpiel
{
    /// <summary>Gehgeschwindigkeit in Karteneinheiten je Sekunde (Stadt; im Buero wie fisi_game_gui 7.0 / 2).</summary>
    public const float TempoStadt = 3.5f;
    public const float TempoInnen = 3.5f;

    private readonly List<(double X, double Y)> _weg = [];
    private double _gegangen;
    private ProbeHaus? _zielHaus;

    public ProbeSpiel(ProbeWelt welt)
    {
        Welt = welt;
        Flaeche = welt.StadtFlaeche;
        FigurX = Flaeche.StartX;
        FigurZ = Flaeche.StartZ;
        Minute = ProbeWelt.StartMinute;
        Zeile = SpielProbeTexte.Hinweis;
    }

    public ProbeWelt Welt { get; }

    public ProbeFlaeche Flaeche { get; private set; }

    public bool ImInnenraum => Flaeche.Id != ProbeWelt.Stadt;

    public float FigurX { get; private set; }

    public float FigurZ { get; private set; }

    /// <summary>Blickrichtung der Figur in Grad um die Hochachse (0 = nach +Z).</summary>
    public float FigurRichtung { get; private set; }

    public int Minute { get; private set; }

    public string Uhrzeit => ProbeWelt.Uhrzeit(Minute);

    public string Zeile { get; private set; }

    public bool Geht => _weg.Count > 0;

    public (double X, double Y)? Ziel => _weg.Count > 0 ? _weg[^1] : null;

    /// <summary>Haus, vor dessen Tuer die Figur steht (nach dem Ankommen), sonst null.</summary>
    public ProbeHaus? VorHaus { get; private set; }

    public bool KannBetreten => !ImInnenraum && VorHaus is { HatInnenraum: true } h && h.Offen(Minute);

    /// <summary>Wird bei jeder Aenderung von Ort, Uhr oder Zeile gerufen (Oberflaeche neu beschriften).</summary>
    public event Action? Geaendert;

    public bool GeheZu(double x, double z)
    {
        _zielHaus = null;
        return Starte(x, z, null);
    }

    public bool GeheZu(ProbeHaus haus)
    {
        if (ImInnenraum)
        {
            Verlassen();
        }
        _zielHaus = haus;
        return Starte(haus.TuerX, haus.TuerZ, haus);
    }

    /// <summary>Haus, dessen Tuer naeher als <paramref name="weite"/> an (x, z) liegt.</summary>
    public ProbeHaus? HausBei(double x, double z, double weite = 2.2) =>
        ImInnenraum ? null : Welt.Haeuser
            .Select(h => (Haus: h, Weite: Math.Sqrt(((h.TuerX - x) * (h.TuerX - x)) + ((h.TuerZ - z) * (h.TuerZ - z)))))
            .Where(p => p.Weite < weite).OrderBy(p => p.Weite).Select(p => p.Haus).FirstOrDefault();

    private bool Starte(double x, double z, ProbeHaus? haus)
    {
        VorHaus = null;
        var weg = Flaeche.Wege.Weg((FigurX, FigurZ), (x, z));
        _weg.Clear();
        if (weg.Count == 0)
        {
            if (haus is not null && Math.Abs(haus.TuerX - FigurX) < 0.6 && Math.Abs(haus.TuerZ - FigurZ) < 0.6)
            {
                Ankommen();
                return true;
            }
            Zeile = SpielProbeTexte.NichtErreichbar;
            Geaendert?.Invoke();
            return false;
        }
        _weg.AddRange(weg);
        Zeile = haus is null ? SpielProbeTexte.Hinweis : string.Format(CultureInfo.InvariantCulture, SpielProbeTexte.Unterwegs, haus.Name);
        if (ImInnenraum)
        {
            Zeile = SpielProbeTexte.ImBuero;
        }
        Geaendert?.Invoke();
        return true;
    }

    /// <summary>Figur um <paramref name="sekunden"/> weiterbewegen; true, solange sie geht.</summary>
    public bool Fortschreiten(double sekunden)
    {
        if (_weg.Count == 0)
        {
            return false;
        }
        var rest = sekunden * (ImInnenraum ? TempoInnen : TempoStadt);
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
                _gegangen += weite;
                _weg.RemoveAt(0);
            }
            else
            {
                FigurX += (float)(dx / weite * rest);
                FigurZ += (float)(dz / weite * rest);
                _gegangen += rest;
                rest = 0;
            }
        }
        if (_weg.Count == 0)
        {
            Ankommen();
        }
        return _weg.Count > 0;
    }

    /// <summary>Sofort ans Ziel (Start-Test: Wechsel messen ohne Gehen).</summary>
    public void SofortAnkommen()
    {
        while (Fortschreiten(1000))
        {
        }
    }

    private void Ankommen()
    {
        Minute += ProbeWelt.Minuten(_gegangen);
        _gegangen = 0;
        if (_zielHaus is { } haus)
        {
            VorHaus = haus;
            Zeile = HausZeile(haus);
            _zielHaus = null;
        }
        else
        {
            Zeile = ImInnenraum ? SpielProbeTexte.ImBuero : SpielProbeTexte.Angekommen;
        }
        Geaendert?.Invoke();
    }

    public string HausZeile(ProbeHaus haus)
    {
        var inv = CultureInfo.InvariantCulture;
        if (haus.Oeffnet is not { } von || haus.Schliesst is not { } bis)
        {
            return string.Format(inv, SpielProbeTexte.ImmerOffen, haus.Name);
        }
        if (!haus.Offen(Minute))
        {
            return string.Format(inv, SpielProbeTexte.Geschlossen, haus.Name, ProbeWelt.Uhrzeit(von));
        }
        return haus.HatInnenraum
            ? string.Format(inv, SpielProbeTexte.Offen, haus.Name, ProbeWelt.Uhrzeit(bis))
            : string.Format(inv, SpielProbeTexte.Offen, haus.Name, ProbeWelt.Uhrzeit(bis)) + " " + string.Format(inv, SpielProbeTexte.KeinInnenraum, haus.Name);
    }

    public bool Betreten()
    {
        if (!KannBetreten)
        {
            return false;
        }
        _weg.Clear();
        _gegangen = 0;
        Flaeche = Welt.BueroFlaeche;
        FigurX = Flaeche.StartX;
        FigurZ = Flaeche.StartZ;
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
        _gegangen = 0;
        Flaeche = Welt.StadtFlaeche;
        var haus = Welt.Haeuser.First(h => h.HatInnenraum);
        FigurX = haus.TuerX;
        FigurZ = haus.TuerZ;
        FigurRichtung = 0;
        VorHaus = haus;
        Zeile = HausZeile(haus);
        Geaendert?.Invoke();
    }

    public void Warten(int minuten)
    {
        Minute += Math.Max(0, minuten);
        if (VorHaus is { } haus && !ImInnenraum)
        {
            Zeile = HausZeile(haus);
        }
        Geaendert?.Invoke();
    }

    /// <summary>Pfeiltaste: einen Schritt in Richtung (dx, dz), wenn dort frei ist.</summary>
    public bool Schritt(int dx, int dz)
    {
        var weite = Flaeche.Wege.Schritt * 2;
        var x = FigurX + (dx * weite);
        var z = FigurZ + (dz * weite);
        if (!Flaeche.Wege.KannStehen(x, z))
        {
            return false;
        }
        _zielHaus = HausBei(x, z, 0.8);
        _weg.Clear();
        _weg.Add((x, z));
        VorHaus = null;
        return true;
    }
}
