(function () {
  const selectedKey = "habitica-tool/colorScheme/selectedId";
  const activeKey = "habitica-tool/colorScheme/activeScheme";
  const preferencesKey = "habitica-tool/colorScheme/preferences";
  const tokenMap = {
    background: "--bg",
    cardBackground: "--card-bg",
    cardBorder: "--card-border",
    ink: "--ink",
    muted: "--muted",
    primary: ["--primary", "--teal"],
    accent: ["--accent", "--gold"],
    danger: "--danger",
    success: "--success",
    focus: "--focus",
    shadow: "--shadow",
    surface: "--surface",
    surfaceStrong: "--surface-strong",
    chartPrimary: "--chart-primary",
    chartSecondary: "--chart-secondary",
    taskNegative: "--task-negative",
    taskNeutral: "--task-neutral",
    taskPositive: "--task-positive",
    appBarBackground: "--appbar-bg",
    appBarText: "--appbar-text",
    drawerBackground: "--drawer-bg",
    drawerText: "--drawer-text",
    buttonText: "--button-text",
    disabledBackground: "--disabled-bg",
    disabledText: "--disabled-text",
    disabledBorder: "--disabled-border",
    inputBackground: "--input-bg",
    inputBorder: "--input-border"
  };

  function parseHexColor(value) {
    const hex = value.trim().replace(/^#/, "");
    if (![3, 4, 6, 8].includes(hex.length)) {
      return null;
    }

    const expanded = hex.length <= 4
      ? hex.split("").map((part) => part + part).join("")
      : hex;
    const red = Number.parseInt(expanded.slice(0, 2), 16);
    const green = Number.parseInt(expanded.slice(2, 4), 16);
    const blue = Number.parseInt(expanded.slice(4, 6), 16);
    if ([red, green, blue].some((part) => Number.isNaN(part))) {
      return null;
    }

    return { red, green, blue };
  }

  function parseRgbColor(value) {
    const match = value.trim().match(/^rgba?\(([^)]+)\)$/i);
    if (!match) {
      return null;
    }

    const parts = match[1].split(",").map((part) => Number.parseFloat(part.trim()));
    if (parts.length < 3 || parts.slice(0, 3).some((part) => Number.isNaN(part))) {
      return null;
    }

    return {
      red: Math.min(255, Math.max(0, parts[0])),
      green: Math.min(255, Math.max(0, parts[1])),
      blue: Math.min(255, Math.max(0, parts[2]))
    };
  }

  function parseColor(value) {
    if (!value) {
      return null;
    }

    return value.trim().startsWith("#") ? parseHexColor(value) : parseRgbColor(value);
  }

  function luminance(color) {
    const channels = [color.red, color.green, color.blue].map((channel) => {
      const normalized = channel / 255;
      return normalized <= 0.03928
        ? normalized / 12.92
        : Math.pow((normalized + 0.055) / 1.055, 2.4);
    });
    return 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
  }

  function contrastRatio(first, second) {
    const firstLuminance = luminance(first);
    const secondLuminance = luminance(second);
    const lighter = Math.max(firstLuminance, secondLuminance);
    const darker = Math.min(firstLuminance, secondLuminance);
    return (lighter + 0.05) / (darker + 0.05);
  }

  function readableTextFor(backgroundValue, preferredTextValue) {
    const background = parseColor(backgroundValue);
    const preferredText = parseColor(preferredTextValue);
    if (!background) {
      return preferredTextValue;
    }

    if (preferredText && contrastRatio(background, preferredText) >= 4.5) {
      return preferredTextValue;
    }

    return luminance(background) > 0.46 ? "#162423" : "#f5efe2";
  }

  function applyDerivedVariables(root, tokens) {
    const drawerBackground = readToken(tokens, "drawerBackground");
    const drawerText = readToken(tokens, "drawerText");
    const inputBackground = parseColor(readToken(tokens, "inputBackground"));
    const readableDrawerText = readableTextFor(drawerBackground, drawerText);

    root.style.setProperty("--drawer-readable-text", readableDrawerText);
    root.style.setProperty("--drawer-readable-muted", `color-mix(in srgb, ${readableDrawerText} 82%, transparent)`);
    root.style.setProperty("--native-control-scheme", inputBackground && luminance(inputBackground) < 0.46 ? "dark" : "light");
    root.style.setProperty("--progress-track", `color-mix(in srgb, ${readToken(tokens, "primary") || "var(--primary)"} 14%, transparent)`);

    // Button and app-bar foregrounds are derived per-background so text stays legible on every
    // scheme. Each filled button paints over a different color (primary/accent/danger/success), so a
    // single authored ButtonText token cannot be correct for all of them.
    const buttonText = readToken(tokens, "buttonText");
    root.style.setProperty("--button-text", readableTextFor(readToken(tokens, "primary"), buttonText));
    root.style.setProperty("--button-text-accent", readableTextFor(readToken(tokens, "accent"), buttonText));
    root.style.setProperty("--button-text-danger", readableTextFor(readToken(tokens, "danger"), buttonText));
    root.style.setProperty("--button-text-success", readableTextFor(readToken(tokens, "success"), buttonText));
    root.style.setProperty("--appbar-text", readableTextFor(readToken(tokens, "appBarBackground"), readToken(tokens, "appBarText")));
  }

  function normalizeScheme(scheme) {
    if (!scheme) {
      return null;
    }

    const tokens = scheme.tokens || scheme.Tokens;
    if (!tokens) {
      return null;
    }

    return {
      id: scheme.id || scheme.Id || "gryphy-light",
      name: scheme.name || scheme.Name || "Gryphy (Light)",
      isDark: scheme.isDark ?? scheme.IsDark ?? false,
      tokens
    };
  }

  function readToken(tokens, key) {
    return tokens[key] || tokens[key[0].toUpperCase() + key.slice(1)];
  }

  function paintStopsToDataUrl(width, height, stops) {
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext("2d");
    for (let y = 0; y < height; y++) {
      for (let x = 0; x < width; x++) {
        ctx.fillStyle = stops[y * width + x];
        ctx.fillRect(x, y, 1, 1);
      }
    }

    return canvas.toDataURL("image/png");
  }

  function averageStops(stops, fallback) {
    const colors = stops.map(parseColor);
    if (colors.some((color) => !color)) {
      return fallback;
    }

    const average = (key) => Math.round(colors.reduce((total, color) => total + color[key], 0) / colors.length);
    return `rgb(${average("red")}, ${average("green")}, ${average("blue")})`;
  }

  function readStop(gradient, name) {
    const camel = name[0].toLowerCase() + name.slice(1);
    return gradient[camel] || gradient[name];
  }

  function setStops(root, prefix, gradient, names) {
    const suffixes = {
      topLeft: "tl", top: "t", topRight: "tr",
      middleLeft: "ml", middle: "m", middleRight: "mr",
      bottomLeft: "bl", bottom: "b", bottomRight: "br"
    };
    for (const name of names) {
      root.style.setProperty(`--${prefix}-grad-${suffixes[name] || name}`, gradient ? readStop(gradient, name) : "initial");
    }
  }

  function applyRasterGradient(root, tokens, tokenName, cssName, prefix, width, height, names, fallback) {
    const gradient = readToken(tokens, tokenName);
    setStops(root, prefix, gradient, names);
    if (!gradient) {
      root.style.setProperty(cssName, fallback);
      return;
    }

    let stops = names.map((name) => readStop(gradient, name));
    if (tokenName === "cardGradient") {
      stops = [...stops.slice(0, 4), averageStops(stops, readToken(tokens, "cardBackground")), ...stops.slice(4)];
    }

    const url = paintStopsToDataUrl(width, height, stops);
    root.style.setProperty(cssName, `url("${url}") center/100% 100% no-repeat, ${fallback}`);
  }

  function applyLinearGradient(root, tokens, tokenName, cssName, prefix, fallback) {
    const gradient = readToken(tokens, tokenName);
    setStops(root, prefix, gradient, ["start", "end"]);
    root.style.setProperty(cssName, gradient
      ? `linear-gradient(180deg, ${readStop(gradient, "start")}, ${readStop(gradient, "end")})`
      : fallback);
  }

  function applyGradientVariables(root, tokens) {
    applyRasterGradient(root, tokens, "backgroundGradient", "--bg-gradient", "bg", 3, 3,
      ["tl", "t", "tr", "ml", "m", "mr", "bl", "b", "br"].map((part) => ({
        tl: "topLeft", t: "top", tr: "topRight", ml: "middleLeft", m: "middle", mr: "middleRight", bl: "bottomLeft", b: "bottom", br: "bottomRight"
      })[part]), "var(--bg)");
    applyRasterGradient(root, tokens, "cardGradient", "--card-gradient", "card", 3, 3,
      ["topLeft", "top", "topRight", "middleLeft", "middleRight", "bottomLeft", "bottom", "bottomRight"], "var(--card-bg)");
    applyRasterGradient(root, tokens, "appBarGradient", "--appbar-gradient", "appbar", 3, 2,
      ["topLeft", "top", "topRight", "bottomLeft", "bottom", "bottomRight"], "var(--appbar-bg)");
    applyRasterGradient(root, tokens, "drawerGradient", "--drawer-gradient", "drawer", 3, 2,
      ["topLeft", "top", "topRight", "bottomLeft", "bottom", "bottomRight"], "var(--drawer-bg)");
    applyRasterGradient(root, tokens, "primaryButtonGradient", "--primary-btn-gradient", "primary-btn", 2, 2,
      ["topLeft", "topRight", "bottomLeft", "bottomRight"], "var(--primary)");
    applyLinearGradient(root, tokens, "secondaryButtonGradient", "--secondary-btn-gradient", "secondary-btn", "var(--accent)");
    applyLinearGradient(root, tokens, "accentChipGradient", "--accent-chip-gradient", "accent-chip", "var(--accent)");
    root.style.setProperty("--heading-text-shadow", readToken(tokens, "headingTextShadow") || "none");
    root.style.setProperty("--appbar-text-shadow", readToken(tokens, "appBarTextShadow") || "none");
    root.style.setProperty("--drawer-text-shadow", readToken(tokens, "drawerTextShadow") || "none");
  }

  function applyColorScheme(scheme) {
    const normalized = normalizeScheme(scheme);
    if (!normalized) {
      return;
    }

    const root = document.documentElement;
    for (const [key, cssVariables] of Object.entries(tokenMap)) {
      const value = readToken(normalized.tokens, key);
      if (value) {
        const variables = Array.isArray(cssVariables) ? cssVariables : [cssVariables];
        for (const cssVariable of variables) {
          root.style.setProperty(cssVariable, value);
        }
      }
    }
    applyGradientVariables(root, normalized.tokens);
    applyDerivedVariables(root, normalized.tokens);

    root.dataset.colorScheme = normalized.id;
    const themeColor = readToken(normalized.tokens, "primary") || readToken(normalized.tokens, "background");
    const themeMeta = document.querySelector("meta[name='theme-color']");
    if (themeColor && themeMeta) {
      themeMeta.setAttribute("content", themeColor);
    }
  }

  function applyStoredColorScheme() {
    try {
      const stored = window.localStorage.getItem(activeKey);
      if (stored) {
        applyColorScheme(JSON.parse(stored));
      }
    } catch {
      window.localStorage.removeItem(activeKey);
    }
  }

  function getPreferences() {
    try {
      const stored = window.localStorage.getItem(preferencesKey);
      if (stored) {
        return JSON.parse(stored);
      }

      const active = normalizeScheme(JSON.parse(window.localStorage.getItem(activeKey) || "null"));
      if (!active) {
        return null;
      }

      return {
        selectedSchemeId: active.id,
        schemaVersion: 2,
        customSchemes: active.id.startsWith("custom-")
          ? [{ id: active.id, name: active.name, isBuiltIn: false, isDark: active.isDark, tokens: active.tokens }]
          : []
      };
    } catch {
      window.localStorage.removeItem(preferencesKey);
      return null;
    }
  }

  function applyAndStore(scheme, preferences) {
    const normalized = normalizeScheme(scheme);
    if (!normalized) {
      return;
    }

    applyColorScheme(normalized);
    try {
      window.localStorage.setItem(selectedKey, normalized.id);
      window.localStorage.setItem(activeKey, JSON.stringify(normalized));
      if (preferences) {
        window.localStorage.setItem(preferencesKey, JSON.stringify(preferences));
      }
    } catch {
      // Applying the scheme matters more than preserving the fast reload cache.
    }
  }

  window.HabiticaColorScheme = {
    applyColorScheme,
    applyStoredColorScheme,
    getPreferences,
    applyAndStore
  };

  applyStoredColorScheme();
})();
