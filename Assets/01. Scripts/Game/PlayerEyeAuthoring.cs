using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

/// <summary>
/// 플레이어 눈 오브젝트를 ECS로 변환하기 위한 Authoring
/// </summary>
public class PlayerEyeAuthoring : MonoBehaviour
{
    public Material eyeMaterial; // Inspector에서 할당할 수 있는 Material

    public class Baker : Baker<PlayerEyeAuthoring>
    {
        public override void Bake(PlayerEyeAuthoring authoring)
        {
            // 눈 엔티티 생성 - Renderable 플래그로 렌더링 가능하게 설정
            Entity entity = GetEntity(TransformUsageFlags.Dynamic | TransformUsageFlags.Renderable);

            // 눈 마커 컴포넌트 추가 (나중에 식별용)
            AddComponent(entity, new PlayerEyeComponent());

            // 부모 GameObject가 있으면 Parent 컴포넌트 추가 (자식 Baker에서는 가능)
            if (authoring.transform.parent != null)
            {
                var parentEntity = GetEntity(authoring.transform.parent, TransformUsageFlags.Dynamic);
                AddComponent(entity, new Parent { Value = parentEntity });
                Debug.Log($"[PlayerEyeAuthoring] Parent set to: {authoring.transform.parent.name}");
            }

            // MeshRenderer가 있는지 확인하고 Material 처리
            var meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer != null && meshRenderer.sharedMaterial != null)
            {
                Debug.Log($"[PlayerEyeAuthoring] MeshRenderer found with material: {meshRenderer.sharedMaterial.name}");
            }
            else if (authoring.eyeMaterial != null)
            {
                Debug.Log($"[PlayerEyeAuthoring] Using assigned eye material: {authoring.eyeMaterial.name}");
            }
            else
            {
                Debug.LogWarning("[PlayerEyeAuthoring] No material found! Eye may not render correctly.");
            }
        }
    }
}

/// <summary>
/// 플레이어 눈임을 나타내는 마커 컴포넌트
/// </summary>
public struct PlayerEyeComponent : IComponentData
{
}
