using Unity.Entities;
using Unity.Physics; // 물리 기능을 위해 필수
using Unity.Mathematics;
using UnityEngine;

[Unity.Burst.BurstCompile]
public partial struct PlayerPhysicsSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        // 1. 입력 받기
        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");
        bool isJumpPressed = Input.GetKeyDown(KeyCode.Space);

        // 2. 물리 속도(PhysicsVelocity)를 제어
        // RefRW<PhysicsVelocity>: 속도를 읽고 쓰기 위해 필요
        foreach (var (velocity, moveSpeed, jumpProps) in 
                 SystemAPI.Query<RefRW<PhysicsVelocity>, RefRO<PlayerMoveSpeed>, RefRO<PlayerJumpProperties> >())
        {
            // --- [이동 로직] ---
            // 기존 속도의 Y값(중력 영향)은 유지하고, X, Z만 변경해야 함
            float3 currentVel = velocity.ValueRO.Linear;
            float3 newVel = currentVel;

            // 입력이 있을 때만 수평 속도 변경
            if (x != 0 || z != 0)
            {
                float3 inputDir = new float3(x, 0, z);
                if (math.lengthsq(inputDir) > 0)
                {
                    inputDir = math.normalize(inputDir);
                }
                
                // 원하는 이동 속도 계산
                float3 moveVel = inputDir * moveSpeed.ValueRO.Value;
                
                // Y축(낙하/점프)은 건드리지 않고 X, Z만 덮어쓰기
                newVel.x = moveVel.x;
                newVel.z = moveVel.z;
            }
            else
            {
                // 입력이 없으면 미끄러짐 방지를 위해 수평 속도 0으로 (원하는 경우 마찰력 사용 가능)
                newVel.x = 0;
                newVel.z = 0;
            }

            // --- [점프 로직] ---
            if (isJumpPressed)
            {
                // 즉시 위쪽 방향으로 속도를 줌 (대입)
                // 실제 게임에서는 바닥에 닿아있는지(Ground Check) 확인이 필요함
                newVel.y = jumpProps.ValueRO.JumpForce;
            }

            // 최종 속도 적용
            velocity.ValueRW.Linear = newVel;
        }
    }
}