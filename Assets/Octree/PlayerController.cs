// PlayerController.cs
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 50f;
    public float sprintMultiplier = 3f;

    void Update()
    {
        Vector3 move = Vector3.zero;
        
        if (Input.GetKey(KeyCode.W)) move.z += 1f;
        if (Input.GetKey(KeyCode.S)) move.z -= 1f;
        if (Input.GetKey(KeyCode.A)) move.x -= 1f;
        if (Input.GetKey(KeyCode.D)) move.x += 1f;
        if (Input.GetKey(KeyCode.E)) move.y += 1f;
        if (Input.GetKey(KeyCode.Q)) move.y -= 1f;
        
        float speed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * sprintMultiplier : moveSpeed;
        
        if (move.sqrMagnitude > 0.01f)
            transform.position += move.normalized * speed * Time.deltaTime;
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
        style.normal.textColor = Color.white;
        
        GUILayout.BeginArea(new Rect(10, 10, 600, 400));
        GUILayout.Label($"캐릭터 위치: {transform.position:F1}", style);
        
        if (OctreeManager.Instance != null)
        {
            var mgr = OctreeManager.Instance;
            bool insideActiveCell = mgr.IsInsideActiveCell(transform.position);
            bool insideRoot = mgr.IsInsideRoot(transform.position);
            
            GUILayout.Space(5);
            GUILayout.Label("<b>== 깊이 구성 ==</b>", style);
            GUILayout.Label($"기본: <color=gray>Depth {mgr.baseSubdivisionDepth}</color>", style);
            GUILayout.Label($"활성 셀: <color=yellow>Depth {mgr.activeTrackingDepth}</color>", style);
            GUILayout.Label($"이웃: <color=cyan>Depth {mgr.neighborPreloadDepth}</color>", style);
            GUILayout.Label($"최대 LOD: <color=lime>Depth {mgr.maxDepth}</color>", style);
            
            GUILayout.Space(5);
            GUILayout.Label("<b>== 상태 ==</b>", style);
            GUILayout.Label($"활성 셀 크기: {mgr.ActiveCellSize:F1}", style);
            GUILayout.Label($"활성 셀 안: {(insideActiveCell ? "<color=lime>예</color>" : "<color=yellow>부분 재렌더링</color>")}", style);
            GUILayout.Label($"루트 안: {(insideRoot ? "<color=lime>예</color>" : "<color=red>전체 재구성</color>")}", style);
            GUILayout.Label($"사용 노드: {mgr.UsedNodeCount}", style);
        }
        
        GUILayout.Space(10);
        GUILayout.Label("WASD: 이동 | QE: 상승/하강 | Shift: 빠르게", style);
        GUILayout.EndArea();
    }
}