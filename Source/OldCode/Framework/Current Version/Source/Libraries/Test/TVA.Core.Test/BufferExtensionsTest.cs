// <copyright file="BufferExtensionsTest.cs" company="TVA">No copyright is claimed pursuant to 17 USC § 105.  All Other Rights Reserved.</copyright>

using System;
using Microsoft.Pex.Framework;
using Microsoft.Pex.Framework.Validation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TVA;

namespace TVA
{
    [TestClass]
    [PexClass(typeof(BufferExtensions))]
    [PexAllowedExceptionFromTypeUnderTest(typeof(ArgumentException), AcceptExceptionSubtypes = true)]
    [PexAllowedExceptionFromTypeUnderTest(typeof(InvalidOperationException))]
    public partial class BufferExtensionsTest
    {
        [PexMethod]
        public byte[] BlockCopy(
            byte[] source,
            int startIndex,
            int length
        )
        {
            byte[] result = BufferExtensions.BlockCopy(source, startIndex, length);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.BlockCopy(Byte[], Int32, Int32)
        }
        [PexMethod]
        public byte[] Combine(byte[] source, byte[] other)
        {
            byte[] result = BufferExtensions.Combine(source, other);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.Combine(Byte[], Byte[])
        }
        [PexMethod]
        public byte[] Combine01(
            byte[] source,
            int sourceOffset,
            int sourceCount,
            byte[] other,
            int otherOffset,
            int otherCount
        )
        {
            byte[] result
               = BufferExtensions.Combine(source, sourceOffset, sourceCount, other, otherOffset, otherCount);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.Combine01(Byte[], Int32, Int32, Byte[], Int32, Int32)
        }
        [PexMethod]
        public byte[] Combine02(
            byte[] source,
            byte[] other1,
            byte[] other2
        )
        {
            byte[] result = BufferExtensions.Combine(source, other1, other2);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.Combine02(Byte[], Byte[], Byte[])
        }
        [PexMethod]
        public byte[] Combine03(
            byte[] source,
            byte[] other1,
            byte[] other2,
            byte[] other3
        )
        {
            byte[] result = BufferExtensions.Combine(source, other1, other2, other3);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.Combine03(Byte[], Byte[], Byte[], Byte[])
        }
        [PexMethod]
        public byte[] Combine04(
            byte[] source,
            byte[] other1,
            byte[] other2,
            byte[] other3,
            byte[] other4
        )
        {
            byte[] result = BufferExtensions.Combine(source, other1, other2, other3, other4);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.Combine04(Byte[], Byte[], Byte[], Byte[], Byte[])
        }
        [PexMethod]
        public byte[] Combine05(byte[][] buffers)
        {
            byte[] result = BufferExtensions.Combine(buffers);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.Combine05(Byte[][])
        }
        [PexMethod]
        public int CompareTo(byte[] source, byte[] other)
        {
            int result = BufferExtensions.CompareTo(source, other);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.CompareTo(Byte[], Byte[])
        }
        [PexMethod]
        public int CompareTo01(
            byte[] source,
            int sourceOffset,
            byte[] other,
            int otherOffset,
            int count
        )
        {
            int result = BufferExtensions.CompareTo(source, sourceOffset, other, otherOffset, count);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.CompareTo01(Byte[], Int32, Byte[], Int32, Int32)
        }
        [PexMethod]
        public int IndexOfSequence(byte[] buffer, byte[] bytesToFind)
        {
            int result = BufferExtensions.IndexOfSequence(buffer, bytesToFind);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.IndexOfSequence(Byte[], Byte[])
        }
        [PexMethod]
        public int IndexOfSequence01(
            byte[] buffer,
            byte[] bytesToFind,
            int startIndex
        )
        {
            int result = BufferExtensions.IndexOfSequence(buffer, bytesToFind, startIndex);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.IndexOfSequence01(Byte[], Byte[], Int32)
        }
        [PexMethod]
        public int IndexOfSequence02(
            byte[] buffer,
            byte[] bytesToFind,
            int startIndex,
            int length
        )
        {
            int result = BufferExtensions.IndexOfSequence(buffer, bytesToFind, startIndex, length);
            return result;
            // TODO: add assertions to method BufferExtensionsTest.IndexOfSequence02(Byte[], Byte[], Int32, Int32)
        }
        [PexMethod]
        public void IndexOfSequencePartial()
        {
            byte[] buffer = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x02, 0x03 };
            byte[] bytesToFind = new byte[] { 0x02, 0x06 };
            int startIndex = 0;
            int length = buffer.Length - startIndex;
            Assert.AreEqual(-1, buffer.IndexOfSequence(bytesToFind));
            Assert.AreEqual(-1, buffer.IndexOfSequence(bytesToFind, startIndex));
            Assert.AreEqual(-1, buffer.IndexOfSequence(bytesToFind, startIndex, length));
        }
    }
}
