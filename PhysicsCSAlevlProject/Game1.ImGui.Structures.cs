using System;
using System.Collections.Generic;
using ImGuiNET;

namespace PhysicsCSAlevlProject;

/// <summary>
/// Quick structure menu and structure management functionality.
/// </summary>
public partial class Game1
{
    /// <summary>
    /// wether to show the local or remote structures in the quick structure menu
    /// </summary>
    private bool _LocalorRemoteStructureTab;

    /// <summary>
    /// Provides a user interface for managing quick access to both local and remote structures. Users can toggle between viewing
    /// locally saved structures and structures stored in a remote database. The local structures tab allows users to refresh the list of available meshes, save the current mesh with a specified name, and load existing meshes from a designated directory. The remote structures tab enables signed-in users to save their current mesh to the database with a custom name, refresh the list of their saved structures, and load any of their previously saved structures into the application. This menu streamlines the process of managing and accessing different mesh configurations for users, enhancing their workflow and organization.
    /// </summary>
    private void QuickStructureMenu()
    {
        if (
            ImGui.Button(
                $"Switch to {(_LocalorRemoteStructureTab ? "Local" : "Remote")} Structures"
            )
        )
        {
            _LocalorRemoteStructureTab = !_LocalorRemoteStructureTab;
        }

        ImGui.Separator();

        if (!_LocalorRemoteStructureTab)
        {
            ImGui.Text("Local Structures:");

            if (_isLoadingQuickMeshes)
            {
                ImGui.TextDisabled(
                    string.IsNullOrWhiteSpace(_loadingQuickMeshFile)
                        ? "Loading structures..."
                        : $"Loading: {_loadingQuickMeshFile}"
                );
            }
            else if (ImGui.Button("Refresh List"))
            {
                _ = LoadAllMeshesFromDirectoryAsync(_structurePath);
            }

            if (ImGui.Button("Save Current Mesh"))
            {
                SaveMeshToJSON(_activeMesh, _quickStructureName, _structurePath);
                _ = LoadAllMeshesFromDirectoryAsync(_structurePath);
            }

            ImGui.SameLine();
            ImGui.InputText("Structure Name", ref _quickStructureName, 100);

            ImGui.BeginChild("QuickStructureLocalList", new System.Numerics.Vector2(0, 200));
            Dictionary<string, Mesh> quickMeshesSnapshot;
            lock (_quickMeshesLock)
            {
                quickMeshesSnapshot = new Dictionary<string, Mesh>(_quickMeshes);
            }

            if (quickMeshesSnapshot.Count == 0 && !_isLoadingQuickMeshes)
            {
                ImGui.TextDisabled("No local structures found.");
            }

            foreach (var meshEntry in quickMeshesSnapshot)
            {
                if (ImGui.MenuItem(meshEntry.Key))
                {
                    var mesh = meshEntry.Value;
                    _activeMesh = mesh;
                    _defaultMesh = mesh;
                    _quickStructureName = meshEntry.Key;
                    SetMode(MeshMode.Interact);
                }
            }
            ImGui.EndChild();
        }
        else
        {
            ImGui.Separator();
            ImGui.Text("Remote Structures (Database):");

            if (_currentUser != null)
            {
                ImGui.BeginChild("QuickStructureRemote", new System.Numerics.Vector2(0, 200));

                ImGui.InputText("Save Name", ref _remoteSaveName, 100);
                ImGui.SameLine();
                if (ImGui.Button("Save to Database"))
                {
                    try
                    {
                        string json = SaveMeshToJsonString(_activeMesh);
                        int structureId = _database.SaveStructureWithName(
                            _currentUser.Id,
                            json,
                            _remoteSaveName
                        );
                        _logger.AddLog($"Saved structure to database (ID: {structureId})");
                        _remoteStructures = _database.GetStructuresForUser(_currentUser.Id);
                    }
                    catch (Exception ex)
                    {
                        _logger.AddLog(
                            $"Failed to save to database: {ex.Message}",
                            ImGuiLogger.LogTypes.Error
                        );
                    }
                }

                ImGui.Separator();

                if (ImGui.Button("Refresh Remote List"))
                {
                    try
                    {
                        _remoteStructures = _database.GetStructuresForUser(_currentUser.Id);
                        _logger.AddLog($"Loaded {_remoteStructures.Count} remote structures");
                    }
                    catch (Exception ex)
                    {
                        _logger.AddLog(
                            $"Failed to load remote structures: {ex.Message}",
                            ImGuiLogger.LogTypes.Error
                        );
                    }
                }

                foreach (var structure in _remoteStructures)
                {
                    if (ImGui.Selectable(structure.DisplayName))
                    {
                        try
                        {
                            string json = _database.GetStructureContent(structure.Id);
                            if (!string.IsNullOrEmpty(json))
                            {
                                _activeMesh = LoadMeshFromJsonString(json);
                                _defaultMesh = _activeMesh;
                                SetMode(MeshMode.Interact);
                                _logger.AddLog($"Loaded structure: {structure.AssignmentTitle}");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.AddLog(
                                $"Failed to load structure: {ex.Message}",
                                ImGuiLogger.LogTypes.Error
                            );
                        }
                    }
                }

                ImGui.EndChild();
            }
            else
            {
                ImGui.TextColored(
                    new System.Numerics.Vector4(1f, 1f, 0f, 1f),
                    "Sign in to access remote structures."
                );
            }
        }
    }
}
