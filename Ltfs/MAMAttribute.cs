using System.Text;

using TapeDrive;

namespace Ltfs;

public class MAMAttribute
{
    public ushort Page { get; set; }
    public int Size {  get; set; }
    public byte[]? Content { get; set; } = null;
    public AttributeFormat AttributeFormat { get; set; }
    public bool NeedWrite { get; set; } = false;
    public bool Must { get; init; }

    public MAMAttribute(ushort page, int size, AttributeFormat attributeFormat, bool must = true)
    {
        Page = page;
        Size = size;
        AttributeFormat = attributeFormat;
        Must = must;
    }

    public void SetAsciiString(string asciiString)
    {
        Content = Encoding.ASCII.GetBytes(asciiString.PadRight(Size))[..Size];
        NeedWrite = true;
    }

    public void SetTextString(string textString)
    {
        Content = new byte[Size];
        
        for (int i=0; i < Size; i++)
            Content[i] = 0x20;

        var textBytes = Encoding.UTF8.GetBytes(textString);
        if (textBytes.Length >= Size)
        {
            textBytes = textBytes[..Size];
            textBytes[^1] = 0;
        }
        Array.Copy(textBytes, Content, textBytes.Length);
        NeedWrite = true;
    }

    public void SetBinary(byte[] binary)
    {
        Content = binary[..Size];
        NeedWrite = true;
    }
}
