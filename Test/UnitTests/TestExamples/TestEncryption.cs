﻿// Copyright (c) 2021 Jon P Smith, GitHub: JonPSmith, web: http://www.thereformedprogrammer.net/
// Licensed under MIT license. See License.txt in the project root for license information.

using ExamplesCommonCode.UsefulCode;
using Xunit;
using Xunit.Abstractions;
using Xunit.Extensions.AssertExtensions;

namespace Test.UnitTests.TestExamples;

public class TestEncryption
{
    private readonly ITestOutputHelper _output;

    public TestEncryption(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void TestEncryptDecrypt()
    {
        //SETUP
        var encryption = new EncryptDecrypt("asaadjn33TbAw441azn");
        var testString = "The cat on the ö and had a great Ō.";

        //ATTEMPT
        var encrypted = encryption.Encrypt(testString);
        var decrypted = encryption.Decrypt(encrypted);

        //VERIFY
        decrypted.ShouldEqual(testString);
        _output.WriteLine($"Original string length = {testString.Length}, encrypted string length = {encrypted.Length}, ratio = {encrypted.Length/testString.Length:P}");
    }
}