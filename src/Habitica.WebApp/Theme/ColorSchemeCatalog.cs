using System.Text.RegularExpressions;

namespace Habitica.WebApp.Theme;

public static partial class ColorSchemeCatalog
{
    public const string AlphaId = "alpha";
    public const string DefaultLightSchemeId = "gryphy-light";
    public const string DefaultDarkSchemeId = "gryphy-dark";
    public const string ForestLegacyId = "forest-legacy";

    private static IReadOnlyList<ColorSchemeDefinition> LegacySchemes { get; } = new[]
    {
        new ColorSchemeDefinition(
            AlphaId,
            "Alpha (Light)",
            true,
            new ColorSchemeTokens(
                "#f5efe2",
                "rgba(255, 250, 241, 0.92)",
                "rgba(23, 63, 59, 0.12)",
                "#162423",
                "#5f6d67",
                "#2d746e",
                "#c5772b",
                "#a13f35",
                "#2d746e",
                "#43a397",
                "0 24px 60px rgba(22, 36, 35, 0.12)",
                "rgba(255, 255, 255, 0.72)",
                "rgba(255, 250, 241, 0.94)",
                "#2d746e",
                "#c5772b",
                "#d6e7e1",
                "#eaf3ee",
                "#f8fcfa",
                "#173f3b",
                "#f5efe2",
                "#163431",
                "#f5efe2",
                "#ffffff",
                "rgba(22, 36, 35, 0.08)",
                "rgba(95, 109, 103, 0.58)",
                "rgba(95, 109, 103, 0.28)",
                "rgba(255, 255, 255, 0.9)",
                "rgba(22, 36, 35, 0.12)")),
        new ColorSchemeDefinition(
            "habitica",
            "Habitica (Light)",
            true,
            new ColorSchemeTokens(
                "#f7f4ff",
                "rgba(255, 255, 255, 0.94)",
                "rgba(79, 42, 147, 0.16)",
                "#24113f",
                "#6f6384",
                "#6133b4",
                "#ffbe5d",
                "#de3f5f",
                "#24cc8f",
                "#2995cd",
                "0 24px 60px rgba(44, 24, 82, 0.14)",
                "rgba(255, 255, 255, 0.78)",
                "rgba(255, 255, 255, 0.96)",
                "#6133b4",
                "#2995cd",
                "#ded3fa",
                "#eee8ff",
                "#faf7ff",
                "#4f2a93",
                "#fff7ff",
                "#2f1d52",
                "#fff7ff",
                "#ffffff",
                "rgba(79, 42, 147, 0.08)",
                "rgba(111, 99, 132, 0.62)",
                "rgba(111, 99, 132, 0.28)",
                "rgba(255, 255, 255, 0.94)",
                "rgba(79, 42, 147, 0.16)")),
        new ColorSchemeDefinition(
            "gryphy-light",
            "Gryphy (Light)",
            true,
            new ColorSchemeTokens(
                "#f7f1ff",
                "rgba(255, 252, 255, 0.94)",
                "rgba(103, 49, 184, 0.16)",
                "#201136",
                "#6a5c77",
                "#7b2dd6",
                "#f0a400",
                "#d94768",
                "#1f9f7a",
                "#3197e5",
                "0 24px 60px rgba(32, 17, 54, 0.14)",
                "rgba(255, 255, 255, 0.78)",
                "rgba(255, 252, 255, 0.96)",
                "#7b2dd6",
                "#3197e5",
                "#dfd1f8",
                "#eee6ff",
                "#fbf8ff",
                "#4f2380",
                "#fff7ff",
                "#2d1945",
                "#fff7ff",
                "#ffffff",
                "rgba(103, 49, 184, 0.08)",
                "rgba(106, 92, 119, 0.62)",
                "rgba(106, 92, 119, 0.28)",
                "rgba(255, 252, 255, 0.94)",
                "rgba(103, 49, 184, 0.16)")),
        new ColorSchemeDefinition(
            "gryphy-dark",
            "Gryphy (Dark)",
            true,
            new ColorSchemeTokens(
                "#12081f",
                "rgba(31, 15, 50, 0.94)",
                "rgba(178, 93, 255, 0.22)",
                "#fff7ff",
                "#c8b9d8",
                "#b25dff",
                "#ffcf26",
                "#ff6f8f",
                "#62d6bd",
                "#58b9ff",
                "0 24px 60px rgba(0, 0, 0, 0.36)",
                "rgba(50, 24, 80, 0.78)",
                "rgba(42, 20, 70, 0.96)",
                "#b25dff",
                "#58b9ff",
                "#241633",
                "#321f49",
                "#452b63",
                "#241336",
                "#fff7ff",
                "#1d102b",
                "#fff7ff",
                "#12081f",
                "rgba(202, 140, 255, 0.1)",
                "rgba(205, 189, 224, 0.54)",
                "rgba(202, 140, 255, 0.26)",
                "#21122f",
                "rgba(202, 140, 255, 0.3)")),
        new ColorSchemeDefinition(
            "midnight-tavern",
            "Midnight Tavern (Dark)",
            true,
            new ColorSchemeTokens(
                "#0d1117",
                "rgba(20, 27, 39, 0.94)",
                "rgba(107, 211, 210, 0.2)",
                "#f5f1e8",
                "#a9b7bd",
                "#6bd3d2",
                "#f2b84b",
                "#ff6b6b",
                "#79d38a",
                "#8bd3ff",
                "0 24px 60px rgba(0, 0, 0, 0.42)",
                "rgba(25, 34, 48, 0.82)",
                "rgba(31, 42, 59, 0.96)",
                "#6bd3d2",
                "#f2b84b",
                "#102326",
                "#173337",
                "#22484d",
                "#111c26",
                "#f5f1e8",
                "#091016",
                "#f5f1e8",
                "#091016",
                "rgba(245, 241, 232, 0.1)",
                "rgba(169, 183, 189, 0.58)",
                "rgba(107, 211, 210, 0.24)",
                "#172231",
                "rgba(107, 211, 210, 0.32)")),
        new ColorSchemeDefinition(
            "dragonfire-keep",
            "Dragonfire Keep (Dark)",
            true,
            new ColorSchemeTokens(
                "#160b08",
                "rgba(42, 20, 14, 0.94)",
                "rgba(255, 126, 53, 0.24)",
                "#fff1df",
                "#d9bca9",
                "#ff7e35",
                "#ffd166",
                "#ff4d5f",
                "#70d68c",
                "#ff9f6e",
                "0 24px 60px rgba(0, 0, 0, 0.44)",
                "rgba(58, 27, 18, 0.78)",
                "rgba(69, 31, 20, 0.96)",
                "#ff7e35",
                "#ffd166",
                "#32170f",
                "#452015",
                "#5c2c1d",
                "#44190f",
                "#fff1df",
                "#2a0f0a",
                "#fff1df",
                "#160b08",
                "rgba(255, 241, 223, 0.1)",
                "rgba(217, 188, 169, 0.56)",
                "rgba(255, 126, 53, 0.28)",
                "#32170f",
                "rgba(255, 126, 53, 0.34)")),
        new ColorSchemeDefinition(
            "neon-rogue",
            "Neon Rogue (Dark)",
            true,
            new ColorSchemeTokens(
                "#09071a",
                "rgba(18, 14, 43, 0.94)",
                "rgba(0, 231, 255, 0.24)",
                "#f7f7ff",
                "#bbb7df",
                "#00e7ff",
                "#ff3df2",
                "#ff4f7a",
                "#40f29a",
                "#9b7cff",
                "0 24px 60px rgba(0, 0, 0, 0.48)",
                "rgba(24, 19, 58, 0.82)",
                "rgba(31, 24, 75, 0.96)",
                "#00e7ff",
                "#ff3df2",
                "#092a33",
                "#0d3d49",
                "#125766",
                "#120d35",
                "#f7f7ff",
                "#0b0825",
                "#f7f7ff",
                "#09071a",
                "rgba(247, 247, 255, 0.1)",
                "rgba(187, 183, 223, 0.54)",
                "rgba(0, 231, 255, 0.3)",
                "#18133a",
                "rgba(0, 231, 255, 0.34)")),
        new ColorSchemeDefinition(
            "frost-healer",
            "Frost Healer (Light)",
            true,
            new ColorSchemeTokens(
                "#eef9ff",
                "rgba(255, 255, 255, 0.94)",
                "rgba(34, 124, 157, 0.16)",
                "#15313d",
                "#607986",
                "#227c9d",
                "#7d5fff",
                "#c84a68",
                "#1f9d7a",
                "#5bbfe8",
                "0 24px 60px rgba(21, 49, 61, 0.12)",
                "rgba(255, 255, 255, 0.78)",
                "rgba(248, 253, 255, 0.96)",
                "#227c9d",
                "#7d5fff",
                "#ccebf5",
                "#e5f6fb",
                "#f8fdff",
                "#19556d",
                "#f4fbff",
                "#164255",
                "#f4fbff",
                "#ffffff",
                "rgba(21, 49, 61, 0.08)",
                "rgba(96, 121, 134, 0.6)",
                "rgba(34, 124, 157, 0.24)",
                "rgba(255, 255, 255, 0.92)",
                "rgba(34, 124, 157, 0.16)")),
        new ColorSchemeDefinition(
            "sunlit-stable",
            "Sunlit Stable (Light)",
            true,
            new ColorSchemeTokens(
                "#fff6dc",
                "rgba(255, 253, 246, 0.94)",
                "rgba(151, 91, 38, 0.16)",
                "#312315",
                "#756756",
                "#2f8a7b",
                "#e39a2d",
                "#b64f44",
                "#3f9b62",
                "#63b6a6",
                "0 24px 60px rgba(49, 35, 21, 0.12)",
                "rgba(255, 255, 255, 0.72)",
                "rgba(255, 253, 246, 0.96)",
                "#2f8a7b",
                "#e39a2d",
                "#cde8df",
                "#e5f4ef",
                "#f8fcfa",
                "#286256",
                "#fff8e8",
                "#24483f",
                "#fff8e8",
                "#ffffff",
                "rgba(49, 35, 21, 0.08)",
                "rgba(117, 103, 86, 0.58)",
                "rgba(151, 91, 38, 0.24)",
                "rgba(255, 255, 255, 0.9)",
                "rgba(151, 91, 38, 0.16)")),
        new ColorSchemeDefinition(
            "mosswood-quest",
            "Mosswood Quest (Light)",
            true,
            new ColorSchemeTokens(
                "#edf2df",
                "rgba(252, 255, 244, 0.94)",
                "rgba(72, 106, 58, 0.18)",
                "#1f2d1d",
                "#66725e",
                "#4f8a46",
                "#a8702c",
                "#a9473f",
                "#3c8f68",
                "#78a85c",
                "0 24px 60px rgba(31, 45, 29, 0.12)",
                "rgba(255, 255, 255, 0.68)",
                "rgba(252, 255, 244, 0.96)",
                "#4f8a46",
                "#a8702c",
                "#d3e5cc",
                "#e9f2e4",
                "#f8fcf6",
                "#365d31",
                "#f7ffef",
                "#2d482b",
                "#f7ffef",
                "#ffffff",
                "rgba(31, 45, 29, 0.08)",
                "rgba(102, 114, 94, 0.58)",
                "rgba(72, 106, 58, 0.25)",
                "rgba(255, 255, 255, 0.88)",
                "rgba(72, 106, 58, 0.16)")),
        new ColorSchemeDefinition(
            "potion-shop",
            "Potion Shop (Light)",
            true,
            new ColorSchemeTokens(
                "#f6edff",
                "rgba(255, 250, 255, 0.94)",
                "rgba(136, 78, 191, 0.18)",
                "#261238",
                "#706079",
                "#8b3fd1",
                "#27a7a3",
                "#cf4772",
                "#2faf72",
                "#c267e8",
                "0 24px 60px rgba(38, 18, 56, 0.14)",
                "rgba(255, 255, 255, 0.74)",
                "rgba(255, 250, 255, 0.96)",
                "#8b3fd1",
                "#27a7a3",
                "#ded1f0",
                "#efe7fa",
                "#fbf8ff",
                "#5b2b83",
                "#fff7ff",
                "#3b2152",
                "#fff7ff",
                "#ffffff",
                "rgba(38, 18, 56, 0.08)",
                "rgba(112, 96, 121, 0.58)",
                "rgba(136, 78, 191, 0.26)",
                "rgba(255, 255, 255, 0.92)",
                "rgba(136, 78, 191, 0.18)")),
        new ColorSchemeDefinition(
            "boss-battle",
            "Boss Battle (Dark)",
            true,
            new ColorSchemeTokens(
                "#171414",
                "rgba(35, 31, 31, 0.94)",
                "rgba(235, 78, 85, 0.24)",
                "#fff0e8",
                "#c9b7ad",
                "#eb4e55",
                "#f0b64f",
                "#ff6b6b",
                "#78d085",
                "#ff8a75",
                "0 24px 60px rgba(0, 0, 0, 0.44)",
                "rgba(45, 39, 39, 0.82)",
                "rgba(55, 47, 47, 0.96)",
                "#eb4e55",
                "#f0b64f",
                "#351b1d",
                "#4a2428",
                "#633137",
                "#421f22",
                "#fff0e8",
                "#241616",
                "#fff0e8",
                "#171414",
                "rgba(255, 240, 232, 0.1)",
                "rgba(201, 183, 173, 0.56)",
                "rgba(235, 78, 85, 0.28)",
                "#2a2424",
                "rgba(235, 78, 85, 0.32)")),
        new ColorSchemeDefinition(
            "quiet-ledger",
            "Quiet Ledger (Light)",
            true,
            new ColorSchemeTokens(
                "#eef0ea",
                "rgba(250, 250, 246, 0.94)",
                "rgba(85, 96, 91, 0.16)",
                "#202624",
                "#66706c",
                "#536b63",
                "#8a7354",
                "#9d5550",
                "#557a61",
                "#7b8f89",
                "0 24px 60px rgba(32, 38, 36, 0.1)",
                "rgba(255, 255, 255, 0.68)",
                "rgba(250, 250, 246, 0.96)",
                "#536b63",
                "#8a7354",
                "#d9e0d8",
                "#e9ede8",
                "#f8faf7",
                "#3f554e",
                "#f7f8f4",
                "#35443f",
                "#f7f8f4",
                "#ffffff",
                "rgba(32, 38, 36, 0.08)",
                "rgba(102, 112, 108, 0.58)",
                "rgba(85, 96, 91, 0.24)",
                "rgba(255, 255, 255, 0.88)",
                "rgba(85, 96, 91, 0.16)")),
        new ColorSchemeDefinition(
            "celestial-inn",
            "Celestial Inn (Dark)",
            true,
            new ColorSchemeTokens(
                "#121c33",
                "rgba(25, 38, 67, 0.94)",
                "rgba(116, 161, 255, 0.22)",
                "#f3f7ff",
                "#bbc7df",
                "#74a1ff",
                "#e8c46a",
                "#ff6f8f",
                "#76d6aa",
                "#b38cff",
                "0 24px 60px rgba(0, 0, 0, 0.38)",
                "rgba(31, 48, 84, 0.8)",
                "rgba(36, 55, 96, 0.96)",
                "#74a1ff",
                "#e8c46a",
                "#1a2d54",
                "#263f73",
                "#345491",
                "#1a2d54",
                "#f3f7ff",
                "#101a31",
                "#f3f7ff",
                "#121c33",
                "rgba(243, 247, 255, 0.1)",
                "rgba(187, 199, 223, 0.56)",
                "rgba(116, 161, 255, 0.28)",
                "#1b2c50",
                "rgba(116, 161, 255, 0.32)")),
        new ColorSchemeDefinition(
            "mana-mirage",
            "Mana Mirage (Dark)",
            true,
            new ColorSchemeTokens(
                "#14001f",
                "rgba(38, 0, 63, 0.94)",
                "rgba(255, 52, 246, 0.28)",
                "#fff4ff",
                "#e5b8ff",
                "#00ffcc",
                "#ff34f6",
                "#ff3366",
                "#b8ff2c",
                "#ffe600",
                "0 24px 60px rgba(0, 0, 0, 0.52)",
                "rgba(43, 0, 69, 0.82)",
                "rgba(57, 0, 95, 0.96)",
                "#00ffcc",
                "#ff34f6",
                "#2a003f",
                "#410061",
                "#5e008a",
                "#25003d",
                "#fff4ff",
                "#090012",
                "#fff4ff",
                "#14001f",
                "rgba(255, 244, 255, 0.1)",
                "rgba(229, 184, 255, 0.56)",
                "rgba(255, 52, 246, 0.3)",
                "#26003f",
                "rgba(0, 255, 204, 0.34)")),
        new ColorSchemeDefinition(
            "mushroom-meadow",
            "Mushroom Meadow (Light)",
            true,
            new ColorSchemeTokens(
                "#fdf0ff",
                "rgba(255, 248, 255, 0.92)",
                "rgba(200, 0, 200, 0.18)",
                "#2a0a3d",
                "#7a5a8a",
                "#c800c8",
                "#00d97e",
                "#ff2e63",
                "#2faf72",
                "#ff8a00",
                "0 24px 60px rgba(120, 0, 120, 0.18)",
                "rgba(255, 255, 255, 0.74)",
                "rgba(255, 248, 255, 0.96)",
                "#c800c8",
                "#00d97e",
                "#e9b3f5",
                "#f3d6fb",
                "#fbf0ff",
                "#6a0080",
                "#fff0ff",
                "#2a0a3d",
                "#fff0ff",
                "#ffffff",
                "rgba(120, 0, 120, 0.08)",
                "rgba(122, 90, 138, 0.58)",
                "rgba(200, 0, 200, 0.26)",
                "rgba(255, 255, 255, 0.92)",
                "rgba(200, 0, 200, 0.18)")),
        new ColorSchemeDefinition(
            "mushroom-trip",
            "Mushroom Trip (Dark)",
            true,
            new ColorSchemeTokens(
                "#0a0014",
                "rgba(30, 0, 50, 0.94)",
                "rgba(255, 0, 200, 0.28)",
                "#f5e8ff",
                "#c89aff",
                "#ff00cc",
                "#00ffaa",
                "#ff2e63",
                "#7bff3d",
                "#ffe600",
                "0 24px 60px rgba(0, 0, 0, 0.52)",
                "rgba(40, 0, 65, 0.82)",
                "rgba(52, 0, 85, 0.96)",
                "#ff00cc",
                "#00ffaa",
                "#2a0040",
                "#3d005c",
                "#56007e",
                "#1a0028",
                "#f5e8ff",
                "#12001f",
                "#f5e8ff",
                "#0a0014",
                "rgba(245, 232, 255, 0.1)",
                "rgba(200, 154, 255, 0.56)",
                "rgba(255, 0, 200, 0.3)",
                "#20002f",
                "rgba(255, 0, 200, 0.34)")),
        new ColorSchemeDefinition(
            "frosted-cake",
            "Frosted Cake (Light)",
            true,
            new ColorSchemeTokens(
                "#fff0f6",
                "rgba(255, 250, 253, 0.92)",
                "rgba(255, 20, 147, 0.2)",
                "#3d0a2a",
                "#9a6a82",
                "#ff1493",
                "#00cfff",
                "#ff3b3b",
                "#19c37d",
                "#ffb300",
                "0 24px 60px rgba(180, 0, 90, 0.16)",
                "rgba(255, 255, 255, 0.74)",
                "rgba(255, 250, 253, 0.96)",
                "#ff1493",
                "#00cfff",
                "#ffc1dd",
                "#ffd9e8",
                "#fff0f6",
                "#ff1493",
                "#fff0f6",
                "#3d0a2a",
                "#fff0f6",
                "#ffffff",
                "rgba(180, 0, 90, 0.08)",
                "rgba(154, 106, 130, 0.58)",
                "rgba(255, 20, 147, 0.26)",
                "rgba(255, 255, 255, 0.92)",
                "rgba(255, 20, 147, 0.2)")),
        new ColorSchemeDefinition(
            "sugar-crash",
            "Sugar Crash (Dark)",
            true,
            new ColorSchemeTokens(
                "#0c0010",
                "rgba(28, 0, 36, 0.94)",
                "rgba(255, 222, 0, 0.26)",
                "#fff6e8",
                "#ffb3d9",
                "#ffde00",
                "#ff3df2",
                "#ff2e4f",
                "#39ff88",
                "#00e7ff",
                "0 24px 60px rgba(0, 0, 0, 0.55)",
                "rgba(40, 0, 52, 0.82)",
                "rgba(54, 0, 70, 0.96)",
                "#ffde00",
                "#ff3df2",
                "#2a0036",
                "#3d004f",
                "#56006e",
                "#1a0022",
                "#fff6e8",
                "#12001a",
                "#fff6e8",
                "#0c0010",
                "rgba(255, 246, 232, 0.1)",
                "rgba(255, 179, 217, 0.56)",
                "rgba(255, 222, 0, 0.3)",
                "#1e0026",
                "rgba(255, 222, 0, 0.34)")),
        new ColorSchemeDefinition(
            "neon-abyss-carnival",
            "Neon Abyss Carnival (Dark)",
            true,
            new ColorSchemeTokens(
                "#09051f",
                "rgba(24, 10, 55, 0.94)",
                "rgba(255, 74, 216, 0.34)",
                "#fff7ff",
                "#c8b8e8",
                "#00f0ff",
                "#ffea00",
                "#ff2f6d",
                "#47ff9c",
                "#b000ff",
                "0 24px 70px rgba(255, 47, 109, 0.22)",
                "rgba(38, 16, 85, 0.82)",
                "rgba(55, 20, 120, 0.96)",
                "#00f0ff",
                "#ffea00",
                "#31113f",
                "#1d3f73",
                "#0b6f5a",
                "#17002f",
                "#fff7ff",
                "#070316",
                "#fff7ff",
                "#09051f",
                "rgba(255, 247, 255, 0.1)",
                "rgba(200, 184, 232, 0.56)",
                "rgba(255, 74, 216, 0.28)",
                "#160b38",
                "rgba(0, 240, 255, 0.38)"))
    };

    public static IReadOnlyList<ColorSchemeDefinition> BuiltInSchemes { get; } = BuildBuiltInSchemes();

    private static IReadOnlyList<ColorSchemeDefinition> BuildBuiltInSchemes()
    {
        var sunlit = LegacyAs("sunlit-stable", "Sunlit Stable", isDark: false);
        var potion = LegacyAs("potion-shop", "Potion Shop", isDark: false);
        var ledger = LegacyAs("quiet-ledger", "Quiet Ledger", isDark: false);
        return new[]
        {
            BuiltIn(DefaultLightSchemeId, "Gryphy (Light)", isDark: false, Tokens(
                "#f7f1ff", "rgba(255, 252, 255, 0.94)", "rgba(103, 49, 184, 0.13)", "#2d2040", "#756881", "#7334bd", "#d99416",
                "#c84a67", "#2a9277", "#438fd0", "0 18px 44px rgba(32, 17, 54, 0.09)", "rgba(255, 255, 255, 0.72)", "rgba(255, 252, 255, 0.92)",
                "#7334bd", "#438fd0", "#e4d8f6", "#f0e9fb", "#faf7ff", "#684095", "#fff8ff", "#3b2356", "#f8f0ff", "#ffffff",
                "rgba(103, 49, 184, 0.06)", "rgba(117, 104, 129, 0.58)", "rgba(117, 104, 129, 0.22)", "rgba(255, 252, 255, 0.92)", "rgba(103, 49, 184, 0.13)")),
            BuiltIn(DefaultDarkSchemeId, "Gryphy (Dark)", isDark: true, Tokens(
                "#12091e", "rgba(30, 16, 47, 0.94)", "rgba(178, 93, 255, 0.18)", "#f3eaf6", "#b5a6c5", "#a765e2", "#d9b33a",
                "#e46380", "#58bfa9", "#5aa9df", "0 22px 52px rgba(0, 0, 0, 0.34)", "rgba(45, 24, 70, 0.76)", "rgba(38, 21, 61, 0.94)",
                "#a765e2", "#5aa9df", "#21172d", "#2d2040", "#3c2a55", "#241436", "#f3eaf6", "#211331", "#f3eaf6", "#140a20",
                "rgba(190, 140, 230, 0.085)", "rgba(181, 166, 197, 0.52)", "rgba(190, 140, 230, 0.22)", "#21142e", "rgba(190, 140, 230, 0.24)")),
            LegacyAs(AlphaId, "Forest Legacy", isDark: false, ForestLegacyId),
            LegacyAs("frosted-cake", "Frosted Cake", isDark: false),
            BuiltIn("arcane-wraith", "Arcane Wraith", isDark: true, Tokens(
                "#0b0920", "rgba(20, 17, 44, 0.94)", "rgba(68, 190, 210, 0.20)", "#ececf7", "#aaa7cb", "#42bfd2", "#d75ad2",
                "#df5d7d", "#55c894", "#907ee0", "0 22px 54px rgba(0, 0, 0, 0.42)", "rgba(25, 22, 56, 0.78)", "rgba(31, 27, 70, 0.92)",
                "#42bfd2", "#d75ad2", "#102631", "#183745", "#1b4c59", "#151037", "#ececf7", "#100d2b", "#ececf7", "#0b0920",
                "rgba(236, 236, 247, 0.08)", "rgba(170, 167, 203, 0.52)", "rgba(68, 190, 210, 0.22)", "#19163a", "rgba(68, 190, 210, 0.26)")),
            BuiltIn("phantom-fair", "Phantom Fair", isDark: true, Tokens(
                "#0b0820", "rgba(23, 13, 51, 0.94)", "rgba(220, 86, 200, 0.28)", "#eee8f2", "#aea2ce", "#43bfd2", "#d6bd42",
                "#df5578", "#5fc991", "#9b61d6", "0 24px 70px rgba(220, 55, 95, 0.20)", "rgba(34, 21, 75, 0.78)", "rgba(43, 27, 95, 0.92)",
                "#43bfd2", "#d6bd42", "#2a1738", "#24385f", "#17584a", "#1a1038", "#eee8f2", "#0d0924", "#eee8f2", "#0b0820",
                "rgba(238, 232, 242, 0.08)", "rgba(174, 162, 206, 0.52)", "rgba(220, 86, 200, 0.22)", "#18113a", "rgba(67, 191, 210, 0.28)")),
            BuiltIn("toxic-swamp", "Toxic Swamp", isDark: true, Tokens(
                "#10190f", "#1d2b1b", "#496a35", "#e8edd6", "#99aa83", "#9bdc2f", "#5b4fd6", "#c83a32", "#5fc94a", "#b6ef42",
                "0 22px 56px rgba(100, 180, 38, 0.16)", "#22331f", "#2c4725", "#9bdc2f", "#5b4fd6", "#3a1715", "#2b3827", "#173717",
                "#213f19", "#e8edd6", "#172814", "#e8edd6", "#10190f", "#2e3c2b", "#7f8d71", "#405234", "#1a2519", "#58723c")),
            BuiltIn("green-menace", "Green Menace", isDark: true, Tokens(
                "#120f12", "#332031", "#654461", "#e8dfd2", "#aeb9b4", "#c72ab7", "#55c964", "#b92828", "#3bae5d", "#5ac568",
                "0 18px 40px rgba(8, 12, 16, 0.31)", "#3a2a38", "#4a3848", "#c72ab7", "#55c964", "#b92828", "#aeb9b4", "#3bae5d",
                "#3d1839", "#e8dfd2", "#351f33", "#e8dfd2", "#f0e8dc", "#3d313c", "#8d9993", "#5a4057", "#2f2630", "#654461")),
            BuiltIn("abyssal-blackwater", "Abyssal Blackwater", isDark: true, Tokens(
                "#000405", "rgba(1, 6, 7, 0.99)", "rgba(38, 150, 156, 0.38)", "#c9e4e2", "#7f9f9e", "#35b8be", "#2d8289",
                "#aa3b4e", "#339f87", "#43c7cd", "0 34px 104px rgba(8, 68, 74, 0.16)", "rgba(1, 7, 8, 0.97)", "rgba(2, 10, 12, 0.99)",
                "#35b8be", "#2d8289", "#100305", "#031113", "#031310", "#000607", "#c9e4e2", "#000202", "#c9e4e2", "#000405",
                "rgba(201, 228, 226, 0.055)", "rgba(127, 159, 158, 0.58)", "rgba(38, 150, 156, 0.24)", "#010809", "rgba(53, 184, 190, 0.36)")),
            BuiltIn("obsidian-glow", "Obsidian Glow", isDark: true, Tokens(
                "#05060a", "rgba(12, 14, 22, 0.96)", "rgba(155, 190, 255, 0.20)", "#e7ecf6", "#98a2b8", "#7fa8ff", "#b78cff",
                "#d85f78", "#58c99b", "#9fc0ff", "0 24px 72px rgba(150, 185, 255, 0.22)", "rgba(15, 18, 30, 0.82)", "rgba(20, 24, 40, 0.96)",
                "#7fa8ff", "#b78cff", "#21151f", "#171d2d", "#13251f", "#080a12", "#e7ecf6", "#07080f", "#e7ecf6", "#05060a",
                "rgba(231, 236, 246, 0.07)", "rgba(152, 162, 184, 0.54)", "rgba(155, 190, 255, 0.16)", "#0e111c", "rgba(155, 190, 255, 0.24)")),
            BuiltIn("blessed-skyhaven", "Blessed Skyhaven", isDark: false, Tokens(
                "#edf8ff", "rgba(255, 255, 255, 0.97)", "rgba(255, 213, 92, 0.62)", "#1b3148", "#6f879d", "#6fbfff", "#f0bd3f",
                "#d75d75", "#56b8f0", "#9bdcff", "0 24px 78px rgba(255, 224, 128, 0.38)", "rgba(245, 252, 255, 0.92)", "rgba(255, 255, 255, 0.99)",
                "#6fbfff", "#f0bd3f", "#ffe4ec", "#f5fcff", "#e2f5ff", "#fff4c5", "#1b3148", "#fafdff", "#1b3148", "#10283d",
                "rgba(27, 49, 72, 0.08)", "rgba(111, 135, 157, 0.62)", "rgba(255, 213, 92, 0.36)", "#fbfeff", "rgba(111, 191, 255, 0.52)")),
            BuiltIn("infernal-covenant", "Infernal Covenant", isDark: true, Tokens(
                "#030000", "rgba(9, 1, 1, 0.99)", "rgba(255, 34, 54, 0.54)", "#e8caca", "#a46d70", "#ff1f36", "#9b0c18",
                "#ff3048", "#9a4a34", "#ff4058", "0 30px 90px rgba(255, 20, 45, 0.32)", "rgba(10, 2, 2, 0.97)", "rgba(18, 3, 4, 0.99)",
                "#ff1f36", "#9b0c18", "#210305", "#140203", "#1a0704", "#070000", "#e8caca", "#010000", "#e8caca", "#030000",
                "rgba(232, 202, 202, 0.06)", "rgba(164, 109, 112, 0.62)", "rgba(255, 34, 54, 0.28)", "#090101", "rgba(255, 31, 54, 0.48)")),
            LegacyAs("midnight-tavern", "Midnight Tavern", isDark: true),
            LegacyAs("dragonfire-keep", "Dragonfire Keep", isDark: true),
            LegacyAs("frost-healer", "Frost Healer", isDark: false),
            sunlit,
            LegacyAs("mosswood-quest", "Mosswood Quest", isDark: false),
            potion,
            LegacyAs("boss-battle", "Boss Battle", isDark: true),
            ledger,
            LegacyAs("celestial-inn", "Celestial Inn", isDark: true),
            BuiltIn("treasure-vault", "Treasure Vault", isDark: false, sunlit.Tokens with
            {
                Primary = "#9a6b22", Accent = "#d19b35", AppBarBackground = "#72501f", DrawerBackground = "#553b18"
            }),
            BuiltIn("mana-spring", "Mana Spring", isDark: false, potion.Tokens with
            {
                Primary = "#5b62b8", Accent = "#2b9fa3", AppBarBackground = "#4a4f94", DrawerBackground = "#363b78"
            }),
            BuiltIn("stonewatch-sanctuary", "Stonewatch Sanctuary", isDark: false, ledger.Tokens with
            {
                Primary = "#68747b", Accent = "#917552", AppBarBackground = "#4b565c", DrawerBackground = "#394348"
            })
        };
    }

    private static ColorSchemeDefinition LegacyAs(string legacyId, string name, bool isDark, string? id = null)
    {
        var legacy = LegacySchemes.Single(scheme => string.Equals(scheme.Id, legacyId, StringComparison.Ordinal));
        return legacy with { Id = id ?? legacy.Id, Name = name, IsDark = isDark };
    }

    private static ColorSchemeDefinition BuiltIn(string id, string name, bool isDark, ColorSchemeTokens tokens)
        => new(id, name, true, tokens, IsDark: isDark);

    private static ColorSchemeTokens Tokens(params string[] values)
    {
        if (values.Length != 28)
        {
            throw new ArgumentException("Color schemes require exactly 28 solid tokens.", nameof(values));
        }

        return new ColorSchemeTokens(
            values[0], values[1], values[2], values[3], values[4], values[5], values[6],
            values[7], values[8], values[9], values[10], values[11], values[12], values[13],
            values[14], values[15], values[16], values[17], values[18], values[19], values[20],
            values[21], values[22], values[23], values[24], values[25], values[26], values[27]);
    }

    public static IReadOnlyList<ColorSchemeTokenDescriptor> EditableTokens { get; } = new[]
    {
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Background), "Background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.CardBackground), "Card background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.CardBorder), "Card border"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Ink), "Text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Muted), "Muted text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Primary), "Primary"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Accent), "Accent"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Danger), "Danger"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Success), "Success"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Focus), "Focus"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Shadow), "Shadow"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.Surface), "Surface"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.SurfaceStrong), "Strong surface"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.ChartPrimary), "Chart primary"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.ChartSecondary), "Chart secondary"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.TaskNegative), "Task min"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.TaskNeutral), "Task base"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.TaskPositive), "Task max"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.AppBarBackground), "Header background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.AppBarText), "Header text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DrawerBackground), "Navigation background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DrawerText), "Navigation text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.PrimaryButtonText), "Primary button text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.SecondaryButtonText), "Secondary button text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DisabledBackground), "Disabled background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DisabledText), "Disabled text"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.DisabledBorder), "Disabled border"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.InputBackground), "Input background"),
        new ColorSchemeTokenDescriptor(nameof(ColorSchemeTokens.InputBorder), "Input border")
    };

    public static ColorSchemeDefinition Alpha => BuiltInSchemes[0];

    public static ColorSchemeDefinition DefaultLight => Resolve(DefaultLightSchemeId, Array.Empty<ColorSchemeDefinition>());

    public static ColorSchemeDefinition DefaultDark => Resolve(DefaultDarkSchemeId, Array.Empty<ColorSchemeDefinition>());

    public static ColorSchemeDefinition Resolve(string? schemeId, IReadOnlyList<ColorSchemeDefinition> customSchemes)
    {
        schemeId = MigrateLegacySchemeId(schemeId);
        return BuiltInSchemes.Concat(customSchemes)
            .Select(Complete)
            .FirstOrDefault(scheme => string.Equals(scheme.Id, schemeId, StringComparison.Ordinal))
            ?? DefaultLight;
    }

    public static ColorSchemeDefinition CreateCustomCopy(ColorSchemeDefinition source, string name)
    {
        return new ColorSchemeDefinition(
            $"custom-{Guid.NewGuid():N}",
            NormalizeName(name, "Custom scheme"),
            false,
            source.Tokens,
            IsDark: source.IsDark);
    }

    public const string RandomSchemeId = "random-theme";

    public static ColorSchemeDefinition PickRandomPreset(
        IReadOnlyList<ColorSchemeDefinition>? customSchemes,
        string? excludeId = null,
        Random? random = null)
    {
        random ??= Random.Shared;
        var pool = BuiltInSchemes
            .Concat(customSchemes ?? Array.Empty<ColorSchemeDefinition>())
            .Where(scheme => !string.Equals(scheme.Id, RandomSchemeId, StringComparison.Ordinal))
            .Where(scheme => excludeId is null || !string.Equals(scheme.Id, excludeId, StringComparison.Ordinal))
            .ToArray();
        if (pool.Length == 0)
        {
            return DefaultLight;
        }

        return Complete(pool[random.Next(pool.Length)]);
    }

    /// <summary>Generate a random theme. <paramref name="chaos"/> in [0,1] scales hue divergence and
    /// saturation: 0 is a calm single-hue palette, 1 is absolute madness with every token roaming its
    /// own hue at maximum saturation. Text and muted tokens stay contrast-derived so the app stays legible.</summary>
    public static ColorSchemeDefinition GenerateRandomTheme(Random? random = null, double chaos = 0.0)
    {
        random ??= Random.Shared;
        chaos = Math.Clamp(chaos, 0.0, 1.0);
        var dark = random.Next(2) == 0;
        var baseHue = random.NextDouble() * 360.0;
        var accentHue = (baseHue + 90.0 + random.NextDouble() * 180.0) % 360.0;

        // At chaos 0 every token sits on the base/accent hue; as chaos rises each draw is jittered
        // up to +/-180 degrees, reaching the full hue circle at chaos 1.
        // Above ~0.85 chaos the jitter can wrap more than a full circle so even the "base" hue stops
        // being a stable anchor, pushing the top of the slider into genuinely unhinged territory.
        var jitterSpan = 180.0 * chaos + 360.0 * Math.Max(0.0, chaos - 0.85);
        double Jitter(double center) => center + Range(random, -1.0, 1.0) * jitterSpan;
        double Hue() => Jitter(baseHue);
        double AccentHueValue() => Jitter(accentHue);
        var bgSatMax = Lerp(0.32, 1.0, chaos);
        var colorSatMin = Lerp(0.58, 0.92, chaos);
        var colorSatMax = Lerp(0.88, 1.0, chaos);

        // At high chaos the background lightness range widens so backgrounds can be jarringly bright
        // or near-black regardless of the light/dark base; ink stays contrast-derived for legibility.
        var bgLightSpread = 0.06 * chaos;
        var background = Hsl(Hue(), Range(random, 0.08 + 0.22 * chaos, bgSatMax), dark ? Range(random, 0.06, 0.16 + bgLightSpread) : Range(random, 0.90 - bgLightSpread * 3.0, 0.97));
        var cardBackground = Hsl(Hue(), Range(random, 0.10, Lerp(0.34, 0.80, chaos)), dark ? Range(random, 0.12, 0.22) : Range(random, 0.95, 0.99));
        var surface = Hsl(Hue(), Range(random, 0.10, Lerp(0.30, 0.80, chaos)), dark ? Range(random, 0.16, 0.26) : Range(random, 0.86, 0.94));
        var surfaceStrong = Hsl(Hue(), Range(random, 0.12, Lerp(0.34, 0.85, chaos)), dark ? Range(random, 0.22, 0.32) : Range(random, 0.80, 0.90));
        var cardBorder = Hsl(Hue(), Range(random, 0.12, Lerp(0.36, 0.95, chaos)), dark ? Range(random, 0.26, 0.40) : Range(random, 0.62, 0.82));
        var ink = ReadableInk(background);
        var muted = ReadableMuted(background, chaos > 0.5);

        // Lightness spread for the loud tokens widens with chaos so primaries roam from deep to neon.
        var lightSpread = 0.12 * chaos;
        var primaryHue = Hue();
        var primary = Hsl(primaryHue, Range(random, colorSatMin, colorSatMax), Range(random, 0.42 - lightSpread, 0.60 + lightSpread));
        var resolvedAccentHue = chaos > 0.85
            ? primaryHue + Range(random, 150.0, 180.0)
            : AccentHueValue();
        var accent = Hsl(resolvedAccentHue, Range(random, colorSatMin, colorSatMax), Range(random, 0.44 - lightSpread, 0.62 + lightSpread));
        var focus = Hsl(AccentHueValue(), Range(random, colorSatMin, colorSatMax), Range(random, 0.48 - lightSpread, 0.64 + lightSpread));
        // Danger/success start semantic (red/green) and drift off-hue as chaos rises.
        var danger = Hsl(Jitter(0.0), Range(random, Lerp(0.62, 0.85, chaos), Lerp(0.82, 1.0, chaos)), Range(random, 0.42, 0.56));
        var success = Hsl(Jitter(140.0), Range(random, Lerp(0.50, 0.85, chaos), Lerp(0.74, 1.0, chaos)), Range(random, 0.38, 0.54));

        var appBarBackground = Hsl(Hue(), Range(random, 0.30, Lerp(0.55, 1.0, chaos)), Range(random, Lerp(0.16, 0.18, chaos), Lerp(0.28, 0.42, chaos)));
        var drawerBackground = Hsl(Hue(), Range(random, 0.30, Lerp(0.55, 1.0, chaos)), Range(random, 0.13, Lerp(0.24, 0.40, chaos)));
        var disabledBackground = Hsl(Hue(), Range(random, 0.06, Lerp(0.18, 0.60, chaos)), dark ? Range(random, 0.24, 0.34) : Range(random, 0.82, 0.90));
        var inputBackground = Hsl(Hue(), Range(random, 0.08, Lerp(0.22, 0.70, chaos)), dark ? Range(random, 0.16, 0.26) : Range(random, 0.94, 0.99));

        var tokens = new ColorSchemeTokens(
            background,
            cardBackground,
            cardBorder,
            ink,
            muted,
            primary,
            accent,
            danger,
            success,
            focus,
            $"0 18px 40px rgba(8, 12, 16, {Range(random, 0.20, 0.42):0.00})",
            surface,
            surfaceStrong,
            primary,
            accent,
            danger,
            muted,
            success,
            appBarBackground,
            ReadableInk(appBarBackground),
            drawerBackground,
            ReadableInk(drawerBackground),
            ReadableInk(primary),
            disabledBackground,
            muted,
            cardBorder,
            inputBackground,
            cardBorder,
            CreateStops9(random, background, SurfaceChaos(chaos, 1.0, 1.4)),
            CreateStops8(random, cardBackground, SurfaceChaos(chaos, 0.5, 1.2)),
            CreateStops6(random, appBarBackground, SurfaceChaos(chaos, 1.0, 1.3)),
            CreateStops6(random, drawerBackground, SurfaceChaos(chaos, 1.0, 1.3)),
            CreateStops4(random, primary, SurfaceChaos(chaos, 0.8, 1.2)),
            CreateStops2(random, cardBackground, SurfaceChaos(chaos, 1.0, 1.5)),
            CreateStops2(random, accent, SurfaceChaos(chaos, 1.0, 1.5)),
            chaos > 0.85 ? $"0 0 {Range(random, 14.0, 22.0):0}px {Hsl(Range(random, 0.0, 360.0), 0.95, 0.58)}" : null,
            SecondaryButtonText: ReadableInk(accent));

        return new ColorSchemeDefinition(RandomSchemeId, "Random theme", false, ApplyRandomContrastGuards(tokens, chaos), IsDark: dark);
    }

    private static ColorSchemeTokens ApplyRandomContrastGuards(ColorSchemeTokens tokens, double chaos)
    {
        var bodyTextMinimum = TextContrastMinimum(chaos);
        var shellTextMinimum = ShellTextContrastMinimum(chaos);
        var buttonTextMinimum = ButtonTextContrastMinimum(chaos);
        var primaryButtonText = EnsureContrastAcross(
            tokens.PrimaryButtonText,
            buttonTextMinimum,
            ColorsWithAverage(tokens.PrimaryButtonGradient!));
        var secondaryButtonText = EnsureContrastAcross(
            tokens.SecondaryButtonText ?? tokens.PrimaryButtonText,
            buttonTextMinimum,
            ColorsWithAverage(tokens.SecondaryButtonGradient!));
        var primaryButtonGradient = EnsureGradientTextContrast(tokens.PrimaryButtonGradient!, primaryButtonText, buttonTextMinimum);
        var secondaryButtonGradient = EnsureGradientTextContrast(tokens.SecondaryButtonGradient!, secondaryButtonText, buttonTextMinimum);

        tokens = tokens with
        {
            Ink = EnsureContrastAcross(
                tokens.Ink,
                bodyTextMinimum,
                ColorsWithAverage(tokens.CardGradient!)
                    .Prepend(tokens.SurfaceStrong)
                    .Prepend(tokens.Surface)
                    .Prepend(tokens.CardBackground)
                    .ToArray()),
            AppBarText = EnsureContrastAcross(
                tokens.AppBarText,
                shellTextMinimum,
                ColorsWithAverage(tokens.AppBarGradient!)
                    .Prepend(tokens.AppBarBackground)
                    .ToArray()),
            DrawerText = EnsureContrastAcross(
                tokens.DrawerText,
                shellTextMinimum,
                ColorsWithAverage(tokens.DrawerGradient!)
                    .Prepend(tokens.DrawerBackground)
                    .ToArray()),
            PrimaryButtonText = primaryButtonText,
            SecondaryButtonText = secondaryButtonText,
            DisabledText = EnsureContrast(tokens.DisabledText, tokens.DisabledBackground, DisabledTextContrastMinimum(chaos)),
            InputBorder = EnsureContrast(tokens.InputBorder, tokens.InputBackground, InputBorderContrastMinimum(chaos)),
            Focus = EnsureContrastAgainstBoth(tokens.Focus, Average(tokens.BackgroundGradient!), tokens.CardBackground, FocusContrastMinimum(chaos)),
            PrimaryButtonGradient = primaryButtonGradient,
            SecondaryButtonGradient = secondaryButtonGradient
        };

        var (primaryHue, _, _) = HexToHsl(tokens.Primary);
        var (dangerHue, dangerSaturation, dangerLightness) = HexToHsl(tokens.Danger);
        if (HueDistance(primaryHue, dangerHue) < 30.0
            && Math.Abs(RelativeLuminance(tokens.Primary) - RelativeLuminance(tokens.Danger)) < 0.15)
        {
            tokens = tokens with { Danger = Hsl(primaryHue + 120.0, dangerSaturation, dangerLightness) };
        }

        return tokens;
    }

    private static double TextContrastMinimum(double chaos)
        => chaos <= 0.6 ? 4.5 : chaos <= 0.85 ? 3.5 : chaos <= 0.99 ? 3.0 : 2.0;

    private static double ShellTextContrastMinimum(double chaos)
        => chaos <= 0.6 ? 4.5 : chaos <= 0.85 ? 3.5 : chaos <= 0.99 ? 3.0 : 2.0;

    private static double ButtonTextContrastMinimum(double chaos)
        => chaos <= 0.6 ? 4.5 : chaos <= 0.85 ? 3.8 : chaos <= 0.99 ? 3.0 : 2.0;

    private static double DisabledTextContrastMinimum(double chaos)
        => chaos <= 0.6 ? 3.0 : chaos <= 0.85 ? 2.5 : 2.0;

    private static double InputBorderContrastMinimum(double chaos)
        => chaos <= 0.6 ? 2.0 : chaos <= 0.85 ? 1.7 : 1.4;

    private static double FocusContrastMinimum(double chaos)
        => chaos <= 0.6 ? 3.0 : chaos <= 0.85 ? 2.5 : 2.0;

    private static string EnsureContrast(string value, string background, double minimum)
        => ContrastRatio(value, background) >= minimum ? value : BestReadableText(background);

    private static string EnsureContrastAcross(string value, double minimum, params string[] backgrounds)
    {
        if (MinimumContrast(value, backgrounds) >= minimum)
        {
            return value;
        }

        return BestReadableText(backgrounds);
    }

    private static string EnsureContrastAgainstBoth(string value, string first, string second, double minimum)
    {
        if (ContrastRatio(value, first) >= minimum && ContrastRatio(value, second) >= minimum)
        {
            return value;
        }

        var dark = "#101010";
        var light = "#f5f5f5";
        return Math.Min(ContrastRatio(dark, first), ContrastRatio(dark, second))
            >= Math.Min(ContrastRatio(light, first), ContrastRatio(light, second))
                ? dark
                : light;
    }

    private static string BestReadableText(string background)
        => ContrastRatio("#101010", background) >= ContrastRatio("#f5f5f5", background) ? "#101010" : "#f5f5f5";

    private static string BestReadableText(params string[] backgrounds)
    {
        var dark = "#101010";
        var light = "#f5f5f5";
        return MinimumContrast(dark, backgrounds) >= MinimumContrast(light, backgrounds) ? dark : light;
    }

    private static double MinimumContrast(string value, params string[] backgrounds)
        => backgrounds.Min(background => ContrastRatio(value, background));

    private static GradientStops4 EnsureGradientTextContrast(GradientStops4 stops, string text, double minimum)
    {
        var adjusted = new GradientStops4(
            EnsureBackgroundTextContrast(stops.TopLeft, text, minimum),
            EnsureBackgroundTextContrast(stops.TopRight, text, minimum),
            EnsureBackgroundTextContrast(stops.BottomLeft, text, minimum),
            EnsureBackgroundTextContrast(stops.BottomRight, text, minimum));
        for (var step = 0; step < 12 && MinimumContrast(text, ColorsWithAverage(adjusted)) < minimum; step++)
        {
            adjusted = new GradientStops4(
                ShiftBackgroundAwayFromText(adjusted.TopLeft, text),
                ShiftBackgroundAwayFromText(adjusted.TopRight, text),
                ShiftBackgroundAwayFromText(adjusted.BottomLeft, text),
                ShiftBackgroundAwayFromText(adjusted.BottomRight, text));
        }

        return adjusted;
    }

    private static GradientStops2 EnsureGradientTextContrast(GradientStops2 stops, string text, double minimum)
    {
        var adjusted = new GradientStops2(
            EnsureBackgroundTextContrast(stops.Start, text, minimum),
            EnsureBackgroundTextContrast(stops.End, text, minimum));
        for (var step = 0; step < 12 && MinimumContrast(text, ColorsWithAverage(adjusted)) < minimum; step++)
        {
            adjusted = new GradientStops2(
                ShiftBackgroundAwayFromText(adjusted.Start, text),
                ShiftBackgroundAwayFromText(adjusted.End, text));
        }

        return adjusted;
    }

    private static string EnsureBackgroundTextContrast(string background, string text, double minimum)
    {
        if (ContrastRatio(text, background) >= minimum)
        {
            return background;
        }

        var candidate = background;
        for (var step = 0; step < 12 && ContrastRatio(text, candidate) < minimum; step++)
        {
            candidate = ShiftBackgroundAwayFromText(candidate, text);
        }

        return candidate;
    }

    private static string ShiftBackgroundAwayFromText(string background, string text)
    {
        var (hue, saturation, lightness) = HexToHsl(background);
        var textIsDark = RelativeLuminance(text) < 0.42;
        lightness = textIsDark ? Math.Min(0.98, lightness + 0.05) : Math.Max(0.02, lightness - 0.05);
        return Hsl(hue, saturation, lightness);
    }

    private static double ContrastRatio(string first, string second)
    {
        var firstLuminance = RelativeLuminance(first);
        var secondLuminance = RelativeLuminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + 0.05) / (Math.Min(firstLuminance, secondLuminance) + 0.05);
    }

    private static double HueDistance(double first, double second)
    {
        var distance = Math.Abs(first - second) % 360.0;
        return Math.Min(distance, 360.0 - distance);
    }

    private static string Average(GradientStops9 stops)
        => Average(stops.TopLeft, stops.Top, stops.TopRight, stops.MiddleLeft, stops.Middle, stops.MiddleRight, stops.BottomLeft, stops.Bottom, stops.BottomRight);

    private static string Average(GradientStops8 stops)
        => Average(stops.TopLeft, stops.Top, stops.TopRight, stops.MiddleLeft, stops.MiddleRight, stops.BottomLeft, stops.Bottom, stops.BottomRight);

    private static string Average(GradientStops6 stops)
        => Average(stops.TopLeft, stops.Top, stops.TopRight, stops.BottomLeft, stops.Bottom, stops.BottomRight);

    private static string Average(GradientStops4 stops)
        => Average(stops.TopLeft, stops.TopRight, stops.BottomLeft, stops.BottomRight);

    private static string Average(params string[] values)
    {
        var colors = values.Select(HexToRgb).ToArray();
        return $"#{(int)Math.Round(colors.Average(color => color.Red)):x2}{(int)Math.Round(colors.Average(color => color.Green)):x2}{(int)Math.Round(colors.Average(color => color.Blue)):x2}";
    }

    private static string[] ColorsWithAverage(GradientStops8 stops)
        => new[]
        {
            stops.TopLeft, stops.Top, stops.TopRight, stops.MiddleLeft, stops.MiddleRight, stops.BottomLeft, stops.Bottom, stops.BottomRight,
            Average(stops)
        };

    private static string[] ColorsWithAverage(GradientStops6 stops)
        => new[]
        {
            stops.TopLeft, stops.Top, stops.TopRight, stops.BottomLeft, stops.Bottom, stops.BottomRight,
            Average(stops)
        };

    private static string[] ColorsWithAverage(GradientStops4 stops)
        => new[]
        {
            stops.TopLeft, stops.TopRight, stops.BottomLeft, stops.BottomRight,
            Average(stops)
        };

    private static string[] ColorsWithAverage(GradientStops2 stops)
        => new[] { stops.Start, stops.End, Average(stops.Start, stops.End) };

    private static (double Red, double Green, double Blue) HexToRgb(string color)
    {
        var hex = color.Trim().TrimStart('#');
        return (
            Convert.ToInt32(hex.AsSpan(0, 2).ToString(), 16),
            Convert.ToInt32(hex.AsSpan(2, 2).ToString(), 16),
            Convert.ToInt32(hex.AsSpan(4, 2).ToString(), 16));
    }

    private static double SurfaceChaos(double chaos, double calmMultiplier, double madnessMultiplier)
    {
        var effective = Math.Pow(chaos, 0.55);
        if (chaos <= 0.6)
        {
            return effective * calmMultiplier;
        }

        var ramp = (chaos - 0.6) / 0.4;
        return effective * Lerp(calmMultiplier, madnessMultiplier, ramp);
    }

    private static GradientStops9 CreateStops9(Random random, string color, double chaos)
        => new(
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos),
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos),
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos));

    private static GradientStops8 CreateStops8(Random random, string color, double chaos)
        => new(
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos),
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos),
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos));

    private static GradientStops6 CreateStops6(Random random, string color, double chaos)
        => new(
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos),
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos));

    private static GradientStops4 CreateStops4(Random random, string color, double chaos)
        => new(
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos),
            VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos));

    private static GradientStops2 CreateStops2(Random random, string color, double chaos)
        => new(VaryGradientColor(random, color, chaos), VaryGradientColor(random, color, chaos));

    private static string VaryGradientColor(Random random, string color, double chaos)
    {
        var (hue, saturation, lightness) = HexToHsl(color);
        var hueLimit = chaos switch
        {
            <= 0.5 => 25.0,
            <= 0.85 => 90.0,
            _ => 180.0
        };
        return Hsl(
            hue + Range(random, -hueLimit, hueLimit) * chaos,
            Math.Clamp(saturation + Range(random, -0.12, 0.18) * chaos, 0.0, 1.0),
            Math.Clamp(lightness + Range(random, -0.12, 0.12) * chaos, 0.02, 0.98));
    }

    private static (double Hue, double Saturation, double Lightness) HexToHsl(string color)
    {
        var hex = color.Trim().TrimStart('#');
        var red = Convert.ToInt32(hex.AsSpan(0, 2).ToString(), 16) / 255.0;
        var green = Convert.ToInt32(hex.AsSpan(2, 2).ToString(), 16) / 255.0;
        var blue = Convert.ToInt32(hex.AsSpan(4, 2).ToString(), 16) / 255.0;
        var max = Math.Max(red, Math.Max(green, blue));
        var min = Math.Min(red, Math.Min(green, blue));
        var delta = max - min;
        var lightness = (max + min) / 2.0;
        if (delta == 0)
        {
            return (0.0, 0.0, lightness);
        }

        var saturation = delta / (1.0 - Math.Abs(2.0 * lightness - 1.0));
        var hue = max == red
            ? 60.0 * (((green - blue) / delta) % 6.0)
            : max == green
                ? 60.0 * ((blue - red) / delta + 2.0)
                : 60.0 * ((red - green) / delta + 4.0);
        return (hue < 0 ? hue + 360.0 : hue, saturation, lightness);
    }

    private static double Range(Random random, double min, double max)
    {
        return min + random.NextDouble() * (max - min);
    }

    private static double Lerp(double from, double to, double amount)
    {
        return from + (to - from) * amount;
    }

    private static string Hsl(double hue, double saturation, double lightness)
    {
        hue = ((hue % 360.0) + 360.0) % 360.0;
        var chroma = (1.0 - Math.Abs(2.0 * lightness - 1.0)) * saturation;
        var huePrime = hue / 60.0;
        var secondary = chroma * (1.0 - Math.Abs(huePrime % 2.0 - 1.0));
        var (red, green, blue) = huePrime switch
        {
            < 1.0 => (chroma, secondary, 0.0),
            < 2.0 => (secondary, chroma, 0.0),
            < 3.0 => (0.0, chroma, secondary),
            < 4.0 => (0.0, secondary, chroma),
            < 5.0 => (secondary, 0.0, chroma),
            _ => (chroma, 0.0, secondary)
        };
        var match = lightness - chroma / 2.0;
        return $"#{ToByte(red + match):x2}{ToByte(green + match):x2}{ToByte(blue + match):x2}";
    }

    private static int ToByte(double channel)
    {
        return Math.Clamp((int)Math.Round(channel * 255.0), 0, 255);
    }

    private static string ReadableInk(string colorValue)
    {
        return RelativeLuminance(colorValue) > 0.42 ? "#162423" : "#f5efe2";
    }

    private static string ReadableMuted(string backgroundValue, bool crazy)
    {
        // Muted text must stay legible on any background, including crazy palettes,
        // so derive it from background luminance rather than a random hue.
        return RelativeLuminance(backgroundValue) > 0.42 ? "#3a4443" : (crazy ? "#d6dcdb" : "#c7cfce");
    }

    private static double RelativeLuminance(string colorValue)
    {
        var hex = colorValue.Trim().TrimStart('#');
        if (hex.Length is 3 or 4)
        {
            hex = string.Concat(hex.Select(c => $"{c}{c}"));
        }

        if (hex.Length < 6
            || !int.TryParse(hex.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var red)
            || !int.TryParse(hex.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var green)
            || !int.TryParse(hex.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var blue))
        {
            return 0.5;
        }

        return (0.2126 * Channel(red) + 0.7152 * Channel(green) + 0.0722 * Channel(blue));

        static double Channel(int value)
        {
            var normalized = value / 255.0;
            return normalized <= 0.03928 ? normalized / 12.92 : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }
    }

    public static string NormalizeName(string? name, string fallback)
    {
        var trimmed = name?.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? fallback : trimmed.Length > 80 ? trimmed[..80] : trimmed;
    }

    public static bool IsBuiltIn(string? schemeId)
    {
        return BuiltInSchemes.Any(scheme => string.Equals(scheme.Id, schemeId, StringComparison.Ordinal));
    }

    public static string MigrateLegacySchemeId(string? schemeId)
    {
        return schemeId switch
        {
            AlphaId => ForestLegacyId,
            "neon-rogue" => "arcane-wraith",
            "neon-abyss-carnival" => "phantom-fair",
            "habitica" or "mushroom-meadow" => DefaultLightSchemeId,
            "mana-mirage" or "mushroom-trip" or "sugar-crash" => DefaultDarkSchemeId,
            _ => string.IsNullOrWhiteSpace(schemeId) ? DefaultLightSchemeId : schemeId
        };
    }

    public static bool GuessIsDark(string colorValue)
    {
        return RelativeLuminance(colorValue) < 0.42;
    }

    public static bool IsValidTokenValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 120)
        {
            return false;
        }

        return CssColorOrShadowPattern().IsMatch(value.Trim());
    }

    /// <summary>Validates a CSS box-shadow / text-shadow value. Unlike <see cref="IsValidTokenValue"/>
    /// this accepts multi-layer comma-separated shadows (each layer optionally prefixed with
    /// <c>inset</c>) and the keyword <c>none</c>, and allows a longer overall string.</summary>
    public static bool IsValidShadowValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 400)
        {
            return false;
        }

        var trimmed = value.Trim();
        if (string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var layer in SplitTopLevelCommas(trimmed))
        {
            var segment = layer.Trim();
            if (segment.StartsWith("inset ", StringComparison.OrdinalIgnoreCase))
            {
                segment = segment["inset ".Length..].Trim();
            }

            if (segment.Length == 0 || !CssColorOrShadowPattern().IsMatch(segment))
            {
                return false;
            }
        }

        return true;
    }

    private static IEnumerable<string> SplitTopLevelCommas(string value)
    {
        var depth = 0;
        var start = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character == '(')
            {
                depth++;
            }
            else if (character == ')')
            {
                if (depth > 0)
                {
                    depth--;
                }
            }
            else if (character == ',' && depth == 0)
            {
                yield return value[start..index];
                start = index + 1;
            }
        }

        yield return value[start..];
    }

    public static ColorSchemeDefinition Complete(ColorSchemeDefinition scheme)
    {
        var fallback = Alpha.Tokens;
        var tokens = scheme.Tokens ?? fallback;
        return scheme with
        {
            Id = string.IsNullOrWhiteSpace(scheme.Id) ? $"custom-{Guid.NewGuid():N}" : scheme.Id,
            Name = NormalizeName(scheme.Name, scheme.IsBuiltIn ? "Alpha" : "Custom scheme"),
            Tokens = new ColorSchemeTokens(
                NormalizeToken(tokens.Background, fallback.Background),
                NormalizeToken(tokens.CardBackground, fallback.CardBackground),
                NormalizeToken(tokens.CardBorder, fallback.CardBorder),
                NormalizeToken(tokens.Ink, fallback.Ink),
                NormalizeToken(tokens.Muted, fallback.Muted),
                NormalizeToken(tokens.Primary, fallback.Primary),
                NormalizeToken(tokens.Accent, fallback.Accent),
                NormalizeToken(tokens.Danger, fallback.Danger),
                NormalizeToken(tokens.Success, fallback.Success),
                NormalizeToken(tokens.Focus, fallback.Focus),
                NormalizeToken(tokens.Shadow, fallback.Shadow),
                NormalizeToken(tokens.Surface, fallback.Surface),
                NormalizeToken(tokens.SurfaceStrong, fallback.SurfaceStrong),
                NormalizeToken(tokens.ChartPrimary, fallback.ChartPrimary),
                NormalizeToken(tokens.ChartSecondary, fallback.ChartSecondary),
                NormalizeToken(tokens.TaskNegative, fallback.TaskNegative),
                NormalizeToken(tokens.TaskNeutral, fallback.TaskNeutral),
                NormalizeToken(tokens.TaskPositive, fallback.TaskPositive),
                NormalizeToken(tokens.AppBarBackground, fallback.AppBarBackground),
                NormalizeToken(tokens.AppBarText, fallback.AppBarText),
                NormalizeToken(tokens.DrawerBackground, fallback.DrawerBackground),
                NormalizeToken(tokens.DrawerText, fallback.DrawerText),
                NormalizeToken(tokens.PrimaryButtonText, fallback.PrimaryButtonText),
                NormalizeToken(tokens.DisabledBackground, fallback.DisabledBackground),
                NormalizeToken(tokens.DisabledText, fallback.DisabledText),
                NormalizeToken(tokens.DisabledBorder, fallback.DisabledBorder),
                NormalizeToken(tokens.InputBackground, fallback.InputBackground),
                NormalizeToken(tokens.InputBorder, fallback.InputBorder),
                tokens.BackgroundGradient,
                tokens.CardGradient,
                tokens.AppBarGradient,
                tokens.DrawerGradient,
                tokens.PrimaryButtonGradient,
                tokens.SecondaryButtonGradient,
                tokens.AccentChipGradient,
                NormalizeOptionalToken(tokens.HeadingTextShadow),
                NormalizeOptionalToken(tokens.AppBarTextShadow),
                NormalizeOptionalToken(tokens.DrawerTextShadow),
                // Backfill secondary button text from primary so legacy schemes (and presets that
                // omit it) get a valid, contrast-safe value rather than the global fallback.
                SecondaryButtonText: NormalizeToken(tokens.SecondaryButtonText, NormalizeToken(tokens.PrimaryButtonText, fallback.PrimaryButtonText)))
        };
    }

    private static string NormalizeToken(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptionalToken(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static string GetTokenValue(ColorSchemeTokens tokens, string tokenName)
    {
        return tokenName switch
        {
            nameof(ColorSchemeTokens.Background) => tokens.Background,
            nameof(ColorSchemeTokens.CardBackground) => tokens.CardBackground,
            nameof(ColorSchemeTokens.CardBorder) => tokens.CardBorder,
            nameof(ColorSchemeTokens.Ink) => tokens.Ink,
            nameof(ColorSchemeTokens.Muted) => tokens.Muted,
            nameof(ColorSchemeTokens.Primary) => tokens.Primary,
            nameof(ColorSchemeTokens.Accent) => tokens.Accent,
            nameof(ColorSchemeTokens.Danger) => tokens.Danger,
            nameof(ColorSchemeTokens.Success) => tokens.Success,
            nameof(ColorSchemeTokens.Focus) => tokens.Focus,
            nameof(ColorSchemeTokens.Shadow) => tokens.Shadow,
            nameof(ColorSchemeTokens.Surface) => tokens.Surface,
            nameof(ColorSchemeTokens.SurfaceStrong) => tokens.SurfaceStrong,
            nameof(ColorSchemeTokens.ChartPrimary) => tokens.ChartPrimary,
            nameof(ColorSchemeTokens.ChartSecondary) => tokens.ChartSecondary,
            nameof(ColorSchemeTokens.TaskNegative) => tokens.TaskNegative,
            nameof(ColorSchemeTokens.TaskNeutral) => tokens.TaskNeutral,
            nameof(ColorSchemeTokens.TaskPositive) => tokens.TaskPositive,
            nameof(ColorSchemeTokens.AppBarBackground) => tokens.AppBarBackground,
            nameof(ColorSchemeTokens.AppBarText) => tokens.AppBarText,
            nameof(ColorSchemeTokens.DrawerBackground) => tokens.DrawerBackground,
            nameof(ColorSchemeTokens.DrawerText) => tokens.DrawerText,
            nameof(ColorSchemeTokens.PrimaryButtonText) => tokens.PrimaryButtonText,
            nameof(ColorSchemeTokens.SecondaryButtonText) => tokens.SecondaryButtonText ?? tokens.PrimaryButtonText,
            nameof(ColorSchemeTokens.DisabledBackground) => tokens.DisabledBackground,
            nameof(ColorSchemeTokens.DisabledText) => tokens.DisabledText,
            nameof(ColorSchemeTokens.DisabledBorder) => tokens.DisabledBorder,
            nameof(ColorSchemeTokens.InputBackground) => tokens.InputBackground,
            nameof(ColorSchemeTokens.InputBorder) => tokens.InputBorder,
            _ => string.Empty
        };
    }

    public static ColorSchemeTokens WithTokenValue(ColorSchemeTokens tokens, string tokenName, string value)
    {
        return tokenName switch
        {
            nameof(ColorSchemeTokens.Background) => tokens with { Background = value },
            nameof(ColorSchemeTokens.CardBackground) => tokens with { CardBackground = value },
            nameof(ColorSchemeTokens.CardBorder) => tokens with { CardBorder = value },
            nameof(ColorSchemeTokens.Ink) => tokens with { Ink = value },
            nameof(ColorSchemeTokens.Muted) => tokens with { Muted = value },
            nameof(ColorSchemeTokens.Primary) => tokens with { Primary = value },
            nameof(ColorSchemeTokens.Accent) => tokens with { Accent = value },
            nameof(ColorSchemeTokens.Danger) => tokens with { Danger = value },
            nameof(ColorSchemeTokens.Success) => tokens with { Success = value },
            nameof(ColorSchemeTokens.Focus) => tokens with { Focus = value },
            nameof(ColorSchemeTokens.Shadow) => tokens with { Shadow = value },
            nameof(ColorSchemeTokens.Surface) => tokens with { Surface = value },
            nameof(ColorSchemeTokens.SurfaceStrong) => tokens with { SurfaceStrong = value },
            nameof(ColorSchemeTokens.ChartPrimary) => tokens with { ChartPrimary = value },
            nameof(ColorSchemeTokens.ChartSecondary) => tokens with { ChartSecondary = value },
            nameof(ColorSchemeTokens.TaskNegative) => tokens with { TaskNegative = value },
            nameof(ColorSchemeTokens.TaskNeutral) => tokens with { TaskNeutral = value },
            nameof(ColorSchemeTokens.TaskPositive) => tokens with { TaskPositive = value },
            nameof(ColorSchemeTokens.AppBarBackground) => tokens with { AppBarBackground = value },
            nameof(ColorSchemeTokens.AppBarText) => tokens with { AppBarText = value },
            nameof(ColorSchemeTokens.DrawerBackground) => tokens with { DrawerBackground = value },
            nameof(ColorSchemeTokens.DrawerText) => tokens with { DrawerText = value },
            nameof(ColorSchemeTokens.PrimaryButtonText) => tokens with { PrimaryButtonText = value },
            nameof(ColorSchemeTokens.SecondaryButtonText) => tokens with { SecondaryButtonText = value },
            nameof(ColorSchemeTokens.DisabledBackground) => tokens with { DisabledBackground = value },
            nameof(ColorSchemeTokens.DisabledText) => tokens with { DisabledText = value },
            nameof(ColorSchemeTokens.DisabledBorder) => tokens with { DisabledBorder = value },
            nameof(ColorSchemeTokens.InputBackground) => tokens with { InputBackground = value },
            nameof(ColorSchemeTokens.InputBorder) => tokens with { InputBorder = value },
            _ => tokens
        };
    }

    public static IReadOnlyList<string> Validate(ColorSchemeDefinition scheme)
    {
        scheme = Complete(scheme);
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(scheme.Name))
        {
            errors.Add("Scheme name is required.");
        }

        foreach (var token in EditableTokens)
        {
            var value = GetTokenValue(scheme.Tokens, token.Name);
            // Shadow holds a box-shadow value (multi-layer / inset / none), not a plain color.
            var valid = token.Name == nameof(ColorSchemeTokens.Shadow)
                ? IsValidShadowValue(value)
                : IsValidTokenValue(value);
            if (!valid)
            {
                errors.Add($"{token.Label} is not a supported color value.");
            }
        }

        ValidateGradient(errors, "Page background gradient", scheme.Tokens.BackgroundGradient);
        ValidateGradient(errors, "Card gradient", scheme.Tokens.CardGradient);
        ValidateGradient(errors, "Header gradient", scheme.Tokens.AppBarGradient);
        ValidateGradient(errors, "Navigation gradient", scheme.Tokens.DrawerGradient);
        ValidateGradient(errors, "Primary button gradient", scheme.Tokens.PrimaryButtonGradient);
        ValidateGradient(errors, "Secondary button gradient", scheme.Tokens.SecondaryButtonGradient);
        ValidateGradient(errors, "Accent chip gradient", scheme.Tokens.AccentChipGradient);
        ValidateOptionalToken(errors, "Heading text shadow", scheme.Tokens.HeadingTextShadow);
        ValidateOptionalToken(errors, "Header text shadow", scheme.Tokens.AppBarTextShadow);
        ValidateOptionalToken(errors, "Navigation text shadow", scheme.Tokens.DrawerTextShadow);
        return errors;
    }

    private static void ValidateGradient(List<string> errors, string label, object? gradient)
    {
        if (gradient is null)
        {
            return;
        }

        foreach (var property in gradient.GetType().GetProperties())
        {
            if (!IsValidTokenValue(property.GetValue(gradient) as string))
            {
                errors.Add($"{label} {property.Name} is not a supported color value.");
            }
        }
    }

    private static void ValidateOptionalToken(List<string> errors, string label, string? value)
    {
        if (value is not null && !IsValidShadowValue(value))
        {
            errors.Add($"{label} is not a supported CSS value.");
        }
    }

    [GeneratedRegex("""^(#[0-9a-fA-F]{3,8}|rgba?\([^)]+\)|hsla?\([^)]+\)|color-mix\([^)]+\)|[a-zA-Z]+|(?:-?\d+(?:\.\d+)?(?:px|rem|em)?\s+){2,6}(?:#[0-9a-fA-F]{3,8}|rgba?\([^)]+\)|[a-zA-Z]+))$""", RegexOptions.CultureInvariant)]
    private static partial Regex CssColorOrShadowPattern();
}

public sealed record ColorSchemeDefinition(
    string Id,
    string Name,
    bool IsBuiltIn,
    ColorSchemeTokens Tokens,
    // Stamp of the last edit to this custom scheme. Used to merge custom schemes across devices
    // (newer timestamp wins per id). Built-ins leave this null and are never merged by id.
    DateTimeOffset? UpdatedAtUtc = null,
    bool IsDark = false);

public sealed record ColorSchemeTokens(
    string Background,
    string CardBackground,
    string CardBorder,
    string Ink,
    string Muted,
    string Primary,
    string Accent,
    string Danger,
    string Success,
    string Focus,
    string Shadow,
    string Surface,
    string SurfaceStrong,
    string ChartPrimary,
    string ChartSecondary,
    string TaskNegative,
    string TaskNeutral,
    string TaskPositive,
    string AppBarBackground,
    string AppBarText,
    string DrawerBackground,
    string DrawerText,
    string PrimaryButtonText,
    string DisabledBackground,
    string DisabledText,
    string DisabledBorder,
    string InputBackground,
    string InputBorder,
    GradientStops9? BackgroundGradient = null,
    GradientStops8? CardGradient = null,
    GradientStops6? AppBarGradient = null,
    GradientStops6? DrawerGradient = null,
    GradientStops4? PrimaryButtonGradient = null,
    GradientStops2? SecondaryButtonGradient = null,
    GradientStops2? AccentChipGradient = null,
    string? HeadingTextShadow = null,
    string? AppBarTextShadow = null,
    string? DrawerTextShadow = null,
    // Filled-secondary button label color. Optional/trailing so the many positional token
    // constructors keep compiling; Complete() backfills it from PrimaryButtonText when unset.
    // Lives here (not next to PrimaryButtonText) only because optional params must follow required ones.
    string? SecondaryButtonText = null);

public sealed record GradientStops9(
    string TopLeft, string Top, string TopRight,
    string MiddleLeft, string Middle, string MiddleRight,
    string BottomLeft, string Bottom, string BottomRight);

public sealed record GradientStops8(
    string TopLeft, string Top, string TopRight,
    string MiddleLeft, string MiddleRight,
    string BottomLeft, string Bottom, string BottomRight);

public sealed record GradientStops6(
    string TopLeft, string Top, string TopRight,
    string BottomLeft, string Bottom, string BottomRight);

public sealed record GradientStops4(
    string TopLeft, string TopRight,
    string BottomLeft, string BottomRight);

public sealed record GradientStops2(string Start, string End);

public sealed record ColorSchemeTokenDescriptor(string Name, string Label);

public sealed record ColorSchemePreferences(
    string SelectedSchemeId,
    IReadOnlyList<ColorSchemeDefinition> CustomSchemes,
    // Stamp of the last selection change. Used to pick the newer selection during cross-device
    // merge. A built-in active scheme syncs as just its id, custom schemes ship their full data.
    DateTimeOffset? SelectedAtUtc = null,
    int SchemaVersion = 0)
{
    public const int CurrentSchemaVersion = 2;
}

public sealed record ColorSchemeState(
    ColorSchemeDefinition ActiveScheme,
    IReadOnlyList<ColorSchemeDefinition> BuiltInSchemes,
    IReadOnlyList<ColorSchemeDefinition> CustomSchemes);
