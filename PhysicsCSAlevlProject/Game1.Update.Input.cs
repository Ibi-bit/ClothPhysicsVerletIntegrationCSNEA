using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace PhysicsCSAlevlProject;

public partial class Game1
{
    /// <summary>
    /// stores the position at the start of a left mouse drag
    /// </summary>
    private Vector2 _initialMousePosWhenPressed;

    /// <summary>
    /// the collider that is currently being dragged by the Move Collider tool,
    /// </summary>
    private Collider _draggedCollider;

    private void HandleUndoRedoShortcuts(bool ctrlHeld, bool shiftHeld)
    {
        if (ctrlHeld && !shiftHeld && Raylib.IsKeyPressed(KeyboardKey.Z))
        {
            if (_meshHistory.Count > 0)
            {
                _meshRedoHistory.Push(_activeMesh.DeepCopy());
                _activeMesh = _meshHistory.Pop();
                _logger.AddLog("Undid last action", ImGuiLogger.LogTypes.Info);
                _paused = true;
            }
            else
            {
                _logger.AddLog("No more history to undo", ImGuiLogger.LogTypes.Warning);
            }
        }
        else if (ctrlHeld && shiftHeld && Raylib.IsKeyPressed(KeyboardKey.Z))
        {
            if (_meshRedoHistory.Count > 0)
            {
                _meshHistory.Push(_activeMesh.DeepCopy());
                _activeMesh = _meshRedoHistory.Pop();
                _logger.AddLog("Redid action", ImGuiLogger.LogTypes.Info);
                _paused = true;
            }
            else
            {
                _logger.AddLog("No more history to redo", ImGuiLogger.LogTypes.Warning);
            }
        }
    }

    private void UpdateCursorColliderFromToolSettings()
    {
        if (_currentToolSet.ContainsKey("Cursor Collider"))
        {
            var props = _currentToolSet["Cursor Collider"].Properties;
            float radius = props.TryGetValue("Radius", out var prop) ? (float)prop : 20f;
            string shape = props.TryGetValue("Shape", out var prop1) ? (string)prop1 : "Circle";
            if (_cursorColliderStore.TryGetValue(shape, out var collider))
            {
                _cursorCollider = collider;
            }
            UpdateCursorColliderSize(radius);
        }
    }

    private void HandlePauseAndStepHotkeys()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            _paused = !_paused;
            MeshHistoryPush();
            if (_paused)
            {
                _logger.AddLog("Simulation paused", ImGuiLogger.LogTypes.Info);
            }
            else
            {
                _logger.AddLog("Simulation resumed", ImGuiLogger.LogTypes.Info);
            }
        }
        else if (!_paused && Raylib.IsKeyPressed(KeyboardKey.Space))
        {
            _paused = true;
            _paused = false;
            _logger.AddLog("Stepped 1 physics tick", ImGuiLogger.LogTypes.Info);
        }
    }

    private void HandleDirectToolSelection()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.D))
        {
            bool ctrlHeld =
                Raylib.IsKeyDown(KeyboardKey.LeftControl)
                || Raylib.IsKeyDown(KeyboardKey.RightControl);
            bool shiftHeld =
                Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);

            if (!ctrlHeld && !shiftHeld && _currentToolSet.ContainsKey("Drag"))
            {
                _selectedToolName = "Drag";
                _logger.AddLog("Switched to tool: Drag");
            }
        }
    }

    private Vector2 HandleMouseAndToolInput(Vector2 currentMousePos, bool imguiWantsMouse)
    {
        if (!imguiWantsMouse)
        {
            Raylib.HideCursor();
            if (
                _leftPressed
                && (_initialMousePosWhenPressed.X >= _windowBounds.X
                    && _initialMousePosWhenPressed.X <= _windowBounds.X + _windowBounds.Width
                    && _initialMousePosWhenPressed.Y >= _windowBounds.Y
                    && _initialMousePosWhenPressed.Y <= _windowBounds.Y + _windowBounds.Height)
                && Raylib.IsWindowFocused()
            )
            {
                var clampedPos = Vector2.Clamp(
                    currentMousePos,
                    new Vector2(0, 0),
                    new Vector2(_windowBounds.Width, _windowBounds.Height)
                );

                if (_selectedToolName == "Drag" && _currentMode == MeshMode.Interact)
                {
                    bool collideWithColliders =
                        _currentToolSet["Drag"].Properties.ContainsKey("CollideWithColliders")
                        && (bool)_currentToolSet["Drag"].Properties["CollideWithColliders"];
                    if (collideWithColliders)
                    {
                        clampedPos = ResolveDragMousePositionAgainstColliders(clampedPos);
                    }
                }

                currentMousePos = clampedPos;
                Raylib.SetMousePosition((int)clampedPos.X, (int)clampedPos.Y);
            }

            if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !_leftPressed)
            {
                _leftPressed = true;
                _initialMousePosWhenPressed = currentMousePos;
                _previousMousePos = currentMousePos;

                float radius;
                switch (_selectedToolName)
                {
                    case "Drag":
                        if (_currentMode == MeshMode.Interact)
                        {
                            var props = _currentToolSet["Drag"].Properties;
                            bool infiniteParticles =
                                props.ContainsKey("InfiniteParticles")
                                && (bool)props["InfiniteParticles"];
                            int maxParticles = infiniteParticles
                                ? -1
                                : (
                                    props.TryGetValue("MaxParticles", out var prop1)
                                        ? (int)prop1
                                        : -1
                                );

                            _meshParticlesInDragArea = GetMeshParticlesInRadius(
                                _initialMousePosWhenPressed,
                                props.TryGetValue("Radius", out var prop)
                                    ? (float)prop
                                    : _dragRadius,
                                maxParticles
                            );
                            if (_meshParticlesInDragArea.Count == 0)
                            {
                                _logger.AddLog(
                                    "Drag tool: no particles found in radius",
                                    ImGuiLogger.LogTypes.Warning
                                );
                            }
                        }
                        break;
                    case "Pin":
                    {
                        MeshHistoryPush();

                        float pinRadius = _currentToolSet["Pin"].Properties.ContainsKey("Radius")
                            ? (float)_currentToolSet["Pin"].Properties["Radius"]
                            : _dragRadius;

                        PinParticleBuildable(_initialMousePosWhenPressed, pinRadius);
                        break;
                    }
                    case "Cut":
                        MeshHistoryPush();
                        radius =
                            _currentToolSet["Cut"].Properties["Radius"] != null
                                ? (float)_currentToolSet["Cut"].Properties["Radius"]
                                : 10f;

                        CutAllSticksInRadiusBuildable(_initialMousePosWhenPressed, radius);
                        break;
                    case "Wind":
                        break;
                    case "PhysicsDrag":
                    {
                        var physProps = _currentToolSet["PhysicsDrag"].Properties;
                        float physRadius = physProps.ContainsKey("Radius")
                            ? (float)physProps["Radius"]
                            : _dragRadius;
                        bool infiniteParticles =
                            physProps.ContainsKey("InfiniteParticles")
                            && (bool)physProps["InfiniteParticles"];
                        int maxParticles = infiniteParticles
                            ? -1
                            : (
                                physProps.TryGetValue("MaxParticles", out var maxParticlesProp)
                                    ? (int)maxParticlesProp
                                    : -1
                            );
                        float strength = physProps.ContainsKey("Strength")
                            ? (float)physProps["Strength"]
                            : 3500f;
                        float damping = physProps.ContainsKey("Damping")
                            ? (float)physProps["Damping"]
                            : 90f;
                        float maxForce = physProps.ContainsKey("MaxForce")
                            ? (float)physProps["MaxForce"]
                            : 30000f;

                        if (_currentMode == MeshMode.Interact)
                        {
                            _meshParticlesInDragArea = GetMeshParticlesInRadius(
                                _initialMousePosWhenPressed,
                                physRadius,
                                maxParticles
                            );
                            _physicsDragParticleOffsets.Clear();
                            foreach (int particleId in _meshParticlesInDragArea)
                            {
                                if (_activeMesh.Particles.TryGetValue(particleId, out var particle))
                                {
                                    _physicsDragParticleOffsets[particleId] =
                                        particle.Position - _initialMousePosWhenPressed;
                                }
                            }
                            if (_meshParticlesInDragArea.Count == 0)
                            {
                                _logger.AddLog(
                                    "PhysicsDrag tool: no particles found in radius",
                                    ImGuiLogger.LogTypes.Warning
                                );
                            }
                            else
                            {
                                _logger.AddLog(
                                    $"PhysicsDrag started: {_meshParticlesInDragArea.Count} particles (radius {physRadius:0.##}, k {strength:0.##}, c {damping:0.##}, maxF {maxForce:0.##})"
                                );
                            }
                        }
                        else
                        {
                            _logger.AddLog(
                                "PhysicsDrag can only be used in Interact mode",
                                ImGuiLogger.LogTypes.Warning
                            );
                        }
                        break;
                    }
                    case "LineCut":
                        break;
                    case "Add Stick Between Particles":
                        MeshHistoryPush();
                        HandleAddStickBetweenParticlesClick(_initialMousePosWhenPressed);
                        break;
                    case "Add Particle":
                    {
                        MeshHistoryPush();
                        float particleMass = _currentToolSet["Add Particle"]
                            .Properties.ContainsKey("Mass")
                            ? (float)_currentToolSet["Add Particle"].Properties["Mass"]
                            : 1f;
                        _activeMesh.AddParticle(
                            _initialMousePosWhenPressed,
                            particleMass,
                            false,
                            Color.White
                        );
                        _logger.AddLog(
                            $"Added particle at {_initialMousePosWhenPressed} (mass {particleMass}) (ID {_activeMesh.NextParticle - 1})"
                        );
                        break;
                    }
                    case "Remove Particle":
                    {
                        MeshHistoryPush();
                        float removeRadius = _currentToolSet["Remove Particle"]
                            .Properties.ContainsKey("Radius")
                            ? (float)_currentToolSet["Remove Particle"].Properties["Radius"]
                            : 10f;
                        var pS = GetMeshParticlesInRadius(
                            _initialMousePosWhenPressed,
                            removeRadius,
                            1
                        );
                        if (pS.Count > 0)
                        {
                            _activeMesh.RemoveParticle(pS[0]);
                            _logger.AddLog($"Removed particle {pS[0]} (ID {pS[0]})");
                        }
                        else
                        {
                            _logger.AddLog(
                                "Remove Particle tool: no particle found in radius",
                                ImGuiLogger.LogTypes.Warning
                            );
                        }
                        break;
                    }
                    case "Inspect Particles":
                    {
                        float inspectRadius = _currentToolSet["Inspect Particles"]
                            .Properties.ContainsKey("Radius")
                            ? (float)_currentToolSet["Inspect Particles"].Properties["Radius"]
                            : 10f;
                        if (
                            _currentToolSet["Inspect Particles"].Properties.ContainsKey("IsLog")
                            && (bool)_currentToolSet["Inspect Particles"].Properties["IsLog"]
                        )
                            InspectParticlesInRadiusLog(_initialMousePosWhenPressed, inspectRadius);
                        else if (
                            !_currentToolSet["Inspect Particles"]
                                .Properties.ContainsKey("SelectRectangle")
                        )
                            InspectParticlesInRadiusWindow(
                                _initialMousePosWhenPressed,
                                inspectRadius
                            );
                        break;
                    }
                    case "Oscillating Particle":
                    {
                        MeshHistoryPush();
                        float mass = _currentToolSet["Oscillating Particle"]
                            .Properties.ContainsKey("Mass")
                            ? (float)_currentToolSet["Oscillating Particle"].Properties["Mass"]
                            : 1f;
                        float amplitude = _currentToolSet["Oscillating Particle"]
                            .Properties.ContainsKey("Amplitude")
                            ? (float)_currentToolSet["Oscillating Particle"].Properties["Amplitude"]
                            : 20f;
                        float frequency = _currentToolSet["Oscillating Particle"]
                            .Properties.ContainsKey("Frequency")
                            ? (float)_currentToolSet["Oscillating Particle"].Properties["Frequency"]
                            : 1f;
                        float angle = _currentToolSet["Oscillating Particle"]
                            .Properties.ContainsKey("Angle")
                            ? (float)_currentToolSet["Oscillating Particle"].Properties["Angle"]
                            : 0f;

                        var op = new OscillatingParticle(
                            _initialMousePosWhenPressed,
                            mass,
                            true,
                            Color.White,
                            amplitude,
                            frequency,
                            angle
                        );
                        _activeMesh.AddParticle(
                            _initialMousePosWhenPressed,
                            mass,
                            true,
                            Color.White,
                            null,
                            op
                        );
                        _logger.AddLog(
                            $"Added oscillating particle at {_initialMousePosWhenPressed} (mass {mass}, amplitude {amplitude}, frequency {frequency})"
                        );
                        break;
                    }
                    case "Place Collider":
                    {
                        var properties = _currentToolSet["Place Collider"].Properties;
                        if (properties["SelectedColliderType"] == null)
                            break;
                        if (properties["SelectedColliderType"].ToString() == "Circle")
                        {
                            var colliderObject = properties["Object"] as Dictionary<string, object>;
                            colliderObject =
                                colliderObject != null
                                && colliderObject.TryGetValue("Circle", out var circleObj)
                                && circleObj is Dictionary<string, object> circleDict
                                    ? circleDict
                                    : null;

                            MeshHistoryPush();
                            radius = colliderObject.ContainsKey("Radius")
                                ? (float)colliderObject["Radius"]
                                : 20f;
                            var collider = new CircleCollider(_initialMousePosWhenPressed, radius);
                            _activeMesh.Colliders.Add(collider);
                            _logger.AddLog(
                                $"Placed circle collider at {_initialMousePosWhenPressed} with radius {radius}"
                            );
                        }
                        else if (properties["SelectedColliderType"].ToString() == "Rectangle")
                        {
                            var colliderObject = properties["Object"] as Dictionary<string, object>;
                            var rectProps =
                                colliderObject != null
                                && colliderObject.TryGetValue("Rectangle", out var rectObj)
                                && rectObj is Dictionary<string, object> rectDict
                                    ? rectDict
                                    : null;

                            float height =
                                rectProps != null
                                && rectProps.TryGetValue("Height", out var heightObj)
                                    ? Convert.ToSingle(heightObj)
                                    : 20f;
                            float width =
                                rectProps != null
                                && rectProps.TryGetValue("Width", out var widthObj)
                                    ? Convert.ToSingle(widthObj)
                                    : 20f;
                            float angle =
                                rectProps != null
                                && rectProps.TryGetValue("Rotation", out var angleObj)
                                    ? Convert.ToSingle(angleObj)
                                    : 0f;

                            MeshHistoryPush();
                            var collider = new SeperatedAxisRectangleCollider(
                                new Rectangle(
                                    (int)(_initialMousePosWhenPressed.X - width / 2f),
                                    (int)(_initialMousePosWhenPressed.Y - height / 2f),
                                    (int)width,
                                    (int)height
                                ),
                                angle
                            );
                            _activeMesh.Colliders.Add(collider);
                            _logger.AddLog(
                                $"Placed rectangle collider at {_initialMousePosWhenPressed} with width {width} and height {height}"
                            );
                        }
                        break;
                    }
                    case "Delete Collider":
                        for (int i = _activeMesh.Colliders.Count - 1; i >= 0; i--)
                        {
                            var collider = _activeMesh.Colliders[i];
                            if (collider.ContainsPoint(currentMousePos, out _))
                            {
                                MeshHistoryPush();
                                _activeMesh.Colliders.RemoveAt(i);
                                _logger.AddLog($"Deleted collider at {currentMousePos}");
                                break;
                            }
                        }
                        break;
                    case "Move Collider":
                        _draggedCollider = null;
                        if (_activeMesh != null && _activeMesh.Colliders != null)
                        {
                            foreach (var collider in _activeMesh.Colliders)
                            {
                                if (collider != null)
                                {
                                    try
                                    {
                                        if (collider.ContainsPoint(currentMousePos, out _))
                                        {
                                            _draggedCollider = collider;
                                            break;
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                        break;
                }
            }
            else if (Raylib.IsMouseButtonReleased(MouseButton.Left))
            {
                if (_selectedToolName == "Wind" && _leftPressed)
                {
                    ApplyWindForceFromDrag(
                        _initialMousePosWhenPressed,
                        currentMousePos,
                        _dragRadius
                    );
                }
                else if (_selectedToolName == "LineCut" && _leftPressed)
                {
                    MeshHistoryPush();
                    Vector2 cutDirection = currentMousePos - _initialMousePosWhenPressed;
                    float cutDistance = cutDirection.Length();
                    if (cutDistance > 5f)
                    {
                        CutSticksAlongLine(_initialMousePosWhenPressed, currentMousePos);
                    }
                    else
                    {
                        _logger.AddLog(
                            "LineCut tool: drag too short to cut",
                            ImGuiLogger.LogTypes.Warning
                        );
                    }
                }
                else if (
                    _selectedToolName == "Select Particles"
                    && _leftPressed
                    && _currentMode != MeshMode.Edit
                    && _currentToolSet["Select Particles"].Properties.ContainsKey("RectangleSelect")
                    && (bool)_currentToolSet["Select Particles"].Properties["RectangleSelect"]
                )
                {
                    InspectParticlesInRectangle(
                        _initialMousePosWhenPressed,
                        currentMousePos,
                        _currentToolSet["Select Particles"].Properties.ContainsKey("IsLog")
                            && (bool)_currentToolSet["Select Particles"].Properties["IsLog"],
                        _currentToolSet["Select Particles"].Properties.ContainsKey("Clear When Use")
                            && (bool)
                                _currentToolSet["Select Particles"].Properties["Clear When Use"]
                    );
                }
                else if (_selectedToolName == "Line Tool" && _leftPressed)
                {
                    MeshHistoryPush();
                    var props = _currentToolSet["Line Tool"].Properties;
                    int constraintsInLine = (int)props["Constraints in Line"];
                    float naturalLengthRatio = (float)props["Natural Length Ratio"];

                    _activeMesh.AddSticksAccrossLength(
                        _initialMousePosWhenPressed,
                        currentMousePos,
                        constraintsInLine,
                        naturalLengthRatio
                    );
                }

                if (_selectedToolName == "PhysicsDrag" && _leftPressed)
                {
                    _logger.AddLog(
                        $"PhysicsDrag released: {_meshParticlesInDragArea.Count} particles affected"
                    );
                }

                _leftPressed = false;
                _initialMousePosWhenPressed = Vector2.Zero;
                _windDirectionArrow = null;
                _cutLine = null;
                _selectRectangle = null;
                _windForce = Vector2.Zero;
                _draggedCollider = null;
                _physicsDragParticleOffsets.Clear();
            }
        }
        else
        {
            Raylib.ShowCursor();
        }

        return currentMousePos;
    }

    private Vector2 ResolveDragMousePositionAgainstColliders(Vector2 position)
    {
        if (_activeMesh?.Colliders == null || _activeMesh.Colliders.Count == 0)
        {
            return position;
        }

        Vector2 resolved = position;

        // Iterate a few times in case the cursor overlaps multiple colliders.
        for (int i = 0; i < 4; i++)
        {
            bool adjusted = false;
            foreach (var collider in _activeMesh.Colliders)
            {
                if (collider != null && collider.ContainsPoint(resolved, out var closestPoint))
                {
                    resolved = closestPoint;
                    adjusted = true;
                }
            }

            if (!adjusted)
            {
                break;
            }
        }

        if (float.IsNaN(resolved.X) || float.IsNaN(resolved.Y))
        {
            return position;
        }

        return new Vector2(
            Math.Clamp(resolved.X, 0, _windowBounds.Width),
            Math.Clamp(resolved.Y, 0, _windowBounds.Height)
        );
    }

    private void UpdateActiveToolVisualsAndActions(Vector2 currentMousePos, bool imguiWantsMouse)
    {
        if (_selectedToolName == "Wind" && _leftPressed)
        {
            Vector2 windDirection = currentMousePos - _initialMousePosWhenPressed;
            float windDistance = windDirection.Length();

            float minDist = _currentToolSet["Wind"].Properties.ContainsKey("MinDistance")
                ? (float)_currentToolSet["Wind"].Properties["MinDistance"]
                : 5f;
            if (windDistance > minDist)
            {
                float arrowThickness = _currentToolSet["Wind"]
                    .Properties.ContainsKey("ArrowThickness")
                    ? (float)_currentToolSet["Wind"].Properties["ArrowThickness"]
                    : 3f;
                _windDirectionArrow = new VectorGraphics.PrimitiveBatch.Arrow(
                    _initialMousePosWhenPressed,
                    currentMousePos,
                    new Raylib_cs.Color(0, 255, 255, 255),
                    arrowThickness
                );

                float strength = _currentToolSet["Wind"].Properties.ContainsKey("StrengthScale")
                    ? (float)_currentToolSet["Wind"].Properties["StrengthScale"]
                    : 1.0f;
                _windForce = !_paused
                    ? windDirection * (windDistance / 50f) * strength
                    : Vector2.Zero;
            }
            else
            {
                _windDirectionArrow = null;
                _windForce = Vector2.Zero;
            }
        }
        else if (
            _selectedToolName == "LineCut" && _leftPressed
            || _selectedToolName == "Line Tool" && _leftPressed
        )
        {
            Vector2 cutDirection = currentMousePos - _initialMousePosWhenPressed;
            float cutDistance = cutDirection.Length();
            float minDist = _currentToolSet[_selectedToolName].Properties.ContainsKey("MinDistance")
                ? (float)_currentToolSet[_selectedToolName].Properties["MinDistance"]
                : 5f;
            if (cutDistance > minDist)
            {
                float thickness = _currentToolSet[_selectedToolName]
                    .Properties.ContainsKey("Thickness")
                    ? (float)_currentToolSet[_selectedToolName].Properties["Thickness"]
                    : 3f;
                _cutLine = new VectorGraphics.PrimitiveBatch.Line(
                    _initialMousePosWhenPressed,
                    currentMousePos,
                    new Raylib_cs.Color(255, 0, 0, 255),
                    thickness
                );
            }
            else
            {
                _cutLine = null;
            }
        }
        else if (_selectedToolName == "Create Grid Mesh")
        {
            var props = _currentToolSet["Create Grid Mesh"].Properties;
            float distance = (float)props["DistanceBetweenParticles"];
            bool pinExteriorEdgeParticles = (bool)props["PinExteriorEdgeParticles"];
            bool connectDiagonalsBothWays = (bool)props["ConnectDiagonalsBothWays"];
            if (Raylib.IsKeyPressed(KeyboardKey.C))
            {
                MeshHistoryPush();
                _activeMesh = Mesh.CreateGridMesh(
                    _initialMousePosWhenPressed,
                    currentMousePos,
                    distance,
                    _activeMesh,
                    pinExteriorEdgeParticles,
                    connectDiagonalsBothWays
                );
            }

            if (_leftPressed)
            {
                var r = GetRectangleFromPoints(_initialMousePosWhenPressed, currentMousePos);
                var pos = new Vector2(r.X, r.Y);
                var size = new Vector2(r.Width, r.Height);
                var rect = new VectorGraphics.PrimitiveBatch.Rectangle(
                    pos,
                    size,
                    new Raylib_cs.Color((byte)0, (byte)100, (byte)0, (byte)13),
                    true
                );
                rect.edgeWidth = 2;
                rect.edgeColor = new Raylib_cs.Color((byte)255, (byte)255, (byte)0, (byte)255);
                _selectRectangle = rect;
            }
            else
            {
                _selectRectangle = null;
            }
        }
        else if (_selectedToolName == "Select Particles")
        {
            var props = _currentToolSet["Select Particles"].Properties;

            if (props.ContainsKey("RectangleSelect") && _leftPressed)
            {
                var r = GetRectangleFromPoints(_initialMousePosWhenPressed, currentMousePos);
                var pos = new Vector2(r.X, r.Y);
                var size = new Vector2(r.Width, r.Height);
                _selectRectangle = new VectorGraphics.PrimitiveBatch.Rectangle(
                    pos,
                    size,
                    new Raylib_cs.Color((byte)0, (byte)0, (byte)255, (byte)25),
                    true
                );
            }
            else
            {
                _selectRectangle = null;
            }
        }
        else if (_selectedToolName == "Add Polygon")
        {
            _activeMesh.BuildPolygon(imguiWantsMouse, MeshHistoryPush);
        }
    }
}
