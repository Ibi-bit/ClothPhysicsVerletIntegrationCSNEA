using ImGuiNET;
using Raylib_cs;
using rlImGui_cs;
using VectorGraphics;

namespace PhysicsCSAlevlProject;

/// <summary>
/// Particle inspection and inspection window functionality.
/// </summary>
public partial class Game1
{
    /// <summary>
    /// Draws the particle inspection window, allowing users to view and manage particles that they have selected for inspection. The window displays a list of inspected particles with checkboxes to toggle their detailed information display. Users can clear all inspected particles, delete them along with their connected sticks, or close the window. For each inspected particle, detailed information such as position, previous position, and pinned status is shown, along with options to pin/unpin the particle or remove it from inspection. This feature provides an in-depth look at specific particles in the mesh for debugging and analysis purposes.
    /// </summary>
    private void DrawInspectParticleWindow()
    {
        if (!ImGui.Begin("Inspect Particle"))
        {
            ImGui.End();
            return;
        }
        ImGui.Text("Particle Info:");
        if (ImGui.Button("Clear All Inspected Particles"))
        {
            _inspectedParticles.Clear();
        }
        if (ImGui.Button("Delete All Inspected Particles With Sticks"))
        {
            foreach (var index in _inspectedParticles)
            {
                _activeMesh.RemoveParticle(index);
            }
            _inspectedParticles.Clear();
            ImGui.End();
            return;
        }
        if (ImGui.Button("Close Window"))
        {
            ImGui.End();
            return;
        }
        ImGui.Text("Press a particle index to inspect");
        float windowWidth = ImGui.GetContentRegionAvail().X;

        float cursorX = 0f;
        ImGui.BeginChild("ParticleIndexList", new System.Numerics.Vector2(0, 80));
        foreach (int pID in _inspectedParticles)
        {
            bool isOpen = _openedInspectedParticles.Contains(pID);
            float itemWidth = ImGui.CalcTextSize(pID.ToString()).X + 30f;

            if (cursorX + itemWidth > windowWidth && cursorX > 0f)
            {
                cursorX = 0f;
            }
            else if (cursorX > 0f)
            {
                ImGui.SameLine();
            }

            if (ImGui.Checkbox(pID.ToString(), ref isOpen))
            {
                if (isOpen)
                {
                    _activeMesh.Particles[pID].Color = Color.Red;
                    _openedInspectedParticles.Add(pID);
                }
                else
                {
                    _activeMesh.Particles[pID].Color = Color.White;

                    _openedInspectedParticles.Remove(pID);
                }
            }
            cursorX += itemWidth + ImGui.GetStyle().ItemSpacing.X;
        }
        ImGui.EndChild();

        ImGui.BeginChild("ParticleInfoScrollArea", new System.Numerics.Vector2(0, -100));
        foreach (var index in _openedInspectedParticles)
        {
            if (!_activeMesh.Particles.TryGetValue(index, out var p))
            {
                continue;
            }

            ImGui.Text($"Particle Index: {index}");
            ImGui.Text($"Position: {p.Position}");
            ImGui.Text($"Previous Position: {p.PreviousPosition}");
            ImGui.Text($"Is Fixed: {p.IsPinned}");
            if (ImGui.Button(p.IsPinned ? "Unpin Particle" : "Pin Particle"))
            {
                p.IsPinned = !p.IsPinned;
            }

            if (ImGui.Button("Remove Particle From Inspect"))
            {
                _inspectedParticles.Remove(index);
                break;
            }
            ImGui.Separator();
        }
        ImGui.EndChild();
        ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.8f, 0.2f, 0.2f, 1f));
        if (ImGui.Button("Close and Clear All"))
        {
            _inspectedParticles.Clear();
        }
        ImGui.PopStyleColor();

        ImGui.End();
    }
}
