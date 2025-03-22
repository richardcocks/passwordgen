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
        public string GetItems(int length)
        {

            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            char[] buffer = Random.Shared.GetItems<char>(characters, length);

            return new(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string GetItemsSecure(int length)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            char[] buffer = RandomNumberGenerator.GetItems<char>(characters, length);

            return new(buffer);
        }

    }
}