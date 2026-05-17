using System;
using System.Collections.Generic;
using System.Numerics;
using ImGuiNET;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Raylib_cs;
using rlImGui_cs;

namespace PhysicsCSAlevlProject;

/// <summary>
/// The FileWriteableMesh class serves as a serializable representation of the Mesh class, allowing for easy saving and loading of mesh data to and from JSON format. It contains nested classes for particle and stick data, as well as collider information, and provides methods to convert between the FileWriteableMesh and the original Mesh class. This design enables the application to persist mesh configurations, including particle properties, stick connections, and colliders, while also supporting oscillating particles with their specific parameters. The ToMesh method reconstructs a Mesh instance from the stored data, ensuring that all relevant properties are correctly transferred for accurate physics simulations when loaded back into the application.
/// </summary>
class FileWriteableMesh
{
    /// <summary>
    /// stores the data for a oscilating particle in a way that can be easily serialized to json,
    /// no anchor position as that is stored as the main position of the particle, but includes the amplitude, frequency and angle of the oscilation
    /// </summary>
    public class OscillationData
    {
        public float Amplitude;
        public float Frequency;
        public float Angle;
    }

    /// <summary>
    /// stores the data for a particle in a way that can be easily serialized to json
    /// </summary>
    public class particleData
    {
        public Vector2 Position;
        public float Mass;
        public bool IsPinned;
        public string ParticleKind;
        public OscillationData Oscillation;
    }

    /// <summary>
    /// stores the data for a stick in a way that can be easily serialized to json
    /// </summary>
    public class stickData
    {
        public int P1Id;
        public int P2Id;
        public float NaturalLength;
    }

    public List<particleData> Particles = new();
    public List<stickData> Sticks = new();
    public List<Collider> Colliders = new();

    public float SpringConstant = 10000f;
    public float Drag = 0.997f;
    public float Mass = 1f;
    public float CollisionFrictionCoefficient = 0.2f;
    public float CollisionBounceCoefficient = 0.2f;

    public FileWriteableMesh() { }

    public FileWriteableMesh(Mesh mesh)
    {
        SpringConstant = mesh.springConstant;
        Drag = mesh.drag;
        Mass = mesh.mass;
        CollisionFrictionCoefficient = mesh.collisionFrictionCoefficient;
        CollisionBounceCoefficient = mesh.collisionBounceCoefficient;

        var particleIdMap = new Dictionary<int, int>();
        foreach (var kvp in mesh.Particles)
        {
            var p = kvp.Value;
            particleIdMap[kvp.Key] = Particles.Count;
            if (p is OscillatingParticle op)
            {
                Particles.Add(
                    new particleData
                    {
                        Position = op.anchorPosition,
                        Mass = p.Mass,
                        IsPinned = p.IsPinned,
                        ParticleKind = "Oscillating",
                        Oscillation = new OscillationData
                        {
                            Amplitude = op.OscillationAmplitude,
                            Frequency = op.OscillationFrequency,
                            Angle = op.OscillationAngle,
                        },
                    }
                );
                continue;
            }

            Particles.Add(
                new particleData
                {
                    Position = p.Position,
                    Mass = p.Mass,
                    IsPinned = p.IsPinned,
                    ParticleKind = "Default",
                }
            );
        }

        foreach (var kvp in mesh.Sticks)
        {
            var s = kvp.Value;
            Sticks.Add(
                new stickData
                {
                    P1Id = particleIdMap[s.P1Id],
                    P2Id = particleIdMap[s.P2Id],
                    NaturalLength = s.Length,
                }
            );
        }

        foreach (var c in mesh.Colliders)
        {
            Colliders.Add(c.DeepCopy());
        }
    }

    public Mesh ToMesh()
    {
        var mesh = new Mesh
        {
            springConstant = SpringConstant,
            drag = Drag,
            mass = Mass,
            collisionFrictionCoefficient = CollisionFrictionCoefficient,
            collisionBounceCoefficient = CollisionBounceCoefficient,
        };

        var indexToParticleId = new Dictionary<int, int>();

        if (Particles != null)
        {
            for (int i = 0; i < Particles.Count; i++)
            {
                var pData = Particles[i];

                bool hasExplicitOscillation =
                    pData.Oscillation != null
                    && string.Equals(
                        pData.ParticleKind,
                        "Oscillating",
                        StringComparison.OrdinalIgnoreCase
                    );

                if (hasExplicitOscillation)
                {
                    int particleId = mesh.AddParticle(
                        pData.Position,
                        pData.Mass,
                        pData.IsPinned,
                        Color.White,
                        null,
                        new OscillatingParticle(
                            pData.Position,
                            pData.Mass,
                            pData.IsPinned,
                            Color.White,
                            pData.Oscillation.Amplitude,
                            pData.Oscillation.Frequency,
                            pData.Oscillation.Angle
                        )
                    );
                    indexToParticleId[i] = particleId;
                    continue;
                }

                int particleIndex = mesh.AddParticle(pData.Position, pData.Mass, pData.IsPinned, Color.White);
                indexToParticleId[i] = particleIndex;
            }
        }

        if (Sticks != null)
        {
            foreach (var sData in Sticks)
            {
                if (
                    indexToParticleId.TryGetValue(sData.P1Id, out int p1Id)
                    && indexToParticleId.TryGetValue(sData.P2Id, out int p2Id)
                )
                {
                    mesh.AddStick(p1Id, p2Id, Color.White, 2, sData.NaturalLength);
                }
            }
        }

        if (Colliders != null)
        {
            foreach (var c in Colliders)
            {
                mesh.Colliders.Add(c.DeepCopy());
            }
        }

        return mesh;
    }
}
