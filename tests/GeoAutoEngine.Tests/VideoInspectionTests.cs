using System.Text.Json.Nodes;
using GeoAutoEngine.Application.VideoInspection;

namespace GeoAutoEngine.Tests;

public sealed class VideoInspectionContractTests
{
    private readonly InspectionExchangeSerializer _serializer = new();

    [Fact]
    [Trait("Category", "FAST")]
    public void OfficialGeoAutoDocsV1FixtureDeserializesAndPreservesStableIdentity()
    {
        var document = _serializer.Deserialize(File.ReadAllText(FixturePath()));

        Assert.Equal(InspectionExchangeSchema.Name, document.Schema);
        Assert.Equal(InspectionExchangeSchema.CurrentVersion, document.SchemaVersion);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), document.Project.ProjectId);
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), document.Report.ReportId);
        Assert.Equal(Guid.Parse("44444444-4444-4444-4444-444444444444"), document.Sections.Single().SectionId);
        Assert.Equal(TimeSpan.FromSeconds(15), document.Sections.Single().Occurrences[0].VideoTimestamp);
        Assert.Equal([2m, 3m, 4m, 5m], document.Sections.Single().Occurrences[1].MeteragePoints);
    }

    [Theory]
    [InlineData("Other.Schema", 1, typeof(InvalidDataException))]
    [InlineData("GeoAutoDocs.Inspection", 2, typeof(NotSupportedException))]
    [Trait("Category", "FAST")]
    public void IncompatibleSchemaOrVersionIsRejected(string schema, int version, Type exceptionType)
    {
        var root = JsonNode.Parse(File.ReadAllText(FixturePath()))!.AsObject();
        root["schema"] = schema;
        root["schemaVersion"] = version;

        Assert.Throws(exceptionType, () => _serializer.Deserialize(root.ToJsonString()));
    }

    private static string FixturePath() => Path.Combine(AppContext.BaseDirectory, "Fixtures", "sample.geoadocs.json");
}

public sealed class LinearReferenceEngineTests
{
    private readonly LinearReferenceEngine _engine = new();
    private readonly StraightGeometry _geometry = new(100);

    [Fact]
    [Trait("Category", "FAST")]
    public void PointForwardUsesAbsoluteInspectionDistance()
    {
        var result = Resolve(Mode("Point", meterage: 17m), InspectionDirection.Forward);

        var point = Assert.Single(result.Locations);
        Assert.Equal(17, point.InspectionDistance);
        Assert.Equal(17, point.CadDistance);
        Assert.Equal(17, point.Position.X);
        Assert.True(result.CanCommit);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void PointReverseUsesGeometryLengthMinusInspectionDistance()
    {
        var result = Resolve(Mode("Point", meterage: 17m), InspectionDirection.Reverse);

        var point = Assert.Single(result.Locations);
        Assert.Equal(83, point.CadDistance);
        Assert.Equal(83, point.Position.X);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MultiPointPreservesOccurrenceAndCreatesIndexedLocations()
    {
        var occurrence = Mode("MultiPoint", points: [2m, 3m, 4m, 5m]);

        var result = Resolve(occurrence, InspectionDirection.Forward);

        Assert.Equal(occurrence.OccurrenceId, result.OccurrenceId);
        Assert.Equal([0, 1, 2, 3], result.Locations.Select(location => location.LocationIndex));
        Assert.Equal([2d, 3d, 4d, 5d], result.Locations.Select(location => location.CadDistance));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void RangeProducesStartAndEndWithoutProportionalCorrection()
    {
        var result = Resolve(Mode("Range", 12m, 40m), InspectionDirection.Reverse);

        Assert.Equal([PositionedLocationRole.Start, PositionedLocationRole.End],
            result.Locations.Select(location => location.Role));
        Assert.Equal([88d, 60d], result.Locations.Select(location => location.CadDistance));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void FromUsesInvestigatedMetersAsEnd()
    {
        var result = _engine.Resolve(Mode("From", 50m), 80m, _geometry, InspectionDirection.Forward);

        Assert.Equal([50d, 80d], result.Locations.Select(location => location.InspectionDistance));
        Assert.True(result.CanCommit);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void EntireSectionUsesZeroThroughInvestigatedMeters()
    {
        var result = _engine.Resolve(Mode("EntireSection"), 80m, _geometry, InspectionDirection.Reverse);

        Assert.Equal([0d, 80d], result.Locations.Select(location => location.InspectionDistance));
        Assert.Equal([100d, 20d], result.Locations.Select(location => location.CadDistance));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void NotApplicableRemainsVisibleWithoutCadMarker()
    {
        var result = Resolve(Mode("NotApplicable"), InspectionDirection.Forward);

        Assert.Empty(result.Locations);
        Assert.True(result.CanCommit);
        Assert.Contains(result.Warnings, warning => warning.Contains("Sem posição CAD", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void OccurrenceBeyondGeometryRequiresAttentionInsteadOfBeingNormalized()
    {
        var result = Resolve(Mode("Point", meterage: 101m), InspectionDirection.Forward);

        Assert.Empty(result.Locations);
        Assert.False(result.CanCommit);
        Assert.Contains(result.Warnings, warning => warning.Contains("fora da geometria", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void StableKeyIsDeterministicAndIndependentOfObjectId()
    {
        var report = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var section = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var occurrence = Guid.Parse("55555555-5555-5555-5555-555555555555");

        var first = VideoInspectionStableKey.Create(report, section, occurrence, 0, PositionedLocationRole.Point);
        var second = VideoInspectionStableKey.Create(report, section, occurrence, 0, PositionedLocationRole.Point);

        Assert.Equal(first, second);
        Assert.Equal(
            "VI:22222222-2222-2222-2222-222222222222:44444444-4444-4444-4444-444444444444:" +
            "55555555-5555-5555-5555-555555555555:0:POINT",
            first);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MultipleInspectionSectionsCanReferenceTheSamePhysicalGeometry()
    {
        var reportId = Guid.NewGuid();
        var first = Section(Guid.NewGuid());
        var second = Section(Guid.NewGuid());

        first.Associate(new CadSectionAssociation(reportId, first.SectionId, "1A2B", InspectionDirection.Forward, 100));
        second.Associate(new CadSectionAssociation(reportId, second.SectionId, "1A2B", InspectionDirection.Reverse, 100));

        Assert.Equal(first.Association!.GeometryHandle, second.Association!.GeometryHandle);
        Assert.NotEqual(first.SectionId, second.SectionId);
    }

    private PositionedInspectionEvent Resolve(InspectionOccurrenceSession occurrence, InspectionDirection direction) =>
        _engine.Resolve(occurrence, 80m, _geometry, direction);

    private static InspectionOccurrenceSession Mode(
        string mode,
        decimal? meterage = null,
        decimal? end = null,
        IReadOnlyList<decimal>? points = null) => new(
            Guid.NewGuid(),
            0,
            Enum.TryParse(mode, out InspectionLocationMode parsed) ? parsed : InspectionLocationMode.Unknown,
            mode,
            meterage,
            end,
            points ?? [],
            null,
            null,
            null,
            "Ocorrência de teste",
            false,
            false,
            null,
            null);

    private static InspectionSectionSession Section(Guid sectionId) => new(
        sectionId,
        Guid.NewGuid(),
        "Local",
        "Trecho",
        null,
        null,
        null,
        null,
        null,
        100m,
        0,
        [],
        []);

    private sealed class StraightGeometry(double length) : ILinearReferenceGeometry
    {
        public double Length { get; } = length;
        public LinearPoint GetPointAtDistance(double distance) => new(distance, 0, 0);
        public LinearVector GetTangentAtDistance(double distance) => new(1, 0, 0);
    }
}

public sealed class InspectionAnnotationPlannerTests
{
    [Theory]
    [InlineData(8.0, 2.0)]
    [InlineData(4.0, 1.0)]
    [InlineData(1.2, 0.3)]
    [Trait("Category", "FAST")]
    public void EntireSectionOffsetUsesQuarterLengthCappedAtTwoMeters(
        double geometryLength,
        double expectedOffset)
    {
        var offset = InspectionAnnotationPlanner.EntireSectionAnnotationOffset(
            Convert.ToDecimal(geometryLength),
            geometryLength);

        Assert.Equal(expectedOffset, offset, 8);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void EntireSectionReverseAnchorIsMeasuredFromGeometryEnd()
    {
        var cadDistance = InspectionAnnotationPlanner.EntireSectionCadDistance(
            1.2m,
            1.2,
            InspectionDirection.Reverse);

        Assert.Equal(0.9, cadDistance, 8);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void CallsUseTechnicalTwoLineContentAndOneAnchorForRanges()
    {
        var point = Event(InspectionLocationMode.Point, "Infiltração", Location(0, 12));
        var range = Event(
            InspectionLocationMode.Range,
            "Deslocamento das juntas",
            Location(0, 12, PositionedLocationRole.Start),
            Location(1, 40, PositionedLocationRole.End));
        var from = Event(
            InspectionLocationMode.From,
            "Infiltração",
            Location(0, 50, PositionedLocationRole.Start),
            Location(1, 80, PositionedLocationRole.End));
        var entire = Event(
            InspectionLocationMode.EntireSection,
            "Deslocamento das juntas",
            Location(0, 0, PositionedLocationRole.Start, cadDistance: 0.3));

        var calls = InspectionAnnotationPlanner.Create([point, range, from, entire]);

        Assert.Equal(4, calls.Count);
        Assert.Equal("12,00 m", calls[0].LocationLine);
        Assert.Equal("INFILTRAÇÃO", calls[0].Cause);
        Assert.Equal("12,00 m – 40,00 m", calls[1].LocationLine);
        Assert.Equal("A PARTIR DE 50,00 m", calls[2].LocationLine);
        Assert.Equal("TODO O TRECHO", calls[3].LocationLine);
        Assert.Equal(0.3, calls[3].Anchor.CadDistance, 8);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void LateralContributionPreservesDirectionReportedByInspection()
    {
        var positioned = new PositionedInspectionEvent(
            Guid.NewGuid(),
            InspectionLocationMode.Point,
            "Contribuição lateral",
            [Location(0, 13)],
            [],
            true,
            "Direita");

        var call = Assert.Single(InspectionAnnotationPlanner.Create([positioned]));

        Assert.Equal("CONTRIBUIÇÃO LATERAL", call.Cause);
        Assert.Equal("DIREITA", call.PositionLine);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void MultiPointKeepsOccurrenceIdAndLocationIndexWhileNotApplicableCreatesNoCall()
    {
        var occurrenceId = Guid.NewGuid();
        var multiPoint = new PositionedInspectionEvent(
            occurrenceId,
            InspectionLocationMode.MultiPoint,
            "Juntas",
            [Location(0, 2), Location(1, 5)],
            [],
            true);
        var notApplicable = Event(InspectionLocationMode.NotApplicable, "Sem posição");

        var calls = InspectionAnnotationPlanner.Create([multiPoint, notApplicable]);

        Assert.Equal(2, calls.Count);
        Assert.All(calls, call => Assert.Equal(occurrenceId, call.OccurrenceId));
        Assert.Equal([0, 1], calls.Select(call => call.Anchor.LocationIndex));
    }

    private static PositionedInspectionEvent Event(
        InspectionLocationMode mode,
        string description,
        params PositionedInspectionLocation[] locations) => new(
            Guid.NewGuid(),
            mode,
            description,
            locations,
            [],
            true);

    private static PositionedInspectionLocation Location(
        int index,
        double inspectionDistance,
        PositionedLocationRole role = PositionedLocationRole.Point,
        double? cadDistance = null) => new(
            index,
            role,
            inspectionDistance,
            cadDistance ?? inspectionDistance,
            new LinearPoint(cadDistance ?? inspectionDistance, 0, 0),
            new LinearVector(1, 0, 0));
}


public sealed class InspectionCalloutLayoutEngineTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public void NearbyAnomaliesKeepTechnicalAnchorsAndReceiveDistinctPlacements()
    {
        var calls = new[] { 5.20, 5.50, 5.85, 6.10 }
            .Select((distance, index) => Call(index, distance, new LinearVector(1, 0, 0)))
            .ToArray();

        var placements = InspectionCalloutLayoutEngine.Arrange(calls);

        Assert.Equal(4, placements.Count);
        Assert.Equal(
            new[] { 5.20, 5.50, 5.85, 6.10 },
            placements.Select(item => item.Call.Anchor.CadDistance));
        Assert.All(
            placements,
            item => Assert.Equal(item.Call.Anchor.CadDistance, item.Call.Anchor.Position.X, 8));
        Assert.Equal(
            4,
            placements.Select(item => (item.TextPoint.X, item.TextPoint.Y)).Distinct().Count());
        Assert.Contains(placements, item => item.Side == 1);
        Assert.Contains(placements, item => item.Side == -1);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public void ReverseDirectionKeepsTextNaturallyReadable()
    {
        var placement = Assert.Single(InspectionCalloutLayoutEngine.Arrange(
            [Call(0, 12.35, new LinearVector(-1, 0, 0))]));

        Assert.Equal(0, placement.TextRotationRadians, 8);
        Assert.Equal(0.5, InspectionCalloutLayoutEngine.TextHeight, 8);
        Assert.Equal(12.35, placement.Call.Anchor.CadDistance, 8);
    }

    private static InspectionAnnotationCall Call(
        int index,
        double distance,
        LinearVector tangent)
    {
        var location = new PositionedInspectionLocation(
            index,
            PositionedLocationRole.Point,
            distance,
            distance,
            new LinearPoint(distance, 0, 0),
            tangent);
        return new InspectionAnnotationCall(
            Guid.NewGuid(),
            InspectionLocationMode.Point,
            location,
            $"{distance:0.00} m",
            "ANOMALIA");
    }
}

public sealed class InspectionPipeProtocolTests
{
    [Fact]
    [Trait("Category", "FAST")]
    public async Task HeaderMatchesOfficialGadxV1LittleEndianLayoutByteForByte()
    {
        await using var stream = new MemoryStream();
        await InspectionPipeProtocol.WriteFrameAsync(
            stream,
            InspectionPipeMessageType.InspectionPush,
            0x01020304,
            [0xAA, 0xBB]);

        Assert.Equal(
            new byte[]
            {
                0x47, 0x41, 0x44, 0x58,
                0x01, 0x00,
                0x03, 0x00,
                0x02, 0x00, 0x00, 0x00,
                0x04, 0x03, 0x02, 0x01,
                0xAA, 0xBB,
            },
            stream.ToArray());
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task ValidFrameRoundTripsMessageRequestIdVersionAndPayload()
    {
        await using var stream = new MemoryStream();
        var payload = InspectionPipeProtocol.SerializePayload(new InspectionAcknowledgement
        {
            Accepted = true,
            Status = "Success",
            Message = "OK",
        });
        await InspectionPipeProtocol.WriteFrameAsync(
            stream,
            InspectionPipeMessageType.InspectionAck,
            123456,
            payload);
        stream.Position = 0;

        var frame = await InspectionPipeProtocol.ReadFrameAsync(stream);

        Assert.Equal(InspectionPipeProtocol.CurrentVersion, frame.ProtocolVersion);
        Assert.Equal(InspectionPipeMessageType.InspectionAck, frame.MessageType);
        Assert.Equal(123456, frame.RequestId);
        Assert.Equal(payload, frame.Payload);
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task TruncatedHeaderIsRejected()
    {
        await using var stream = new MemoryStream(new byte[InspectionPipeProtocol.HeaderLength - 1]);

        await Assert.ThrowsAsync<EndOfStreamException>(() => InspectionPipeProtocol.ReadFrameAsync(stream));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task InvalidMagicIsRejected()
    {
        var bytes = await HeaderBytes();
        bytes[0] = (byte)'X';
        await using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<InvalidDataException>(() => InspectionPipeProtocol.ReadFrameAsync(stream));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task UnknownMessageTypeIsRejected()
    {
        var bytes = await HeaderBytes();
        bytes[6] = 99;
        bytes[7] = 0;
        await using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<InvalidDataException>(() => InspectionPipeProtocol.ReadFrameAsync(stream));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task DeclaredPayloadAboveSixteenMebibytesIsRejectedBeforePayloadAllocation()
    {
        var bytes = await HeaderBytes();
        WriteInt32LittleEndian(bytes, 8, InspectionPipeProtocol.MaxPayloadBytes + 1);
        await using var stream = new MemoryStream(bytes);

        await Assert.ThrowsAsync<InvalidDataException>(() => InspectionPipeProtocol.ReadFrameAsync(stream));
    }

    [Fact]
    [Trait("Category", "FAST")]
    public async Task IncompletePayloadIsRejected()
    {
        var bytes = await HeaderBytes();
        WriteInt32LittleEndian(bytes, 8, 4);
        await using var stream = new MemoryStream(bytes.Concat(new byte[3]).ToArray());

        await Assert.ThrowsAsync<EndOfStreamException>(() => InspectionPipeProtocol.ReadFrameAsync(stream));
    }

    private static async Task<byte[]> HeaderBytes()
    {
        await using var stream = new MemoryStream();
        await InspectionPipeProtocol.WriteFrameAsync(stream, InspectionPipeMessageType.Hello, 7, []);
        return stream.ToArray();
    }

    private static void WriteInt32LittleEndian(byte[] buffer, int offset, int value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }
}
