using System;
using System.Collections.Generic;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject
{
    public abstract class Game : IDisposable
    {
        protected virtual void Initialize() { }

        protected virtual void LoadContent() { }

        protected virtual void Update(GameTime gameTime) { }

        protected virtual void Draw(GameTime gameTime) { }

        protected virtual void OnExiting(object sender, ExitingEventArgs args) { }

        public void Run()
        {
            Initialize();
            LoadContent();

            Raylib.SetTargetFPS(60);

            var totalTime = TimeSpan.Zero;
            while (!Raylib.WindowShouldClose())
            {
                float frameSeconds = Raylib.GetFrameTime();
                var gameTime = new GameTime
                {
                    ElapsedGameTime = TimeSpan.FromSeconds(frameSeconds),
                    TotalGameTime = totalTime,
                };

                Update(gameTime);
                Draw(gameTime);

                totalTime += TimeSpan.FromSeconds(frameSeconds);
            }

            OnExiting(this, new ExitingEventArgs());
            Dispose();
        }

        public virtual void Dispose()
        {
            if (Raylib.IsWindowReady())
            {
                Raylib.CloseWindow();
            }
        }
    }

    public sealed class GameTime
    {
        public TimeSpan ElapsedGameTime { get; init; }

        public TimeSpan TotalGameTime { get; init; }
    }

    public sealed class ExitingEventArgs : EventArgs { }

    public partial class Game1 : Game
    {
        /// <summary>
        /// The main class that is used to generate and maniplulate primitive shapes like lines, rectangles and circles. This is used to draw the particles, sticks and colliders in the mesh.
        /// </summary>
        private PrimitiveBatch _primitiveBatch;

        /// <summary>
        /// The ImGuiRenderer is used to render the ImGui UI elements in the application. It is initialized in the Initialize method and is responsible for drawing the ImGui interface on top of the game content. This allows for creating interactive UI elements such as buttons, sliders, and windows that can be used for debugging, tool selection, and other user interactions within the application.
        /// </summary>

        /// <summary>
        /// the database class is an interface for the postgress sql
        /// </summary>
        public Game1Database _database;

        /// <summary>
        /// stores the current assignment being worked on  for use in the UI
        /// </summary>
        public string _currentAssignmentTitle;

        /// <summary>
        /// just a bool for if the left mouse is pressed
        /// </summary>
        private bool _leftPressed;

        /// <summary>
        /// just a toggle for if the simulating is paused or not
        /// </summary>
        private bool _paused;

        /// <summary>
        /// the bounds of the physics window stored as a rectangle for easy collision checking and clamping of mouse position
        /// </summary>
        private static Rectangle _windowBounds;

        /// <summary>
        /// The bounds of the window that have been changed, used for updating the window size and position.
        /// </summary>
        private Rectangle _changedBounds;

        /// <summary>
        /// a toggle if the iniital aspect ratio is to be respected
        /// </summary>
        private bool _keepAspectRatio;

        /// <summary>
        /// the aspect ratio of the window used for if _keepAspectRatio is true
        /// </summary>
        private float _lockedAspectRatio;

        /// <summary>
        /// stores the state of the mesh before any changes are made to it for undo functionality, also stores the state of the mesh before any changes are made for redo functionality
        /// </summary>
        private MyStack<Mesh> _meshHistory;

        /// <summary>
        /// stores the state of the mesh before any changes are made for redo functionality, this is cleared whenever a new change is made to the mesh to prevent redoing changes that are no longer relevant. This allows for a linear undo/redo history where the user can only redo actions that were undone, and any new action will clear the redo history to maintain consistency in the state of the mesh.
        /// </summary>
        private MyStack<Mesh> _meshRedoHistory;

        /// <summary>
        /// stores the current mesh that physics is itterated on and the user is interacts with
        /// </summary>
        private Mesh _activeMesh;

        /// <summary>
        /// a fallback for if the _activeMesh is null for any reason, this should never be used but it prevents the application from crashing if something goes wrong with the mesh loading or creation process. This mesh is a simple default mesh with no particles or sticks, and default physics parameters.
        /// </summary>
        private Mesh _defaultMesh;

        /// <summary>
        /// the possible modes
        /// </summary>
        private enum MeshMode
        {
            Interact,
            Edit,
        }

        /// <summary>
        /// stores the current mode the program is in
        /// </summary>
        private MeshMode _currentMode = MeshMode.Interact;

        /// <summary>
        /// Sets the window size to the specified width and height, and updates the internal window bounds accordingly.
        /// </summary>
        public void SetWindowSize(float width, float height)
        {
            if (width > 0 && height > 0)
            {
                Raylib.SetWindowSize((int)width, (int)height);
            }

            _windowBounds = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            _changedBounds = _windowBounds;
        }

        public Game1()
        {
            Raylib.InitWindow(960, 768, "Cloth Physics Simulation");
            Raylib.SetExitKey(KeyboardKey.Null);
            SetWindowSize(960, 768);
        }

        /// <summary>
        /// initialises the core variables
        /// </summary>
        protected override void Initialize()
        {
            _primitiveBatch = new PrimitiveBatch(20f);
            _primitiveBatch.CreateTextures(20f);

            _meshHistory = new MyStack<Mesh>();
            _meshRedoHistory = new MyStack<Mesh>();

            _currentAssignmentTitle = "";

            var cbInit = new Rectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight());
            _windowBounds = new Rectangle(0, 0, cbInit.Width, cbInit.Height);
            _changedBounds = _windowBounds;
            _keepAspectRatio = true;
            _lockedAspectRatio = cbInit.Height > 0 ? cbInit.Width / (float)cbInit.Height : 1f;

            _leftPressed = false;
            _paused = false;

            _defaultMesh = new Mesh
            {
                springConstant = 5000f,
                mass = 0.1f,
                drag = 0.997f,
            };
            _activeMesh = _defaultMesh;
            _currentMode = MeshMode.Interact;

            InitializeImGui();
            InitializePhysics();
            InitializeRender();
            InitializeTools();
            InitializeUpdate();

        }

        /// <summary>
        /// loads textures and fonts
        /// </summary>
        protected override void LoadContent()
        {
        }

        /// <summary>
        /// Handles the application exit event by performing necessary cleanup and logging before the application closes.
        /// </summary>
        protected override void OnExiting(object sender, ExitingEventArgs args)
        {
            OnExit();
        }

        /// <summary>
        /// cleans up and logs before exiting
        /// </summary>
        private void OnExit()
        {
            _logger?.AddLog("Application closing...", ImGuiLogger.LogTypes.Info);

            _logger.SaveLogsToFile("logs.log");
            rlImGui.Shutdown();
        }
    }
}
