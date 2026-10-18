using System.Collections.Generic;
using JPS4;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Construye la rejilla de pathfinding (PathfindingGridBuilder) a partir de Tilemaps:
///  - Tilemaps "bloqueados": cualquier celda con tile es NO caminable (agua, árboles, arbustos, muros...).
///  - Niveles de altura: cada nivel tiene un Tilemap con el suelo de esa altura.
///  - Rampas: celdas que conectan alturas distintas.
/// Dos celdas vecinas de distinta altura solo se conectan a través de una rampa;
/// en el resto, el borde del desnivel se marca como no caminable.
///
/// Uso: poner en un GameObject de la escena, asignar el PathfindingGridBuilder y los Tilemaps.
/// El PathfindingGridBuilder debe tener Collision Type = Custom (esta clase le da la rejilla).
///
/// Además expone una API de celdas (TryWorldToCell, CellToWorld, IsWalkable) que usa
/// el TroopPlacementManager para colocar tropas.
/// </summary>
/// 
[DefaultExecutionOrder(-200)] // Antes que las tropas, que piden caminos en su Start
public class TerrainGridBaker : MonoBehaviour
{
    public enum EdgeMode
    {
        BlockHigherSide, // Bloquea la celda alta del borde (el filo de la meseta)
        BlockLowerSide,  // Bloquea la celda baja del borde (el pie del acantilado)
        BlockBoth
    }

    [System.Serializable]
    public class HeightLevel
    {
        public string name = "Nivel 0";
        public int level = 0;
        [Tooltip("Tilemap con el suelo de este nivel de altura.")]
        public Tilemap ground;
    }

    private const int NoGround = int.MinValue;

    [Header("Referencias")]
    [SerializeField] private PathfindingGridBuilder grid;

    [Header("Zonas no caminables")]
    [SerializeField, Tooltip("Cualquier celda con tile en estos Tilemaps no es caminable.")]
    private List<Tilemap> blockedTilemaps = new();
    [SerializeField, Tooltip("Opcional: capas de Collider2D que también bloquean (se comprueba el centro de cada celda).")]
    private LayerMask obstacleColliders;

    [Header("Alturas")]
    [SerializeField] private List<HeightLevel> heightLevels = new();
    [SerializeField, Tooltip("Celdas que conectan alturas distintas (escaleras / rampas).")]
    private List<Tilemap> rampTilemaps = new();
    [SerializeField] private EdgeMode edgeMode = EdgeMode.BlockHigherSide;
    [SerializeField, Tooltip("Si hay niveles de altura, las celdas sin suelo en ningún nivel (agua, vacío) se bloquean.")]
    private bool blockCellsWithoutGround = true;

    [Header("Rejilla")]
    [SerializeField, Tooltip("Celdas extra alrededor de los tilemaps.")]
    private int padding = 1;

    [Header("Debug")]
    [SerializeField] private bool drawGizmos = true;

    // Resultado del último cálculo
    private int2 size;
    private Vector2 origin;     // Esquina inferior izquierda de la rejilla (mundo)
    private float cellSize;
    private bool[] walkable;
    private int[] levels;
    private bool[] ramps;

    private void Awake()
    {
        Bake();
    }

    /// <summary>Calcula la rejilla y se la entrega al PathfindingGridBuilder.</summary>
    public void Bake()
    {
        if (!Compute()) return;

        // La rejilla del plugin está centrada en su transform
        Vector2 centre = origin + new Vector2(size.x, size.y) * (cellSize * 0.5f);
        grid.transform.position = new Vector3(centre.x, centre.y, grid.transform.position.z);

        var native = new NativeArray<bool>(walkable, Allocator.Temp);
        bool ok = grid.TryBuildExternal(native, size);
        native.Dispose();

        if (!ok) Debug.LogError("[TerrainGridBaker] No se pudo construir la rejilla.");
        else Debug.Log($"[TerrainGridBaker] Rejilla construida: {size.x}x{size.y} celdas de {cellSize} unidades.");
    }

    [ContextMenu("Previsualizar rejilla (gizmos)")]
    private void Preview()
    {
        Compute();
    }

    // ------------------------------------------------------------------
    //  API de celdas (la usa el TroopPlacementManager)
    // ------------------------------------------------------------------

    /// <summary>True cuando la rejilla ya está calculada.</summary>
    public bool IsReady => walkable != null;

    /// <summary>Lado de una celda en unidades de mundo.</summary>
    public float CellSize => cellSize;

    /// <summary>Esquina inferior izquierda de la rejilla en el mundo.</summary>
    public Vector2 Origin => origin;

    /// <summary>Tamaño de la rejilla en celdas.</summary>
    public Vector2Int GridSize => new Vector2Int(size.x, size.y);

    /// <summary>Celda que contiene un punto del mundo. False si cae fuera de la rejilla.</summary>
    public bool TryWorldToCell(Vector3 world, out Vector2Int cell)
    {
        cell = default;
        if (walkable == null) return false;

        int x = Mathf.FloorToInt((world.x - origin.x) / cellSize);
        int y = Mathf.FloorToInt((world.y - origin.y) / cellSize);
        if (x < 0 || y < 0 || x >= size.x || y >= size.y) return false;

        cell = new Vector2Int(x, y);
        return true;
    }

    /// <summary>Centro de una celda en el mundo (z = 0).</summary>
    public Vector3 CellToWorld(Vector2Int cell)
    {
        Vector2 c = CellCenter(cell.x, cell.y);
        return new Vector3(c.x, c.y, 0f);
    }

    /// <summary>True si la celda existe y es caminable (no agua, árbol, borde de desnivel...).</summary>
    public bool IsWalkable(Vector2Int cell)
    {
        if (walkable == null) return false;
        if (cell.x < 0 || cell.y < 0 || cell.x >= size.x || cell.y >= size.y) return false;
        return walkable[cell.y * size.x + cell.x];
    }

    // ------------------------------------------------------------------

    private bool Compute()
    {
        if (grid == null) grid = FindFirstObjectByType<PathfindingGridBuilder>();
        if (grid == null)
        {
            Debug.LogError("[TerrainGridBaker] Falta el PathfindingGridBuilder.");
            return false;
        }

        cellSize = grid.CellSize;
        if (!TryGetBounds(out Vector2 min, out Vector2 max))
        {
            Debug.LogWarning("[TerrainGridBaker] No hay tilemaps con tiles. Asigna al menos uno.");
            return false;
        }

        min -= Vector2.one * (padding * cellSize);
        max += Vector2.one * (padding * cellSize);

        origin = min;
        size = new int2(Mathf.CeilToInt((max.x - min.x) / cellSize),
                        Mathf.CeilToInt((max.y - min.y) / cellSize));

        int count = size.x * size.y;
        walkable = new bool[count];
        levels = new int[count];
        ramps = new bool[count];

        bool useLevels = heightLevels.Count > 0;

        // Pasada 1: suelo, rampas y bloqueos
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int i = y * size.x + x;
                Vector2 p = CellCenter(x, y);

                int lvl = NoGround;
                foreach (var hl in heightLevels)
                {
                    if (hl.ground != null && HasTile(hl.ground, p))
                        lvl = Mathf.Max(lvl, hl.level);
                }
                levels[i] = lvl;

                foreach (var r in rampTilemaps)
                {
                    if (r != null && HasTile(r, p)) { ramps[i] = true; break; }
                }

                bool blocked = false;

                foreach (var b in blockedTilemaps)
                {
                    if (b != null && HasTile(b, p)) { blocked = true; break; }
                }

                if (!blocked && obstacleColliders.value != 0 && Physics2D.OverlapPoint(p, obstacleColliders))
                    blocked = true;

                if (!blocked && useLevels && blockCellsWithoutGround && lvl == NoGround && !ramps[i])
                    blocked = true;

                walkable[i] = !blocked;
            }
        }

        // Pasada 2: bordes entre alturas distintas (salvo rampas)
        if (useLevels)
        {
            int[] dx = { 1, -1, 0, 0 };
            int[] dy = { 0, 0, 1, -1 };

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int i = y * size.x + x;
                    if (levels[i] == NoGround || ramps[i]) continue;

                    for (int d = 0; d < 4; d++)
                    {
                        int nx = x + dx[d], ny = y + dy[d];
                        if (nx < 0 || ny < 0 || nx >= size.x || ny >= size.y) continue;

                        int j = ny * size.x + nx;
                        if (levels[j] == NoGround || ramps[j]) continue;

                        if (levels[i] > levels[j] && edgeMode != EdgeMode.BlockLowerSide) walkable[i] = false;
                        if (levels[i] < levels[j] && edgeMode != EdgeMode.BlockHigherSide) walkable[i] = false;
                    }
                }
            }
        }

        return true;
    }

    private Vector2 CellCenter(int x, int y)
    {
        return origin + new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
    }

    private static bool HasTile(Tilemap map, Vector2 worldPoint)
    {
        return map.HasTile(map.WorldToCell(new Vector3(worldPoint.x, worldPoint.y, map.transform.position.z)));
    }

    private bool TryGetBounds(out Vector2 min, out Vector2 max)
    {
        // Variables locales: una función local no puede usar parámetros out
        Vector2 lo = new Vector2(float.MaxValue, float.MaxValue);
        Vector2 hi = new Vector2(float.MinValue, float.MinValue);
        bool any = false;

        void Include(Tilemap map)
        {
            if (map == null || map.GetUsedTilesCount() == 0) return;
            BoundsInt b = map.cellBounds;
            Vector3 a = map.CellToWorld(b.min);
            Vector3 c = map.CellToWorld(b.max);
            lo = Vector2.Min(lo, Vector2.Min(a, c));
            hi = Vector2.Max(hi, Vector2.Max(a, c));
            any = true;
        }

        foreach (var m in blockedTilemaps) Include(m);
        foreach (var h in heightLevels) Include(h.ground);
        foreach (var r in rampTilemaps) Include(r);

        min = lo;
        max = hi;
        return any;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos || walkable == null || levels == null) return;

        Vector3 cube = new Vector3(cellSize * 0.95f, cellSize * 0.95f, 0.01f);

        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                int i = y * size.x + x;
                Vector2 c = CellCenter(x, y);

                if (!walkable[i])
                {
                    Gizmos.color = new Color(1f, 0.15f, 0.15f, 0.45f);   // No caminable
                }
                else if (ramps[i])
                {
                    Gizmos.color = new Color(1f, 0.9f, 0.1f, 0.45f);      // Rampa
                }
                else if (levels[i] != NoGround)
                {
                    Gizmos.color = Color.HSVToRGB((levels[i] * 0.17f + 0.35f) % 1f, 0.6f, 1f);
                    Gizmos.color = new Color(Gizmos.color.r, Gizmos.color.g, Gizmos.color.b, 0.2f); // Color por altura
                }
                else
                {
                    continue;
                }

                Gizmos.DrawCube(c, cube);
            }
        }
    }
}