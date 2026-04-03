using Microsoft.AspNetCore.Builder;

using TapeDrive;
using TapeDrive.Utils;

namespace LtfsServer.Features.Test;

public static class APITest
{
    public static void MapTestApi(this WebApplication app)
    {
        app.MapGet("/api/test/struct-metadata", () =>
        {
            var sample = new FixedFormatSenseData
            {
                Valid = true,
                ErrorCode = 0x70,
                Mark = true,
                EOM = true,
                ILI = false,
                SenseKey = 0x03,
                InformationBytes = [0x12, 0x34, 0x56, 0x78],
                AdditionalSenseLength = 0x10,
                CommandSpecificInformationBytes = [0x9A, 0xBC, 0xDE, 0xF0],
                AdditionalSenseCode = 0x30,
                AdditionalSenseCodeQualifier = 0x07,
                FieldReplaceableUnitCode = 0x7E,
                SKSV = true,
                CPE = false,
                BPV = true,
                BitPointer = 0x05,
                FieldPointerOrDriveErrorCode = 0x1122,
                CLN = true,
            };

            return Results.Ok(StructParser.ToMetadataDocument(sample));
        });
    }
}