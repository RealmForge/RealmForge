using Unity.Burst;
using Unity.Entities;
using Unity.NetCode;
using Unity.Mathematics;
using UnityEngine;

[UpdateInGroup(typeof(GhostInputSystemGroup))]
partial struct PlayerInputSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<NetworkStreamInGame>();
        state.RequireForUpdate<PlayerInput>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (RefRW<PlayerInput> PlayerInput in SystemAPI.Query<RefRW<PlayerInput>>().WithAll<GhostOwnerIsLocal>())
        {
            float2 inputVector = new float2();
            if (Input.GetKey(KeyCode.W))
            {
                inputVector.y = +1f;
            }
            if (Input.GetKey(KeyCode.S)) {
                inputVector.y = -1f;
            }

            if (Input.GetKey(KeyCode.A))
            {
                inputVector.x = -1f;
            }

            if (Input.GetKey(KeyCode.D))
            {
                inputVector.x = +1f;
            }

            bool jump = Input.GetKeyDown(KeyCode.Space); // GetKeyDown = 눌린 순간만 true

            // 마우스 입력 (항상 받기)
            float2 mouseDelta = new float2(
                Input.GetAxis("Mouse X"),
                Input.GetAxis("Mouse Y")
            );

            PlayerInput.ValueRW.inputVector = inputVector;
            PlayerInput.ValueRW.jump = jump;
            PlayerInput.ValueRW.mouseDelta = mouseDelta;
        }

    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
