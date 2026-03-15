using FluentAssertions;
using foamscript.Services;

namespace foamscript.Tests.Services;

public class VtkSliceParserTests
{
    private const string SampleVtk = """
        # vtk DataFile Version 5.1
        vtk output
        ASCII
        DATASET POLYDATA
        POINTS 4 float
        0.0 0.0 0.0
        1.0 0.0 0.0
        1.0 0.0 1.0
        0.0 0.0 1.0
        POLYGONS 2 8
        3 0 1 2
        3 0 2 3
        POINT_DATA 4
        FIELD FieldData 2
        p 1 4 float
        100.0 200.0 150.0 120.0
        U 3 4 float
        10.0 0.0 0.0
        20.0 0.0 0.0
        15.0 0.0 0.0
        12.0 0.0 0.0
        """;

    [Fact]
    public void Parse_ExtractsPointCoordinates()
    {
        var result = VtkSliceParser.Parse(SampleVtk);

        result.Points.Should().HaveCount(4);
        result.Points[0].X.Should().BeApproximately(0.0, 1e-6);
        result.Points[0].Z.Should().BeApproximately(0.0, 1e-6);
        result.Points[2].X.Should().BeApproximately(1.0, 1e-6);
        result.Points[2].Z.Should().BeApproximately(1.0, 1e-6);
    }

    [Fact]
    public void Parse_ExtractsScalarField()
    {
        var result = VtkSliceParser.Parse(SampleVtk);

        result.ScalarFields.Should().ContainKey("p");
        result.ScalarFields["p"].Should().HaveCount(4);
        result.ScalarFields["p"][0].Should().BeApproximately(100.0, 1e-6);
    }

    [Fact]
    public void Parse_ExtractsVectorFieldAsMagnitude()
    {
        var result = VtkSliceParser.Parse(SampleVtk);

        // U is a 3-component vector; parser should compute magnitude
        result.ScalarFields.Should().ContainKey("U");
        result.ScalarFields["U"][0].Should().BeApproximately(10.0, 1e-6);
        result.ScalarFields["U"][1].Should().BeApproximately(20.0, 1e-6);
    }

    [Fact]
    public void Parse_EmptyInput_ReturnsEmptyResult()
    {
        var result = VtkSliceParser.Parse("");

        result.Points.Should().BeEmpty();
        result.ScalarFields.Should().BeEmpty();
    }
}
