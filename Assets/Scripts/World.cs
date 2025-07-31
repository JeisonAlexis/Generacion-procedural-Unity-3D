using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;


public class World : MonoBehaviour
{
    public static World actual;
    public int semilla;
    public int anchoChunk = 16;
    public int alturaChunk = 50;
    public float rangoVista = 5;
    public GameObject prefabChunk;

    private List<Chunk> chunks;
    private Transform jugador;
    private float tiempo;

    private Queue<Vector3Int> posicionesPorCrear = new Queue<Vector3Int>();
    private HashSet<Vector3Int> posicionesYaEncoladas = new HashSet<Vector3Int>();
    private Coroutine generadorChunksCoroutine;
    private Dictionary<Vector3Int, byte[,,]> chunksGuardados = new Dictionary<Vector3Int, byte[,,]>();

    void Awake()
    {
        World.actual = this;
    }

    void Start()
    {


        if (semilla == 0)
            semilla = Random.Range(0, int.MaxValue);

        chunks = new List<Chunk>();
        tiempo = 1;
        jugador = GameObject.FindGameObjectWithTag("Player").transform;
        jugador.position = new Vector3(0, alturaChunk + 10, 0);

        CrearChunksInmediatos();
        generadorChunksCoroutine = StartCoroutine(ProcesarColaDeChunks());

    }

    IEnumerator ProcesarColaDeChunks()
    {
        while (true)
        {
            if (posicionesPorCrear.Count > 0)
            {
                Vector3Int posicion = posicionesPorCrear.Dequeue();
                posicionesYaEncoladas.Remove(posicion);

                if (BuscarChunk(posicion) == null)
                {
                    GameObject chunkObj = Instantiate(prefabChunk, posicion, Quaternion.identity);
                    Chunk chunk = chunkObj.GetComponent<Chunk>();

                    byte[,,] bloques = chunksGuardados.ContainsKey(posicion)
    ? chunksGuardados[posicion]
    : CargarChunkDeDisco(posicion);

                    if (bloques != null)
                    {
                        chunk.ImportarBloques(bloques);
                        chunk.GenerarMallaVisualAsync();
                    }



                    AgregarChunk(chunk);
                }

                yield return null;
            }
            else
            {
                yield return null;
            }
        }
    }

    private void EliminarChunksLejanos()
    {
        float maxDistXZ = rangoVista * anchoChunk + anchoChunk;
        Vector2 pj = new Vector2(jugador.position.x, jugador.position.z);

        for (int i = chunks.Count - 1; i >= 0; i--)
        {
            Chunk c = chunks[i];
            Vector2 pc = new Vector2(c.transform.position.x, c.transform.position.z);
            if (Vector2.Distance(pj, pc) > maxDistXZ)
            {
                // usa el mismo floor-to-multiple:
                Vector3Int posChunk = new Vector3Int(
                    Mathf.FloorToInt(c.transform.position.x / anchoChunk) * anchoChunk,
                    0,
                    Mathf.FloorToInt(c.transform.position.z / anchoChunk) * anchoChunk
                );

                byte[,,] datos = c.ExportarBloques();
                chunksGuardados[posChunk] = datos;
                GuardarChunkEnDisco(posChunk, datos);

                Destroy(c.gameObject);
                chunks.RemoveAt(i);
            }
        }
    }




    private void CrearChunksInmediatos()
    {
        float distanciaMaxima = rangoVista * anchoChunk;
        Vector3 posJugador = jugador.position;

        for (float x = posJugador.x - distanciaMaxima; x < posJugador.x + distanciaMaxima; x += anchoChunk)
        {
            for (float z = posJugador.z - distanciaMaxima; z < posJugador.z + distanciaMaxima; z += anchoChunk)
            {
                Vector3Int posicion = new Vector3Int(
                    Mathf.FloorToInt(x / anchoChunk) * anchoChunk,
                    0,
                    Mathf.FloorToInt(z / anchoChunk) * anchoChunk
                );

                if (BuscarChunk(posicion) == null)
                {
                    GameObject chunkObj = Instantiate(prefabChunk, posicion, Quaternion.identity);
                    Chunk chunk = chunkObj.GetComponent<Chunk>();

                    byte[,,] bloques = chunksGuardados.ContainsKey(posicion)
    ? chunksGuardados[posicion]
    : CargarChunkDeDisco(posicion);

                    if (bloques != null)
                    {
                        chunk.ImportarBloques(bloques);
                        chunk.GenerarMallaVisualAsync();
                    }


                    AgregarChunk(chunk);
                }
            }
        }
    }

    void Update()
    {
        tiempo += Time.deltaTime;

        if (tiempo > 1f)
        {
            tiempo = 0f;
            EncolarChunksParaCrear();
        }

        EliminarChunksLejanos();

    }

    private void EncolarChunksParaCrear()
    {
        float maxXZ = rangoVista * anchoChunk;
        Vector2 posJugXZ = new Vector2(jugador.position.x, jugador.position.z);

        for (float x = jugador.position.x - maxXZ; x < jugador.position.x + maxXZ; x += anchoChunk)
            for (float z = jugador.position.z - maxXZ; z < jugador.position.z + maxXZ; z += anchoChunk)
            {
                Vector3Int pos = new Vector3Int(
                    Mathf.FloorToInt(x / anchoChunk) * anchoChunk,
                    0,
                    Mathf.FloorToInt(z / anchoChunk) * anchoChunk
                );

                // Sólo encola si no está ya instanciado y dentro del rango XZ
                Vector2 posXZ = new Vector2(pos.x, pos.z);
                if (BuscarChunk(pos) == null
                    && !posicionesYaEncoladas.Contains(pos)
                    && Vector2.Distance(posXZ, posJugXZ) <= maxXZ)
                {
                    posicionesPorCrear.Enqueue(pos);
                    posicionesYaEncoladas.Add(pos);
                }
            }
    }


    public void AgregarChunk(Chunk chunk)
    {
        chunks.Add(chunk);
    }

    public Chunk BuscarChunk(Vector3Int posicion)
    {
        foreach (Chunk c in chunks)
        {
            Vector3 pos = c.transform.position;
            if (pos.x <= posicion.x && pos.z <= posicion.z &&
                pos.x + anchoChunk > posicion.x && pos.z + anchoChunk > posicion.z)
            {
                return c;
            }
        }
        return null;
    }

    public void ActualizarChunkEn(Vector3Int posGlobal)
    {
        Vector3Int posChunk = new Vector3Int(
            Mathf.FloorToInt(posGlobal.x / (float)anchoChunk) * anchoChunk,
            0,
            Mathf.FloorToInt(posGlobal.z / (float)anchoChunk) * anchoChunk
        );

        Chunk chunk = BuscarChunk(posChunk);
        if (chunk != null)
        {
            chunk.GenerarMallaVisualAsync();
        }
    }

    public void SetBlockGlobal(Vector3Int posGlobal, byte block)
    {
        // 1) Calcula la posición de origen del chunk
        Vector3Int posChunk = new Vector3Int(
            Mathf.FloorToInt(posGlobal.x / (float)anchoChunk) * anchoChunk,
            0,
            Mathf.FloorToInt(posGlobal.z / (float)anchoChunk) * anchoChunk
        );

        // 2) Busca el chunk cargado en memoria
        Chunk chunk = BuscarChunk(posChunk);
        if (chunk == null)
        {
            Debug.LogWarning($"SetBlockGlobal: no encontré chunk en {posChunk} para modificar {posGlobal}");
            return;
        }

        // 3) Convierte la posición global a coordenadas locales dentro del chunk
        Vector3Int posLocal = new Vector3Int(
            posGlobal.x - posChunk.x,
            posGlobal.y,
            posGlobal.z - posChunk.z
        );

        // 4) Aplica el cambio y actualiza la malla
        chunk.SetBlock(posLocal, block);
        chunk.GenerarMallaVisualAsync();

        // 5) Exporta el array actualizado y guarda en memoria y en disco
        byte[,,] datos = chunk.ExportarBloques();
        chunksGuardados[posChunk] = datos;
        GuardarChunkEnDisco(posChunk, datos);

        // 6) Opcional: log para depurar
        Debug.Log($"[MOD] posGlobal={posGlobal}, posChunk={posChunk}, posLocal={posLocal}, block={block}");

        // 7) Actualiza vecinos si el bloque modificado está en un borde
        Vector3Int left = new Vector3Int(-1, 0, 0);
        Vector3Int right = new Vector3Int(1, 0, 0);
        Vector3Int back = new Vector3Int(0, 0, -1);
        Vector3Int forward = new Vector3Int(0, 0, 1);
        Vector3Int down = new Vector3Int(0, -1, 0);
        Vector3Int up = new Vector3Int(0, 1, 0);

        if (posLocal.x == 0) ActualizarChunkEn(posGlobal + left);
        else if (posLocal.x == anchoChunk - 1) ActualizarChunkEn(posGlobal + right);

        if (posLocal.z == 0) ActualizarChunkEn(posGlobal + back);
        else if (posLocal.z == anchoChunk - 1) ActualizarChunkEn(posGlobal + forward);

        if (posLocal.y == 0) ActualizarChunkEn(posGlobal + down);
        else if (posLocal.y == alturaChunk - 1) ActualizarChunkEn(posGlobal + up);
    }





    private string ObtenerRutaChunk(Vector3Int posicion)
    {
        string path = Path.Combine(Application.persistentDataPath, "chunks");
        Directory.CreateDirectory(path);
        return Path.Combine(path, $"{posicion.x}_{posicion.z}.bin");
    }

    private void GuardarChunkEnDisco(Vector3Int posicion, byte[,,] bloques)
    {
        string ruta = ObtenerRutaChunk(posicion);
        //Debug.Log($"Guardando chunk en disco: {ruta}");
        using (BinaryWriter writer = new BinaryWriter(File.Open(ruta, FileMode.Create)))
        {
            for (int x = 0; x < anchoChunk; x++)
                for (int y = 0; y < alturaChunk; y++)
                    for (int z = 0; z < anchoChunk; z++)
                        writer.Write(bloques[x, y, z]);
        }

    }

    private byte[,,] CargarChunkDeDisco(Vector3Int posicion)
    {
        string ruta = ObtenerRutaChunk(posicion);
        if (!File.Exists(ruta))
            return null;

        byte[,,] bloques = new byte[anchoChunk, alturaChunk, anchoChunk];
        using (BinaryReader reader = new BinaryReader(File.Open(ruta, FileMode.Open)))
        {
            for (int x = 0; x < anchoChunk; x++)
                for (int y = 0; y < alturaChunk; y++)
                    for (int z = 0; z < anchoChunk; z++)
                        bloques[x, y, z] = reader.ReadByte();
        }
        return bloques;
    }

    
   






}
