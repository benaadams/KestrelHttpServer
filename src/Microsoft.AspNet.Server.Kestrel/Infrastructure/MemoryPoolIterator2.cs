﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Numerics;

namespace Microsoft.AspNet.Server.Kestrel.Infrastructure
{
    public struct MemoryPoolIterator2
    {
        private readonly static int _vectorSpan = Vector<byte>.Count;

        private MemoryPoolBlock2 _block;
        private int _index;

        public MemoryPoolIterator2(MemoryPoolBlock2 block)
        {
            _block = block;
            _index = _block?.Start ?? 0;
        }
        public MemoryPoolIterator2(MemoryPoolBlock2 block, int index)
        {
            _block = block;
            _index = index;
        }

        public bool IsDefault => _block == null;

        public bool IsEnd
        {
            get
            {
                if (_block == null)
                {
                    return true;
                }
                else if (_index < _block.End)
                {
                    return false;
                }
                else
                {
                    var block = _block.Next;
                    while (block != null)
                    {
                        if (block.Start < block.End)
                        {
                            return false; // subsequent block has data - IsEnd is false
                        }
                        block = block.Next;
                    }
                    return true;
                }
            }
        }

        public MemoryPoolBlock2 Block => _block;

        public int Index => _index;

        public int Take()
        {
            if (_block == null)
            {
                return -1;
            }
            else if (_index < _block.End)
            {
                return _block.Array[_index++];
            }

            var block = _block;
            var index = _index;
            while (true)
            {
                if (index < block.End)
                {
                    _block = block;
                    _index = index + 1;
                    return block.Array[index];
                }
                else if (block.Next == null)
                {
                    return -1;
                }
                else
                {
                    block = block.Next;
                    index = block.Start;
                }
            }
        }

        public int Peek()
        {
            if (_block == null)
            {
                return -1;
            }
            else if (_index < _block.End)
            {
                return _block.Array[_index];
            }
            else if (_block.Next == null)
            {
                return -1;
            }

            var block = _block.Next;
            var index = block.Start;
            while (true)
            {
                if (index < block.End)
                {
                    return block.Array[index];
                }
                else if (block.Next == null)
                {
                    return -1;
                }
                else
                {
                    block = block.Next;
                    index = block.Start;
                }
            }
        }

        public unsafe long PeekLong()
        {
            if (_block == null)
            {
                return -1;
            }
            else if (_block.End - _index >= sizeof(long))
            {
                return *(long*)(_block.Pointer + _index);
            }
            else if (_block.Next == null)
            {
                return -1;
            }
            else
            {
                var blockBytes = _block.End - _index;
                var nextBytes = sizeof(long) - blockBytes;

                if (_block.Next.End - _block.Next.Start < nextBytes)
                {
                    return -1;
                }

                var blockLong = *(long*)(_block.Pointer + _block.End - sizeof(long));

                var nextLong = *(long*)(_block.Next.Pointer + _block.Next.Start);

                return (blockLong >> (sizeof(long) - blockBytes) * 8) | (nextLong << (sizeof(long) - nextBytes) * 8);
            }
        }

        public unsafe int Seek(ref Vector<byte> byte0Vector)
        {
            if (IsDefault)
            {
                return -1;
            }

            var following = _block.End - _index;
            var block = _block;
            var index = _index;
            byte[] array;
            var byte0 = byte0Vector[0];

            while (true)
            {
                while (following == 0)
                {
                    var newBlock = block.Next;
                    if (newBlock == null)
                    {
                        _block = block;
                        _index = index;
                        return -1;
                    }
                    index = newBlock.Start;
                    following = newBlock.End - index;
                    block = newBlock;
                }
                array = block.Array;
                while (following > 0)
                {
#if !DEBUG // Need unit tests to test Vector path
                    // Check will be Jitted away https://github.com/dotnet/coreclr/issues/1079
                    if (Vector.IsHardwareAccelerated)
                    {
#endif
                        if (following >= _vectorSpan)
                        {
                            var byte0Equals = Vector.Equals(new Vector<byte>(array, index), byte0Vector);

                            if (byte0Equals.Equals(Vector<byte>.Zero))
                            {
                                following -= _vectorSpan;
                                index += _vectorSpan;
                                continue;
                            }

                            _block = block;
                            _index = index + FindFirstEqualByte(ref byte0Equals);
                            return byte0;
                        }
#if !DEBUG // Need unit tests to test Vector path
                    }
#endif
                    var pCurrent = block.Pointer + index;
                    var pEnd = pCurrent + following;
                    do
                    {
                        if (*pCurrent == byte0)
                        {
                            _block = block;
                            _index = index;
                            return byte0;
                        }
                        pCurrent++;
                        index++;
                    } while (pCurrent < pEnd);

                    following = 0;
                    break;
                }
            }
        }

        public unsafe int Seek(ref Vector<byte> byte0Vector, ref Vector<byte> byte1Vector)
        {
            if (IsDefault)
            {
                return -1;
            }

            var following = _block.End - _index;
            var block = _block;
            var index = _index;
            byte[] array;
            int byte0Index = int.MaxValue;
            int byte1Index = int.MaxValue;
            var byte0 = byte0Vector[0];
            var byte1 = byte1Vector[0];

            while (true)
            {
                while (following == 0)
                {
                    var newBlock = block.Next;
                    if (newBlock == null)
                    {
                        _block = block;
                        _index = index;
                        return -1;
                    }
                    index = newBlock.Start;
                    following = newBlock.End - index;
                    block = newBlock;
                }
                array = block.Array;
                while (following > 0)
                {

#if !DEBUG // Need unit tests to test Vector path
                    // Check will be Jitted away https://github.com/dotnet/coreclr/issues/1079
                    if (Vector.IsHardwareAccelerated)
                    {
#endif
                        if (following >= _vectorSpan)
                        {
                            var data = new Vector<byte>(array, index);
                            var byte0Equals = Vector.Equals(data, byte0Vector);
                            var byte1Equals = Vector.Equals(data, byte1Vector);

                            if (!byte0Equals.Equals(Vector<byte>.Zero))
                            {
                                byte0Index = FindFirstEqualByte(ref byte0Equals);
                            }
                            if (!byte1Equals.Equals(Vector<byte>.Zero))
                            {
                                byte1Index = FindFirstEqualByte(ref byte1Equals);
                            }

                            if (byte0Index == int.MaxValue && byte1Index == int.MaxValue)
                            {
                                following -= _vectorSpan;
                                index += _vectorSpan;
                                continue;
                            }

                            _block = block;

                            if (byte0Index < byte1Index)
                            {
                                _index = index + byte0Index;
                                return byte0;
                            }

                            _index = index + byte1Index;
                            return byte1;
                        }
#if !DEBUG // Need unit tests to test Vector path
                    }
#endif
                    var pCurrent = block.Pointer + index;
                    var pEnd = pCurrent + following;
                    do
                    {
                        if (*pCurrent == byte0)
                        {
                            _block = block;
                            _index = index;
                            return byte0;
                        }
                        if (*pCurrent == byte1)
                        {
                            _block = block;
                            _index = index;
                            return byte1;
                        }
                        pCurrent++;
                        index++;
                    } while (pCurrent != pEnd);

                    following = 0;
                    break;
                }
            }
        }

        public unsafe int Seek(ref Vector<byte> byte0Vector, ref Vector<byte> byte1Vector, ref Vector<byte> byte2Vector)
        {
            if (IsDefault)
            {
                return -1;
            }

            var following = _block.End - _index;
            var block = _block;
            var index = _index;
            byte[] array;
            int byte0Index = int.MaxValue;
            int byte1Index = int.MaxValue;
            int byte2Index = int.MaxValue;
            var byte0 = byte0Vector[0];
            var byte1 = byte1Vector[0];
            var byte2 = byte2Vector[0];

            while (true)
            {
                while (following == 0)
                {
                    var newBlock = block.Next;
                    if (newBlock == null)
                    {
                        _block = block;
                        _index = index;
                        return -1;
                    }
                    index = newBlock.Start;
                    following = newBlock.End - index;
                    block = newBlock;
                }
                array = block.Array;
                while (following > 0)
                {
#if !DEBUG // Need unit tests to test Vector path
                    // Check will be Jitted away https://github.com/dotnet/coreclr/issues/1079
                    if (Vector.IsHardwareAccelerated)
                    {
#endif
                        if (following >= _vectorSpan)
                        {
                            var data = new Vector<byte>(array, index);
                            var byte0Equals = Vector.Equals(data, byte0Vector);
                            var byte1Equals = Vector.Equals(data, byte1Vector);
                            var byte2Equals = Vector.Equals(data, byte2Vector);

                            if (!byte0Equals.Equals(Vector<byte>.Zero))
                            {
                                byte0Index = FindFirstEqualByte(ref byte0Equals);
                            }
                            if (!byte1Equals.Equals(Vector<byte>.Zero))
                            {
                                byte1Index = FindFirstEqualByte(ref byte1Equals);
                            }
                            if (!byte2Equals.Equals(Vector<byte>.Zero))
                            {
                                byte2Index = FindFirstEqualByte(ref byte2Equals);
                            }

                            if (byte0Index == int.MaxValue && byte1Index == int.MaxValue && byte2Index == int.MaxValue)
                            {
                                following -= _vectorSpan;
                                index += _vectorSpan;
                                continue;
                            }

                            _block = block;

                            int toReturn, toMove;
                            if (byte0Index < byte1Index)
                            {
                                if (byte0Index < byte2Index)
                                {
                                    toReturn = byte0;
                                    toMove = byte0Index;
                                }
                                else
                                {
                                    toReturn = byte2;
                                    toMove = byte2Index;
                                }
                            }
                            else
                            {
                                if (byte1Index < byte2Index)
                                {
                                    toReturn = byte1;
                                    toMove = byte1Index;
                                }
                                else
                                {
                                    toReturn = byte2;
                                    toMove = byte2Index;
                                }
                            }

                            _index = index + toMove;
                            return toReturn;
                        }
#if !DEBUG // Need unit tests to test Vector path
                    }
#endif
                    var pCurrent = block.Pointer + index;
                    var pEnd = pCurrent + following;
                    do
                    {
                        if (*pCurrent == byte0)
                        {
                            _block = block;
                            _index = index;
                            return byte0;
                        }
                        if (*pCurrent == byte1)
                        {
                            _block = block;
                            _index = index;
                            return byte1;
                        }
                        if (*pCurrent == byte2)
                        {
                            _block = block;
                            _index = index;
                            return byte2;
                        }
                        pCurrent++;
                        index++;
                    } while (pCurrent != pEnd);

                    following = 0;
                    break;
                }
            }
        }

        private static int FindFirstEqualByte(ref Vector<byte> byteEquals)
        {
            // Quasi-tree search
            var vector64 = Vector.AsVectorInt64(byteEquals);
            for (var i = 0; i < Vector<long>.Count; i++)
            {
                var longValue = vector64[i];
                if (longValue == 0) continue;

                var shift = i << 1;
                var offset = shift << 2;
                var vector32 = Vector.AsVectorInt32(byteEquals);
                if (vector32[shift] != 0)
                {
                    if (byteEquals[offset] != 0) return offset;
                    if (byteEquals[offset + 1] != 0) return offset + 1;
                    if (byteEquals[offset + 2] != 0) return offset + 2;
                    return offset + 3;
                }
                if (byteEquals[offset + 4] != 0) return offset + 4;
                if (byteEquals[offset + 5] != 0) return offset + 5;
                if (byteEquals[offset + 6] != 0) return offset + 6;
                return offset + 7;
            }
            throw new InvalidOperationException();
        }

        /// <summary>
        /// Save the data at the current location then move to the next available space.
        /// </summary>
        /// <param name="data">The byte to be saved.</param>
        /// <returns>true if the operation successes. false if can't find available space.</returns>
        public bool Put(byte data)
        {
            if (_block == null)
            {
                return false;
            }
            else if (_index < _block.End)
            {
                _block.Array[_index++] = data;
                return true;
            }

            var block = _block;
            var index = _index;
            while (true)
            {
                if (index < block.End)
                {
                    _block = block;
                    _index = index + 1;
                    block.Array[index] = data;
                    return true;
                }
                else if (block.Next == null)
                {
                    return false;
                }
                else
                {
                    block = block.Next;
                    index = block.Start;
                }
            }
        }

        public int GetLength(MemoryPoolIterator2 end)
        {
            if (IsDefault || end.IsDefault)
            {
                return -1;
            }

            var block = _block;
            var index = _index;
            var length = 0;
            while (true)
            {
                if (block == end._block)
                {
                    return length + end._index - index;
                }
                else if (block.Next == null)
                {
                    throw new InvalidOperationException("end did not follow iterator");
                }
                else
                {
                    length += block.End - index;
                    block = block.Next;
                    index = block.Start;
                }
            }
        }

        public unsafe MemoryPoolIterator2 CopyTo(byte[] array, int offset, int count, out int actual)
        {
            if (IsDefault)
            {
                actual = 0;
                return this;
            }

            var block = _block;
            var index = _index;
            var remaining = count;
            while (true)
            {
                var following = block.End - index;
                if (remaining <= following)
                {
                    actual = count;
                    if (array != null)
                    {
#if DOTNET5_4 || DNXCORE50
                        fixed (byte* pDst = array)
                        {
                            Buffer.MemoryCopy(block.Pointer + index, pDst + offset, remaining, remaining);
                        }
#else
                        Buffer.BlockCopy(block.Array, index, array, offset, remaining);
#endif
                    }
                    return new MemoryPoolIterator2(block, index + remaining);
                }
                else if (block.Next == null)
                {
                    actual = count - remaining + following;
                    if (array != null)
                    {
#if DOTNET5_4 || DNXCORE50
                        fixed (byte* pDst = array)
                        {
                            Buffer.MemoryCopy(block.Pointer + index, pDst + offset, following, following);
                        }
#else
                        Buffer.BlockCopy(block.Array, index, array, offset, following);
#endif
                    }
                    return new MemoryPoolIterator2(block, index + following);
                }
                else
                {
                    if (array != null)
                    {
#if DOTNET5_4 || DNXCORE50
                        fixed (byte* pDst = array)
                        {
                            Buffer.MemoryCopy(block.Pointer + index, pDst + offset, following, following);
                        }
#else
                        Buffer.BlockCopy(block.Array, index, array, offset, following);
#endif
                    }
                    offset += following;
                    remaining -= following;
                    block = block.Next;
                    index = block.Start;
                }
            }
        }

        public void CopyFrom(byte[] data)
        {
            CopyFrom(data, 0, data.Length);
        }

#if DOTNET5_4 || DNXCORE50
        public unsafe void CopyFrom(byte* data, int count)
        {
            var block = _block;
            var blockIndex = _index;
            var blockRemaining = block.BlockEndOffset - blockIndex;

            if (blockRemaining >= count)
            {
                _index = blockIndex + count;

                Buffer.MemoryCopy(data, block.Pointer + blockIndex, count, count);

                block.End = _index;
                return;
            }

            do
            {
                if (blockRemaining == 0)
                {
                    var nextBlock = block.Pool.Lease();
                    blockIndex = nextBlock.Data.Offset;
                    blockRemaining = nextBlock.Data.Count;
                    block.Next = nextBlock;
                    block = nextBlock;
                }

                if (count > blockRemaining)
                {
                    count -= blockRemaining;

                    Buffer.MemoryCopy(data, block.Pointer + blockIndex, blockRemaining, blockRemaining);
        
                    data += blockRemaining;
                    block.End = blockIndex + blockRemaining;
                    blockRemaining = 0;
                    continue;
                }
                else
                {
                    _index = blockIndex + count;

                    Buffer.MemoryCopy(data, block.Pointer + blockIndex, count, count);

                    block.End = _index;
                    _block = block;
                    return;
                }
            } while (true);
        }

        public unsafe void CopyFrom(byte[] data, int offset, int count)
        {
            var block = _block;
            var blockIndex = _index;
            var blockRemaining = block.BlockEndOffset - blockIndex;

            fixed (byte* pSrc = data)
            {
                if (blockRemaining >= count)
                {
                    _index = blockIndex + count;

                    Buffer.MemoryCopy(pSrc + offset, block.Pointer + blockIndex, count, count);

                    block.End = _index;
                    return;
                }

                do
                {
                    if (blockRemaining == 0)
                    {
                        var nextBlock = block.Pool.Lease();
                        blockIndex = nextBlock.Data.Offset;
                        blockRemaining = nextBlock.Data.Count;
                        block.Next = nextBlock;
                        block = nextBlock;
                    }

                    if (count > blockRemaining)
                    {
                        count -= blockRemaining;

                        Buffer.MemoryCopy(pSrc + offset, block.Pointer + blockIndex, blockRemaining, blockRemaining);
        
                        offset += blockRemaining;
                        block.End = blockIndex + blockRemaining;
                        blockRemaining = 0;
                        continue;
                    }
                    else
                    {
                        _index = blockIndex + count;

                        Buffer.MemoryCopy(pSrc + offset, block.Pointer + blockIndex, count, count);

                        block.End = _index;
                        _block = block;
                        return;
                    }
                } while (true);
            }
        }
#else
        public unsafe void CopyFrom(byte[] data, int offset, int count)
        {
            var block = _block;
            var blockIndex = _index;
            var blockRemaining = block.BlockEndOffset - blockIndex;

            if (blockRemaining >= count)
            {
                _index = blockIndex + count;

                Buffer.BlockCopy(data, offset, block.Array, blockIndex, count);

                block.End = _index;
                return;
            }

            do
            {
                if (blockRemaining == 0)
                {
                    var nextBlock = block.Pool.Lease();
                    blockIndex = nextBlock.Data.Offset;
                    blockRemaining = nextBlock.Data.Count;
                    block.Next = nextBlock;
                    block = nextBlock;
                }

                if (count > blockRemaining)
                {
                    count -= blockRemaining;

                    Buffer.BlockCopy(data, offset, block.Array, blockIndex, blockRemaining);

                    block.End = blockIndex + blockRemaining;
                    offset += blockRemaining;
                    blockRemaining = 0;
                    continue;
                }
                else
                {
                    _index = blockIndex + count;

                    Buffer.BlockCopy(data, offset, block.Array, blockIndex, count);

                    block.End = _index;
                    _block = block;
                    return;
                }
            } while (true);
        }
#endif

        public unsafe void CopyFromAscii(string data)
        {
            var block = _block;
            var blockIndex = _index;
            var count = data.Length;

            var blockRemaining = block.BlockEndOffset - blockIndex;

            fixed (char* pData = data)
            {
                if (blockRemaining >= count)
                {
                    _index = blockIndex + count;

                    CopyFromAscii(pData, block.Pointer + blockIndex, count);

                    block.End = _index;
                    return;
                }

                var input = pData;
                do
                {
                    if (blockRemaining == 0)
                    {
                        var nextBlock = block.Pool.Lease();
                        blockIndex = nextBlock.Data.Offset;
                        blockRemaining = nextBlock.Data.Count;
                        block.Next = nextBlock;
                        block = nextBlock;
                    }

                    if (count > blockRemaining)
                    {
                        count -= blockRemaining;

                        CopyFromAscii(input, block.Pointer + blockIndex, blockRemaining);

                        block.End = blockIndex + blockRemaining;
                        input += blockRemaining;
                        blockRemaining = 0;
                        continue;
                    }
                    else
                    {
                        _index = blockIndex + count;

                        CopyFromAscii(input, block.Pointer + blockIndex, count);

                        block.End = _index;
                        _block = block;
                        return;
                    }
                } while (true);
            }
        }

        private unsafe static void CopyFromAscii(char* input, byte* output, int count)
        {
            var i = 0;

            while (i + 11 < count)
            {
                i += 12;
                *(output) = (byte)*(input);
                *(output + 1) = (byte)*(input + 1);
                *(output + 2) = (byte)*(input + 2);
                *(output + 3) = (byte)*(input + 3);
                *(output + 4) = (byte)*(input + 4);
                *(output + 5) = (byte)*(input + 5);
                *(output + 6) = (byte)*(input + 6);
                *(output + 7) = (byte)*(input + 7);
                *(output + 8) = (byte)*(input + 8);
                *(output + 9) = (byte)*(input + 9);
                *(output + 10) = (byte)*(input + 10);
                *(output + 11) = (byte)*(input + 11);
                output += 12;
                input += 12;
            }
            if (i + 6 < count)
            {
                i += 6;
                *(output) = (byte)*(input);
                *(output + 1) = (byte)*(input + 1);
                *(output + 2) = (byte)*(input + 2);
                *(output + 3) = (byte)*(input + 3);
                *(output + 4) = (byte)*(input + 4);
                *(output + 5) = (byte)*(input + 5);
                output += 6;
                input += 6;
            }
            if (i + 3 < count)
            {
                i += 4;
                *(output) = (byte)*(input);
                *(output + 1) = (byte)*(input + 1);
                *(output + 2) = (byte)*(input + 2);
                *(output + 3) = (byte)*(input + 3);
                output += 4;
                input += 4;
            }
            while (i < count)
            {
                i++;
                *(output++) = (byte)*(input++);
            }
        }
    }
}
