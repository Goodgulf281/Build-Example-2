using UnityEngine;

namespace Goodgulf.TerrainUtils
{

    [System.Serializable]
    public class Biome
    {
        public string name;

        [Header("Height Settings")]
        public float baseHeight;        // Flat offset
        public float amplitude;         // Vertical scale
        public float noiseScale;        // Detail frequency

        [Header("FBM")]
        public int octaves = 5;
        public float lacunarity = 2f;
        public float gain = 0.5f;
    }


    public class BiomeTerrainGenerator : MonoBehaviour
    {
        [Header("Global")] public int seed = 12345;
        public float biomeScale = 0.0002f;
        public float biomeBlendWidth = 0.1f;

        [Header("Biomes (ordered)")] public Biome[] biomes;

        void Awake()
        {
            SimplexNoise.Initialize(seed);
        }

        public float SampleHeight(float worldX, float worldZ)
        {
            // --- 1. Sample biome noise ---
            float biomeValue = SimplexNoise.Noise(
                worldX * biomeScale,
                worldZ * biomeScale
            );

            // Normalize [-1,1] → [0,1]
            biomeValue = (biomeValue + 1f) * 0.5f;

            // Map biomeValue to biome index
            float biomePos = biomeValue * (biomes.Length - 1);
            int biomeIndex = Mathf.FloorToInt(biomePos);

            int biomeA = Mathf.Clamp(biomeIndex, 0, biomes.Length - 1);
            int biomeB = Mathf.Clamp(biomeIndex + 1, 0, biomes.Length - 1);

            float blend = biomePos - biomeIndex;
            blend = Mathf.SmoothStep(0f, 1f, blend / biomeBlendWidth);

            // --- 2. Sample height for each biome ---
            float hA = SampleBiomeHeight(biomes[biomeA], worldX, worldZ);
            float hB = SampleBiomeHeight(biomes[biomeB], worldX, worldZ);

            // --- 3. Blend biomes ---
            return Mathf.Lerp(hA, hB, blend);
        }

        float SampleBiomeHeight(Biome biome, float worldX, float worldZ)
        {
            float noise = FBM(
                worldX * biome.noiseScale,
                worldZ * biome.noiseScale,
                biome.octaves,
                biome.lacunarity,
                biome.gain
            );

            // Normalize [-1,1] → [0,1]
            noise = (noise + 1f) * 0.5f;

            return biome.baseHeight + noise * biome.amplitude;
        }

        float FBM(float x, float y, int octaves, float lacunarity, float gain)
        {
            float value = 0f;
            float amp = 1f;
            float freq = 1f;

            for (int i = 0; i < octaves; i++)
            {
                value += SimplexNoise.Noise(x * freq, y * freq) * amp;
                freq *= lacunarity;
                amp *= gain;
            }

            return value;
        }
        
        [ContextMenu("Generate with Biome")]
        public void GenerateAllTerrains()
        {
            SimplexNoise.Initialize(seed);
            
            Terrain[] terrains = Terrain.activeTerrains;

            foreach (Terrain terrain in terrains)
            {
                Debug.Log($"Generating terrain {terrain.name}");
                GenerateTerrain(terrain);
            }
            
            Debug.Log($"Finished generating terrain");
        }

        void GenerateTerrain(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;

            int res = data.heightmapResolution;
            float[,] heights = new float[res, res];

            Debug.Log($"Terrain resolution = {res}");
            
            Vector3 terrainPos = terrain.transform.position;
            Vector3 terrainSize = data.size;

            Debug.Log($"Terrain size = {terrainSize}");
            
            for (int y = 0; y < res; y++)
            {
                for (int x = 0; x < res; x++)
                {
                    // Normalized terrain coords
                    float nx = (float)x / (res - 1);
                    float ny = (float)y / (res - 1);

                    // Convert to WORLD coordinates
                    float worldX = terrainPos.x + nx * terrainSize.x;
                    float worldZ = terrainPos.z + ny * terrainSize.z;
                    
                    float height = SampleHeight(worldX, worldZ);
                    heights[y, x] = height;
                }
            }

            data.SetHeights(0, 0, heights);
        }
        
        
    }

}