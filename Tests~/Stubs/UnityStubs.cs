// Unity API の最小限スタブ。
// テストプロジェクトは Unity なしで動作するため、コンパイルに必要な型を定義する。
using System;
using System.Collections.Generic;
using System.Linq;

namespace UnityEngine.SceneManagement
{
    public struct Scene
    {
        // テスト環境ではシーン外オブジェクトのみ扱うため常に false を返す
        public bool IsValid() => false;
    }
}

namespace UnityEngine
{
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f)
        { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color black => new Color(0, 0, 0, 1);
        public static Color white => new Color(1, 1, 1, 1);
        public static Color operator *(Color c, float f)
            => new Color(c.r * f, c.g * f, c.b * f, c.a * f);
    }

    public enum HideFlags
    {
        None = 0, HideInHierarchy = 1, HideInInspector = 2, DontSaveInEditor = 4,
        NotEditable = 8, DontSaveInBuild = 16, DontUnloadUnusedAsset = 32,
        DontSave = 52, HideAndDontSave = 61,
    }

    public class Object
    {
        public string name = "";
        public HideFlags hideFlags;

        public static void DestroyImmediate(Object obj) { }
        public static void Destroy(Object obj)          { }
    }

    public class Transform : Object
    {
        public Transform? parent;

        public Transform? Find(string name)
        {
            // スタブ実装: 常に null を返す
            return null;
        }
    }

    public class GameObject : Object
    {
        public Transform transform { get; }
        public SceneManagement.Scene scene { get; } = default; // テスト環境: IsValid() = false
        public GameObject()           { transform = new Transform { name = "" }; }
        public GameObject(string n)   { name = n; transform = new Transform { name = n }; }
        public T? GetComponent<T>() where T : class => null;
        public T? GetComponentInParent<T>() where T : class => null;
        public T AddComponent<T>() where T : Component, new() => new T();
    }

    public class Component : Object
    {
        public Transform transform { get; set; } = new Transform();
        public GameObject gameObject { get; set; } = new GameObject();
        public T? GetComponent<T>() where T : class => null;
        public T? GetComponentInParent<T>() where T : class => null;
        public T AddComponent<T>() where T : Component, new() => new T();
    }

    public class Behaviour : Component
    {
        public bool enabled = true;
    }

    public class MonoBehaviour : Behaviour { }

    public class Renderer : Component
    {
        public Material[] sharedMaterials = Array.Empty<Material>();
    }

    public class SkinnedMeshRenderer : Renderer { }
    public class MeshRenderer : Renderer { }

    public class Material : Object
    {
        private readonly HashSet<string> _props = new HashSet<string>();
        public Shader shader = new Shader();
        public Material() { }
        public Material(Shader s) { shader = s; }
        public bool HasProperty(string n) => _props.Contains(n);
        public void AddProperty(string n) => _props.Add(n);
    }

    public class Shader : Object { }

    public abstract class Motion : Object { }

    public class AnimationClip : Motion
    {
        // テスト検証用: AnimationUtility.SetEditorCurve が書き込む
        internal readonly List<(UnityEditor.EditorCurveBinding binding, AnimationCurve curve)> _bindings
            = new List<(UnityEditor.EditorCurveBinding, AnimationCurve)>();
    }

    public class AnimationCurve
    {
        public static AnimationCurve Constant(float timeStart, float timeEnd, float value)
            => new AnimationCurve();
    }

    public enum WrapMode { Default, Once, Loop, PingPong, ClampForever, Clamp = 8 }

    public class ScriptableObject : Object
    {
        public static T CreateInstance<T>() where T : ScriptableObject, new() => new T();
    }

    public class RuntimeAnimatorController : Object { }

    public class Animator : Behaviour
    {
        public RuntimeAnimatorController? runtimeAnimatorController;
    }

    public enum AnimatorControllerParameterType { Float = 1, Int = 3, Bool = 4, Trigger = 9 }

    public class AnimatorControllerParameter
    {
        public string name = "";
        public AnimatorControllerParameterType type;
        public float defaultFloat;
        public int   defaultInt;
        public bool  defaultBool;
    }

    public class AvatarMask : Object { }

    public static class Mathf
    {
        public static float Clamp(float v, float min, float max)
            => v < min ? min : v > max ? max : v;
        public static float Max(float a, float b) => a > b ? a : b;
        public static float Abs(float v) => v < 0 ? -v : v;
    }

    public struct Vector2 { public float x, y; }
    public struct Vector3
    {
        public float x, y, z;
        public static Vector3 zero => default;
    }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class TooltipAttribute : Attribute { public TooltipAttribute(string s) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DisallowMultipleComponent : Attribute { }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AddComponentMenu : Attribute { public AddComponentMenu(string s) { } }

    public static class Application
    {
        public static string dataPath { get; } = "/tmp/TestAssets";
    }

    public static class Debug
    {
        public static void Log(object message)      { }
        public static void LogWarning(object msg)   { }
        public static void LogError(object message) { }
    }

    public static class Undo
    {
        public static void RegisterCreatedObjectUndo(Object obj, string name) { }
        public static T AddComponent<T>(GameObject go) where T : Component, new()
            => go.AddComponent<T>();
    }
}

namespace UnityEditor
{
    using UnityEngine;

    public struct EditorCurveBinding : IEquatable<EditorCurveBinding>
    {
        public string path;
        public Type   type;
        public string propertyName;
        public bool   isPPtrCurve;
        public bool   isDiscreteCurve;

        public static EditorCurveBinding FloatCurve(string path, Type type, string propertyName)
            => new EditorCurveBinding { path = path, type = type, propertyName = propertyName };

        public bool Equals(EditorCurveBinding o)
            => path == o.path && type == o.type && propertyName == o.propertyName;
        public override bool Equals(object? obj) => obj is EditorCurveBinding b && Equals(b);
        public override int  GetHashCode()       => HashCode.Combine(path, type, propertyName);
    }

    public static class AnimationUtility
    {
        public static void SetEditorCurve(
            AnimationClip clip, EditorCurveBinding binding, AnimationCurve curve)
            => clip._bindings.Add((binding, curve));

        public static EditorCurveBinding[] GetCurveBindings(AnimationClip clip)
            => clip._bindings.Select(b => b.binding).ToArray();

        // テスト環境ではシーン外オブジェクトのみ扱うため空配列を返す（BuildBindingCache がフォールバックに進む）
        public static EditorCurveBinding[] GetAnimatableBindings(
            UnityEngine.GameObject gameObject, UnityEngine.GameObject root)
            => Array.Empty<EditorCurveBinding>();
    }

    public static class AssetDatabase
    {
        public static string CreateFolder(string parent, string name)          => $"{parent}/{name}";
        public static void   CreateAsset(Object asset, string path)            { }
        public static void   AddObjectToAsset(Object obj, Object assetObject)  { }
        public static void   AddObjectToAsset(Object obj, string path)         { }
        public static void   RemoveObjectFromAsset(Object obj)                 { }
        public static void   DeleteAsset(string path)                          { }
        public static void   SaveAssets()                                      { }
        public static void   Refresh()                                         { }
        public static bool   IsValidFolder(string path)                        => false;
        public static T?     LoadAssetAtPath<T>(string path) where T : class  => null;
    }

    public static class EditorUtility
    {
        public static bool DisplayDialog(string title, string message, string ok, string cancel = "")
            => true;
        public static void SetDirty(Object target)                        { }
        public static void CopySerialized(Object source, Object dest)     { }
        public static string SaveFolderPanel(string title, string dir, string defaultName)
            => string.Empty;
    }

    public static class Selection
    {
        public static GameObject? activeGameObject;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public sealed class MenuItem : Attribute { public MenuItem(string s) { } }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CreateAssetMenu : Attribute
    {
        public string? fileName;
        public string? menuName;
        public int     order;
    }

    [AttributeUsage(AttributeTargets.Class)]
    public sealed class CustomEditor : Attribute { public CustomEditor(Type t) { } }

    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class SerializeField : Attribute { }

    public class Editor : Object
    {
        public virtual void OnInspectorGUI() { }
    }

    public class EditorWindow : Object
    {
        public Vector2 minSize;
        protected static T GetWindow<T>(string title = "") where T : EditorWindow, new() => new T();
        protected void Repaint() { }
    }

    public class EditorStyles
    {
        public static GUIStyle boldLabel    { get; } = new GUIStyle();
        public static GUIStyle helpBox      { get; } = new GUIStyle();
        public static GUIStyle textField    { get; } = new GUIStyle();
    }

    public class GUIStyle { }

    public static class EditorGUILayout
    {
        public static UnityEngine.Object ObjectField(string l, UnityEngine.Object o, Type t, bool b)
            => o!;
        public static int   Popup(string l, int i, string[] opts)               => i;
        public static int   IntField(string l, int v)                           => v;
        public static float Slider(string l, float v, float min, float max)     => v;
        public static string TextField(string l, string v)                      => v ?? "";
        public static string TextField(string v, params GUILayoutOption[] opts) => v ?? "";
        public static Color ColorField(string l, Color c)                       => c;
        public static bool  ToggleLeft(string l, bool v)                        => v;
        public static bool  Foldout(bool f, string l, bool t)                  => f;
        public static float FloatField(float v, params GUILayoutOption[] opts)  => v;
        public static float FloatField(string l, float v, params GUILayoutOption[] opts) => v;
        public static void  LabelField(string l, GUIStyle? s = null)            { }
        public static void  Space(float px)                                     { }
        public static Rect  GetControlRect(bool hasLabel = false, float height = 0f) => default;
        public static void  HelpBox(string msg, MessageType t)                  { }
        public static void  BeginHorizontal()                                   { }
        public static void  EndHorizontal()                                     { }
        public static void  BeginVertical(GUIStyle? s = null)                  { }
        public static void  EndVertical()                                       { }
        public static Vector2 BeginScrollView(Vector2 pos)                      => pos;
        public static void  EndScrollView()                                     { }
    }

    public static class EditorGUI
    {
        public static int  indentLevel;
        public static void DrawRect(Rect r, Color c)     { }
        public static void BeginDisabledGroup(bool d)    { }
        public static void EndDisabledGroup()            { }

        public sealed class DisabledScope : IDisposable
        {
            public DisabledScope(bool disabled) { }
            public void Dispose() { }
        }
    }

    public static class GUILayout
    {
        public static bool   Button(string l, params GUILayoutOption[] opts)    => false;
        public static string TextField(string v, params GUILayoutOption[] opts) => v ?? "";
        public static int    SelectionGrid(int i, string[] opts, int cols,
            params GUILayoutOption[] extra) => i;
        public static void   Label(string l, GUIStyle? s = null, params GUILayoutOption[] opts) { }
        public static GUILayoutOption Width(float w)       => new GUILayoutOption();
        public static GUILayoutOption Height(float h)      => new GUILayoutOption();
        public static GUILayoutOption ExpandWidth(bool e)  => new GUILayoutOption();
        public static float HorizontalSlider(float value, float min, float max,
            params GUILayoutOption[] opts) => value;
    }

    public class GUILayoutOption { }

    public static class GUI
    {
        public static bool changed;
    }

    public struct Rect { }

    public enum MessageType { None, Info, Warning, Error }

    public class SerializedObject : IDisposable
    {
        public SerializedObject(UnityEngine.Object obj) { }
        // テスト環境では null を返す → FixMeshRendererCustomType が早期リターンするため問題なし
        public SerializedProperty? FindProperty(string propertyPath) => null;
        public void ApplyModifiedPropertiesWithoutUndo() { }
        public void Dispose() { }
    }

    public class SerializedProperty
    {
        public bool isArray    => false;
        public int  arraySize  => 0;
        public int  intValue   { get; set; }
        public SerializedProperty? GetArrayElementAtIndex(int index)         => null;
        public SerializedProperty? FindPropertyRelative(string relativePath) => null;
    }

    public static class ShaderUtil
    {
        public enum ShaderPropertyType { Color = 0, Vector = 1, Float = 2, Range = 3, TexEnv = 4 }

        public static int GetPropertyCount(UnityEngine.Shader shader) => 0;
        public static string GetPropertyName(UnityEngine.Shader shader, int idx) => "";
        public static string GetPropertyDescription(UnityEngine.Shader shader, int idx) => "";
        public static ShaderPropertyType GetPropertyType(UnityEngine.Shader shader, int idx)
            => ShaderPropertyType.Float;
    }
}

namespace UnityEditor.Animations
{
    using UnityEngine;

    public enum AnimatorConditionMode { If = 1, IfNot = 2, Greater = 3, Less = 4, Equals = 6, NotEqual = 7 }
    public enum AnimatorLayerBlendingMode { Override, Additive }
    public enum BlendTreeType { Simple1D, SimpleDirectional2D, FreeformDirectional2D, FreeformCartesian2D, Direct }

    public struct AnimatorCondition
    {
        public string parameter;
        public AnimatorConditionMode mode;
        public float threshold;
    }

    public class AnimatorStateTransition : UnityEngine.Object
    {
        public bool hasExitTime;
        public float exitTime;
        public float duration;
        public bool hasFixedDuration;
        private readonly List<AnimatorCondition> _conditions = new List<AnimatorCondition>();
        public AnimatorCondition[] conditions => _conditions.ToArray();

        public void AddCondition(AnimatorConditionMode mode, float threshold, string parameter)
            => _conditions.Add(new AnimatorCondition
               { mode = mode, threshold = threshold, parameter = parameter });
    }

    public class AnimatorTransition : UnityEngine.Object
    {
        public void AddCondition(AnimatorConditionMode mode, float threshold, string parameter) { }
    }

    public class AnimatorState : UnityEngine.Object
    {
        public Motion?  motion;
        public bool     writeDefaultValues = true;
        public bool     timeParameterActive;
        public string   timeParameter = "";

        private readonly List<AnimatorStateTransition> _transitions = new List<AnimatorStateTransition>();
        public AnimatorStateTransition[] transitions => _transitions.ToArray();

        public AnimatorStateTransition AddTransition(AnimatorState destinationState)
        {
            var t = new AnimatorStateTransition();
            _transitions.Add(t);
            return t;
        }
    }

    public class AnimatorStateMachine : UnityEngine.Object
    {
        public AnimatorState? defaultState;
        public Vector3 entryPosition;
        public Vector3 anyStatePosition;
        public Vector3 exitPosition;

        private readonly List<AnimatorState>           _states   = new List<AnimatorState>();
        private readonly List<AnimatorStateTransition> _anyTrans = new List<AnimatorStateTransition>();

        public AnimatorState[] states => _states.ToArray();

        public AnimatorState AddState(string name)
        {
            var s = new AnimatorState { name = name };
            _states.Add(s);
            return s;
        }

        public AnimatorStateTransition AddAnyStateTransition(AnimatorState dest)
        {
            var t = new AnimatorStateTransition();
            _anyTrans.Add(t);
            return t;
        }

        public AnimatorTransition AddEntryTransition(AnimatorState dest) => new AnimatorTransition();
    }

    public class BlendTree : Motion
    {
        public string blendParameter = "";
        public BlendTreeType blendType;
        private readonly List<(Motion motion, float threshold)> _children
            = new List<(Motion, float)>();

        public void AddChild(Motion motion, float threshold) => _children.Add((motion, threshold));
        public void AddChild(Motion motion)                  => _children.Add((motion, 0f));
        public int ChildCount => _children.Count;
    }

    public class AnimatorControllerLayer
    {
        public string name = "";
        public float defaultWeight = 1f;
        public AnimatorLayerBlendingMode blendingMode = AnimatorLayerBlendingMode.Override;
        public AvatarMask? avatarMask;
        public AnimatorStateMachine stateMachine = new AnimatorStateMachine();
    }

    public class AnimatorController : RuntimeAnimatorController
    {
        private readonly List<AnimatorControllerLayer>     _layers     = new List<AnimatorControllerLayer>();
        private readonly List<AnimatorControllerParameter> _parameters = new List<AnimatorControllerParameter>();

        public AnimatorControllerLayer[]     layers     => _layers.ToArray();
        public AnimatorControllerParameter[] parameters => _parameters.ToArray();

        public void AddLayer(AnimatorControllerLayer layer) => _layers.Add(layer);
        public void AddLayer(string name)
            => _layers.Add(new AnimatorControllerLayer { name = name });
        public void RemoveLayer(int index)
        { if (index >= 0 && index < _layers.Count) _layers.RemoveAt(index); }
        public void AddParameter(string name, AnimatorControllerParameterType type)
            => _parameters.Add(new AnimatorControllerParameter { name = name, type = type });
        public void RemoveParameter(int index)
        { if (index >= 0 && index < _parameters.Count) _parameters.RemoveAt(index); }

        public static AnimatorController CreateAnimatorControllerAtPath(string path)
            => new AnimatorController();
    }
}

