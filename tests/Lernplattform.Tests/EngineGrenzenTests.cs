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
    /// <summary>Namensraeume der Engine in Lernplattform.Core.</summary>
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

    private static bool IstEngine(Type t)
    {
        var ns = (Aussen(t).Namespace) ?? "";
        return EngineNamensraeume.Any(e => ns == e || ns.StartsWith(e + ".", StringComparison.Ordinal));
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
        var core = typeof(StadtSzene).Assembly;
        var engine = core.GetTypes().Where(IstEngine).ToList();
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
        var core = typeof(StadtSzene).Assembly;
        Assert.DoesNotContain(core.GetReferencedAssemblies(), a => a.Name!.StartsWith("Stadtbau", StringComparison.Ordinal));
        var fremd = core.GetTypes().Where(t => t.Namespace is { } ns && !ns.StartsWith("Lernplattform.", StringComparison.Ordinal)
            && !ns.StartsWith("Microsoft.", StringComparison.Ordinal) && !ns.StartsWith("System.", StringComparison.Ordinal)).Select(t => t.FullName).ToList();
        Assert.Empty(fremd);

        var wurzel = Pfade.Wurzel;
        var projekte = Directory.GetDirectories(Path.Combine(wurzel, "src"), "Lernplattform.*");
        Assert.Equal(4, projekte.Length);
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
