using System;

namespace fin.model.io.exporters;

internal static class ExporterRuntimeOptions {
  public static bool AnimationOnly =>
      GetBooleanEnvironmentVariable_("UNIVERSAL_ASSET_TOOL_ANIMATION_ONLY") ||
      GetBooleanEnvironmentVariable_("MELTY_ANIMATION_ONLY");

  private static bool GetBooleanEnvironmentVariable_(string name) {
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value)) {
      return false;
    }

    return value.Equals("1", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
           value.Equals("on", StringComparison.OrdinalIgnoreCase);
  }
}
