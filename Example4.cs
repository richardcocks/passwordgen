using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class Example4
    {

        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

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
        public string GetItems()
        {
            char[] buffer = Random.Shared.GetItems<char>(characters, Length);
            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string GetItemsSecure()
        {
            char[] buffer = RandomNumberGenerator.GetItems<char>(characters, Length);
            return new(buffer);
        }

    }
}