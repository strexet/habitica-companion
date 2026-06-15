using Habitica.Application.Dashboard;
using Habitica.Domain.Dashboard;
using Habitica.Domain.User;

namespace Habitica.Application.Tests.Dashboard;

public sealed class HealthPotionRecoveryEstimateFactoryTests
{
    [Fact]
    public void Create_recommends_one_potion_when_it_removes_knockout_risk()
    {
        var estimate = new HealthPotionRecoveryEstimateFactory().Create(
            CreateUser(health: 10m, maxHealth: 50m, gold: 25m),
            CreateDamageEstimate(damage: 12m, risk: PendingDamageRisk.Danger));

        Assert.True(estimate.ShouldShow);
        Assert.Equal(1, estimate.RecommendedPotionCount);
        Assert.True(estimate.RecommendedCountRemovesKnockoutRisk);
        Assert.Equal(25m, estimate.ExpectedHealthAfterOnePotion);
        Assert.Equal(13m, estimate.ExpectedHealthAfterOnePotionAndCron);
        Assert.Contains("removes the current knockout risk", estimate.RecommendationText);
    }

    [Fact]
    public void Create_caps_recommendation_by_available_gold()
    {
        var estimate = new HealthPotionRecoveryEstimateFactory().Create(
            CreateUser(health: 5m, maxHealth: 50m, gold: 25m),
            CreateDamageEstimate(damage: 36m, risk: PendingDamageRisk.Danger));

        Assert.True(estimate.ShouldShow);
        Assert.Equal(1, estimate.AffordablePotionCount);
        Assert.Equal(1, estimate.RecommendedPotionCount);
        Assert.False(estimate.RecommendedCountRemovesKnockoutRisk);
    }

    [Fact]
    public void Create_recommends_multiple_potions_when_required_for_survival()
    {
        var estimate = new HealthPotionRecoveryEstimateFactory().Create(
            CreateUser(health: 5m, maxHealth: 50m, gold: 100m),
            CreateDamageEstimate(damage: 36m, risk: PendingDamageRisk.Danger));

        Assert.True(estimate.ShouldShow);
        Assert.Equal(3, estimate.RecommendedPotionCount);
        Assert.True(estimate.RecommendedCountRemovesKnockoutRisk);
        Assert.Contains("3 Health Potions", estimate.RecommendationText);
    }

    [Fact]
    public void Create_caps_healing_at_maximum_health()
    {
        var estimate = new HealthPotionRecoveryEstimateFactory().Create(
            CreateUser(health: 42m, maxHealth: 50m, gold: 100m),
            CreateDamageEstimate(damage: 35m, risk: PendingDamageRisk.Warning));

        Assert.True(estimate.ShouldShow);
        Assert.Equal(1, estimate.MaximumUsefulPotionCount);
        Assert.Equal(8m, estimate.EffectiveHealingFromOnePotion);
        Assert.Equal(50m, estimate.ExpectedHealthAfterOnePotion);
        Assert.Equal(15m, estimate.ExpectedHealthAfterOnePotionAndCron);
    }

    [Fact]
    public void Create_hides_recovery_when_full_health_and_no_damage()
    {
        var estimate = new HealthPotionRecoveryEstimateFactory().Create(
            CreateUser(health: 50m, maxHealth: 50m, gold: 100m),
            CreateDamageEstimate(damage: 0m, risk: PendingDamageRisk.None));

        Assert.False(estimate.ShouldShow);
        Assert.False(estimate.CanBuySinglePotion);
        Assert.Equal(0, estimate.MaximumUsefulPotionCount);
    }

    [Fact]
    public void Create_marks_incomplete_damage_without_safe_claim()
    {
        var estimate = new HealthPotionRecoveryEstimateFactory().Create(
            CreateUser(health: 10m, maxHealth: 50m, gold: 25m),
            CreateDamageEstimate(
                damage: 0m,
                risk: PendingDamageRisk.Info,
                readiness: PendingDamageReadiness.Incomplete));

        Assert.True(estimate.ShouldShow);
        Assert.True(estimate.IsBasedOnIncompleteDamageEstimate);
        Assert.Contains("incomplete", estimate.RecommendationText);
    }

    [Fact]
    public void Create_does_not_claim_survival_when_danger_estimate_is_incomplete()
    {
        var estimate = new HealthPotionRecoveryEstimateFactory().Create(
            CreateUser(health: 10m, maxHealth: 50m, gold: 25m),
            CreateDamageEstimate(
                damage: 12m,
                risk: PendingDamageRisk.Danger,
                readiness: PendingDamageReadiness.Incomplete));

        Assert.True(estimate.ShouldShow);
        Assert.False(estimate.RecommendedCountRemovesKnockoutRisk);
        Assert.Contains("incomplete estimate", estimate.RecommendationText);
        Assert.DoesNotContain("removes the current knockout risk", estimate.RecommendationText);
    }

    [Fact]
    public void Create_hides_recovery_when_damage_is_paused_by_inn()
    {
        var estimate = new HealthPotionRecoveryEstimateFactory().Create(
            CreateUser(health: 10m, maxHealth: 50m, gold: 25m),
            CreateDamageEstimate(
                damage: 0m,
                risk: PendingDamageRisk.Info,
                isDamagePausedByInn: true));

        Assert.False(estimate.ShouldShow);
    }

    private static PendingDamageEstimate CreateDamageEstimate(
        decimal damage,
        PendingDamageRisk risk,
        PendingDamageReadiness readiness = PendingDamageReadiness.Estimated,
        IReadOnlyList<string>? excludedSources = null,
        bool isDamagePausedByInn = false)
    {
        return new PendingDamageEstimate(
            damage,
            Math.Max(0m, 10m - damage),
            Array.Empty<PendingDamageSource>(),
            excludedSources ?? Array.Empty<string>(),
            risk,
            readiness,
            IsDamagePausedByInn: isDamagePausedByInn);
    }

    private static UserSnapshot CreateUser(decimal health, decimal maxHealth, decimal gold)
    {
        return new UserSnapshot(
            DateTimeOffset.Parse("2026-04-25T08:00:00Z"),
            "Mage Tester",
            "wizard",
            15,
            health,
            maxHealth,
            33.5m,
            40m,
            125.1m,
            74.9m,
            gold,
            "party-123",
            null,
            null,
            new EquipmentSnapshot(
                new GearSlotsSnapshot(null, null, null, null, null),
                new GearSlotsSnapshot(null, null, null, null, null)),
            new InventorySnapshot(0, 0, 0, 0, 0, 0, Array.Empty<string>()));
    }
}
