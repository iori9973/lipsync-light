using System.Collections.Generic;
using System.Linq;
using LipsyncLight;
using UnityEditor;
using UnityEngine;
using Xunit;

namespace LipsyncLight.Tests;

public class GetRelativePathTests
{
    [Fact]
    public void DirectChild_ReturnsSingleName()
    {
        var root  = new Transform { name = "AvatarRoot" };
        var child = new Transform { name = "Body", parent = root };

        string path = LipsyncLightBuilder.GetRelativePath(root, child);

        Assert.Equal("Body", path);
    }

    [Fact]
    public void GrandChild_ReturnsTwoLevels()
    {
        var root       = new Transform { name = "AvatarRoot" };
        var child      = new Transform { name = "Body", parent = root };
        var grandchild = new Transform { name = "Head", parent = child };

        string path = LipsyncLightBuilder.GetRelativePath(root, grandchild);

        Assert.Equal("Body/Head", path);
    }

    [Fact]
    public void SameAsRoot_ReturnsEmpty()
    {
        var root = new Transform { name = "AvatarRoot" };

        string path = LipsyncLightBuilder.GetRelativePath(root, root);

        Assert.Equal("", path);
    }

    [Fact]
    public void ThreeLevels_ReturnsSlashSeparatedPath()
    {
        var root = new Transform { name = "Root" };
        var a    = new Transform { name = "A", parent = root };
        var b    = new Transform { name = "B", parent = a };
        var c    = new Transform { name = "C", parent = b };

        string path = LipsyncLightBuilder.GetRelativePath(root, c);

        Assert.Equal("A/B/C", path);
    }
}

public class BuildPropertyPathTests
{
    [Fact]
    public void MaterialIndex0_UsesMaterialPrefix()
    {
        string result = LipsyncLightBuilder.BuildPropertyPath(0, "_EmissionColor");

        Assert.Equal("material._EmissionColor", result);
    }

    [Fact]
    public void MaterialIndex1_UsesMaterialsArrayPrefix()
    {
        string result = LipsyncLightBuilder.BuildPropertyPath(1, "_EmissionColor");

        Assert.Equal("materials[1]._EmissionColor", result);
    }

    [Fact]
    public void MaterialIndex3_UsesMaterialsArrayWith3()
    {
        string result = LipsyncLightBuilder.BuildPropertyPath(3, "_EmissionColor");

        Assert.Equal("materials[3]._EmissionColor", result);
    }

    [Fact]
    public void CustomPropertyName_IsPreserved()
    {
        string result = LipsyncLightBuilder.BuildPropertyPath(0, "_EmissionColor2");

        Assert.Equal("material._EmissionColor2", result);
    }
}

public class DetectFromPropertyNamesTests
{
    [Fact]
    public void ContainsEmissionColor_ReturnsEmissionColor()
    {
        var names = new[] { "_Color", "_EmissionColor", "_MainTex" };

        string? result = ShaderPropertyDetector.DetectFromPropertyNames(names);

        Assert.Equal("_EmissionColor", result);
    }

    [Fact]
    public void ContainsEmissionColor2_ReturnsEmissionColor2()
    {
        var names = new[] { "_Color", "_EmissionColor2" };

        string? result = ShaderPropertyDetector.DetectFromPropertyNames(names);

        Assert.Equal("_EmissionColor2", result);
    }

    [Fact]
    public void BothPresent_PrefersEmissionColor()
    {
        var names = new[] { "_EmissionColor", "_EmissionColor2" };

        string? result = ShaderPropertyDetector.DetectFromPropertyNames(names);

        Assert.Equal("_EmissionColor", result);
    }

    [Fact]
    public void NoEmissionProperty_ReturnsNull()
    {
        var names = new[] { "_Color", "_MainTex", "_BumpMap" };

        string? result = ShaderPropertyDetector.DetectFromPropertyNames(names);

        Assert.Null(result);
    }

    [Fact]
    public void EmptyList_ReturnsNull()
    {
        string? result = ShaderPropertyDetector.DetectFromPropertyNames(
            Enumerable.Empty<string>());

        Assert.Null(result);
    }
}

public class CreateEmissionClipTests
{
    private static (GameObject avatarRoot, MeshRenderer renderer) BuildHierarchy(
        string rootName, string childName)
    {
        var avatarRoot     = new GameObject(rootName);
        var childTransform = new Transform { name = childName, parent = avatarRoot.transform };
        var renderer       = new MeshRenderer();
        renderer.transform = childTransform;
        return (avatarRoot, renderer);
    }

    [Fact]
    public void SingleTarget_SingleProperty_Creates4Bindings()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "Body");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = renderer,
                MaterialIndex = 0,
                PropertyNames = new List<string> { "_EmissionColor" },
                OnColor       = Color.white,
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, t => t.OnColor, "Test");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        Assert.Equal(4, bindings.Length); // r, g, b, a
    }

    [Fact]
    public void SingleTarget_TwoProperties_Creates8Bindings()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "Body");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = renderer,
                MaterialIndex = 0,
                PropertyNames = new List<string> { "_EmissionColor", "_EmissionColor2" },
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "Test");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        Assert.Equal(8, bindings.Length); // 2 properties × 4 channels
    }

    [Fact]
    public void TwoTargets_SingleProperty_Creates8Bindings()
    {
        var (avatarRoot, r1) = BuildHierarchy("Root", "Body");
        var childTransform2  = new Transform { name = "Face", parent = avatarRoot.transform };
        var r2 = new MeshRenderer { transform = childTransform2 };

        var targets = new List<EmissionTarget>
        {
            new EmissionTarget { Renderer = r1, MaterialIndex = 0, PropertyNames = new List<string> { "_EmissionColor" } },
            new EmissionTarget { Renderer = r2, MaterialIndex = 0, PropertyNames = new List<string> { "_EmissionColor" } },
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "Test");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        Assert.Equal(8, bindings.Length);
    }

    [Fact]
    public void PropertyPath_Index0_UsesMaterialPrefix()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "Body");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = renderer,
                MaterialIndex = 0,
                PropertyNames = new List<string> { "_EmissionColor" },
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "T");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        Assert.All(bindings, b => Assert.StartsWith("material._EmissionColor", b.propertyName));
    }

    [Fact]
    public void PropertyPath_Index2_UsesMaterialsArrayPrefix()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "Body");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = renderer,
                MaterialIndex = 2,
                PropertyNames = new List<string> { "_EmissionColor" },
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "T");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        Assert.All(bindings, b => Assert.StartsWith("materials[2]._EmissionColor", b.propertyName));
    }

    [Fact]
    public void RelativePath_IsCorrectInBinding()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "Body");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = renderer,
                MaterialIndex = 0,
                PropertyNames = new List<string> { "_EmissionColor" },
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "T");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        Assert.All(bindings, b => Assert.Equal("Body", b.path));
    }

    [Fact]
    public void NullRenderer_IsSkipped()
    {
        var avatarRoot = new GameObject("Root");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = null!,
                MaterialIndex = 0,
                PropertyNames = new List<string> { "_EmissionColor" },
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "T");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        Assert.Empty(bindings);
    }

    [Fact]
    public void MaterialIndex1_WithVariantMap_CreatesPPtrCurveNotFloatCurves()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "HMD2");
        var target = new EmissionTarget
        {
            Renderer      = renderer,
            MaterialIndex = 1,
            PropertyNames = new List<string> { "_Emission2ndColor" },
        };
        var targets    = new List<EmissionTarget> { target };
        var variantMat = new Material();
        var variantMap = new Dictionary<(EmissionTarget, string), Material>
        {
            { (target, "TestClip"), variantMat },
        };

        var clip = LipsyncLightBuilder.CreateEmissionClip(
            avatarRoot, targets, _ => Color.white, "TestClip", variantMap);

        // PPtrCurve が追加され float curve は追加されていない
        Assert.Empty(AnimationUtility.GetCurveBindings(clip));
        var pptrBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        Assert.Single(pptrBindings);
        Assert.Equal("m_Materials.Array.data[1]", pptrBindings[0].propertyName);
        Assert.True(pptrBindings[0].isPPtrCurve);
        Assert.Equal("HMD2", pptrBindings[0].path);
    }

    [Fact]
    public void MaterialIndex1_WithoutVariantMap_FallsBackToFloatCurves()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "Body");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = renderer,
                MaterialIndex = 1,
                PropertyNames = new List<string> { "_Emission2ndColor" },
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "Test");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        // variantMap なしのフォールバック: float curve が生成される
        Assert.Equal(4, bindings.Length);
    }

    [Fact]
    public void EmptyPropertyNames_IsSkipped()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "Body");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = renderer,
                MaterialIndex = 0,
                PropertyNames = new List<string>(), // 空
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "T");
        var bindings = AnimationUtility.GetCurveBindings(clip);

        Assert.Empty(bindings);
    }

    [Fact]
    public void ChannelSuffixes_ContainRGBA()
    {
        var (avatarRoot, renderer) = BuildHierarchy("Root", "Body");
        var targets = new List<EmissionTarget>
        {
            new EmissionTarget
            {
                Renderer      = renderer,
                MaterialIndex = 0,
                PropertyNames = new List<string> { "_EmissionColor" },
            }
        };

        var clip     = LipsyncLightBuilder.CreateEmissionClip(avatarRoot, targets, _ => Color.white, "T");
        var bindings = AnimationUtility.GetCurveBindings(clip);
        var props    = bindings.Select(b => b.propertyName).ToList();

        Assert.Contains("material._EmissionColor.r", props);
        Assert.Contains("material._EmissionColor.g", props);
        Assert.Contains("material._EmissionColor.b", props);
        Assert.Contains("material._EmissionColor.a", props);
    }
}
