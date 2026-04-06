using System;
using System.Linq;

using fin.image.util;

namespace fin.model.util;

public static class PrimaryTextureFinder {
  private static void DebugChoice_(IReadOnlyMaterial material,
                                   string reason,
                                   IReadOnlyTexture? chosen) {
    var textureNames = string.Join(", ",
        material.Textures.Select(t => t?.Name ?? "<null>"));

    Console.WriteLine(
        $"[PTFDBG] material='{material.Name ?? "<null>"}' " +
        $"type='{material.GetType().FullName}' " +
        $"reason='{reason}' " +
        $"chosen='{chosen?.Name ?? "<null>"}' " +
        $"textureCount={material.Textures.Count()} " +
        $"textures=[{textureNames}]");
  }

  public static IReadOnlyTexture? GetFor(IReadOnlyMaterial material) {
    if (material is IReadOnlyNullMaterial
                    or IReadOnlyHiddenMaterial
                    or IReadOnlyColorMaterial) {
      DebugChoice_(material, "null/hidden/color material", null);
      return null;
    }

    if (material is IReadOnlyFixedFunctionMaterial fixedFunctionMaterial) {
      var chosen = GetFor(fixedFunctionMaterial);
      DebugChoice_(material, "fixed function material", chosen);
      return chosen;
    }

    if (material is IReadOnlyTextureMaterial textureMaterial) {
      var chosen = GetFor(textureMaterial);
      DebugChoice_(material, "texture material", chosen);
      return chosen;
    }

    if (material is IReadOnlyStandardMaterial standardMaterial) {
      var chosen = GetFor(standardMaterial);
      DebugChoice_(material, "standard material", chosen);
      return chosen;
    }

    throw new NotImplementedException();
  }

  public static IReadOnlyTexture? GetFor(IReadOnlyTextureMaterial material)
    => material.Texture;

  public static IReadOnlyTexture? GetFor(
      IReadOnlyFixedFunctionMaterial material) {
    var textures = material.Textures;

    var compiledTexture = material.CompiledTexture;
    if (compiledTexture != null) {
      DebugChoice_(material, "compiled texture", compiledTexture);
      return compiledTexture;
    }

    var prioritizedTextures = textures
        .OrderByDescending(texture => texture.ColorType == ColorType.COLOR)
        .ThenByDescending(texture =>
            TransparencyTypeUtil.GetTransparencyType(texture.Image) ==
            TransparencyType.OPAQUE)
        .ToArray();

    if (prioritizedTextures.Length > 0) {
      DebugChoice_(material, "prioritized first texture", prioritizedTextures[0]);
      return prioritizedTextures[0];
    }

    var fallback = material.Textures.LastOrDefault((IReadOnlyTexture?) null);
    DebugChoice_(material, "last texture fallback", fallback);
    return fallback;
  }

  public static IReadOnlyTexture? GetFor(IReadOnlyStandardMaterial material) {
    var chosen = material.DiffuseTexture ?? material.AmbientOcclusionTexture;
    DebugChoice_(material, "DiffuseTexture ?? AmbientOcclusionTexture", chosen);
    return chosen;
  }
}
