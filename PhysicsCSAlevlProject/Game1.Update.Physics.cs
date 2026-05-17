using System;
using System.Numerics;
using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;

namespace PhysicsCSAlevlProject;

public partial class Game1
{
    private void RunPhysicsUpdate(Vector2 currentMousePos)
    {
        int stepsThisFrame = 0;
        const int maxStepsPerFrame = 10000;
        int subSteps = Math.Max(1, _subSteps);
        Vector2 mouseDelta = currentMousePos - _previousMousePos;
            int plannedStepsThisFrame = _paused
                ? _stepsToStep
                : Math.Min((int)(_timeAccumulator / FixedTimeStep), maxStepsPerFrame);
            int totalIterations = Math.Max(1, plannedStepsThisFrame * subSteps);
            Vector2 deltaPerIteration = mouseDelta / totalIterations;

        while (
            (_timeAccumulator >= FixedTimeStep || _stepsToStep > 0)
            && stepsThisFrame < maxStepsPerFrame
        )
        {
            float subDt = FixedTimeStep / subSteps;

            for (int subStepIndex = 0; subStepIndex < subSteps; subStepIndex++)
            {
                foreach (var particle in _activeMesh.Particles.Values)
                {
                    particle.AccumulatedForce = Vector2.Zero;
                }
                if (!_useConstraintSolver)
                {
                    ApplyStickForcesDictionary(_activeMesh.Sticks, 1f);
                }
                float iterationLerpFactor = 1f / (stepsThisFrame + 1f);

                Vector2 cursorCenter = GetCursorColliderCenter();
                cursorCenter = Vector2.Lerp(cursorCenter, currentMousePos, iterationLerpFactor);
                SetCursorColliderCenter(cursorCenter);

                if (
                    _leftPressed
                    && _selectedToolName == "Drag"
                    && _currentMode == MeshMode.Interact
                )
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

                if (
                    _leftPressed
                    && _selectedToolName == "PhysicsDrag"
                    && _currentMode == MeshMode.Interact
                )
                {
                    ApplyPhysicsDragForces(currentMousePos, subDt);
                }

                UpdateParticles(subDt);
            }

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
