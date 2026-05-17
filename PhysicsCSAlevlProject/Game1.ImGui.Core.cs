using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;
using System.Numerics;

namespace PhysicsCSAlevlProject;

/// <summary>
/// Core ImGui functionality including initialization, main draw loop, and mode/tool switching.
/// </summary>
public partial class Game1
{
    /// <summary>
    /// toggle for if the config window is open
    /// </summary>
    private bool _showConfigurationWindow;

    /// <summary>
    /// toggle for if the readme window is open
    /// </summary>
    private bool _showReadMeWindow;

    ///<summary>
    /// toggle for if the structure window is open
    /// </summary>
    private bool _showStructureWindow;

    /// <summary>
    /// toggle for if the save window is open
    /// </summary>
    private bool _showSaveWindow;

    /// <summary>
    /// toggle for if the sign-in window is open
    /// </summary>
    private bool _showSignInWindow;

    /// <summary>
    /// toggle for if the logger window is open
    /// </summary>
    private bool _showLoggerWindow;

    /// <summary>
    /// toggle for if the confirm new mesh popup is open
    /// </summary>
    private bool _showConfirmNewMeshPopup;

    /// <summary>
    /// toggle for if the save assignment popup is open
    /// </summary>
    private bool _showSaveAssignmentPopup;

    /// <summary>
    /// toggle for if the teacher assignments window is open
    /// </summary>
    private bool _showTeacherAssignmentsWindow;

    /// <summary>
    /// the logger used for logging messages to the ImGui logger window
    /// </summary>
    private ImGuiLogger _logger;

    /// <summary>
    /// the name of the current mesh, used for saving and loading
    /// </summary>
    private string _meshName;

    /// <summary>
    /// the path to the directory where structures are saved and loaded from
    /// </summary>
    private string _structurePath;

    /// <summary>
    /// the name of the quick structure, used for saving and loading quick structures
    /// </summary>
    private string _quickStructureName;

    /// <summary>
    /// the collider currently being used for cursor interaction
    /// </summary>
    private Collider _cursorCollider;

    /// <summary>
    /// a store of colliders that can be used for cursor interaction, indexed by name
    /// </summary>
    private Dictionary<string, Collider> _cursorColliderStore;

    /// <summary>
    /// a dictionary of meshes that can be quickly loaded, indexed by name
    /// </summary>
    private Dictionary<string, Mesh> _quickMeshes;

    /// <summary>
    /// a dictionary of template functions for creating new meshes based on predefined configurations, indexed by name
    /// </summary>
    private Dictionary<string, Func<Mesh>> _template;

    ///<summary>
    /// the number of steps to simulate when using the step forward and step back
    /// </summary>
    private int _buttonSteps;

    ///<summary>
    /// if ctrl is currently held, used for tool switching
    /// </summary>
    private bool _ctrlHeld;

    ///<summary>
    /// if shift is currently held, used for mode and tool switching
    /// </summary>
    private bool _shiftHeld;

    ///<summary>
    /// if caps lock is currently active, used for mode switching
    /// </summary>
    private bool _capsActive;

    ///<summary>
    /// if alt is currently held, used for mode switching on Mac
    /// </summary>
    private bool _altHeld;

    /// <summary>
    /// whats in the user id input field in the sign-in window
    /// </summary>
    private string _userInputUserId;

    /// <summary>
    /// whats in the password input field in the sign-in window
    /// </summary>
    private string _password;

    /// <summary>
    /// stores all remote structures for the user, loaded from the database when the user signs in
    /// </summary>
    private List<Game1Database.StructureInfo> _remoteStructures = new();

    /// <summary>
    /// the name of the structure to save to the database when saving, taken from the input field in the teacher assignments window
    /// </summary>
    private string _remoteSaveName = "MyStructure";

    /// <summary>
    /// lists all teachers in the database
    /// </summary>
    private List<Game1Database.User> _allTeachers = new();

    /// <summary>
    /// stores the assignments for each teacher, loaded from the database when the user opens the teacher assignments window
    /// </summary>
    private Dictionary<int, List<Game1Database.Assignment>> _teacherAssignments = new();

    /// <summary>
    /// the index of the currently selected teacher tab in the teacher assignments window, used for displaying the correct assignments when the user has multiple teachers
    /// </summary>
    private int _selectedTeacherTabIndex = 0;

    /// <summary>
    /// stores the input field for creating a new assigmnment in the teacher assignments window
    /// </summary>
    private string _newAssignmentTitle = "";

    /// <summary>
    /// stores the input field for creating a new assignment description in the teacher assignments window
    /// </summary>
    private string _newAssignmentDescription = "";

    /// <summary>
    /// stores the input field for creating a new assignment due date in the teacher assignments window
    /// </summary>
    private string _newAssignmentDueDate = "";

    /// <summary>
    /// initializes ImGui related variables and state
    /// </summary>
    private void InitializeImGui()
    {
        rlImGui.Setup(true);

        // ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.ViewportsEnable;
        // ImGui.GetIO().ConfigFlags |= ImGuiConfigFlags.DockingEnable;
        // ImGui.DockSpace(ImGui.GetID("MyDockspace"));

        _showConfigurationWindow = false;
        _showReadMeWindow = false;
        _showStructureWindow = false;
        _showSaveWindow = false;
        _showSignInWindow = false;
        _showLoggerWindow = false;
        _showConfirmNewMeshPopup = false;
        _showTeacherAssignmentsWindow = false;

        _buttonSteps = 10;

        _logger = new ImGuiLogger();
        _database = new Game1Database(ref _logger);

        _logger.RegisterEnvVar("mouseX", () => Raylib.GetMousePosition().X.ToString());
        _logger.RegisterEnvVar("mouseY", () => Raylib.GetMousePosition().Y.ToString());
        _logger.RegisterEnvVar("windowW", () => _windowBounds.Width.ToString());
        _logger.RegisterEnvVar("windowH", () => _windowBounds.Height.ToString());
        _meshName = "MyMesh";
        _structurePath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "..",
            "..",
            "..",
            "JSONStructures"
        );
        _quickStructureName = "QuickStructure";

        _cursorColliderStore = new Dictionary<string, Collider>
        {
            { "Rectangle", new RectangleCollider(new Rectangle(0, 0, 10, 10)) },
            { "Circle", new CircleCollider(Vector2.Zero, 5f) },
        };
        _cursorCollider = _cursorColliderStore["Rectangle"];
        _quickMeshes = LoadAllMeshesFromDirectory(_structurePath);
        _template = new Dictionary<string, Func<Mesh>>
        {
            {
                "Cloth 20x20",
                () =>
                    Mesh.CreateClothMesh(
                        new Vector2(220, 20),
                        new Vector2(420, 320),
                        10f,
                        null,
                        _activeMesh.springConstant,
                        _activeMesh.drag,
                        _activeMesh.mass
                    )
            },
            {
                "Cloth 30x20",
                () =>
                    Mesh.CreateClothMesh(
                        new Vector2(220, 20),
                        new Vector2(520, 220),
                        10f,
                        null,
                        _activeMesh.springConstant,
                        _activeMesh.drag,
                        _activeMesh.mass
                    )
            },
        };

        _userInputUserId = "";
        _password = "";
        _newAssignmentTitle = "";
        _newAssignmentDescription = "";
        _newAssignmentDueDate = "";

        _commandRegistry = new CommandRegistry(_logger);
        _commandRegistry.RegisterType(this, typeof(Game1));
        _commandRegistry.RegisterType(_activeMesh, typeof(Mesh));
    }

    /// <summary>
    /// main drawing fucntion for imgui called every frame  in the main draw loop
    /// </summary>
    /// <param name="gameTime"></param>
    private void ImGuiDraw(GameTime gameTime)
    {
        rlImGui.Begin();
        _ctrlHeld = ImGui.GetIO().KeyCtrl;
        _shiftHeld = ImGui.GetIO().KeyShift;
        _altHeld = ImGui.GetIO().KeyAlt;

        if (ImGui.IsKeyPressed(ImGuiKey.CapsLock))
        {
            _capsActive = !_capsActive;
        }

        DrawMainMenuBar();
        // Temporarily disabled - uncomment when crash is fixed
        // if (_selectedToolName == "Move Collider" && _currentMode == MeshMode.Edit)
        // {
        //     DrawRightClickColliderMenu();
        // }
        if (_showConfigurationWindow)
        {
            DrawConfigurationWindow();
        }
        if (_showReadMeWindow)
        {
            DrawReadMeWindow();
        }
        if (_showStructureWindow)
        {
            DrawStructureWindow();
        }
        if (_showSaveWindow)
        {
            DrawSaveWindow();
        }
        if (_showLoggerWindow)
        {
            _logger.DrawLogs(ref _showLoggerWindow);
        }
        if (_showSignInWindow)
        {
            DrawSignInWindow();
        }
        if (_showTeacherAssignmentsWindow)
        {
            DrawTeacherAssignmentsWindow();
        }
        if (_inspectedParticles.Count > 0)
        {
            DrawInspectParticleWindow();
        }
        DrawPopUps();

        ModeSwitchingImGui();
        ToolSwitchingImGui();

        rlImGui.End();

    }

    /// <summary>
    /// opens all pop up windows based on their respective toggles and then unset the toggles
    /// </summary>
    private void DrawPopUps()
    {
        if (_showConfirmNewMeshPopup)
        {
            ImGui.OpenPopup("ConfirmNewMeshPopup");
            _showConfirmNewMeshPopup = false;
        }
        if (_showSaveAssignmentPopup && _selectedAssignment != null)
        {
            ImGui.OpenPopup("SaveToAssignmentPopup");
            _showSaveAssignmentPopup = false;
        }
        PopUpSaveProjectToAssignment(_selectedAssignment);

        if (ImGui.BeginPopupModal("ConfirmNewMeshPopup"))
        {
            ImGui.Text("Are you sure you want to create a new mesh? Unsaved changes will be lost.");
            if (ImGui.Button("Yes"))
            {
                _activeMesh = new Mesh();
                _defaultMesh = _activeMesh;
                SetMode(MeshMode.Interact);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("No"))
            {
                ImGui.CloseCurrentPopup();
            }
            ImGui.EndPopup();
        }
    }

    /// <summary>
    /// handles the switching between interaction modes by using the ImGui input system over monogames and draws the overlay
    /// that shows the current mode and the other modes available when shift is held, this allows for quick and intuitive switching between modes without needing to navigate through menus
    /// </summary>
    private void ModeSwitchingImGui()
    {
        bool isMac = OperatingSystem.IsMacOS();
        bool backwardModifierHeld = isMac ? _altHeld : _ctrlHeld;

        if (_shiftHeld && ImGui.IsKeyPressed(ImGuiKey.Tab))
        {
            int delta = backwardModifierHeld ? -1 : 1;
            int modeCount = Enum.GetValues(typeof(MeshMode)).Length;
            int newModeIndex = ((int)_currentMode + delta + modeCount) % modeCount;
            SetMode((MeshMode)newModeIndex);
        }

        if (_shiftHeld && !_ctrlHeld)
        {
            var drawList = ImGui.GetForegroundDrawList();
            drawList.AddText(
                new System.Numerics.Vector2(10, 30),
                ImGui.GetColorU32(ImGuiCol.Text),
                "Mode:"
            );
            var modes = Enum.GetValues(typeof(MeshMode));
            for (int i = 0; i < modes.Length; i++)
            {
                var mode = (MeshMode)modes.GetValue(i)!;
                string text = mode == _currentMode ? $"> {mode} <" : $"  {mode}";
                uint color =
                    mode == _currentMode
                        ? ImGui.GetColorU32(new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f))
                        : ImGui.GetColorU32(ImGuiCol.Text);
                drawList.AddText(new System.Numerics.Vector2(10, 48 + i * 18), color, text);
            }
        }
    }

    /// <summary>
    /// handles the switching between tools by using the ImGui input system over monogames and draws the overlay that shows the current tool and the other tools available when ctrl is held, this allows for quick and intuitive switching between tools without needing to navigate through menus
    /// </summary>
    private void ToolSwitchingImGui()
    {
        if (!_ctrlHeld)
            return;
        List<string> toolNames = _currentToolSet.Keys.ToList();
        int currentIndex = toolNames.IndexOf(_selectedToolName);
        if (_shiftHeld && ImGui.IsKeyPressed(ImGuiKey.T))
        {
            int nextIndex = (currentIndex - 1 + toolNames.Count) % toolNames.Count;
            _selectedToolName = toolNames[nextIndex];
            _logger.AddLog($"Switched to tool: {_selectedToolName}");
        }
        else if (ImGui.IsKeyPressed(ImGuiKey.T))
        {
            int nextIndex = (currentIndex + 1) % toolNames.Count;
            _selectedToolName = toolNames[nextIndex];
            _logger.AddLog($"Switched to tool: {_selectedToolName}");
        }
        for (int i = 0; i < toolNames.Count; i++)
        {
            var toolName = toolNames[i];
            uint color =
                toolName == _selectedToolName
                    ? ImGui.GetColorU32(new System.Numerics.Vector4(0.2f, 1f, 0.2f, 1f))
                    : ImGui.GetColorU32(ImGuiCol.Text);
            ImGui
                .GetForegroundDrawList()
                .AddText(new System.Numerics.Vector2(10, 60 + i * 18), color, toolName);
        }
    }

    /// <summary>
    /// Sets the current mode of the application (Interact or Edit) and updates the state accordingly. When switching to Edit mode, the simulation is paused to allow for editing without physics interference. The method also pushes the current mesh state to the history stack for undo functionality, resets input states related to mouse interactions, and clears any temporary data used for tools like wind or cutting. This ensures a smooth transition between modes while preserving the user's work and providing a consistent experience when switching between interacting with the mesh and editing it.
    /// </summary> <param name="mode"></param>
    private void SetMode(MeshMode mode)
    {
        _currentMode = mode;

        _activeMesh ??= _defaultMesh;
        EnsureSelectedToolValid();
        if (_currentMode == MeshMode.Edit)
        {
            _paused = true;
        }

        MeshHistoryPush();
        _leftPressed = false;
        _windDirectionArrow = null;
        _cutLine = null;
        _meshParticlesInDragArea.Clear();
        _physicsDragParticleOffsets.Clear();
        _stickToolFirstParticleId = null;
    }
}
