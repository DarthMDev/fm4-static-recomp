// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using System.Buffers.Binary;
using System.Text;

namespace Fm4StaticRecomp.Launcher.Core.Content;

// The `<file name>.header` the SDK keeps beside each content folder (ContentManager::
// WriteContentHeaderFile): XCONTENT_AGGREGATE_DATA, big-endian, then the licence mask as a
// little-endian u32 when the content has one. The game cannot open content without it.
public static class ContentHeader
{
    public const uint SaveGame = 1;
    public const uint Marketplace = 2;
    private const int Size = 0x148;
    private const int DisplayNameOffset = 0x08;
    private const int DisplayNameChars = 128;
    private const int FileNameOffset = 0x108;
    private const int FileNameChars = 42;
    private const int TitleIdOffset = 0x140;
    private const uint DeviceId = 1;
    private const uint TitleId = 0x4D530910;

    public static byte[] Build(uint contentType, string displayName, string fileName, uint? licenseMask)
    {
        var header = new byte[Size + (licenseMask is null ? 0 : 4)];
        BinaryPrimitives.WriteUInt32BigEndian(header, DeviceId);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4), contentType);
        Encoding.BigEndianUnicode.GetBytes(displayName.AsSpan(0, Math.Min(displayName.Length, DisplayNameChars)),
            header.AsSpan(DisplayNameOffset));
        Encoding.ASCII.GetBytes(fileName.AsSpan(0, Math.Min(fileName.Length, FileNameChars)), header.AsSpan(FileNameOffset));
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(TitleIdOffset), TitleId);
        if (licenseMask is { } mask) BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(Size), mask);
        return header;
    }
}
