using System;
using System.Collections.Generic;
using System.Numerics;

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
      if (string.IsNullOrEmpty(bone.Name) ||
          !nodesByName.TryGetValue(bone.Name, out var node) ||
          !finAnimation.BoneTracks.TryGetValue(bone, out var boneTracks)) {
        continue;
      }

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
          channel.PositionKeys.Add(new VectorKey(
              frame,
              ToAssimpVector_(translationsOrScales[frame] * MODEL_SCALE)));
        }
      }

      if (hasRotations) {
        boneTracks.Rotations!.GetAllFrames(rotations);
        for (var frame = 0; frame < rotations.Length; ++frame) {
          channel.RotationKeys.Add(new QuaternionKey(
              frame,
              ToAssimpQuaternion_(rotations[frame])));
        }
      }

      if (hasScales) {
        boneTracks.Scales!.GetAllFrames(translationsOrScales);
        for (var frame = 0; frame < translationsOrScales.Length; ++frame) {
          channel.ScalingKeys.Add(new VectorKey(
              frame,
              ToAssimpVector_(translationsOrScales[frame])));
        }
      }

      assAnimation.NodeAnimationChannels.Add(channel);
    }

    return assAnimation.NodeAnimationChannels.Count > 0
        ? assAnimation
        : null;
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

  private static Vector3D ToAssimpVector_(Vector3 value)
    => new() { X = value.X, Y = value.Y, Z = value.Z };

  private static Assimp.Quaternion ToAssimpQuaternion_(NumericsQuaternion value)
    => new() { X = value.X, Y = value.Y, Z = value.Z, W = value.W };
}
