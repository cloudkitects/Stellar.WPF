using System;
using System.IO;
using System.Text;

namespace Stellar.WPF.Utilities;

/// <summary>
/// File stream wrapper with encoding auto-detection.
/// </summary>
public static class FileReader
{
    /// <summary>
    /// shortcut to UTF-8 with no byte order mark (BOM). 
    /// </summary>
    static readonly Encoding UTF8NoBOM = new UTF8Encoding(false);

    /// <summary>
    /// Whether the given encoding is Unicode (UTF).
    /// </summary>
    /// <remarks>
    /// Returns true for UTF-7, UTF-8, UTF-16 LE, UTF-16 BE,
    /// UTF-32 LE and UTF-32 BE, and false for all other encodings.
    /// </remarks>
    public static bool IsUnicode(Encoding encoding) => (encoding ?? throw new ArgumentNullException(nameof(encoding))).CodePage switch
    {
        65000 or 65001 or 1200 or 1201 or 12000 or 12001 => true,
        _ => false,
    };

    /// <summary>
    /// Whether the given encoding is ASCII compatible.
    /// </summary>
    static bool IsASCIICompatible(Encoding encoding)
    {
        var bytes = encoding.GetBytes("Az");
        
        return bytes.Length == 2 && bytes[0] == 'A' && bytes[1] == 'z';
    }

    /// <summary>
    /// Switch UTF-7 to UTF-8 No BOM.
    /// </summary>

    static Encoding RemoveBOM(Encoding encoding) => encoding.CodePage switch
    {
        65001 => UTF8NoBOM,
        _ => encoding,
    };

    /// <summary>
    /// Reads the content of the given stream.
    /// </summary>
    /// <param name="stream">The stream to read.
    /// The stream must support seeking and must be positioned at its beginning.</param>
    /// <param name="defaultEncoding">The encoding to use if the encoding cannot be auto-detected.</param>
    /// <returns>The file content as string.</returns>
    public static string ReadFileContent(Stream stream, Encoding defaultEncoding)
    {
        using var reader = OpenStream(stream, defaultEncoding);
        
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Reads the content of the file.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="defaultEncoding">The encoding to use if the encoding cannot be auto-detected.</param>
    /// <returns>The file content as string.</returns>
    public static string ReadFileContent(string fileName, Encoding defaultEncoding)
    {
        using var fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        return ReadFileContent(fs, defaultEncoding);
    }

    /// <summary>
    /// Opens the specified file for reading.
    /// </summary>
    /// <param name="fileName">The file to open.</param>
    /// <param name="defaultEncoding">The encoding to use if the encoding cannot be auto-detected.</param>
    /// <returns>Returns a StreamReader that reads from the stream. Use
    /// <see cref="StreamReader.CurrentEncoding"/> to get the encoding that was used.</returns>
    public static StreamReader OpenFile(string fileName, Encoding defaultEncoding)
    {
        if (fileName is null)
        {
            throw new ArgumentNullException(nameof(fileName));
        }

        var stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
        
        try
        {
            return OpenStream(stream, defaultEncoding);
            
            // don't use finally: the stream must be kept open until the StreamReader closes it
        }
        catch
        {
            stream.Dispose();
            
            throw;
        }
    }

    /// <summary>
    /// Opens the specified stream for reading.
    /// </summary>
    /// <param name="stream">The stream to open.</param>
    /// <param name="defaultEncoding">The encoding to use if the encoding cannot be auto-detected.</param>
    /// <returns>Returns a StreamReader that reads from the stream. Use
    /// <see cref="StreamReader.CurrentEncoding"/> to get the encoding that was used.</returns>
    public static StreamReader OpenStream(Stream stream, Encoding defaultEncoding)
    {
        if (stream is null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        if (stream.Position != 0)
        {
            throw new ArgumentException("stream is not positioned at beginning.", nameof(stream));
        }

        if (defaultEncoding is null)
        {
            throw new ArgumentNullException(nameof(defaultEncoding));
        }

        if (stream.Length >= 2)
        {
            // the autodetection of StreamReader is not capable of detecting the difference
            // between ISO-8859-1 and UTF-8 without BOM.
            var byte0 = stream.ReadByte();
            var byte1 = stream.ReadByte();
            
            switch ((byte0 << 8) | byte1)
            {
                case 0x0000: // either UTF-32 Big Endian or a binary file; use StreamReader
                case 0xfffe: // Unicode BOM (UTF-16 LE or UTF-32 LE)
                case 0xfeff: // UTF-16 BE BOM
                case 0xefbb: // start of UTF-8 BOM
                             // StreamReader autodetection works
                    stream.Position = 0;
                    return new StreamReader(stream);
                
                    default:
                    return AutoDetect(stream, (byte)byte0, (byte)byte1, defaultEncoding);
            }
        }
        else
        {
            return defaultEncoding is not null
                ? new StreamReader(stream, defaultEncoding)
                : new StreamReader(stream);
        }
    }

    static StreamReader AutoDetect(Stream stream, byte byte0, byte byte1, Encoding defaultEncoding)
    {
         // first 500 KB only
        var max = (int)Math.Min(stream.Length, 500000);
        
        const int ASCII = 0;
        const int Error = 1;
        const int UTF8 = 2;
        const int UTF8Sequence = 3;
        
        var state = ASCII;
        var sequenceLength = 0;
        
        byte b;
        
        for (var i = 0; i < max; i++)
        {
            if (i == 0)
            {
                b = byte0;
            }
            else if (i == 1)
            {
                b = byte1;
            }
            else
            {
                b = (byte)stream.ReadByte();
            }
            if (b < 0x80)
            {
                // normal ASCII character
                if (state == UTF8Sequence)
                {
                    state = Error;
                    break;
                }
            }
            else if (b < 0xc0)
            {
                // 10xxxxxx : continues UTF8 byte sequence
                if (state == UTF8Sequence)
                {
                    --sequenceLength;
                    
                    if (sequenceLength < 0)
                    {
                        state = Error;
                        break;
                    }
                    
                    if (sequenceLength == 0)
                    {
                        state = UTF8;
                    }
                }
                else
                {
                    state = Error;
                    break;
                }
            }
            else if (b >= 0xc2 && b < 0xf5)
            {
                // beginning of byte sequence
                if (state == UTF8 || state == ASCII)
                {
                    state = UTF8Sequence;
                    
                    if (b < 0xe0)
                    {
                        sequenceLength = 1; // one more byte following
                    }
                    else if (b < 0xf0)
                    {
                        sequenceLength = 2; // two more bytes following
                    }
                    else
                    {
                        sequenceLength = 3; // three more bytes following
                    }
                }
                else
                {
                    state = Error;
                    break;
                }
            }
            else
            {
                // 0xc0, 0xc1, 0xf5 to 0xff are invalid in UTF-8 (see RFC 3629)
                state = Error;
                break;
            }
        }

        stream.Position = 0;
        
        switch (state)
        {
            case ASCII:
                return new StreamReader(stream, IsASCIICompatible(defaultEncoding) ? RemoveBOM(defaultEncoding) : Encoding.ASCII);
            
            case Error:
                // When the file seems to be non-UTF8,
                // we read it using the user-specified encoding so it is saved again
                // using that encoding.
                if (IsUnicode(defaultEncoding))
                {
                    // the file is not Unicode, so don't read it using Unicode even if the
                    // user has chosen Unicode as the default encoding.

                    defaultEncoding = Encoding.Default; // use system encoding instead
                }
                return new StreamReader(stream, RemoveBOM(defaultEncoding));
            
            default:
                return new StreamReader(stream, UTF8NoBOM);
        }
    }
}

