// <copyright file="BufferExtensionsTest.BlockCopy.g.cs" company="TVA">No copyright is claimed pursuant to 17 USC § 105.  All Other Rights Reserved.</copyright>
// <auto-generated>
// This file contains automatically generated unit tests.
// Do NOT modify this file manually.
// 
// When Pex is invoked again,
// it might remove or update any previously generated unit tests.
// 
// If the contents of this file becomes outdated, e.g. if it does not
// compile anymore, you may delete this file and invoke Pex again.
// </auto-generated>
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Pex.Framework.Generated;

namespace TVA
{
    public partial class BufferExtensionsTest
    {
[TestMethod]
[PexGeneratedBy(typeof(BufferExtensionsTest))]
[ExpectedException(typeof(ArgumentNullException))]
public void BlockCopy01()
{
    byte[] bs;
    bs = this.BlockCopy((byte[])null, 0, 0);
}
[TestMethod]
[PexGeneratedBy(typeof(BufferExtensionsTest))]
[ExpectedException(typeof(ArgumentOutOfRangeException))]
public void BlockCopy02()
{
    byte[] bs;
    byte[] bs1 = new byte[0];
    bs = this.BlockCopy(bs1, 0, 0);
}
[TestMethod]
[PexGeneratedBy(typeof(BufferExtensionsTest))]
[ExpectedException(typeof(ArgumentOutOfRangeException))]
public void BlockCopy03()
{
    byte[] bs;
    byte[] bs1 = new byte[0];
    bs = this.BlockCopy(bs1, int.MinValue, 0);
}
[TestMethod]
[PexGeneratedBy(typeof(BufferExtensionsTest))]
[ExpectedException(typeof(ArgumentOutOfRangeException))]
public void BlockCopy04()
{
    byte[] bs;
    byte[] bs1 = new byte[0];
    bs = this.BlockCopy(bs1, 0, int.MinValue);
}
[TestMethod]
[PexGeneratedBy(typeof(BufferExtensionsTest))]
public void BlockCopy05()
{
    byte[] bs;
    byte[] bs1 = new byte[1];
    bs = this.BlockCopy(bs1, 0, 0);
    Assert.IsNotNull((object)bs);
    Assert.AreEqual<int>(0, bs.Length);
}
[TestMethod]
[PexGeneratedBy(typeof(BufferExtensionsTest))]
public void BlockCopy06()
{
    byte[] bs;
    byte[] bs1 = new byte[1];
    bs = this.BlockCopy(bs1, 0, 3);
    Assert.IsNotNull((object)bs);
    Assert.AreEqual<int>(1, bs.Length);
    Assert.AreEqual<byte>((byte)0, bs[0]);
}
    }
}
