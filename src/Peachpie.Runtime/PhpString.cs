﻿using Pchp.Core.Text;
using Pchp.Core.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Pchp.Core.Text
{
    #region BlobChar

    /// <summary>
    /// A character that can be either a single <see cref="byte"/> or Unicode <see cref="char"/>.
    /// Used internally.
    /// </summary>
    [DebuggerNonUserCode]
    internal struct BlobChar
    {
        private readonly short _b;
        private readonly char _ch;

        public bool IsByte => _b >= 0;

        /// <summary>
        /// Character stored as UTF16.
        /// </summary>
        public bool IsUnicode => _ch != 0;

        /// <summary>
        /// Character needs to be stored as a single byte,
        /// conversion to char would change its semantic.
        /// </summary>
        public bool IsBinary => _b >= 0xf0;

        public BlobChar(byte b) : this() { _b = b; }
        public BlobChar(char c) : this() { _ch = c; _b = -1; }
        public BlobChar(long l)
        {
            if (l <= 0xff)
            {
                _b = (short)l;
                _ch = (char)0;
            }
            else
            {
                _ch = (char)l;
                _b = -1;
            }
        }

        /// <summary>
        /// Converts the character to a value.
        /// </summary>
        public PhpValue AsValue() => IsBinary
            ? PhpValue.Create(new PhpString.Blob(new[] { (byte)_b }))   // [0x10, 0xff]
            : PhpValue.Create(AsChar().ToString()); // Char is preferred if can be used

        public char AsChar() => IsByte ? (char)_b : _ch;

        public byte AsByte() => IsByte ? (byte)_b : (byte)_ch;

        public void Output(Context ctx)
        {
            if (IsByte)
                ctx.OutputStream.WriteByte((byte)_b);
            else
                ctx.Output.Write(_ch);
        }

        public override string ToString() => AsChar().ToString();

        /// <summary>
        /// Converts the value to a character.
        /// </summary>
        public static BlobChar FromValue(PhpValue value)
        {
            switch (value.TypeCode)
            {
                case PhpTypeCode.Long:
                    return new BlobChar(value.Long);

                case PhpTypeCode.String:
                    return new BlobChar(Core.Convert.ToChar(value.String));

                case PhpTypeCode.MutableString:
                    return value.MutableStringBlob[0];

                case PhpTypeCode.Alias:
                    return FromValue(value.Alias.Value);

                // TODO: other types

                default:
                    throw new NotSupportedException(value.TypeCode.ToString());
            }
        }

        /// <summary>
        /// Copies characters to a new array of <see cref="char"/>s.
        /// Single-byte chars are encoded to Unicode chars.
        /// </summary>
        public static char[] ToCharArray(BlobChar[] chars, Encoding enc)
        {
            // TODO: more decent code

            var result = new List<char>(chars.Length);

            Debug.Assert(chars != null);

            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i].IsByte)
                {
                    int j = i;
                    while (j < chars.Length && chars[j].IsByte)
                    {
                        j++;
                    }

                    // encode bytes (i..j] to char array
                    var maxchars = enc.GetMaxCharCount(j - i);
                    var tmp = new char[maxchars];
                    var src = new byte[j - i];

                    for (int b = 0; b < src.Length; b++, i++)
                    {
                        src[b] = (byte)chars[i]._b;
                    }

                    var charscount = enc.GetChars(src, 0, src.Length, tmp, 0);
                    result.AddRange(new ArraySegment<char>(tmp, 0, charscount));
                }
                else
                {
                    result.Add(chars[i]._ch);
                }
            }

            return result.ToArray(); // TODO: span of underlaying items
        }

        /// <summary>
        /// Decode characters into a new array of <see cref="byte"/>s.
        /// Unicode chars are decoded to single-byte chars.
        /// </summary>
        public static byte[] ToByteArray(BlobChar[] chars, Encoding enc)
        {
            // TODO: more decent code

            var result = new List<byte>(chars.Length);

            Debug.Assert(chars != null);

            for (int i = 0; i < chars.Length; i++)
            {
                if (chars[i].IsByte)
                {
                    result.Add((byte)chars[i]._b);
                }
                else
                {
                    int j = i;
                    while (j < chars.Length && !chars[j].IsByte)
                    {
                        j++;
                    }

                    // encode bytes (i..j] to char array
                    var maxbytes = enc.GetMaxByteCount(j - i);
                    var tmp = new byte[maxbytes];
                    var src = new char[j - i];

                    for (int b = 0; b < src.Length; b++, i++)
                    {
                        src[b] = chars[i]._ch;
                    }

                    var bytescount = enc.GetBytes(src, 0, src.Length, tmp, 0);
                    result.AddRange(new ArraySegment<byte>(tmp, 0, bytescount));
                }
            }

            return result.ToArray(); // TODO: span of underlaying items
        }

        internal static BlobChar[] ToBlobCharArray(string str)
        {
            var arr = new BlobChar[str.Length];
            for (int i = 0; i < str.Length; i++)
            {
                arr[i] = str[i];
            }
            return arr;
        }

        internal static BlobChar[] ToBlobCharArray(char[] chars)
        {
            var arr = new BlobChar[chars.Length];
            for (int i = 0; i < chars.Length; i++)
            {
                arr[i] = chars[i];
            }
            return arr;
        }

        internal static BlobChar[] ToBlobCharArray(byte[] bytes)
        {
            var arr = new BlobChar[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                arr[i] = bytes[i];
            }
            return arr;
        }

        public static implicit operator PhpValue(BlobChar b) => b.AsValue();
        public static implicit operator BlobChar(PhpValue value) => FromValue(value);
        public static implicit operator BlobChar(char c) => new BlobChar(c);
        public static implicit operator BlobChar(byte b) => new BlobChar(b);
    }

    #endregion

    #region IMutableString

    /// <summary>
    /// Provides access to <see cref="PhpString"/> with write access.
    /// </summary>
    internal interface IMutableString : IPhpArray
    {
        void Add(string value);
        void Add(byte[] value);
        void Add(PhpString value);
        void Add(PhpValue value, Context ctx);

        BlobChar this[int index] { get; set; }

        int Length { get; }
    }

    #endregion
}

namespace Pchp.Core
{
    /// <summary>
    /// String builder providing fast concatenation and character replacements for hybrid strings (both unicode and binary).
    /// </summary>
    /// <remarks>Optimized for concatenation and output.</remarks>
    [DebuggerDisplay("{ToString()}", Type = PhpVariable.TypeNameString)]
    [DebuggerNonUserCode]
    public struct PhpString : IPhpConvertible
    {
        //[StructLayout(LayoutKind.Explicit)]
        //struct Chunk
        //{
        //    [FieldOffset(0)]
        //    object _obj;

        //    [FieldOffset(0)]
        //    public string _string;

        //    [FieldOffset(0)]
        //    public byte[] _bytes;

        //    [FieldOffset(0)]
        //    public char[] _chars;

        //    [FieldOffset(0)]
        //    public PhpString _phpstring;

        //    public bool IsString => _obj != null && _obj.GetType() == typeof(string);

        //    public bool IsByteArray => _obj != null && _obj.GetType() == typeof(byte[]);

        //    public bool IsCharArray => _obj != null && _obj.GetType() == typeof(char[]);

        //    public bool IsPhpString => _obj != null && _obj.GetType() == typeof(PhpString);
        //}

        [DebuggerDisplay("{DebugString}")]
        public sealed class Blob : IMutableString
        {
            #region enum Flags

            [Flags]
            enum Flags : byte
            {
                None = 0,

                /// <summary>
                /// The blob contains <see cref="byte"/>[].
                /// </summary>
                ContainsBinary = 1,

                IsNonEmpty = 2,

                IsArrayOfChunks = 4,

                /// <summary>
                /// Whether the blob contains mutable instances that have to be cloned when copying.
                /// </summary>
                ContainsMutables = 8,
            }

            #endregion

            #region Fields

            /// <summary>
            /// One string or concatenated string chunks of either <see cref="string"/>, <see cref="byte"/>[], <see cref="char"/>[] or <see cref="Blob"/>.
            /// </summary>
            object _chunks;

            /// <summary>
            /// Count of objects in <see cref="_chunks"/>.
            /// </summary>
            int _chunksCount;

            /// <summary>
            /// String information.
            /// </summary>
            Flags _flags;

            /// <summary>
            /// Cached length of the concatenated string.
            /// </summary>
            int _length = -1;

            /// <summary>
            /// Cached concatenated string.
            /// </summary>
            string _string;

            /// <summary>
            /// References count. More than one reference means the data are shared accross more instances (read-only).
            /// </summary>
            int _refs = 1;

            #endregion

            #region DebugString

            string DebugString
            {
                get
                {
                    if (ContainsBinaryData)
                    {
                        var bytes = ToBytes(Encoding.UTF8);
                        var str = new StringBuilder(bytes.Length);

                        foreach (var b in bytes)
                        {
                            if (b < 0x7f && b >= 0x20)
                            {
                                str.Append((char)b);
                            }
                            else
                            {
                                str.Append("\\x");
                                str.Append(b.ToString("x2"));
                            }
                        }

                        return str.ToString();
                    }
                    else
                    {
                        return ToString(Encoding.UTF8);
                    }
                }
            }

            #endregion

            /// <summary>
            /// Gets value indicating the string contains <see cref="byte"/> instead of unicode <see cref="string"/>.
            /// </summary>
            public bool ContainsBinaryData => (_flags & Flags.ContainsBinary) != 0;

            /// <summary>
            /// Gets value indicating the string is empty.
            /// </summary>
            public bool IsEmpty => (_flags & Flags.IsNonEmpty) == 0;

            /// <summary>
            /// The string is represented internally as array of chunks.
            /// </summary>
            private bool IsArrayOfChunks => (_flags & Flags.IsArrayOfChunks) != 0;

            /// <summary>
            /// Gets value indicating that this instance of data is shared and cannot be written to.
            /// </summary>
            public bool IsShared => _refs > 1;

            #region Construction

            public Blob()
            {
                // pre-cache
                _length = 0;
                _string = string.Empty;
            }

            public Blob(string x, string y)
            {
                if (string.IsNullOrEmpty(y))
                {
                    Add(x);
                }
                else if (string.IsNullOrEmpty(x))
                {
                    Add(y);
                }
                else
                {
                    _chunks = new object[2] { x, y };
                    _chunksCount = 2;
                    _flags = Flags.IsArrayOfChunks | Flags.IsNonEmpty;
                }
            }

            public Blob(string value)
            {
                Add(value);
            }

            public Blob(byte[] value)
            {
                Add(value);
            }

            private Blob(Blob blob)
            {
                _chunks = blob._chunks;
                _chunksCount = blob._chunksCount;
                _flags = blob._flags;

                // pre-cache
                _length = blob._length;
                _string = blob._string;
            }

            /// <summary>
            /// Makes this instance of string data shared and returns self.
            /// </summary>
            /// <returns>This instance.</returns>
            public Blob AddRef()
            {
                _refs++;
                return this;
            }

            public Blob ReleaseOne()
            {
                _refs--;

                var clone = new Blob(this);
                clone.InplaceDeepCopy();
                return clone;
            }

            void InplaceDeepCopy()
            {
                var chunks = _chunks;
                if (chunks != null)
                {
                    if (chunks.GetType() == typeof(object[]))
                    {
                        Debug.Assert(IsArrayOfChunks);
                        var arr = (object[])chunks;
                        var newarr = new object[arr.Length];
                        Array.Copy(arr, newarr, _chunksCount);

                        if ((_flags & Flags.ContainsMutables) != 0)
                        {
                            for (int i = 0; i < _chunksCount; i++)
                            {
                                InplaceDeepCopy(ref newarr[i]);
                            }
                        }
                    }
                    else
                    {
                        InplaceDeepCopy(ref _chunks);
                    }
                }
            }

            static void InplaceDeepCopy(ref object chunk)
            {
                AssertChunkObject(chunk);
                if (chunk.GetType() == typeof(string)) { }  // immutable
                else if (chunk.GetType() == typeof(Blob)) chunk = ((Blob)chunk).AddRef();
                else chunk = ((Array)chunk).Clone();   // byte[], char[]
            }

            #endregion

            #region Length

            /// <summary>
            /// Gets the count of characters and binary characters.
            /// </summary>
            public int Length
            {
                get
                {
                    if (_length < 0)
                        _length = GetLength();

                    return _length;
                }
            }

            int GetLength()
            {
                var chunks = _chunks;
                if (chunks != null)
                {
                    if (chunks.GetType() == typeof(object[]))
                    {
                        Debug.Assert(IsArrayOfChunks);

                        // TODO: cache length for current chunks version
                        return ChunkLength((object[])chunks, _chunksCount);
                    }
                    else
                    {
                        return ChunkLength(chunks);
                    }
                }

                return 0;
            }

            static int ChunkLength(object chunk)
            {
                AssertChunkObject(chunk);

                switch (chunk)
                {
                    case string str: return str.Length;
                    case Blob b: return b.Length;
                    default: return ((Array)chunk).Length;
                }
            }

            static int ChunkLength(object[] chunks, int count)
            {
                int length = 0;

                for (int i = 0; i < count; i++)
                {
                    length += ChunkLength(chunks[i]);
                }

                return length;
            }

            #endregion

            #region Add, Append

            public void Append(string value) => Add(value);
            public void Append(PhpString value) => Add(value);

            public void Add(string value)
            {
                if (!string.IsNullOrEmpty(value))
                {
                    AddChunk(value);
                }
            }

            public void Add(Blob value)
            {
                Debug.Assert(value != null);
                Debug.Assert(!value.IsEmpty);
                Debug.Assert(value._chunks != null);

                if (value.IsArrayOfChunks)
                {
                    AddChunk(value.AddRef());
                }
                else
                {
                    // if containing only one chunk, add it directly
                    var chunk = value._chunks;
                    InplaceDeepCopy(ref chunk);
                    AddChunk(chunk);
                }

                _flags |= (value._flags & (Flags.ContainsBinary | Flags.ContainsMutables));    // maintain the binary data flag
            }

            public void Add(byte[] value)
            {
                if (value != null && value.Length != 0)
                {
                    AddChunk(value);
                    _flags |= Flags.ContainsBinary | Flags.ContainsMutables;
                }
            }

            public void Add(PhpString value)
            {
                if (!value.IsEmpty)
                {
                    Add(value._blob);
                }
            }

            public void Add(PhpValue value, Context ctx)
            {
                switch (value.TypeCode)
                {
                    case PhpTypeCode.String:
                        Add(value.String);
                        break;

                    case PhpTypeCode.MutableString:
                        Add(value.MutableStringBlob);
                        break;

                    case PhpTypeCode.Alias:
                        Add(value.Alias.Value, ctx);
                        break;

                    default:
                        Add(value.ToStringOrThrow(ctx));
                        break;
                }
            }

            #endregion

            #region AddChunk

            [Conditional("DEBUG")]
            static void AssertChunkObject(object chunk)
            {
                Debug.Assert(chunk is byte[] || chunk is string || chunk is char[] || chunk is Blob || chunk is BlobChar[]);
            }

            void AddChunk(object newchunk)
            {
                AssertChunkObject(newchunk);

                var chunks = _chunks;
                if (chunks != null)
                {
                    Debug.Assert(!this.IsEmpty);

                    // TODO: Compact byte[] chunks together

                    if (IsArrayOfChunks)
                    {
                        Debug.Assert(chunks.GetType() == typeof(object[]));
                        AddChunkToArray((object[])chunks, newchunk);
                    }
                    else
                    {
                        AssertChunkObject(chunks);
                        _chunks = new object[2] { chunks, newchunk };
                        _chunksCount = 2;
                        _flags |= Flags.IsArrayOfChunks;
                    }
                }
                else
                {
                    _chunks = newchunk;
                    _flags |= Flags.IsNonEmpty;
                }

                //
                _string = null;
                _length = -1;
            }

            void AddChunkToArray(object[] chunks, object newchunk)
            {
                Debug.Assert(chunks != null);
                Debug.Assert(_chunksCount > 0);

                if (_chunksCount >= chunks.Length)
                {
                    Debug.Assert(chunks.Length != 0);
                    var newarr = new object[chunks.Length * 2];
                    Array.Copy(chunks, newarr, chunks.Length);
                    _chunks = chunks = newarr;

                    // TODO: when chunks.Length ~ N => compact
                }

                //
                chunks[_chunksCount++] = newchunk;
            }

            #endregion

            #region Output

            public void Output(Context ctx)
            {
                var chunks = _chunks;
                if (chunks != null)
                {
                    if (chunks.GetType() == typeof(object[]))
                    {
                        OutputChunks(ctx, (object[])chunks, _chunksCount);
                    }
                    else
                    {
                        OutputChunk(ctx, chunks);
                    }
                }
            }

            static void OutputChunk(Context ctx, object chunk)
            {
                AssertChunkObject(chunk);

                switch (chunk)
                {
                    case string str: ctx.Output.Write(str); break;
                    case byte[] barr: ctx.OutputStream.Write(barr); break;
                    case Blob b: b.Output(ctx); break;
                    case char[] carr: ctx.Output.Write(carr); break;
                    case BlobChar[] barr: OutputChunk(ctx, barr); break;
                    default: throw new ArgumentException(chunk.GetType().ToString());
                }
            }

            static void OutputChunk(Context ctx, BlobChar[] chars)
            {
                for (int i = 0; i < chars.Length; i++)
                {
                    chars[i].Output(ctx);
                }
            }

            static void OutputChunks(Context ctx, object[] chunks, int count)
            {
                for (int i = 0; i < count; i++)
                {
                    OutputChunk(ctx, chunks[i]);
                }
            }

            #endregion

            #region ToString

            public string ToString(Encoding encoding)
            {
                // TODO: cache the result for current chunks version

                var chunks = _chunks;
                if (chunks != null)
                {
                    return (chunks.GetType() == typeof(object[]))
                        ? ChunkToString(encoding, (object[])chunks, _chunksCount)
                        : ChunkToString(encoding, chunks);
                }
                else
                {
                    return string.Empty;
                }
            }

            static string ChunkToString(Encoding encoding, object[] chunks, int count)
            {
                if (count == 1)
                {
                    return ChunkToString(encoding, chunks[0]);
                }
                else
                {
                    var builder = new StringBuilder(32);    // TODO: pooled instance

                    for (int i = 0; i < count; i++)
                    {
                        builder.Append(ChunkToString(encoding, chunks[i]));
                    }

                    return builder.ToString();
                }
            }

            static string ChunkToString(Encoding encoding, object chunk)
            {
                AssertChunkObject(chunk);

                switch (chunk)
                {
                    case string str: return str;
                    case byte[] barr: return encoding.GetString(barr);
                    case Blob b: return b.ToString(encoding);
                    case char[] carr: return new string(carr);
                    case BlobChar[] barr: return new string(BlobChar.ToCharArray(barr, encoding));
                    default: throw new ArgumentException(chunk.GetType().ToString());
                }
            }

            #endregion

            #region ToBytes

            /// <summary>
            /// Gets string encoded into array of bytes according to corrent string encoding.
            /// </summary>
            public byte[] ToBytes(Context ctx) => ToBytes(ctx.StringEncoding);

            public byte[] ToBytes(Encoding encoding)
            {
                var chunks = _chunks;
                if (chunks != null)
                {
                    return (chunks.GetType() == typeof(object[]))
                        ? ChunkToBytes(encoding, (object[])chunks, _chunksCount)
                        : ChunkToBytes(encoding, chunks);
                }
                else
                {
                    return ArrayUtils.EmptyBytes;
                }
            }

            static byte[] ChunkToBytes(Encoding encoding, object[] chunks, int count)
            {
                if (count == 1)
                {
                    return ChunkToBytes(encoding, chunks[0]);
                }
                else
                {

                    var buffer = new List<byte>();
                    for (int i = 0; i < count; i++)
                    {
                        buffer.AddRange(ChunkToBytes(encoding, chunks[i]));
                    }

                    return buffer.ToArray();
                }
            }

            static byte[] ChunkToBytes(Encoding encoding, object chunk)
            {
                AssertChunkObject(chunk);

                switch (chunk)
                {
                    case string str: return encoding.GetBytes(str);
                    case byte[] barr: return barr;
                    case Blob b: return b.ToBytes(encoding);
                    case char[] carr: return encoding.GetBytes(carr);
                    case BlobChar[] barr: return BlobChar.ToByteArray(barr, encoding);
                    default: throw new ArgumentException(chunk.GetType().ToString());
                }
            }

            #endregion

            #region this[int]

            BlobChar IMutableString.this[int index]
            {
                get => this[index];
                set => this[index] = value;
            }

            /// <summary>
            /// Gets or sets character at given index according to PHP semantics.
            /// </summary>
            /// <param name="index">Character index.</param>
            /// <returns>Character at given position.</returns>
            internal BlobChar this[int index]
            {
                get
                {
                    var chunks = _chunks;
                    if (chunks != null)
                    {
                        if (chunks.GetType() == typeof(object[]))
                        {
                            var arr = (object[])chunks;
                            foreach (var ch in arr)
                            {
                                int ch_length = ChunkLength(ch);
                                if (index < ch_length)
                                {
                                    return GetCharInChunk(ch, index);
                                }
                                index -= ch_length;
                            }
                        }
                        else
                        {
                            return GetCharInChunk(chunks, index);
                        }
                    }

                    throw new ArgumentOutOfRangeException();
                }
                set
                {
                    if (index >= this.Length)
                    {
                        if (index > this.Length)
                        {
                            this.AddChunk(new string('\0', index - this.Length));
                        }

                        object chunk;   // byte[] | string
                        if (value.IsBinary)
                        {
                            _flags |= Flags.ContainsBinary;
                            chunk = new[] { value.AsByte() };
                        }
                        else
                        {
                            chunk = value.ToString();
                        }

                        this.AddChunk(chunk);
                    }
                    else if (index >= 0)
                    {
                        // TODO: EnsureWritable

                        _flags |= Flags.ContainsMutables;   // any immutable value will be converted to a mutable

                        if (value.IsByte)
                        {
                            _flags |= Flags.ContainsBinary;
                        }

                        var chunks = _chunks;
                        if (chunks != null)
                        {
                            if (chunks.GetType() == typeof(object[]))
                            {
                                var arr = (object[])chunks;
                                for (int i = 0; i < arr.Length; i++)
                                {
                                    int ch_length = ChunkLength(arr[i]);
                                    if (index < ch_length)
                                    {
                                        SetCharInChunk(ref arr[i], index, value);
                                        return;
                                    }
                                    index -= ch_length;
                                }
                            }
                            else
                            {
                                SetCharInChunk(ref _chunks, index, value);
                            }
                        }
                    }
                    else
                    {
                        // index < 0, ignored
                    }
                }
            }

            static BlobChar GetCharInChunk(object chunk, int index)
            {
                AssertChunkObject(chunk);

                if (chunk.GetType() == typeof(string)) return ((string)chunk)[index];
                if (chunk.GetType() == typeof(byte[])) return ((byte[])chunk)[index];
                if (chunk.GetType() == typeof(char[])) return ((char[])chunk)[index];
                if (chunk.GetType() == typeof(Blob)) return ((Blob)chunk)[index];
                if (chunk.GetType() == typeof(BlobChar[])) return ((BlobChar[])chunk)[index];

                throw new ArgumentException(chunk.GetType().ToString());
            }

            static void SetCharInChunk(ref object chunk, int index, BlobChar ch)
            {
                AssertChunkObject(chunk);

                if (chunk is string str)
                {
                    if (ch.IsByte)
                    {
                        var chars = BlobChar.ToBlobCharArray(str);
                        chars[index] = ch;
                        chunk = chars;
                    }
                    else
                    {
                        var chars = str.ToCharArray();
                        chars[index] = ch.AsChar();
                        chunk = chars;
                    }
                }
                else if (chunk.GetType() == typeof(byte[]))
                {
                    if (ch.IsByte)
                    {
                        ((byte[])chunk)[index] = ch.AsByte();
                    }
                    else
                    {
                        var chars = BlobChar.ToBlobCharArray((byte[])chunk);
                        chars[index] = ch;
                        chunk = chars;
                    }
                }
                else if (chunk.GetType() == typeof(char[]))
                {
                    if (ch.IsByte)
                    {
                        var chars = BlobChar.ToBlobCharArray((char[])chunk);
                        chars[index] = ch;
                        chunk = chars;
                    }
                    else
                    {
                        ((char[])chunk)[index] = ch.AsChar();
                    }
                }
                else if (chunk.GetType() == typeof(Blob))
                {
                    ((Blob)chunk)[index] = ch;
                }
                else if (chunk.GetType() == typeof(BlobChar[]))
                {
                    ((BlobChar[])chunk)[index] = ch;
                }
                else
                {
                    throw new ArgumentException(chunk.GetType().ToString());
                }
            }

            #endregion

            #region IPhpConvertible

            public bool ToBoolean()
            {
                var chunks = _chunks;
                if (chunks != null)
                {
                    if (chunks.GetType() == typeof(object[]))
                    {
                        return _chunksCount >= 1 && ChunkToBoolean(((object[])chunks)[0]);
                    }
                    else
                    {
                        return ChunkToBoolean(chunks);
                    }
                }

                return false;
            }

            static bool ChunkToBoolean(object chunk)
            {
                AssertChunkObject(chunk);

                if (chunk.GetType() == typeof(string)) return Convert.ToBoolean((string)chunk);
                if (chunk.GetType() == typeof(byte[])) return Convert.ToBoolean((byte[])chunk);
                if (chunk.GetType() == typeof(Blob)) return ((Blob)chunk).ToBoolean();
                if (chunk.GetType() == typeof(char[])) return Convert.ToBoolean((char[])chunk);
                if (chunk.GetType() == typeof(BlobChar[])) return Convert.ToBoolean((BlobChar[])chunk);
                throw new ArgumentException();
            }

            #endregion

            #region IPhpArray

            /// <summary>
            /// Gets number of items in the collection.
            /// </summary>
            int IPhpArray.Count => this.Length;

            /// <summary>
            /// Gets value at given index.
            /// Gets <c>void</c> value in case the key is not found.
            /// </summary>
            PhpValue IPhpArray.GetItemValue(IntStringKey key)
            {
                int index = key.IsInteger ? key.Integer : (int)Convert.StringToLongInteger(key.String);
                return (index >= 0 && index < this.Length) ? this[index].AsValue() : PhpValue.Create(string.Empty);
            }

            PhpValue IPhpArray.GetItemValue(PhpValue index)
            {
                if (index.TryToIntStringKey(out IntStringKey key))
                {
                    return ((IPhpArray)this).GetItemValue(key);
                }

                return new PhpValue(string.Empty);
            }

            void IPhpArray.SetItemValue(PhpValue index, PhpValue value)
            {
                if (index.TryToIntStringKey(out IntStringKey key))
                {
                    ((IPhpArray)this).SetItemValue(key, value);
                }
                else
                {
                    throw new ArgumentException();
                }
            }

            /// <summary>
            /// Sets value at specific index. Value must not be an alias.
            /// </summary>
            void IPhpArray.SetItemValue(IntStringKey key, PhpValue value)
            {
                int index = key.IsInteger ? key.Integer : (int)Convert.StringToLongInteger(key.String);
                this[index] = value;
            }

            /// <summary>
            /// Writes aliased value at given index.
            /// </summary>
            void IPhpArray.SetItemAlias(IntStringKey key, PhpAlias alias) { throw new NotSupportedException(); }

            void IPhpArray.SetItemAlias(PhpValue index, PhpAlias alias) { throw new NotSupportedException(); }

            /// <summary>
            /// Add a value to the end of array.
            /// Value can be an alias.
            /// </summary>
            void IPhpArray.AddValue(PhpValue value) { throw new NotSupportedException(); }

            /// <summary>
            /// Removes a value matching given key.
            /// In case the value is not found, the method does nothing.
            /// </summary>
            void IPhpArray.RemoveKey(IntStringKey key) { throw new NotSupportedException(); }

            void IPhpArray.RemoveKey(PhpValue index)
            {
                throw new NotImplementedException();
            }

            /// <summary>
            /// Ensures the item at given index is alias.
            /// </summary>
            PhpAlias IPhpArray.EnsureItemAlias(IntStringKey key) { throw new NotSupportedException(); }

            /// <summary>
            /// Ensures the item at given index is class object.
            /// </summary>
            object IPhpArray.EnsureItemObject(IntStringKey key) { throw new NotSupportedException(); }

            /// <summary>
            /// Ensures the item at given index is array.
            /// </summary>
            IPhpArray IPhpArray.EnsureItemArray(IntStringKey key) { throw new NotSupportedException(); }

            #endregion
        }

        /// <summary>
        /// Content of the string, may be shared.
        /// Cannot be <c>null</c>.
        /// </summary>
        Blob _blob; // TODO: allow union of "null|string|byte[]|Blob"

        /// <summary>
        /// Gets the count of characters and binary characters.
        /// </summary>
        public int Length => _blob.Length;

        /// <summary>
        /// Gets value indicating the string contains <see cref="byte"/> instead of unicode <see cref="string"/>.
        /// </summary>
        public bool ContainsBinaryData => _blob.ContainsBinaryData;

        /// <summary>
        /// Gets value indicating the string is empty.
        /// </summary>
        public bool IsEmpty => ReferenceEquals(_blob, null) || _blob.IsEmpty;

        /// <summary>
        /// The value is not initialized.
        /// </summary>
        public bool IsDefault => ReferenceEquals(_blob, null);

        /// <summary>
        /// Empty immutable string.
        /// </summary>
        public static PhpString Empty => default(PhpString);

        #region Construction, DeepCopy

        public PhpString(Blob blob)
        {
            _blob = blob;
        }

        public PhpString(PhpString from)
            : this(from._blob?.AddRef())
        {
        }

        public PhpString(PhpValue x, Context ctx)
        {
            _blob = new Blob();
            _blob.Add(x, ctx);
        }

        public PhpString(string value)
        {
            _blob = new Blob(value);    // TODO: _blob = string
        }

        public PhpString(byte[] value)
        {
            _blob = new Blob(value);
        }

        public PhpString(string x, string y)
        {
            _blob = new Blob(x, y);
        }

        public PhpString DeepCopy() => new PhpString(this);

        #endregion

        /// <summary>
        /// Gets mutable access to the string value.
        /// </summary>
        public Blob EnsureWritable() => _blob.IsShared ? (_blob = _blob.ReleaseOne()) : _blob;

        /// <summary>
        /// Outputs the string content to the context output streams.
        /// </summary>
        internal void Output(Context ctx) => _blob?.Output(ctx);

        // Prepend

        #region IPhpConvertible

        public bool ToBoolean() => _blob != null && _blob.ToBoolean();

        public double ToDouble() => Convert.StringToDouble(ToString());

        public long ToLong() => Convert.StringToLongInteger(ToString());

        public Convert.NumberInfo ToNumber(out PhpNumber number)
        {
            double d;
            long l;
            var info = Convert.StringToNumber(ToString(), out l, out d);
            number = (info & Convert.NumberInfo.Double) != 0
                ? PhpNumber.Create(d)
                : PhpNumber.Create(l);

            return info;
        }

        public string ToString(Context ctx) => ToString(ctx.StringEncoding);

        public string ToStringOrThrow(Context ctx) => ToString(ctx.StringEncoding);

        public object ToClass() => new stdClass(AsPhpValue(this));

        public PhpArray ToArray() => PhpArray.New(PhpValue.Create(this.DeepCopy()));

        #endregion

        #region this[int]

        /// <summary>
        /// Gets or sets character at given index according to PHP semantics.
        /// </summary>
        /// <param name="index">Character index.</param>
        /// <returns>Character at given position.</returns>
        public char this[int index]
        {
            get => _blob[index].AsChar();
            set => EnsureWritable()[index] = value;
        }

        #endregion

        public object Clone() => DeepCopy();

        /// <summary>
        /// Operator that checks the string is default/uninitialized not containing any value.
        /// </summary>
        public static bool IsNull(PhpString value) => value.IsDefault;

        /// <summary>
        /// Gets instance of blob that is not shared.
        /// </summary>
        public static Blob AsWritable(PhpString str) => str.EnsureWritable();

        /// <summary>
        /// Gets read-only array access to the string.
        /// For write access, use <see cref="EnsureWritable()"/>.
        /// </summary>
        public static Blob AsArray(PhpString str) => str._blob ?? new Blob();

        /// <summary>
        /// Wraps the string into <see cref="PhpValue"/>.
        /// </summary>
        internal static PhpValue AsPhpValue(PhpString str) => str.IsEmpty ? PhpValue.Create(string.Empty) : PhpValue.Create(str._blob.AddRef());

        public override string ToString() => _blob?.ToString(Encoding.UTF8);

        public string ToString(Encoding encoding) => _blob.ToString(encoding);

        public byte[] ToBytes(Context ctx) => ToBytes(ctx.StringEncoding);

        public byte[] ToBytes(Encoding encoding) => IsEmpty ? Array.Empty<byte>() : _blob.ToBytes(encoding);

        public PhpNumber ToNumber()
        {
            double d;
            long l;
            var info = Convert.StringToNumber(ToString(), out l, out d);
            return (info & Convert.NumberInfo.Double) != 0 ? PhpNumber.Create(d) : PhpNumber.Create(l);
        }
    }
}
