using System;
using System.Reflection.Emit;
using System.Xml.Schema;
using System.Xml;
using System.Xml.Serialization;
using Xunit.Abstractions;
using Microsoft.VisualStudio.TestPlatform.Utilities;
using Ltfs.Label;
using Ltfs;
using Ltfs.Index;

namespace LtfsTest;

public class LtfsMAMTest
{
    [Fact]
    public void MAMSetTest()
    {
        MAMAttributes ltfsMAMAttributes = new MAMAttributes();

        try
        {
            ltfsMAMAttributes.ApplicationVendor.SetAsciiString("capybara");
            ltfsMAMAttributes.ApplicationName.SetAsciiString("LTFS capybara");
            ltfsMAMAttributes.ApplicationVersion.SetAsciiString("0.0.1");

            ltfsMAMAttributes.UserMediumTextLabel.SetTextString("VOL001");
            ltfsMAMAttributes.TextLocalizationIdentifier.SetBinary([0x81]);

            ltfsMAMAttributes.Barcode.SetAsciiString("TEST01L6");

            ltfsMAMAttributes.ApplicationFormatVersion.SetAsciiString("2.4.0");

            //ltfsMAMAttributes.MediaPool.SetTextString("");

            //ltfsMAMAttributes.MediumGloballyUniqueIdentifier.SetAsciiString("");
            //ltfsMAMAttributes.MediaPoolGloballyUniqueIdentifier.SetAsciiString("");

            Logger.Debug("set MAM");
        }
        catch (Exception ex)
        {
            Assert.Fail(ex.Message);
        }
    }

}
