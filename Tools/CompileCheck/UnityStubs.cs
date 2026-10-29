// Just enough of the Unity API (signatures only) to compile the game without Unity: Tools/CompileCheck/check.sh.
// When the game starts using a Unity member that is missing here, add it.
#pragma warning disable
using System;
namespace UnityEngine
{
    public class Object { public string name; public static void Destroy(Object o) { } public static void DontDestroyOnLoad(Object o) { }
        public static T FindFirstObjectByType<T>() where T : Object => null; public static T[] FindObjectsByType<T>(FindObjectsSortMode m) where T : Object => null;
        public static implicit operator bool(Object o) => o != null; }
    public enum FindObjectsSortMode { None, InstanceID }
    public class Component : Object { public GameObject gameObject; public Transform transform; public string tag;
        public T GetComponent<T>() => default(T); public T GetComponentInParent<T>() => default(T); public T[] GetComponentsInChildren<T>() => null; public T[] GetComponentsInChildren<T>(bool inactive) => null; }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour { }
    public sealed class GameObject : Object { public GameObject() { } public GameObject(string n) { } public Transform transform; public int layer; public string tag;
        public static GameObject CreatePrimitive(PrimitiveType t) => null; public T AddComponent<T>() where T : Component => null; public T GetComponent<T>() => default(T); public void SetActive(bool v) { } }
    public enum PrimitiveType { Sphere, Capsule, Cylinder, Cube, Plane, Quad }
    public class Transform : Component { public Vector3 position, localPosition, localScale, forward; public Quaternion rotation, localRotation;
        public void SetParent(Transform p, bool w) { } public void SetPositionAndRotation(Vector3 p, Quaternion r) { } }
    public struct Vector2 { public static Vector2 zero; public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator /(Vector2 a, float b) => a; }
    public struct Vector3 { public float x, y, z; public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero, up, forward, one; public float magnitude => 0f; public float sqrMagnitude => 0f; public Vector3 normalized => this;
        public static Vector3 operator +(Vector3 a, Vector3 b) => a; public static Vector3 operator -(Vector3 a, Vector3 b) => a;
        public static Vector3 operator *(Vector3 a, float b) => a; public static Vector3 operator *(float b, Vector3 a) => a;
        public static bool operator ==(Vector3 a, Vector3 b) => true; public static bool operator !=(Vector3 a, Vector3 b) => true;
        public override bool Equals(object o) => true; public override int GetHashCode() => 0;
        public static Vector3 ClampMagnitude(Vector3 v, float m) => v; public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a; }
    public struct Quaternion { public static Quaternion identity; public static Quaternion Euler(float x, float y, float z) => identity;
        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => a; public static Quaternion LookRotation(Vector3 f) => identity;
        public static Vector3 operator *(Quaternion q, Vector3 v) => v; }
    public struct Color { public float r, g, b, a; public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white, black; public static Color operator *(Color c, float f) => c; }
    public struct Matrix4x4 { public static Matrix4x4 Scale(Vector3 v) => default(Matrix4x4); }
    public struct Rect { public float x, y, width, height; public float xMax => 0f; public float yMax => 0f; public Vector2 center => default(Vector2); public Rect(float x, float y, float w, float h) { this.x = x; this.y = y; width = w; height = h; } public bool Contains(Vector2 p) => true; }
    public struct RectInt { public int x, y, width, height; public int xMax => 0; public int yMax => 0; public RectInt(int x, int y, int w, int h) { this.x = x; this.y = y; width = w; height = h; } }
    public struct Ray { public Ray(Vector3 o, Vector3 d) { } }
    public struct RaycastHit { public Collider collider; public Vector3 point; public float distance; }
    public static class Physics { public static RaycastHit[] RaycastAll(Ray r, float d) => null; public static bool Raycast(Ray r, out RaycastHit h, float d) { h = default(RaycastHit); return false; }
        public static bool Raycast(Vector3 o, Vector3 dir, out RaycastHit h, float d) { h = default(RaycastHit); return false; } }
    public static class Mathf { public const float Deg2Rad = 0f, PI = 3.14159f; public static float Sin(float f) => f; public static float Cos(float f) => f; public static float Exp(float f) => f;
        public static float Max(float a, float b) => a; public static int Max(int a, int b) => a; public static float Min(float a, float b) => a; public static int Min(int a, int b) => a;
        public static float Clamp(float v, float a, float b) => v; public static float Clamp01(float v) => v; public static float Lerp(float a, float b, float t) => a;
        public static float InverseLerp(float a, float b, float v) => a; public static int RoundToInt(float f) => 0; public static int CeilToInt(float f) => 0; public static float Abs(float f) => f; public static float Sqrt(float f) => f; }
    public static class Random { public static float value; public static float Range(float a, float b) => a; public static int Range(int a, int b) => a; }
    public static class Time { public static float time, deltaTime; public static int frameCount; }
    public static class Screen { public static int width, height; }
    public static class Application { public static int targetFrameRate; }
    public static class Debug { public static void Log(object o) { } public static void LogWarning(object o) { } public static void LogError(object o) { } }
    public static class PlayerPrefs { public static void SetString(string k, string v) { } public static string GetString(string k) => null; public static bool HasKey(string k) => false; public static void DeleteKey(string k) { } public static void Save() { } }
    public static class JsonUtility { public static string ToJson(object o) => null; public static T FromJson<T>(string s) => default(T); }
    public enum KeyCode { LeftShift, RightShift, Alpha1, Alpha2, Alpha3, Alpha4, Alpha5, Alpha6, Alpha7, Alpha8, Alpha9, W, A, S, D, V, H, F1, Escape, LeftArrow, RightArrow, UpArrow, DownArrow }
    public static class Input { public static bool GetKey(KeyCode k) => false; public static bool GetKeyDown(KeyCode k) => false; public static bool GetMouseButtonDown(int b) => false;
        public static bool GetMouseButton(int b) => false; public static Vector3 mousePosition; public static Vector2 mouseScrollDelta; public static float GetAxisRaw(string a) => 0f; }
    public enum CursorLockMode { None, Locked, Confined }
    public static class Cursor { public static CursorLockMode lockState; public static bool visible; }
    public enum CameraClearFlags { Skybox, SolidColor }
    public sealed class Camera : Behaviour { public static Camera main; public Ray ScreenPointToRay(Vector3 p) => default(Ray); public Vector3 WorldToScreenPoint(Vector3 p) => p;
        public float fieldOfView, nearClipPlane, farClipPlane; public CameraClearFlags clearFlags; public Color backgroundColor; }
    public sealed class AudioListener : Behaviour { }
    public enum LightType { Spot, Directional, Point }
    public enum LightShadows { None, Hard, Soft }
    public sealed class Light : Behaviour { public LightType type; public float intensity, range; public Color color; public LightShadows shadows; }
    public enum FogMode { Linear = 1 }
    public static class RenderSettings { public static Rendering.AmbientMode ambientMode; public static Color ambientLight, fogColor; public static bool fog; public static FogMode fogMode; public static float fogStartDistance, fogEndDistance; }
    public class Renderer : Component { public Material sharedMaterial; public Rendering.ShadowCastingMode shadowCastingMode; }
    public class Texture : Object { public TextureWrapMode wrapMode; public FilterMode filterMode; public int anisoLevel; }
    public enum TextureFormat { RGBA32 }
    public enum TextureWrapMode { Repeat, Clamp }
    public enum FilterMode { Point, Bilinear }
    public sealed class Texture2D : Texture { public Texture2D(int w, int h, TextureFormat f, bool mip) { } public static Texture2D whiteTexture; public void SetPixels(Color[] c) { } public void Apply() { } }
    public class Material : Object { public Material(Material m) { } public Color color; public Texture mainTexture; public Vector2 mainTextureScale; public bool HasProperty(string p) => false; public void SetFloat(string p, float v) { } public void EnableKeyword(string k) { } public void SetColor(string p, Color c) { } }
    public class Collider : Component { public bool isTrigger, enabled; }
    public sealed class BoxCollider : Collider { public Vector3 center, size; }
    public sealed class CapsuleCollider : Collider { public float radius, height; public Vector3 center; }
    public sealed class CharacterController : Collider { public float height, radius, stepOffset, skinWidth; public Vector3 center; public bool isGrounded; public void Move(Vector3 m) { } }
    public enum RuntimeInitializeLoadType { AfterSceneLoad, BeforeSceneLoad, SubsystemRegistration }
    public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) { } }
    public enum FullScreenMode { Windowed }
    public enum FontStyle { Normal, Bold }
    public enum TextAnchor { UpperRight, MiddleCenter }
    public class GUIStyleState { public Color textColor; }
    public class RectOffset { public RectOffset(int l, int r, int t, int b) { } }
    public class GUIContent { public GUIContent(string s) { } }
    public class GUIStyle { public GUIStyle(GUIStyle o) { } public RectOffset padding; public Vector2 CalcSize(GUIContent c) => default(Vector2); public int fontSize; public bool richText, wordWrap; public FontStyle fontStyle; public TextAnchor alignment; public GUIStyleState normal; }
    public class GUISkin { public GUIStyle label, button; }
    public delegate void WindowFunction(int id);
    public static class GUI { public static Matrix4x4 matrix; public static Color color; public static GUISkin skin; public static void DrawTexture(Rect r, Texture t) { }
        public static void Label(Rect r, string s, GUIStyle st) { } public static bool Toggle(Rect r, bool v, string s, GUIStyle st) => v; public static void DragWindow() { }
        public static bool Button(Rect r, string s) => false; public static Vector2 BeginScrollView(Rect p, Vector2 s, Rect v) => s; public static void EndScrollView() { } }
    public class GUILayoutOption { }
    public static class GUIUtility { public static void RotateAroundPivot(float a, Vector2 p) { } }
    public static class GUILayout { public static Rect Window(int id, Rect r, WindowFunction f, string t, params GUILayoutOption[] o) => r; public static void Label(string s, GUIStyle st, params GUILayoutOption[] o) { }
        public static bool Button(string s, params GUILayoutOption[] o) => false; public static GUILayoutOption Width(float w) => null; public static void BeginHorizontal(params GUILayoutOption[] o) { } public static void EndHorizontal() { } public static void Space(float f) { } }
    public enum EventType { MouseDown, MouseMove, Repaint }
    public class Event { public static Event current; public EventType type; public Vector2 mousePosition; public int button; public bool shift; public void Use() { } }
}
namespace UnityEngine.Rendering
{
    public enum AmbientMode { Skybox, Trilight, Flat }
    public enum ShadowCastingMode { Off, On, TwoSided, ShadowsOnly }
}
namespace UnityEditor
{
    public sealed class InitializeOnLoadAttribute : Attribute { }
    public sealed class MenuItem : Attribute { public MenuItem(string s) { } }
    public static class EditorApplication { public static Action delayCall; public static bool isPlayingOrWillChangePlaymode; }
    public sealed class EditorBuildSettingsScene { public EditorBuildSettingsScene(string p, bool e) { } }
    public static class EditorBuildSettings { public static EditorBuildSettingsScene[] scenes; }
    public static class PlayerSettings { public static string productName; public static UnityEngine.FullScreenMode fullScreenMode; public static int defaultScreenWidth, defaultScreenHeight; public static bool resizableWindow; }
    public enum BuildTarget { StandaloneWindows64 }
    public enum BuildOptions { None }
    public struct BuildPlayerOptions { public string[] scenes; public string locationPathName; public BuildTarget target; public BuildOptions options; }
    public static class BuildPipeline { public static Build.Reporting.BuildReport BuildPlayer(BuildPlayerOptions o) => null; }
    public static class EditorUtility { public static void RevealInFinder(string p) { } }
}
namespace UnityEditor.Build.Reporting
{
    public enum BuildResult { Succeeded, Failed }
    public struct BuildSummary { public BuildResult result; }
    public class BuildReport { public BuildSummary summary; }
}
namespace UnityEditor.SceneManagement
{
    public struct Scene { }
    public enum NewSceneSetup { EmptyScene }
    public enum NewSceneMode { Single }
    public static class EditorSceneManager { public static Scene NewScene(NewSceneSetup s, NewSceneMode m) => default(Scene); public static bool SaveScene(Scene s, string p) => true; public static Scene OpenScene(string p) => default(Scene); }
}
