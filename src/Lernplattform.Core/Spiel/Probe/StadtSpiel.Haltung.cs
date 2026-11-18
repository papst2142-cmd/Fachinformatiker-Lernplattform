using System.Numerics;
using Lernplattform.Core.Daten;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle A 0.81.0 (Weisung P12-272, Sammelliste 349, Nico 15.11.2026: „an den PC auf den Stuhl setzen und dann erst das Menü“,
/// „allgemein hinsetzen“, „ins Bett legen“; Antworten 1b 2c 3a 4b). Mit 3D:
/// <list type="bullet">
/// <item>1b: Stuhl an einem Schreibtisch antippen: die Figur geht hin und setzt sich; erst im Sitzen stehen „Lernen“,
/// „PC benutzen“ bzw. „Firma öffnen“ unten. Stehend fallen sie weg, solange es dort einen freien Stuhl gibt.</item>
/// <item>2c: steht die Figur vor einem freien Sitzmoebel, steht unten „Hinsetzen“ (auch am PC-Stuhl: so geht es mit Tastatur
/// und Screenreader).</item>
/// <item>3a: Bett der eigenen Wohnung antippen: die Figur legt sich hin; erst liegend steht „Schlafen“ unten. Vor einem Bett steht
/// „Hinlegen“.</item>
/// <item>4b: an der Haltestelle setzt sie sich nur ueber „Hinsetzen“ auf die Bank; zum Einsteigen steht sie vorher auf.</item>
/// </list>
/// Aufstehen: „Aufstehen“ oder woanders hin tippen. Sitzen und Liegen sind nur Darstellung: kein Spielschritt, kein Ereignis,
/// keine Spielzeit (Fingerabdruecke gleich). Ohne 3D (<see cref="OhneDreiD"/>) und ohne Grafik bleibt alles wie bis A 0.80.0.
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>Aktion des Knopfs „Hinsetzen“.</summary>
    public const string AktionHinsetzen = "hinsetzen";

    /// <summary>Aktion des Knopfs „Hinlegen“.</summary>
    public const string AktionHinlegen = "hinlegen";

    /// <summary>Aktion des Knopfs „Aufstehen“.</summary>
    public const string AktionAufstehen = "aufstehen";

    /// <summary>Knoepfe, die es mit 3D nur im Sitzen auf einem PC-Stuhl gibt.</summary>
    public static readonly IReadOnlyList<string> PcAktionen = ["lernen", Spielwelt.AktionComputer, Spielwelt.AktionFirma];

    /// <summary>Knopf, den es mit 3D nur im Liegen gibt.</summary>
    private const string AktionSchlafen = "schlafen";

    private static readonly AsyncLocal<bool> HaltungAlt = new();

    /// <summary>Gegenprobe (nur Tests): wie bis A 0.80.0, ohne Sitzen und Liegen.</summary>
    public static void HaltungAltFuerTests(bool an) => HaltungAlt.Value = an;

    /// <summary>Die Oberflaeche zeigt kein 3D (Ortsliste): dann gibt es weder Sitzen noch Liegen.</summary>
    public bool OhneDreiD { get; private set; }

    /// <summary>Der Platz, auf dem die Figur drinnen sitzt oder liegt (null = sie steht oder sitzt draussen).</summary>
    public Sitzgelegenheit? MeinPlatz { get; private set; }

    /// <summary>Nummer der Haltestelle, auf deren Bank die Figur sitzt (null = keine).</summary>
    public int? AufDerBank { get; private set; }

    /// <summary>Die Figur sitzt oder liegt (drinnen oder auf einer Bank).</summary>
    public bool SitztOderLiegt => MeinPlatz is not null || AufDerBank is not null;

    private Innenraum? _platzInnen;
    private (float X, float Z, float Richtung) _stehplatz;
    private Sitzgelegenheit? _setzenNach;

    private bool HaltungAn => Grafik is not null && !OhneDreiD && !HaltungAlt.Value;

    /// <summary>
    /// Lage der Figur zum Zeichnen: Stelle (drinnen ueber dem Boden des Raums, draussen ueber dem Boden an der Haltestelle),
    /// Blick und ob sie liegt. Null = stehend wie bisher.
    /// </summary>
    public (Vector3 Ort, float Blick, bool Liegt)? Haltung
    {
        get
        {
            if (MeinPlatz is { } p && ReferenceEquals(Innen, _platzInnen))
            {
                return (p.Ort, p.Blick, p.Art == SitzArt.Liegen);
            }
            if (AufDerBank is { } nr && !ImInnenraum && Szene.Haltestellen.FirstOrDefault(h => h.Nr == nr) is { } halt)
            {
                var (ort, blick) = Haltestellen.BankSitz(halt, KopfHinten());
                return (ort, blick, false);
            }
            return null;
        }
    }

    private (PyObjekt? Aussehen, IReadOnlyList<Figur.Stueck>? Kleidung, float Wert)? _kopfHinten;

    /// <summary>Welle A 0.81.0 (Sammelliste 351): wie weit der Kopf der sitzenden Figur (Aussehen und Kleidung) hinter ihre Stelle reicht, gemerkt bis zur naechsten Aenderung.</summary>
    private float KopfHinten()
    {
        var aussehen = Ablauf.Stand.Profil is { } profil ? SpielDaten.Get(profil, "aussehen") as PyObjekt : null;
        var kleidung = Kleidung;
        if (_kopfHinten is not { } k || !ReferenceEquals(k.Aussehen, aussehen) || !ReferenceEquals(k.Kleidung, kleidung))
        {
            k = (aussehen, kleidung, Figur.KopfHinten(Grafik, aussehen, kleidung));
            _kopfHinten = k;
        }
        return k.Wert;
    }

    /// <summary>Welle A 0.81.0 (Sammelliste 351): Art des Platzes, auf dem die Figur sitzt oder liegt (Bank = Sitzen; null = sie steht).</summary>
    public SitzArt? HaltungArt => Haltung is null ? null : MeinPlatz?.Art ?? SitzArt.Sitzen;

    /// <summary>
    /// Welle A 0.81.0 (Sammelliste 351, Weisung P12-276): Lage der sitzenden oder liegenden Figur (Hoehe <paramref name="groesse"/>)
    /// zum Zeichnen. Die Stelle ist drinnen die Hoehe ueber dem Boden des Raums und draussen (Bank) die Hoehe ueber dem Boden der
    /// Stadt, auf dem das Haeuschen steht. Bis P12-276 kam draussen die Hoehe unter der stehenden Figur dazu
    /// (<paramref name="bodenDraussen"/>, Strasse 0,058): die Figur schwebte ueber der Bank (Gegenprobe <see cref="Figur.SitzenAltFuerTests"/>).
    /// </summary>
    public static Matrix4x4 HaltungLage((Vector3 Ort, float Blick, bool Liegt) haltung, float groesse, float bodenDraussen) =>
        Matrix4x4.CreateScale(groesse) * (haltung.Liegt ? Matrix4x4.CreateRotationX(-MathF.PI / 2) : Matrix4x4.Identity)
        * Matrix4x4.CreateRotationY(haltung.Blick) * Matrix4x4.CreateTranslation(haltung.Ort + new Vector3(0, Figur.SitzenWieBisher ? bodenDraussen : 0f, 0));

    /// <summary>Die Oberflaeche meldet, ob sie 3D zeigt. Ohne 3D steht die Figur auf, Knoepfe wie bis A 0.80.0.</summary>
    public void OhneDreiDSetzen(bool ohne)
    {
        if (ohne == OhneDreiD)
        {
            return;
        }
        OhneDreiD = ohne;
        Aufstehen();
        if (ImInnenraum && !Einrichten)
        {
            InnenText();
        }
        Geaendert?.Invoke();
    }

    /// <summary>Hinsetzen bzw. hinlegen: die Figur merkt sich, wo sie stand.</summary>
    private void Setzen(Sitzgelegenheit platz)
    {
        if (!SitztOderLiegt)
        {
            _stehplatz = (FigurX, FigurZ, FigurRichtung);
        }
        WegLeeren();
        MeinPlatz = platz;
        _platzInnen = Innen;
        InnenText();
        Geaendert?.Invoke();
    }

    /// <summary>Aufstehen (die Figur steht wieder, wo sie vorher stand); false = sie stand schon.</summary>
    private bool Aufstehen()
    {
        _setzenNach = null;
        if (!SitztOderLiegt)
        {
            return false;
        }
        (FigurX, FigurZ, FigurRichtung) = _stehplatz;
        MeinPlatz = null;
        _platzInnen = null;
        AufDerBank = null;
        return true;
    }

    /// <summary>Knopf „Hinsetzen“, „Hinlegen“ oder „Aufstehen“; false = keine dieser Aktionen.</summary>
    private bool HaltungAktion(string aktion)
    {
        switch (aktion)
        {
            case AktionAufstehen:
                Aufstehen();
                if (ImInnenraum)
                {
                    InnenText();
                }
                else
                {
                    Knoepfe = MitfahrtKnoepfe();
                }
                Geaendert?.Invoke();
                return true;
            case AktionHinsetzen when !ImInnenraum && WartetAn is { } nr:
                _stehplatz = (FigurX, FigurZ, FigurRichtung);
                AufDerBank = nr;
                Knoepfe = MitfahrtKnoepfe();
                Geaendert?.Invoke();
                return true;
            case AktionHinsetzen or AktionHinlegen when Innen is { } innen
                && innen.FreierPlatzNahe(FigurX, FigurZ, s => (s.Art == SitzArt.Liegen) == (aktion == AktionHinlegen) && PlatzNutzbar(s, FigurX, FigurZ)) is { } platz:
                Setzen(platz);
                return true;
            case AktionHinsetzen or AktionHinlegen:
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Drinnen getippt (1b, 3a): auf einen freien PC-Stuhl oder auf ein Bett der eigenen Wohnung geht die Figur zu und setzt bzw.
    /// legt sich bei der Ankunft. true = so behandelt (Ergebnis in <paramref name="geht"/>).
    /// </summary>
    private bool ZumPlatzTippen(Innenraum innen, double x, double z, out bool geht)
    {
        geht = false;
        if (!HaltungAn || (innen.PcStuhlBei(x, z) ?? (Ort == SpielDaten.OrtZuhause ? innen.LiegeplatzBei(x, z) : null)) is not { } ziel)
        {
            return false;
        }
        var (w, t) = (ziel.Rechteck.X2 - ziel.Rechteck.X1, ziel.Rechteck.Z2 - ziel.Rechteck.Z1);
        // Sammelliste 383: zu Hause nie ueber die Wand: durch die Tuer an eine Seite im Raum des Platzes
        var weg = RaumRegel
            ? WegInDenRaum(innen, ziel.Rechteck.X1, ziel.Rechteck.Z1, w, t, null) ?? []
            : MitWeg(() => innen.Weg(FigurX, FigurZ, ziel.Mitte.X, ziel.Mitte.Y, Wohnung.MoebelReichweite + (Math.Min(w, t) / 2)));
        WegLeeren();
        _weg.AddRange(weg);
        if (weg.Count > 0)
        {
            _setzenNach = ziel;
            geht = true;
        }
        else if (ziel.Abstand(FigurX, FigurZ) <= Wohnung.MoebelReichweite + 0.3 && PlatzNutzbar(ziel, FigurX, FigurZ))
        {
            Setzen(ziel);
        }
        else
        {
            Zeile = SpielProbeTexte.NichtErreichbar;
            Knoepfe = [];
        }
        Geaendert?.Invoke();
        return true;
    }

    /// <summary>Bei der Ankunft drinnen: war ein Stuhl oder Bett das Ziel, setzt bzw. legt sich die Figur. true = so geschehen.</summary>
    private bool AmPlatzAngekommen()
    {
        if (_setzenNach is not { } ziel || !ReferenceEquals(Innen, _zielInnen))
        {
            _setzenNach = null;
            return false;
        }
        _setzenNach = null;
        Setzen(ziel);
        return true;
    }

    private Innenraum? _zielInnen;

    /// <summary>
    /// Mit 3D: Knoepfe nach der Haltung. Sitzend am PC-Stuhl bleiben die PC-Knoepfe, liegend „Schlafen“, dazu „Aufstehen“.
    /// Stehend fallen PC-Knoepfe weg, wenn ein freier PC-Stuhl in Reichweite ist (Hinweis in der Zeile), ebenso „Schlafen“ vor
    /// einem freien Bett; vor einem freien Platz steht „Hinsetzen“ bzw. „Hinlegen“.
    /// </summary>
    private List<(string Aktion, string Name)> HaltungKnoepfe(Innenraum innen, List<(string Aktion, string Name)> knoepfe)
    {
        if (!HaltungAn)
        {
            return knoepfe;
        }
        if (MeinPlatz is { } p)
        {
            if (!ReferenceEquals(innen, _platzInnen))
            {
                // der Raum wurde neu gebaut (z. B. Etage, Einrichtung): die Figur steht auf
                Aufstehen();
                return HaltungKnoepfe(innen, knoepfe);
            }
            var bleibt = knoepfe.Where(k => (p.Art == SitzArt.PcStuhl || !PcAktionen.Contains(k.Aktion)) && (p.Art == SitzArt.Liegen || k.Aktion != AktionSchlafen)).ToList();
            Zeile = (p.Art == SitzArt.Liegen ? SpielProbeTexte.DuLiegst : SpielProbeTexte.DuSitzt) + (bleibt.Count > 0 ? "\n" + Zeile : "");
            return [.. bleibt, (AktionAufstehen, SpielProbeTexte.KnopfAufstehen)];
        }
        if (knoepfe.Any(k => PcAktionen.Contains(k.Aktion)) && innen.FreierPlatzNahe(FigurX, FigurZ, s => s.Art == SitzArt.PcStuhl && PlatzNutzbar(s, FigurX, FigurZ)) is not null)
        {
            knoepfe = [.. knoepfe.Where(k => !PcAktionen.Contains(k.Aktion))];
            Zeile += "\n" + SpielProbeTexte.ErstHinsetzen;
        }
        if (knoepfe.Any(k => k.Aktion == AktionSchlafen) && innen.FreierPlatzNahe(FigurX, FigurZ, s => s.Art == SitzArt.Liegen && PlatzNutzbar(s, FigurX, FigurZ)) is not null)
        {
            knoepfe = [.. knoepfe.Where(k => k.Aktion != AktionSchlafen)];
            Zeile += "\n" + SpielProbeTexte.ErstHinlegen;
        }
        if (innen.FreierPlatzNahe(FigurX, FigurZ, s => s.Art != SitzArt.Liegen && PlatzNutzbar(s, FigurX, FigurZ)) is not null)
        {
            knoepfe = [.. knoepfe, (AktionHinsetzen, SpielProbeTexte.KnopfHinsetzen)];
        }
        if (innen.FreierPlatzNahe(FigurX, FigurZ, s => s.Art == SitzArt.Liegen && PlatzNutzbar(s, FigurX, FigurZ)) is not null)
        {
            knoepfe = [.. knoepfe, (AktionHinlegen, SpielProbeTexte.KnopfHinlegen)];
        }
        return knoepfe;
    }

    /// <summary>Draussen an der Haltestelle (4b): „Hinsetzen“ bzw. sitzend „Aufstehen“.</summary>
    private List<(string Aktion, string Name)> BankKnoepfe() =>
        !HaltungAn || WartetAn is null || Mitfahrt != MitfahrtStufe.Keine ? []
        : AufDerBank is not null ? [(AktionAufstehen, SpielProbeTexte.KnopfAufstehen)]
        : [(AktionHinsetzen, SpielProbeTexte.KnopfHinsetzen)];
}
