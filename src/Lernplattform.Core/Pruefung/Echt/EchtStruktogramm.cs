using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Lernplattform.Core.Texte;

namespace Lernplattform.Core.Pruefung.Echt;

/// <summary>Ein Zweig eines Strukturblocks (Ja, Nein, Fall, Sonst, Rumpf) mit seinen Bloecken von oben nach unten.</summary>
public sealed record EchtZweig(string Art, string Text, IReadOnlyList<EchtBlock> Bloecke);

/// <summary>Ein Strukturblock eines Struktogramms (Anweisung, Verzweigung, ...) mit Text und Zweigen.</summary>
public sealed record EchtBlock(string Typ, string Text, IReadOnlyList<EchtZweig> Zweige);

/// <summary>
/// A 1.12.0 "Prüfung im echten Format" W2 (Weisung P12-390, Plan P12-250): Struktogramm nach DIN 66261 (Nassi-Shneiderman).
/// Anders als die Diagramme im Raster (<see cref="EchtZeichnung"/>) ist ein Struktogramm eine Schachtelung: ein Block steckt
/// im Zweig oder Rumpf eines anderen. Diese Klasse liest das Musterbild aus pruefungssaetze.json (streng, Format 5) und ist
/// der Baukasten der eigenen Zeichnung: jede Aenderung laeuft ueber eine Methode, die nur Zustaende zulaesst, die sich
/// wieder lesen lassen. Gespeichert wird als JSON-Text in der Antwort <c>&lt;aufgabe&gt;.&lt;nr&gt;/bau</c> (leer = nichts
/// Eigenes), wie beim Raster-Baukasten (<see cref="EchtBaukasten.Feld"/>).
/// <para>Stellen (Pfade) sind Texte aus Zahlen mit Punkt: "" = oberste Folge, "1" = zweiter Block oben, "1.0" = erster
/// Zweig dieses Blocks, "1.0.2" = dritter Block darin. Ungerade Zahl von Teilen = Block, gerade = Zweig.</para>
/// </summary>
public sealed class EchtStruktogramm
{
    /// <summary>Diagrammart in pruefungssaetze.json (ab Format 5).</summary>
    public const string Art = "struktogramm";

    /// <summary>Hoechstens so viele Zeichen je Text (Anweisung, Bedingung, Fall).</summary>
    public const int TextLaenge = 120;

    /// <summary>Hoechstens so viele Bloecke im ganzen Struktogramm.</summary>
    public const int BloeckeMax = 60;

    /// <summary>Hoechstens so tief geschachtelt (Ebene 1 = oberste Folge).</summary>
    public const int TiefeMax = 8;

    /// <summary>Fallauswahl: so viele Faelle (ohne "sonst") mindestens bzw. hoechstens.</summary>
    public const int FaelleMin = 2;

    public const int FaelleMax = 6;

    public const string Anweisung = "anweisung";
    public const string Verzweigung = "verzweigung";
    public const string Fallauswahl = "fallauswahl";
    public const string Zaehlschleife = "zaehlschleife";
    public const string Solange = "solange";
    public const string Wiederhole = "wiederhole";
    public const string Aufruf = "aufruf";

    /// <summary>Die sieben Bausteine in der Reihenfolge der Knoepfe (die Folge ergibt sich von selbst).</summary>
    public static readonly IReadOnlyList<string> Bausteine = [Anweisung, Verzweigung, Fallauswahl, Zaehlschleife, Solange, Wiederhole, Aufruf];

    public const string Ja = "ja";
    public const string Nein = "nein";
    public const string Fall = "fall";
    public const string Sonst = "sonst";
    public const string Rumpf = "rumpf";

    private static readonly JsonSerializerOptions Ausgabe = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private sealed class Knoten(string typ, string text)
    {
        public string Typ { get; } = typ;
        public string Text { get; set; } = text;
        public List<Ast> Zweige { get; } = [];
    }

    private sealed class Ast(string art, string text)
    {
        public string Art { get; } = art;
        public string Text { get; set; } = text;
        public List<Knoten> Bloecke { get; } = [];
    }

    private readonly Ast _wurzel = new("folge", "");

    private EchtStruktogramm()
    {
    }

    /// <summary>Konnte die gespeicherte Zeichnung nicht gelesen werden (der Baukasten beginnt dann leer)?</summary>
    public bool Unlesbar { get; private set; }

    /// <summary>Die Zeichnung wie sie jetzt ist (zum Zeichnen, fuer die Textfassung und die Zaehlung).</summary>
    public IReadOnlyList<EchtBlock> Bloecke => Fest(_wurzel).Bloecke;

    /// <summary>Hat man etwas gezeichnet?</summary>
    public bool Eigenes => _wurzel.Bloecke.Count > 0;

    /// <summary>Neuer, leerer Baukasten.</summary>
    public static EchtStruktogramm Neu() => new();

    /// <summary>
    /// Gespeicherte Zeichnung lesen. Leer = nichts Eigenes. Ein Text, der sich nicht lesen laesst (nur bei Eingriffen von
    /// aussen moeglich), stuerzt nie ab: der Baukasten beginnt leer und <see cref="Unlesbar"/> ist wahr.
    /// </summary>
    public static EchtStruktogramm Laden(string? text)
    {
        var bau = new EchtStruktogramm();
        if (string.IsNullOrWhiteSpace(text))
        {
            return bau;
        }
        try
        {
            var bloecke = Lesen(JsonNode.Parse(text), "Zeichnung", vollstaendig: false);
            bau._wurzel.Bloecke.AddRange(bloecke.Select(Los));
        }
        catch (Exception e) when (e is JsonException or EchtLadefehler or InvalidOperationException or FormatException)
        {
            bau = new EchtStruktogramm { Unlesbar = true };
        }
        return bau;
    }

    /// <summary>Zeichnung als JSON-Text (leer, wenn nichts gezeichnet ist).</summary>
    public string Json() => Eigenes ? JsonSerializer.Serialize(AlsJson(Bloecke), Ausgabe) : "";

    /// <summary>Ein Struktogramm als JSON-Objekt (gleiches Format wie das Musterbild in pruefungssaetze.json).</summary>
    public static JsonObject AlsJson(IReadOnlyList<EchtBlock> bloecke) => new()
    {
        ["art"] = Art,
        ["bloecke"] = Liste(bloecke),
    };

    private static JsonArray Liste(IReadOnlyList<EchtBlock> bloecke) => [.. bloecke.Select(b => (JsonNode)BlockJson(b))];

    private static JsonObject BlockJson(EchtBlock b)
    {
        var o = new JsonObject { ["typ"] = b.Typ, ["text"] = b.Text };
        switch (b.Typ)
        {
            case Verzweigung:
                o["ja"] = Liste(b.Zweige[0].Bloecke);
                o["nein"] = Liste(b.Zweige[1].Bloecke);
                break;
            case Fallauswahl:
                o["faelle"] = new JsonArray([.. b.Zweige.Where(z => z.Art == Fall)
                    .Select(z => (JsonNode)new JsonObject { ["text"] = z.Text, ["bloecke"] = Liste(z.Bloecke) })]);
                o["sonst"] = Liste(b.Zweige[^1].Bloecke);
                break;
            case Zaehlschleife or Solange or Wiederhole:
                o["rumpf"] = Liste(b.Zweige[0].Bloecke);
                break;
        }
        return o;
    }

    // -- Lesen (Musterbild streng, gespeicherte Zeichnung) --------------------------------------------------------------

    /// <summary>
    /// Struktogramm aus JSON lesen ({"art": "struktogramm", "bloecke": [...]}). <paramref name="vollstaendig"/> = Musterbild:
    /// jeder Block und jeder Fall hat Text, kein Schleifenrumpf ist leer, mindestens ein Block. Jeder Verstoss ist ein
    /// <see cref="EchtLadefehler"/> mit klarer Meldung.
    /// </summary>
    public static IReadOnlyList<EchtBlock> Lesen(JsonNode? knoten, string wo, bool vollstaendig)
    {
        if (knoten is not JsonObject d)
        {
            throw new EchtLadefehler(wo + " fehlt");
        }
        if (Text(d["art"]) != Art)
        {
            throw new EchtLadefehler(wo + ": Art muss \"" + Art + "\" sein");
        }
        var zahl = 0;
        var bloecke = Folge(d["bloecke"], wo, vollstaendig, 1, ref zahl);
        if (vollstaendig && bloecke.Count == 0)
        {
            throw new EchtLadefehler(wo + ": das Struktogramm hat keinen Block");
        }
        return bloecke;
    }

    private static List<EchtBlock> Folge(JsonNode? knoten, string wo, bool vollstaendig, int tiefe, ref int zahl)
    {
        if (knoten is not JsonArray liste)
        {
            throw new EchtLadefehler(wo + ": Liste der Blöcke fehlt");
        }
        if (tiefe > TiefeMax)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: tiefer als {TiefeMax} Ebenen geschachtelt"));
        }
        var bloecke = new List<EchtBlock>();
        foreach (var eintrag in liste)
        {
            if (eintrag is not JsonObject o)
            {
                throw new EchtLadefehler(wo + ": Block ist kein Objekt");
            }
            if (++zahl > BloeckeMax)
            {
                throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture, $"{wo}: mehr als {BloeckeMax} Blöcke"));
            }
            var typ = Text(o["typ"]) ?? "";
            if (!Bausteine.Contains(typ))
            {
                throw new EchtLadefehler(wo + ": Baustein \"" + typ + "\" gibt es im Struktogramm nicht");
            }
            var text = BlockText(o["text"], wo, vollstaendig, Name(typ));
            var zweige = new List<EchtZweig>();
            switch (typ)
            {
                case Verzweigung:
                    zweige.Add(new EchtZweig(Ja, "", Folge(o["ja"], wo, vollstaendig, tiefe + 1, ref zahl)));
                    zweige.Add(new EchtZweig(Nein, "", Folge(o["nein"], wo, vollstaendig, tiefe + 1, ref zahl)));
                    break;
                case Fallauswahl:
                    if (o["faelle"] is not JsonArray faelle || faelle.Count < FaelleMin || faelle.Count > FaelleMax)
                    {
                        throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                            $"{wo}: eine Fallauswahl hat {FaelleMin} bis {FaelleMax} Fälle"));
                    }
                    foreach (var f in faelle)
                    {
                        var fall = f as JsonObject ?? throw new EchtLadefehler(wo + ": Fall ist kein Objekt");
                        zweige.Add(new EchtZweig(Fall, BlockText(fall["text"], wo, vollstaendig, "Fall"),
                            Folge(fall["bloecke"], wo, vollstaendig, tiefe + 1, ref zahl)));
                    }
                    zweige.Add(new EchtZweig(Sonst, "", Folge(o["sonst"], wo, vollstaendig, tiefe + 1, ref zahl)));
                    break;
                case Zaehlschleife or Solange or Wiederhole:
                    var rumpf = Folge(o["rumpf"], wo, vollstaendig, tiefe + 1, ref zahl);
                    if (vollstaendig && rumpf.Count == 0)
                    {
                        throw new EchtLadefehler(wo + ": " + Name(typ) + " „" + text + "“ mit leerem Rumpf");
                    }
                    zweige.Add(new EchtZweig(Rumpf, "", rumpf));
                    break;
            }
            bloecke.Add(new EchtBlock(typ, text, zweige));
        }
        return bloecke;
    }

    private static string BlockText(JsonNode? knoten, string wo, bool vollstaendig, string was)
    {
        var text = Text(knoten) ?? "";
        if (text.Contains('\n', StringComparison.Ordinal) || text.Length > TextLaenge)
        {
            throw new EchtLadefehler(string.Create(CultureInfo.InvariantCulture,
                $"{wo}: Text von {was} länger als {TextLaenge} Zeichen oder mit Zeilenumbruch"));
        }
        if (vollstaendig && text.Trim().Length == 0)
        {
            throw new EchtLadefehler(wo + ": " + was + " ohne Text");
        }
        return text;
    }

    private static string? Text(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    private static Knoten Los(EchtBlock b)
    {
        var k = new Knoten(b.Typ, b.Text);
        foreach (var z in b.Zweige)
        {
            var ast = new Ast(z.Art, z.Text);
            ast.Bloecke.AddRange(z.Bloecke.Select(Los));
            k.Zweige.Add(ast);
        }
        return k;
    }

    private static EchtZweig Fest(Ast a) => new(a.Art, a.Text, [.. a.Bloecke.Select(Fest)]);

    private static EchtBlock Fest(Knoten k) => new(k.Typ, k.Text, [.. k.Zweige.Select(Fest)]);

    // -- Stellen -------------------------------------------------------------------------------------------------------

    private static int[]? Teile(string? pfad)
    {
        if (pfad is null)
        {
            return null;
        }
        if (pfad.Length == 0)
        {
            return [];
        }
        var teile = pfad.Split('.');
        var zahlen = new int[teile.Length];
        for (var i = 0; i < teile.Length; i++)
        {
            if (!int.TryParse(teile[i], NumberStyles.None, CultureInfo.InvariantCulture, out zahlen[i]))
            {
                return null;
            }
        }
        return zahlen;
    }

    private static string Pfad(IEnumerable<int> teile) => string.Join('.', teile.Select(t => t.ToString(CultureInfo.InvariantCulture)));

    /// <summary>Zweig (gerade Zahl von Teilen) an einer Stelle, sonst null.</summary>
    private Ast? AstAn(int[] t)
    {
        if (t.Length % 2 != 0)
        {
            return null;
        }
        var ast = _wurzel;
        for (var i = 0; i < t.Length; i += 2)
        {
            if (t[i] >= ast.Bloecke.Count || t[i + 1] >= ast.Bloecke[t[i]].Zweige.Count)
            {
                return null;
            }
            ast = ast.Bloecke[t[i]].Zweige[t[i + 1]];
        }
        return ast;
    }

    /// <summary>Block (ungerade Zahl von Teilen) an einer Stelle mit dem Zweig, in dem er steht, sonst null.</summary>
    private (Ast Ast, int Index, Knoten Block)? BlockAn(int[] t)
    {
        if (t.Length % 2 != 1 || AstAn(t[..^1]) is not { } ast || t[^1] >= ast.Bloecke.Count)
        {
            return null;
        }
        return (ast, t[^1], ast.Bloecke[t[^1]]);
    }

    /// <summary>Gibt es an der Stelle einen Block?</summary>
    public bool IstBlock(string? pfad) => Teile(pfad) is { } t && BlockAn(t) is not null;

    /// <summary>Gibt es an der Stelle einen Zweig (ohne die oberste Folge)?</summary>
    public bool IstZweig(string? pfad) => Teile(pfad) is { Length: > 0 } t && AstAn(t) is not null;

    /// <summary>Block an einer Stelle (oder null).</summary>
    public EchtBlock? Block(string? pfad) => Teile(pfad) is { } t && BlockAn(t) is { } b ? Fest(b.Block) : null;

    /// <summary>Zweig an einer Stelle (oder null).</summary>
    public EchtZweig? Zweig(string? pfad) => Teile(pfad) is { Length: > 0 } t && AstAn(t) is { } a ? Fest(a) : null;

    /// <summary>Wie tief steht die Stelle (Ebene 1 = oberste Folge)?</summary>
    private static int Ebene(int[] t) => (t.Length / 2) + 1;

    private int Zahl() => Zaehle(_wurzel);

    private static int Zaehle(Ast a) => a.Bloecke.Sum(b => 1 + b.Zweige.Sum(Zaehle));

    // -- Ziele ("Neuer Block kommt ...") --------------------------------------------------------------------------------

    /// <summary>
    /// Moegliche Ziele fuer einen neuen Block bei dieser Wahl, das erste ist die Vorgabe: beim Block "nach diesem Block",
    /// "vor diesem Block" und je Zweig "in ... (ans Ende)"; beim Zweig "ans Ende" und "an den Anfang"; ohne Wahl ans Ende
    /// der obersten Folge (und an den Anfang, wenn schon etwas da ist).
    /// </summary>
    public IReadOnlyList<string> Ziele(string? gewaehlt)
    {
        var t = Teile(gewaehlt) ?? [];
        if (BlockAn(t) is { } b)
        {
            List<string> ziele = [EchtTexte.KommtNach, EchtTexte.KommtVor];
            ziele.AddRange(b.Block.Zweige.Select((z, i) => Beschriftungen.Einsetzen(EchtTexte.KommtInZweig, ZweigName(Fest(b.Block), i))));
            return ziele;
        }
        if (t.Length > 0 && AstAn(t) is not null)
        {
            return [EchtTexte.KommtZweigEnde, EchtTexte.KommtZweigAnfang];
        }
        return _wurzel.Bloecke.Count == 0 ? [EchtTexte.KommtEnde] : [EchtTexte.KommtEnde, EchtTexte.KommtAnfang];
    }

    /// <summary>
    /// Neuen Block einfuegen; <paramref name="ziel"/> ist die Nummer in <see cref="Ziele"/>. Gibt die Stelle des neuen
    /// Blocks zurueck, null wenn das Ziel nicht passt oder Zahl/Tiefe nicht reichen.
    /// </summary>
    public string? Einfuegen(string typ, string? gewaehlt, int ziel = 0)
    {
        if (!Bausteine.Contains(typ) || Zahl() >= BloeckeMax)
        {
            return null;
        }
        var t = Teile(gewaehlt) ?? [];
        Ast ast;
        int[] astPfad;
        int index;
        if (BlockAn(t) is { } b)
        {
            if (ziel is 0 or 1)
            {
                (ast, astPfad, index) = (b.Ast, t[..^1], b.Index + (ziel == 0 ? 1 : 0));
            }
            else if (ziel - 2 < b.Block.Zweige.Count)
            {
                (ast, astPfad) = (b.Block.Zweige[ziel - 2], [.. t, ziel - 2]);
                index = ast.Bloecke.Count;
            }
            else
            {
                return null;
            }
        }
        else if (AstAn(t) is { } a && ziel is 0 or 1)
        {
            (ast, astPfad, index) = (a, t, ziel == 0 ? a.Bloecke.Count : 0);
        }
        else
        {
            return null;
        }
        var neu = new Knoten(typ, "");
        var mitZweigen = typ is Verzweigung or Fallauswahl or Zaehlschleife or Solange or Wiederhole;
        if (Ebene(astPfad) + (mitZweigen ? 1 : 0) > TiefeMax)
        {
            return null;
        }
        switch (typ)
        {
            case Verzweigung:
                neu.Zweige.Add(new Ast(Ja, ""));
                neu.Zweige.Add(new Ast(Nein, ""));
                break;
            case Fallauswahl:
                for (var i = 0; i < FaelleMin; i++)
                {
                    neu.Zweige.Add(new Ast(Fall, ""));
                }
                neu.Zweige.Add(new Ast(Sonst, ""));
                break;
            case Zaehlschleife or Solange or Wiederhole:
                neu.Zweige.Add(new Ast(Rumpf, ""));
                break;
        }
        ast.Bloecke.Insert(index, neu);
        return Pfad([.. astPfad, index]);
    }

    // -- Aendern -------------------------------------------------------------------------------------------------------

    /// <summary>Text eines Blocks oder eines Falls setzen (unveraendert, wie eingegeben; ohne Zeilenumbruch).</summary>
    public bool Beschriften(string? pfad, string text)
    {
        if (text.Contains('\n', StringComparison.Ordinal) || text.Contains('\r', StringComparison.Ordinal) || text.Length > TextLaenge
            || Teile(pfad) is not { } t)
        {
            return false;
        }
        if (BlockAn(t) is { } b)
        {
            b.Block.Text = text;
            return true;
        }
        if (t.Length > 0 && AstAn(t) is { Art: Fall } fall)
        {
            fall.Text = text;
            return true;
        }
        return false;
    }

    /// <summary>Kann der Block eine Stelle nach oben (-1) bzw. unten (+1) innerhalb seines Zweigs?</summary>
    public bool Schiebbar(string? pfad, int richtung) => Teile(pfad) is { } t && BlockAn(t) is { } b
        && b.Index + richtung >= 0 && b.Index + richtung < b.Ast.Bloecke.Count;

    /// <summary>Block im selben Zweig um eine Stelle schieben; gibt die neue Stelle zurueck (oder null).</summary>
    public string? Schieben(string? pfad, int richtung)
    {
        if (!Schiebbar(pfad, richtung) || BlockAn(Teile(pfad)!) is not { } b)
        {
            return null;
        }
        var t = Teile(pfad)!;
        (b.Ast.Bloecke[b.Index], b.Ast.Bloecke[b.Index + richtung]) = (b.Ast.Bloecke[b.Index + richtung], b.Ast.Bloecke[b.Index]);
        return Pfad([.. t[..^1], b.Index + richtung]);
    }

    /// <summary>Kann der Block eine Ebene nach aussen (steht er in einem Zweig)?</summary>
    public bool Herausbar(string? pfad) => Teile(pfad) is { Length: >= 3 } t && BlockAn(t) is not null;

    /// <summary>Block eine Ebene nach aussen: direkt hinter den Block, in dessen Zweig er stand. Gibt die neue Stelle zurueck.</summary>
    public string? Heraus(string? pfad)
    {
        if (!Herausbar(pfad) || BlockAn(Teile(pfad)!) is not { } b)
        {
            return null;
        }
        var t = Teile(pfad)!;
        var aussen = t[..^2];
        var eltern = BlockAn(aussen)!.Value;
        b.Ast.Bloecke.RemoveAt(b.Index);
        eltern.Ast.Bloecke.Insert(eltern.Index + 1, b.Block);
        return Pfad([.. aussen[..^1], eltern.Index + 1]);
    }

    /// <summary>Wie viele Bloecke stecken in einem Block (ohne ihn selbst)? Fuer die Rueckfrage vor dem Entfernen.</summary>
    public int Inhalt(string? pfad) => Teile(pfad) is { } t && BlockAn(t) is { } b ? b.Block.Zweige.Sum(Zaehle) : 0;

    /// <summary>Block mit allem, was darin steht, entfernen.</summary>
    public bool Entfernen(string? pfad)
    {
        if (Teile(pfad) is not { } t || BlockAn(t) is not { } b)
        {
            return false;
        }
        b.Ast.Bloecke.RemoveAt(b.Index);
        return true;
    }

    /// <summary>Kann die Fallauswahl an dieser Stelle noch einen Fall bekommen?</summary>
    public bool FallMoeglich(string? pfad) => Teile(pfad) is { } t && BlockAn(t) is { Block.Typ: Fallauswahl } b
        && b.Block.Zweige.Count - 1 < FaelleMax;

    /// <summary>Neuen (leeren) Fall vor "sonst" anhaengen; gibt die Stelle des neuen Falls zurueck.</summary>
    public string? FallNeu(string? pfad)
    {
        if (!FallMoeglich(pfad) || BlockAn(Teile(pfad)!) is not { } b)
        {
            return null;
        }
        var index = b.Block.Zweige.Count - 1;
        b.Block.Zweige.Insert(index, new Ast(Fall, ""));
        return Pfad([.. Teile(pfad)!, index]);
    }

    /// <summary>Kann dieser Fall entfernt werden (Fall, nicht "sonst", und es bleiben genug Faelle)?</summary>
    public bool FallEntfernbar(string? pfad) => Teile(pfad) is { Length: > 0 } t && AstAn(t) is { Art: Fall }
        && BlockAn(t[..^1]) is { } b && b.Block.Zweige.Count - 1 > FaelleMin;

    /// <summary>Fall mit seinem Inhalt entfernen; gibt die Stelle der Fallauswahl zurueck.</summary>
    public string? FallEntfernen(string? pfad)
    {
        if (!FallEntfernbar(pfad))
        {
            return null;
        }
        var t = Teile(pfad)!;
        BlockAn(t[..^1])!.Value.Block.Zweige.RemoveAt(t[^1]);
        return Pfad(t[..^1]);
    }

    // -- Namen und Liste (Tastatur, Screenreader) ------------------------------------------------------------------------

    /// <summary>Name eines Bausteins ("Zählschleife").</summary>
    public static string Name(string typ) => typ switch
    {
        Anweisung => "Anweisung",
        Verzweigung => "Verzweigung",
        Fallauswahl => "Fallauswahl",
        Zaehlschleife => "Zählschleife",
        Solange => "Solange-Schleife (kopfgesteuert)",
        Wiederhole => "Wiederhole-Schleife (fußgesteuert)",
        Aufruf => "Aufruf",
        _ => typ,
    };

    /// <summary>Beschriftung des Knopfs je Baustein.</summary>
    public static string Knopf(string typ) => typ switch
    {
        Solange => "Solange (kopfgesteuert)",
        Wiederhole => "Wiederhole (fußgesteuert)",
        _ => Name(typ),
    };

    /// <summary>Mehrzahl fuer die Zaehlung ("Zählschleifen").</summary>
    public static string Mehrzahl(string typ) => typ switch
    {
        Anweisung => "Anweisungen",
        Verzweigung => "Verzweigungen",
        Fallauswahl => "Fallauswahlen",
        Zaehlschleife => "Zählschleifen",
        Solange => "Solange-Schleifen",
        Wiederhole => "Wiederhole-Schleifen",
        Aufruf => "Aufrufe",
        _ => typ,
    };

    /// <summary>Name des Textfelds eines Blocks ("Bedingung", "Schleifenkopf" ...).</summary>
    public static string Feldname(string typ) => typ switch
    {
        Verzweigung or Solange or Wiederhole => EchtTexte.FeldBedingung,
        Fallauswahl => EchtTexte.FeldAusdruck,
        Zaehlschleife => EchtTexte.FeldKopf,
        Aufruf => EchtTexte.FeldUnterprogramm,
        _ => EchtTexte.FeldAnweisung,
    };

    /// <summary>Name eines Zweigs eines Blocks: "Ja-Zweig", "Nein-Zweig", "Fall „1“" (ohne Text "Fall 1"), "Sonst", "Rumpf".</summary>
    public static string ZweigName(EchtBlock b, int i) => b.Zweige[i].Art switch
    {
        Ja => "Ja-Zweig",
        Nein => "Nein-Zweig",
        Fall => b.Zweige[i].Text.Length > 0 ? "Fall „" + b.Zweige[i].Text + "“" : "Fall " + (i + 1).ToString(CultureInfo.InvariantCulture),
        Sonst => "Sonst-Zweig",
        _ => "Rumpf",
    };

    /// <summary>Kurzname eines Blocks mit Text: "Verzweigung „differenz > 0“".</summary>
    public static string Kurz(EchtBlock b) => Name(b.Typ) + (b.Text.Length > 0 ? " „" + b.Text + "“" : "");

    private static string Anzahl(EchtZweig z, string name) => z.Bloecke.Count switch
    {
        0 => Beschriftungen.Einsetzen(EchtTexte.ZweigLeer, name),
        1 => Beschriftungen.Einsetzen(EchtTexte.BlockIn, name),
        var n => Beschriftungen.Einsetzen(EchtTexte.BloeckeIn, name, n),
    };

    /// <summary>
    /// Name einer Stelle fuer Liste und Screenreader, z. B. "Verzweigung „differenz > 0“, im Rumpf von Zählschleife „für i
    /// …“, Ja-Zweig: 2 Blöcke, Nein-Zweig: 1 Block" bzw. "Nein-Zweig von Verzweigung „differenz > 0“: leer".
    /// </summary>
    public string Beschreibung(string? pfad)
    {
        var t = Teile(pfad) ?? [];
        if (BlockAn(t) is { } b)
        {
            var block = Fest(b.Block);
            var teile = new List<string> { Kurz(block), Ort(t) };
            teile.AddRange(block.Zweige.Select((z, i) => Anzahl(z, ZweigName(block, i))));
            return string.Join(", ", teile);
        }
        if (t.Length > 0 && AstAn(t) is { } a && BlockAn(t[..^1]) is { } e)
        {
            var eltern = Fest(e.Block);
            return Anzahl(Fest(a), Beschriftungen.Einsetzen(EchtTexte.ZweigVon, ZweigName(eltern, t[^1]), Kurz(eltern)));
        }
        return "";
    }

    private string Ort(int[] t)
    {
        if (t.Length == 1)
        {
            return EchtTexte.Oben;
        }
        var eltern = Fest(BlockAn(t[..^2])!.Value.Block);
        return Beschriftungen.Einsetzen(EchtTexte.ImZweig, ZweigName(eltern, t[^2]), Kurz(eltern));
    }

    /// <summary>
    /// Alle Stellen von oben nach unten fuer die Liste "Deine Zeichnung": je Block eine Zeile, je Zweig eines Blocks eine Zeile
    /// (auch leere Zweige lassen sich so waehlen). Ebene = Einrueckung.
    /// </summary>
    public IReadOnlyList<(string Pfad, string Zeile, int Ebene)> Zeilen()
    {
        var zeilen = new List<(string, string, int)>();
        void Folge(Ast ast, int[] pfad, string nummer, int ebene)
        {
            for (var i = 0; i < ast.Bloecke.Count; i++)
            {
                int[] p = [.. pfad, i];
                var nr = nummer + (i + 1).ToString(CultureInfo.InvariantCulture);
                var block = Fest(ast.Bloecke[i]);
                zeilen.Add((Pfad(p), nr + "  " + Kurz(block), ebene));
                for (var z = 0; z < block.Zweige.Count; z++)
                {
                    if (block.Zweige.Count > 1)
                    {
                        zeilen.Add((Pfad([.. p, z]), Anzahl(block.Zweige[z], ZweigName(block, z)), ebene + 1));
                    }
                    Folge(ast.Bloecke[i].Zweige[z], [.. p, z], nr + ".", ebene + (block.Zweige.Count > 1 ? 2 : 1));
                }
            }
        }
        Folge(_wurzel, [], "", 0);
        return zeilen;
    }

    // -- Textfassung (Als Text zeigen, Screenreader) ---------------------------------------------------------------------

    /// <summary>Textfassung eines Struktogramms: je Block eine Zeile mit Nummer, Zweige darunter, leere Zweige "∅ (leer)".</summary>
    public static IReadOnlyList<string> Textfassung(IReadOnlyList<EchtBlock> bloecke)
    {
        var zeilen = new List<string> { EchtTexte.StruktogrammKopf };
        void Folge(IReadOnlyList<EchtBlock> folge, string nummer, string einzug)
        {
            for (var i = 0; i < folge.Count; i++)
            {
                var b = folge[i];
                var nr = nummer + (i + 1).ToString(CultureInfo.InvariantCulture);
                zeilen.Add(einzug + nr + " " + Name(b.Typ) + ": " + b.Text);
                for (var z = 0; z < b.Zweige.Count; z++)
                {
                    var innen = einzug + "    ";
                    if (b.Zweige.Count > 1)
                    {
                        zeilen.Add(innen + ZweigName(b, z) + ":");
                        innen += "    ";
                    }
                    if (b.Zweige[z].Bloecke.Count == 0)
                    {
                        zeilen.Add(innen + EchtTexte.LeerText);
                    }
                    Folge(b.Zweige[z].Bloecke, nr + ".", innen);
                }
            }
        }
        if (bloecke.Count == 0)
        {
            zeilen.Add(EchtTexte.LeerText);
        }
        Folge(bloecke, "", "");
        return zeilen;
    }

    // -- Zaehlung nach dem Abgeben (nur Struktur, keine Texte) -------------------------------------------------------------

    private static IEnumerable<(EchtBlock Block, string Ort, int Tiefe)> Alle(IReadOnlyList<EchtBlock> folge, string ort, int tiefe)
    {
        foreach (var b in folge)
        {
            yield return (b, ort, tiefe);
            for (var z = 0; z < b.Zweige.Count; z++)
            {
                var name = b.Zweige[z].Art switch
                {
                    Ja => "im Ja-Zweig einer Verzweigung",
                    Nein => "im Nein-Zweig einer Verzweigung",
                    Fall => "in einem Fall einer Fallauswahl",
                    Sonst => "im Sonst-Zweig einer Fallauswahl",
                    _ => "im Rumpf einer " + Name(b.Typ),
                };
                foreach (var x in Alle(b.Zweige[z].Bloecke, name, tiefe + 1))
                {
                    yield return x;
                }
            }
        }
    }

    private static int Leere(IReadOnlyList<EchtBlock> bloecke) => Alle(bloecke, "", 1).Sum(x => x.Block.Zweige.Count(z => z.Bloecke.Count == 0));

    /// <summary>
    /// Zaehlung der eigenen Zeichnung gegen das Musterbild (Plan P12-250 Abschnitt 4, Antwort 3a): Anzahl je Blockart,
    /// Schachtelung der Strukturbloecke (welcher Block in welchem Zweig), Zahl der Faelle, leere Zweige, tiefste Ebene.
    /// Texte werden nie verglichen; die Punkte setzt man selbst mit den Haekchen.
    /// </summary>
    public static IReadOnlyList<EchtZaehlung> Zaehlen(IReadOnlyList<EchtBlock> eigen, IReadOnlyList<EchtBlock> muster)
    {
        var zeilen = new List<EchtZaehlung>();
        var e = Alle(eigen, EchtTexte.Oben, 1).ToList();
        var m = Alle(muster, EchtTexte.Oben, 1).ToList();
        foreach (var typ in Bausteine)
        {
            var (a, b) = (e.Count(x => x.Block.Typ == typ), m.Count(x => x.Block.Typ == typ));
            if (a > 0 || b > 0)
            {
                zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlAnzahl, Mehrzahl(typ), a, b), a == b));
            }
        }
        // Schachtelung: nur Strukturbloecke (Anweisungen und Aufrufe zaehlt schon die erste Liste)
        static IEnumerable<string> Orte(IEnumerable<(EchtBlock Block, string Ort, int Tiefe)> alle) =>
            alle.Where(x => x.Block.Zweige.Count > 0).Select(x => Name(x.Block.Typ) + "\u0001" + x.Ort);
        var orteE = Orte(e).ToList();
        var orteM = Orte(m).ToList();
        foreach (var ort in orteM.Concat(orteE).Distinct(StringComparer.Ordinal))
        {
            var (a, b) = (orteE.Count(x => x == ort), orteM.Count(x => x == ort));
            var teile = ort.Split('\u0001');
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlOrt, teile[0], teile[1], a, b), a == b));
        }
        var faelleE = e.Where(x => x.Block.Typ == Fallauswahl).Sum(x => x.Block.Zweige.Count - 1);
        var faelleM = m.Where(x => x.Block.Typ == Fallauswahl).Sum(x => x.Block.Zweige.Count - 1);
        if (faelleE > 0 || faelleM > 0)
        {
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlFaelle, faelleE, faelleM), faelleE == faelleM));
        }
        var (leerE, leerM) = (Leere(eigen), Leere(muster));
        zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlLeer, leerE, leerM), leerE == leerM));
        var (tiefeE, tiefeM) = (e.Select(x => x.Tiefe).DefaultIfEmpty(0).Max(), m.Select(x => x.Tiefe).DefaultIfEmpty(0).Max());
        zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlTiefe, tiefeE, tiefeM), tiefeE == tiefeM));
        var ohneText = e.Where(x => x.Block.Text.Trim().Length == 0).Select(x => Name(x.Block.Typ)).ToList();
        if (ohneText.Count > 0)
        {
            zeilen.Add(new(Beschriftungen.Einsetzen(EchtTexte.ZaehlOhneText, string.Join(", ", ohneText.Distinct(StringComparer.Ordinal))), false));
        }
        return zeilen;
    }

    /// <summary>Tiefste Ebene eines Struktogramms (1 = nur oberste Folge, 0 = leer).</summary>
    public static int Tiefe(IReadOnlyList<EchtBlock> bloecke) => Alle(bloecke, "", 1).Select(x => x.Tiefe).DefaultIfEmpty(0).Max();
}
