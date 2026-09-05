using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;
using System.Threading.Tasks;

namespace PhysicsCSAlevlProject;

public partial class Game1
{
    /// <summary>
    /// the fixed time step used for the physics simulation, set to 1/60th of a second for a target of 60 updates per second. The physics simulation will accumulate elapsed time and perform updates in fixed increments of this time step to ensure consistent and stable physics behavior regardless of frame rate variations. This approach allows for more accurate and deterministic physics simulations, as the same sequence of updates will occur given the same initial conditions, even if the rendering frame rate fluctuates. The use of a fixed time step is a common practice in game development to maintain stable and predictable physics interactions.
    /// </summary>
    private const float FixedTimeStep = 1f / 60f;

    /// <summary>
    /// how much to change the bounds compared to the window
    /// </summary>
    private Vector2 _collisonBoundsDifference;

    /// <summary>
    /// the base force applied every iteration usually just gravity
    /// </summary>
    private Vector2 _baseForce;

    /// <summary>
    /// the amount of time a frame is taking to calculate the physics for, used to determine how many physics updates to perform in a frame
    /// </summary>
    private float _timeAccumulator;

    /// <summary>
    /// a toggle for if the constraint solver should be used to correct stick lengths after the particles have been updated, this can help with stability but can cause jitter and other unwanted behaviour if not used carefully
    /// </summary>
    private bool _useConstraintSolver;

    /// <summary>
    /// how many substeps to perform in the constraint solver, more substeps can increase stability but also increase the chance of jitter and other unwanted behaviour if not used carefully
    /// </summary>
    private int _subSteps;

    /// <summary>
    /// amount of steps immediately due to  a user input in the menu bar
    /// </summary>
    private int _stepsToStep;

    private void InitializePhysics()
    {
        _collisonBoundsDifference = new Vector2(0, -10);
        _baseForce = new Vector2(0, 980f);
        _timeAccumulator = 0f;
        _useConstraintSolver = false;
        _activeMesh.Colliders = new();
        _stepsToStep = 0;

        _subSteps = 10;
    }

    /// <summary>
    /// Applies spring forces to the particles based on the sticks connecting them. The force applied is proportional to the stretch of the stick from its rest length, multiplied by the spring constant. The forces are accumulated on each particle for later use in the integration step.
    /// </summary>
    /// <param name="sticks"></param>
    /// <param name="timeRatio"></param>
    private void ApplyStickForcesDictionary(
        Dictionary<int, Mesh.MeshStick> sticks,
        float timeRatio = 1f
    )
    {
        float scaledK = _activeMesh.springConstant * 1;

        foreach (var stick in sticks.Values)
        {
            if (stick.Length <= 0f)
                continue;
            Vector2 v = stick.P1.Position - stick.P2.Position;
            float L = v.Length();
            if (L <= 0f)
                continue;
            Vector2 dir = v / L;
            float stretch = L - stick.Length;
            Vector2 springForce = dir * stretch * scaledK;
            stick.P1.AccumulatedForce -= springForce;
            stick.P2.AccumulatedForce += springForce;
        }
    }

    private void UpdateStickComplianceFromSpringConstant()
    {
        float stiffness = Math.Max(1e-4f, _activeMesh.springConstant);
        float compliance = 1f / stiffness;



        Parallel.ForEach(_activeMesh.Sticks.Values, stick => 
        {
            stick.Compliance = compliance;
        });
    }

    private void SolveStickConstraintsXPBD(
        Dictionary<int, Mesh.MeshStick> sticks,
        int iterations,
        float dt
    )
    {
        foreach (var stick in sticks.Values)
            stick.Lambda = 0f;

        float dt2 = dt * dt;

        for (int i = 0; i < iterations; i++)
        {
            foreach (var stick in sticks.Values)
            {
                if (stick.Length <= 0f)
                    continue;

                Vector2 delta = stick.P1.Position - stick.P2.Position;
                float dist = delta.Length();
                if (dist <= 1e-6f)
                    continue;

                float w1 = stick.P1.IsPinned ? 0f : 1f / _activeMesh.mass;
                float w2 = stick.P2.IsPinned ? 0f : 1f / _activeMesh.mass;
                float wSum = w1 + w2;
                if (wSum <= 0f)
                    continue;

                float C = dist - stick.Length;
                float compliance = stick.Compliance / dt2;

                float deltaLambda = (-C - compliance * stick.Lambda) / (wSum + compliance);
                stick.Lambda += deltaLambda;

                Vector2 correction = (delta / dist) * deltaLambda;
                stick.P1.Position += correction * w1;
                stick.P2.Position -= correction * w2;
            }
        }
    }

    /// <summary>
    /// Updates the colors of the sticks based on their stretch relative to their rest length.
    /// </summary>
    /// <param name="sticks"></param>
    private void UpdateStickColorsDictionary(Dictionary<int, Mesh.MeshStick> sticks)
    {
        int count = 0;
        float sum = 0f;
        float sumSq = 0f;

        Parallel.ForEach(sticks.Values, s =>
        {
            if (s.Length <= 0f)
                return;
            Vector2 v = s.P1.Position - s.P2.Position;
            float L = v.Length();
            if (L <= 0f)
                return;
            float e = (L - s.Length) / s.Length;
            sum += e;
            sumSq += e * e;
            count++;
        });

        float mean = count > 0 ? sum / count : 0f;
        float variance = count > 0 ? (sumSq / count) - mean * mean : 0f;
        if (variance < 0f)
            variance = 0f;
        float std = (float)Math.Sqrt(variance);

        Parallel.ForEach(sticks.Values, s =>
        {
            if (s.Length <= 0f)
                return;
            Vector2 v = s.P1.Position - s.P2.Position;
            float L = v.Length();
            if (L <= 0f)
                return;
            float e = (L - s.Length) / s.Length;

            float intensity;
            if (count > 0 && std > 1e-5f)
            {
                float z = (e - mean) / std;
                intensity = Math.Clamp((z - 0.5f) / 1.5f, 0f, 1f);
            }
            else
            {
                intensity = Math.Clamp((L / s.Length - 1f) / 0.5f, 0f, 1f);
            }
            float eased = intensity * intensity;
            s.Color = Color.Lerp(Color.White, Color.Red, eased);
        });
    }

    /// <summary>
    /// Updates the positions of the particles based on the accumulated forces, applying simple Verlet integration. It also handles collisions with colliders and the boundaries of the window, applying appropriate responses based on the bounce and friction coefficients. Additionally, it calculates statistics about the forces applied to the particles for potential use in visualization or debugging.
    /// </summary>
    /// <param name="deltaTime"></param>
    private void IntegrateParticles(float deltaTime)
    {
        float dragPerStep = MathF.Pow(_activeMesh.drag, 1f);

        Parallel.ForEach(_activeMesh.Particles.Values, particle =>
        {
            if (particle is OscillatingParticle op)
            {
                op.UpdateOscillation(deltaTime);
                particle.AccumulatedForce = Vector2.Zero;
                return;
            }

            if (particle.IsPinned)
            {
                particle.AccumulatedForce = Vector2.Zero;
                return;
            }

            bool isBeingDragged =
                _leftPressed
                && _selectedToolName == "Drag"
                && _meshParticlesInDragArea.Contains(particle.ID);

            if (!isBeingDragged)
            {
                Vector2 acceleration = _baseForce;
                if (_activeMesh.mass > 0f)
                    acceleration += (particle.AccumulatedForce + _windForce) / _activeMesh.mass;

                Vector2 velocity = (particle.Position - particle.PreviousPosition) * dragPerStep;
                Vector2 oldPosition = particle.Position;

                particle.Position =
                    particle.Position + velocity + acceleration * (deltaTime * deltaTime);
                particle.Position = new Vector2(
                    float.IsNaN(particle.Position.X) ? oldPosition.X : particle.Position.X,
                    float.IsNaN(particle.Position.Y) ? oldPosition.Y : particle.Position.Y
                );
                particle.PreviousPosition = oldPosition;
            }

            Vector2 beforeCorrection = particle.Position;
            bool collisionOccurred = false;
            Vector2 collisionNormal = Vector2.Zero;
            IEnumerable<Collider> collidersToCheck = _activeMesh.Colliders;
            if (_selectedToolName == "Cursor Collider" && _cursorCollider != null)
            {
                collidersToCheck = _activeMesh.Colliders.Concat(new[] { _cursorCollider });
            }

            foreach (var collider in collidersToCheck)
            {
                Vector2 clampedPosition;
                if (collider.ContainsPoint(particle.Position, out clampedPosition))
                {
                    collisionOccurred = true;
                    Vector2 originalPosition = particle.Position;
                    particle.Position = clampedPosition;
                    if (collider is SeperatedAxisRectangleCollider sarc)
                    {
                        Vector2 localPoint = particle.Position - sarc.Position;
                        float xProjection = Vector2.Dot(localPoint, sarc.Axis[0]);
                        float yProjection = Vector2.Dot(localPoint, sarc.Axis[1]);
                        if (
                            Math.Abs(sarc.HalfWidth - Math.Abs(xProjection))
                            < Math.Abs(sarc.HalfHeight - Math.Abs(yProjection))
                        )
                            collisionNormal = sarc.Axis[0] * Math.Sign(xProjection);
                        else
                            collisionNormal = sarc.Axis[1] * Math.Sign(yProjection);
                    }
                    else if (collider is CircleCollider cc)
                    {
                        collisionNormal = Vector2.Normalize(particle.Position - cc.Position);
                    }
                    else
                    {
                        Vector2 separation = clampedPosition - originalPosition;
                        if (separation.LengthSquared() > 1e-8f)
                        {
                            collisionNormal = Vector2.Normalize(separation);
                        }
                        else
                        {
                            collisionNormal = Vector2.UnitY;
                        }
                    }
                }
            }

            float xSeparation = 0f;
            if (particle.Position.X < 0)
            {
                particle.Position.X = 0;
                xSeparation = particle.Position.X - beforeCorrection.X;
                collisionOccurred = true;
            }
            else if (particle.Position.X > _windowBounds.Width)
            {
                particle.Position.X = _windowBounds.Width;
                xSeparation = particle.Position.X - beforeCorrection.X;
                collisionOccurred = true;
            }

            float ySeparation = 0f;
            if (particle.Position.Y < 0)
            {
                particle.Position.Y = 0;
                ySeparation = particle.Position.Y - beforeCorrection.Y;
                collisionOccurred = true;
            }
            else if (particle.Position.Y > _windowBounds.Height - 10)
            {
                particle.Position.Y = _windowBounds.Height - 10;
                ySeparation = particle.Position.Y - beforeCorrection.Y;
                collisionOccurred = true;
            }

            if (collisionOccurred && collisionNormal.Equals(Vector2.Zero))
            {
                if (Math.Abs(xSeparation) > Math.Abs(ySeparation))
                    collisionNormal = Math.Sign(xSeparation) * Vector2.UnitX;
                else if (Math.Abs(ySeparation) > 0f)
                    collisionNormal = Math.Sign(ySeparation) * Vector2.UnitY;
            }

            if (collisionOccurred)
            {
                Vector2 velocity = particle.Position - particle.PreviousPosition;
                float normalVel = Vector2.Dot(velocity, collisionNormal);
                Vector2 normalComponent = collisionNormal * normalVel;
                Vector2 tangentComponent = velocity - normalComponent;
                normalComponent *= -_activeMesh.collisionBounceCoefficient;
                tangentComponent *= 1f - _activeMesh.collisionFrictionCoefficient;
                Vector2 newVelocity = normalComponent + tangentComponent;
                particle.PreviousPosition = particle.Position - newVelocity;
            }
            else if (particle.Position != beforeCorrection)
            {
                particle.PreviousPosition = beforeCorrection;
            }
        });
    }

    private void ResolveCollisions()
    {
        float forceMagnitudeSum = 0f,
            forceMagnitudeSquaredSum = 0f,
            maxForceMagnitude = 0f;
        int totalForceCount = 0;

        foreach (var particle in _activeMesh.Particles.Values)
        {
            Vector2 totalForce = _baseForce + particle.AccumulatedForce + _windForce;
            float mag = totalForce.Length();
            forceMagnitudeSum += mag;
            forceMagnitudeSquaredSum += mag * mag;
            totalForceCount++;
            if (mag > maxForceMagnitude)
                maxForceMagnitude = mag;
        }

        if (totalForceCount > 0)
        {
            _activeMesh.meanForceMagnitude = forceMagnitudeSum / totalForceCount;
            float meanSquare = _activeMesh.meanForceMagnitude * _activeMesh.meanForceMagnitude;
            float variance = (forceMagnitudeSquaredSum / totalForceCount) - meanSquare;
            if (variance < 0f)
            {
                variance = 0f;
            }

            _activeMesh.forceStdDeviation = (float)Math.Sqrt(variance);
            _activeMesh.maxForceMagnitude = maxForceMagnitude;
        }
        else
        {
            _activeMesh.meanForceMagnitude = 0f;
            _activeMesh.forceStdDeviation = 0f;
            _activeMesh.maxForceMagnitude = 0f;
        }
    }
}
