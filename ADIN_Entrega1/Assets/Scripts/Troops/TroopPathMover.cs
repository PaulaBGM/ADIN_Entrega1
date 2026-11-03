using System.Collections.Generic;
using JPS4;
using UnityEngine;

/// <summary>
/// Mueve la tropa por el camino que calcula el plugin JPS4 (PathfindingGridBuilder).
/// Sustituye al NavMeshAgent: MoveTo / Stop / IsMoving.
/// El giro se hace solo con SpriteRenderer.flipX (el arte de todas las tropas mira a la derecha).
/// </summary>
public class TroopPathMover : MonoBehaviour
{
    [SerializeField] private PathfindingGridBuilder grid;   // Si se deja vacío, se busca en la escena
    [SerializeField] private float speed = 2f;               // Unidades de mundo por segundo
    [SerializeField] private float nodeTolerance = 0.03f;    // Distancia para dar un nodo por alcanzado
    [SerializeField] private bool flipSpriteByDirection = true;

    [SerializeField, Tooltip("SpriteRenderer del cuerpo (el del Animator). Si se deja vacío se busca solo.")]
    private SpriteRenderer bodySprite;

    public enum InitialFacing { AsAuthored, Right, Left }

    [SerializeField, Tooltip("Hacia dónde mira la tropa al aparecer. Equipo azul: Right. Equipo morado: Left.")]
    private InitialFacing initialFacing = InitialFacing.AsAuthored;

    private List<Vector3> path;
    private int index;
    private SpriteRenderer sprite;
    private float nextDiagnostic;

    public bool IsMoving { get; private set; }
    public float Speed { get => speed; set => speed = value; }

    /// <summary>True si la tropa mira a la izquierda.</summary>
    public bool FacingLeft => sprite != null && sprite.flipX;

    // Se mantiene por compatibilidad con scripts antiguos: ya no se gira con la escala.
    public bool MirrorsWithScale => false;

    private void Awake()
    {
        if (grid == null) grid = FindFirstObjectByType<PathfindingGridBuilder>();
        sprite = FindBodySprite();

        if (sprite == null)
            Debug.LogWarning($"[{name}] TroopPathMover no encuentra el SpriteRenderer del cuerpo; no podrá girar.", this);
    }

    private void Start()
    {
        ApplyInitialFacing();
    }

    private SpriteRenderer FindBodySprite()
    {
        if (bodySprite != null) return bodySprite;

        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            SpriteRenderer onAnimator = animator.GetComponent<SpriteRenderer>();
            if (onAnimator != null) return onAnimator;
        }

        return GetComponentInChildren<SpriteRenderer>();
    }

    /// <summary>Orienta la tropa según 'initialFacing'. Útil también al reiniciar la formación.</summary>
    public void ApplyInitialFacing()
    {
        if (initialFacing == InitialFacing.Right) Flip(1f);
        else if (initialFacing == InitialFacing.Left) Flip(-1f);
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

    /// <summary>
    /// Deja el sprite sin voltear, tal como está dibujado. Para animaciones que ya traen
    /// su propia dirección (ataques del lancero con clips de izquierda).
    /// </summary>
    public void ResetFacing()
    {
        if (sprite != null) sprite.flipX = false;
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
        sprite.flipX = dirX < 0f;
    }
}