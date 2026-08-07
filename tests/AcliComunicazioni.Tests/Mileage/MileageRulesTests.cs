using AcliComunicazioni.Application.Mileage;
using Xunit;

namespace AcliComunicazioni.Tests.Mileage;

public sealed class MileageRulesTests
{
    private static readonly DateTime Today = new(2026, 8, 7);

    [Fact]
    public void ValidateInput_NormalizesRouteAndDescription()
    {
        var result = MileageRules.ValidateInput(
            18_200,
            18_230,
            new DateTime(2026, 8, 4),
            "  Trento - Rovereto  ",
            "  Riunione  ",
            Today);

        Assert.Equal("Trento - Rovereto", result.Route);
        Assert.Equal("Riunione", result.Description);
    }

    [Fact]
    public void ValidateInput_AllowsZeroKilometerEntry()
    {
        var result = MileageRules.ValidateInput(
            18_150,
            18_150,
            new DateTime(2026, 8, 4),
            "Spostamento annullato",
            null,
            Today);

        Assert.Equal("Spostamento annullato", result.Route);
        Assert.Null(result.Description);
    }

    [Fact]
    public void ValidateInput_RejectsNegativeStartKilometers()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            MileageRules.ValidateInput(
                -1,
                0,
                new DateTime(2026, 8, 4),
                "Trento",
                null,
                Today));

        Assert.Contains("I chilometri iniziali non possono essere negativi", exception.Message);
    }

    [Fact]
    public void ValidateInput_RejectsEndBeforeStart()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MileageRules.ValidateInput(
                18_200,
                18_199,
                new DateTime(2026, 8, 4),
                "Trento",
                null,
                Today));

        Assert.Contains("I chilometri finali non possono essere inferiori", exception.Message);
    }

    [Fact]
    public void ValidateInput_RejectsFutureDate()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MileageRules.ValidateInput(
                18_200,
                18_220,
                Today.AddDays(1),
                "Trento",
                null,
                Today));

        Assert.Contains("non può essere futura", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidateInput_RejectsMissingRoute(string? route)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MileageRules.ValidateInput(
                18_200,
                18_220,
                Today,
                route,
                null,
                Today));

        Assert.Equal("Inserisci il tragitto.", exception.Message);
    }

    [Fact]
    public void EnsureNoOverlap_AllowsAdjacentIntervals()
    {
        var existing = new[]
        {
            new MileageInterval(18_000, 18_100),
            new MileageInterval(18_200, 18_300)
        };

        MileageRules.EnsureNoOverlap(18_100, 18_150, existing);
        MileageRules.EnsureNoOverlap(18_150, 18_200, existing);
    }

    [Theory]
    [InlineData(18_150, 18_201)]
    [InlineData(18_199, 18_250)]
    [InlineData(18_210, 18_220)]
    [InlineData(18_050, 18_250)]
    public void EnsureNoOverlap_RejectsEveryOverlap(
        int startKilometers,
        int endKilometers)
    {
        var existing = new[]
        {
            new MileageInterval(18_000, 18_100),
            new MileageInterval(18_200, 18_300)
        };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            MileageRules.EnsureNoOverlap(startKilometers, endKilometers, existing));

        Assert.Contains("si sovrappone", exception.Message);
    }

    [Theory]
    [InlineData(2026, 8, 2)]
    [InlineData(2026, 8, 3)]
    [InlineData(2026, 8, 4)]
    public void EnsureDateWithinNeighbors_AcceptsDatesInsideGap(
        int year,
        int month,
        int day)
    {
        MileageRules.EnsureDateWithinNeighbors(
            new DateTime(year, month, day),
            new DateTime(2026, 8, 2),
            new DateTime(2026, 8, 4));
    }

    [Theory]
    [InlineData(2026, 8, 1, "successiva al 02/08/2026")]
    [InlineData(2026, 8, 5, "precedente al 04/08/2026")]
    public void EnsureDateWithinNeighbors_RejectsDatesOutsideGap(
        int year,
        int month,
        int day,
        string expectedMessage)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            MileageRules.EnsureDateWithinNeighbors(
                new DateTime(year, month, day),
                new DateTime(2026, 8, 2),
                new DateTime(2026, 8, 4)));

        Assert.Contains(expectedMessage, exception.Message);
    }

    [Theory]
    [InlineData(18_100, 18_189, 89)]
    [InlineData(18_150, 18_189, 39)]
    [InlineData(18_189, 18_189, 0)]
    [InlineData(18_200, 18_189, 0)]
    public void CalculateGap_ReturnsOnlyMissingKilometers(
        int previousEndKilometers,
        int startKilometers,
        int expectedGap)
    {
        var result = MileageRules.CalculateGap(
            previousEndKilometers,
            startKilometers);

        Assert.Equal(expectedGap, result);
    }
}
