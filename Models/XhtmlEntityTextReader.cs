using System;
using System.IO;

namespace OpenccNetLibGui.Models;

internal sealed class XhtmlEntityTextReader : TextReader
{
    internal const int BufferSize = 4096;
    private const int MaxEntityLength = 8;
    private static readonly (string Entity, string Replacement)[] Entities =
    {
        ("&nbsp;", "&#160;"),
        ("&ensp;", "&#8194;"),
        ("&emsp;", "&#8195;"),
        ("&thinsp;", "&#8201;"),
        ("&ndash;", "&#8211;"),
        ("&mdash;", "&#8212;"),
        ("&lsquo;", "&#8216;"),
        ("&rsquo;", "&#8217;"),
        ("&ldquo;", "&#8220;"),
        ("&rdquo;", "&#8221;"),
        ("&hellip;", "&#8230;")
    };

    private readonly TextReader _source;
    private readonly char[] _input;
    private int _start;
    private int _end;
    private bool _eof;
    private bool _disposed;
    private ReadOnlyMemory<char> _replacement;

    public XhtmlEntityTextReader(TextReader source, char[] input)
    {
        _source = source;
        _input = input;
    }

    public override int Peek()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        Prepare();
        if (!_replacement.IsEmpty) return _replacement.Span[0];
        return _start == _end ? -1 : _input[_start];
    }

    public override int Read()
    {
        var value = Peek();
        if (!_replacement.IsEmpty)
            _replacement = _replacement[1..];
        else if (value != -1)
            _start++;
        return value;
    }

    public override int Read(char[] buffer, int index, int count)
    {
        ArgumentNullException.ThrowIfNull(buffer);
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (buffer.Length - index < count)
            throw new ArgumentException("The destination range exceeds the buffer.");
        return Read(buffer.AsSpan(index, count));
    }

    public override int Read(Span<char> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var written = 0;
        while (!destination.IsEmpty)
        {
            Prepare();
            var replacing = !_replacement.IsEmpty;
            var input = replacing ? _replacement.Span : _input.AsSpan(_start, _end - _start);
            if (input.IsEmpty) break;

            var count = Math.Min(input.Length, destination.Length);
            if (!replacing)
            {
                // A leading ampersand left by Prepare is an unmatched reference.
                var nextEntity = input[1..count].IndexOf('&');
                if (nextEntity >= 0) count = nextEntity + 1;
                _start += count;
            }
            else
            {
                _replacement = _replacement[count..];
            }
            input[..count].CopyTo(destination);
            destination = destination[count..];
            written += count;
        }
        return written;
    }

    private void Prepare()
    {
        if (!_replacement.IsEmpty) return;
        if (_start == _end) Fill(1);
        if (_start == _end || _input[_start] != '&') return;

        Fill(MaxEntityLength);
        var candidate = _input.AsSpan(_start, Math.Min(_end - _start, MaxEntityLength));
        var semicolon = candidate.IndexOf(';');
        if (semicolon < 0) return;

        // Match lexically throughout the document, including XML markup.
        candidate = candidate[..(semicolon + 1)];
        foreach (var (entity, replacement) in Entities)
        {
            if (!candidate.SequenceEqual(entity.AsSpan())) continue;
            _start += candidate.Length;
            _replacement = replacement.AsMemory();
            return;
        }
    }

    private void Fill(int minimum)
    {
        if (_eof || _end - _start >= minimum) return;
        var remaining = _end - _start;
        _input.AsSpan(_start, remaining).CopyTo(_input);
        _start = 0;
        _end = remaining;
        while (_end < minimum)
        {
            var count = _source.Read(_input, _end, _input.Length - _end);
            if (count == 0)
            {
                _eof = true;
                break;
            }
            _end += count;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_disposed)
        {
            _disposed = true;
            _source.Dispose();
        }
        base.Dispose(disposing);
    }
}
