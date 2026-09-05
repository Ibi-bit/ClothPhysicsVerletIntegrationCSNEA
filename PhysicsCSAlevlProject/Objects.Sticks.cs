using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

/// <summary>
/// The Stick class represents a connection between two particles in the physics simulation, defined by its endpoints (P1 and P2) and its natural length. The DrawableStick class extends Stick by adding visual properties such as color and width, and includes a method to draw itself using a SpriteBatch and PrimitiveBatch. The IsCut property allows for simulating stick breakage, where a stick can be marked as cut to prevent it from being drawn or participating in physics interactions. This design allows for flexible representation of connections between particles, supporting both intact and broken sticks while also providing visual feedback for rendering the mesh structure.
/// </summary>
class Stick
{
    /// <summary>
    /// the inital particle the stick is connected to
    /// </summary>
    public Particle P1;

    /// <summary>
    /// the final particle the stick is connected to
    /// </summary>
    public Particle P2;

    /// <summary>
    /// the natural length of the stick, which is the distance between the two particles when the stick is at rest
    /// </summary>
    public float Length;
    public float Compliance { get; set; } = 0.0f;
    public float Lambda { get; set; } = 0.0f;

    public Stick()
    {
        P1 = null;
        P2 = null;
        Length = 0f;
    }

    public Stick(Particle p1, Particle p2)
    {
        P1 = p1;
        P2 = p2;
        Length = Vector2.Distance(p1.Position, p2.Position);
    }
}

class DrawableStick : Stick
{
    /// <summary>
    /// the line object used for drawing the stick, which is created based on the positions of the two particles
    /// </summary>
    private PrimitiveBatch.Line line;
    public Color Color { get; set; }
    public float Width { get; set; }
    public bool IsCut { get; set; }

    public DrawableStick()
        : base()
    {
        Color = Color.White;
        Width = 2.0f;
        IsCut = false;
    }

    public DrawableStick(Particle p1, Particle p2, Color color, float width = 2.0f)
        : base(p1, p2)
    {
        Color = color;
        Width = width;
        UpdateLine();
    }

    public DrawableStick(Particle p1, Particle p2, Color color)
        : base(p1, p2)
    {
        Color = color;
        Width = 2.0f;
        UpdateLine();
    }

    private void UpdateLine()
    {
        line = new PrimitiveBatch.Line(P1.Position, P2.Position, Color, Width);
    }

    public void Draw(
        float stickDrawThickness = -1
    )
    {
        float w = -1 != stickDrawThickness ? stickDrawThickness : Width;
        line = new PrimitiveBatch.Line(P1.Position, P2.Position, Color, w);
        if (!IsCut)
            line.Draw();
    }
}
