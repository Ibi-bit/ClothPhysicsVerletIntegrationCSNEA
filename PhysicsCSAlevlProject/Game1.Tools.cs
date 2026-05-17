using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection.Emit;
using System.Xml;
using ImGuiNET;
using Raylib_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

public partial class Game1
{
    /// <summary>
    /// the selected tool name
    /// </summary>
    private string _selectedToolName;

    /// <summary>
    /// a store of all tools used in the interact mode
    /// </summary>
    private Dictionary<string, Tool> _interactTools;

    /// <summary>
    /// a store of all tools used in the edit mode
    /// </summary>
    private Dictionary<string, Tool> _buildTools;

    /// <summary>
    /// a reference to the current tool set based on the mode the program is in, this is used to simplify code when drawing the tool menu and settings as it can just reference this instead of checking the mode multiple times
    /// </summary>
    private Dictionary<string, Tool> _currentToolSet =>
        _currentMode == MeshMode.Edit ? _buildTools : _interactTools;

    /// <summary>
    /// the radius used for the drag tool,
    /// </summary>
    private float _dragRadius;

    /// <summary>
    /// lists the ID of all particles selected but the select tool
    /// </summary>
    private List<int> _inspectedParticles;

    /// <summary>
    /// the list if the ID of all particles opened in the select window
    /// </summary>
    private List<int> _openedInspectedParticles;

    /// <summary>
    /// stores all factories
    /// </summary>
    private List<Factory> _factories = new();

    /// <summary>
    /// the first particle ID to be paired with another to create a stick when using the add stick between particles tool, this is set to null when no particle has been selected yet or the pairing has been completed
    /// </summary>
    private int? _stickToolFirstParticleId;

    /// <summary>
    /// initializes all tools and their settings,
    ///  this is called in the Initialize method of the game and sets up the interact and build tools with their default properties and values. It also adds factories for creating complex structures like tires and cloth meshes.
    /// This method ensures that all tools are properly configured and ready for use when the application starts, allowing users to interact with and build meshes effectively using the provided toolset.
    /// </summary>
    private void InitializeTools()
    {
        _selectedToolName = "Drag";
        _dragRadius = 20f;
        _inspectedParticles = new List<int>();
        _openedInspectedParticles = new List<int>();
        _stickToolFirstParticleId = null;

        _factories.Add(new TireFactory(args => _activeMesh.CreateHubSpokeTire(args)));
        _factories.Add(new ClothFactory(args => _activeMesh.ArgClothMeshFactory(args)));

        InitializeInteractTools();
        InitializeBuildTools();
    }

    /// <summary>
    /// initalises all tools in the Interact Mode with their default properties and values, this includes the Drag, Pin, Cut, Wind, LineCut, Select Particles and Cursor Collider tools. Each tool is configured with specific properties such as radius, strength scale, min distance and shape options that can be adjusted by the user in the UI. This method ensures that all interact tools are properly set up and ready for use when the application starts, allowing users to effectively manipulate the mesh using these tools.
    /// </summary>
    private void InitializeInteractTools()
    {
        _interactTools = new Dictionary<string, Tool>
        {
            { "Drag", new Tool("Drag", null, true) },
            { "PhysicsDrag", new Tool("PhysicsDrag", null, true) },
            { "Pin", new Tool("Pin", null, false) },
            { "Cut", new Tool("Cut", null, false) },
            { "Wind", new Tool("Wind", null, false) },
            { "LineCut", new Tool("LineCut", null, false) },
            { "Select Particles", new Tool("Select Particles", null, false) },
            { "Cursor Collider", new Tool("Cursor Collider", null, false) },
        };
        foreach (var tool in _interactTools.Values)
        {
            tool.Properties = new Dictionary<string, object>();
        }

        _interactTools["Drag"].Properties["Radius"] = 20f;
        _interactTools["Drag"].Properties["MaxParticles"] = 20;
        _interactTools["Drag"].Properties["InfiniteParticles"] = true;
        _interactTools["Drag"].Properties["CollideWithColliders"] = true;

        _interactTools["PhysicsDrag"].Properties["Radius"] = 20f;
        _interactTools["PhysicsDrag"].Properties["MaxParticles"] = 20;
        _interactTools["PhysicsDrag"].Properties["InfiniteParticles"] = true;
        _interactTools["PhysicsDrag"].Properties["Strength"] = 3500f;
        _interactTools["PhysicsDrag"].Properties["Damping"] = 90f;
        _interactTools["PhysicsDrag"].Properties["MaxForce"] = 30000f;

        _interactTools["Pin"].Properties["Radius"] = 20f;

        _interactTools["Cut"].Properties["Radius"] = 10f;

        _interactTools["Wind"].Properties["MinDistance"] = 5f;
        _interactTools["Wind"].Properties["StrengthScale"] = 1.0f;
        _interactTools["Wind"].Properties["ArrowThickness"] = 3f;

        _interactTools["LineCut"].Properties["MinDistance"] = 5f;
        _interactTools["LineCut"].Properties["Thickness"] = 3f;

        _interactTools["Select Particles"].Properties["Radius"] = 10f;
        _interactTools["Select Particles"].Properties["IsLog"] = false;
        _interactTools["Select Particles"].Properties["Clear When Use"] = false;
        _interactTools["Select Particles"].Properties["RectangleSelect"] = true;

        _interactTools["Cursor Collider"].Properties["Radius"] = 50f;
        _interactTools["Cursor Collider"].Properties["Shape"] = "Circle";
    }

    /// <summary>
    /// initializes all tools in the Build Mode with their default properties and values,
    /// </summary>
    private void InitializeBuildTools()
    {
        _buildTools = new Dictionary<string, Tool>
        {
            { "Add Particle", new Tool("Add Particle", null, false) },
            { "Add Stick Between Particles", new Tool("Add Stick Between Particles", null, false) },
            { "Remove Particle", new Tool("Remove Particle", null, false) },
            { "LineCut", new Tool("LineCut", null, false) },
            { "Pin", new Tool("Pin", null, false) },
            { "Create Grid Mesh", new Tool("Create Grid Mesh", null, false) },
            { "Line Tool", new Tool("Line Tool", null, false) },
            { "Add Polygon", new Tool("Add Polygon", null, false) },
            { "Oscillating Particle", new Tool("Oscillating Particle", null, false) },
            { "Place Collider", new Tool("Place Collider", null, false) },
            { "Move Collider", new Tool("Move Collider", null, false) },
            { "Delete Collider", new Tool("Delete Collider", null, false) },
        };
        foreach (var tool in _buildTools.Values)
        {
            tool.Properties = new Dictionary<string, object>();
        }

        _buildTools["Add Particle"].Properties["SnapToGrid"] = true;
        _buildTools["Add Stick Between Particles"].Properties["Radius"] = 15f;
        _buildTools["Remove Particle"].Properties["Radius"] = 10f;
        _buildTools["LineCut"].Properties["MinDistance"] = 5f;
        _buildTools["LineCut"].Properties["Thickness"] = 3f;
        _buildTools["Pin"].Properties["Radius"] = 20f;
        _buildTools["Create Grid Mesh"].Properties["DistanceBetweenParticles"] = 10f;
        _buildTools["Create Grid Mesh"].Properties["PinExteriorEdgeParticles"] = false;
        _buildTools["Create Grid Mesh"].Properties["ConnectDiagonalsBothWays"] = false;
        _buildTools["Line Tool"].Properties["Constraints in Line"] = 100;
        _buildTools["Line Tool"].Properties["Natural Length Ratio"] = 1.0f;
        _buildTools["Oscillating Particle"].Properties["Amplitude"] = 20f;
        _buildTools["Oscillating Particle"].Properties["Frequency"] = 1f;
        _buildTools["Oscillating Particle"].Properties["Angle"] = 0f;
        var colliderObject = new Dictionary<string, object>();
        colliderObject["Circle"] = new Dictionary<string, object> { { "Radius", 20f } };
        colliderObject["Rectangle"] = new Dictionary<string, object>
        {
            { "Width", 40f },
            { "Height", 20f },
            { "Rotation", 0f },
        };
        _buildTools["Place Collider"].Properties["Object"] = colliderObject;
        _buildTools["Place Collider"].Properties["SelectedColliderType"] = "Circle";

        var moveColliderObject = new Dictionary<string, object>();
        moveColliderObject["Circle"] = new Dictionary<string, object> { { "Radius", 20f } };
        moveColliderObject["Rectangle"] = new Dictionary<string, object>
        {
            { "Width", 40f },
            { "Height", 20f },
            { "Rotation", 0f },
        };
        _buildTools["Move Collider"].Properties["Object"] = moveColliderObject;
        _buildTools["Move Collider"].Properties["SelectedColliderType"] = "Circle";
    }

    /// <summary>
    /// Draws all tools for the selected mode
    /// </summary>
    private void DrawToolMenuItems()
    {
        EnsureSelectedToolValid();
        foreach (var toolName in _currentToolSet.Keys)
        {
            bool isSelected = _selectedToolName == toolName;
            if (isSelected)
            {
                ImGui.PushStyleColor(
                    ImGuiCol.Text,
                    new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f)
                );
            }

            if (ImGui.Selectable(toolName, isSelected))
            {
                if (!string.Equals(_selectedToolName, toolName, StringComparison.Ordinal))
                {
                    _logger.AddLog($"Selected tool: {toolName}");
                }
                _selectedToolName = toolName;
            }

            if (isSelected)
            {
                ImGui.PopStyleColor();
            }
        }
    }

    /// <summary>
    /// Draws the settings for the currently selected tool in the UI.
    ///  Depending on which tool is selected, it displays different configurable parameters such as radius, strength, or shape options. The method uses ImGui controls like sliders, checkboxes, and combo boxes to allow users to adjust these parameters in real-time. It also includes error handling to catch any exceptions that may occur while drawing the tool settings, ensuring that the application remains stable and provides feedback on any issues encountered.
    /// </summary>
    private void DrawSelectedToolSettings()
    {
        try
        {
            if (string.IsNullOrEmpty(_selectedToolName))
            {
                ImGui.Text("No tool selected");
                return;
            }

            EnsureSelectedToolValid();
            ImGui.Text($"Settings for {_selectedToolName} Tool:");

            switch (_selectedToolName)
            {
                case "Drag":
                {
                    var props = _currentToolSet["Drag"].Properties;
                    float radius = (float)props["Radius"];
                    if (ImGui.SliderFloat("Radius", ref radius, 5f, 100f))
                    {
                        props["Radius"] = radius;
                    }

                    bool infiniteParticles = (bool)props["InfiniteParticles"];
                    if (ImGui.Checkbox("Infinite Particles", ref infiniteParticles))
                    {
                        props["InfiniteParticles"] = infiniteParticles;
                    }

                    int maxParticles = (int)props["MaxParticles"];
                    string maxParticlesLabel = infiniteParticles
                        ? "Max Particles: ∞"
                        : "Max Particles";

                    ImGui.BeginDisabled(infiniteParticles);
                    if (ImGui.SliderInt(maxParticlesLabel, ref maxParticles, 1, 100))
                    {
                        props["MaxParticles"] = maxParticles;
                    }

                    ImGui.EndDisabled();

                    bool collideWithColliders =
                        props.ContainsKey("CollideWithColliders")
                        && (bool)props["CollideWithColliders"];
                    if (ImGui.Checkbox("Collide With Colliders", ref collideWithColliders))
                    {
                        props["CollideWithColliders"] = collideWithColliders;
                    }
                    break;
                }
                case "Pin":
                {
                    var props = _currentToolSet["Pin"].Properties;
                    float radius = (float)props["Radius"];
                    if (ImGui.SliderFloat("Radius", ref radius, 5f, 100f))
                    {
                        props["Radius"] = radius;
                    }
                    break;
                }
                case "Cut":
                {
                    var props = _currentToolSet["Cut"].Properties;
                    float radius = (float)props["Radius"];
                    if (ImGui.SliderFloat("Radius", ref radius, 1f, 100f))
                    {
                        props["Radius"] = radius;
                    }
                    break;
                }
                case "Wind":
                {
                    var props = _currentToolSet["Wind"].Properties;
                    float minDist = (float)props["MinDistance"];
                    if (ImGui.SliderFloat("Min Distance", ref minDist, 0f, 50f))
                    {
                        props["MinDistance"] = minDist;
                    }

                    float strength = (float)props["StrengthScale"];
                    if (ImGui.SliderFloat("Strength Scale", ref strength, 0.0f, 5.0f))
                    {
                        props["StrengthScale"] = strength;
                    }

                    float thickness = (float)props["ArrowThickness"];
                    if (ImGui.SliderFloat("Arrow Thickness", ref thickness, 1f, 10f))
                    {
                        props["ArrowThickness"] = thickness;
                    }
                    break;
                }
                case "PhysicsDrag":
                {
                    var props = _currentToolSet["PhysicsDrag"].Properties;
                    float radius = (float)props["Radius"];
                    if (ImGui.SliderFloat("Radius", ref radius, 5f, 100f))
                    {
                        props["Radius"] = radius;
                    }

                    bool infiniteParticles = (bool)props["InfiniteParticles"];
                    if (ImGui.Checkbox("Infinite Particles", ref infiniteParticles))
                    {
                        props["InfiniteParticles"] = infiniteParticles;
                    }

                    int maxParticles = (int)props["MaxParticles"];
                    string maxParticlesLabel = infiniteParticles
                        ? "Max Particles: ∞"
                        : "Max Particles";

                    ImGui.BeginDisabled(infiniteParticles);
                    if (ImGui.SliderInt(maxParticlesLabel, ref maxParticles, 1, 100))
                    {
                        props["MaxParticles"] = maxParticles;
                    }
                    ImGui.EndDisabled();

                    float strength = (float)props["Strength"];
                    if (ImGui.SliderFloat("Strength", ref strength, 100f, 10000f))
                    {
                        props["Strength"] = strength;
                    }

                    float damping = (float)props["Damping"];
                    if (ImGui.SliderFloat("Damping", ref damping, 0f, 300f))
                    {
                        props["Damping"] = damping;
                    }

                    float maxForce = (float)props["MaxForce"];
                    if (ImGui.SliderFloat("Max Force", ref maxForce, 100f, 100000f))
                    {
                        props["MaxForce"] = maxForce;
                    }
                    break;
                }
                case "LineCut":
                {
                    var props = _currentToolSet["LineCut"].Properties;
                    float minDist = (float)props["MinDistance"];
                    if (ImGui.SliderFloat("Min Distance", ref minDist, 0f, 50f))
                    {
                        props["MinDistance"] = minDist;
                    }

                    float thickness = (float)props["Thickness"];
                    if (ImGui.SliderFloat("Line Thickness", ref thickness, 1f, 10f))
                    {
                        props["Thickness"] = thickness;
                    }
                    break;
                }
                case "Select Particles":
                {
                    var props = _currentToolSet["Select Particles"].Properties;
                    float radius = (float)props["Radius"];
                    if (ImGui.SliderFloat("Radius", ref radius, 5f, 100f))
                    {
                        props["Radius"] = radius;
                    }

                    bool isLog = (bool)props["IsLog"];
                    if (ImGui.Checkbox("Log to Console or track realtime in window", ref isLog))
                    {
                        props["IsLog"] = isLog;
                    }

                    bool clearWhenUse = (bool)props["Clear When Use"];
                    if (ImGui.Checkbox("Clear When Use", ref clearWhenUse))
                    {
                        props["Clear When Use"] = clearWhenUse;
                    }

                    bool rectangleSelect = (bool)props["RectangleSelect"];
                    if (ImGui.Checkbox("Select with Rectangle", ref rectangleSelect))
                    {
                        props["RectangleSelect"] = rectangleSelect;
                    }
                    break;
                }
                case "Add Particle":
                {
                    var props = _currentToolSet["Add Particle"].Properties;
                    bool snapToGrid = (bool)props["SnapToGrid"];
                    if (ImGui.Checkbox("Snap To Grid", ref snapToGrid))
                    {
                        props["SnapToGrid"] = snapToGrid;
                    }
                    break;
                }
                case "Add Stick Between Particles":
                {
                    var props = _currentToolSet["Add Stick Between Particles"].Properties;
                    float radius = (float)props["Radius"];
                    if (ImGui.SliderFloat("Radius", ref radius, 5f, 100f))
                    {
                        props["Radius"] = radius;
                    }
                    break;
                }
                case "Remove Stick":
                {
                    var props = _currentToolSet["Remove Stick"].Properties;
                    float radius = (float)props["Radius"];
                    if (ImGui.SliderFloat("Radius", ref radius, 1f, 100f))
                    {
                        props["Radius"] = radius;
                    }
                    break;
                }
                case "Create Grid Mesh":
                {
                    var props = _currentToolSet["Create Grid Mesh"].Properties;
                    float distance = (float)props["DistanceBetweenParticles"];
                    if (ImGui.SliderFloat("Distance Between Particles", ref distance, 5f, 100f))
                    {
                        props["DistanceBetweenParticles"] = distance;
                    }

                    bool pinExteriorEdgeParticles = (bool)props["PinExteriorEdgeParticles"];
                    if (ImGui.Checkbox("Pin Exterior Edge Particles", ref pinExteriorEdgeParticles))
                    {
                        props["PinExteriorEdgeParticles"] = pinExteriorEdgeParticles;
                    }

                    bool connectDiagonalsBothWays = (bool)props["ConnectDiagonalsBothWays"];
                    if (ImGui.Checkbox("Connect Diagonals Both Ways", ref connectDiagonalsBothWays))
                    {
                        props["ConnectDiagonalsBothWays"] = connectDiagonalsBothWays;
                    }
                    break;
                }
                case "Cursor Collider":
                {
                    var props = _currentToolSet["Cursor Collider"].Properties;
                    float radius = (float)props["Radius"];
                    if (ImGui.SliderFloat("Radius", ref radius, 1f, 100f))
                        props["Radius"] = radius;

                    string[] shapes = _cursorColliderStore.Keys.ToArray();
                    int shapeIndex = Array.IndexOf(shapes, (string)props["Shape"]);
                    if (shapeIndex < 0)
                        shapeIndex = 0;
                    if (ImGui.Combo("Shape", ref shapeIndex, shapes, shapes.Length))
                    {
                        props["Shape"] = shapes[shapeIndex];
                    }
                    break;
                }
                case "Line Tool":
                {
                    var props = _currentToolSet["Line Tool"].Properties;
                    int constraintsInLine = (int)props["Constraints in Line"];
                    if (ImGui.SliderInt("Constraints in Line", ref constraintsInLine, 1, 500))
                    {
                        props["Constraints in Line"] = constraintsInLine;
                    }

                    float naturalLengthRatio = (float)props["Natural Length Ratio"];
                    if (
                        ImGui.InputFloat("Natural Length Ratio", ref naturalLengthRatio, 0.1f, 3.0f)
                    )
                    {
                        props["Natural Length Ratio"] = naturalLengthRatio;
                    }
                    break;
                }
                case "Oscillating Particle":
                {
                    var props = _currentToolSet["Oscillating Particle"].Properties;
                    float amplitude = (float)props["Amplitude"];
                    if (ImGui.SliderFloat("Amplitude", ref amplitude, 1f, 100f))
                    {
                        props["Amplitude"] = amplitude;
                    }

                    float frequency = (float)props["Frequency"];
                    if (ImGui.SliderFloat("Frequency", ref frequency, 0.1f, 10f))
                    {
                        props["Frequency"] = frequency;
                    }

                    float angle = (float)props["Angle"];
                    if (ImGui.SliderAngle("Angle (degrees)", ref angle, 0f, 360f))
                    {
                        props["Angle"] = angle;
                    }
                    break;
                }
                case "Place Collider":
                {
                    var props = _currentToolSet["Place Collider"].Properties;
                    var colliderObject = (Dictionary<string, object>)props["Object"];

                    string[] colliderTypes = colliderObject.Keys.ToArray();
                    int colliderTypeIndex = Array.IndexOf(
                        colliderTypes,
                        (string)props["SelectedColliderType"]
                    );
                    if (colliderTypeIndex < 0)
                        colliderTypeIndex = 0;
                    if (
                        ImGui.Combo(
                            "Collider Type",
                            ref colliderTypeIndex,
                            colliderTypes,
                            colliderTypes.Length
                        )
                    )
                    {
                        props["SelectedColliderType"] = colliderTypes[colliderTypeIndex];
                    }

                    string selectedColliderType = (string)props["SelectedColliderType"];
                    if (selectedColliderType == "Circle")
                    {
                        var circleProps = (Dictionary<string, object>)colliderObject["Circle"];
                        float radius = (float)circleProps["Radius"];
                        if (ImGui.SliderFloat("Radius", ref radius, 1f, 100f))
                        {
                            circleProps["Radius"] = radius;
                        }
                    }
                    else if (selectedColliderType == "Rectangle")
                    {
                        var rectProps = (Dictionary<string, object>)colliderObject["Rectangle"];
                        float width = (float)rectProps["Width"];
                        float height = (float)rectProps["Height"];
                        float rotation = (float)rectProps["Rotation"];

                        if (ImGui.SliderFloat("Width", ref width, 1f, 200f))
                        {
                            rectProps["Width"] = width;
                        }

                        if (ImGui.SliderFloat("Height", ref height, 1f, 200f))
                        {
                            rectProps["Height"] = height;
                        }

                        if (ImGui.SliderAngle("Rotation", ref rotation, -180f, 180f))
                        {
                            float normalizedRotation = rotation;
                            if (normalizedRotation < 0)
                                normalizedRotation += MathF.PI * 2;
                            rectProps["Rotation"] = normalizedRotation;
                        }
                    }
                    break;
                }
                case "Move Collider":
                    ImGui.Text("Left-click to select and drag colliders");
                    ImGui.Text("(Right-click menu temporarily disabled)");
                    break;
                default:
                    ImGui.Text("No settings available for this tool");
                    break;
            }
        }
        catch (Exception ex)
        {
            ImGui.Text($"Tool Settings Error: {ex.Message}");
            Console.WriteLine($"DrawSelectedToolSettings error: {ex.Message}\n{ex.StackTrace}");
        }
    }

    /// <summary>
    /// makes sure the currently selected tool is valid for the current mode
    /// </summary>
    private void EnsureSelectedToolValid()
    {
        var set = _currentToolSet;
        if (!set.ContainsKey(_selectedToolName))
        {
            var enumerator = set.Keys.GetEnumerator();
            if (enumerator.MoveNext())
            {
                _selectedToolName = enumerator.Current;
            }
        }
    }

    /// <summary>
    /// Pins or unpins particles within a specified radius of a given center point. The method retrieves the IDs of particles that are within the radius from the center, and toggles their pinned state. If a particle is pinned, it becomes unpinned, and if it is unpinned, it becomes pinned. The method also logs the action taken for each particle, including its ID and position. If no particles are found within the radius, it logs a warning message indicating that no particles were affected by the pin tool at the specified location and radius.
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    private void PinParticleBuildable(Vector2 center, float radius)
    {
        var particleIDs = GetMeshParticlesInRadius(center, radius);
        if (particleIDs.Count == 0)
        {
            _logger.AddLog(
                $"Pin tool: no particles found within radius {radius} at {center}",
                ImGuiLogger.LogTypes.Warning
            );
            return;
        }
        foreach (var id in particleIDs)
            if (_activeMesh.Particles.TryGetValue(id, out var particle))
            {
                particle.IsPinned = !particle.IsPinned;
                string action = particle.IsPinned ? "pinned" : "unpinned";
                _logger.AddLog($"Mesh particle {id} {action} at {particle.Position}");
            }
    }

    /// <summary>
    /// Cuts sticks that are within a specified radius of a given center point. The method iterates through all the sticks in the active mesh and calculates the center point of each stick. If the distance from the stick's center to the specified center point is less than or equal to the given radius, the stick is marked for removal. After checking all sticks, the method removes the marked sticks from the mesh and logs the action taken for each cut stick, including its ID and center position. If no sticks are found within the radius, it logs a warning message indicating that no sticks were affected by the cut tool at the specified location and radius.
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    private void CutAllSticksInRadiusBuildable(Vector2 center, float radius)
    {
        var sticksToRemove = new List<int>();

        foreach (var kvp in _activeMesh.Sticks)
        {
            var stick = kvp.Value;
            Vector2 stickCenter = (stick.P1.Position + stick.P2.Position) * 0.5f;
            float distance = Vector2.Distance(stickCenter, center);
            if (distance <= radius)
            {
                sticksToRemove.Add(kvp.Key);
                _logger.AddLog($"Cut stick {kvp.Key} at {stickCenter}");
            }
        }

        foreach (var stickId in sticksToRemove)
        {
            _activeMesh.RemoveStick(stickId);
        }

        if (sticksToRemove.Count == 0)
        {
            _logger.AddLog(
                $"Cut tool: no sticks found within radius {radius} at {center}",
                ImGuiLogger.LogTypes.Warning
            );
        }
    }

    /// <summary>
    /// Applies a wind force to particles based on the drag distance and direction between a start and end position. The method calculates the wind direction as the vector from the start position to the end position, and determines the distance of the drag. If the drag distance is below a specified minimum threshold, it logs a warning message and does not apply any force. If the drag distance is sufficient, it calculates the wind force by scaling the wind direction with the drag distance and a strength factor. The resulting wind force is stored in a variable for later application to particles in the simulation. This method allows users to interactively apply wind forces to particles by dragging across the screen, with configurable parameters for minimum drag distance and strength scaling.
    /// </summary>
    /// <param name="startPos"></param>
    /// <param name="endPos"></param>
    /// <param name="_"></param>
    private void ApplyWindForceFromDrag(Vector2 startPos, Vector2 endPos, float _)
    {
        Vector2 windDirection = endPos - startPos;
        float windDistance = windDirection.Length();

        float minDist = _currentToolSet["Wind"].Properties.ContainsKey("MinDistance")
            ? (float)_currentToolSet["Wind"].Properties["MinDistance"]
            : 5f;

        if (windDistance < minDist)
        {
            _logger.AddLog(
                $"Wind tool: drag distance {windDistance:0.00} below minimum {minDist:0.00}",
                ImGuiLogger.LogTypes.Warning
            );
            return;
        }

        float strength = _currentToolSet["Wind"].Properties.ContainsKey("StrengthScale")
            ? (float)_currentToolSet["Wind"].Properties["StrengthScale"]
            : 1.0f;

        _windForce = windDirection * (windDistance / 50f) * strength;
        _logger.AddLog($"Wind applied: force {_windForce}");
    }

    /// <summary>
    /// cuts all sticks within a segment of a line
    /// </summary>
    /// <param name="lineStart"></param>
    /// <param name="lineEnd"></param>
    private void CutSticksAlongLine(Vector2 lineStart, Vector2 lineEnd)
    {
        _activeMesh.CutSticksAlongLine(lineStart, lineEnd);
    }

    /// <summary>
    /// Retrieves a list of particle IDs from the active mesh that are within a specified radius of a given mouse position. The method iterates through all particles in the active mesh and calculates the distance from each particle's position to the mouse position. If the distance is less than the specified radius, the particle's ID is added to the list of nearby particles. The method also takes an optional parameter for maximum particles to return; if this limit is reached, it returns the list immediately. This function is used by various tools to identify which particles should be affected based on their proximity to the user's cursor.
    /// </summary>
    /// <param name="mousePosition"></param>
    /// <param name="radius"></param>
    /// <param name="maxParticles"></param>
    /// <returns>list of particle IDs</returns>
    private List<int> GetMeshParticlesInRadius(
        Vector2 mousePosition,
        float radius,
        int maxParticles = -1
    )
    {
        var particleIds = new List<int>();

        if (_currentMode is MeshMode.Interact or MeshMode.Edit)
        {
            foreach (var kvp in _activeMesh.Particles)
            {
                Vector2 pos = kvp.Value.Position;
                if (Vector2.DistanceSquared(pos, mousePosition) < (radius * radius))
                {
                    particleIds.Add(kvp.Key);
                }
                if (maxParticles > 0 && particleIds.Count >= maxParticles)
                {
                    return particleIds;
                }
            }
        }

        return particleIds;
    }

    /// <summary>
    /// Drags particles in the active mesh based on the current mouse state and whether the user is actively dragging. The method takes a list of particle IDs that are within the drag radius and updates their positions accordingly. If the user is dragging, it calculates the movement delta from the previous mouse position and applies this delta to each particle's current and previous positions, effectively moving them with the cursor. The particles being dragged are visually highlighted by changing their color to yellow. If the user is not dragging, it resets the color of the affected particles back to white. This method allows for interactive manipulation of particles in the mesh using a drag tool.
    /// </summary>
    /// <param name="mouseState"></param>
    /// <param name="isDragging"></param>
    /// <param name="particleIds"></param>
    private void DragMeshParticles(bool isDragging, List<int> particleIds)
    {
        Vector2 mousePos = Raylib.GetMousePosition();

        if (isDragging && _currentMode != MeshMode.Edit)
        {
            Vector2 frameDelta = mousePos - _previousMousePos;
            foreach (int particleId in particleIds)
            {
                if (_activeMesh.Particles.TryGetValue(particleId, out var particle))
                {
                    if (!particle.IsPinned)
                    {
                        particle.Position += frameDelta;
                        particle.PreviousPosition += frameDelta;
                        particle.Color = Color.Yellow;
                    }
                }
            }
        }
        else
        {
            foreach (int particleId in particleIds)
            {
                if (_activeMesh.Particles.TryGetValue(particleId, out var particle))
                {
                    if (!particle.IsPinned)
                    {
                        particle.Color = Color.White;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Inspects particles within a specified radius of a given center point and logs their information to the console. The method iterates through all particles in the active mesh and calculates the distance from each particle's position to the center point. If the distance is less than or equal to the specified radius, it logs the particle's ID, position, and pinned state to the console. Additionally, it visually highlights inspected particles by changing their color to cyan. If no particles are found within the radius, it logs a warning message indicating that no particles were inspected at the specified location and radius.
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    private void InspectParticlesInRadiusLog(Vector2 center, float radius)
    {
        foreach (var kvp in _activeMesh.Particles)
        {
            var particle = kvp.Value;
            float distance = Vector2.Distance(particle.Position, center);
            particle.Color = Color.White;
            if (distance <= radius)
            {
                _logger.AddLog(
                    $"Particle ID {kvp.Key} - Pos: {particle.Position}, Pinned: {particle.IsPinned}"
                );
                particle.Color = new Color(0, 255, 255, 255); // Cyan
            }
            _activeMesh.Particles[kvp.Key] = particle;
        }
    }

    /// <summary>
    /// Inspects particles within a specified radius of a given center point and visually highlights them in the mesh.
    /// </summary>
    /// <param name="center"></param>
    /// <param name="radius"></param>
    private void InspectParticlesInRadiusWindow(Vector2 center, float radius)
    {
        if ((bool)_interactTools["Select Particles"].Properties["Clear When Use"])
            _inspectedParticles.Clear();
        foreach (var kvp in _activeMesh.Particles)
        {
            var particle = kvp.Value;
            float distance = Vector2.Distance(particle.Position, center);
            particle.Color = Color.White;

            if (distance <= radius)
            {
                particle.Color = new Color(0, 255, 255, 255); // Cyan
                _inspectedParticles.Add(kvp.Key);
            }
            _activeMesh.Particles[kvp.Key] = particle;
        }
    }

    /// <summary>
    /// Inspects particles within a rectangle defined by two corner points and either logs their information to the console or visually highlights them in the mesh,
    /// </summary>
    /// <param name="rectStart"></param>
    /// <param name="rectEnd"></param>
    /// <param name="isLog"></param>
    /// <param name="clearWhenUse"></param>
    private void InspectParticlesInRectangle(
        Vector2 rectStart,
        Vector2 rectEnd,
        bool isLog,
        bool clearWhenUse
    )
    {
        if (clearWhenUse)
            _inspectedParticles.Clear();

        var particles = GetParticlesInRectangle(rectStart, rectEnd);
        foreach (var id in particles)
        {
            if (!_activeMesh.Particles.TryGetValue(id, out var particle))
                continue;

            particle.Color = new Color(0, 255, 255, 255); // Cyan
            _activeMesh.Particles[id] = particle;

            if (!_inspectedParticles.Contains(id))
                _inspectedParticles.Add(id);

            if (isLog)
            {
                _logger.AddLog($"Particle ID {id} at {particle.Position}");
            }
        }
    }

    /// <summary>
    /// gets all partices in a rectangle defined by two corner points used for inspection
    /// </summary>
    /// <param name="rectStart"></param>
    /// <param name="rectEnd"></param>
    /// <returns></returns>
    private List<int> GetParticlesInRectangle(Vector2 rectStart, Vector2 rectEnd)
    {
        var result = new List<int>();
        Rectangle rect = GetRectangleFromPoints(rectStart, rectEnd);

        foreach (var kvp in _activeMesh.Particles)
        {
            var particle = kvp.Value;
            if (Collider.IsPointInside(rect, particle.Position))
            {
                result.Add(kvp.Key);
            }
        }
        return result;
    }

    /// <summary>
    /// Handles the logic for adding a stick between two particles when the user clicks on the canvas. The method first retrieves the radius parameter from the tool settings and then gets the IDs of particles that are within this radius of the click position. If no particles are found, it logs a warning message. If one or more particles are found, it checks if there is already a first particle selected for creating a stick. If not, it sets the first particle ID and highlights it in yellow. If there is already a first particle selected and the newly clicked particle is different, it creates a stick between the two particles, logs the action, and resets their colors back to white. If the user clicks on the same particle that is already selected as the first particle, it logs a message indicating that a stick cannot be created to the same particle and prompts the user to select a different particle.
    /// </summary>
    /// <param name="clickPos"></param>
    private void HandleAddStickBetweenParticlesClick(Vector2 clickPos)
    {
        float radius = _currentToolSet["Add Stick Between Particles"].Properties["Radius"]
            is float r
            ? r
            : 10f;
        var ids = GetMeshParticlesInRadius(clickPos, radius, 1);
        if (ids.Count == 0)
        {
            _logger.AddLog(
                $"Add Stick tool: no particle found within radius {radius} at {clickPos}",
                ImGuiLogger.LogTypes.Warning
            );
            return;
        }
        if (ids.Count >= 1)
        {
            int hitId = ids[0];
            if (_stickToolFirstParticleId == null)
            {
                _stickToolFirstParticleId = hitId;
                if (_activeMesh.Particles.TryGetValue(hitId, out var p))
                {
                    p.Color = Color.Yellow;
                    _activeMesh.Particles[hitId] = p;
                }
            }
            else if (_stickToolFirstParticleId.Value != hitId)
            {
                _activeMesh.AddStickBetween(_stickToolFirstParticleId.Value, hitId);
                _logger.AddLog(
                    $"Added stick between particles {_stickToolFirstParticleId.Value} and {hitId}",
                    ImGuiLogger.LogTypes.Info
                );
                if (_activeMesh.Particles.TryGetValue(_stickToolFirstParticleId.Value, out var p1))
                {
                    p1.Color = Color.White;
                    _activeMesh.Particles[_stickToolFirstParticleId.Value] = p1;
                }
                if (_activeMesh.Particles.TryGetValue(hitId, out var p2))
                {
                    p2.Color = Color.White;
                    _activeMesh.Particles[hitId] = p2;
                }
                _stickToolFirstParticleId = null;
            }
            else
            {
                _logger.AddLog(
                    "Cannot create stick to the same particle. Select a different particle."
                );
            }
        }
    }

    /// <summary>
    /// Pushes the History for undo and redo functionality. When a change is made to the mesh, this method creates a deep copy of the current active mesh and pushes it onto the history stack. It also clears the redo history stack, ensuring that any new changes invalidate the previous redo states. This allows users to undo and redo their actions on the mesh effectively, maintaining a consistent state of the mesh throughout their editing process.
    /// </summary>
    private void MeshHistoryPush()
    {
        _meshHistory.Push(_activeMesh.DeepCopy());
        _meshRedoHistory.Clear();
    }

    /// <summary>
    /// creates a recangle from two points
    /// </summary>
    /// <param name="point1"></param>
    /// <param name="point2"></param>
    /// <returns>axis alignedRectangle</returns>
    private Rectangle GetRectangleFromPoints(Vector2 point1, Vector2 point2)
    {
        int x = (int)Math.Min(point1.X, point2.X);
        int y = (int)Math.Min(point1.Y, point2.Y);
        int width = (int)Math.Abs(point1.X - point2.X);
        int height = (int)Math.Abs(point1.Y - point2.Y);
        return new Rectangle(x, y, width, height);
    }
}
