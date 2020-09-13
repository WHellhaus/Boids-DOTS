using NUnit.Framework;
using Unity.PerformanceTesting;
using Unity.PerformanceTesting.Exceptions;
using SampleGroup = Unity.PerformanceTesting.SampleGroup;

public class MeasureCustomTests
{
    [Test, Performance]
    public void MeasureCustom_SampleGroup_CorrectValues()
    {
        SampleGroup sg = new SampleGroup("REGULAR", SampleUnit.Byte, true);
        Measure.Custom(sg, 10D);

        var test = PerformanceTest.Active;
        Assert.AreEqual(test.SampleGroups.Count, 1);
        Assert.AreEqual(test.SampleGroups[0].Samples.Count, 1);
        Assert.AreEqual(test.SampleGroups[0].Samples[0], 10D, 0.001D);
        AssertDefinition(test.SampleGroups[0], "REGULAR", SampleUnit.Byte, true);
    }

    [Test, Performance]
    public void MeasureCustom_SampleGroupWithSamples_CorrectValues()
    {
        SampleGroup sg = new SampleGroup("REGULAR", SampleUnit.Byte, true);

        Measure.Custom(sg, 10D);
        Measure.Custom(sg, 20D);

        var test = PerformanceTest.Active;
        Assert.AreEqual(test.SampleGroups.Count, 1);
        Assert.AreEqual(test.SampleGroups[0].Samples.Count, 2);
        Assert.AreEqual(test.SampleGroups[0].Samples[0], 10D, 0.001D);
        Assert.AreEqual(test.SampleGroups[0].Samples[1], 20D, 0.001D);
        AssertDefinition(test.SampleGroups[0], "REGULAR", SampleUnit.Byte, true);
    }

    [Test, Performance]
    public void MeasureCustom_PercentileSample_CorrectValues()
    {
        SampleGroup sg = new SampleGroup("PERCENTILE", SampleUnit.Second, true);
        Measure.Custom(sg, 10D);

        var test = PerformanceTest.Active;
        Assert.AreEqual(test.SampleGroups.Count, 1);
        Assert.AreEqual(test.SampleGroups[0].Samples.Count, 1);
        Assert.AreEqual(test.SampleGroups[0].Samples[0], 10D, 0.001D);
        AssertDefinition(test.SampleGroups[0], "PERCENTILE", SampleUnit.Second, true);
    }

    [Test, Performance]
    public void MeasureCustom_PercentileSamples_CorrectValues()
    {
        var sg = new SampleGroup("PERCENTILE", SampleUnit.Second, true);

        Measure.Custom(sg, 10D);
        Measure.Custom(sg, 20D);

        var test = PerformanceTest.Active;
        Assert.AreEqual(test.SampleGroups.Count, 1);
        Assert.AreEqual(test.SampleGroups[0].Samples.Count, 2);
        Assert.AreEqual(test.SampleGroups[0].Samples[0], 10D, 0.001D);
        Assert.AreEqual(test.SampleGroups[0].Samples[1], 20D, 0.001D);
        AssertDefinition(test.SampleGroups[0], "PERCENTILE", SampleUnit.Second, true);
    }

    [Test, Performance]
    public void MeasureCustom_MultipleSampleGroups()
    {
        SampleGroup sg = new SampleGroup("REGULAR", SampleUnit.Byte, true);
        SampleGroup sg2 = new SampleGroup("PERCENTILE", SampleUnit.Second, true);
        Measure.Custom(sg, 20D);
        Measure.Custom(sg2, 10D);

        var test = PerformanceTest.Active;
        Assert.AreEqual(test.SampleGroups.Count, 2);
        AssertDefinition(test.SampleGroups[0], "REGULAR", SampleUnit.Byte, true);
        AssertDefinition(test.SampleGroups[1], "PERCENTILE", SampleUnit.Second, true);
    }

    [Test, Performance]
    public void MeasureCustom_NaN_Throws()
    {
        SampleGroup sg = new SampleGroup("REGULAR", SampleUnit.Byte, true);

        Assert.Throws<PerformanceTestException>(() => Measure.Custom(sg, double.NaN));
    }

    private static void AssertDefinition(SampleGroup sampleGroup, string name, SampleUnit sampleUnit,
        bool increaseIsBetter)
    {
        Assert.AreEqual(sampleGroup.Name, name);
        Assert.AreEqual(sampleGroup.Unit, sampleUnit);
        Assert.AreEqual(sampleGroup.IncreaseIsBetter, increaseIsBetter);
    }
}