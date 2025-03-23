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
        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
        private const string charactersShortSet = "abcdefghjkmnpqrstuwxyzABCDEFGHJKLMNPQRSTVWXYZ0123456789@#$%&()_+";

        [Params(14, 24, 32)]
        public int Length { get; set; }

        [BenchmarkCategory("Vulnerable"), Benchmark(Baseline = true)]
        public string GeneratePassword()
        {
            string password = "";
            System.Random random = new System.Random();

            for (int i = 0; i < Length; i++)
            {
                password += characters[random.Next(characters.Length)];
            }
            return password;
        }

        [BenchmarkCategory("Secure"), Benchmark(Baseline = true)]
        public string SecureRandom()
        {
            string password = "";

            for (int i = 0; i < Length; i++)
            {
                password += characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return password;
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string Buffer()
        {
            byte[] bytebuffer = new byte[Length];
            Random.Shared.NextBytes(bytebuffer);

            char[] buffer = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                buffer[i] = charactersShortSet[bytebuffer[i] % 64];
            }

            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string BufferSecure()
        {
            byte[] bytebuffer = new byte[Length];
            RandomNumberGenerator.Fill(bytebuffer);

            char[] buffer = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                buffer[i] = charactersShortSet[bytebuffer[i] % 64];
            }

            return new(buffer);
        }

    }
}