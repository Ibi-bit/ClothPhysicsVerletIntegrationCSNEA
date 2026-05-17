using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

/// <summary>
/// The Mesh class stores everything that should be loadable in and savebal for undo/redo functionality to file saving
/// </summary>
class Mesh
{
    /// <summary>
    /// the id for the next particle to be added to the mesh, which is used to ensure that each particle has a unique identifier.
    /// </summary>
    private int _nextParticleId = 1;

    /// <summary>
    /// the id for the next stick to be added to the mesh, which is used to ensure that each stick has a unique identifier.
    /// </summary>
    private int _nextStickId = 1;

    /// <summary>
    /// the thickness to use when drawing the sticks in the mesh. If set to a negative value, it will use the default thickness defined in the DrawableStick class
    /// </summary>
    public float stickDrawThickness = -1;

    /// <summary>
    /// A dictionary that maps particle IDs to their corresponding DrawableParticle instances in the mesh.
    /// </summary>
    public Dictionary<int, DrawableParticle> Particles { get; } = new();

    /// <summary>
    /// the mean force used for colouring the sticks
    /// </summary>
    public float meanForceMagnitude = 0f;

    /// <summary>
    /// the standert deviation of the force used for colouring the sticks
    /// </summary>
    public float forceStdDeviation = 0f;

    /// <summary>
    /// the max force magnitude experienced by any stick in the mesh, used for normalizing stick colours based on force. This value is updated during the physics simulation to reflect the current maximum force magnitude, allowing for dynamic coloring of sticks based on their stress levels.
    /// </summary>
    public float maxForceMagnitude = 0f;

    /// <summary>
    /// the spring constant used for the sticks in the physics simulation, which determines how stiff the sticks are
    /// </summary>
    public float springConstant = 10000f;

    /// <summary>
    /// the amount constant to multiply the velocity of the particles to simulate drag and icnrease stabilty
    /// </summary>
    public float drag = 0.997f;

    /// <summary>
    /// the mass of all the particles in the mesh
    /// </summary>
    public float mass = 1f;

    /// <summary>
    /// the collision friction coefficient used during collision response
    /// </summary>
    public float collisionFrictionCoefficient = 0.2f;

    /// <summary>
    /// the collision bounce coefficient used during collision response
    /// </summary>
    public float collisionBounceCoefficient = 0.2f;

    /// <summary>
    /// the particle id for the first particle when building a polygon to connect the first and last part together
    /// </summary>
    private int _polygonInitialParticle = -1;

    /// <summary>
    /// the final particle id when building a polygon, used to connect the new particle to the last one created
    /// </summary>
    private int _polygonFinalParticle = -1;

    /// <summary>
    /// all of the ids in the polygon currently being created
    /// </summary>
    private readonly List<int> _polygonVertices = new();

    /// <summary>
    /// if the polygon is currently being built
    /// </summary>
    private bool _isPolygonBuilding = false;

    /// <summary>
    /// all colliders in the mesh
    /// </summary>
    public List<Collider> Colliders = new List<Collider>();

    public class MeshStick : DrawableStick
    {
        /// <summary>
        /// the id of the stick
        /// </summary>
        public int Id;

        /// <summary>
        /// the id of the first particle the stick is connected too
        /// </summary>
        public int P1Id;

        /// <summary>
        /// the id of the second particle the stick is connected too
        /// </summary>
        public int P2Id;

        public MeshStick() { }

        public MeshStick(DrawableParticle p1, DrawableParticle p2, Color color, float width = 2.0f)
            : base(p1, p2, color, width) { }
    }

    /// <summary>
    /// the dictionary store of all sticks and maps stick IDs to their corresponding MeshStick instances in the mesh.
    /// </summary>
    public Dictionary<int, MeshStick> Sticks { get; } = new();

    /// <summary>
    /// a helper dictionary that maps particle IDs to the set of stick IDs that are connected to that particle.
    /// </summary>
    private readonly Dictionary<int, HashSet<int>> _particleToStickIds = new();

    private void MarkTopologyDirty() { }

    public void RestoreStickReferences()
    {
        foreach (var stick in Sticks.Values)
        {
            if (
                Particles.ContainsKey(stick.P1Id)
                && Particles.TryGetValue(stick.P2Id, out var particle)
            )
            {
                stick.P1 = Particles[stick.P1Id];
                stick.P2 = particle;
            }
        }
        _particleToStickIds.Clear();
        foreach (var particle in Particles.Values)
        {
            _particleToStickIds[particle.ID] = new HashSet<int>();
        }
        foreach (var stick in Sticks.Values)
        {
            if (_particleToStickIds.TryGetValue(stick.P1Id, out var id))
                id.Add(stick.Id);
            if (_particleToStickIds.TryGetValue(stick.P2Id, out var id2))
                id2.Add(stick.Id);
        }
    }

    public Mesh DeepCopy()
    {
        var copy = new Mesh
        {
            stickDrawThickness = stickDrawThickness,
            meanForceMagnitude = meanForceMagnitude,
            forceStdDeviation = forceStdDeviation,
            maxForceMagnitude = maxForceMagnitude,
            springConstant = springConstant,
            drag = drag,
            mass = mass,
            collisionFrictionCoefficient = collisionFrictionCoefficient,
            collisionBounceCoefficient = collisionBounceCoefficient,
            _nextParticleId = _nextParticleId,
            _nextStickId = _nextStickId,
            _polygonInitialParticle = _polygonInitialParticle,
            _polygonFinalParticle = _polygonFinalParticle,
            _isPolygonBuilding = _isPolygonBuilding,
        };

        copy._polygonVertices.AddRange(_polygonVertices);

        foreach (var kvp in Particles)
        {
            var p = kvp.Value;
            DrawableParticle cloned;

            if (p is OscillatingParticle op)
            {
                var clonedOp = new OscillatingParticle(
                    op.Position,
                    op.Mass,
                    op.IsPinned,
                    op.Color,
                    op.OscillationAmplitude,
                    op.OscillationFrequency,
                    op.OscillationAngle
                )
                {
                    Size = op.Size,
                    PreviousPosition = op.PreviousPosition,
                    AccumulatedForce = op.AccumulatedForce,
                    ID = op.ID,
                    IsSelected = op.IsSelected,
                    TotalForceMagnitude = op.TotalForceMagnitude,
                };
                cloned = clonedOp;
            }
            else
            {
                cloned = new DrawableParticle(p.Position, p.Mass, p.IsPinned, p.Color)
                {
                    Size = p.Size,
                    PreviousPosition = p.PreviousPosition,
                    AccumulatedForce = p.AccumulatedForce,
                    ID = p.ID,
                    IsSelected = p.IsSelected,
                    TotalForceMagnitude = p.TotalForceMagnitude,
                };
            }
            copy.Particles[kvp.Key] = cloned;
            copy._particleToStickIds[kvp.Key] = new HashSet<int>();
        }
        Colliders ??= new List<Collider>();
        copy.Colliders = new List<Collider>();
        copy.Colliders.AddRange(Colliders);

        foreach (var kvp in Sticks)
        {
            var s = kvp.Value;
            var cloned = new MeshStick(
                copy.Particles[s.P1Id],
                copy.Particles[s.P2Id],
                s.Color,
                s.Width
            )
            {
                Id = s.Id,
                P1Id = s.P1Id,
                P2Id = s.P2Id,
                Length = s.Length,
                IsCut = s.IsCut,
            };
            copy.Sticks[kvp.Key] = cloned;
            if (copy._particleToStickIds.TryGetValue(cloned.P1Id, out var set1))
                set1.Add(cloned.Id);
            if (copy._particleToStickIds.TryGetValue(cloned.P2Id, out var set2))
                set2.Add(cloned.Id);
        }

        return copy;
    }

    protected int RegisterParticle(DrawableParticle particle)
    {
        if (particle == null)
            return -1;
        if (particle.ID > 0 && Particles.ContainsKey(particle.ID))
        {
            return particle.ID;
        }
        int id = _nextParticleId++;
        particle.ID = id;
        Particles[id] = particle;
        _particleToStickIds[id] = new HashSet<int>();
        MarkTopologyDirty();
        return id;
    }

    public int AddParticle(
        Vector2 position,
        float mass,
        bool isPinned,
        Color color,
        Vector2? size = null,
        OscillatingParticle oscillatingProps = null
    )
    {
        var particle = oscillatingProps ?? new DrawableParticle(position, mass, isPinned, color);
        if (size.HasValue)
        {
            particle.Size = size.Value;
        }
        int id = _nextParticleId++;
        particle.ID = id;
        Particles[id] = particle;
        _particleToStickIds[id] = new HashSet<int>();
        MarkTopologyDirty();
        return id;
    }

    public int NextParticle => _nextParticleId;

    public void ResetMesh()
    {
        Particles.Clear();
        Sticks.Clear();
        _nextParticleId = 1;
        _nextStickId = 1;
        _particleToStickIds.Clear();
        ResetPolygonBuilder();
        MarkTopologyDirty();
    }

    /// <summary>
    ///     Handles the logic for building a polygon by allowing the user to click to create vertices and sticks between them. The user can left-click to add vertices, press Enter to close the polygon by connecting the last vertex to the first, press Escape to cancel the polygon building process, or press C to create a collider from the current vertices. The method takes into account the current keyboard and mouse states, as well as whether ImGui is currently using the mouse input, to ensure that the polygon building interactions work smoothly without interfering with other UI interactions. The beforeChange action can be used to perform any necessary setup or state changes before modifying the mesh with new particles and sticks.
    /// </summary>
    /// <param name="keyboardState"></param>
    /// <param name="previousKeyboardState"></param>
    /// <param name="mouseState"></param>
    /// <param name="previousMouseState"></param>
    /// <param name="imguiWantsMouse"></param>
    /// <param name="beforeChange"></param>
    public void BuildPolygon(bool imguiWantsMouse, Action beforeChange = null)
    {
        Vector2 mousePos = Raylib.GetMousePosition();

        if (Raylib.IsMouseButtonPressed(MouseButton.Left) && !imguiWantsMouse)
        {
            beforeChange?.Invoke();
            if (!_isPolygonBuilding)
            {
                _isPolygonBuilding = true;
                _polygonVertices.Clear();

                int newParticleId = AddParticle(mousePos, 0.1f, false, Color.White);
                _polygonVertices.Add(newParticleId);
                _polygonInitialParticle = newParticleId;
                _polygonFinalParticle = newParticleId;
            }
            else
            {
                int newParticleId = AddParticle(mousePos, 0.1f, false, Color.White);

                if (_polygonFinalParticle != -1)
                {
                    AddStickBetween(_polygonFinalParticle, newParticleId);
                }

                _polygonVertices.Add(newParticleId);
                _polygonFinalParticle = newParticleId;
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter))
        {
            if (_isPolygonBuilding && _polygonVertices.Count >= 3)
            {
                beforeChange?.Invoke();
                AddStickBetween(_polygonFinalParticle, _polygonInitialParticle);
                ResetPolygonBuilder();
            }
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.Escape))
        {
            if (_isPolygonBuilding)
            {
                beforeChange?.Invoke();
                ResetPolygonBuilder();
            }
        }
        else if (Raylib.IsKeyPressed(KeyboardKey.C))
        {
            if (_isPolygonBuilding && _polygonVertices.Count >= 2)
            {
                Colliders.Add(
                    new PolygonSeperatedAxisCollider(
                        _polygonVertices.Select(id => Particles[id].Position).ToArray()
                    )
                );
                foreach (var id in _polygonVertices)
                {
                    RemoveParticle(id);
                }
                ResetPolygonBuilder();
            }
        }
    }

    public void ResetPolygonBuilder()
    {
        _isPolygonBuilding = false;
        _polygonVertices.Clear();
        _polygonInitialParticle = -1;
        _polygonFinalParticle = -1;
    }

    public bool RemoveParticle(int particleId)
    {
        if (!Particles.ContainsKey(particleId))
            return false;

        if (_particleToStickIds.TryGetValue(particleId, out var stickIds))
        {
            foreach (var sid in stickIds.ToList())
            {
                RemoveStick(sid);
            }
        }

        _particleToStickIds.Remove(particleId);
        bool removed = Particles.Remove(particleId);
        if (removed)
        {
            MarkTopologyDirty();
        }
        return removed;
    }

    public void CutSticksAlongLine(Vector2 lineStart, Vector2 lineEnd)
    {
        var sticksToCut = new List<int>();

        foreach (var kvp in Sticks)
        {
            var stick = kvp.Value;
            if (LinesIntersect(lineStart, lineEnd, stick.P1.Position, stick.P2.Position))
            {
                sticksToCut.Add(kvp.Key);
            }
        }

        foreach (var stickId in sticksToCut)
        {
            RemoveStick(stickId);
        }
    }

    private bool LinesIntersect(Vector2 p1, Vector2 p2, Vector2 p3, Vector2 p4)
    {
        float ccw(Vector2 A, Vector2 B, Vector2 C)
        {
            return (C.Y - A.Y) * (B.X - A.X) > (B.Y - A.Y) * (C.X - A.X) ? 1 : -1;
        }

        return Math.Sign(ccw(p1, p3, p4)) != Math.Sign(ccw(p2, p3, p4))
            && Math.Sign(ccw(p1, p2, p3)) != Math.Sign(ccw(p1, p2, p4));
    }

    public int? AddStick(
        int p1Id,
        int p2Id,
        Color color,
        float width = 2.0f,
        float naturalLength = -1f
    )
    {
        if (p1Id == p2Id)
            return null;
        if (!Particles.ContainsKey(p1Id) || !Particles.ContainsKey(p2Id))
            return null;

        bool exists = Sticks.Values.Any(s =>
            (s.P1Id == p1Id && s.P2Id == p2Id) || (s.P1Id == p2Id && s.P2Id == p1Id)
        );
        if (exists)
            return null;

        var s = new MeshStick(Particles[p1Id], Particles[p2Id], color, width)
        {
            Id = _nextStickId++,
            P1Id = p1Id,
            P2Id = p2Id,
        };
        if (naturalLength > 0f)
        {
            s.Length = naturalLength;
        }
        Sticks[s.Id] = s;
        if (!_particleToStickIds.ContainsKey(p1Id))
            _particleToStickIds[p1Id] = new HashSet<int>();
        if (!_particleToStickIds.ContainsKey(p2Id))
            _particleToStickIds[p2Id] = new HashSet<int>();
        _particleToStickIds[p1Id].Add(s.Id);
        _particleToStickIds[p2Id].Add(s.Id);
        MarkTopologyDirty();
        return s.Id;
    }

    public void AddSticksAccrossLength(
        Vector2 Start,
        Vector2 End,
        int numberOfSticks,
        float naturalLengthRatio = 1f
    )
    {
        if (numberOfSticks < 1)
            return;

        Vector2 direction = End - Start;
        float segmentLength = direction.Length() / numberOfSticks;
        direction = Vector2.Normalize(direction);

        List<int> particleIds = new List<int>();
        for (int i = 0; i <= numberOfSticks; i++)
        {
            Vector2 position = Start + direction * segmentLength * i;
            int particleId = AddParticleAt(position);
            particleIds.Add(particleId);
        }

        for (int i = 0; i < particleIds.Count - 1; i++)
        {
            AddStickBetween(particleIds[i], particleIds[i + 1], segmentLength / naturalLengthRatio);
        }
    }

    public bool RemoveStick(int stickId)
    {
        if (!Sticks.TryGetValue(stickId, out var s))
            return false;

        if (_particleToStickIds.TryGetValue(s.P1Id, out var set1))
            set1.Remove(stickId);
        if (_particleToStickIds.TryGetValue(s.P2Id, out var set2))
            set2.Remove(stickId);
        bool removed = Sticks.Remove(stickId);
        if (removed)
        {
            MarkTopologyDirty();
        }
        return removed;
    }

    public int RemoveSticksBetween(int p1Id, int p2Id)
    {
        var toRemove = Sticks
            .Values.Where(s =>
                (s.P1Id == p1Id && s.P2Id == p2Id) || (s.P1Id == p2Id && s.P2Id == p1Id)
            )
            .Select(s => s.Id)
            .ToList();
        foreach (var sid in toRemove)
            RemoveStick(sid);
        return toRemove.Count;
    }

    public IEnumerable<MeshStick> GetSticksForParticle(int particleId)
    {
        if (_particleToStickIds.TryGetValue(particleId, out var ids))
        {
            foreach (var sid in ids)
            {
                if (Sticks.TryGetValue(sid, out var s))
                    yield return s;
            }
        }
    }

    public void Draw(bool drawParticles, bool drawConstraints)
    {
        if (drawConstraints)
        {
            foreach (var s in Sticks.Values)
            {
                s.Draw();
            }
        }

        if (drawParticles)
        {
            foreach (var p in Particles.Values)
            {
                p.Draw();
            }
        }
    }

    public int AddParticleAt(Vector2 position, bool isPinned = false)
    {
        return AddParticle(position, isPinned ? 0f : mass, isPinned, Color.White);
    }

    public int? AddStickBetween(int p1Id, int p2Id, float naturalLength = -1f)
    {
        return AddStick(p1Id, p2Id, Color.White, 2, naturalLength);
    }

    public int? FindClosestParticle(Vector2 position, float radius)
    {
        float minDist = radius;
        int? closest = null;
        foreach (var kvp in Particles)
        {
            float dist = Vector2.Distance(kvp.Value.Position, position);
            if (dist < minDist)
            {
                minDist = dist;
                closest = kvp.Key;
            }
        }
        return closest;
    }

    /// <summary>
    ///   Creates a grid mesh of particles and sticks between them based on the specified start and end positions, distance between particles, and an optional existing mesh to build upon. The method calculates the number of particles needed in both width and height directions based on the provided distance, and then iteratively adds particles at the calculated positions. It also creates sticks between adjacent particles to form a grid structure. The method ensures that the number of particles does not exceed a reasonable limit (1000 in either direction) to prevent performance issues. Finally, it returns the modified mesh with the newly added grid structure.
    /// </summary>
    /// <param name="Start"></param>
    /// <param name="End"></param>
    /// <param name="DistanceBetweenParticles"></param>
    /// <param name="mesh"></param>
    /// <returns></returns>
    public static Mesh CreateGridMesh(
        Vector2 Start,
        Vector2 End,
        float DistanceBetweenParticles,
        Mesh mesh,
        bool pinExteriorEdgeParticles = false,
        bool connectDiagonalsBothWays = false
    )
    {
        if (DistanceBetweenParticles <= 0)
            return mesh ?? new Mesh();

        int width = (int)Math.Max(1, Math.Abs(End.X - Start.X) / DistanceBetweenParticles) + 1;
        int height = (int)Math.Max(1, Math.Abs(End.Y - Start.Y) / DistanceBetweenParticles) + 1;
        (Start, End) = (Vector2.Min(Start, End), Vector2.Max(Start, End));
        width = Math.Min(width, 1000);
        height = Math.Min(height, 1000);

        int[,] particleIds = new int[width, height];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                Vector2 position =
                    Start + new Vector2(x * DistanceBetweenParticles, y * DistanceBetweenParticles);
                particleIds[x, y] = mesh.AddParticleAt(position);
            }
        }

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int p1Id = particleIds[x, y];

                if (x < width - 1)
                {
                    int p2Id = particleIds[x + 1, y];
                    mesh.AddStickBetween(p1Id, p2Id);
                }

                if (y < height - 1)
                {
                    int p2Id = particleIds[x, y + 1];
                    mesh.AddStickBetween(p1Id, p2Id);
                }

                if (connectDiagonalsBothWays && x < width - 1 && y < height - 1)
                {
                    int bottomRightId = particleIds[x + 1, y + 1];
                    int rightId = particleIds[x + 1, y];
                    int bottomId = particleIds[x, y + 1];
                    float diagonalStickLength = DistanceBetweenParticles * MathF.Sqrt(2f);
                    mesh.AddStickBetween(p1Id, bottomRightId, diagonalStickLength);
                    mesh.AddStickBetween(rightId, bottomId, diagonalStickLength);
                }
            }
        }

        if (pinExteriorEdgeParticles)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bool isExteriorEdge = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    if (!isExteriorEdge)
                        continue;

                    int particleId = particleIds[x, y];
                    if (mesh.Particles.ContainsKey(particleId))
                    {
                        mesh.Particles[particleId].IsPinned = true;
                        mesh.Particles[particleId].Mass = 0f;
                    }
                }
            }
        }
        return mesh;
    }

    [ConsoleCommand("Mesh.ResetSimulation")]
    public void ResetSimulation(string[] parameters)
    {
        foreach (var p in Particles.Values)
        {
            p.PreviousPosition = p.Position;
            p.AccumulatedForce = Vector2.Zero;
        }
    }

    /// <summary>
    /// Creates a hub-and-spoke tire structure in the mesh based on the specified center position, inner and outer radii, and the number of segments for the rim. The method calculates the positions of particles for both the inner and outer rims of the tire, and then creates sticks to connect these particles in a hub-and-spoke pattern. It ensures that the inner and outer radii are valid and that there are enough segments to form a proper tire structure. The resulting structure consists of two concentric circles of particles connected by sticks, with additional sticks connecting the inner rim to the outer rim to create a strong and flexible tire-like structure in the mesh.
    /// </summary>
    /// <param name="args"></param>
    /// <exception cref="ArgumentException"></exception>
    public void CreateHubSpokeTire(object[] args)
    {
        if (args.Length < 5)
        {
            throw new ArgumentException(
                "CreateHubSpokeTire needs 5 inputs: centerX, centerY, innerRadius, outerRadius, segments."
            );
        }
        Vector2 center = new Vector2(float.Parse((string)args[0]), float.Parse((string)args[1]));
        float innerRadius = float.Parse((string)args[2]);
        float outerRadius = float.Parse((string)args[3]);
        int rimParticleCount = int.Parse((string)args[4]);

        if (innerRadius <= 0f || outerRadius <= 0f)
        {
            throw new ArgumentException("Inner and outer radius must be greater than zero.");
        }

        if (outerRadius <= innerRadius)
        {
            throw new ArgumentException("Outer radius must be greater than inner radius.");
        }

        if (rimParticleCount < 3)
        {
            throw new ArgumentException("Segments must be at least 3.");
        }

        List<int> innerRimIds = new List<int>();
        List<int> outerRimIds = new List<int>();

        for (int i = 0; i < rimParticleCount; i++)
        {
            float angle = 2f * MathF.PI * i / rimParticleCount;
            Vector2 innerPos =
                center
                + new Vector2(MathF.Cos(angle) * innerRadius, MathF.Sin(angle) * innerRadius);
            Vector2 outerPos =
                center
                + new Vector2(MathF.Cos(angle) * outerRadius, MathF.Sin(angle) * outerRadius);

            innerRimIds.Add(AddParticleAt(innerPos));
            outerRimIds.Add(AddParticleAt(outerPos));
        }

        float radialStickLength = outerRadius - innerRadius;
        float outerStickLength = 2f * outerRadius * MathF.Sin(MathF.PI / rimParticleCount);
        float diagonalStickLength = MathF.Sqrt(
            radialStickLength * radialStickLength + outerStickLength * outerStickLength
        );

        for (int i = 0; i < rimParticleCount; i++)
        {
            int next = (i + 1) % rimParticleCount;
            AddStickBetween(innerRimIds[i], outerRimIds[next], diagonalStickLength);
            AddStickBetween(innerRimIds[i], outerRimIds[i], radialStickLength);
            AddStickBetween(outerRimIds[i], innerRimIds[next], diagonalStickLength);
        }
    }

    /// <summary>
    /// Creates a cloth mesh in the specified area defined by the start and end positions, with particles spaced according to the natural length parameter. The method builds a grid mesh using the CreateGridMesh method and then pins the top-left and top-right particles to create a hanging cloth effect. The spring constant, drag, and mass parameters can be customized to adjust the behavior of the cloth simulation. The resulting mesh will have a flexible cloth-like structure that can interact with forces and collisions in the physics simulation.
    /// </summary>
    /// <param name="Start"></param>
    /// <param name="End"></param>
    /// <param name="naturalLength"></param>
    /// <param name="mesh"></param>
    /// <param name="springConstant"></param>
    /// <param name="drag"></param>
    /// <param name="mass"></param>
    /// <returns></returns>
    public static Mesh CreateClothMesh(
        Vector2 Start,
        Vector2 End,
        float naturalLength,
        Mesh mesh = null,
        float springConstant = 10000f,
        float drag = 0.997f,
        float mass = 1f
    )
    {
        mesh = mesh ?? new Mesh();
        mesh.springConstant = springConstant;
        mesh.drag = drag;
        mesh.mass = mass;
        int offsetid = mesh._nextParticleId;

        CreateGridMesh(Start, End, naturalLength, mesh);
        int idWidth = (int)Math.Max(1, Math.Abs(End.X - Start.X) / naturalLength) + 1;
        int idHeight = (int)Math.Max(1, Math.Abs(End.Y - Start.Y) / naturalLength) + 1;
        int topLeftParticleId = offsetid;
        int topRightParticleId = offsetid + (idWidth - 1) * idHeight;

        if (mesh.Particles.ContainsKey(topLeftParticleId))
        {
            mesh.Particles[topLeftParticleId].IsPinned = true;
            mesh.Particles[topLeftParticleId].Mass = 0f;
        }

        if (mesh.Particles.ContainsKey(topRightParticleId))
        {
            mesh.Particles[topRightParticleId].IsPinned = true;
            mesh.Particles[topRightParticleId].Mass = 0f;
        }
        return mesh;
    }

    public void ArgClothMeshFactory(object[] args)
    {
        if (args.Length < 5)
        {
            throw new ArgumentException(
                "CreateClothMesh needs 5 inputs: startX, startY, endX, endY, naturalLength."
            );
        }
        try
        {
            Vector2 Start = new Vector2(float.Parse((string)args[0]), float.Parse((string)args[1]));
            Vector2 End = new Vector2(float.Parse((string)args[2]), float.Parse((string)args[3]));
            float naturalLength = float.Parse((string)args[4]);
            CreateClothMesh(Start, End, naturalLength, this, springConstant, drag, mass);
        }
        catch (Exception ex)
        {
            throw new ArgumentException("Invalid input for CreateClothMesh: " + ex.Message);
        }
    }
}
