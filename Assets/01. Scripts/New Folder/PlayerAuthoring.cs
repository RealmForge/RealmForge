using Unity.Entities;
using UnityEngine;

public class PlayerAuthoring : MonoBehaviour
{
    public float MoveSpeed = 5.0f;
    public float JumpForce = 5.0f; // 점프 힘 추가

    public class PlayerBaker : Baker<PlayerAuthoring>
    {
        public override void Bake(PlayerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // 이동 데이터
            AddComponent(entity, new PlayerMoveSpeed
            {
                Value = authoring.MoveSpeed
            });

            // 점프 데이터 추가
            AddComponent(entity, new PlayerJumpProperties
            {
                JumpForce = authoring.JumpForce
            });
        }
    }
}