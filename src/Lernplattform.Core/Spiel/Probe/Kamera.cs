using System.Numerics;

namespace Lernplattform.Core.Spiel.Probe;

/// <summary>
/// Kamera der 3D-Stichprobe: schaut schraeg von oben auf einen Zielpunkt am Boden (Y = 0).
/// Ziehen verschiebt das Ziel am Boden, Zoomen aendert den Abstand. Matrizen mit
/// System.Numerics (Zeilenvektoren); die Speicherfolge passt so ohne Umdrehen zu GLSL
/// (<c>uMvp * vec4(p, 1)</c>).
/// </summary>
public sealed class Kamera
{
    /// <summary>Naechster Abstand: 2 (Nachtrag 1 zu P7-12: die Figur ist jetzt 1,8 m = 0,36 hoch, vorher 5 bei 1,24).</summary>
    public const float AbstandMin = 2f;
    public const float AbstandMax = 40f;

    public Kamera(float zielX, float zielZ, float abstand)
    {
        ZielX = zielX;
        ZielZ = zielZ;
        Abstand = Math.Clamp(abstand, AbstandMin, AbstandMax);
    }

    public float ZielX { get; private set; }

    public float ZielZ { get; private set; }

    public float Abstand { get; private set; }

    /// <summary>Neigung ueber dem Boden in Grad.</summary>
    public float Neigung { get; init; } = 52f;

    /// <summary>Drehung um die Hochachse in Grad (0 = Blick nach -Z), W4: per zwei Finger oder Tasten Q/E.</summary>
    public float Drehung { get; set; } = 0f;

    public float Sichtfeld { get; init; } = 40f;

    /// <summary>Grenzen fuer das Ziel (Welt), damit man die Stadt nicht verliert.</summary>
    public (float X1, float Z1, float X2, float Z2) Grenzen { get; init; } = (-100, -100, 100, 100);

    public Vector3 Ziel => new(ZielX, 0, ZielZ);

    public Vector3 Auge
    {
        get
        {
            var n = Neigung * MathF.PI / 180f;
            var d = Drehung * MathF.PI / 180f;
            var waagrecht = Abstand * MathF.Cos(n);
            return Ziel + new Vector3(MathF.Sin(d) * waagrecht, Abstand * MathF.Sin(n), MathF.Cos(d) * waagrecht);
        }
    }

    public Matrix4x4 Sicht => Matrix4x4.CreateLookAt(Auge, Ziel, Vector3.UnitY);

    /// <summary>
    /// Naechste Ebene des Sichtraums: je weiter vorn, desto genauer der Tiefenpuffer (am Handy 16 Bit;
    /// mit 0,5 flimmerten Fussweg/Strasse und Wasser/Bruecke, Wunsch Nico 25.10.2026). Sie waechst mit
    /// dem Abstand (bei 5 wie bisher 1,5), damit die Kamera nah an die kleinere Figur kann, ohne vorn etwas
    /// abzuschneiden, und weit weg der Tiefenpuffer genau bleibt.
    /// </summary>
    public float Nah => Math.Max(0.3f, Abstand * 0.3f);

    /// <summary>
    /// Nachtrag 1 zu P7-13 (Wunsch 2): das Bild um diesen Anteil der halben Bildhoehe nach oben schieben
    /// (Bildraum, 0 = Ziel in der Mitte). So steht das Ziel (die Figur) in der Mitte des freien Bereichs
    /// zwischen Kopfzeile und Textkarte statt hinter der Karte; Tippen rechnet mit demselben Bild.
    /// </summary>
    public float Versatz { get; set; }

    public Matrix4x4 Projektion(float breite, float hoehe) =>
        Matrix4x4.CreatePerspectiveFieldOfView(Sichtfeld * MathF.PI / 180f, Math.Max(breite, 1) / Math.Max(hoehe, 1), Nah, 200f)
        * Matrix4x4.CreateTranslation(0, Versatz, 0);

    public void Setzen(float zielX, float zielZ)
    {
        ZielX = Math.Clamp(zielX, Grenzen.X1, Grenzen.X2);
        ZielZ = Math.Clamp(zielZ, Grenzen.Z1, Grenzen.Z2);
    }

    /// <summary>
    /// Ziehen um <paramref name="dx"/>/<paramref name="dy"/> Bildpunkte bei Bildhoehe <paramref name="hoehe"/>:
    /// der Boden folgt dem Finger ungefaehr (Massstab aus Abstand und Sichtfeld).
    /// </summary>
    public void Verschieben(float dx, float dy, float hoehe)
    {
        var proPunkt = 2f * Abstand * MathF.Tan(Sichtfeld * MathF.PI / 360f) / Math.Max(hoehe, 1);
        var d = Drehung * MathF.PI / 180f;
        var rechts = new Vector2(MathF.Cos(d), -MathF.Sin(d));
        var vor = new Vector2(-MathF.Sin(d), -MathF.Cos(d));
        var n = Neigung * MathF.PI / 180f;
        var schritt = (-dx * proPunkt * rechts) + (dy * proPunkt / MathF.Max(MathF.Sin(n), 0.3f) * vor);
        Setzen(ZielX + schritt.X, ZielZ + schritt.Y);
    }

    /// <summary>Faktor &gt; 1 = naeher heran.</summary>
    public void Zoomen(float faktor)
    {
        if (faktor > 0)
        {
            Abstand = Math.Clamp(Abstand / faktor, AbstandMin, AbstandMax);
        }
    }

    /// <summary>Um die Hochachse drehen (Grad), Ergebnis immer in [0, 360).</summary>
    public void Drehen(float grad)
    {
        Drehung = ((Drehung + grad) % 360f + 360f) % 360f;
    }

    /// <summary>Strahl vom Auge durch den Bildpunkt (x, y): Start und Richtung (Laenge 1) oder null.</summary>
    public (Vector3 Start, Vector3 Richtung)? Strahl(float x, float y, float breite, float hoehe)
    {
        if (!Matrix4x4.Invert(Sicht * Projektion(breite, hoehe), out var umgekehrt))
        {
            return null;
        }
        var nx = (2f * x / Math.Max(breite, 1)) - 1f;
        var ny = 1f - (2f * y / Math.Max(hoehe, 1));
        var nah = Vector4.Transform(new Vector4(nx, ny, 0f, 1f), umgekehrt);
        var fern = Vector4.Transform(new Vector4(nx, ny, 1f, 1f), umgekehrt);
        var a = new Vector3(nah.X, nah.Y, nah.Z) / nah.W;
        var b = new Vector3(fern.X, fern.Y, fern.Z) / fern.W;
        var r = b - a;
        return r.LengthSquared() < 1e-12f ? null : (a, Vector3.Normalize(r));
    }

    /// <summary>Bildpunkt eines Weltpunkts (x, y in Punkten) oder null, wenn er hinter der Kamera liegt.</summary>
    public (float X, float Y)? Bildpunkt(Vector3 p, float breite, float hoehe)
    {
        var v = Vector4.Transform(new Vector4(p, 1f), Sicht * Projektion(breite, hoehe));
        if (v.W <= 1e-6f)
        {
            return null;
        }
        return ((v.X / v.W + 1f) / 2f * breite, (1f - (v.Y / v.W)) / 2f * hoehe);
    }

    /// <summary>Punkt am Boden (Y = 0) unter dem Bildpunkt (x, y) oder null (Blick in den Himmel).</summary>
    public Vector2? Boden(float x, float y, float breite, float hoehe)
    {
        if (!Matrix4x4.Invert(Sicht * Projektion(breite, hoehe), out var umgekehrt))
        {
            return null;
        }
        var nx = (2f * x / Math.Max(breite, 1)) - 1f;
        var ny = 1f - (2f * y / Math.Max(hoehe, 1));
        var nah = Vector4.Transform(new Vector4(nx, ny, 0f, 1f), umgekehrt);
        var fern = Vector4.Transform(new Vector4(nx, ny, 1f, 1f), umgekehrt);
        var a = new Vector3(nah.X, nah.Y, nah.Z) / nah.W;
        var b = new Vector3(fern.X, fern.Y, fern.Z) / fern.W;
        if (MathF.Abs(b.Y - a.Y) < 1e-6f)
        {
            return null;
        }
        var t = -a.Y / (b.Y - a.Y);
        if (t < 0)
        {
            return null;
        }
        var p = a + ((b - a) * t);
        return new Vector2(p.X, p.Z);
    }
}
