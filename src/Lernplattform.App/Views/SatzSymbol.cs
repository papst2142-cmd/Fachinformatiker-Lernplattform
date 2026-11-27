using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Lernplattform.Design;

namespace Lernplattform.Views;

/// <summary>
/// A 1.17.0 (Weisung P12-430, Abnahmesaetze B2 bis B4): zweifarbiges Symbol aus <see cref="Symbole.Satz3"/>. Form in der
/// Grundfarbe (muted, gewaehlt accent), Akzent im zweiten Akzent der Farbwelt (accent2, wechselt mit Farbwelt und eigenen
/// Farben), mit einem Spalt in der Farbe der Flaeche darunter (<see cref="Grund"/>), ausgesparte Teile in derselben Farbe.
/// Wie im Entwurf P12-271/272: alle drei Farben kommen aus den Pinseln der Farbwelt (Ressourcen), nie fest.
/// </summary>
public sealed class SatzSymbol : Control
{
    public static readonly StyledProperty<IBrush?> FormProperty = AvaloniaProperty.Register<SatzSymbol, IBrush?>(nameof(Form));
    public static readonly StyledProperty<IBrush?> AkzentProperty = AvaloniaProperty.Register<SatzSymbol, IBrush?>(nameof(Akzent));
    public static readonly StyledProperty<IBrush?> GrundProperty = AvaloniaProperty.Register<SatzSymbol, IBrush?>(nameof(Grund));

    static SatzSymbol()
    {
        AffectsRender<SatzSymbol>(FormProperty, AkzentProperty, GrundProperty);
    }

    private readonly SymbolTeil[] _teile;
    private string _formPinsel = "";
    private string _grundPinsel = "";

    /// <summary>Symbol <paramref name="name"/> (Schluessel in <see cref="Symbole.Satz3"/>) mit Pinseln der Farbwelt.</summary>
    public SatzSymbol(string name, string formPinsel, string grundPinsel, double groesse)
    {
        if (!Symbole.Satz3.TryGetValue(name, out var teile))
        {
            throw new ArgumentException("Symbol fehlt in Satz 3: " + name, nameof(name));
        }
        Name = name;
        _teile = teile;
        Width = groesse;
        Height = groesse;
        FarbenSetzen(formPinsel, grundPinsel);
        this.Farbe(AkzentProperty, "Pinsel.accent2");
        Avalonia.Automation.AutomationProperties.SetAccessibilityView(this, Avalonia.Automation.AccessibilityView.Raw);
    }

    /// <summary>Schluessel des Symbols (z. B. START).</summary>
    public new string Name { get; }

    public IBrush? Form { get => GetValue(FormProperty); set => SetValue(FormProperty, value); }
    public IBrush? Akzent { get => GetValue(AkzentProperty); set => SetValue(AkzentProperty, value); }
    public IBrush? Grund { get => GetValue(GrundProperty); set => SetValue(GrundProperty, value); }

    /// <summary>Ressource der Grundfarbe (Start-Test und Tests: gewaehlt = Pinsel.accent).</summary>
    public string FormPinsel => _formPinsel;

    /// <summary>Ressource der Flaeche unter dem Symbol (Spalt und Aussparungen).</summary>
    public string GrundPinsel => _grundPinsel;

    /// <summary>Wechselt die Pinsel (Hervorhebung: Form accent, Grund card_hi).</summary>
    public void FarbenSetzen(string formPinsel, string grundPinsel)
    {
        if (formPinsel != _formPinsel)
        {
            _formPinsel = formPinsel;
            this.FarbeNeu(FormProperty, formPinsel);
        }
        if (grundPinsel != _grundPinsel)
        {
            _grundPinsel = grundPinsel;
            this.FarbeNeu(GrundProperty, grundPinsel);
        }
    }

    public override void Render(DrawingContext context)
    {
        var groesse = Math.Min(Bounds.Width, Bounds.Height);
        if (groesse <= 0)
        {
            return;
        }
        var faktor = groesse / 24;
        using var _ = context.PushTransform(Matrix.CreateScale(faktor, faktor)
            * Matrix.CreateTranslation((Bounds.Width - groesse) / 2, (Bounds.Height - groesse) / 2));
        var spalt = new Pen(Grund, Symbole.AkzentSpalt, lineJoin: PenLineJoin.Round);
        foreach (var teil in _teile)
        {
            switch (teil.Art)
            {
                case SymbolTeilArt.Form:
                    context.DrawGeometry(Form, null, teil.Geometrie);
                    break;
                case SymbolTeilArt.Akzent:
                    // wie paint-order: stroke im Entwurf: erst der Spalt, dann die Flaeche darueber
                    context.DrawGeometry(null, spalt, teil.Geometrie);
                    context.DrawGeometry(Akzent, null, teil.Geometrie);
                    break;
                case SymbolTeilArt.Grund:
                    context.DrawGeometry(Grund, null, teil.Geometrie);
                    break;
                case SymbolTeilArt.Linie:
                    context.DrawGeometry(null, new Pen(Grund, teil.Breite, lineCap: PenLineCap.Round), teil.Geometrie);
                    break;
            }
        }
    }
}
