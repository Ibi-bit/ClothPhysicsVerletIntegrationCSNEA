using System;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using System.Numerics;

namespace PhysicsCSAlevlProject;

/// <summary>
/// Window drawing functionality for structures, save dialogs, readme, and configuration.
/// </summary>
public partial class Game1
{
    /// <summary>
    /// Draws the structure window for loading and managing saved meshes.
    /// </summary>
    private void DrawStructureWindow()
    {
        if (!ImGui.Begin("Structure", ref _showStructureWindow))
        {
            ImGui.End();
            return;
        }
        ImGui.Text("Available Meshes:");
        ImGui.Separator();

        var meshes = LoadAllMeshesFromDirectory(_structurePath);

        if (meshes.Count == 0)
        {
            ImGui.TextDisabled("No saved meshes found.");
        }

        foreach (var meshEntry in meshes)
        {
            if (ImGui.Button($"Load {meshEntry.Key}"))
            {
                _activeMesh = meshEntry.Value;
                _defaultMesh = _activeMesh;
                SetMode(MeshMode.Interact);
                _showStructureWindow = false;
            }
        }

        ImGui.End();
    }

    /// <summary>
    /// Draws the save window for saving the current mesh with a specified name.
    ///
    /// </summary>
    private void DrawSaveWindow()
    {
        if (!ImGui.Begin("Save Mesh", ref _showSaveWindow))
        {
            ImGui.End();
            return;
        }
        ImGui.InputText("Mesh Name", ref _meshName, 100);
        if (ImGui.Button("Save"))
        {
            SaveMeshToJSON(_activeMesh, _meshName, _structurePath);
        }
        ImGui.End();
    }

    /// <summary>
    /// Draws the readme window with instructions and tips for using the application.
    ///
    /// </summary>
    private void DrawReadMeWindow()
    {
        if (!ImGui.Begin("ReadMe", ref _showReadMeWindow))
        {
            ImGui.End();
            return;
        }

        string modeBackCycleModifier = OperatingSystem.IsMacOS() ? "Alt" : "Ctrl";
        string currentUserText =
            _currentUser == null
                ? "Not signed in"
                : $"{_currentUser.Username} (ID {_currentUser.Id})";

        ImGui.TextWrapped(
            "Cloth Physics quick guide. Use this panel as a fast reference for controls and common workflows."
        );
        ImGui.Separator();

        ImGui.Text("Keyboard Shortcuts");
        ImGui.BulletText("Esc: Pause / Resume simulation");
        ImGui.BulletText("Space (while paused): Step 1 physics tick");
        ImGui.BulletText("Ctrl + Z: Undo");
        ImGui.BulletText("Ctrl + Shift + Z: Redo");
        ImGui.BulletText("Shift + Tab: Next mode");
        ImGui.BulletText($"Shift + {modeBackCycleModifier} + Tab: Previous mode");
        ImGui.BulletText("Ctrl + T: Next tool");
        ImGui.BulletText("Ctrl + Shift + T: Previous tool");
        ImGui.BulletText("D: Switch to Drag tool");
        ImGui.BulletText("C (Create Grid Mesh tool): Build grid in selected rectangle");

        ImGui.Separator();
        ImGui.Text("Mouse Basics");
        ImGui.BulletText("Left click / drag applies the currently selected tool action");
        ImGui.BulletText("Drag and PhysicsDrag: drag particles in the selected radius");
        ImGui.BulletText("Wind and LineCut: click-drag to draw effect direction/line");
        ImGui.BulletText("LineCut: Works in both Interact and Edit modes");

        ImGui.Separator();
        ImGui.Text("Helpful Tips");
        ImGui.BulletText("Use Time menu while paused to step multiple ticks quickly");
        ImGui.BulletText("Tune mass, spring constant, drag and substeps in Quick Settings");
        ImGui.BulletText("Save local structures in Quick Structures and refresh to reload list");
        ImGui.BulletText("Sign in to save/load remote structures and submit assignment work");
        ImGui.BulletText("Use Select Particles to inspect/pin/remove groups safely");

        ImGui.Separator();
        ImGui.Text("Current Session");
        ImGui.BulletText($"Mode: {_currentMode}");
        ImGui.BulletText($"Tool: {_selectedToolName}");
        ImGui.BulletText($"Simulation: {(_paused ? "Paused" : "Running")}");
        ImGui.BulletText($"User: {currentUserText}");

        ImGui.End();
    }

    /// <summary>
    /// draws the settings menu for changing window size, physics settings and other configuration options
    /// </summary>
    private void DrawConfigurationWindow()
    {
        if (!ImGui.Begin("Configuration", ref _showConfigurationWindow))
        {
            ImGui.End();
            return;
        }

        ImGui.Text("Window Size:");
        if (_changedBounds.Width <= 0 || _changedBounds.Height <= 0)
        {
            _changedBounds = _windowBounds;
        }

        bool ratioToggled = ImGui.Checkbox("Keep Aspect Ratio", ref _keepAspectRatio);
        if (ratioToggled && _keepAspectRatio)
        {
            _lockedAspectRatio =
                _changedBounds.Height > 0
                    ? _changedBounds.Width / (float)_changedBounds.Height
                    : 1f;
        }
        else if (_keepAspectRatio && _lockedAspectRatio <= 0.0001f)
        {
            _lockedAspectRatio =
                _changedBounds.Height > 0
                    ? _changedBounds.Width / (float)_changedBounds.Height
                    : 1f;
        }

        float newWidth = _changedBounds.Width;
        float newHeight = _changedBounds.Height;
        bool widthChanged = ImGui.InputFloat("Width", ref newWidth);

        ImGui.BeginDisabled(_keepAspectRatio);
        bool heightChanged = ImGui.InputFloat("Height", ref newHeight);
        ImGui.EndDisabled();

        if (_keepAspectRatio)
        {
            float aspect =
                _lockedAspectRatio > 0.0001f
                    ? _lockedAspectRatio
                    : (
                        _changedBounds.Height > 0
                            ? _changedBounds.Width / (float)_changedBounds.Height
                            : 1f
                    );

            if (widthChanged && newWidth > 0)
            {
                _changedBounds.Width = newWidth;
                _changedBounds.Height = Math.Max(1, (int)Math.Round(newWidth / aspect));
            }
            else if (heightChanged && newHeight > 0)
            {
                _changedBounds.Height = newHeight;
                _changedBounds.Width = Math.Max(1, (int)Math.Round(newHeight * aspect));
            }
        }
        else
        {
            if (widthChanged)
                _changedBounds.Width = Math.Max(1, newWidth);
            if (heightChanged)
                _changedBounds.Height = Math.Max(1, newHeight);
        }

        if (ImGui.Button("Apply Size"))
        {
            SetWindowSize(_changedBounds.Width, _changedBounds.Height);
        }
        ImGui.Checkbox("Draw Particles", ref _drawParticles);
        ImGui.Checkbox("Draw Constraints", ref _drawConstraints);
        ImGui.SliderFloat("Constraint Thickness", ref _activeMesh.stickDrawThickness, 1f, 10f);

        ImGui.Separator();
        ImGui.Text("Physics Controls:");

        ImGui.InputFloat("Global Mass", ref _activeMesh.mass);
        ImGui.InputFloat("Global Spring Constant", ref _activeMesh.springConstant);
        ImGui.SliderInt("Physics Substeps", ref _subSteps, 1, 600);

        float drag = _activeMesh.drag;
        if (ImGui.SliderFloat("Drag (1.0 = no friction)", ref drag, 0.9f, 1.0f))
        {
            _activeMesh.drag = drag;
        }

        ImGui.Separator();
        ImGui.Text("Base Force:");

        ImGui.SliderFloat("X", ref _baseForce.X, -1000f, 1000f);
        ImGui.SameLine();
        if (ImGui.Button("Set X to 0"))
        {
            _baseForce.X = 0;
        }

        ImGui.SliderFloat("Y", ref _baseForce.Y, -1000f, 1000f);
        ImGui.SameLine();
        if (ImGui.Button("Set Y to 0"))
        {
            _baseForce.Y = 0;
        }
        if (ImGui.Button("Reset Base Force"))
        {
            _baseForce = new Vector2(0, 980f);
        }

        ImGui.Text($"Current Base Force: {_baseForce}");
        ImGui.Text("Note: The Y axis is inverted, so positive Y values point downwards.");

        ImGui.Separator();

        if (ImGui.Button(_paused ? "Resume (Esc)" : "Pause (Esc)"))
        {
            _paused = !_paused;
        }
        ImGui.SameLine();
        var red = new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1f);
        ImGui.PushStyleColor(ImGuiCol.Button, red);
        if (ImGui.Button("Reset Simulation", new System.Numerics.Vector2(120, 0)))
        {
            _activeMesh.ResetMesh();
        }
        ImGui.PopStyleColor();

        ImGui.End();
    }
}
