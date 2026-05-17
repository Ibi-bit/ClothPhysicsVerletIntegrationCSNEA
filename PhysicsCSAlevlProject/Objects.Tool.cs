using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

public class Tool
{
    public string Name;

    public PrimitiveBatch.Shape CursorIcon;
    public Dictionary<string, object> Properties = new Dictionary<string, object>();

    public Tool(string name, PrimitiveBatch.Shape CursorIcon, bool Centred)
    {
        Name = name;

        this.CursorIcon = CursorIcon;
    }

    public void Draw( Vector2 cursorPosition)
    {
        if (CursorIcon != null)
        {
            if (CursorIcon is PrimitiveBatch.Rectangle rect)
            {
                var drawRect = new PrimitiveBatch.Rectangle(
                    new Vector2(
                        cursorPosition.X - rect.size.X / 2,
                        cursorPosition.Y - rect.size.Y / 2
                    ),
                    rect.size,
                    rect.color
                );
                drawRect.Draw();
            }
            else if (CursorIcon is PrimitiveBatch.Circle circle)
            {
                var drawCircle = new PrimitiveBatch.Circle(
                    cursorPosition,
                    circle.radius,
                    circle.color,
                    true
                );
                drawCircle.Draw();
            }
        }
    }
}
