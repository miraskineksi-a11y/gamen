using UnityEngine;
using UnityEngine.Rendering;

namespace Caribbean
{
    /// <summary>
    /// Океан: сетка, сгущающаяся к центру и следующая за камерой. Волны рисует шейдер
    /// Caribbean/Water, а WaveField считает те же волны на CPU. Под водой — тёмное «дно»,
    /// которое просвечивает сквозь прозрачную воду (у островов просвечивает песок).
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class Ocean : MonoBehaviour
    {
        public Transform follow;
        Transform water, floor;
        const float Snap = 2f;

        public void Build()
        {
            var shader = Palette.LoadShader("CaribbeanWater", "Unlit/Transparent");
            var mat = new Material(shader) { name = "Water" };
            water = MakeObject("Water", BuildGrid(128, 1600f), mat);
            water.gameObject.layer = 4; // слой Water: лучи его не видят (у воды нет коллайдера)

            var fm = BuildQuad(4200f);
            floor = MakeObject("SeaFloor", fm, Palette.Unlit(new Color(0.03f, 0.17f, 0.23f)));
            floor.position = new Vector3(0, -24f, 0);
            WaveField.PushToShaders();
        }

        Transform MakeObject(string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        static Mesh BuildGrid(int n, float extent)
        {
            var verts = new Vector3[n * n];
            for (int j = 0; j < n; j++)
                for (int i = 0; i < n; i++)
                {
                    float u = i / (float)(n - 1) * 2f - 1f;
                    float v = j / (float)(n - 1) * 2f - 1f;
                    verts[j * n + i] = new Vector3(Map(u, extent), 0f, Map(v, extent));
                }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int k = 0;
            for (int j = 0; j < n - 1; j++)
                for (int i = 0; i < n - 1; i++)
                {
                    int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            var m = new Mesh { name = "OceanGrid" };
            m.vertices = verts;
            m.triangles = tris;
            m.RecalculateNormals();
            m.bounds = new Bounds(Vector3.zero, new Vector3(extent * 2.2f, 60f, extent * 2.2f));
            return m;
        }

        // шаг ~2 м у центра, ~50 м у краёв
        static float Map(float u, float extent) => extent * (0.08f * u + 0.92f * u * u * u);

        static Mesh BuildQuad(float size)
        {
            float h = size / 2f;
            var m = new Mesh { name = "FloorQuad" };
            m.vertices = new[] { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            return m;
        }

        void Update()
        {
            WaveField.Time += Time.deltaTime;
            WaveField.PushToShaders();
            var cam = follow != null ? follow : (Camera.main != null ? Camera.main.transform : null);
            if (cam == null || water == null) return;
            var p = cam.position;
            water.position = new Vector3(Mathf.Round(p.x / Snap) * Snap, 0f, Mathf.Round(p.z / Snap) * Snap);
            floor.position = new Vector3(p.x, -24f, p.z);
        }
    }
}
