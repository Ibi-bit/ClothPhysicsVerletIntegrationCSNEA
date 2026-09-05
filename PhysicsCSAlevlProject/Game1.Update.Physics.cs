using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using System.Threading.Tasks;

namespace PhysicsCSAlevlProject;

public partial class Game1
{
    private const int PhysicsProfileWindowSize = 30;

    private readonly Dictionary<string, long> _physicsProfileTicks = new();
    private readonly Dictionary<string, double> _physicsPhaseAverageMs = new();
    private int _physicsProfileSampleCount;
    private int _solverIterations = 4;

    private void AccumulatePhysicsProfile(string phaseName, Action action)
    {
        long start = Stopwatch.GetTimestamp();
        action();
        long elapsedTicks = Stopwatch.GetTimestamp() - start;

        if (!_physicsProfileTicks.ContainsKey(phaseName))
        {
            _physicsProfileTicks[phaseName] = 0;
        }

        _physicsProfileTicks[phaseName] += elapsedTicks;
    }

    private void EndPhysicsProfileSample()
    {
        _physicsProfileSampleCount++;

        if (_physicsProfileSampleCount < PhysicsProfileWindowSize)
        {
            return;
        }

        double ticksPerMs = Stopwatch.Frequency / 1000.0;

        foreach (var phaseName in _physicsProfileTicks.Keys)
        {
            _physicsPhaseAverageMs[phaseName] =
                (_physicsProfileTicks[phaseName] / ticksPerMs) / PhysicsProfileWindowSize;
        }

        _physicsProfileTicks.Clear();
        _physicsProfileSampleCount = 0;
    }

    private void RunPhysicsUpdate(Vector2 currentMousePos)
    {
        const int maxStepsPerFrame = 10000;
        int stepsThisFrame = 0;

        while (
            (_timeAccumulator >= FixedTimeStep || _stepsToStep > 0)
            && stepsThisFrame < maxStepsPerFrame
        )
        {
            RunPhysicsStep(currentMousePos);

            if (_stepsToStep > 0)
                _stepsToStep--;
            else
                _timeAccumulator -= FixedTimeStep;

            stepsThisFrame++;
        }

        if (stepsThisFrame == maxStepsPerFrame)
        {
            _timeAccumulator = Math.Min(_timeAccumulator, FixedTimeStep);
        }

        UpdateStickColorsDictionary(_activeMesh.Sticks);
    }

    private void RunPhysicsStep(Vector2 currentMousePos)
    {
        int subSteps = Math.Max(1, _subSteps);
        float subDt = FixedTimeStep / subSteps;

        for (int subStepIndex = 0; subStepIndex < subSteps; subStepIndex++)
        {
            AccumulatePhysicsProfile("prepare", () => PreparePhysicsSubstep(currentMousePos, subDt));
            AccumulatePhysicsProfile("integrate", () => IntegrateParticles(subDt));
            // AccumulatePhysicsProfile("collisionA", () => ResolveCollisionPhase());
            AccumulatePhysicsProfile("constraints", () => SolveConstraintPhase(subDt));
            AccumulatePhysicsProfile("collision", () => ResolveCollisionPhase());
        }

        EndPhysicsProfileSample();
    }

    private void PreparePhysicsSubstep(Vector2 currentMousePos, float subDt)
    {
        ResetParticleForces();

        // if (!_useConstraintSolver)
        // {
        //     ApplyStickForcesDictionary(_activeMesh.Sticks, 1f);
        // }

        SetCursorColliderCenter(currentMousePos);
        ApplyToolDrivenMotion(currentMousePos, subDt);
        ApplyPhysicsDragForces(currentMousePos, subDt);
    }

    private void ResetParticleForces()
    {

        Parallel.ForEach(_activeMesh.Particles.Values, particle =>
        {
            particle.AccumulatedForce = Vector2.Zero;
        });
    }

    private void ApplyToolDrivenMotion(Vector2 currentMousePos, float subDt)
    {
        Vector2 mouseDelta = currentMousePos - _previousMousePos;
        int subSteps = Math.Max(1, _subSteps);
        int totalIterations = Math.Max(1, subSteps);
        Vector2 deltaPerIteration = mouseDelta / totalIterations;

        if (_leftPressed && _selectedToolName == "Drag" && _currentMode == MeshMode.Interact)
        {
            foreach (int particleId in _meshParticlesInDragArea)
            {
                if (
                    _activeMesh.Particles.TryGetValue(particleId, out var particle)
                    && !particle.IsPinned
                )
                {
                    particle.PreviousPosition = particle.Position;
                    particle.Position += deltaPerIteration;
                }
            }
        }
    }

    private void ResolveCollisionPhase()
    {
        ResolveCollisions();
    }

    private void SolveConstraintPhase(float subDt)
    {
        UpdateStickComplianceFromSpringConstant();
        SolveStickConstraintsXPBD(_activeMesh.Sticks, _solverIterations, subDt);
    }

    private void ApplyPhysicsDragForces(Vector2 targetPosition, float deltaTime)
    {
        if (!_currentToolSet.ContainsKey("PhysicsDrag"))
        {
            return;
        }

        var props = _currentToolSet["PhysicsDrag"].Properties;
        float strength = props.ContainsKey("Strength") ? (float)props["Strength"] : 3500f;
        float damping = props.ContainsKey("Damping") ? (float)props["Damping"] : 90f;
        float maxForce = props.ContainsKey("MaxForce") ? (float)props["MaxForce"] : 30000f;

        if (deltaTime <= 0f)
        {
            return;
        }

        foreach (int particleId in _meshParticlesInDragArea)
        {
            if (
                !_activeMesh.Particles.TryGetValue(particleId, out var particle)
                || particle.IsPinned
            )
            {
                continue;
            }

            Vector2 target = targetPosition;
            if (_physicsDragParticleOffsets.TryGetValue(particleId, out var particleOffset))
            {
                target += particleOffset;
            }

            Vector2 displacement = target - particle.Position;
            Vector2 velocity = (particle.Position - particle.PreviousPosition) / deltaTime;
            Vector2 force = (strength * displacement) - (damping * velocity);

            float forceLengthSquared = force.LengthSquared();
            if (forceLengthSquared > maxForce * maxForce)
            {
                force = Vector2.Normalize(force) * maxForce;
            }

            particle.AccumulatedForce += force;
        }
    }

    private void ApplyPostPhysicsToolEffects(Vector2 currentMousePos)
    {
        if (_selectedToolName == "Drag")
        {
            foreach (int particleId in _meshParticlesInDragArea)
            {
                if (_activeMesh.Particles.TryGetValue(particleId, out var particle))
                {
                    particle.Color = _leftPressed ? Color.Yellow : Color.White;
                }
            }

            if (_paused)
            {
                DragMeshParticles(_leftPressed, _meshParticlesInDragArea);
            }
        }
        else if (_selectedToolName == "Move Collider" && _draggedCollider != null && _leftPressed)
        {
            Vector2 delta = currentMousePos - _previousMousePos;
            if (delta.LengthSquared() > 0)
            {
                _draggedCollider.Position += delta;
            }
        }
        // else if (_selectedToolName == "Move Collider" && mouseState.RightButton == ButtonState.Pressed && !_prevMouseState.RightButton.HasFlag(ButtonState.Pressed))
        // {
        //     ...
        // }
    }
}
