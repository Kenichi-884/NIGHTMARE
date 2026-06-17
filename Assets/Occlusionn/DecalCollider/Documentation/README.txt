🌟 Decal Collider - README

Decal Collider projects visual decal content onto surfaces and builds matching collider output for gameplay interactions. 🛡️
It supports grid and mesh projection workflows with runtime-friendly controls for dynamic scenes.

==================================================
🎯 What Does It Do?
==================================================

- Projects decals using grid-based or mesh-based workflows.
- Generates MeshCollider output aligned with projected visuals.
- Supports alpha-based collider hole handling.
- Supports live updates for Sprite and TextMeshPro sources.
- Includes distance-based LOD controls for runtime stability.
- Exposes runtime APIs for dynamic usage.

==================================================
⚡ Quick Start
==================================================

1. Add component:
   - `Add Component > Physics > Decal Collider`
2. Configure projection mode (`Grid` or `Mesh`).
3. Tune projection bounds and density values.
4. Enable live updates if source content is dynamic.
5. Validate triangle/build-time/memory stats.

==================================================
🧩 Main Sections (Detailed)
==================================================

`Projection`
- `Grid Projection`: fast setup for standard decal usage.
- `Mesh Projection`: improved wrapping for sprites, TMP, and complex meshes.

`Live Link`
- Updates output when source sprite/text changes.
- Optional collider rebuild during live updates.

`LOD`
- Reduces mesh/collider density with distance.
- Minimizes runtime overhead in large scenes.

`Diagnostics`
- Tracks build time, triangle count, and memory usage.
- Supports debug rays for projection inspection.

==================================================
🧠 Important Behavior Notes
==================================================

- This component can generate complex collider data; monitor heavy scenes.
- Alpha-masked textures can create automatic collider cutouts.
- TextMeshPro projection requires TMP package.
- Target Unity versions: 2021.3+ (including Unity 6).

==================================================
🛠️ Unity Menu Shortcuts
==================================================

- Component path: `Add Component > Physics > Decal Collider`
- No dedicated top-level Tools menu is required for baseline usage.

==================================================
🧪 Troubleshooting
==================================================

- Projection is not visible:
  - Check projection direction and bounds.
  - Verify target renderer and layer mask.
- Collider is too heavy:
  - Lower density settings.
  - Use stronger LOD reduction.
- Live updates are slow:
  - Increase update interval.
  - Reduce collider updates during live mode.

==================================================
📌 Quick Recommendations
==================================================

- Start with low density and raise only where needed.
- Enable live updates only on objects that need dynamic changes.
- Validate profiler metrics before shipping.

==================================================
🔗 Useful Links
==================================================

- Documentation (PDF): `Assets/Occlusionn/DecalCollider/Documentation/Documentation.pdf`
- Documentation (GitBook): https://occlusionn.gitbook.io/docs
- Community & Support (Discord): https://discord.gg/DQwrhkzac6
