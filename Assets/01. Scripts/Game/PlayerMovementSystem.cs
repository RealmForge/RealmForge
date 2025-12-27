using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;
using Unity.Transforms;

// 플레이어 이동 시스템
[UpdateInGroup(typeof(PredictedSimulationSystemGroup))]
[UpdateAfter(typeof(GravitySystem))]
[UpdateBefore(typeof(PlayerOrientationSystem))]
[BurstCompile]
partial struct PlayerMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var deltaTime = SystemAPI.Time.DeltaTime;
        var physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
        var networkTime = SystemAPI.GetSingleton<NetworkTime>();
        var currentTick = networkTime.ServerTick.TickIndexForValidTick;

        foreach (var (playerInput, localTransform, velocity, physicsVelocity, groundState, playerComp)
                 in SystemAPI.Query<
                    RefRO<PlayerInput>,
                    RefRO<LocalTransform>,
                    RefRW<PlayerVelocity>,
                    RefRW<PhysicsVelocity>,
                    RefRW<PlayerGroundState>,
                    RefRW<PlayerComponent>>()
                     .WithAll<Simulate, GhostOwnerIsLocal>())
        {
            // 인스펙터에서 설정한 값 사용
            float moveSpeed = playerComp.ValueRO.MoveSpeed;
            float jumpForce = playerComp.ValueRO.JumpForce;

            // 고정 값
            float airControl = 0.3f;
            float groundAcceleration = 10f;
            float friction = 5f;
            uint jumpCooldownTicks = 18; // 점프 쿨다운 (틱 수, 60fps 기준 약 0.3초)

            // 플레이어의 up 방향 (지면 노말)
            var up = groundState.ValueRO.GroundNormal;

            // 플레이어 로컬 방향 계산
            var playerRotation = localTransform.ValueRO.Rotation;
            var forward = math.mul(playerRotation, new float3(0, 0, 1));
            var right = math.mul(playerRotation, new float3(1, 0, 0));

            // 입력에 따른 이동 방향 (플레이어 로컬 기준)
            var inputDirection = right * playerInput.ValueRO.inputVector.x +
                               forward * playerInput.ValueRO.inputVector.y;

            // 입력 정규화
            var inputLength = math.length(inputDirection);
            if (inputLength > 0.001f)
            {
                inputDirection = inputDirection / inputLength;
            }

            if (groundState.ValueRO.IsGrounded)
            {
                // 지면에서의 이동
                var targetVelocity = inputDirection * moveSpeed;

                // 수평 속도만 조정 (수직 속도는 중력이 처리)
                var currentVerticalVel = up * math.dot(velocity.ValueRO.Value, up);
                var currentHorizontalVel = velocity.ValueRO.Value - currentVerticalVel;

                // 부드럽게 목표 속도로 가속
                var velocityChange = targetVelocity - currentHorizontalVel;
                velocity.ValueRW.Value += velocityChange * math.min(1f, deltaTime * groundAcceleration);

                // 점프 - 충분한 틱이 지났고, 위쪽 속도가 거의 없을 때만
                var upwardVelocity = math.dot(velocity.ValueRO.Value, up);
                var ticksSinceLastJump = currentTick - playerComp.ValueRO.LastJumpTick;

                if (playerInput.ValueRO.jump && upwardVelocity < 0.1f && ticksSinceLastJump >= jumpCooldownTicks)
                {
                    velocity.ValueRW.Value += up * jumpForce;
                    // 점프 즉시 IsGrounded를 false로 설정 (중복 점프 방지)
                    groundState.ValueRW.IsGrounded = false;
                    // 마지막 점프 틱 저장 (연타 방지)
                    playerComp.ValueRW.LastJumpTick = currentTick;
                }

                // 마찰 적용
                velocity.ValueRW.Value *= 1f - (friction * deltaTime);
            }
            else
            {
                // 공중에서는 제한적인 제어
                var airAcceleration = inputDirection * moveSpeed * airControl;
                velocity.ValueRW.Value += airAcceleration * deltaTime;
            }

            // PhysicsVelocity 설정 - 물리 엔진이 충돌을 처리하며 위치를 업데이트함
            physicsVelocity.ValueRW.Linear = velocity.ValueRO.Value;
        }
    }
}
