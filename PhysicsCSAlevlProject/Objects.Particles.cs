using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

/// <summary>
/// The Particle class represents a single particle in the physics simulation, containing properties such as position, mass, accumulated force, and whether it is pinned or selected. The DrawableParticle class extends Particle by adding visual properties like color and size, and includes a method to draw itself using a SpriteBatch and PrimitiveBatch. The OscillatingParticle class further extends DrawableParticle to include oscillation parameters such as amplitude, frequency, and angle, allowing it to move in a sinusoidal pattern around an anchor position. This design allows for flexible representation of particles in the simulation, supporting both static and dynamic behaviors while also providing visual feedback for rendering.
/// </summary>
class Particle
{
    /// <summary>
    /// the position of the particle
    /// </summary>
    public Vector2 Position;

    /// <summary>
    /// the previous position of the particle
    /// </summary>
    public Vector2 PreviousPosition;

    /// <summary>
    /// the mass of the particle
    /// </summary>
    public float Mass;

    /// <summary>
    /// the accumalated force due to hooks law
    /// </summary>
    public Vector2 AccumulatedForce;

    /// <summary>
    /// the ID of the particle
    /// </summary>
    public int ID;

    /// <summary>
    /// magnitude of of the total force applied to the particle
    /// </summary>
    public float TotalForceMagnitude;

    /// <summary>
    /// toggle for if th eparticle is pinned used to determine if the particle should be affected by physics or not
    /// </summary>
    public bool IsPinned;

    /// <summary>
    /// toggle for if the particle is selected used for the UI to determine if the particle should be drawn with a selection highlight and to allow the particle to be dragged with the mouse when selected
    /// </summary>
    public bool IsSelected;

    public Particle()
    {
        Position = Vector2.Zero;
        PreviousPosition = Vector2.Zero;
        Mass = 1.0f;
        AccumulatedForce = Vector2.Zero;
        IsPinned = false;
        IsSelected = false;
        ID = -1;
        TotalForceMagnitude = 0f;
    }

    public Particle(Vector2 position, float mass, bool isPinned)
    {
        Position = position;
        PreviousPosition = position;
        Mass = mass;
        AccumulatedForce = Vector2.Zero;

        IsPinned = isPinned || mass <= 0;
        IsSelected = false;
        ID = -1;
    }
}

class DrawableParticle : Particle
{
    /// <summary>
    /// the rectangle used for drawing the particle, which is updated based on the particle's position and size. The rectangle is drawn in the Draw method, and its position is centered around the particle's position to ensure accurate rendering of the particle on the screen.
    /// </summary>
    private PrimitiveBatch.Rectangle rectangle;

    /// <summary>
    /// the colour of the particle
    /// </summary>
    public Color Color { get; set; }

    /// <summary>
    /// the size of the particle to be used in drawing
    /// </summary>
    public Vector2 Size { get; set; }

    public DrawableParticle()
        : base(Vector2.Zero, 1.0f, false)
    {
        Size = new Vector2(10, 10);
        Color = Color.White;
        UpdateRectangle();
    }

    public DrawableParticle(Vector2 position, float mass, Vector2 size, Color color)
        : base(position, mass, false)
    {
        Size = size;
        Color = color;
        UpdateRectangle();
    }

    public DrawableParticle(Vector2 position, float mass, Color color)
        : base(position, mass, false)
    {
        Size = new Vector2(10, 10);
        Color = color;
        UpdateRectangle();
    }

    public DrawableParticle(Vector2 position, float mass, bool isPinned, Color color)
        : base(position, mass, isPinned)
    {
        Size = new Vector2(5, 5);
        Color = color;
        UpdateRectangle();
    }

    private void UpdateRectangle()
    {
        rectangle = new PrimitiveBatch.Rectangle(Position, Size, Color);
    }

    public void Draw()
    {
        Vector2 middle = Position - Size / 2;
        if (IsPinned)
        {
            rectangle = new PrimitiveBatch.Rectangle(middle, Size, new Color(138, 43, 226, 255));
            rectangle.Draw();
            return;
        }
        rectangle = new PrimitiveBatch.Rectangle(middle, Size, Color);
        rectangle.Draw();
    }
}

/// <summary>
/// the OscillatingParticle class extends DrawableParticle to include oscillation parameters such as amplitude, frequency, and angle,
/// allowing it to move in a sinusoidal pattern around an anchor position.
///  The UpdateOscillation method updates the particle's position based on the oscillation parameters and the elapsed time, creating a dynamic movement effect.
/// The SetAnchorPosition method allows changing the anchor point around which the particle oscillates, providing flexibility in how the particle behaves within the simulation.
/// </summary>
/// <param name="position"></param>
/// <param name="mass"></param>
/// <param name="isPinned"></param>
/// <param name="color"></param>
/// <param name="amplitude"></param>
/// <param name="frequency"></param>
/// <param name="angle"></param>
class OscillatingParticle(
    Vector2 position,
    float mass,
    bool isPinned,
    Color color,
    float amplitude,
    float frequency,
    float angle
) : DrawableParticle(position, mass, isPinned, color)
{
    /// <summary>
    /// the amplitude of the oscillation,
    /// </summary>
    public float OscillationAmplitude = amplitude;

    /// <summary>
    /// the frequency of the oscillation, which determines how fast the particle oscillates around its anchor position.
    /// </summary>
    public float OscillationFrequency = frequency;
    public float OscillationAngle = angle;
    private float _oscillationTime = 0f;
    public Vector2 anchorPosition = position;

    public void UpdateOscillation(float deltaTime)
    {
        _oscillationTime += deltaTime;
        float oscillationOffset =
            OscillationAmplitude
            * MathF.Sin(2 * MathF.PI * OscillationFrequency * _oscillationTime);

        Position = new Vector2(
            anchorPosition.X + oscillationOffset * MathF.Cos(OscillationAngle - MathF.PI / 2),
            anchorPosition.Y + oscillationOffset * MathF.Sin(OscillationAngle - MathF.PI / 2)
        );
        PreviousPosition = Position;
    }

    public void SetAnchorPosition(Vector2 newAnchor)
    {
        anchorPosition = newAnchor;
    }
}
