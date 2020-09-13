using NUnit.Framework;
using System.Threading;
using Unity.PerformanceTesting;
using SampleGroup = Unity.PerformanceTesting.SampleGroup;

public class MeasureScope
{
    [Test, Performance]
    public void MeasureScope_WithoutDefinition_MeasuresDefaultSample()
    {
        using (Measure.Scope())
        {
            Thread.Sleep(1);
        }

        var result = PerformanceTest.Active;
        Assert.That(result.SampleGroups.Count, Is.EqualTo(1));
        Assert.That(result.SampleGroups[0].Samples[0], Is.GreaterThan(0.0f));
        AssertDefinition(result.SampleGroups[0], "Time", SampleUnit.Millisecond, false);
    }

    [Test, Performance]
    public void MeasureScope_WithDefinition_MeasuresSample()
    {
        using (Measure.Scope("TEST"))
        {
            Thread.Sleep(1);
        }

        var result = PerformanceTest.Active;
        Assert.That(result.SampleGroups.Count, Is.EqualTo(1));
        Assert.That(result.SampleGroups[0].Samples[0], Is.GreaterThan(0.0f));
        AssertDefinition(result.SampleGroups[0], "TEST", SampleUnit.Millisecond, false);
    }
    
    [Test, Performance]
    public void MeasureMultipleScopes_WithDefinition_MeasuresSamples()
    {
        using (Measure.Scope("TEST"))
        {
            Thread.Sleep(1);
        }
        
        using (Measure.Scope("TEST"))
        {
            Thread.Sleep(1);
        }
        
        using (Measure.Scope("TEST"))
        {
            Thread.Sleep(1);
        }
        
        using (Measure.Scope("TEST"))
        {
            Thread.Sleep(1);
        }

        var result = PerformanceTest.Active;
        Assert.That(result.SampleGroups.Count, Is.EqualTo(1));
        Assert.That(result.SampleGroups[0].Samples.Count, Is.EqualTo(4));
        Assert.That(result.SampleGroups[0].Samples[0], Is.GreaterThan(0.0f));
        AssertDefinition(result.SampleGroups[0], "TEST", SampleUnit.Millisecond, false);
    }
    
    private static void AssertDefinition(SampleGroup sampleGroup, string name, SampleUnit sampleUnit, bool increaseIsBetter)
    {
        Assert.AreEqual(sampleGroup.Name, name);
        Assert.AreEqual(sampleGroup.Unit, sampleUnit);
        Assert.AreEqual(sampleGroup.IncreaseIsBetter, increaseIsBetter);
    }
}