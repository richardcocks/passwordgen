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
        public string StringBuilder()
        {

            StringBuilder password = new(Length);
            for (int i = 0; i < Length; i++)
            {
                password.Append(characters[Random.Shared.Next(characters.Length)]);
            }

            return password.ToString();
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string StringBuilderSecure()
        {


            StringBuilder password = new(Length);
            for (int i = 0; i < Length; i++)
            {
                password.Append(characters[RandomNumberGenerator.GetInt32(characters.Length)]);
            }

            return password.ToString();
        }

        [BenchmarkCategory("Vulnerable"), Benchmark()]
        public string CharArray()
        {

            char[] buffer = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                buffer[i] = characters[Random.Shared.Next(characters.Length)];
            }

            return new string(buffer);
        }

        [BenchmarkCategory("Secure"), Benchmark()]
        public string CharArraySecure()
        {
            char[] buffer = new char[Length];

            for (int i = 0; i < Length; i++)
            {
                buffer[i] = characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }

            return new string(buffer);
        }

    }
}