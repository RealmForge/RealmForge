using Unity.Entities;
using Unity.Services.Matchmaker.Models;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public class Baker : Baker<PlayerAuthoring>
    {

        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerComponent());
        }
    }
}

public struct PlayerComponent : IComponentData
{
    
}
