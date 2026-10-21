using System.Collections.Generic;
using FSM.Enemy;
using UnityEngine;
using UnityEngine.Tilemaps;
#if !ENABLE_LEGACY_INPUT_MANAGER && ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>Teclas que se pueden asignar a una tropa o a la base (los nombres coinciden con Key del Input System).</summary>
public enum PlacementKey
{
    None,
    A, B, C, D, E, F, G, H, I, J, K, L, M, N, O, P, Q, R, S, T, U, V, W, X, Y, Z,
    Digit0, Digit1, Digit2, Digit3, Digit4, Digit5, Digit6, Digit7, Digit8, Digit9,
    Space, UpArrow, DownArrow, LeftArrow, RightArrow
}

/// <summary>
/// Colocaci�n en grid para dos jugadores, al estilo Mario + Rabbids:
///  - Cada jugador tiene su campo (zona del mapa) y solo puede colocar tropas dentro de �l.
///  - Con la tecla de una tropa (J1: 1,2,3,4 � J2: 7,8,9,0) se elige jugador y tropa; clic izquierdo la coloca en una celda.
///  - Una celda solo admite una tropa y solo celdas caminables.
///  - Cada jugador puede mover su base a cualquier celda libre de su campo.
///  - Clic derecho quita una tropa. "Empezar combate" (Enter) las activa a todas a la vez:
///    cada tropa va directa a la base enemiga y ataca a los contrincantes con los que se encuentra.
///  - "Reiniciar" (R) repite la misma formaci�n; "Vaciar" la borra para montar otra combinaci�n.
/// </summary>
public class TroopPlacementManager : MonoBehaviour
{
    private const int CellPixels = 8; // P�xeles por celda en la textura de la rejilla

    [System.Serializable]
    public class TroopOption
    {
        public string label = "Melee";
        [Tooltip("Prefab de la tropa (del Project, no de la escena).")]
        public GameObject prefab;
        [Tooltip("Tecla que selecciona esta tropa.")]
        public PlacementKey key = PlacementKey.None;
    }

    [System.Serializable]
    public class PlayerSetup
    {
        public string name = "Jugador 1";
        [Tooltip("Color de su campo y de sus celdas disponibles.")]
        public Color color = new Color(0.25f, 0.55f, 1f, 1f);
        [Tooltip("Base del jugador (el objeto de la torre en la escena). Se puede mover dentro de su campo.")]
        public Transform baseTransform;
        [Tooltip("Tecla para mover la base (opcional: tambi�n hay un bot�n en el panel).")]
        public PlacementKey baseKey = PlacementKey.None;
        [Tooltip("Ajuste visual al centrar la base en la celda (por si el pivote del sprite no queda centrado).")]
        public Vector2 baseOffset;
        [Tooltip("Opcional: Tilemap pintado con las celdas de su campo. Si ning�n jugador lo tiene, el mapa se parte en dos.")]
        public Tilemap zone;
        public List<TroopOption> troops = new();

        // Datos en ejecuci�n
        [System.NonSerialized] public Transform targetPoint;   // Punto al que van las tropas ENEMIGAS (la celda de esta base)
        [System.NonSerialized] public Vector2Int baseCell;
        [System.NonSerialized] public bool hasBaseCell;
    }

    private class PlacedTroop
    {
        public int player;
        public int option;
        public GameObject go;
    }

    private enum Phase { Placement, Battle }
    private enum UiAction { None, Start, Reset, Clear }

    [Header("Referencias")]
    [SerializeField] private TerrainGridBaker terrain;
    [SerializeField, Tooltip("Si se deja vac�a se usa la Main Camera.")] private Camera cam;
    [SerializeField, Tooltip("Opcional: objeto padre donde se crean las tropas.")] private Transform troopsParent;

    [Header("Campos de los jugadores")]
    [SerializeField, Tooltip("Solo si no hay Tilemaps de zona: l�nea que parte el mapa en dos. Vac�o = mitad de la rejilla.")]
    private Transform divider;
    [SerializeField, Tooltip("Sin Tilemaps de zona: true = el jugador 1 ocupa la parte izquierda y el 2 la derecha.")]
    private bool player1OnLeft = true;

    [Header("Jugadores y tropas (dise�o: J1 = 1,2,3,4 � J2 = 7,8,9,0)")]
    [SerializeField]
    private List<PlayerSetup> players = new()
    {
        MakePlayer("Jugador 1", new Color(0.25f, 0.55f, 1f, 1f),
            PlacementKey.Digit1, PlacementKey.Digit2, PlacementKey.Digit3, PlacementKey.Digit4),
        MakePlayer("Jugador 2", new Color(0.8f, 0.3f, 1f, 1f),
            PlacementKey.Digit7, PlacementKey.Digit8, PlacementKey.Digit9, PlacementKey.Digit0)
    };

    [Header("Cursor y rejilla")]
    [SerializeField] private Color validColor = new Color(0.2f, 1f, 0.3f, 0.5f);
    [SerializeField] private Color invalidColor = new Color(1f, 0.2f, 0.2f, 0.5f);
    [SerializeField, Tooltip("Orden de dibujo de la rejilla. Si tapa a las tropas, b�jalo.")] private int gridSortingOrder = 20;
    [SerializeField] private int cursorSortingOrder = 100;

    private readonly Dictionary<Vector2Int, PlacedTroop> placed = new();
    private Phase phase = Phase.Placement;
    private UiAction pendingAction = UiAction.None;

    private int activePlayer;
    private int activeOption;   // -1 = mover la base

    private bool initialized;
    private bool overlayDirty = true;
    private Vector2Int gridSize;
    private float cellSize;
    private int[] zoneOwner;    // Por celda: jugador due�o del campo (-1 = de nadie)

    private SpriteRenderer cursor;
    private Texture2D cursorTexture;
    private SpriteRenderer overlay;
    private Texture2D overlayTexture;

    private Rect panelRect;
    private float uiScale = 1f;
    private string status = "";
    private float statusUntil;

    private static PlayerSetup MakePlayer(string playerName, Color color, params PlacementKey[] keys)
    {
        string[] labels = { "Melee", "Tanque", "A distancia", "Healer" };
        var p = new PlayerSetup { name = playerName, color = color };
        for (int i = 0; i < keys.Length; i++)
            p.troops.Add(new TroopOption { label = labels[i], key = keys[i] });
        return p;
    }

    // ------------------------------------------------------------------

    private void Awake()
    {
        if (terrain == null) terrain = FindFirstObjectByType<TerrainGridBaker>();
        if (cam == null) cam = Camera.main;

        if (terrain == null) Debug.LogError("[TroopPlacementManager] Falta el TerrainGridBaker.");
        if (cam == null) Debug.LogError("[TroopPlacementManager] No hay c�mara (asigna una o etiqueta la Main Camera).");
        if (players.Count < 2) Debug.LogWarning("[TroopPlacementManager] Hacen falta 2 jugadores en la lista.");

        foreach (var p in players)
        {
            if (p.baseTransform == null)
                Debug.LogWarning($"[TroopPlacementManager] {p.name} no tiene Base: las tropas enemigas no tendr�n a d�nde ir.");

            foreach (var o in p.troops)
            {
                if (o.prefab == null)
                    Debug.LogWarning($"[TroopPlacementManager] {p.name}: la tropa '{o.label}' no tiene prefab.");
                else if (o.prefab.GetComponent<TroopController>() == null)
                    Debug.LogWarning($"[TroopPlacementManager] {p.name}: el prefab de '{o.label}' no tiene TroopController.");
            }
        }

        CreateCursor();
    }

    private void OnDestroy()
    {
        if (cursor != null) Destroy(cursor.gameObject);
        if (cursorTexture != null) Destroy(cursorTexture);
        if (overlay != null) Destroy(overlay.gameObject);
        if (overlayTexture != null) Destroy(overlayTexture);

        foreach (var p in players)
            if (p.targetPoint != null) Destroy(p.targetPoint.gameObject);
    }

    private void Initialize()
    {
        gridSize = terrain.GridSize;
        cellSize = terrain.CellSize;

        // Due�o del campo de cada celda (no cambia durante la partida)
        zoneOwner = new int[gridSize.x * gridSize.y];
        for (int y = 0; y < gridSize.y; y++)
            for (int x = 0; x < gridSize.x; x++)
                zoneOwner[y * gridSize.x + x] = ComputeZoneOwner(new Vector2Int(x, y));

        // Celda inicial de cada base y punto al que ir�n las tropas enemigas
        foreach (var p in players)
        {
            var go = new GameObject($"Objetivo {p.name}");
            p.targetPoint = go.transform;

            if (p.baseTransform != null)
            {
                go.transform.position = p.baseTransform.position;
                if (terrain.TryWorldToCell(p.baseTransform.position, out Vector2Int c))
                {
                    p.baseCell = c;
                    p.hasBaseCell = true;
                    go.transform.position = terrain.CellToWorld(c);
                }
            }
        }

        CreateOverlay();
        initialized = true;
    }

    private void Update()
    {
        if (terrain == null || cam == null || !terrain.IsReady) return;
        if (!initialized) Initialize();

        if (phase == Phase.Placement) HandleSelectionKeys();

        if (PlacementInput.EnterKey) pendingAction = UiAction.Start;
        if (PlacementInput.ResetKey) pendingAction = UiAction.Reset;
        RunPendingAction();

        if (phase != Phase.Placement)
        {
            cursor.enabled = false;
            overlay.enabled = false;
            return;
        }

        overlay.enabled = true;
        if (overlayDirty) RebuildOverlay();

        // Celda bajo el rat�n (ignorando el panel de la interfaz)
        Vector2 mouse = PlacementInput.MousePosition;
        Vector2Int cell = default;
        bool hasCell = !IsOverUi(mouse) && TryGetCell(mouse, out cell);

        if (!hasCell)
        {
            cursor.enabled = false;
            return;
        }

        bool canPlace = CanPlace(activePlayer, cell, out string reason);
        ShowCursor(cell, canPlace);

        if (PlacementInput.LeftClick)
        {
            if (canPlace)
            {
                if (activeOption < 0) MoveBase(activePlayer, cell);
                else Place(activePlayer, activeOption, cell);
                overlayDirty = true;
            }
            else
            {
                Flash(reason);
            }
        }
        else if (PlacementInput.RightClick)
        {
            Remove(cell);
        }
    }

    // ------------------------------------------------------------------
    //  Selecci�n
    // ------------------------------------------------------------------

    private void HandleSelectionKeys()
    {
        for (int pi = 0; pi < players.Count; pi++)
        {
            var p = players[pi];

            for (int oi = 0; oi < p.troops.Count; oi++)
            {
                PlacementKey key = p.troops[oi].key;
                if (key != PlacementKey.None && PlacementInput.WasPressed(key)) Select(pi, oi);
            }

            if (p.baseKey != PlacementKey.None && PlacementInput.WasPressed(p.baseKey)) Select(pi, -1);
        }
    }

    private void Select(int player, int option)
    {
        activePlayer = player;
        activeOption = option;
        overlayDirty = true;
    }

    // ------------------------------------------------------------------
    //  Campos y celdas
    // ------------------------------------------------------------------

    private int ComputeZoneOwner(Vector2Int cell)
    {
        Vector3 center = terrain.CellToWorld(cell);

        // 1) Tilemaps de zona pintados a mano
        bool anyZone = false;
        for (int i = 0; i < players.Count; i++)
        {
            Tilemap zone = players[i].zone;
            if (zone == null) continue;

            anyZone = true;
            if (zone.HasTile(zone.WorldToCell(center))) return i;
        }
        if (anyZone) return -1;

        // 2) Sin zonas: el mapa se parte en dos por una l�nea vertical
        if (players.Count < 2) return -1;
        float splitX = divider != null
            ? divider.position.x
            : terrain.Origin.x + gridSize.x * cellSize * 0.5f;

        bool isLeft = center.x < splitX;
        return isLeft == player1OnLeft ? 0 : 1;
    }

    private int GetOwner(Vector2Int cell)
    {
        if (zoneOwner == null) return -1;
        if (cell.x < 0 || cell.y < 0 || cell.x >= gridSize.x || cell.y >= gridSize.y) return -1;
        return zoneOwner[cell.y * gridSize.x + cell.x];
    }

    private bool IsOccupied(Vector2Int cell)
    {
        if (placed.TryGetValue(cell, out var troop) && troop.go != null) return true;

        foreach (var p in players)
            if (p.hasBaseCell && p.baseCell == cell) return true;

        return false;
    }

    private bool TryGetCell(Vector2 screen, out Vector2Int cell)
    {
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, Mathf.Abs(cam.transform.position.z)));
        return terrain.TryWorldToCell(world, out cell);
    }

    private bool CanPlace(int player, Vector2Int cell, out string reason)
    {
        reason = "";

        if (player < 0 || player >= players.Count)
        {
            reason = "Elige una tropa con su tecla";
            return false;
        }

        var p = players[player];

        if (activeOption >= 0 && (activeOption >= p.troops.Count || p.troops[activeOption].prefab == null))
        {
            reason = "Esa tropa no tiene prefab";
            return false;
        }
        if (!terrain.IsWalkable(cell))
        {
            reason = "Esa celda no es caminable";
            return false;
        }
        if (GetOwner(cell) != player)
        {
            reason = $"Esa celda no est� en el campo de {p.name}";
            return false;
        }
        if (IsOccupied(cell))
        {
            reason = "Esa celda ya est� ocupada";
            return false;
        }
        return true;
    }

    // ------------------------------------------------------------------
    //  Colocaci�n
    // ------------------------------------------------------------------

    private void Place(int player, int optionIndex, Vector2Int cell)
    {
        var opt = players[player].troops[optionIndex];

        Vector3 pos = terrain.CellToWorld(cell);
        pos.z = opt.prefab.transform.position.z;

        var go = Instantiate(opt.prefab, pos, opt.prefab.transform.rotation, troopsParent);
        go.name = $"{players[player].name} - {opt.label} ({cell.x},{cell.y})";

        var controller = go.GetComponent<TroopController>();
        if (controller != null)
            controller.enabled = false; // No arranca su FSM hasta que empiece el combate

        placed[cell] = new PlacedTroop { player = player, option = optionIndex, go = go };
    }

    private void MoveBase(int player, Vector2Int cell)
    {
        var p = players[player];
        Vector3 center = terrain.CellToWorld(cell);

        if (p.baseTransform != null)
        {
            p.baseTransform.position = new Vector3(center.x + p.baseOffset.x, center.y + p.baseOffset.y, p.baseTransform.position.z);
        }

        p.baseCell = cell;
        p.hasBaseCell = true;
        p.targetPoint.position = center; // Las tropas enemigas ir�n a esta celda
    }

    private void Remove(Vector2Int cell)
    {
        if (!placed.TryGetValue(cell, out var troop)) return;
        if (troop.go != null) Destroy(troop.go);
        placed.Remove(cell);
        overlayDirty = true;
    }

    // ------------------------------------------------------------------
    //  Fases
    // ------------------------------------------------------------------

    private void RunPendingAction()
    {
        var action = pendingAction;
        pendingAction = UiAction.None;

        switch (action)
        {
            case UiAction.Start: StartBattle(); break;
            case UiAction.Reset: ResetToPlacement(); break;
            case UiAction.Clear: ClearAll(); break;
        }
    }

    private void StartBattle()
    {
        if (phase != Phase.Placement) return;

        if (placed.Count == 0)
        {
            Flash("Coloca al menos una tropa");
            return;
        }

        foreach (var troop in placed.Values)
        {
            if (troop.go == null) continue;

            var controller = troop.go.GetComponent<TroopController>();
            if (controller == null) continue;

            // Cada tropa va directa a la base del otro jugador
            int enemy = (troop.player + 1) % players.Count;
            controller.Waypoint = players[enemy].targetPoint;

            controller.enabled = true; // Su Start() lanza el WalkState
        }

        phase = Phase.Battle;
    }

    /// <summary>Borra el combate y vuelve a crear la misma formaci�n, lista para otra prueba.</summary>
    private void ResetToPlacement()
    {
        var layout = new List<(Vector2Int cell, int player, int option)>();

        foreach (var kv in placed)
        {
            layout.Add((kv.Key, kv.Value.player, kv.Value.option));
            if (kv.Value.go != null) Destroy(kv.Value.go);
        }
        placed.Clear();

        foreach (var item in layout)
            Place(item.player, item.option, item.cell);

        phase = Phase.Placement;
        overlayDirty = true;
    }

    private void ClearAll()
    {
        foreach (var troop in placed.Values)
            if (troop.go != null) Destroy(troop.go);

        placed.Clear();
        phase = Phase.Placement;
        overlayDirty = true;
    }

    private void Flash(string message)
    {
        status = message;
        statusUntil = Time.time + 2f;
    }

    // ------------------------------------------------------------------
    //  Cursor y rejilla visibles
    // ------------------------------------------------------------------

    private void CreateCursor()
    {
        cursorTexture = new Texture2D(1, 1) { filterMode = FilterMode.Point };
        cursorTexture.SetPixel(0, 0, Color.white);
        cursorTexture.Apply();

        // 1 p�xel por unidad: el sprite mide 1x1 unidades y se escala al tama�o de celda
        var sprite = Sprite.Create(cursorTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);

        var go = new GameObject("PlacementCursor");
        cursor = go.AddComponent<SpriteRenderer>();
        cursor.sprite = sprite;
        cursor.sortingOrder = cursorSortingOrder;
        cursor.enabled = false;
    }

    private void ShowCursor(Vector2Int cell, bool valid)
    {
        cursor.enabled = true;
        cursor.transform.position = terrain.CellToWorld(cell);
        cursor.transform.localScale = Vector3.one * cellSize * 0.92f;
        cursor.color = valid ? validColor : invalidColor;
    }

    private void CreateOverlay()
    {
        int w = gridSize.x * CellPixels;
        int h = gridSize.y * CellPixels;

        overlayTexture = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        // CellPixels p�xeles por celda: la textura cubre justo la rejilla del baker
        var sprite = Sprite.Create(overlayTexture, new Rect(0, 0, w, h), Vector2.zero, CellPixels / cellSize);

        var go = new GameObject("PlacementGrid");
        go.transform.position = new Vector3(terrain.Origin.x, terrain.Origin.y, 0f);

        overlay = go.AddComponent<SpriteRenderer>();
        overlay.sprite = sprite;
        overlay.sortingOrder = gridSortingOrder;
    }

    /// <summary>
    /// Dibuja el campo de cada jugador con su color (tenue) y, m�s intenso y con borde,
    /// las celdas donde el jugador activo puede colocar ahora.
    /// </summary>
    private void RebuildOverlay()
    {
        overlayDirty = false;

        int w = gridSize.x * CellPixels;
        int h = gridSize.y * CellPixels;
        var pixels = new Color32[w * h]; // Transparente por defecto

        for (int cy = 0; cy < gridSize.y; cy++)
        {
            for (int cx = 0; cx < gridSize.x; cx++)
            {
                var cell = new Vector2Int(cx, cy);
                int owner = zoneOwner[cy * gridSize.x + cx];
                if (owner < 0 || owner >= players.Count || !terrain.IsWalkable(cell)) continue;

                bool highlighted = owner == activePlayer && !IsOccupied(cell);
                Color c = players[owner].color;
                byte r = ToByte(c.r), g = ToByte(c.g), b = ToByte(c.b);
                byte fill = ToByte(highlighted ? 0.35f : 0.10f);
                byte edge = ToByte(highlighted ? 0.85f : 0.22f);

                for (int py = 0; py < CellPixels; py++)
                {
                    for (int px = 0; px < CellPixels; px++)
                    {
                        bool isEdge = px == 0 || py == 0 || px == CellPixels - 1 || py == CellPixels - 1;
                        pixels[(cy * CellPixels + py) * w + cx * CellPixels + px] =
                            new Color32(r, g, b, isEdge ? edge : fill);
                    }
                }
            }
        }

        overlayTexture.SetPixels32(pixels);
        overlayTexture.Apply(false);
    }

    private static byte ToByte(float value)
    {
        return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
    }

    // ------------------------------------------------------------------
    //  Interfaz (IMGUI: no necesita Canvas ni EventSystem)
    // ------------------------------------------------------------------

    private bool IsOverUi(Vector2 screen)
    {
        Vector2 gui = new Vector2(screen.x / uiScale, (Screen.height - screen.y) / uiScale);
        return panelRect.Contains(gui);
    }

    private static string KeyLabel(PlacementKey key)
    {
        return key == PlacementKey.None ? "-" : key.ToString().Replace("Digit", "");
    }

    private void OnGUI()
    {
        Matrix4x4 previousMatrix = GUI.matrix;
        uiScale = Mathf.Max(0.5f, Screen.height / 1080f); // Legible tambi�n en pantallas 4K
        GUI.matrix = Matrix4x4.Scale(new Vector3(uiScale, uiScale, 1f));

        bool placing = phase == Phase.Placement;
        float height = placing ? 130f + 36f * players.Count : 104f;
        panelRect = new Rect(10f, 10f, 980f, height);

        string statusText = Time.time < statusUntil ? status : "";

        GUILayout.BeginArea(panelRect, GUI.skin.box);

        GUILayout.Label(placing
            ? "COLOCACI�N � tecla = elegir tropa � clic izquierdo = colocar en tu campo � clic derecho = quitar"
            : "COMBATE EN CURSO � R = volver a colocar con la misma formaci�n");

        if (placing)
        {
            for (int pi = 0; pi < players.Count; pi++)
            {
                var p = players[pi];

                GUILayout.BeginHorizontal();
                GUILayout.Label(p.name, GUILayout.Width(110f));

                for (int oi = 0; oi < p.troops.Count; oi++)
                {
                    bool on = activePlayer == pi && activeOption == oi;
                    string label = $"{KeyLabel(p.troops[oi].key)} � {p.troops[oi].label}";
                    if (GUILayout.Toggle(on, label, GUI.skin.button) && !on) Select(pi, oi);
                }

                bool baseOn = activePlayer == pi && activeOption == -1;
                string baseLabel = p.baseKey == PlacementKey.None ? "Mover base" : $"{KeyLabel(p.baseKey)} � Mover base";
                if (GUILayout.Toggle(baseOn, baseLabel, GUI.skin.button) && !baseOn) Select(pi, -1);

                GUILayout.EndHorizontal();
            }
        }

        GUILayout.BeginHorizontal();
        GUI.enabled = placing;
        if (GUILayout.Button("Empezar combate (Enter)")) pendingAction = UiAction.Start;
        GUI.enabled = true;
        if (GUILayout.Button("Reiniciar misma formaci�n (R)")) pendingAction = UiAction.Reset;
        if (GUILayout.Button("Vaciar")) pendingAction = UiAction.Clear;
        GUILayout.EndHorizontal();

        GUILayout.Label($"Tropas colocadas: {placed.Count}    {statusText}");

        GUILayout.EndArea();

        GUI.matrix = previousMatrix;
    }
}

/// <summary>Rat�n y teclado que funcionan con el Input Manager antiguo y con el nuevo Input System.</summary>
internal static class PlacementInput
{
#if ENABLE_LEGACY_INPUT_MANAGER
    private static readonly Dictionary<PlacementKey, KeyCode> cache = new();

    private static KeyCode Resolve(PlacementKey key)
    {
        if (!cache.TryGetValue(key, out KeyCode code))
        {
            // En el Input Manager antiguo los d�gitos se llaman Alpha0..Alpha9
            string name = key.ToString().Replace("Digit", "Alpha");
            if (!System.Enum.TryParse<KeyCode>(name, out code)) code = KeyCode.None;
            cache[key] = code;
        }
        return code;
    }

    public static Vector2 MousePosition => Input.mousePosition;
    public static bool LeftClick => Input.GetMouseButtonDown(0);
    public static bool RightClick => Input.GetMouseButtonDown(1);
    public static bool EnterKey => Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    public static bool ResetKey => Input.GetKeyDown(KeyCode.R);

    public static bool WasPressed(PlacementKey key)
    {
        KeyCode code = Resolve(key);
        return code != KeyCode.None && Input.GetKeyDown(code);
    }
#elif ENABLE_INPUT_SYSTEM
    private static readonly Dictionary<PlacementKey, Key> cache = new();

    private static Key Resolve(PlacementKey key)
    {
        if (!cache.TryGetValue(key, out Key code))
        {
            if (!System.Enum.TryParse<Key>(key.ToString(), out code)) code = Key.None;
            cache[key] = code;
        }
        return code;
    }

    public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
    public static bool LeftClick => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public static bool RightClick => Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

    public static bool EnterKey =>
        Keyboard.current != null && (Keyboard.current.enterKey.wasPressedThisFrame || Keyboard.current.numpadEnterKey.wasPressedThisFrame);

    public static bool ResetKey => Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame;

    public static bool WasPressed(PlacementKey key)
    {
        Key code = Resolve(key);
        return code != Key.None && Keyboard.current != null && Keyboard.current[code].wasPressedThisFrame;
    }
#else
    public static Vector2 MousePosition => Vector2.zero;
    public static bool LeftClick => false;
    public static bool RightClick => false;
    public static bool EnterKey => false;
    public static bool ResetKey => false;
    public static bool WasPressed(PlacementKey key) => false;
#endif
}