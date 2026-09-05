using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

/// <summary>
/// Main menu bar and factory menu functionality.
/// </summary>
public partial class Game1
{
    /// <summary>
    /// the currently selected factory action, used for quick structure creation
    /// </summary>
    private Factory _selectedFactoryAction;

    /// <summary>
    /// draws the main menu bar at the top of the screen
    /// </summary>
    private void DrawMainMenuBar()
    {
        if (!ImGui.BeginMainMenuBar())
        {
            return;
        }

        if (ImGui.BeginMenu("File"))
        {
            if (ImGui.MenuItem("New"))
            {
                _showConfirmNewMeshPopup = true;
            }
            if (ImGui.MenuItem("Open"))
            {
                _showStructureWindow = true;
            }
            if (ImGui.MenuItem("Save"))
            {
                _showSaveWindow = true;
            }
            if (ImGui.MenuItem("Sign In/Sign Out"))
            {
                _showSignInWindow = true;
            }

            if (ImGui.MenuItem("Exit"))
            {
                Raylib.CloseWindow();
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Time"))
        {
            if (ImGui.Button(_paused ? "Resume (Esc)" : "Pause (Esc)"))
            {
                _paused = !_paused;
            }

            ImGui.SameLine();
            ImGui.PushStyleColor(
                ImGuiCol.Button,
                new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1f)
            );
            if (ImGui.Button("Reset Simulation (R)"))
            {
                _activeMesh.ResetMesh();
            }

            ImGui.PopStyleColor();
            ImGui.BeginDisabled(!_paused);

            if (ImGui.Button("Step Forward (T)"))
            {
                _stepsToStep += _buttonSteps;
            }

            ImGui.SameLine();
            ImGui.InputInt("Steps to Step", ref _buttonSteps, 1, 100);

            ImGui.EndDisabled();
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Mode"))
        {
            if (ImGui.MenuItem("Interact", null, _currentMode == MeshMode.Interact))
            {
                SetMode(MeshMode.Interact);
            }
            if (ImGui.MenuItem("Edit", null, _currentMode == MeshMode.Edit))
            {
                SetMode(MeshMode.Edit);
            }
            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Assignments"))
        {
            if (ImGui.MenuItem("Browse by Teacher"))
            {
                _showTeacherAssignmentsWindow = true;
                if (_allTeachers.Count == 0)
                {
                    try
                    {
                        _allTeachers = _database.GetTeachersWithInfo();
                        foreach (var teacher in _allTeachers)
                        {
                            _teacherAssignments[teacher.Id] = _database.GetAssignmentsForTeacher(
                                teacher.Id
                            );
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.AddLog(
                            $"Could not load teacher assignments: {ex.Message}",
                            ImGuiLogger.LogTypes.Error
                        );
                    }
                }
            }
            ImGui.Separator();
            DrawAssignmentMenuItems();

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Quick Structures"))
        {
            QuickStructureMenu();

            ImGui.EndMenu();
        }

        if (ImGui.BeginMenu("Quick Settings"))
        {
            ImGui.SliderFloat("Global Mass", ref _activeMesh.mass, 0.1f, 100f);
            ImGui.SliderFloat("Spring Constant", ref _activeMesh.springConstant, 100f, 100000f);
            ImGui.SliderInt("Constraint Iterations", ref _solverIterations, 1, 16);
            ImGui.TextDisabled("XPBD compliance is derived from spring stiffness.");

            ImGui.SliderInt("Physics Substeps", ref _subSteps, 1, 20);
            ImGui.SliderFloat(
                "Collision Friction Coefficient",
                ref _activeMesh.collisionFrictionCoefficient,
                0f,
                1f
            );
            ImGui.SliderFloat(
                "Collision Bounce Coefficient",
                ref _activeMesh.collisionBounceCoefficient,
                0f,
                1f
            );

            float drag = _activeMesh.drag;
            if (ImGui.SliderFloat("Drag (1.0 = no friction)", ref drag, 0.9f, 1.0f))
            {
                _activeMesh.drag = drag;
            }

            ImGui.SliderFloat("Base Force X", ref _baseForce.X, -1000f, 1000f);
            ImGui.SliderFloat("Base Force Y", ref _baseForce.Y, -1000f, 1000f);
            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Tool Menu"))
        {
            DrawToolMenuItems();
            ImGui.Separator();
            DrawSelectedToolSettings();
            ImGui.EndMenu();
        }
        if (ImGui.MenuItem(_paused ? "Resume (Esc)" : "Pause (Esc)"))
        {
            _paused = !_paused;
        }
        ImGui.SameLine();
        if (ImGui.BeginMenu("Show"))
        {
            ImGui.MenuItem("Configuration", null, ref _showConfigurationWindow);
            ImGui.MenuItem("Logger", null, ref _showLoggerWindow);
            ImGui.MenuItem("ReadMe", null, ref _showReadMeWindow);

            ImGui.EndMenu();
        }
        if (ImGui.BeginMenu("Factories"))
        {
            FactoryMenu();

            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }

    /// <summary>
    /// the menu for all factories that can be used to quickly create complex structures in the mesh,
    /// </summary>
    private void FactoryMenu()
    {
        foreach (var factory in _factories)
        {
            if (ImGui.Selectable(factory.Name, false))
            {
                _selectedFactoryAction = factory;
            }
        }

        if (_selectedFactoryAction == null)
        {
            return;
        }

        foreach (var key in _selectedFactoryAction.Parameters.Keys.ToList())
        {
            string value = _selectedFactoryAction.Parameters[key];
            if (ImGui.InputText(key, ref value, 100))
            {
                _selectedFactoryAction.Parameters[key] = value;
            }
        }

        if (ImGui.Button("Run Factory") && _selectedFactoryAction != null)
        {
            MeshHistoryPush();
            try
            {
                object[] args = _selectedFactoryAction.Parameters.Values.Cast<object>().ToArray();
                _selectedFactoryAction.Method(args);
            }
            catch (Exception ex)
            {
                _logger.AddLog(
                    $"Factory execution error: {ex.Message}",
                    ImGuiLogger.LogTypes.Error
                );
            }
        }
    }
}
