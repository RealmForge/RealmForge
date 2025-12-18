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
            PlayerInput.ValueRW.inputVector = inputVector;
        }

    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        
    }
}
