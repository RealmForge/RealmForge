using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

public class PlayerInputAuthoring : MonoBehaviour
{
    public class Baker : Baker<PlayerInputAuthoring>
    {
        public override void Bake(PlayerInputAuthoring authoring){
            Entity entity =GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new PlayerInput());
        } 
    }
}

public struct PlayerInput : IInputComponentData
{
    public float2 inputVector;
}