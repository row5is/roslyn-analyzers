﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Test.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NetCore.Analyzers.Security.UnitTests
{
    public class DoNotHardCodeEncryptionKeyTests : TaintedDataAnalyzerTestBase
    {
        public DoNotHardCodeEncryptionKeyTests(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override DiagnosticDescriptor Rule => DoNotHardCodeEncryptionKey.Rule;

        protected override IEnumerable<string> AdditionalCSharpSources => new string[] { readOnlySpanAndAesGcmAndAesCcmCSharpSourceCode };

        public const string readOnlySpanAndAesGcmAndAesCcmCSharpSourceCode = @"
namespace System
{
    public struct ReadOnlySpan<T>
    {
        public ReadOnlySpan (T[] array)
        {
        }
    }
}

namespace System.Security.Cryptography
{
    public sealed class AesGcm
    {
        public AesGcm (byte[] key)
        {
        }

        public AesGcm (ReadOnlySpan<byte> key)
        {
        }
    }

    public sealed class AesCcm
    {
        public AesCcm (byte[] key)
        {
        }

        public AesCcm (ReadOnlySpan<byte> key)
        {
        }
    }
}";

        [Fact]
        public void Test_HardcodedStaticReadonlyField__Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

internal static class Program
{
    private static readonly Byte[] _key = { 1, 2, 3 };

    private static void Main()
    {
        using (var aes = Aes.Create())
        {
            aes.Key = Program._key;
        }
    }
}");
            // Ideally, we'd treat _key as hardcoded.
        }

        [Fact]
        public void Test_HardcodedInString_CreateEncryptor_NeedValueContentAnalysis_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] key = Convert.FromBase64String(""AAAAAaazaoensuth"");
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(11, 9, 9, 22, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[] Convert.FromBase64String(string s)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_ASCIIEncodingGetBytesWithStringParameter_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System.Text;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] key = new ASCIIEncoding().GetBytes(""AAAAAaazaoensuth"");
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(11, 9, 9, 22, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[] Encoding.GetBytes(string s)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_EncodingUTF8GetBytesWithStringParameter_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System.Text;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] key = Encoding.UTF8.GetBytes(""AAAAAaazaoensuth"");
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(11, 9, 9, 22, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[] Encoding.GetBytes(string s)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_ASCIIEncodingGetBytesWithStringAndInt32AndInt32AndByteArrayAndInt32Parameters_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System.Text;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] key, byte[] someOtherBytesForIV)
    {
        new ASCIIEncoding().GetBytes(""AAAAAaazaoensuth"", 0, 3, key, 0);
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(11, 9, 9, 38, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] key, byte[] someOtherBytesForIV)", "string chars", "int ASCIIEncoding.GetBytes(string chars, int charIndex, int charCount, byte[] bytes, int byteIndex)"));
        }

        [Fact]
        public void Test_ASCIIEncodingGetBytesWithCharArrayAndInt32AndInt32AndByteArrayAndInt32Parameters_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System.Text;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] key, byte[] someOtherBytesForIV)
    {
        char[] chars = new char[] {'1', '2', '3'};
        new ASCIIEncoding().GetBytes(chars, 0, 3, key, 0);
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(12, 9, 9, 24, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] key, byte[] someOtherBytesForIV)", "char[]", "void TestClass.TestMethod(byte[] key, byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInStringWithVariable_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        string someHardCodedBase64String = ""AAAAAaazaoensuth"";
        byte[] key = Convert.FromBase64String(someHardCodedBase64String);
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(12, 9, 10, 22, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[] Convert.FromBase64String(string s)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInMultilinesString_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        string someHardCodedBase64String = ""sssdsdsdsdsdsds"" +
                                          ""sdasdsadasddsda""  + 
                                          ""sdasdsadasddsda"" ;
        byte[] key = Convert.FromBase64String(someHardCodedBase64String);
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);

    }
}",
            GetCSharpResultAt(14, 9, 12, 22, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[] Convert.FromBase64String(string s)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInByteArray_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] rgbKey = new byte[] {1, 2, 3};
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(11, 9, 9, 25, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_AesGcmWithByteArrayParameter_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        byte[] key = new byte[] {1, 2, 3};
        AesGcm aesGcm = new AesGcm(key);
    }
}",
            GetCSharpResultAt(10, 25, 9, 22, "AesGcm.AesGcm(byte[] key)", "void TestClass.TestMethod()", "byte[]", "void TestClass.TestMethod()"));
        }

        [Fact]
        public void Test_AesGcmWithReadOnlySpanParameter_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        ReadOnlySpan<byte> key = new ReadOnlySpan<byte>(new byte[] {1, 2, 3});
        AesGcm aesGcm = new AesGcm(key);
    }
}",
            GetCSharpResultAt(10, 25, 9, 57, "AesGcm.AesGcm(ReadOnlySpan<byte> key)", "void TestClass.TestMethod()", "byte[]", "void TestClass.TestMethod()"));
        }

        [Fact]
        public void Test_HardcodedInStringWithVariable_AesGcm_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        string someHardCodedBase64String = ""AAAAAaazaoensuth"";
        byte[] key = Convert.FromBase64String(someHardCodedBase64String);
        AesGcm aesGcm = new AesGcm(key);
    }
}",
            GetCSharpResultAt(11, 25, 10, 22, "AesGcm.AesGcm(byte[] key)", "void TestClass.TestMethod()", "byte[] Convert.FromBase64String(string s)", "void TestClass.TestMethod()"));
        }

        [Fact]
        public void Test_AesCcmWithByteArrayParameter_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        byte[] key = new byte[] {1, 2, 3};
        AesCcm aesCcm = new AesCcm(key);
    }
}",
            GetCSharpResultAt(10, 25, 9, 22, "AesCcm.AesCcm(byte[] key)", "void TestClass.TestMethod()", "byte[]", "void TestClass.TestMethod()"));
        }

        [Fact]
        public void Test_AesCcmWithReadOnlySpanParameter_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        ReadOnlySpan<byte> key = new ReadOnlySpan<byte>(new byte[] {1, 2, 3});
        AesCcm aesCcm = new AesCcm(key);
    }
}",
            GetCSharpResultAt(10, 25, 9, 57, "AesCcm.AesCcm(ReadOnlySpan<byte> key)", "void TestClass.TestMethod()", "byte[]", "void TestClass.TestMethod()"));
        }

        [Fact]
        public void Test_HardcodedInStringWithVariable_AesCcm_Diagnostic()
        {
            VerifyCSharpWithDependencies(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        string someHardCodedBase64String = ""AAAAAaazaoensuth"";
        byte[] key = Convert.FromBase64String(someHardCodedBase64String);
        AesCcm aesCcm = new AesCcm(key);
    }
}",
            GetCSharpResultAt(11, 25, 10, 22, "AesCcm.AesCcm(byte[] key)", "void TestClass.TestMethod()", "byte[] Convert.FromBase64String(string s)", "void TestClass.TestMethod()"));
        }

        [Fact]
        public void Test_HardcodedInByteArray_CreateDecryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] rgbKey = new byte[] {1, 2, 3};
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateDecryptor(rgbKey, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(11, 9, 9, 25, "ICryptoTransform SymmetricAlgorithm.CreateDecryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInByteArrayWithVariable_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte b = 1;
        byte[] rgbKey = new byte[] {b};
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(12, 9, 10, 25, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInByteArray_KeyProperty_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] rgbKey = new byte[] {1, 2, 3};
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.Key = rgbKey;
    }
}",
            GetCSharpResultAt(11, 9, 9, 25, "byte[] SymmetricAlgorithm.Key", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInByteArray_CreateEncryptorFromDerivedClassOfSymmetricAlgorithm_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] rgbKey = new byte[] {1, 2, 3};
        Aes aes = Aes.Create();
        aes.CreateEncryptor(rgbKey, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(11, 9, 9, 25, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInByteArray_AesKey_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        using (var aes = Aes.Create())
        {
            aes.Key = new Byte[] { 1, 2, 3 };
        }
    }
}",
            GetCSharpResultAt(11, 13, 11, 23, "byte[] SymmetricAlgorithm.Key", "void TestClass.TestMethod()", "byte[]", "void TestClass.TestMethod()"));
        }

        [Fact]
        public void Test_HardcodedInByteArray_CreateEncryptor_Multivalues_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] rgbKey = new byte[] {1, 2, 3};
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            rgbKey = new byte[] {4, 5, 6};
        }

        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(18, 9, 14, 22, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"),
            GetCSharpResultAt(18, 9, 9, 25, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInByteArray_CreateEncryptor_WithoutAssignment_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(new byte[] {1, 2, 3}, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(10, 9, 10, 30, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_MaybeHardcoded_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV, byte[] rgbKey)
    {
        Random r = new Random();

        if (r.Next(6) == 4)
        {
            rgbKey = new byte[] {4, 5, 6};
        }

        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey, someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(17, 9, 13, 22, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV, byte[] rgbKey)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV, byte[] rgbKey)"));
        }

        [Fact]
        public void Test_PassTaintedSourceInfoAsParameter_SinkMethodParameters_Interprocedual_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] key = Convert.FromBase64String(""AAAAAaazaoensuth"");
        CreateEncryptor(key, someOtherBytesForIV);
    }

    public void CreateEncryptor(byte[] rgbKey, byte[] rgbIV)
    {
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey, rgbIV);
    }
}",
            GetCSharpResultAt(16, 9, 9, 22, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "byte[] Convert.FromBase64String(string s)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_PassTaintedSourceInfoAsParameter_SinkProperties_Interprocedual_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod()
    {
        byte[] key = Convert.FromBase64String(""AAAAAaazaoensuth"");
        CreateEncryptor(key);
    }

    public void CreateEncryptor(byte[] rgbKey)
    {
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.Key = rgbKey;
    }
}",
                GetCSharpResultAt(16, 9, 9, 22, "byte[] SymmetricAlgorithm.Key", "void TestClass.CreateEncryptor(byte[] rgbKey)", "byte[] Convert.FromBase64String(string s)", "void TestClass.TestMethod()"));
        }

        [Fact]
        public void Test_HardcodedIn2DByteArray_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Linq;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[,] rgbKey = new byte[,] { { 1, 2, 3 }, { 4, 5, 6 } };
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey.Cast<byte>().ToArray(), someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(12, 9, 10, 26, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[,]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_HardcodedInJaggedArrayInitializer_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Linq;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV, byte unknownByte)
    {
        byte[][] rgbKey = new byte[3][]
        {
            new byte[] { 1, 2 },
            new byte[] { 3, 4, 5 },
            new byte[] { unknownByte }
        };
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey.Cast<byte>().ToArray(), someOtherBytesForIV);
    }
}",
            GetCSharpResultAt(17, 9, 13, 13, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV, byte unknownByte)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV, byte unknownByte)"),
            GetCSharpResultAt(17, 9, 12, 13, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV, byte unknownByte)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV, byte unknownByte)"));
        }

        [Fact]
        public void Test_HardcodeByParamsBytesArray_CreateEncryptor_Diagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] rgbKey = GetArray(1, 2, 3);
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey, someOtherBytesForIV);
    }

    public byte[] GetArray(params byte[] array)
    {
        return array;
    }
}",
            GetCSharpResultAt(11, 9, 9, 25, "ICryptoTransform SymmetricAlgorithm.CreateEncryptor(byte[] rgbKey, byte[] rgbIV)", "void TestClass.TestMethod(byte[] someOtherBytesForIV)", "byte[]", "void TestClass.TestMethod(byte[] someOtherBytesForIV)"));
        }

        [Fact]
        public void Test_ASCIIEncodingGetBytesWithCharArrayParameter_CreateEncryptor_NoDiagnostic()
        {
            VerifyCSharp(@"
using System.Text;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(char[] chars, byte[] someOtherBytesForIV)
    {
        byte[] key = new ASCIIEncoding().GetBytes(chars);
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}");
        }

        [Fact]
        public void Test_ASCIIEncodingGetBytesWithCharArrayAndInt32AndInt32AndByteArrayAndInt32Parameters_CreateEncryptor_NoDiagnostic()
        {
            VerifyCSharp(@"
using System.Text;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(char[] chars, byte[] key, byte[] someOtherBytesForIV)
    {
        new ASCIIEncoding().GetBytes(chars, 0, 3, key, 0);
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}");
        }

        [Fact]
        public void Test_ElementTypeIsTypeParameter_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;

class TestClass<T1> where T1 : struct
{
    public void MethodWithArrayParameter<T2>(params T2[] arr) where T2 : struct
    {
    }

    public void TestMethod(T1 t)
    {
        MethodWithArrayParameter(t);
    }
}");
        }

        [Fact]
        public void Test_HardcodedInJaggedArray_CreateEncryptor_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Linq;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[][] rgbKey = new byte[2][];
        rgbKey[0] = new byte[2] { 1, 2 };
        rgbKey[1] = new byte[3] { 3, 4, 5 };
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey.Cast<byte>().ToArray(), someOtherBytesForIV);
    }
}");
        }

        [Fact]
        public void Test_NotHardcoded_CreateEncryptor_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV, byte[] rgbKey)
    {
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey, someOtherBytesForIV);
    }
}");
        }

        [Fact]
        public void Test_HardcodedInArrayThenOverwrite_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV, byte[] key)
    {
        byte[] rgbKey = new byte[] {1, 2, 3};
        rgbKey = key;
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(rgbKey, someOtherBytesForIV);
    }
}");
        }

        [Fact]
        public void Test_NotHardcodedInString_CreateEncryptor_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] key = Convert.FromBase64String(Console.ReadLine());
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}");
        }

        // For now, it doesn't support checking return tainted source info.
        [Fact]
        public void Test_ReturnTaintedSourceInfo_Interprocedual_NoDiagnostic()
        {
            VerifyCSharp(@"
using System;
using System.Security.Cryptography;

class TestClass
{
    public byte[] GetKey()
    {
        return Convert.FromBase64String(""AAAAAaazaoensuth"");
    }

    public void TestMethod(byte[] someOtherBytesForIV)
    {
        byte[] key = GetKey();
        SymmetricAlgorithm rijn = SymmetricAlgorithm.Create();
        rijn.CreateEncryptor(key, someOtherBytesForIV);
    }
}");
        }

        [Fact, WorkItem(2723, "https://github.com/dotnet/roslyn-analyzers/issues/2723")]
        public void Test_ArrayInitializerInAttribute()
        {
            VerifyCSharp(@"
using System;

class MyAttr : Attribute
{
    public MyAttr (byte[] array) { }
}

[MyAttr(new byte[]{ 1 })]
class C
{
}");
        }

        protected override DiagnosticAnalyzer GetBasicDiagnosticAnalyzer()
        {
            return new DoNotHardCodeEncryptionKey();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new DoNotHardCodeEncryptionKey();
        }
    }
}
