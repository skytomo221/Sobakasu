using NUnit.Framework;
using Sobakasu.SobakasuCompiler;

namespace Tests;

public class Tests
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void Test1()
    {
        Assert.Pass();
    }

    [Test]
    public void Parse()
    {
        SobakasuCompilerParser compiler = new SobakasuCompilerParser();
        compiler.Parse("123");
    }
}
