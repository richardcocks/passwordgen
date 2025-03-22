using System.Security.Cryptography;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;

namespace PasswordGen
{
    [MemoryDiagnoser]
    [GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
    [CategoriesColumn]
    public class Example2
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
        public string StringBuilder(int length)
        {

            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            StringBuilder password = new(length);
            for (int i = 0; i < length; i++)
            {
                password.Append(characters[Random.Shared.Next(characters.Length)]);
            }

            return password.ToString();
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string StringBuilderSecure(int length)
        {

            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

            StringBuilder password = new(length);
            for (int i = 0; i < length; i++)
            {
                password.Append(characters[RandomNumberGenerator.GetInt32(characters.Length)]);
            }

            return password.ToString();
        }


        [BenchmarkCategory("Vulnerable"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string CharArray(int length)
        {

            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = characters[Random.Shared.Next(characters.Length)];
            }

            return new string(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        [Arguments(14)]
        [Arguments(24)]
        [Arguments(32)]
        public string CharArraySecure(int length)
        {
            string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";
            char[] buffer = new char[length];

            for (int i = 0; i < length; i++)
            {
                buffer[i] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }

            return new string(buffer);
        }

    }
}