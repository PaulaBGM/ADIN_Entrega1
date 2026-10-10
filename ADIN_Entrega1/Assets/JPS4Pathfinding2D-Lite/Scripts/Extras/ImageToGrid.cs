using System;
using JPS4.Editor;
using JPS4.Optimized;
using UnityEngine;
using Unity.Collections;
using UnityEngine;

namespace JPS4
{
    public class ImageToGrid : MonoBehaviour
    {
        [SerializeField] private PathfindingGridBuilder builder;
        [SerializeField] private Texture2D sourceTexture;

        private void Awake()
        {
            // Build immediately for demo:
            Build();
        }

        [Button("Build")]
        public void Build()
        {
            if (sourceTexture == null)
            {
                Debug.LogError("No source texture assigned.");
                return;
            }

            int width = sourceTexture.width;
            int height = sourceTexture.height;
            NativeArray<bool> grid = new NativeArray<bool>(width * height, Allocator.Persistent);
            FillGridFromTexture(sourceTexture, grid);

            if (builder) builder.TryBuildExternal(grid);
            grid.Dispose();
        }

        void FillGridFromTexture(Texture2D tex, NativeArray<bool> grid)
        {
            var pixels = tex.GetPixels32();
            int total = pixels.Length;

            const float threshold = 0.1f;
            for (int i = 0; i < total; i++)
            {
                Color32 c = pixels[i];
                grid[i] = c.r >= threshold;
            }
        }
    }

}