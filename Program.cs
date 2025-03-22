using BenchmarkDotNet.Running;
using PasswordGen;

/*
var x = new PasswordGenerator()
{
    n = 12
};
Console.WriteLine($"PasswordChatGpt: {x.PasswordChatGpt()}");
Console.WriteLine($"PasswordChatGptStringBuilder: {x.PasswordChatGptStringBuilder()}");
Console.WriteLine($"PasswordUsingCharBufferCharLookupSpan: {x.PasswordUsingCharBufferCharLookupSpan()}");
Console.WriteLine($"PasswordUsingCharBufferCharLookupReducedSetPregen: {x.PasswordUsingCharBufferCharLookupReducedSetPregen()}");
*/

BenchmarkRunner.Run<Example5>();
