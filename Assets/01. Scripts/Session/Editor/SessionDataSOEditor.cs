using UnityEngine;
using UnityEditor;
using System.IO;

namespace RealmForge.Session.Editor
{
    /// <summary>
    /// SessionDataSO asset을 자동으로 생성하는 에디터 유틸리티
    /// </summary>
    public static class SessionDataSOEditor
    {
        private const string ResourcesPath = "Assets/Resources";
        private const string AssetName = "SessionData";
        private const string AssetPath = ResourcesPath + "/" + AssetName + ".asset";

        [MenuItem("RealmForge/Session/Create Session Data Asset")]
        public static void CreateSessionDataAsset()
        {
            // Resources 폴더가 없으면 생성
            if (!AssetDatabase.IsValidFolder(ResourcesPath))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
                Debug.Log("[SessionDataSOEditor] Created Resources folder");
            }

            // 이미 존재하는지 확인
            var existingAsset = AssetDatabase.LoadAssetAtPath<SessionDataSO>(AssetPath);
            if (existingAsset != null)
            {
                Debug.Log("[SessionDataSOEditor] SessionData asset already exists!");
                Selection.activeObject = existingAsset;
                EditorGUIUtility.PingObject(existingAsset);
                return;
            }

            // 새 ScriptableObject 생성
            var sessionData = ScriptableObject.CreateInstance<SessionDataSO>();
            AssetDatabase.CreateAsset(sessionData, AssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SessionDataSOEditor] SessionData asset created at: {AssetPath}");
            Selection.activeObject = sessionData;
            EditorGUIUtility.PingObject(sessionData);
        }

        [MenuItem("RealmForge/Session/Select Session Data Asset")]
        public static void SelectSessionDataAsset()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SessionDataSO>(AssetPath);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                Debug.LogWarning("[SessionDataSOEditor] SessionData asset not found! Create it first.");
            }
        }

        [MenuItem("RealmForge/Session/Clear Session Data")]
        public static void ClearSessionData()
        {
            var asset = AssetDatabase.LoadAssetAtPath<SessionDataSO>(AssetPath);
            if (asset != null)
            {
                asset.Clear();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                Debug.Log("[SessionDataSOEditor] SessionData cleared!");
            }
            else
            {
                Debug.LogWarning("[SessionDataSOEditor] SessionData asset not found!");
            }
        }

        /// <summary>
        /// 프로젝트 로드 시 자동으로 SessionData asset이 있는지 확인
        /// </summary>
        [InitializeOnLoadMethod]
        private static void EnsureSessionDataExists()
        {
            // 에디터가 완전히 로드된 후 실행
            EditorApplication.delayCall += () =>
            {
                if (!File.Exists(AssetPath))
                {
                    Debug.LogWarning("[SessionDataSOEditor] SessionData asset not found. Creating automatically...");
                    CreateSessionDataAsset();
                }
            };
        }
    }
}
