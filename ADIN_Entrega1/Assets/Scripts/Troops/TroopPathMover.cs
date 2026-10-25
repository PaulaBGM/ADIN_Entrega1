using System.Collections.Generic;
using JPS4;
using UnityEngine;

/// <summary>
/// Mueve la tropa por el camino que calcula el plugin JPS4 (PathfindingGridBuilder).
/// Sustituye al NavMeshAgent: MoveTo / Stop / IsMoving.
/// </summary>
public class TroopPathMover : MonoBehaviour
{
    [SerializeField] private PathfindingGridBuilder grid;   // Si se deja vacío, se busca en la escena
    [SerializeField] private float speed = 2f;               // Unidades de mundo por segundo
    [SerializeField] private float nodeTolerance = 0.03f;    // Distancia para dar un nodo por alcanzado
    [SerializeField] private bool flipSpriteByDirection = true; // Asume que el sprite mira a la derecha
    [SerializeField, Tooltip("Girar con la escala X del objeto del sprite (Visual) en vez de flipX. Así el hitbox del ataque también se gira con la tropa.")]
    private bool mirrorWithScale = true;

    private List<Vector3> path;
    private int index;
    private SpriteRenderer sprite;

    public bool IsMoving { get; private set; }
    public float Speed { get => speed; set => speed = value; }

    private void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<PathfindingGridBuilder>();
        sprite = GetComponentInChildren<SpriteRenderer>();

        // Si el prefab tiene Flip X marcado (p. ej. el equipo que mira a la izquierda),
        // lo pasamos a la escala para no girar dos veces.
        if (mirrorWithScale && sprite != null && sprite.flipX)
        {
            sprite.flipX = false;
            Vector3 scale = sprite.transform.localScale;
            scale.x = -Mathf.Abs(scale.x);
            sprite.transform.localScale = scale;
        }
    }

    /// <summary>Calcula un camino hasta destination y empieza a seguirlo. Devuelve false si no hay camino.</summary>
    public bool MoveTo(Vector3 destination)
    {
        if (grid == null)
        {
            Debug.LogError($"[{name}] TroopPathMover no encuentra un PathfindingGridBuilder en la escena.");
            return false;
        }

        // Con Collision Type = Custom la rejilla la construye el TerrainGridBaker: no llamar a Build()
        if (!grid.IsBuilt && grid.CollisionSettings.CollisionType != ECollisionType.Custom)
            grid.Build();

        if (!grid.IsBuilt)
        {
            Debug.LogWarning($"[{name}] La rejilla no está construida. Con Collision Type = Custom necesitas un " +
                             "TerrainGridBaker en la escena con al menos un Tilemap asignado (mira su aviso en la consola).");
            IsMoving = false;
            return false;
        }

        var config = new PathConfig
        {
            CollapseSkips = true,     // Un nodo por celda, así el movimiento es continuo
            UseClosestTarget = true,  // Si el destino no es alcanzable, ir a lo más cercano
            QueryDynamic = false
        };

        path = grid.GetPath(transform.position, destination, config);

        if (path == null || path.Count == 0)
        {
            IsMoving = false;
            if (Time.time >= nextDiagnostic)
            {
                nextDiagnostic = Time.time + 2f; // No spamear la consola
                Debug.LogWarning($"[{name}] Sin camino. Inicio: {Describe(transform.position)} | Destino: {Describe(destination)}");
            }
            return false;
        }

        // El primer nodo es el centro de la celda actual. Si ya hemos pasado de él
        // (por ejemplo al recalcular mientras andamos), lo saltamos para no dar un paso atrás.
        index = 0;
        if (path.Count > 1)
        {
            float segment = Vector2.Distance(path[0], path[1]);
            if (Vector2.Distance(transform.position, path[1]) < segment)
                index = 1;
        }

        IsMoving = true;
        return true;
    }

    public void Stop()
    {
        IsMoving = false;
        path = null;
    }

    private float nextDiagnostic;

    /// <summary>Texto de diagnóstico: celda, si está dentro de la rejilla y si es caminable.</summary>
    private string Describe(Vector3 world)
    {
        var tg = grid.Grid;
        Vector3 local = grid.transform.InverseTransformPoint(world) - grid.LocalLeftCorner;
        bool inside = local.x >= 0 && local.y >= 0 &&
                      local.x < tg.Size.x * grid.CellSize && local.y < tg.Size.y * grid.CellSize;

        var cell = grid.GetGridPosition(world);
        string walk = inside ? (tg.IsTraversable(cell) ? "caminable" : "BLOQUEADA") : "FUERA DE LA REJILLA";
        return $"pos ({world.x:0.0}, {world.y:0.0}) celda ({cell.x}, {cell.y}) -> {walk}";
    }

    /// <summary>Girar el sprite hacia un punto (para el ataque, cuando está parada).</summary>
    public void FaceTowards(Vector3 point)
    {
        Flip(point.x - transform.position.x);
    }

    private void Update()
    {
        if (!IsMoving || path == null) return;

        // Saltar nodos ya alcanzados
        while (index < path.Count && Vector2.Distance(transform.position, path[index]) <= nodeTolerance)
            index++;

        if (index >= path.Count)
        {
            Stop(); // Ha llegado al final del camino
            return;
        }

        Vector3 node = path[index];
        Vector3 target = new Vector3(node.x, node.y, transform.position.z); // Conservar la Z de la tropa
        Vector3 before = transform.position;

        transform.position = Vector3.MoveTowards(before, target, speed * Time.deltaTime);
        Flip(transform.position.x - before.x);
    }

    private void Flip(float dirX)
    {
        if (!flipSpriteByDirection || sprite == null || Mathf.Abs(dirX) < 0.0001f) return;

        if (mirrorWithScale)
        {
            Transform visual = sprite.transform;
            Vector3 scale = visual.localScale;
            float sign = dirX < 0f ? -1f : 1f;
            if (!Mathf.Approximately(Mathf.Sign(scale.x), sign))
            {
                scale.x = -scale.x;
                visual.localScale = scale;
            }
        }
        else sprite.flipX = dirX < 0f;
    }
}