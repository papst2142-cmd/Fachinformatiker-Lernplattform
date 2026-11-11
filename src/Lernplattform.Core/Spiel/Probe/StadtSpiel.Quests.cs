using Lernplattform.Core.Daten;
using Lernplattform.Core.Stadtbau;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Welle Q2 (A 0.65.0, Weisung P12-190): Questgeber draussen ansprechen. Jeder Questgeber draussen gehoert zum Ort, dessen
/// Standplatz ihm am naechsten ist (<see cref="QuestgeberOrt"/>). Kommt die Figur an diesem Ort an (auch ohne 3D ueber die
/// Ortsliste) oder bis <see cref="Reichweite"/> an den Questgeber heran, steht unter der Ansicht sein Text und, wenn er
/// heute einen Auftrag hat, der Knopf „Auftrag annehmen“ (Aktion "auftrag:&lt;id&gt;" wie im Buero). Nur Darstellung und
/// Bedienung: Tickets, Geld und Ruf laufen ueber den heutigen Ablauf, kein neues Ereignis.
/// </summary>
public sealed partial class StadtSpiel
{
    /// <summary>So nah muss die Figur draussen an einem Questgeber stehen, um ihn anzusprechen (Feldmass).</summary>
    public const double Reichweite = 1.0;

    /// <summary>So weit vor einem Questgeber bleibt die Figur stehen, wenn er angetippt wurde.</summary>
    public const double VorDemQuestgeber = 0.55;

    /// <summary>Die Questgeber draussen (aus der Szene, Fassung 0: keine).</summary>
    public IReadOnlyList<Questebene.Draussen> QuestgeberDraussen => Szene.Questgeber;

    /// <summary>Ort, zu dem ein Questgeber draussen gehoert: der mit dem naechsten Standplatz (gleich weit: erster der Karte).</summary>
    public string? QuestgeberOrt(Questebene.Draussen q) => Szene.QuestgeberOrt(q);

    /// <summary>Questgeber draussen in Reichweite der Figur (der naechste), sonst null.</summary>
    public Questebene.Draussen? QuestgeberNahe(double x, double z) =>
        QuestgeberDraussen.Select(q => (q, a: Math.Sqrt(((q.X - x) * (q.X - x)) + ((q.Z - z) * (q.Z - z)))))
            .Where(p => p.a <= Reichweite).OrderBy(p => p.a).Select(p => p.q).FirstOrDefault();

    /// <summary>Questgeber draussen an der getippten Stelle (wie das Tippen auf eine Person drinnen), sonst null.</summary>
    private Questebene.Draussen? QuestgeberBei(double x, double z) =>
        QuestgeberDraussen.Select(q => (q, a: Math.Sqrt(((q.X - x) * (q.X - x)) + ((q.Z - z) * (q.Z - z)))))
            .Where(p => p.a <= 0.6).OrderBy(p => p.a).Select(p => p.q).FirstOrDefault();

    /// <summary>Ziel vor einem angetippten Questgeber (in Blickrichtung, sonst seine Stelle).</summary>
    private (double X, double Z) ZielBeimQuestgeber(double x, double z)
    {
        if (QuestgeberBei(x, z) is not { } q)
        {
            return (x, z);
        }
        var (bx, by) = Etagen.Richtung(q.Blick);
        var vorn = (X: q.X + (bx * VorDemQuestgeber), Z: q.Z + (by * VorDemQuestgeber));
        return Szene.Laufraster.KannStehen(vorn.X, vorn.Z) ? vorn : (q.X, q.Z);
    }

    /// <summary>Text und Knopf eines Questgebers draussen (wie eine Person im Buero).</summary>
    private (string Zeile, List<(string Aktion, string Name)> Knoepfe) QuestgeberText(Questebene.Draussen q)
    {
        var (titel, text, knoepfe) = SpielOrte.Nachricht(Questebene.OrtDraussen, q.X, q.Z, q.Daten, Ablauf.Stand, new PyObjekt { { "raeume", new List<object?>() } });
        return (text.Length > 0 ? titel + "\n" + text : titel, knoepfe);
    }

    /// <summary>Angekommen im Freien: ein Questgeber in Reichweite spricht, sonst der Text „im Freien“.</summary>
    private bool QuestgeberImFreien()
    {
        if (QuestgeberNahe(FigurX, FigurZ) is not { } q)
        {
            return false;
        }
        var (zeile, knoepfe) = QuestgeberText(q);
        Zeile = MitUhrHinweis(zeile);
        Knoepfe = knoepfe;
        return true;
    }

    /// <summary>
    /// Vor einem Ort angekommen: Questgeber draussen, die zu diesem Ort gehoeren und heute einen Auftrag haben, mit Text und
    /// Knopf (auch ohne 3D); ihre Knoepfe kommen nach denen des Orts.
    /// </summary>
    private void QuestgeberAmOrt(string ort)
    {
        if (QuestgeberDraussen.Count == 0)
        {
            return;
        }
        var auftraege = Ablauf.Stand.Auftraege(Questebene.OrtDraussen);
        var knoepfe = Knoepfe.ToList();
        foreach (var q in QuestgeberDraussen)
        {
            if (QuestgeberOrt(q) != ort || auftraege.Get(q.Id) is not List<object?> { Count: > 0 })
            {
                continue;
            }
            var (zeile, k) = QuestgeberText(q);
            Zeile = Zeile + "\n" + zeile;
            knoepfe.AddRange(k.Where(x => !knoepfe.Contains(x)));
        }
        Knoepfe = knoepfe;
    }

    /// <summary>Nach einem geloesten Ticket draussen: Text und Knoepfe neu (der Knopf des erledigten Auftrags faellt weg).</summary>
    private void DraussenText()
    {
        if (ImInnenraum || Geht)
        {
            return;
        }
        if (ImFreien)
        {
            if (!QuestgeberImFreien())
            {
                Zeile = SpielProbeTexte.ImFreien;
                Knoepfe = [];
            }
            return;
        }
        Zeile = OrtZeile(Jetzt.Ort);
        AussenKnoepfe(Jetzt.Ort);
    }
}
