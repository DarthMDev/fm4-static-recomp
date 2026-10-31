// Copyright (c) 2026, i2kiselev
// BSD 3-Clause License: see LICENSE in the repository root.
using System.Buffers.Binary;
using System.Text;

namespace Fm4StaticRecomp.Launcher.Core.Content;

public sealed class SqliteRow(long rowId, long[] typeOffsets, object?[] values)
{
    public long RowId { get; } = rowId;

    // A column past the record's end (added by ALTER TABLE later) reads as null.
    public object? Value(int column) => column < values.Length ? values[column] : null;

    // Where the column's serial type varint sits in the file.
    public long TypeOffset(int column) => typeOffsets[column];
}

// Reads a SQLite database file straight from its bytes (sqlite.org/fileformat.html), as far as the
// unicorn flags need: a table's columns and rows, and where each value's serial type sits in the
// file. The launcher carries no SQLite library.
public sealed class SqliteFile
{
    private const int HeaderSize = 100;
    private readonly byte[] _data;
    private readonly int _pageSize;
    private readonly int _usable;

    public SqliteFile(byte[] data)
    {
        _data = data;
        if (data.Length < HeaderSize || !data.AsSpan(0, 16).SequenceEqual("SQLite format 3\0"u8))
            throw Bad("not a SQLite database");
        var size = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(16));
        _pageSize = size == 1 ? 65536 : size;
        _usable = _pageSize - data[20];
        // 2 is write-ahead logging, whose changes may sit in a -wal file beside this one.
        if (data[18] != 1 || data[19] != 1) throw Bad("in write-ahead log mode");
        if (BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(56)) != 1) throw Bad("not UTF-8");
        if (_pageSize < 512 || data.Length % _pageSize != 0) throw Bad("damaged");
    }

    public (int RootPage, IReadOnlyList<string> Columns) Table(string name)
    {
        foreach (var row in Rows(1))
            if (row.Value(0) is "table" && name.Equals(row.Value(1) as string, StringComparison.OrdinalIgnoreCase) &&
                row.Value(3) is long root && row.Value(4) is string sql)
                return ((int)root, ColumnNames(sql));
        throw Bad($"without the table {name}");
    }

    public IEnumerable<SqliteRow> Rows(int rootPage)
    {
        var pending = new Stack<int>([rootPage]);
        var visited = new HashSet<int>();
        while (pending.Count > 0)
        {
            var page = pending.Pop();
            if (!visited.Add(page) || page < 1 || (long)page * _pageSize > _data.Length) throw Bad("damaged");
            var start = (page - 1) * _pageSize;
            var header = page == 1 ? start + HeaderSize : start;
            var type = _data[header];
            var cells = BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(header + 3));
            var pointers = header + (type == 0x05 ? 12 : 8);
            if (type == 0x05)
            {
                // Children pushed in reverse, so rows come in rowid order.
                pending.Push((int)BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(header + 8)));
                for (var i = cells - 1; i >= 0; i--)
                    pending.Push((int)BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(start + Pointer(pointers, i))));
            }
            else if (type == 0x0D)
            {
                for (var i = 0; i < cells; i++) yield return Row(start + Pointer(pointers, i));
            }
            else throw Bad("damaged");
        }
    }

    private int Pointer(int pointers, int index) => BinaryPrimitives.ReadUInt16BigEndian(_data.AsSpan(pointers + 2 * index));

    private SqliteRow Row(int cell)
    {
        var at = cell;
        var size = Varint(_data, ref at);
        var rowId = Varint(_data, ref at);
        var (payload, offsets) = Payload(at, size);

        var p = 0;
        var headerSize = (int)Varint(payload, ref p);
        List<long> types = [], typeOffsets = [];
        while (p < headerSize)
        {
            typeOffsets.Add(offsets[p]);
            types.Add(Varint(payload, ref p));
        }
        var values = new object?[types.Count];
        var body = headerSize;
        for (var i = 0; i < types.Count; i++)
        {
            var length = Length(types[i]);
            if (body + length > payload.Length) throw Bad("damaged");
            values[i] = Value(types[i], payload.AsSpan(body, length));
            body += length;
        }
        return new SqliteRow(rowId, [.. typeOffsets], values);
    }

    // The payload's bytes and each byte's offset in the file, through its overflow pages.
    private (byte[] Payload, long[] Offsets) Payload(int at, long size)
    {
        if (size > _data.Length) throw Bad("damaged");
        var total = (int)size;
        var maxLocal = _usable - 35;
        var minLocal = (_usable - 12) * 32 / 255 - 23;
        var local = total;
        if (total > maxLocal)
        {
            local = minLocal + (total - minLocal) % (_usable - 4);
            if (local > maxLocal) local = minLocal;
        }
        var payload = new byte[total];
        var offsets = new long[total];
        var done = 0;
        void Take(int from, int count)
        {
            if (from + count > _data.Length) throw Bad("damaged");
            Array.Copy(_data, from, payload, done, count);
            for (var i = 0; i < count; i++) offsets[done + i] = from + i;
            done += count;
        }
        Take(at, local);
        if (done < total)
        {
            var next = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(at + local));
            while (done < total)
            {
                if (next < 1 || (long)next * _pageSize > _data.Length) throw Bad("damaged");
                var page = (int)(next - 1) * _pageSize;
                next = BinaryPrimitives.ReadUInt32BigEndian(_data.AsSpan(page));
                Take(page + 4, Math.Min(_usable - 4, total - done));
            }
        }
        return (payload, offsets);
    }

    private static int Length(long type) => type switch
    {
        0 or 8 or 9 => 0,
        >= 1 and <= 4 => (int)type,
        5 => 6,
        6 or 7 => 8,
        >= 12 => (int)((type - 12) / 2),
        _ => throw Bad("damaged"),
    };

    private static object? Value(long type, ReadOnlySpan<byte> bytes)
    {
        switch (type)
        {
            case 0: return null;
            case 8: return 0L;
            case 9: return 1L;
            case 7: return BinaryPrimitives.ReadDoubleBigEndian(bytes);
            case >= 12 when type % 2 == 0: return bytes.ToArray();
            case >= 13: return Encoding.UTF8.GetString(bytes);
        }
        long value = (sbyte)bytes[0];
        for (var i = 1; i < bytes.Length; i++) value = (value << 8) | bytes[i];
        return value;
    }

    private static long Varint(byte[] bytes, ref int at)
    {
        long value = 0;
        for (var i = 0; i < 9; i++)
        {
            if (at >= bytes.Length) throw Bad("damaged");
            var b = bytes[at++];
            if (i == 8) return (value << 8) | b;
            value = (value << 7) | (long)(b & 0x7F);
            if (b < 0x80) return value;
        }
        return value;
    }

    // CREATE TABLE's column names: definitions split at top-level commas, constraints left out.
    private static List<string> ColumnNames(string sql)
    {
        var open = sql.IndexOf('(');
        var close = sql.LastIndexOf(')');
        if (open < 0 || close < open) throw Bad("with a table it cannot read");
        List<string> parts = [];
        var depth = 0;
        var part = new StringBuilder();
        char? quote = null;
        foreach (var c in sql[(open + 1)..close])
        {
            if (quote is { } q)
            {
                if (c == q) quote = null;
            }
            else if (c is '[') quote = ']';
            else if (c is '"' or '`' or '\'') quote = c;
            else if (c == '(') depth++;
            else if (c == ')') depth--;
            else if (c == ',' && depth == 0)
            {
                parts.Add(part.ToString());
                part.Clear();
                continue;
            }
            part.Append(c);
        }
        parts.Add(part.ToString());
        string[] constraints = ["CONSTRAINT", "PRIMARY", "UNIQUE", "CHECK", "FOREIGN"];
        return parts.Select(p => p.Trim())
            .Where(p => !constraints.Any(k => p.StartsWith(k + " ", StringComparison.OrdinalIgnoreCase) ||
                                               p.StartsWith(k + "(", StringComparison.OrdinalIgnoreCase)))
            .Select(ColumnName).ToList();
    }

    private static string ColumnName(string definition) => definition[0] switch
    {
        '[' => definition[1..definition.IndexOf(']')],
        '"' or '`' => definition[1..definition.IndexOf(definition[0], 1)],
        _ => definition.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries)[0],
    };

    private static InvalidDataException Bad(string what) => new($"The game's database is {what}.");
}
