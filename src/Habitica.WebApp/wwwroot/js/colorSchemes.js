(function () {
  const selectedKey = "habitica-tool/colorScheme/selectedId";
  const activeKey = "habitica-tool/colorScheme/activeScheme";
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
    taskPositive: "--task-positive"
  };

  function normalizeScheme(scheme) {
    if (!scheme) {
      return null;
    }

    const tokens = scheme.tokens || scheme.Tokens;
    if (!tokens) {
      return null;
    }

    return {
      id: scheme.id || scheme.Id || "alpha",
      name: scheme.name || scheme.Name || "Alpha",
      tokens
    };
  }

  function readToken(tokens, key) {
    return tokens[key] || tokens[key[0].toUpperCase() + key.slice(1)];
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

  function applyAndStore(scheme) {
    const normalized = normalizeScheme(scheme);
    if (!normalized) {
      return;
    }

    applyColorScheme(normalized);
    try {
      window.localStorage.setItem(selectedKey, normalized.id);
      window.localStorage.setItem(activeKey, JSON.stringify(normalized));
    } catch {
      // Applying the scheme matters more than preserving the fast reload cache.
    }
  }

  window.HabiticaColorScheme = {
    applyColorScheme,
    applyStoredColorScheme,
    applyAndStore
  };

  applyStoredColorScheme();
})();
