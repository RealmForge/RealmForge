using Unity.Entities;
using Unity.Collections;
using Unity.NetCode;

namespace RealmForge.Game.UI
{
    [GhostComponent(PrefabType = GhostPrefabType.AllPredicted)]
    public struct PlayerNameComponent : IComponentData
    {
        [GhostField] public FixedString64Bytes DisplayName;
        [GhostField] public int NetworkId;
    }
}
