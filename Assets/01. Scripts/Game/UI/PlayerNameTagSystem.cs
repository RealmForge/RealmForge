using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace RealmForge.Game.UI
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class PlayerNameTagSystem : SystemBase
    {
        private Dictionary<Entity, GameObject> nameTagMap = new Dictionary<Entity, GameObject>();
        private GameObject nameTagPrefab;

        protected override void OnCreate()
        {
            nameTagMap = new Dictionary<Entity, GameObject>();
        }

        protected override void OnUpdate()
        {
            // 네임태그 프리팹 확인 (처음 한 번만)
            if (nameTagPrefab == null)
            {
                CreateNameTagPrefab();
            }

            // 디버그: PlayerNameComponent를 가진 엔티티 수 확인
            int playerCount = 0;
            Entities
                .WithAll<PlayerNameComponent>()
                .WithNone<Prefab>()
                .WithoutBurst()
                .ForEach((Entity entity) =>
                {
                    playerCount++;
                }).Run();

            if (playerCount > 0)
            {
                Debug.Log($"[PlayerNameTagSystem] Found {playerCount} players with PlayerNameComponent");
            }

            // 새로운 플레이어에 대해 네임태그 생성
            Entities
                .WithAll<PlayerNameComponent>()
                .WithNone<Prefab>()
                .WithoutBurst()
                .ForEach((Entity entity, in PlayerNameComponent nameComp, in LocalTransform transform) =>
                {
                    Debug.Log($"[PlayerNameTagSystem] Processing entity with name: {nameComp.DisplayName}");

                    if (!nameTagMap.ContainsKey(entity))
                    {
                        GameObject nameTag = Object.Instantiate(nameTagPrefab);
                        nameTag.name = $"NameTag_{nameComp.DisplayName}";

                        // 텍스트 설정
                        Text textComponent = nameTag.GetComponentInChildren<Text>();
                        if (textComponent != null)
                        {
                            textComponent.text = nameComp.DisplayName.ToString();
                        }

                        nameTagMap[entity] = nameTag;
                        Debug.Log($"[PlayerNameTagSystem] Created nametag for: {nameComp.DisplayName}");
                    }
                }).Run();

            // 기존 네임태그 위치 및 회전 업데이트 (플레이어 로컬 좌표계 기준)
            foreach (var kvp in nameTagMap)
            {
                Entity entity = kvp.Key;
                GameObject nameTag = kvp.Value;

                if (nameTag == null || !SystemAPI.Exists(entity))
                    continue;

                if (SystemAPI.HasComponent<LocalTransform>(entity))
                {
                    LocalTransform transform = SystemAPI.GetComponent<LocalTransform>(entity);

                    // 플레이어의 로컬 "위" 방향으로 1.5 유닛 떨어진 위치에 배치
                    Unity.Mathematics.float3 upDirection = Unity.Mathematics.math.mul(transform.Rotation, new Unity.Mathematics.float3(0, 1, 0));
                    nameTag.transform.position = transform.Position + upDirection * 1.5f;

                    // 플레이어의 회전을 따라가도록 설정 (중력 방향에 맞춰 회전)
                    nameTag.transform.rotation = transform.Rotation;
                }
            }

            // 삭제된 엔티티의 네임태그 제거
            List<Entity> entitiesToRemove = new List<Entity>();
            foreach (var entity in nameTagMap.Keys)
            {
                if (!SystemAPI.Exists(entity))
                {
                    entitiesToRemove.Add(entity);
                }
            }

            foreach (var entity in entitiesToRemove)
            {
                if (nameTagMap.TryGetValue(entity, out GameObject nameTag))
                {
                    if (nameTag != null)
                    {
                        Object.Destroy(nameTag);
                    }
                    nameTagMap.Remove(entity);
                    Debug.Log($"[PlayerNameTagSystem] Removed nametag for destroyed entity");
                }
            }
        }

        protected override void OnDestroy()
        {
            // 모든 네임태그 정리
            foreach (var kvp in nameTagMap)
            {
                if (kvp.Value != null)
                {
                    Object.Destroy(kvp.Value);
                }
            }
            nameTagMap.Clear();
        }

        private void CreateNameTagPrefab()
        {
            // World Space Canvas 생성
            GameObject prefab = new GameObject("PlayerNameTag");

            Canvas canvas = prefab.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            RectTransform canvasRect = prefab.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(200, 50); // 픽셀 단위로 충분히 크게 설정
            canvasRect.pivot = new Vector2(0.5f, 0.5f); // 중앙을 기준점으로 설정 (머리 정중앙에 배치하기 위해)

            // CanvasScaler 추가 - 높은 해상도로 설정
            CanvasScaler scaler = prefab.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100; // 10에서 100으로 증가

            // GraphicRaycaster 추가 (렌더링에 도움)
            prefab.AddComponent<GraphicRaycaster>();

            // Billboard 컴포넌트 추가
            prefab.AddComponent<NameTagBillboard>();

            // Canvas 스케일 조정 (5배 증가)
            canvasRect.localScale = new Vector3(0.05f, 0.05f, 0.05f);

            // 텍스트 오브젝트 생성
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(prefab.transform, false);

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // Material 명시적으로 설정 (중요!)
            if (text.font != null)
            {
                text.material = text.font.material;
            }

            text.text = "Player";
            text.fontSize = 36; // 더 큰 폰트 크기
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false; // 레이캐스트 비활성화 (성능 향상)

            // 디버깅: 폰트와 텍스트 상태 확인
            Debug.Log($"[PlayerNameTagSystem] Font: {text.font != null}, Material: {text.material != null}, Text: '{text.text}', FontSize: {text.fontSize}, Color: {text.color}");

            RectTransform textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            nameTagPrefab = prefab;
            Debug.Log("[PlayerNameTagSystem] Created nametag prefab with improved settings");
        }
    }
}
