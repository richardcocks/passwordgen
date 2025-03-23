using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;

namespace PasswordGen
{
    [MemoryDiagnoser]
    public class Example1
    {
        [Params(14, 24, 32)]
        public int Length { get; set; }

        private const string characters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()_+";

        [Benchmark(Baseline = true)]
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

        [Benchmark()]
        public string SecureRandom(int length)
        {
            string password = "";

            for (int i = 0; i < length; i++)
            {
                password += characters[RandomNumberGenerator.GetInt32(characters.Length)];
            }
            return password;
        }

        [Benchmark()]
        public string GeneratePasswordSharedRandom()
        {
            string password = "";

            for (int i = 0; i < Length; i++)
            {
                password += characters[Random.Shared.Next(characters.Length)];
            }
            return password;
        }

    }
}