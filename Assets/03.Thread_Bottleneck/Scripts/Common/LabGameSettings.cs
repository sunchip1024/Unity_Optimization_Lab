using UnityEngine;
namespace OptimizationLab
{
    [CreateAssetMenu(menuName = "Optimization Lab/Game Settings")]
    public sealed class LabGameSettings : ScriptableObject
    {
        public int seed = 1313;
        public float duration = 180f;
        public float playerSpeed = 6f;
        public int maxEnemies = 180;
        public float spawnPerSecond = 5f;
        public int maxProjectiles = 160;
        public int maxGems = 300;
        public GameObject playerPrefab, enemyPrefab, projectilePrefab, gemPrefab, orbitPrefab;
        public Mesh crowdMesh;
        public Material crowdMaterial;
        public Material ultimateMaterial;
    }
}
