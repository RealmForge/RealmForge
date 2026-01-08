using Unity.Entities;
using UnityEngine;

public class EntitiesReferenceAuthoring : MonoBehaviour
{
    public GameObject playerPrefab;

    public class Baker : Baker<EntitiesReferenceAuthoring>
    {

        public override void Bake(EntitiesReferenceAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new EntitiesReference
            {
                playerPrefabEntity = GetEntity(authoring.playerPrefab, TransformUsageFlags.Dynamic),
            });
        }
    }
}

public struct EntitiesReference : IComponentData
{
    public Entity playerPrefabEntity;
}
