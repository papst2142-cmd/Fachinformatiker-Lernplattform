using System.Reflection;
using System.Reflection.Emit;
using System.Text.RegularExpressions;
using Lernplattform.Core.Spiel.Probe;

namespace Lernplattform.Tests;

/// <summary>
/// Weisung P12-175 Punkt 2 (Engine festigen Stufe 1): die Trennlinie der eigenen Engine als Test.
/// Engine = der gemeinsame Teil von Spiel und Werkzeug: Namensraeume <c>Lernplattform.Core.Spiel.Probe</c> und
/// <c>Lernplattform.Core.Stadtbau</c> (Ordner <c>Core/Spiel/Probe/</c>, <c>Core/Stadtbau/</c>) und die Zeichenflaeche
/// <c>App/Spiel/StadtFlaeche*.cs</c> (siehe <c>docs/ENGINE.md</c>).
/// Regeln:
/// (1) Engine-Code nutzt keinen Namensraum des Lernteils (<see cref="Lernteil"/>), nichts aus <c>Stadtbau.*</c> (Werkzeug)
///     und keine Oberflaeche des Lernteils.
/// (2) Die Lernplattform enthaelt vom Werkzeug nichts ausser dem gemeinsamen Teil: keine Werkzeug-Assembly, kein Typ
///     ausserhalb von <c>Lernplattform.*</c>, kein Verweis und keine verlinkte Datei aus <c>src/Stadtbau.*</c>.
/// Geprueft wird im uebersetzten Code (IL jeder Methode, Felder, Signaturen, Basistypen) von Lernplattform.Core, damit
/// auch voll ausgeschriebene Namen und Namen ohne <c>using</c> zaehlen. Die App-Datei bindet dieses Testprojekt nicht ein,
/// dort gilt eine Quelltext-Regel (<see cref="AppFunde"/>). Jede Regel hat eine Gegenprobe.
/// </summary>
public class EngineGrenzenTests
{
    /// <summary>Weisung P12-179 Teil B: die Projekte Lernplattform.Engine, Lernplattform.Grundlagen und Lernplattform.Core.</summary>
    internal static Assembly EngineDll => typeof(StadtSzene).Assembly;

    internal static Assembly GrundlagenDll => typeof(Lernplattform.Core.Spiel.Py).Assembly;

    internal static Assembly CoreDll => typeof(Lernplattform.Core.Spiel.Spielstand).Assembly;

    /// <summary>Alle Typen der drei Projekte (in ihnen liegen Engine, Grundlagen, Spiel-Kern und Lernteil).</summary>
    internal static IEnumerable<Type> AlleTypen => new[] { EngineDll, GrundlagenDll, CoreDll }.Distinct().SelectMany(a => a.GetTypes());

    /// <summary>Namensraeume der Engine.</summary>
    internal static readonly string[] EngineNamensraeume = ["Lernplattform.Core.Spiel.Probe", "Lernplattform.Core.Stadtbau"];

    /// <summary>Namensraeume des Lernteils (Lernen, Pruefen, Auswerten); die Engine darf keinen davon nutzen.</summary>
    internal static readonly string[] Lernteil =
    [
        "Lernplattform.Core.Lernen", "Lernplattform.Core.Pruefung", "Lernplattform.Core.Dashboard",
        "Lernplattform.Core.Rechner", "Lernplattform.Core.Inhalte", "Lernplattform.Core.Hilfe",
        "Lernplattform.Core.Fortschritt", "Lernplattform.Core.Suche", "Lernplattform.Core.Projekt",
        // Oberflaeche des Lernteils (App)
        "Lernplattform.Views", "Lernplattform.ViewModels", "Lernplattform.Messung", "Lernplattform.Programm",
    ];

    /// <summary>
    /// Feste, benannte Ausnahmen (Engine-Typ, genutzter Typ, Grund). Heute leer: die Engine nutzt keinen Typ des Lernteils.
    /// Neue Eintraege nur mit Weisung.
    /// </summary>
    internal static readonly (string Engine, string Genutzt, string Grund)[] Ausnahmen = [];

    /// <summary>
    /// Weisung P12-179 Schritt 1: die Grundlagen, die Engine und Spiel-Kern beide nutzen (Python-Nachbau, Datenhelfer,
    /// Zeit-Typen, Laufraster). Sie nutzen nur sich selbst (Test <see cref="Grundlagen_NutzenNurSichSelbst"/>); ab Teil B
    /// liegen sie in einem eigenen Projekt. Neue Eintraege nur, wenn der Typ geschlossen bleibt.
    /// </summary>
    internal static readonly string[] Grundlagen =
    [
        "Lernplattform.Core.Spiel.Py", "Lernplattform.Core.Spiel.PyDict", "Lernplattform.Core.Spiel.PyFormat",
        "Lernplattform.Core.Spiel.PyMenge", "Lernplattform.Core.Spiel.PySet", "Lernplattform.Core.Spiel.PyTupel",
        "Lernplattform.Core.Spiel.PyVergleich", "Lernplattform.Core.Daten.PyObjekt", "Lernplattform.Core.Daten.PythonJson",
        "Lernplattform.Core.Mathe.PythonRunden", "Lernplattform.Core.Mathe.PythonRundenStellen", "Lernplattform.Core.Mathe.PythonPow",
        "Lernplattform.Core.Dashboard.PyText", "Lernplattform.Core.Rechner.PythonZeichen", "Lernplattform.Core.Suche.PythonKlein",
        "Lernplattform.Core.Spiel.Jahreszeit", "Lernplattform.Core.Spiel.Festzeit", "Lernplattform.Core.Spiel.SpielDaten",
        "Lernplattform.Core.Spiel.SpielInhalt", "Lernplattform.Core.Spiel.Wege", "Lernplattform.Core.Spiel.LaufOrt",
        "Lernplattform.Core.Spiel.FreiFeld", "Lernplattform.Core.Spiel.Wegsuche", "Lernplattform.Core.Spiel.ModellKontext",
        "Lernplattform.Core.Spiel.Aussehen", "Lernplattform.Core.Spiel.StadtModelle", "Lernplattform.Core.Spiel.ISpielSicht",
        "Lernplattform.Core.Spiel.SpielFehler", "Lernplattform.Core.Spiel.PyAusgabe", "Lernplattform.Core.Spiel.Jahreszeiten",
    ];

    /// <summary>
    /// Weisung P12-179 (E1): Typen mit dem Namensraum der Engine, die das Spiel selbst fuehren und darum beim Spiel-Kern
    /// bleiben (Spielanbindung): die laufende Stadt (Schritte, Ton, Kassenbuch) und die Merker der Tastatur und Hinweise.
    /// Ab Teil B liegen sie in <c>Lernplattform.Core</c>, nicht im Engine-Projekt.
    /// </summary>
    internal static readonly string[] Spielanbindung =
    [
        "Lernplattform.Core.Spiel.Probe.StadtSpiel", "Lernplattform.Core.Spiel.Probe.HinweisWahl",
        "Lernplattform.Core.Spiel.Probe.Tastenbelegung", "Lernplattform.Core.Stadtbau.StadtbauSpiel",
    ];

    private static bool IstEngine(Type t)
    {
        var ns = (Aussen(t).Namespace) ?? "";
        return EngineNamensraeume.Any(e => ns == e || ns.StartsWith(e + ".", StringComparison.Ordinal));
    }

    /// <summary>Engine ohne Spielanbindung (Weisung P12-179).</summary>
    internal static bool IstEngineOhneAnbindung(Type t) => IstEngine(t) && !Spielanbindung.Contains(Aussen(t).FullName);

    /// <summary>
    /// Weisung P12-179 Schritt 1 und 2: die Engine kennt den Spiel-Kern nicht. Jeder Typ aus <c>Lernplattform.*</c>, den ein
    /// Engine-Typ nutzt (Signaturen, Felder, IL), ist selbst Engine oder Grundlage; was die Engine vom Spiel braucht, kommt
    /// ueber die Schnittstellen <see cref="IStadtSicht"/>, <see cref="ISpielSicht"/> und <see cref="ISpielRegeln"/>.
    /// </summary>
    [Fact]
    public void Engine_KenntDenSpielKernNicht_NurGrundlagen()
    {
        var engine = AlleTypen.Where(IstEngineOhneAnbindung).ToList();
        Assert.True(engine.Count > 100, $"nur {engine.Count} Engine-Typen gefunden");
        var funde = FremdeTypen(engine, t => IstEngineOhneAnbindung(t) || Grundlagen.Contains(Aussen(t).FullName)).ToList();
        Assert.True(funde.Count == 0, "Engine nutzt den Spiel-Kern:\n" + string.Join("\n", funde.Select(f => $"{f.Engine} -> {f.Genutzt}")));
    }

    /// <summary>Die Grundlagen nutzen nur sich selbst (sonst zoegen sie den Spiel-Kern oder den Lernteil mit).</summary>
    [Fact]
    public void Grundlagen_NutzenNurSichSelbst()
    {
        var typen = AlleTypen.Where(t => Grundlagen.Contains(Aussen(t).FullName)).ToList();
        Assert.Equal(Grundlagen.Length, typen.Select(t => Aussen(t).FullName).Distinct().Count());
        var funde = FremdeTypen(typen, t => Grundlagen.Contains(Aussen(t).FullName)).ToList();
        Assert.True(funde.Count == 0, "Grundlage nutzt mehr:\n" + string.Join("\n", funde.Select(f => $"{f.Engine} -> {f.Genutzt}")));
    }

    /// <summary>Gegenprobe: ein Typ, der den Spiel-Kern (Spielstand, Stadt) nutzt, wird gefunden; Grundlagen nicht.</summary>
    [Fact]
    public void Gegenprobe_SpielKern_WirdGefunden()
    {
        var funde = FremdeTypen([typeof(KenntSpielKern)], t => Grundlagen.Contains(Aussen(t).FullName)).Select(f => f.Genutzt).ToList();
        Assert.Contains(typeof(Lernplattform.Core.Spiel.Spielstand).FullName!, funde);
        Assert.Contains(typeof(Lernplattform.Core.Spiel.Stadt).FullName!, funde);
        Assert.DoesNotContain(typeof(Lernplattform.Core.Spiel.Py).FullName!, funde);
        Assert.DoesNotContain(funde, f => f.StartsWith("System.", StringComparison.Ordinal));
    }

    private sealed class KenntSpielKern
    {
#pragma warning disable CS0649, CS0169
        private Lernplattform.Core.Spiel.Spielstand? _stand;
#pragma warning restore CS0649, CS0169
        public static int Zahl(Lernplattform.Core.Spiel.Stadt s) => Lernplattform.Core.Spiel.Py.Int(1L) > 0 ? s.GetHashCode() : 0;
    }

    /// <summary>Genutzte Typen aus Lernplattform.Core, die <paramref name="erlaubt"/> nicht zulaesst (ohne eigene Typen des Compilers).</summary>
    internal static IEnumerable<(string Engine, string Genutzt)> FremdeTypen(IEnumerable<Type> typen, Func<Type, bool> erlaubt)
    {
        var eigene = new[] { EngineDll, GrundlagenDll, CoreDll };
        var gesehen = new HashSet<(string, string)>();
        foreach (var t in typen)
        {
            foreach (var g in Genutzt(t).OfType<Type>().SelectMany(Zerlegen))
            {
                var a = Aussen(g);
                if (!eigene.Contains(a.Assembly) || a.FullName is null || a.FullName.Contains('<') || a.Namespace is null
                    || a.Namespace.StartsWith("System.", StringComparison.Ordinal) || Aussen(t) == a || erlaubt(g))
                {
                    continue;
                }
                if (gesehen.Add((Aussen(t).FullName!, a.FullName)))
                {
                    yield return (Aussen(t).FullName!, a.FullName);
                }
            }
        }
    }

    private static Type Aussen(Type t)
    {
        while (t.DeclaringType is { } d)
        {
            t = d;
        }
        return t;
    }

    private static bool Verboten(string? ns) => ns is not null && (ns == "Stadtbau" || ns.StartsWith("Stadtbau.", StringComparison.Ordinal)
        || Lernteil.Any(l => ns == l || ns.StartsWith(l + ".", StringComparison.Ordinal)));

    [Fact]
    public void Engine_NutztKeinenLernteil_UndNichtsVomWerkzeug()
    {
        var engine = AlleTypen.Where(IstEngine).ToList();
        // Gegenprobe der Auswahl: die Engine ist da und die Zuordnung trifft Typen beider Ordner
        Assert.Contains(typeof(StadtSzene), engine);
        Assert.Contains(typeof(Lernplattform.Core.Stadtbau.StadtFassung), engine);
        Assert.True(engine.Count > 100, $"nur {engine.Count} Engine-Typen gefunden");

        var funde = Funde(engine).Where(f => !Ausnahmen.Any(a => a.Engine == f.Engine && a.Genutzt == f.Genutzt)).ToList();
        Assert.True(funde.Count == 0, "Engine nutzt Lernteil oder Werkzeug:\n" + string.Join("\n", funde.Select(f => $"{f.Engine} -> {f.Genutzt}")));
    }

    /// <summary>Gegenprobe: ein Typ mit verbotenen Verweisen (Feld, Methode, Rueckgabe) wird gefunden.</summary>
    [Fact]
    public void Gegenprobe_EingeschleusterVerweis_WirdGefunden()
    {
        var funde = Funde([typeof(Eingeschleust)]).Select(f => f.Genutzt).ToList();
        Assert.Contains(typeof(Lernplattform.Core.Inhalte.Lerninhalte).FullName!, funde);
        Assert.Contains(typeof(Lernplattform.Core.Dashboard.DashboardWerte).FullName!, funde);
        // ein erlaubter Verweis (Spiel-Kern, System) zaehlt nicht
        Assert.DoesNotContain(funde, f => f.StartsWith("System.", StringComparison.Ordinal) || f.StartsWith("Lernplattform.Core.Spiel.", StringComparison.Ordinal));
        // und die Pruefung selbst nennt den Namensraum als verboten
        Assert.True(Verboten("Stadtbau.Core"));
        Assert.True(Verboten("Lernplattform.Core.Pruefung"));
        Assert.False(Verboten("Lernplattform.Core.Spiel"));
    }

    private sealed class Eingeschleust
    {
#pragma warning disable CS0649, CS0169
        private Lernplattform.Core.Dashboard.DashboardWerte? _werte;
#pragma warning restore CS0649, CS0169
        public static int Zahl() => typeof(Lernplattform.Core.Inhalte.Lerninhalte).Name.Length + Math.Abs(-1);
    }

    /// <summary>Alle verbotenen Typverweise der gegebenen Typen (aus Signaturen und IL).</summary>
    internal static IEnumerable<(string Engine, string Genutzt)> Funde(IEnumerable<Type> typen)
    {
        var gesehen = new HashSet<(string, string)>();
        foreach (var t in typen)
        {
            foreach (var g in Genutzt(t).OfType<Type>().SelectMany(Zerlegen))
            {
                if (Verboten(g.Namespace) && gesehen.Add((Aussen(t).FullName!, g.FullName ?? g.Name)))
                {
                    yield return (Aussen(t).FullName!, g.FullName ?? g.Name);
                }
            }
        }
    }

    private static IEnumerable<Type> Zerlegen(Type t)
    {
        if (t.HasElementType)
        {
            foreach (var e in Zerlegen(t.GetElementType()!))
            {
                yield return e;
            }
            yield break;
        }
        if (t.IsGenericType && !t.IsGenericTypeDefinition)
        {
            yield return t.GetGenericTypeDefinition();
            foreach (var a in t.GetGenericArguments().SelectMany(Zerlegen))
            {
                yield return a;
            }
            yield break;
        }
        if (!t.IsGenericParameter)
        {
            yield return t;
        }
    }

    private const BindingFlags Alle = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static IEnumerable<Type?> Genutzt(Type t)
    {
        yield return t.BaseType;
        foreach (var i in t.GetInterfaces())
        {
            yield return i;
        }
        foreach (var f in t.GetFields(Alle))
        {
            yield return f.FieldType;
        }
        foreach (var p in t.GetProperties(Alle))
        {
            yield return p.PropertyType;
        }
        foreach (var m in t.GetMethods(Alle).Cast<MethodBase>().Concat(t.GetConstructors(Alle)))
        {
            if (m is MethodInfo mi)
            {
                yield return mi.ReturnType;
            }
            foreach (var p in m.GetParameters())
            {
                yield return p.ParameterType;
            }
            foreach (var g in AusIl(m))
            {
                yield return g;
            }
        }
    }

    private static readonly Dictionary<short, OpCode> Befehle = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!).ToDictionary(o => o.Value);

    /// <summary>Typen hinter allen Token im IL einer Methode (Typ, Feld, Methode, Typ-Token von ldtoken/typeof).</summary>
    private static IEnumerable<Type?> AusIl(MethodBase m)
    {
        var il = m.GetMethodBody()?.GetILAsByteArray();
        if (il is null)
        {
            yield break;
        }
        var modul = m.Module;
        Type[]? typArg = m.DeclaringType is { IsGenericType: true } dt ? dt.GetGenericArguments() : null;
        Type[]? methArg = m.IsGenericMethod ? m.GetGenericArguments() : null;
        var i = 0;
        while (i < il.Length)
        {
            short wert = il[i++];
            if (wert == 0xFE)
            {
                wert = unchecked((short)(0xFE00 | il[i++]));
            }
            var op = Befehle[wert];
            switch (op.OperandType)
            {
                case OperandType.InlineNone:
                    break;
                case OperandType.ShortInlineBrTarget:
                case OperandType.ShortInlineI:
                case OperandType.ShortInlineVar:
                    i += 1;
                    break;
                case OperandType.InlineVar:
                    i += 2;
                    break;
                case OperandType.InlineI8:
                case OperandType.InlineR:
                    i += 8;
                    break;
                case OperandType.InlineSwitch:
                    var n = BitConverter.ToInt32(il, i);
                    i += 4 + 4 * n;
                    break;
                case OperandType.InlineType:
                case OperandType.InlineField:
                case OperandType.InlineMethod:
                case OperandType.InlineTok:
                    var token = BitConverter.ToInt32(il, i);
                    i += 4;
                    MemberInfo? ziel;
                    try
                    {
                        ziel = modul.ResolveMember(token, typArg, methArg);
                    }
                    catch (ArgumentException)
                    {
                        ziel = null;
                    }
                    switch (ziel)
                    {
                        case Type ty:
                            yield return ty;
                            break;
                        case FieldInfo fi:
                            yield return fi.DeclaringType;
                            yield return fi.FieldType;
                            break;
                        case MethodBase mb:
                            yield return mb.DeclaringType;
                            if (mb is MethodInfo r)
                            {
                                yield return r.ReturnType;
                            }
                            foreach (var p in mb.GetParameters())
                            {
                                yield return p.ParameterType;
                            }
                            break;
                    }
                    break;
                default:
                    // InlineBrTarget, InlineI, InlineString, InlineSig, ShortInlineR
                    i += 4;
                    break;
            }
        }
    }

    /// <summary>
    /// Die Zeichenflaeche der Engine (<c>App/Spiel/StadtFlaeche*.cs</c>) liegt in der App, die dieses Testprojekt nicht
    /// einbindet. Regel im Quelltext: jede <c>using</c>-Zeile und jeder voll ausgeschriebene Name <c>Lernplattform.…</c>
    /// oder <c>Stadtbau.…</c> liegt nicht im Lernteil oder Werkzeug; dazu kein Bezeichner, der als Typ nur im Wurzel-
    /// Namensraum <c>Lernplattform</c> der App steht (der ist aus <c>Lernplattform.Spiel</c> ohne <c>using</c> sichtbar).
    /// </summary>
    [Fact]
    public void StadtFlaeche_NutztKeinenLernteil_UndNichtsVomWerkzeug()
    {
        var app = Path.Combine(Pfade.Wurzel, "src", "Lernplattform.App");
        var dateien = Directory.GetFiles(Path.Combine(app, "Spiel"), "StadtFlaeche*.cs");
        Assert.True(dateien.Length >= 2, "StadtFlaeche*.cs nicht gefunden");
        var wurzelTypen = WurzelTypen(app);
        Assert.Contains("App", wurzelTypen);
        var funde = dateien.SelectMany(d => AppFunde(File.ReadAllText(d), wurzelTypen).Select(f => $"{Path.GetFileName(d)}: {f}")).ToList();
        Assert.True(funde.Count == 0, "StadtFlaeche nutzt Lernteil oder Werkzeug:\n" + string.Join("\n", funde));
    }

    [Fact]
    public void Gegenprobe_StadtFlaeche_EingeschleusteZeilen_WerdenGefunden()
    {
        var wurzelTypen = new HashSet<string> { "MainView" };
        Assert.Empty(AppFunde("using System;\nusing Lernplattform.Core.Spiel.Probe;\nvar x = Lernplattform.Core.Spiel.Py.Gleich(1, 1);", wurzelTypen));
        Assert.Single(AppFunde("using Lernplattform.Core.Lernen;", wurzelTypen));
        Assert.Single(AppFunde("using Lernplattform.Views;", wurzelTypen));
        Assert.Single(AppFunde("var k = Stadtbau.Core.Katalog.Praefix;", wurzelTypen));
        Assert.Single(AppFunde("var w = Lernplattform.Core.Dashboard.DashboardWerte.Leer;", wurzelTypen));
        Assert.Single(AppFunde("var m = MainView.Aktuell;", wurzelTypen));
        // in Kommentaren und Texten zaehlt nichts
        Assert.Empty(AppFunde("// using Lernplattform.Core.Lernen;\nvar s = \"MainView Stadtbau.Core\";", wurzelTypen));
    }

    /// <summary>Typnamen, die im Wurzel-Namensraum <c>Lernplattform</c> der App deklariert sind.</summary>
    internal static HashSet<string> WurzelTypen(string app)
    {
        var namen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var d in Directory.EnumerateFiles(app, "*.cs", SearchOption.AllDirectories)
                     .Where(d => !d.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(t => t is "bin" or "obj")))
        {
            var text = File.ReadAllText(d);
            if (!Regex.IsMatch(text, @"^namespace Lernplattform\s*;", RegexOptions.Multiline))
            {
                continue;
            }
            foreach (Match m in Regex.Matches(text, @"\b(?:class|record|struct|enum|interface)\s+(\w+)"))
            {
                namen.Add(m.Groups[1].Value);
            }
        }
        return namen;
    }

    internal static List<string> AppFunde(string quelltext, HashSet<string> wurzelTypen)
    {
        var text = Regex.Replace(quelltext, @"//[^\n]*", "");
        text = Regex.Replace(text, @"/\*.*?\*/", "", RegexOptions.Singleline);
        text = Regex.Replace(text, @"@?""(?:\\.|[^""\\])*""", "\"\"");
        var funde = new List<string>();
        foreach (Match m in Regex.Matches(text, @"\b((?:Lernplattform|Stadtbau)(?:\.\w+)+)"))
        {
            var name = m.Groups[1].Value;
            if (Verboten(name) || Verboten(string.Join('.', name.Split('.')[..^1])))
            {
                funde.Add(name);
            }
        }
        foreach (Match m in Regex.Matches(text, @"(?<![\w.])([A-Z]\w*)\b"))
        {
            if (wurzelTypen.Contains(m.Groups[1].Value))
            {
                funde.Add("Lernplattform." + m.Groups[1].Value);
            }
        }
        return funde.Distinct().ToList();
    }

    /// <summary>
    /// Regel (2): die Lernplattform enthaelt vom Werkzeug nichts ausser dem gemeinsamen Teil. Lernplattform.Core bindet
    /// keine Werkzeug-Assembly ein, jeder Typ liegt in <c>Lernplattform.*</c>; kein Projekt <c>src/Lernplattform.*</c>
    /// verweist auf ein Werkzeug-Projekt oder bindet eine Datei aus <c>src/Stadtbau.*</c> ein. Feste Ausnahme:
    /// <c>InternalsVisibleTo</c> (das Werkzeug darf hineinsehen, die Lernplattform nimmt dabei nichts mit).
    /// </summary>
    [Fact]
    public void Lernplattform_EnthaeltVomWerkzeugNurDenGemeinsamenTeil()
    {
        foreach (var dll in new[] { EngineDll, GrundlagenDll, CoreDll })
        {
            Assert.DoesNotContain(dll.GetReferencedAssemblies(), a => a.Name!.StartsWith("Stadtbau", StringComparison.Ordinal));
        }
        var fremd = AlleTypen.Where(t => t.Namespace is { } ns && !ns.StartsWith("Lernplattform.", StringComparison.Ordinal)
            && !ns.StartsWith("Microsoft.", StringComparison.Ordinal) && !ns.StartsWith("System.", StringComparison.Ordinal)).Select(t => t.FullName).ToList();
        Assert.Empty(fremd);

        var wurzel = Pfade.Wurzel;
        var projekte = Directory.GetDirectories(Path.Combine(wurzel, "src"), "Lernplattform.*");
        Assert.Equal(6, projekte.Length);
        var funde = projekte.SelectMany(p => Directory.GetFiles(p, "*.csproj")).SelectMany(d => CsprojFunde(File.ReadAllText(d))
            .Select(f => $"{Path.GetFileName(d)}: {f}")).ToList();
        Assert.True(funde.Count == 0, string.Join("\n", funde));
    }

    [Fact]
    public void Gegenprobe_Csproj_VerweisAufsWerkzeug_WirdGefunden()
    {
        Assert.Empty(CsprojFunde("<InternalsVisibleTo Include=\"Stadtbau.App\" />"));
        Assert.Single(CsprojFunde("<ProjectReference Include=\"..\\Stadtbau.Core\\Stadtbau.Core.csproj\" />"));
        Assert.Single(CsprojFunde("<Compile Include=\"../Stadtbau.Desktop/Updatequelle.cs\" Link=\"x.cs\" />"));
        Assert.Single(CsprojFunde("<EmbeddedResource Include=\"..\\..\\inhalte_stadtbau\\katalog.json\" />"));
    }

    /// <summary>
    /// Weisung P12-179 Schritt 3: die Trennlinie auch auf Projektebene. Die Engine verweist nur auf die Grundlagen, die
    /// Grundlagen auf kein eigenes Projekt; Lernplattform.Core und Stadtbau.Core verweisen auf die Engine. Geprueft in den
    /// Projektdateien (ProjectReference) und in den gebauten Dateien (Verweise der DLL). Jeder Typ mit dem Namensraum der
    /// Engine liegt in der Engine (ausser der Spielanbindung, die in Lernplattform.Core liegt), jede Grundlage in den Grundlagen.
    /// </summary>
    [Fact]
    public void Projekte_EngineKenntNurDieGrundlagen()
    {
        var wurzel = Pfade.Wurzel;
        string Text(string projekt) => File.ReadAllText(Path.Combine(wurzel, "src", projekt, projekt + ".csproj"));
        Assert.Empty(ProjektVerweisFunde(Text("Lernplattform.Engine"), ["Lernplattform.Grundlagen"]));
        Assert.Empty(ProjektVerweisFunde(Text("Lernplattform.Grundlagen"), []));
        Assert.Equal(["Lernplattform.Engine", "Lernplattform.Grundlagen"], ProjektVerweise(Text("Lernplattform.Core")).Order(StringComparer.Ordinal));
        Assert.Contains("Lernplattform.Engine", ProjektVerweise(Text("Stadtbau.Core")));

        static List<string> Eigene(Assembly a) => [.. a.GetReferencedAssemblies().Select(n => n.Name!)
            .Where(n => n.StartsWith("Lernplattform", StringComparison.Ordinal) || n.StartsWith("Stadtbau", StringComparison.Ordinal)).Order(StringComparer.Ordinal)];
        Assert.Equal(["Lernplattform.Grundlagen"], Eigene(EngineDll));
        Assert.Empty(Eigene(GrundlagenDll));
        Assert.Equal(["Lernplattform.Engine", "Lernplattform.Grundlagen"], Eigene(CoreDll));
        Assert.Equal("Lernplattform.Engine", EngineDll.GetName().Name);
        Assert.Equal("Lernplattform.Grundlagen", GrundlagenDll.GetName().Name);

        var falsch = AlleTypen.Where(t => t.FullName is { } n && !n.Contains('<') && t.DeclaringType is null).Where(t =>
            (IstEngineOhneAnbindung(t) && t.Assembly != EngineDll)
            || (Spielanbindung.Contains(t.FullName) && t.Assembly != CoreDll)
            || (Grundlagen.Contains(t.FullName) != (t.Assembly == GrundlagenDll))
            || (t.Assembly == EngineDll && !IstEngineOhneAnbindung(t))).Select(t => $"{t.FullName} in {t.Assembly.GetName().Name}").ToList();
        Assert.True(falsch.Count == 0, "Typ im falschen Projekt:\n" + string.Join("\n", falsch));
    }

    /// <summary>Gegenprobe: ein Verweis der Engine auf Lernplattform.Core (oder das Werkzeug) wird gefunden.</summary>
    [Fact]
    public void Gegenprobe_EngineVerweistAufCore_WirdGefunden()
    {
        var mitCore = "<ItemGroup>\n<ProjectReference Include=\"..\\Lernplattform.Grundlagen\\Lernplattform.Grundlagen.csproj\" />\n"
            + "<ProjectReference Include=\"..\\Lernplattform.Core\\Lernplattform.Core.csproj\" />\n</ItemGroup>";
        Assert.Equal(["Lernplattform.Core"], ProjektVerweisFunde(mitCore, ["Lernplattform.Grundlagen"]));
        Assert.Equal(["Stadtbau.Core"], ProjektVerweisFunde("<ProjectReference Include=\"../Stadtbau.Core/Stadtbau.Core.csproj\"/>", ["Lernplattform.Grundlagen"]));
        Assert.Empty(ProjektVerweisFunde(File.ReadAllText(Path.Combine(Pfade.Wurzel, "src", "Lernplattform.Engine", "Lernplattform.Engine.csproj")),
            ["Lernplattform.Grundlagen"]));
    }

    /// <summary>Namen der Projekte, auf die eine Projektdatei verweist (ProjectReference).</summary>
    internal static List<string> ProjektVerweise(string csproj) =>
        [.. Regex.Matches(csproj, @"<ProjectReference\s+Include=""([^""]*)""").Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')))];

    /// <summary>Verweise einer Projektdatei, die nicht in <paramref name="erlaubt"/> stehen.</summary>
    internal static List<string> ProjektVerweisFunde(string csproj, IReadOnlyCollection<string> erlaubt) =>
        [.. ProjektVerweise(csproj).Where(v => !erlaubt.Contains(v))];

    internal static List<string> CsprojFunde(string csproj)
    {
        var funde = new List<string>();
        foreach (Match m in Regex.Matches(csproj, @"<(\w+)\s+(?:Include|Update)=""([^""]*)"""))
        {
            if (m.Groups[1].Value == "InternalsVisibleTo")
            {
                continue;
            }
            if (Regex.IsMatch(m.Groups[2].Value, @"(^|[\\/])(Stadtbau\.|inhalte_stadtbau)"))
            {
                funde.Add($"{m.Groups[1].Value} {m.Groups[2].Value}");
            }
        }
        return funde;
    }
}
