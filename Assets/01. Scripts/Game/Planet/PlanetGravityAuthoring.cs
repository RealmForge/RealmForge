using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using UnityEngine;

public struct PlanetComponent : IComponentData
{
    public float3 Center;
    public float Gravity;
}

public class PlanetGravityAuthoring : MonoBehaviour
{
    [Header("Planet Settings")]
    [Tooltip("행성 중력 강도")]
    public float gravity = 20f;

    class Baker : Baker<PlanetGravityAuthoring>
    {
        public override void Bake(PlanetGravityAuthoring authoring)
        {
            // Physics를 위해 TransformUsageFlags에 Dynamic 사용
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new PlanetComponent
            {
                Center = authoring.transform.position,
                Gravity = authoring.gravity
            });

            // 참고: 행성 GameObject에 MeshCollider나 SphereCollider를 추가하면
            // Unity.Physics가 자동으로 PhysicsCollider 컴포넌트로 변환합니다.
        }
    }
}
