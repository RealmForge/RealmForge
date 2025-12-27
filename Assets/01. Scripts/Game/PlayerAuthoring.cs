using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Services.Matchmaker.Models;
using UnityEngine;
using RealmForge.Game.UI;

public class PlayerAuthoring : MonoBehaviour
{
    [Header("Player Settings")]
    [Tooltip("플레이어 높이 (캡슐 높이)")]
    public float height = 2f;

    [Header("Movement Settings")]
    [Tooltip("이동 속도")]
    public float moveSpeed = 10f;

    [Tooltip("점프 힘")]
    public float jumpForce = 8f;

    public class Baker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            // 플레이어 컴포넌트
            AddComponent(entity, new PlayerComponent
            {
                Height = authoring.height,
                MoveSpeed = authoring.moveSpeed,
                JumpForce = authoring.jumpForce
            });
            AddComponent(entity, new PlayerVelocity { Value = float3.zero });
            AddComponent(entity, new PlayerGroundState
            {
                IsGrounded = false,
                GroundNormal = new float3(0, 1, 0)
            });

            // Physics 컴포넌트 (수동 추가)
            // PhysicsVelocity 추가 - 물리 엔진이 이걸 사용해서 충돌 처리
            AddComponent(entity, new PhysicsVelocity());

            // PhysicsMass 추가 - 회전은 수동으로 제어하기 위해 관성 무한대로 설정
            var physicsMass = PhysicsMass.CreateDynamic(MassProperties.UnitSphere, 1f);
            physicsMass.InverseInertia = float3.zero; // 회전 잠금 (관성 무한대)
            AddComponent(entity, physicsMass);

            // 기본 Unity 중력 비활성화
            AddComponent(entity, new PhysicsGravityFactor { Value = 0f });

            // Angular 속도 감쇠 (회전 방지)
            AddComponent(entity, new PhysicsDamping { Linear = 0f, Angular = 1f });

            // 플레이어 이름 컴포넌트 (Ghost 동기화를 위해 prefab에 추가)
            AddComponent(entity, new PlayerNameComponent
            {
                DisplayName = new Unity.Collections.FixedString64Bytes("Unknown"),
                NetworkId = 0
            });
        }
    }
}

public struct PlayerComponent : IComponentData
{
    public float Height;
    public float MoveSpeed;
    public float JumpForce;
    public uint LastJumpTick; // 마지막 점프한 틱
}

public struct PlayerVelocity : IComponentData
{
    public float3 Value;
}

public struct PlayerGroundState : IComponentData
{
    public bool IsGrounded;
    public float3 GroundNormal;
}
