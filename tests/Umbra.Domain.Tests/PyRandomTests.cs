using Umbra.Domain.Util;

namespace Umbra.Domain.Tests;

/// <summary>PyRandom against values printed by CPython 3.14 (random.seed(s); random(), randint(-5, 5), choice("abcdefg")).</summary>
public class PyRandomTests
{
    [Theory]
    [InlineData(0L, new[] { 0.8444218515250481, 0.7579544029403025, 0.420571580830845 }, new[] { -1, 3, 2, 1, -1, 2 }, "cebeb")]
    [InlineData(1L, new[] { 0.13436424411240122, 0.8474337369372327, 0.763774618976614 }, new[] { -1, -4, 2, 2, 2, 5 }, "dgbad")]
    [InlineData(42L, new[] { 0.6394267984578837, 0.025010755222666936, 0.27502931836911926 }, new[] { -2, -3, -4, 5, 3, -4 }, "edaaa")]
    [InlineData(7000L, new[] { 0.32245340231603203, 0.33955395652292353, 0.22368546883478369 }, new[] { 3, -5, 3, -3, 4, -1 }, "daaab")]
    [InlineData(2028149L, new[] { 0.8266514511878965, 0.48945666443751956, 0.3012202723901065 }, new[] { 5, 3, -2, -4, -2, 3 }, "aedcd")]
    [InlineData(1099511627781L, new[] { 0.5043802970418443, 0.2686044399723282, 0.9257865475671585 }, new[] { 3, -5, -3, -4, -3, -2 }, "ffbbf")]
    public void SameDrawsAsCPython(long seed, double[] randoms, int[] ints, string choices)
    {
        var rng = new PyRandom(seed);
        Assert.Equal(randoms, new[] { rng.Random(), rng.Random(), rng.Random() });
        Assert.Equal(ints, Enumerable.Range(0, 6).Select(_ => rng.RandInt(-5, 5)).ToArray());
        var letters = "abcdefg".ToCharArray();
        Assert.Equal(choices, new string(Enumerable.Range(0, 5).Select(_ => rng.Choice(letters)).ToArray()));
    }

    /// <summary>random.seed(s); 12 x choices('ABCD', weights=[40,28,29,3]); 4 x sample(13 letters, 3); 2 x sample(26 letters, 7).</summary>
    [Theory]
    [InlineData(3L, "ABABBAACAADB", "ihg,kcd,kci,gla", "vycfsbj,yaiptmn")]
    [InlineData(99L, "BAAACAACBCBB", "jmd,mfg,kdc,hkc", "skcwqxp,kmbiran")]
    public void WeightedAndSampleAsCPython(long seed, string weighted, string small, string large)
    {
        var rng = new PyRandom(seed); var abcd = "ABCD".ToCharArray(); var weights = new double[] { 40, 28, 29, 3 };
        Assert.Equal(weighted, new string(Enumerable.Range(0, 12).Select(_ => rng.Weighted(abcd, weights)).ToArray()));
        var thirteen = "abcdefghijklm".ToCharArray(); var alphabet = "abcdefghijklmnopqrstuvwxyz".ToCharArray();
        Assert.Equal(small, string.Join(",", Enumerable.Range(0, 4).Select(_ => new string(rng.Sample(thirteen, 3).ToArray()))));
        Assert.Equal(large, string.Join(",", Enumerable.Range(0, 2).Select(_ => new string(rng.Sample(alphabet, 7).ToArray()))));
    }
}
