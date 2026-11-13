using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Design;
using Lernplattform.Views;
using Stadtbau.Core;
using Texte = Stadtbau.Core.Texte;

namespace Stadtbau.Oberflaeche;

/// <summary>
/// Welle S 0.49.0 (Weisung P12-231 Antwort 2c, Weisung P12-229, Sammelliste 328): zwei getrennte Listen rechts in JEDER
/// Ansicht (Draufsicht, 3D, Grundriss, Editor): „Stadt – n rot“ (draussen, Quests, Grenzen) und „Innenräume – m rot“ (jeder
/// geaenderte Innenraum, jede Etage, Innenraeume eigener Gebaeude), nach Ort gruppiert und antippbar. Rot aus allen Orten;
/// gelbe Hinweise nur fuer die offene Ansicht (draussen in der Stadt-Liste, im Grundriss beim offenen Raum). Die Innenraeume
/// rechnet <see cref="InnenFunde"/> im Hintergrund, derselbe Weg wie <see cref="Projekt.RoteFunde"/> (Speichern/Senden).
/// </summary>
public sealed partial class Hauptansicht
{
    /// <summary>
    /// Gegenprobe (nur Start-Test): Listen wie bis S 0.48.0 — draussen keine Innenraeume, im Grundriss nur der offene Raum,
    /// im Editor keine Liste.
    /// </summary>
    internal static bool FundlistenAltFuerTests { get; set; }

    /// <summary>Gegenprobe Patch S 0.49.1 (Sammelliste 332): Kopf „Innenräume“ im Grundriss wie in S 0.49.0 (zaehlt rote Funde unveraenderter Raeume nicht; nur Start-Test).</summary>
    internal static bool UeberschriftAltFuerTests { get; set; }

    /// <summary>Rote Funde des offenen, unveraenderten Raums, die im Grundriss unter „Innenräume“ stehen (Start-Test).</summary>
    internal int UnveraendertRot => _innen && _entwurf is null && _ort is { } o && !_innenSchluessel.Contains(o) ? Funde.Count(f => f.Rot) : 0;

    private List<InnenFunde.Ort> _innenAlle = [];
    private HashSet<string> _innenSchluessel = [];
    private int _innenAlleStand;
    private TaskCompletionSource _innenAlleGeprueft = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // draussen (Draufsicht und 3D teilen die rechte Spalte): Liste der Innenraeume unter „Stadt – n rot“
    private readonly StackPanel _innenListeDraussenInhalt = new() { Spacing = 6 };
    private Klappgruppe? _innenListeDraussen;

    // Grundriss: „Stadt – n rot“ ueber „Innenräume – m rot“; dort der offene Raum zuerst, darunter die anderen Raeume
    private readonly StackPanel _stadtListeInnenInhalt = new() { Spacing = 6 };
    private Klappgruppe? _stadtListeInnen;
    private readonly TextBlock _offenerRaumKopf = Schrift.Text("", "Klein", FontWeight.Bold);
    private readonly StackPanel _innenAndere = new() { Spacing = 6 };

    // Editor
    private readonly StackPanel _stadtListeEntwurfInhalt = new() { Spacing = 6 };
    private readonly StackPanel _innenListeEntwurfInhalt = new() { Spacing = 6 };
    private Klappgruppe? _stadtListeEntwurf;
    private Klappgruppe? _innenListeEntwurf;

    /// <summary>Fertig, sobald die Innenraeume nach der letzten Aenderung geprueft sind (Start-Test).</summary>
    internal Task InnenAlleGeprueft => _innenAlleGeprueft.Task;

    /// <summary>Die Funde aller Innenraeume (rot und gelb, je Raum) nach der letzten Pruefung (Start-Test).</summary>
    internal IReadOnlyList<InnenFunde.Ort> InnenAlle => _innenAlle;

    /// <summary>Die Listen der offenen Ansicht (Start-Test): Stadt-Liste und Innenraum-Liste.</summary>
    internal (Klappgruppe? Stadt, Klappgruppe? Innen) Fundlisten => _entwurf is not null ? (_stadtListeEntwurf, _innenListeEntwurf)
        : _innen ? (_stadtListeInnen, _pruefenInnen) : (_pruefenAussen, _innenListeDraussen);

    /// <summary>Zahl der roten Funde in der Liste „Innenräume“ (zaehlt nur Raeume, die das Senden sperren).</summary>
    internal int InnenRot => InnenAnzeige().Where(o => _innenSchluessel.Contains(o.Schluessel)).Sum(o => o.Rot);

    /// <summary>Zahl der roten Funde in der Liste „Stadt“.</summary>
    internal int StadtRot => AussenFunde.Count(f => f.Rot);

    /// <summary>Der Grundriss ist die offene Ansicht (Start-Test).</summary>
    internal bool ZeigtGrundriss => _innen && _entwurf is null;

    /// <summary>Listen neu zeichnen (Start-Test, nach dem Umschalten der Gegenprobe).</summary>
    internal void NeuZeichnenFuerTests() => FundlistenZeigen();

    /// <summary>Gruppen der Innenraum-Liste im Grundriss (unter „Innenräume“, ohne den offenen Raum).</summary>
    internal Control InnenAndereListe => _innenAndere;

    private void FundlistenAussenBauen(StackPanel rechts)
    {
        _innenListeDraussen?.Inhalt.Children.Remove(_innenListeDraussenInhalt);
        _innenListeDraussen = new Klappgruppe("funde_innen", Texte.FundeInnenTitel(0), Texte.FundeInnenTipp, true, _innenListeDraussenInhalt);
        rechts.Children.Insert(rechts.Children.IndexOf(_pruefenAussen!) + 1, _innenListeDraussen);
    }

    private Klappgruppe FundlistenInnenBauen()
    {
        _stadtListeInnen?.Inhalt.Children.Remove(_stadtListeInnenInhalt);
        _stadtListeInnen = new Klappgruppe("funde_stadt", Texte.FundeStadtTitel(0), Texte.FundeStadtTipp, true, _stadtListeInnenInhalt);
        return _stadtListeInnen;
    }

    private void FundlistenEntwurfBauen(EntwurfAnsicht ansicht)
    {
        _stadtListeEntwurfInhalt.Children.Clear();
        _innenListeEntwurfInhalt.Children.Clear();
        // der Editor wird je Oeffnen neu gebaut: die Inhalte erst aus den Gruppen des vorigen Editors loesen (sonst
        // „already has a visual parent“ beim zweiten Oeffnen)
        _stadtListeEntwurf?.Inhalt.Children.Remove(_stadtListeEntwurfInhalt);
        _innenListeEntwurf?.Inhalt.Children.Remove(_innenListeEntwurfInhalt);
        _stadtListeEntwurf = new Klappgruppe("funde_stadt_entwurf", Texte.FundeStadtTitel(0), Texte.FundeStadtTipp, true, _stadtListeEntwurfInhalt);
        _innenListeEntwurf = new Klappgruppe("funde_innen_entwurf", Texte.FundeInnenTitel(0), Texte.FundeInnenTipp, true, _innenListeEntwurfInhalt);
        ansicht.Fundlisten.Children.Clear();
        if (!FundlistenAltFuerTests)
        {
            ansicht.Fundlisten.Children.Add(_stadtListeEntwurf);
            ansicht.Fundlisten.Children.Add(_innenListeEntwurf);
        }
        FundlistenZeigen();
    }

    /// <summary>Alle Innenraeume pruefen: Kopien jetzt (Oberflaechen-Faden), Rechnung im Hintergrund.</summary>
    private void InnenAllePruefenStarten()
    {
        if (_projekt is null || _daten is null)
        {
            return;
        }
        var stand = ++_innenAlleStand;
        if (_innenAlleGeprueft.Task.IsCompleted)
        {
            _innenAlleGeprueft = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }
        var auftrag = InnenFunde.Vorbereiten(_projekt);
        _innenSchluessel = [.. auftrag.Raeume.Select(r => r.Schluessel)];
        _ = Hintergrund(Task.Run(() => InnenFunde.Rechnen(auftrag))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (stand != _innenAlleStand)
            {
                return;
            }
            if (t.IsFaulted)
            {
                _ordner.Fehler("Prüfung Innenräume: " + t.Exception?.InnerException);
                _innenAlle = [new InnenFunde.Ort("", Texte.FundeInnen,
                    [new StadtbauFund("Die Prüfung ist abgebrochen: " + t.Exception?.InnerException?.Message, "Bitte CC Bescheid geben.", true)])];
            }
            else
            {
                _innenAlle = t.Result;
            }
            FundlistenZeigen();
            _innenAlleGeprueft.TrySetResult();
        }), TaskScheduler.Default);
    }

    /// <summary>
    /// Raeume fuer die Innenraum-Liste: das Ergebnis der Hintergrund-Pruefung; der offene Raum mit den frischen Funden der
    /// offenen Ansicht (rot und gelb), auch wenn er unveraendert ist (dann mit Hinweis und ohne Zahl).
    /// </summary>
    private List<InnenFunde.Ort> InnenAnzeige()
    {
        var liste = new List<InnenFunde.Ort>(_innenAlle);
        if (_innen && _ort is { } o && _projekt is not null && _geprueft.Task.IsCompleted)
        {
            var offen = new InnenFunde.Ort(o, _projekt.OrtName(o), Funde);
            var i = liste.FindIndex(x => x.Schluessel == o);
            if (i >= 0)
            {
                liste[i] = offen;
            }
            else
            {
                liste.Add(offen);
            }
        }
        return liste;
    }

    /// <summary>Beide Listen in der offenen Ansicht neu zeichnen (Titel, Gruppen, Eintraege).</summary>
    private void FundlistenZeigen()
    {
        if (FundlistenAltFuerTests)
        {
            if (_innenListeDraussen is not null)
            {
                _innenListeDraussen.IsVisible = false;
            }
            if (_stadtListeInnen is not null)
            {
                _stadtListeInnen.IsVisible = false;
            }
            _offenerRaumKopf.IsVisible = false;
            _innenAndere.Children.Clear();
            PruefTitel(_pruefenAussen, AussenFunde);
            PruefTitel(_pruefenInnen, Funde);
            return;
        }
        var stadtRot = StadtRot;
        var stadtTitel = Texte.FundeStadtTitel(stadtRot);
        var stadtFarbe = stadtRot > 0 ? "Pinsel.red" : "Pinsel.text";
        var innenRot = InnenRot;
        var innenTitel = Texte.FundeInnenTitel(innenRot);
        var innenFarbe = innenRot > 0 ? "Pinsel.red" : "Pinsel.text";
        var alle = InnenAnzeige();

        // draussen: die Stadt-Liste ist die bisherige Pruefliste (rot und gelb draussen), dazu die Innenraeume nur rot
        _pruefenAussen?.TitelSetzen(stadtTitel, stadtFarbe);
        if (_innenListeDraussen is not null)
        {
            _innenListeDraussen.IsVisible = true;
            _innenListeDraussen.TitelSetzen(innenTitel, innenFarbe);
            InnenGruppen(_innenListeDraussenInhalt, alle.Where(o => _innenSchluessel.Contains(o.Schluessel)), nurRot: true);
        }

        // Grundriss: Stadt nur rot; Innenraeume: offener Raum (rot und gelb) mit Kopf, darunter die anderen nur rot
        if (_stadtListeInnen is not null)
        {
            _stadtListeInnen.IsVisible = true;
            _stadtListeInnen.TitelSetzen(stadtTitel, stadtFarbe);
            StadtEintraege(_stadtListeInnenInhalt);
        }
        // Patch S 0.49.1 (Sammelliste 332): im Grundriss stehen unter „Innenräume“ auch die roten Funde des offenen Raums, wenn er
        // unveraendert ist (sie sperren nicht); der Kopf nennt sie, sonst stuende dort „alles in Ordnung“ ueber roten Funden
        var unveraendertRot = _ort is { } o2 && !_innenSchluessel.Contains(o2) && !UeberschriftAltFuerTests ? Funde.Count(f => f.Rot) : 0;
        _pruefenInnen?.TitelSetzen(Texte.FundeInnenTitel(innenRot, unveraendertRot), innenRot + unveraendertRot > 0 ? "Pinsel.red" : "Pinsel.text");
        if (_ort is { } offen && _projekt is not null)
        {
            _offenerRaumKopf.IsVisible = true;
            var rotOffen = Funde.Count(f => f.Rot);
            _offenerRaumKopf.Text = Texte.FundeOrtKopf(_projekt.OrtName(offen), rotOffen) + " · " + Texte.FundeOffenerRaum
                + (_innenSchluessel.Contains(offen) || rotOffen == 0 ? "" : " – " + Texte.FundeUnveraendert);
            _offenerRaumKopf.Farbe(TextBlock.ForegroundProperty, rotOffen > 0 ? "Pinsel.red" : "Pinsel.text");
        }
        InnenGruppen(_innenAndere, alle.Where(o => o.Schluessel != _ort && _innenSchluessel.Contains(o.Schluessel)), nurRot: true, leerText: null);

        // Editor: beide nur rot
        if (_entwurf is not null && _stadtListeEntwurf is not null && _innenListeEntwurf is not null)
        {
            _stadtListeEntwurf.TitelSetzen(stadtTitel, stadtFarbe);
            _innenListeEntwurf.TitelSetzen(innenTitel, innenFarbe);
            StadtEintraege(_stadtListeEntwurfInhalt);
            InnenGruppen(_innenListeEntwurfInhalt, alle.Where(o => _innenSchluessel.Contains(o.Schluessel)), nurRot: true);
        }
    }

    /// <summary>Rote Funde draussen als Eintraege (Antippen: zur Stelle in der Draufsicht bzw. 3D).</summary>
    private void StadtEintraege(StackPanel ziel)
    {
        ziel.Children.Clear();
        var rot = AussenFunde.Where(f => f.Rot).ToList();
        if (rot.Count == 0)
        {
            ziel.Children.Add(Schrift.Text("✓ " + Texte.FundeStadtKeine, "Klein", farbe: "Pinsel.green"));
            return;
        }
        foreach (var f in rot)
        {
            ziel.Children.Add(FundListe.Eintrag(f, f.X is not null && f.Y is not null ? () => _ = StadtFundOeffnenAsync(f) : null));
        }
    }

    /// <summary>Funde je Raum gruppiert: Kopf „Name (n rot)“, darunter die Eintraege (Antippen oeffnet den Raum an der Stelle).</summary>
    private void InnenGruppen(StackPanel ziel, IEnumerable<InnenFunde.Ort> orte, bool nurRot, string? leerText = Texte.FundeInnenKeine)
    {
        ziel.Children.Clear();
        foreach (var o in orte)
        {
            var funde = nurRot ? o.Funde.Where(f => f.Rot).ToList() : o.Funde;
            if (funde.Count == 0)
            {
                continue;
            }
            var kopf = Schrift.Text(Texte.FundeOrtKopf(o.Name, o.Rot), "Klein", FontWeight.Bold);
            kopf.Margin = new Thickness(0, ziel.Children.Count > 0 ? 6 : 0, 0, 0);
            ziel.Children.Add(kopf);
            foreach (var f in funde)
            {
                var schluessel = o.Schluessel;
                ziel.Children.Add(FundListe.Eintrag(f, f.X is not null && f.Y is not null && schluessel.Length > 0 ? () => _ = InnenFundOeffnenAsync(schluessel, f) : null));
            }
        }
        if (ziel.Children.Count == 0 && leerText is not null)
        {
            ziel.Children.Add(Schrift.Text("✓ " + leerText, "Klein", farbe: "Pinsel.green"));
        }
    }

    /// <summary>Fund eines Innenraums antippen: Editor verlassen (mit Rueckfrage), Raum oeffnen, an die Stelle springen.</summary>
    internal async Task InnenFundOeffnenAsync(string schluessel, StadtbauFund f)
    {
        if (_entwurf is not null)
        {
            await EntwurfZurueckAsync();
            if (_entwurf is not null)
            {
                return;
            }
        }
        if (_projekt is null || !_projekt.GibtInnenort(schluessel))
        {
            return;
        }
        if (_ort != schluessel || !_innen)
        {
            OrtOeffnen(schluessel);
        }
        ZuFund(f);
    }

    /// <summary>Fund draussen antippen: Editor verlassen (mit Rueckfrage), Draufsicht (oder offene 3D-Ansicht), an die Stelle.</summary>
    internal async Task StadtFundOeffnenAsync(StadtbauFund f)
    {
        if (_entwurf is not null)
        {
            await EntwurfZurueckAsync();
            if (_entwurf is not null)
            {
                return;
            }
        }
        if (_innen || _geschichteOffen || _inhalt.Content != _kartenAnsicht)
        {
            ReiterZeigen(innen: false);
        }
        ZuAussenFund(f);
    }
}
