// PlayerController.cs
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    [Header("이동")]
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
        var style = new GUIStyle(GUI.skin.label) { fontSize = 16, richText = true };
        style.normal.textColor = Color.white;
        
        GUILayout.BeginArea(new Rect(10, 10, 350, 350));
        
        GUILayout.Label($"<b>위치:</b> {transform.position:F0}", style);
        
        if (OctreeManager.Instance != null)
        {
            var mgr = OctreeManager.Instance;
            
            GUILayout.Space(10);
            GUILayout.Label($"<b>노드 수:</b> <color=yellow>{mgr.UsedNodeCount}</color>", style);
            GUILayout.Label($"<b>여유 노드:</b> {mgr.FreeNodeCount}", style);
            GUILayout.Label($"<b>플레이어 깊이:</b> <color=cyan>{mgr.PlayerNodeDepth}</color> / {mgr.maxDepth}", style);
            GUILayout.Label($"<b>이번 프레임 분할:</b> {mgr.LastSubdivisions}", style);
            GUILayout.Label($"<b>루트 안:</b> {(mgr.IsInsideRoot(transform.position) ? "<color=lime>O</color>" : "<color=red>X</color>")}", style);
            
            GUILayout.Space(10);
            GUILayout.Label("<b>-- 색상 --</b>", style);
            GUILayout.Label("<color=#808080>■</color>0 <color=#FF3333>■</color>1 <color=#FF9933>■</color>2 <color=#FFFF33>■</color>3", style);
            GUILayout.Label("<color=#33FF33>■</color>4 <color=#33FFFF>■</color>5 <color=#3366FF>■</color>6 <color=#9933FF>■</color>7 <color=#FF33FF>■</color>8", style);
        }
        
        GUILayout.Space(10);
        GUILayout.Label("WASD QE Shift", style);
        
        GUILayout.EndArea();
    }
}