using Unity.Entities;

/// <summary>
/// Mesh generation request flag (triggers MeshGenerationSystem)
/// </summary>
public struct MeshGenerationRequest : IComponentData, IEnableableComponent { }

/// <summary>
/// Mesh apply request flag (triggers MeshApplySystem)
/// </summary>
public struct MeshApplyRequest : IComponentData, IEnableableComponent { }
