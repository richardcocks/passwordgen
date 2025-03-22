using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class Example3
    {

        [BenchmarkCategory("Vulnerable"), Benchmark(Baseline = true)]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string GeneratePassword(int length)
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            System.Random random = new System.Random();

            for (int i = 0; i < length; i++)
            {
                password += characters[random.Next(characters.Length)];
            }
            return password;
        }

        [BenchmarkCategory("Secure"), Benchmark(Baseline = true)]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string SecureRandom(int length)
        {
            string password = "";
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            for (int i = 0; i < length; i++)
            {
                password += characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return password;
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string Buffer(int length)
        {

            string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

            byte[] bytebuffer = new byte[length];
            Random.Shared.NextBytes(bytebuffer);

            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = characters[bytebuffer[i] % 64];
            }

            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string BufferSecure(int length)
        {
            string characters = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

            byte[] bytebuffer = new byte[length];
            RandomNumberGenerator.Fill(bytebuffer);

            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = characters[bytebuffer[i] % 64];
            }

            return new(buffer);
        }

    }
}