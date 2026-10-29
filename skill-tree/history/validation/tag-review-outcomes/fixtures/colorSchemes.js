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

    const candidates = ["#000000", "#ffffff"];
    return candidates.reduce((best, candidate) =>
      contrastRatio(background, parseColor(candidate)) > contrastRatio(background, parseColor(best))
        ? candidate : best);
  }

  function colorAlpha(value) {
    if (!value) return 1;
    const rgba = value.match(/^rgba\(([^)]+)\)$/i);
    if (rgba) {
      const alpha = Number(rgba[1].split(",")[3]);
      return Number.isFinite(alpha) ? Math.max(0, Math.min(1, alpha)) : 1;
    }
    const hex = value.trim().replace(/^#/, "");
    if (/^[0-9a-f]{8}$/i.test(hex)) return Number.parseInt(hex.slice(6), 16) / 255;
    if (/^[0-9a-f]{4}$/i.test(hex)) return Number.parseInt(hex[3] + hex[3], 16) / 255;
    return 1;
  }

  function surfaceColors(tokens) {
    const base = parseColor(readToken(tokens, "background")) || { red: 255, green: 255, blue: 255 };
    const over = (value, under) => {
      const parsed = parseColor(value);
      if (!parsed) return under;
      const alpha = colorAlpha(value);
      return Object.fromEntries(["red", "green", "blue"].map(key => [key, parsed[key] * alpha + under[key] * (1 - alpha)]));
    };
    const stops = name => Object.values(readToken(tokens, name) || {}).filter(value => typeof value === "string");
    const backgrounds = [base, ...stops("backgroundGradient").map(value => over(value, base))];
    const samples = backgrounds.flatMap(background => [background,
      ...["cardBackground", "surface", "surfaceStrong", "inputBackground"].map(name => over(readToken(tokens, name) || "", background)),
      ...stops("cardGradient").map(value => over(value, background))]);
    return samples.flatMap(surface => [surface, ...["primary", "accent", "danger", "success"].flatMap(name => {
      const tint = parseColor(readToken(tokens, name));
      return tint ? [.08, .16].map(alpha => Object.fromEntries(["red", "green", "blue"].map(key => [key, tint[key] * alpha + surface[key] * (1 - alpha)]))) : [];
    })]);
  }

  function readableSurfaceText(preferredValue, inkValue, samples) {
    const levels = samples.map(luminance);
    const score = (candidate, alpha = 1) => {
      const level = luminance(candidate);
      return Math.min(...levels.map((other, index) => {
        const foreground = alpha === 1 ? level : luminance(Object.fromEntries(["red", "green", "blue"].map(key => [key, candidate[key] * alpha + samples[index][key] * (1 - alpha)])));
        return (Math.max(foreground, other) + .05) / (Math.min(foreground, other) + .05);
      }));
    };
    const preferred = parseColor(preferredValue), ink = parseColor(inkValue);
    if (preferred && score(preferred, colorAlpha(preferredValue)) >= 4.5) return preferredValue;
    if (preferred && ink) {
      for (let step = 1; step <= 10; step++) {
        const mixed = Object.fromEntries(["red", "green", "blue"].map(key => [key, Math.round(preferred[key] * (1 - step / 10) + ink[key] * step / 10)]));
        if (score(mixed) >= 4.5) return `rgb(${mixed.red}, ${mixed.green}, ${mixed.blue})`;
      }
    }
    const candidates = [inkValue, "#000000", "#ffffff"].filter(value => parseColor(value));
    return candidates.reduce((best, value) => score(parseColor(value), colorAlpha(value)) > score(parseColor(best), colorAlpha(best)) ? value : best);
  }

  function applyDerivedVariables(root, tokens) {
    const samples = surfaceColors(tokens);
    for (const name of ["primary", "accent", "danger", "success", "muted"]) {
      root.style.setProperty(`--text-${name}`, readableSurfaceText(readToken(tokens, name), readToken(tokens, "ink"), samples));
    }
    const drawerBackground = readToken(tokens, "drawerBackground");
    const drawerText = readToken(tokens, "drawerText");
    const inputBackground = parseColor(readToken(tokens, "inputBackground"));
    const readableDrawerText = readableTextFor(drawerBackground, drawerText);

    root.style.setProperty("--drawer-readable-text", readableDrawerText);
    root.style.setProperty("--drawer-readable-muted", `color-mix(in srgb, ${readableDrawerText} 82%, transparent)`);
    root.style.setProperty("--native-control-scheme", inputBackground && luminance(inputBackground) < 0.46 ? "dark" : "light");
    root.style.setProperty("--progress-track", `color-mix(in srgb, ${readToken(tokens, "primary") || "var(--primary)"} 14%, transparent)`);

    // Button and app-bar foregrounds are derived per-background so text stays legible on every
    // scheme. Each filled button paints over a different color (primary/accent/danger/success).
    // Primary and secondary buttons get their own authored token; danger/success reuse the primary
    // button token as the preferred hint (readableTextFor still auto-corrects for contrast).
    const primaryButtonText = readToken(tokens, "primaryButtonText");
    const secondaryButtonText = readToken(tokens, "secondaryButtonText") || primaryButtonText;
    root.style.setProperty("--button-text", readableTextFor(readToken(tokens, "primary"), primaryButtonText));
    root.style.setProperty("--button-text-accent", readableTextFor(readToken(tokens, "accent"), secondaryButtonText));
    root.style.setProperty("--button-text-danger", readableTextFor(readToken(tokens, "danger"), primaryButtonText));
    root.style.setProperty("--button-text-success", readableTextFor(readToken(tokens, "success"), primaryButtonText));
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

  let taskColorContext;
  function refreshTaskContrast() {
    if (!document.querySelectorAll || typeof getComputedStyle !== "function") return;
    const ink = getComputedStyle(document.documentElement).getPropertyValue("--ink").trim();
    const updates = [];
    for (const card of document.querySelectorAll(".task-card")) {
      let backgroundValue = getComputedStyle(card).backgroundColor;
      if (!parseColor(backgroundValue)) {
        taskColorContext ??= document.createElement("canvas").getContext("2d", { willReadFrequently: true });
        taskColorContext.clearRect(0, 0, 1, 1);
        taskColorContext.fillStyle = backgroundValue; taskColorContext.fillRect(0, 0, 1, 1);
        const rgb = taskColorContext.getImageData(0, 0, 1, 1).data;
        backgroundValue = `rgb(${rgb[0]}, ${rgb[1]}, ${rgb[2]})`;
      }
      const foreground = readableTextFor(backgroundValue, ink);
      if (foreground && card.style.getPropertyValue("--task-readable-ink") !== foreground)
        updates.push([card, foreground]);
    }
    for (const [card, foreground] of updates) card.style.setProperty("--task-readable-ink", foreground);
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
    refreshTaskContrast();

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

  function applyAndStore(scheme, preferences, apply = true) {
    const normalized = normalizeScheme(scheme);
    if (!normalized) {
      return;
    }

    if (apply) applyColorScheme(normalized);
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
    refreshTaskContrast,
    applyStoredColorScheme,
    getPreferences,
    applyAndStore
  };

  applyStoredColorScheme();
})();
