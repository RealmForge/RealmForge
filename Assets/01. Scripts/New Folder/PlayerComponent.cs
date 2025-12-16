using Unity.Entities;

public struct PlayerJumpProperties : IComponentData
{
    public float JumpForce;
}

// IComponentData를 상속받아 데이터임을 명시합니다.
public struct PlayerMoveSpeed : IComponentData
{
    public float Value;
}