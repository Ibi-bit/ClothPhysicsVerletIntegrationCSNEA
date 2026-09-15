# A-Level NEA: Verlet-Integration Cloth Physics

A cloth physics simulation built in **C# with MonoGame** for my A-Level Computer Science NEA. The technical solution scored **42 out of 42 marks**.

## Why Verlet integration?

Velocity is derived from the *previous* and *current* positions of each particle, which makes it far more suitable than classic Euler integration for a constrained-particle simulation: collisions and restraints are handled simply by changing the current position.

Later iterations also store accumulated force on each particle (spring constants), producing a hybrid of Euler and Verlet integration.

## Tech stack

- **C# · MonoGame**
- Verlet integration and particle spring physics
- Rendered with my own vector-primitives library — published separately as [MonoGame2DVectorGraphics](https://github.com/Ibi-bit/MonoGame2DVectorGraphics)
- GUI/menu system via an open-source ImGui extension for MonoGame
- Doxygen documentation (`Doxyfile`)

## Repository layout (highlights)

- `PhysicsCSAlevlProject/` — the main game/physics project
- `infrastructure/` — build and support tooling
- `LegacyJsonConverter/` — legacy data-conversion tooling
- `*.tsv` / `*.mmd` — AQA specification tables and flow diagrams

## Getting started

The repository uses git submodules, so clone with:

```sh
git clone --recurse-submodules https://github.com/Ibi-bit/ClothPhysicsVerletIntegrationCSNEA.git
```

Open the project in your IDE (with MonoGame support) and run the main project.