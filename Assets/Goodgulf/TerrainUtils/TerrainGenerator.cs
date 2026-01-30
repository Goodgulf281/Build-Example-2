using UnityEngine;
using UnityEngine.Serialization;


namespace Goodgulf.TerrainUtils
{


    
    public static class SimplexNoise
    {
        // Gradients for 2D
        static readonly Vector2[] gradients =
        {
            new(1,1), new(-1,1), new(1,-1), new(-1,-1),
            new(1,0), new(-1,0), new(0,1), new(0,-1)
        };

        static int[] perm = new int[512];

        public static void Initialize(int seed)
        {
            System.Random rng = new System.Random(seed);
            int[] p = new int[256];

            for (int i = 0; i < 256; i++)
                p[i] = i;

            // Fisher–Yates shuffle
            for (int i = 255; i > 0; i--)
            {
                int swap = rng.Next(i + 1);
                (p[i], p[swap]) = (p[swap], p[i]);
            }

            for (int i = 0; i < 512; i++)
                perm[i] = p[i & 255];
        }

        static int FastFloor(float x) => x > 0 ? (int)x : (int)x - 1;

        static float Dot(Vector2 g, float x, float y) =>
            g.x * x + g.y * y;

        public static float Noise(float x, float y)
        {
            const float F2 = 0.366025403f; // (sqrt(3)-1)/2
            const float G2 = 0.211324865f; // (3-sqrt(3))/6

            float s = (x + y) * F2;
            int i = FastFloor(x + s);
            int j = FastFloor(y + s);

            float t = (i + j) * G2;
            float X0 = i - t;
            float Y0 = j - t;

            float x0 = x - X0;
            float y0 = y - Y0;

            int i1, j1;
            if (x0 > y0) { i1 = 1; j1 = 0; }
            else         { i1 = 0; j1 = 1; }

            float x1 = x0 - i1 + G2;
            float y1 = y0 - j1 + G2;
            float x2 = x0 - 1f + 2f * G2;
            float y2 = y0 - 1f + 2f * G2;

            int ii = i & 255;
            int jj = j & 255;

            float n0, n1, n2;

            float t0 = 0.5f - x0 * x0 - y0 * y0;
            if (t0 < 0) n0 = 0f;
            else
            {
                t0 *= t0;
                n0 = t0 * t0 * Dot(gradients[perm[ii + perm[jj]] & 7], x0, y0);
            }

            float t1 = 0.5f - x1 * x1 - y1 * y1;
            if (t1 < 0) n1 = 0f;
            else
            {
                t1 *= t1;
                n1 = t1 * t1 * Dot(gradients[perm[ii + i1 + perm[jj + j1]] & 7], x1, y1);
            }

            float t2 = 0.5f - x2 * x2 - y2 * y2;
            if (t2 < 0) n2 = 0f;
            else
            {
                t2 *= t2;
                n2 = t2 * t2 * Dot(gradients[perm[ii + 1 + perm[jj + 1]] & 7], x2, y2);
            }

            // Scale result to [-1, 1]
            return 70f * (n0 + n1 + n2);
        }
        
        public static float FBM(
            float x,
            float y,
            int octaves,
            float lacunarity,
            float gain)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;

            for (int i = 0; i < octaves; i++)
            {
                value += SimplexNoise.Noise(x * frequency, y * frequency) * amplitude;
                frequency *= lacunarity;
                amplitude *= gain;
            }

            return value;
        }
        
    }

    

    public static class SeededNoise
    {
        public static float Perlin(float x, float y, int seed)
        {
            // Hash seed into large offsets
            /*
            float offsetX = seed * 1000.123f;
            float offsetY = seed * 2000.456f;
    
            float result = Mathf.PerlinNoise(x + offsetX, y + offsetY);
            */
            const float seedScale = 0.001f;

            float sx = x + seed * 131.37f * seedScale;
            float sy = y + seed * 719.91f * seedScale;

            float result = Mathf.PerlinNoise(sx, sy);
            
            if(Random.Range(0,1000.0f) < 1.0f)
                Debug.Log($"Perlin({x}, {y}, {seed})={result} ");
            
            return result;
        }
        
        public static float FBM(float x, float y, int seed)
        {
            float value = 0f;
            float amplitude = 1f;
            float frequency = 1f;
    
            for (int i = 0; i < 5; i++)
            {
                value += SeededNoise.Perlin(
                    x * frequency,
                    y * frequency,
                    seed + i * 101
                ) * amplitude;
    
                amplitude *= 0.5f;
                frequency *= 2f;
            }
    
            return value;
        }
    }

    
    public class TerrainGenerator : MonoBehaviour
    {
        [Header("Generation Settings")]
        public int Seed = 12345;
        public float NoiseScale = 0.0015f;
        public float HeightMultiplier = 0.2f;
        public bool UseFBM = false;
        
            
        [ContextMenu("Generate Simplex")]
        public void GenerateAllTerrains()
        {
            SimplexNoise.Initialize(Seed);
            
            Terrain[] terrains = Terrain.activeTerrains;

            foreach (Terrain terrain in terrains)
            {
                Debug.Log($"Generating terrain for terrain {terrain.name}");
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

                    // Sample seeded noise in world space
                    float noise;
                    if (UseFBM)
                    {
                        float height = SimplexNoise.FBM(
                            worldX * NoiseScale,
                            worldZ * NoiseScale,
                            octaves: 5,
                            lacunarity: 2f,
                            gain: 0.5f
                        );

                        // Normalize from [-1,1] → [0,1]
                        height = (height + 1f) * 0.5f;

                        heights[y, x] = height * HeightMultiplier;
                    }
                    else
                    {
                        noise = SeededNoise.Perlin(
                            worldX * NoiseScale,
                            worldZ * NoiseScale,
                            Seed
                        );
                        
                        heights[y, x] = noise * HeightMultiplier;
                    }

                    
                    //if(x%32==0 && y%32==0)
                    //    Debug.Log($"H({y},{x}=={heights[y, x]})");
                }
            }

            data.SetHeights(0, 0, heights);
        }
        
        [ContextMenu("Reset All Terrains")]
        public void ResetAllTerrainsToZero()
        {
            Terrain[] terrains = Terrain.activeTerrains;

            foreach (Terrain terrain in terrains)
            {
                ResetTerrainToZero(terrain);
            }
        }

        private void ResetTerrainToZero(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;

            int res = data.heightmapResolution;
            float[,] heights = new float[res, res]; // defaults to 0

            data.SetHeights(0, 0, heights);
        }
        
        
        
        
    }



}