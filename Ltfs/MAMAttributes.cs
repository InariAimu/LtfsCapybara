using TapeDrive;

namespace Ltfs;

using WriteFunction = Func<ushort, byte[], AttributeFormat, byte, bool>;
using ReadFunction = Func<ushort, byte, byte[]>;


public class MAMAttributes
{
    /// <summary>
    /// Application Vendor
    /// This attribute shall be set to indicate the manufacturer of the LTFS software which formatted the volume.
    /// It shall be consistent with the Company name (if any) used in the Creator format in LTFS label and index constructs (see Section 7.2 Creator format).
    /// The attribute shall be left-aligned, and shall be padded with ASCII space (20h) characters if the company name is less than 8 characters in length.
    /// If the company name exceeds 8 ASCII characters then the 8 left-most characters of the name shall be used.
    /// </summary>
    public MAMAttribute ApplicationVendor { get; } = new(0x0800, 8, AttributeFormat.Ascii);

    /// <summary>
    /// Application Name
    /// This attribute shall be set to the ASCII string "LTFS", left-aligned and followed by at least one ASCII space (20h) character.
    /// This may be followed by a vendor-specific ASCII string further identifying the application, also left-aligned and padded with ASCII space characters.
    /// If no further identification is desired then ASCII space characters shall be added to pad to the width of the field.
    /// </summary>
    public MAMAttribute ApplicationName { get; } = new(0x0801, 32, AttributeFormat.Ascii);

    /// <summary>
    /// Application Version
    /// This attribute shall be set to indicate the application version used to format the volume and shall be consistent with the Version identifier (if any) used in the Creator format in LTFS label and index constructs (see Section 7.2 Creator format).
    /// The attribute shall be left-aligned and padded with ASCII space (20h) characters.
    /// The LTFS format specification does not define any particular style or content for the value of this attribute.
    /// </summary>
    public MAMAttribute ApplicationVersion { get; } = new(0x0802, 8, AttributeFormat.Ascii);

    /// <summary>
    /// User Medium Text Label
    /// </summary>
    public MAMAttribute UserMediumTextLabel { get; } = new(0x0803, 160, AttributeFormat.Text, false);

    /// <summary>
    /// Text Localization Identifier
    /// This defines the character set used for the User Medium Text Label attribute (Section 10.4.5 User Medium Text Label), in accordance with the table in the T10/SPC-4 draft standard (SPC-4 r36e Table 448).
    /// If this attribute is not set then the default assumed value shall be ASCII (value 00h).
    /// NOTE: It is strongly recommended that the attribute should be set to indicate UTF-8 encoding (value 81h) for compatibility with the encoding used in the rest of the LTFS format.
    /// </summary>
    public MAMAttribute TextLocalizationIdentifier { get; } = new(0x0805, 1, AttributeFormat.Binary, false);

    /// <summary>
    /// Barcode
    /// It is recommended that this attribute should be set to match the physical cartridge label (if any).
    /// If set, it shall be left-aligned and padded with ASCII space (20h) characters.
    /// NOTE: This attribute is related to the volume identifier in the VOL1 label (see Section 8.1.1) but without the restriction of six characters; the attribute can hold up to 32 characters.
    /// </summary>
    public MAMAttribute Barcode { get; } = new(0x0806, 32, AttributeFormat.Ascii, false);

    /// <summary>
    /// Application Format Version
    /// This attribute shall be set to indicate the version of the LTFS format specification with which this volume was formatted.
    /// It shall be consistent with the version attribute of the ltfslabel element as found in the LTFS label construct (see Section 8.1.2 LTFS Label).
    /// It shall be left-aligned and padded with ASCII space (20h) characters.
    /// NOTE: In the special case where a volume is migrated to a newer version of the format, this attribute should be updated to continue to provide an accurate value.
    /// </summary>
    public MAMAttribute ApplicationFormatVersion { get; } = new(0x080b, 16, AttributeFormat.Ascii);

    /// <summary>
    /// Media Pool
    /// This attribute may be set to a media pool name and/or additional information as specified in Annex F.4.
    /// </summary>
    //public LtfsMAMAttribute MediaPool { get; } = new(0x0808, 160, AttributeFormat.Text, false);

    /// <summary>
    /// Medium Globally Unique Identifier
    /// This attribute may be used to store the volume UUID, generated when a volume is formatted.
    /// It provides access to the UUID of the volume without requiring it to be mounted.
    /// </summary>
    //public LtfsMAMAttribute MediumGloballyUniqueIdentifier { get; } = new(0x0820, 36, AttributeFormat.Binary, false);

    /// <summary>
    /// Media Pool Globally Unique Identifier
    /// This attribute may be set to a media pool UUID as specified in Annex F.4.
    /// </summary>
    //public LtfsMAMAttribute MediaPoolGloballyUniqueIdentifier { get; } = new(0x0821, 36, AttributeFormat.Binary, false);


    /// <summary>
    /// Read all attribute contents through reflection
    /// </summary>
    public void ReadAll(ReadFunction readFunc)
    {
        var props = GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(MAMAttribute));
        foreach (var prop in props)
        {
            var attr = (MAMAttribute?)prop.GetValue(this);
            if (attr != null)
                attr.Content = readFunc(attr.Page, (byte)attr.AttributeFormat);
        }
    }

    /// <summary>
    /// Write all attribute contents through reflection
    /// </summary>
    public void WriteAll(WriteFunction writeFunc)
    {
        var props = GetType().GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(MAMAttribute));
        foreach (var prop in props)
        {
            var attr = (MAMAttribute?)prop.GetValue(this);
            if (attr == null)
                continue;

            if (attr.NeedWrite)
            {
                if (attr.Content is null)
                {
                    if (attr.Must)
                        throw new InvalidOperationException($"Attribute {attr.Page} must be written but has no content.");
                    else
                        continue;
                }
                if (!writeFunc(attr.Page, attr.Content, attr.AttributeFormat, 0))
                    throw new InvalidOperationException($"Failed to write attribute {attr.Page}.");

                attr.NeedWrite = false;
            }
        }
    }

}
