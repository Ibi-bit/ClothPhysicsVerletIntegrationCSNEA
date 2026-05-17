using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

public partial class Game1
{
    /// <summary>
    /// determines whether to draw particles
    /// </summary>
    private bool _drawParticles;

    /// <summary>
    /// determines whether to draw constraints (sticks)
    /// </summary>
    private bool _drawConstraints;

    /// <summary>
    /// initiales rendering related variables and settings
    /// </summary>
    private void InitializeRender()
    {
        _drawParticles = true;
        _drawConstraints = true;
    }

    private void DrawCollisionBounds()
    {
        var rect = new Rectangle(
            _windowBounds.X,
            _windowBounds.Y,
            (int)(_windowBounds.Width + _collisonBoundsDifference.X),
            (int)(_windowBounds.Height + _collisonBoundsDifference.Y)
        );

        var collisionBounds = new PrimitiveBatch.Rectangle(
            new Vector2(rect.X, rect.Y),
            new Vector2(rect.Width, rect.Height),
            Color.Black,
            false
        );
        collisionBounds.Draw();
    }

    private void DrawSceneContent()
    {
        if (_activeMesh?.Colliders != null)
        {
            foreach (var collider in _activeMesh.Colliders)
            {
                collider?.Draw();
            }
        }

        if (_activeMesh != null)
        {
            _activeMesh.Draw(_drawParticles, _drawConstraints);
        }

        if (_windDirectionArrow != null)
        {
            _windDirectionArrow.Draw();
        }

        if (_cutLine != null)
        {
            _cutLine.Draw();
        }

        if (_selectRectangle != null)
        {
            _selectRectangle.Draw();
        }
    }

    private void DrawCursorOverlay(Vector2 currentMousePos, bool imguiWantsMouse)
    {
        if (imguiWantsMouse)
        {
            return;
        }

        float radius = 0f;
        int alphaValue = (int)(byte.MaxValue * 0.4f);
        Color cursorColor = new Color(255, 255, 255, alphaValue); // White with alpha
        bool shouldDrawCursor = false;

        if (!string.IsNullOrEmpty(_selectedToolName) && _currentToolSet != null)
        {
            if (_currentToolSet.ContainsKey(_selectedToolName))
            {
                var props = _currentToolSet[_selectedToolName].Properties;

                ConfigureToolCursor(
                    currentMousePos,
                    props,
                    alphaValue,
                    ref radius,
                    ref cursorColor,
                    ref shouldDrawCursor
                );
            }
        }

        if (shouldDrawCursor && radius > 0f)
        {
            DrawCircleCursor(currentMousePos, radius, cursorColor);
            return;
        }

        DrawCrosshairCursor(currentMousePos, cursorColor);
    }

    /// <summary>
    /// the central draw loop for the application where every other draw function is called from
    /// </summary>
    /// <param name="gameTime"></param>
    protected override void Draw(GameTime gameTime)
    {
        Raylib.BeginDrawing();
        Raylib.ClearBackground(new Color(100, 149, 237, 255)); // CornflowerBlue

        Vector2 currentMousePos = Raylib.GetMousePosition();
        bool imguiWantsMouse = ImGuiNET.ImGui.GetIO().WantCaptureMouse;

        DrawCollisionBounds();
        DrawSceneContent();

        DrawCursorOverlay(currentMousePos, imguiWantsMouse);

        ImGuiDraw(gameTime);

        Raylib.EndDrawing();
    }

    private void ConfigureToolCursor(
        Vector2 currentMousePos,
        Dictionary<string, object> props,
        int alphaValue,
        ref float radius,
        ref Color cursorColor,
        ref bool shouldDrawCursor
    )
    {
        switch (_selectedToolName)
        {
            case "Drag":
                radius = props.TryGetValue("Radius", out var dragRadius) ? (float)dragRadius : 20f;
                cursorColor = new Color(255, 255, 0, alphaValue); // Yellow
                shouldDrawCursor = true;
                break;

            case "Pin":
                radius = props.TryGetValue("Radius", out var pinRadius) ? (float)pinRadius : 20f;
                cursorColor = new Color(138, 43, 226, alphaValue); // BlueViolet
                shouldDrawCursor = true;
                break;

            case "Cut":
                radius = props.TryGetValue("Radius", out var cutRadius) ? (float)cutRadius : 10f;
                cursorColor = new Color(255, 0, 0, alphaValue); // Red
                shouldDrawCursor = true;
                break;

            case "PhysicsDrag":
                radius = props.TryGetValue("Radius", out var physRadius) ? (float)physRadius : 20f;
                cursorColor = new Color(255, 165, 0, alphaValue); // Orange
                shouldDrawCursor = true;
                break;

            case "Inspect Particles":
                radius = props.TryGetValue("Radius", out var inspectRadius)
                    ? (float)inspectRadius
                    : 10f;
                cursorColor = new Color(0, 255, 255, alphaValue); // Cyan
                shouldDrawCursor = true;
                break;

            case "Cursor Collider":
                DrawCursorColliderPreview(currentMousePos, props, alphaValue);
                shouldDrawCursor = false;
                break;

            case "Add Stick Between Particles":
                radius = props.TryGetValue("Radius", out var stickRadius)
                    ? (float)stickRadius
                    : 15f;
                cursorColor = new Color(0, 255, 0, alphaValue); // Green
                shouldDrawCursor = true;
                break;

            case "Remove Particle":
                radius = props.TryGetValue("Radius", out var removeRadius)
                    ? (float)removeRadius
                    : 10f;
                cursorColor = new Color(255, 0, 0, alphaValue); // Red
                shouldDrawCursor = true;
                break;

            case "Place Collider":
                HandlePlaceColliderPreview(
                    currentMousePos,
                    props,
                    alphaValue,
                    ref radius,
                    ref cursorColor,
                    ref shouldDrawCursor
                );
                break;

            default:
                shouldDrawCursor = false;
                break;
        }
    }

    private void DrawCursorColliderPreview(
        Vector2 currentMousePos,
        Dictionary<string, object> props,
        int alphaValue
    )
    {
        float radius = props.TryGetValue("Radius", out var colliderRadius)
            ? (float)colliderRadius
            : 50f;
        string shape = props.TryGetValue("Shape", out var shapeObj) ? (string)shapeObj : "Circle";

        if (shape == "Circle")
        {
            var cursorCollider = new PrimitiveBatch.Circle(
                currentMousePos,
                radius,
                new Color(255, 0, 0, alphaValue),
                true
            );
            cursorCollider.Draw();
            return;
        }

        if (shape == "Rectangle")
        {
            int size = (int)(radius * 2f);
            var cursorRect = new PrimitiveBatch.Rectangle(
                currentMousePos - new Vector2(size / 2f, size / 2f),
                new Vector2(size, size),
                new Color(255, 0, 0, alphaValue),
                true
            );
            cursorRect.Draw();
        }
    }

    private void HandlePlaceColliderPreview(
        Vector2 currentMousePos,
        Dictionary<string, object> props,
        int alphaValue,
        ref float radius,
        ref Color cursorColor,
        ref bool shouldDrawCursor
    )
    {
        string selectedType = props.TryGetValue(
            "SelectedColliderType",
            out var selectedColliderType
        )
            ? selectedColliderType?.ToString() ?? "Circle"
            : "Circle";

        props.TryGetValue("Object", out var objectValue);
        var objectDict = objectValue as Dictionary<string, object>;

        if (selectedType == "Rectangle")
        {
            object rectangleObj = null;
            if (objectDict != null)
            {
                objectDict.TryGetValue("Rectangle", out rectangleObj);
            }

            var rectangleDict = rectangleObj as Dictionary<string, object>;
            float width =
                rectangleDict != null && rectangleDict.TryGetValue("Width", out var widthObj)
                    ? Convert.ToSingle(widthObj)
                    : 40f;
            float height =
                rectangleDict != null && rectangleDict.TryGetValue("Height", out var heightObj)
                    ? Convert.ToSingle(heightObj)
                    : 20f;
            float rotation =
                rectangleDict != null && rectangleDict.TryGetValue("Rotation", out var rotationObj)
                    ? Convert.ToSingle(rotationObj)
                    : 0f;

            var cursorRect = new PrimitiveBatch.Rectangle(
                currentMousePos - new Vector2(width, height) / 2f,
                new Vector2(width, height),
                new Color(255, 0, 0, alphaValue),
                true
            );
            cursorRect.rotation = rotation;
            cursorRect.Draw();
            shouldDrawCursor = false;
            return;
        }

        object circleObj = null;
        if (objectDict != null)
        {
            objectDict.TryGetValue("Circle", out circleObj);
        }
        var circleDict = circleObj as Dictionary<string, object>;
        float placeRadius = 20f;
        if (circleDict != null && circleDict.TryGetValue("Radius", out var radiusObj))
        {
            placeRadius = Convert.ToSingle(radiusObj);
        }

        radius = placeRadius;
        cursorColor = new Color(255, 0, 0, alphaValue);
        shouldDrawCursor = true;
    }

    private void DrawCircleCursor(Vector2 position, float radius, Color color)
    {
        var cursorCircleFilled = new PrimitiveBatch.Circle(position, radius, color, true);
        cursorCircleFilled.Draw();

        var cursorCircleOutline = new PrimitiveBatch.Circle(position, radius, color, false);
        cursorCircleOutline.Draw();
    }

    private void DrawCrosshairCursor(Vector2 position, Color color)
    {
        const int crosshairSize = 10;

        var horizontalLine = new PrimitiveBatch.Line(
            position - new Vector2(crosshairSize, 0),
            position + new Vector2(crosshairSize, 0),
            color,
            2
        );
        var verticalLine = new PrimitiveBatch.Line(
            position - new Vector2(0, crosshairSize),
            position + new Vector2(0, crosshairSize),
            color,
            2
        );

        horizontalLine.Draw();
        verticalLine.Draw();
    }
}
