using UnityEngine;

/// <summary>
/// 간단한 캐릭터 이동
/// WASD: XZ 이동, QE: Y 이동
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("이동 설정")]
    public float moveSpeed = 50f;
    public float sprintMultiplier = 3f;

    void Update()
    {
        Vector3 move = Vector3.zero;
        
        // WASD: XZ 평면
        if (Input.GetKey(KeyCode.W)) move.z += 1f;
        if (Input.GetKey(KeyCode.S)) move.z -= 1f;
        if (Input.GetKey(KeyCode.A)) move.x -= 1f;
        if (Input.GetKey(KeyCode.D)) move.x += 1f;
        
        // QE: Y축
        if (Input.GetKey(KeyCode.E)) move.y += 1f;
        if (Input.GetKey(KeyCode.Q)) move.y -= 1f;
        
        float speed = Input.GetKey(KeyCode.LeftShift) ? moveSpeed * sprintMultiplier : moveSpeed;
        
        if (move.sqrMagnitude > 0.01f)
            transform.position += move.normalized * speed * Time.deltaTime;
    }

    void OnGUI()
    {
        var style = new GUIStyle(GUI.skin.label) { fontSize = 14 };
        style.normal.textColor = Color.white;
        
        GUILayout.BeginArea(new Rect(10, 10, 450, 200));
        GUILayout.Label($"캐릭터 위치: {transform.position:F1}", style);
        
        if (OctreeManager.Instance != null)
        {
            var mgr = OctreeManager.Instance;
            bool inside = mgr.IsInsideCenterCube(transform.position);
            
            GUILayout.Label($"현재 Pivot: {mgr.CurrentPivot:F1}", style);
            GUILayout.Label($"중앙 큐브: ({mgr.CenterMin:F1} ~ {mgr.CenterMax:F1})", style);
            GUILayout.Label($"중앙 큐브 안: {(inside ? "<color=lime>예</color>" : "<color=red>아니오</color>")}", style);
        }
        
        GUILayout.Space(10);
        GUILayout.Label("WASD: 수평 이동 | QE: 상승/하강 | Shift: 빠르게", style);
        GUILayout.EndArea();
    }
}