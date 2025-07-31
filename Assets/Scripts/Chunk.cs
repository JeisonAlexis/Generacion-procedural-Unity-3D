using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SimplexNoise;


[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]

public class Chunk : MonoBehaviour
{

    private byte[,,] blocks;
    private MeshFilter meshFilter;
    private MeshCollider meshCollider;
    private List<Vector3> posicionCasas = new List<Vector3>();
    public Chunk vecinoIzquierda;
    public Chunk vecinoDerecha;
    public Chunk vecinoArriba;
    public Chunk vecinoAbajo;
    public Chunk vecinoFrente;
    public Chunk vecinoAtras;
    private bool mallaGenerada = false;
    private bool datosImportados = false;


    void Start()
    {
        World.actual.AgregarChunk(this);
        meshFilter = GetComponent<MeshFilter>();
        meshCollider = GetComponent<MeshCollider>();

        // ② Solo genera procedural si NO venimos de ImportarBloques:
        if (!datosImportados)
        {
            blocks = new byte[World.actual.anchoChunk, World.actual.alturaChunk, World.actual.anchoChunk];
            GenerarEstructura();
            GenerarArboles();
            // La malla la generas más tarde cuando tengan vecinos listos
        }
        // Si datosImportados == true, ya habrá un ImportarBloques() + GenerarMallaVisualAsync()
    }



    void Update()
    {
        if (!mallaGenerada && VecinosListos())
        {
            AsignarVecinos();
            GenerarMallaVisualAsync(); // En lugar de GenerarMallaVisual()
            mallaGenerada = true;
        }

    }

    public void GenerarMallaVisualAsync()
    {
        StartCoroutine(GenerarMallaCR());
    }

    public byte[,,] ExportarBloques()
    {
        //Debug.Log($"[Chunk] Exportando bloque en ({transform.position}) — valor en [0,0,0] = {blocks[0,0,0]}");

        return (byte[,,])this.blocks.Clone();
    }

    public void ImportarBloques(byte[,,] datos)
    {
        // ③ Asigna y marca que vinimos cargados
        this.blocks = (byte[,,])datos.Clone();
        datosImportados = true;

        // Ahora sí genera la malla visual de una vez
        GenerarMallaVisualAsync();
    }


    private IEnumerator GenerarMallaCR()
    {
        // mismos temporales
        List<Vector3> verts = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();


        int ancho = World.actual.anchoChunk;
        int alto = World.actual.alturaChunk;

        for (int x = 0; x < ancho; x++)
        {
            for (int y = 0; y < alto; y++)
            {
                for (int z = 0; z < ancho; z++)
                {
                    if (this.blocks[x, y, z] == 0) continue;

                    // caras visibles
                    if (this.EsTransparente(x - 1, y, z))
                        CrearCara(this.blocks[x, y, z], new Vector3(x, y, z), Vector3.up, Vector3.forward, true, verts, uvs, tris);
                    if (this.EsTransparente(x + 1, y, z))
                        CrearCara(this.blocks[x, y, z], new Vector3(x + 1, y, z), Vector3.up, Vector3.forward, false, verts, uvs, tris);
                    if (this.EsTransparente(x, y + 1, z))
                        CrearCara(this.blocks[x, y, z], new Vector3(x, y + 1, z), Vector3.forward, Vector3.right, false, verts, uvs, tris);
                    if (this.EsTransparente(x, y - 1, z) && y > 0)
                        CrearCara(this.blocks[x, y, z], new Vector3(x, y, z), Vector3.forward, Vector3.right, true, verts, uvs, tris);
                    if (this.EsTransparente(x, y, z - 1))
                        CrearCara(this.blocks[x, y, z], new Vector3(x, y, z), Vector3.up, Vector3.right, false, verts, uvs, tris);
                    if (this.EsTransparente(x, y, z + 1))
                        CrearCara(this.blocks[x, y, z], new Vector3(x, y, z + 1), Vector3.up, Vector3.right, true, verts, uvs, tris);
                }
            }

            // Cada X columnas, cede el control un frame
            if (x % 4 == 0)
                yield return null;
        }

        Mesh mesh = new Mesh
        {
            name = "Chunk",
            vertices = verts.ToArray(),
            uv = uvs.ToArray(),
            triangles = tris.ToArray()
        };
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = null;
        meshCollider.sharedMesh = mesh;
    }



    bool VecinosListos()
    {
        Vector3Int pos = Vector3Int.RoundToInt(transform.position);
        int ancho = World.actual.anchoChunk;

        return World.actual.BuscarChunk(pos + Vector3Int.left * ancho) != null &&
               World.actual.BuscarChunk(pos + Vector3Int.right * ancho) != null &&
               World.actual.BuscarChunk(pos + new Vector3Int(0, 0, 1) * ancho) != null &&
               World.actual.BuscarChunk(pos + new Vector3Int(0, 0, -1) * ancho) != null;
    }

    public void AsignarVecinos()
    {
        Vector3Int pos = Vector3Int.RoundToInt(transform.position);
        int ancho = World.actual.anchoChunk;

        vecinoIzquierda = World.actual.BuscarChunk(pos + Vector3Int.left * ancho);
        vecinoDerecha = World.actual.BuscarChunk(pos + Vector3Int.right * ancho);
        vecinoFrente = World.actual.BuscarChunk(pos + new Vector3Int(0, 0, -1) * ancho);
        vecinoAtras = World.actual.BuscarChunk(pos + new Vector3Int(0, 0, 1) * ancho);
    }


    void GenerarArboles()
    {

        //pasar coordenadas locales a globales
        int chunkX = (int)(transform.position.x / World.actual.anchoChunk);
        int chunkZ = (int)(transform.position.z / World.actual.anchoChunk);

        int distanciaMinima = 8; // Distancia mínima entre árboles y casas (en bloques)

        for (int x = 3; x < World.actual.anchoChunk - 3; x++)
        {
            for (int z = 3; z < World.actual.anchoChunk - 3; z++) //se inicia y se resta 3 porque (no queremos generar arboles cerca de los bordes de un chunk ya que se cortan
            {
                for (int y = World.actual.alturaChunk - 1; y > 0; y--) //se busca en y el bloque mas arriba (luego se comprueba si es pasto o no para poner un arbol
                {
                    if (this.blocks[x, y, z] != 2) continue; // Si no hay pasto, sigue buscando.

                    if (y + 1 >= World.actual.alturaChunk) break; // si nos salimos de la altuma maxima nos salimos de este ciclo (porque ya no hay bloques que revisar)

                    // Convertir la posición del árbol a coordenadas globales (una cosa son las coordenadas dentro del chunk y otras las globales OJO)
                    Vector2 treePos = new Vector2(
                        x + chunkX * World.actual.anchoChunk,
                        z + chunkZ * World.actual.anchoChunk
                    );

                    //Evitar generar árboles cerca de casas (porque puede afectar la estructura interna de la casa)
                    bool cercaDeCasa = false;
                    foreach (Vector3 casaPos in posicionCasas)
                    {
                        float distance = Vector2.Distance(treePos, new Vector2(casaPos.x, casaPos.z));
                        if (distance < distanciaMinima)
                        {
                            cercaDeCasa = true;
                            break;
                        }
                    }

                    if (cercaDeCasa) continue; // Si está demasiado cerca de una casa, salta este árbol

                    bool plantarArbol = true; // si pasó todas las pruebas anteriores significa que el arbol se puede plantar (solo queda revisar que no se salga en y)

                    for (int dx = -8; dx <= 8 && plantarArbol; dx++)
                    {
                        for (int dz = -8; dz <= 8 && plantarArbol; dz++)
                        {
                            int checkX = x + dx, checkZ = z + dz;

                            if (checkX < 0 || checkX >= World.actual.anchoChunk ||
                                checkZ < 0 || checkZ >= World.actual.anchoChunk) continue; //Evitamos salirnos de los limites x z del chunk

                            for (int dy = 0; dy < 4; dy++)
                            {
                                int checkY = y + dy;
                                if (checkY >= World.actual.alturaChunk) break; //Evitamos salirnos de los limites y del chunk 

                                byte block = this.blocks[checkX, checkY, checkZ];
                                if (block == 3 || block == 9 || block == 11 || block == 14) //evita que parezcan arboles en la misma posicion
                                {
                                    plantarArbol = false;
                                    break;
                                }
                            }
                        }
                    }

                    if (!plantarArbol) break;

                    int hash = HashCoherente(chunkX, chunkZ);

                    int semillaLocal = World.actual.semilla + chunkX * 73856093 + chunkZ * 19349663;
                    System.Random rng = new System.Random(semillaLocal);
                    int tipoDeArbol = rng.Next(0, 4);


                    if (tipoDeArbol == 0)
                        ArbolNormal(x, y + 1, z);
                    else if (tipoDeArbol == 1)
                        ArbolSakura(x, y + 1, z);
                    else if (tipoDeArbol == 2)
                        ArbolRedondo(x, y + 1, z);
                    else if (tipoDeArbol == 3)
                        ArbolesLargos(x, y + 1, z);


                    break; // si ya plantamos nos salimos del bucle
                }
            }
        }
    }


    void ArbolNormal(int x, int y, int z)
    {
        int alturatronco = Random.Range(4, 6); //una altura de tronco variable
        int alturaMaxima = Mathf.Min(y + alturatronco, World.actual.alturaChunk - 1); //me ayuda a controlar que la altura del arbol no exceda la del chunk

        // Generar tronco
        for (int i = 0; i < alturatronco; i++)
        {
            int actualY = y + i;
            if (actualY < World.actual.alturaChunk)
            {
                this.blocks[x, actualY, z] = 3; // Tronco
            }
        }

        // Generar hojas sin esquinas
        int inicioHojas = alturaMaxima - 2;
        for (int lx = -2; lx <= 2; lx++)
        {
            for (int lz = -2; lz <= 2; lz++)
            {
                if (Mathf.Abs(lx) == 2 && Mathf.Abs(lz) == 2) continue; // Omite las esquinas en las hojas para que no quede cuadrado el arbol

                for (int ly = 0; ly < 3; ly++)
                {
                    int hojaX = x + lx;
                    int hojaY = inicioHojas + ly;
                    int hojaZ = z + lz;

                    if (hojaX >= 0 && hojaX < World.actual.anchoChunk &&
                        hojaY >= 0 && hojaY < World.actual.alturaChunk &&
                        hojaZ >= 0 && hojaZ < World.actual.anchoChunk)
                    {
                        this.blocks[hojaX, hojaY, hojaZ] = 4; // Hojas
                    }
                }
            }
        }
    }


    void ArbolesLargos(int x, int y, int z)
    {
        int trunkHeight = Random.Range(7, 10); // Árbol un poco más alto
        int maxTreeHeight = Mathf.Min(y + trunkHeight, World.actual.alturaChunk - 1);

        for (int i = 0; i < trunkHeight; i++)
        {
            int currentY = y + i;
            if (currentY < World.actual.alturaChunk)
            {
                this.blocks[x, currentY, z] = 9; // Tronco de arbol grande
            }
        }

        int leafStart = maxTreeHeight - 3;
        for (int lx = -3; lx <= 3; lx++)
        {
            for (int lz = -3; lz <= 3; lz++)
            {
                for (int ly = 0; ly < 4; ly++) // Capa superior con más hojas
                {
                    int leafX = x + lx;
                    int leafY = leafStart + ly;
                    int leafZ = z + lz;

                    // Forma "cupula" o "escalera" (segun se mire) para las hojas
                    if (Mathf.Abs(lx) + Mathf.Abs(lz) + ly < 5)
                    {
                        if (leafX >= 0 && leafX < World.actual.anchoChunk &&
                            leafY >= 0 && leafY < World.actual.alturaChunk &&
                            leafZ >= 0 && leafZ < World.actual.anchoChunk)
                        {
                            this.blocks[leafX, leafY, leafZ] = 10; // Hojas grandes
                        }
                    }
                }
            }
        }
    }


    void ArbolRedondo(int x, int y, int z)
    {
        int trunkHeight = Random.Range(6, 9); // Árbol con tronco un poco mas bajo que el anterior
        int maxTreeHeight = Mathf.Min(y + trunkHeight, World.actual.alturaChunk - 1);

        for (int i = 0; i < trunkHeight; i++)
        {
            int currentY = y + i;
            if (currentY < World.actual.alturaChunk)
            {
                this.blocks[x, currentY, z] = 11; // Tronco de tipo 11
            }
        }

        int leafStart = maxTreeHeight - 3;
        for (int lx = -3; lx <= 3; lx++)
        {
            for (int lz = -3; lz <= 3; lz++)
            {
                for (int ly = 0; ly < 4; ly++) // Capa superior con más hojas
                {
                    int leafX = x + lx;
                    int leafY = leafStart + ly;
                    int leafZ = z + lz;

                    // Genera una forma redonda usando distancia euclidiana
                    float distance = Mathf.Sqrt(lx * lx + lz * lz + ly * ly);
                    if (distance < 3.5f) // entre mas alto el valor mas redondo
                    {
                        if (leafX >= 0 && leafX < World.actual.anchoChunk && leafY >= 0 && leafY < World.actual.alturaChunk && leafZ >= 0 && leafZ < World.actual.anchoChunk)
                        {
                            this.blocks[leafX, leafY, leafZ] = 12; // Hojas del redondo (bloque 12)
                        }
                    }
                }
            }
        }
    }


    void ArbolSakura(int x, int y, int z)
    {
        int trunkHeight = 4; // Altura fija de 4 bloques
        int maxTreeHeight = Mathf.Min(y + trunkHeight, World.actual.alturaChunk - 1);

        // Generar tronco
        for (int i = 0; i < trunkHeight; i++)
        {
            int currentY = y + i;
            if (currentY < World.actual.alturaChunk)
            {
                this.blocks[x, currentY, z] = 14; // Tronco de Sakura
            }
        }

        int leafStart = maxTreeHeight - 1;

        // Generar hojas con forma de sakura
        for (int lx = -3; lx <= 3; lx++)
        {
            for (int lz = -3; lz <= 3; lz++)
            {
                for (int ly = 0; ly < 3; ly++) // Capa superior con más hojas
                {
                    int leafX = x + lx;
                    int leafY = leafStart + ly;
                    int leafZ = z + lz;

                    // Forma más redonda y con ramas sobresaliendo
                    float distance = Mathf.Sqrt(lx * lx + lz * lz);
                    if (distance < 2.8f || (distance < 3.5f && Random.Range(0, 4) == 0)) //usamos el concepto anterior para hacerlo redondo pero ponemos bloques random por ahi
                    {
                        if (leafX >= 0 && leafX < World.actual.anchoChunk && leafY >= 0 && leafY < World.actual.alturaChunk && leafZ >= 0 && leafZ < World.actual.anchoChunk)
                        {
                            this.blocks[leafX, leafY, leafZ] = 15; // Hojas de Sakura
                        }
                    }
                }
            }
        }
    }




    void GenerarEstructura()
    {
        float noiseValue;
        float stoneValue;
        Vector3 noisePos = new Vector3();

        // Establecer la semilla para que los chunks tengan coherencia entre si
        Random.InitState(World.actual.semilla);

        // Generar un offset aleatorio con valores más dispersos
        Vector3 offset = new Vector3(
            Random.Range(-5000f, 5000f),
            Random.Range(-5000f, 5000f),
            Random.Range(-5000f, 5000f)
        );

        Vector3 chunkOffset = new Vector3(transform.position.x, transform.position.y, transform.position.z);

        for (int x = 0; x < World.actual.anchoChunk; x++)
        {
            for (int z = 0; z < World.actual.anchoChunk; z++) //recorremos las dimensiones en z y x del chunk
            {
                this.blocks[x, 0, z] = 5; // Capa más baja de bloque 5 (para mi es como la bedrock por asi decirlo)

                float tipoTerreno = this.GenerarRuido(new Vector3(x, 0, z) + chunkOffset, offset, 50f);
                bool montaña = tipoTerreno > 0.35f;

                float heightNoise = this.GenerarRuido(new Vector3(x, 0, z) + chunkOffset, offset, montaña ? 30f : 100f);
                //montaña es true entonces devuelve 30f sies false devuelve 100f (basicamente para controlar el ruido de perlin 
                int terrainHeight = Mathf.FloorToInt((heightNoise + 1) * (World.actual.alturaChunk - 10) * 0.4f + (montaña ? 10 : 0));
                //0.4 reduciendo la escala de la variación para que el terreno no sea demasiado alto.

                for (int y = 1; y < terrainHeight; y++)
                {
                    noisePos.Set(x + transform.position.x, y + transform.position.y, z + transform.position.z); //pasamos las coordenadas locales del chunk a globales

                    stoneValue = this.GenerarRuido(noisePos, offset, 20f); //con esto generaremos variaciones de piedra
                    float tmp = (5 - y) / 15f;
                    stoneValue += tmp;

                    noiseValue = this.GenerarRuido(noisePos, offset, 50f); //con esto generaremos variaciones de piedra
                    tmp = (50 - y) / 50f;
                    noiseValue += tmp;


                    

                    //tmp ajusta los valores de ruido según la altura favorece la piedra en la parte baja y la tierra en otras capa

                    noiseValue += stoneValue; //hacemos que los ruidos se junten para que la tierra y la piedra se lleguen a combinar

                    if (stoneValue > 0.6f)
                    {
                        this.blocks[x, y, z] = 6; // Piedra
                    }
                    else if (noiseValue > 0.2f)
                    {
                        this.blocks[x, y, z] = 1; // Tierra
                    }
                    else
                    {
                        this.blocks[x, y, z] = 0; // Aire
                    }


                }
            }
        }

        // **Colocar pasto en la superficie**
        for (int x = 0; x < World.actual.anchoChunk; x++)
        {
            for (int z = 0; z < World.actual.anchoChunk; z++)
            {
                for (int y = World.actual.alturaChunk - 1; y > 0; y--)
                {
                    if (this.blocks[x, y, z] == 1)
                    {
                        if (this.blocks[x, y + 1, z] == 0)
                        {
                            this.blocks[x, y, z] = 2; // Convertir en pasto
                        }
                    }
                }
            }
        }

    }
    



    float GenerarRuido(Vector3 position, Vector3 offset, float escala)
    {



        float nX = (position.x + offset.x) / escala;
        float nY = (position.y + offset.y) / escala;
        float nZ = (position.z + offset.z) / escala;

        //la división por la escala controla cuán "estirado" o "comprimido" se ve el patrón de ruido en el espacio.

        return Noise.Generate(nX, nY, nZ);

    }

    public void SetBlock(Vector3 pos, byte block)
    {

        if (!((pos.x < 0) || (pos.y < 0) || (pos.z < 0) || (pos.x >= World.actual.anchoChunk) || (pos.y >= World.actual.alturaChunk) || (pos.z >= World.actual.anchoChunk)))
        {

            this.blocks[((int)pos.x), ((int)pos.y), ((int)pos.z)] = block;

        }
    }

    public byte GetBlock(int x, int y, int z)
    {

        if ((x < 0) || (y < 0) || (z < 0) || (x >= World.actual.anchoChunk) || (y >= World.actual.alturaChunk) || (z >= World.actual.anchoChunk))
        {

            return 0;

        }
        else
        {

            return this.blocks[x, y, z];
        }
    }

    public byte GetBlockGlobal(int x, int y, int z)
    {
        int ancho = World.actual.anchoChunk;
        int alto = World.actual.alturaChunk;

        if (x >= 0 && x < ancho && y >= 0 && y < alto && z >= 0 && z < ancho)
        {
            return this.blocks[x, y, z];
        }

        // Verificar qué vecino consultar
        if (x < 0 && vecinoIzquierda != null)
            return vecinoIzquierda.GetBlock(x + ancho, y, z);
        if (x >= ancho && vecinoDerecha != null)
            return vecinoDerecha.GetBlock(x - ancho, y, z);

        if (z < 0 && vecinoFrente != null)
            return vecinoFrente.GetBlock(x, y, z + ancho);
        if (z >= ancho && vecinoAtras != null)
            return vecinoAtras.GetBlock(x, y, z - ancho);

        if (y < 0 && vecinoAbajo != null)
            return vecinoAbajo.GetBlock(x, y + alto, z);
        if (y >= alto && vecinoArriba != null)
            return vecinoArriba.GetBlock(x, y - alto, z);

        return 0;
    }


    bool EsTransparente(int x, int y, int z)
    {
        byte block = this.GetBlockGlobal(x, y, z);
        return block == 0;
    }

    void CrearCara(byte block, Vector3 corner, Vector3 up, Vector3 side, bool reversed, List<Vector3> verts, List<Vector2> uvs, List<int> tris)
    {

        int index = verts.Count;

        //añade los vertices

        verts.Add(corner);
        verts.Add(corner + up);
        verts.Add(corner + up + side);
        verts.Add(corner + side);



        int x;
        int y;

        //en este caso la textura es de 4 x 4 es por ello que se divide por 4

        int tilling = 4;
        float uvWidth = 1f / tilling;

        x = block % tilling;
        y = block / tilling;

        if (x == 0)
        {

            x = tilling;
        }
        else
        {

            y++;
        }

        //donde se cuadran las texturas de los bloques (el algoritmo es supermalo, se debe mejorar en proximas versiones)

        uvs.Add(new Vector2(x * uvWidth, uvWidth * (tilling - y)));
        uvs.Add(new Vector2(x * uvWidth, uvWidth * (tilling - y) + uvWidth));
        uvs.Add(new Vector2((x * uvWidth) - uvWidth, uvWidth * (tilling - y) + uvWidth));
        uvs.Add(new Vector2((x * uvWidth) - uvWidth, uvWidth * (tilling - y)));

        //aqui simplemente revertimos la cara en caso de ser necesario, la izquierda es la inversa que la derecha, arriba es la inversa de abajo etc...
        //segun esto se cuadran los triangulos de la cara

        if (!reversed)
        {

            tris.Add(index + 0);
            tris.Add(index + 1);
            tris.Add(index + 2);
            tris.Add(index + 2);
            tris.Add(index + 3);
            tris.Add(index + 0);
        }
        else
        {

            tris.Add(index + 1);
            tris.Add(index + 0);
            tris.Add(index + 2);
            tris.Add(index + 3);
            tris.Add(index + 2);
            tris.Add(index + 0);

        }

    }


    int HashCoherente(int x, int z)
    {
        // Usa una combinación típica con números primos
        int semillaHash = World.actual.semilla;
        int n = x * 73856093 ^ z * 19349663 ^ semillaHash * 83492791;
        return Mathf.Abs(n);
    }


}