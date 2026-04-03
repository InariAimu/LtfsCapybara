using Microsoft.AspNetCore.Builder;

using TapeDrive.SCSICommands;
using TapeDrive.Utils;

namespace LtfsServer.Features.Test;

public static class APITest
{
    public static void MapTestApi(this WebApplication app)
    {
        app.MapGet("/api/test/struct-metadata", () =>
        {
            var sample = new SenseResponse
            {
                StatusQualifier = 0x1234,
                DATAPRES = 0x02,
                Status = 0x5A,
                ResponseData = [0xDE, 0xAD, 0xBE, 0xEF],
                SenseData = [0x70, 0x00, 0x03, 0x12, 0x34, 0x56, 0x78, 0x0A],
            };

            return Results.Ok(StructParser.ToMetadataDocument(sample));
        });
    }
}