using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;

using Assimp;

using NumericsQuaternion = System.Numerics.Quaternion;

namespace fin.model.io.exporters.assimp.indirect;

public sealed class AssimpIndirectAnimationFixer {
  private const float MODEL_SCALE = 100f;

  public static void Fix(IReadOnlyModel model, Scene sc) {
    sc.Animations.Clear();

    var finAnimations = model.AnimationManager.Animations;
    if (finAnimations.Count == 0) {
      return;
    }

    var nodesByName = new Dictionary<string, Node>(StringComparer.Ordinal);
    PopulateNodes_(sc.RootNode, nodesByName);

    foreach (var finAnimation in finAnimations) {
      var assAnimation = BuildAnimation_(
          finAnimation,
          model.Skeleton.Bones,
          nodesByName);
      if (assAnimation != null) {
        sc.Animations.Add(assAnimation);
      }
    }
  }

  private static Animation? BuildAnimation_(
      IReadOnlyModelAnimation finAnimation,
      IReadOnlyList<IReadOnlyBone> bones,
      IReadOnlyDictionary<string, Node> nodesByName) {
    var frameCount = Math.Max(finAnimation.FrameCount, 1);

    var translationsOrScales = new Vector3[frameCount];
    var rotations = new NumericsQuaternion[frameCount];

    var assAnimation = new Animation {
        Name = string.IsNullOrEmpty(finAnimation.Name)
            ? "animation"
            : finAnimation.Name,
        TicksPerSecond = finAnimation.FrameRate,
        DurationInTicks = Math.Max(frameCount - 1, 1),
    };

    foreach (var bone in bones) {
      if (string.IsNullOrEmpty(bone.Name)) {
        continue;
      }

      if (!nodesByName.TryGetValue(bone.Name, out var node)) {
        continue;
      }

      if (!finAnimation.BoneTracks.ContainsKey(bone)) {
        continue;
      }

      var boneTracks = finAnimation.BoneTracks[bone];

      var hasTranslations = boneTracks.Translations?.HasAnyData ?? false;
      var hasRotations = boneTracks.Rotations?.HasAnyData ?? false;
      var hasScales = boneTracks.Scales?.HasAnyData ?? false;
      if (!hasTranslations && !hasRotations && !hasScales) {
        continue;
      }

      var channel = new NodeAnimationChannel { NodeName = node.Name };

      if (hasTranslations) {
        boneTracks.Translations!.GetAllFrames(translationsOrScales);
        for (var frame = 0; frame < translationsOrScales.Length; ++frame) {
          channel.PositionKeys.Add(CreateVectorKey_(
              frame,
              translationsOrScales[frame] * MODEL_SCALE));
        }
      }

      if (hasRotations) {
        boneTracks.Rotations!.GetAllFrames(rotations);
        for (var frame = 0; frame < rotations.Length; ++frame) {
          channel.RotationKeys.Add(CreateQuaternionKey_(frame, rotations[frame]));
        }
      }

      if (hasScales) {
        boneTracks.Scales!.GetAllFrames(translationsOrScales);
        for (var frame = 0; frame < translationsOrScales.Length; ++frame) {
          channel.ScalingKeys.Add(CreateVectorKey_(
              frame,
              translationsOrScales[frame]));
        }
      }

      assAnimation.NodeAnimationChannels.Add(channel);
    }

    return assAnimation.NodeAnimationChannels.Count > 0
        ? assAnimation
        : null;
  }

  private static VectorKey CreateVectorKey_(double time, Vector3 value) {
    var ctor = typeof(VectorKey).GetConstructors()
                                .FirstOrDefault(c => c.GetParameters().Length == 2)
               ?? throw new InvalidOperationException(
                   "Could not find a usable VectorKey constructor.");

    var valueType = ctor.GetParameters()[1].ParameterType;
    var assimpValue = CreateVectorLikeValue_(valueType, value.X, value.Y, value.Z);
    return (VectorKey) ctor.Invoke([time, assimpValue]);
  }

  private static QuaternionKey CreateQuaternionKey_(
      double time,
      NumericsQuaternion value) {
    var ctor = typeof(QuaternionKey).GetConstructors()
                                    .FirstOrDefault(c => c.GetParameters().Length == 2)
               ?? throw new InvalidOperationException(
                   "Could not find a usable QuaternionKey constructor.");

    var valueType = ctor.GetParameters()[1].ParameterType;
    var assimpValue = CreateQuaternionLikeValue_(valueType, value);
    return (QuaternionKey) ctor.Invoke([time, assimpValue]);
  }

  private static object CreateVectorLikeValue_(Type type,
                                               float x,
                                               float y,
                                               float z) {
    if (type == typeof(Vector3)) {
      return new Vector3(x, y, z);
    }

    var instance = Activator.CreateInstance(type);
    if (instance != null &&
        TrySetComponent_(instance, type, "X", x) &&
        TrySetComponent_(instance, type, "Y", y) &&
        TrySetComponent_(instance, type, "Z", z)) {
      return instance;
    }

    var ctor = type.GetConstructor([typeof(float), typeof(float), typeof(float)]);
    if (ctor != null) {
      return ctor.Invoke([x, y, z]);
    }

    throw new InvalidOperationException(
        $"Could not construct vector value for Assimp type '{type.FullName}'.");
  }

  private static object CreateQuaternionLikeValue_(Type type,
                                                   NumericsQuaternion value) {
    if (type == typeof(NumericsQuaternion)) {
      return value;
    }

    var instance = Activator.CreateInstance(type);
    if (instance != null &&
        TrySetComponent_(instance, type, "X", value.X) &&
        TrySetComponent_(instance, type, "Y", value.Y) &&
        TrySetComponent_(instance, type, "Z", value.Z) &&
        TrySetComponent_(instance, type, "W", value.W)) {
      return instance;
    }

    var ctor = type.GetConstructor(
        [typeof(float), typeof(float), typeof(float), typeof(float)]);
    if (ctor != null) {
      return ctor.Invoke([value.X, value.Y, value.Z, value.W]);
    }

    throw new InvalidOperationException(
        $"Could not construct quaternion value for Assimp type '{type.FullName}'.");
  }

  private static bool TrySetComponent_(object instance,
                                       Type type,
                                       string name,
                                       float value) {
    var property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
    if (property?.CanWrite == true) {
      property.SetValue(instance, value);
      return true;
    }

    var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
    if (field != null) {
      field.SetValue(instance, value);
      return true;
    }

    return false;
  }

  private static void PopulateNodes_(
      Node node,
      IDictionary<string, Node> nodesByName) {
    if (!string.IsNullOrEmpty(node.Name)) {
      nodesByName[node.Name] = node;
    }

    foreach (var child in node.Children) {
      PopulateNodes_(child, nodesByName);
    }
  }
}
