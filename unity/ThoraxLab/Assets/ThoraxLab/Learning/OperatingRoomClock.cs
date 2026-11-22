using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace ThoraxLab.Learning
{
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class OperatingRoomClock : MonoBehaviour
    {
        const int MaxElapsed = 99 * 3600 + 59 * 60 + 59;
        static readonly byte[] DigitMasks = { 0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f };
        static readonly Dictionary<char, byte[]> Letters = new Dictionary<char, byte[]>
        {
            { 'T', new byte[] { 0x01, 0x01, 0x7f, 0x01, 0x01 } },
            { 'I', new byte[] { 0x00, 0x41, 0x7f, 0x41, 0x00 } },
            { 'M', new byte[] { 0x7f, 0x02, 0x0c, 0x02, 0x7f } },
            { 'E', new byte[] { 0x7f, 0x49, 0x49, 0x49, 0x41 } },
            { 'L', new byte[] { 0x7f, 0x40, 0x40, 0x40, 0x40 } },
            { 'A', new byte[] { 0x7e, 0x09, 0x09, 0x09, 0x7e } },
            { 'P', new byte[] { 0x7f, 0x09, 0x09, 0x09, 0x06 } },
            { 'S', new byte[] { 0x46, 0x49, 0x49, 0x49, 0x31 } },
            { 'D', new byte[] { 0x7f, 0x41, 0x41, 0x22, 0x1c } }
        };
        static readonly Color32[] Inks =
        {
            new Color32(20, 44, 45, 255), new Color32(130, 248, 224, 255),
            new Color32(43, 35, 25, 255), new Color32(255, 199, 115, 255)
        };

        readonly int[] digitStarts = new int[12];
        readonly List<Vector2> uvs = new List<Vector2>(1200);
        GameObject face;
        Mesh mesh;
        Texture2D palette;
        string lastAttemptId;
        double elapsed, nextDraw, previousSample = -1;
        public int ClockSeconds { get; private set; } = -1;
        public int ElapsedSeconds { get; private set; } = -1;

        void OnEnable()
        {
            if (!gameObject.scene.IsValid()) return;
            var material = Resources.Load<Material>("Learning/GuidanceLine");
            if (!material) { Debug.LogError("Operating room clock needs the GuidanceLine material.", this); return; }
            BuildDisplay(material);
            Update();
        }

        void Update()
        {
            if (!mesh) return;
            if (!Application.IsPlaying(gameObject))
            {
                Draw(14 * 3600 + 28 * 60 + 36, 0);
                return;
            }

            var app = LabRuntime.Instance;
            var attempt = app ? app.Engine?.Attempt : null;
            double now = Time.realtimeSinceStartupAsDouble;
            if (attempt != null && lastAttemptId != attempt.id)
            {
                lastAttemptId = attempt.id;
                elapsed = 0;
                // Older checkpoints only carry elapsed time at recorded interactions.
                if (attempt.actions != null)
                    foreach (var action in attempt.actions)
                        if (action != null && action.elapsedSeconds > elapsed && !double.IsInfinity(action.elapsedSeconds))
                            elapsed = Math.Min(MaxElapsed, action.elapsedSeconds);
                previousSample = -1;
            }
            if (attempt != null && attempt.status == AttemptStatus.Active && Time.timeScale > 0 && app.CanInteract)
            {
                if (previousSample >= 0) elapsed = Math.Min(MaxElapsed, elapsed + now - previousSample);
                previousSample = now;
            }
            else previousSample = -1;

            if (now < nextDraw) return;
            var time = DateTime.Now.TimeOfDay;
            Draw((int)time.TotalSeconds, attempt != null ? (int)elapsed : 0);
            nextDraw = now + 1 - time.Ticks % TimeSpan.TicksPerSecond / (double)TimeSpan.TicksPerSecond;
        }

        void OnApplicationFocus(bool focused) { previousSample = -1; }
        void OnApplicationPause(bool paused) { previousSample = -1; }

        static Vector2 Ink(int index) => new Vector2((index + .5f) / Inks.Length, .5f);

        void BuildDisplay(Material material)
        {
            var vertices = new List<Vector3>(1200);
            var triangles = new List<int>(2400);
            uvs.Clear();

            void Polygon(int ink, params Vector2[] corners)
            {
                int first = vertices.Count;
                foreach (var p in corners)
                {
                    // Viewed from local +Z, the observer's right is local -X.
                    vertices.Add(new Vector3(-p.x, p.y, .002f));
                    uvs.Add(Ink(ink));
                }
                for (int i = 2; i < corners.Length; i++)
                {
                    triangles.Add(first); triangles.Add(first + i - 1); triangles.Add(first + i);
                }
            }

            void Square(float left, float bottom, float size, int ink)
            {
                Polygon(ink, new Vector2(left, bottom), new Vector2(left, bottom + size),
                    new Vector2(left + size, bottom + size), new Vector2(left + size, bottom));
            }

            void Bar(Vector2 from, Vector2 to, int ink)
            {
                var direction = (to - from).normalized;
                from += direction * .0028f; to -= direction * .0028f;
                var bevel = direction * .0067f;
                var side = new Vector2(-bevel.y, bevel.x);
                Polygon(ink, from, from + bevel + side, to - bevel + side, to, to - bevel - side, from + bevel - side);
            }

            for (int row = 0; row < 2; row++)
            {
                float y = row == 0 ? .093f : -.093f;
                int off = row * 2, on = off + 1;
                for (int digit = 0; digit < 6; digit++)
                {
                    float x = -.22f + digit * .119f + (digit / 2) * .042f;
                    var left = new Vector2(x, y);
                    var right = new Vector2(x + .082f, y);
                    var rise = new Vector2(0, .064f);
                    digitStarts[row * 6 + digit] = vertices.Count;
                    Bar(left + rise, right + rise, off); Bar(right + rise, right, off);
                    Bar(right, right - rise, off); Bar(right - rise, left - rise, off);
                    Bar(left - rise, left, off); Bar(left, left + rise, off); Bar(left, right, off);
                    Debug.Assert(vertices.Count - digitStarts[row * 6 + digit] == 7 * 6, "LED digit layout changed.");
                    if (digit == 1 || digit == 3)
                    {
                        Square(x + .115f, y + .023f, .013f, on);
                        Square(x + .115f, y - .036f, .013f, on);
                    }
                }
                const float pitch = .0068f;
                float letterX = -.56f;
                foreach (char letter in row == 0 ? "TIME" : "ELAPSED")
                {
                    var columns = Letters[letter];
                    for (int x = 0; x < columns.Length; x++)
                        for (int bit = 0; bit < 7; bit++)
                            if ((columns[x] & (1 << bit)) != 0)
                                Square(letterX + x * pitch, y + (2.5f - bit) * pitch, pitch, on);
                    letterX += 6 * pitch;
                }
            }

            mesh = new Mesh { name = "Operating room LED segments", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            mesh.SetVertices(vertices); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
            palette = new Texture2D(Inks.Length, 1, TextureFormat.RGBA32, false)
            {
                name = "Operating room LED colors", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            };
            palette.SetPixels32(Inks); palette.Apply(false, true);
            face = new GameObject("Operating room LED face", typeof(MeshFilter), typeof(MeshRenderer));
            face.hideFlags = HideFlags.HideAndDontSave;
            face.layer = gameObject.layer;
            face.transform.SetParent(transform, false);
            face.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = face.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var colors = new MaterialPropertyBlock();
            colors.SetTexture("_BaseMap", palette); colors.SetColor("_BaseColor", Color.white);
            renderer.SetPropertyBlock(colors);
        }

        void Draw(int clock, int timer)
        {
            if (clock == ClockSeconds && timer == ElapsedSeconds) return;
            for (int row = 0; row < 2; row++)
            {
                int seconds = row == 0 ? clock : timer;
                if (seconds == (row == 0 ? ClockSeconds : ElapsedSeconds)) continue;
                int digits = seconds / 3600 * 10000 + seconds / 60 % 60 * 100 + seconds % 60;
                for (int i = 5; i >= 0; i--, digits /= 10)
                {
                    int start = digitStarts[row * 6 + i], mask = DigitMasks[digits % 10];
                    for (int segment = 0; segment < 7; segment++)
                    {
                        var ink = Ink(row * 2 + ((mask >> segment) & 1));
                        for (int vertex = 0; vertex < 6; vertex++) uvs[start++] = ink;
                    }
                }
            }
            ClockSeconds = clock; ElapsedSeconds = timer;
            mesh.SetUVs(0, uvs);
        }

        void OnDisable()
        {
            foreach (var generated in new UnityEngine.Object[] { face, mesh, palette })
            {
                if (!generated) continue;
                if (Application.IsPlaying(gameObject)) Destroy(generated);
                else DestroyImmediate(generated);
            }
            face = null; mesh = null; palette = null;
            previousSample = -1; nextDraw = 0;
            ClockSeconds = ElapsedSeconds = -1;
        }
    }
}
